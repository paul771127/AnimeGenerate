#!/usr/bin/env python3
"""Package image_gen masters/face edits with locked pixels; no image generation/API calls."""
import argparse,hashlib,json,re,uuid,struct,zlib
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFilter,ImageFont,ImageOps
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/SpellDuel/Resources/Story2D'
DOC=ROOT/'Docs/story2d_stage2'
EXPRS=['normal','smile','angry','sad','surprised','serious']

def read(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def write(path,obj):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
def meta(path,alpha):
    dest=path.with_suffix(path.suffix+'.meta')
    if dest.exists():return
    template=(ART/'chars/erin_normal.png.meta').read_text(encoding='utf-8')
    template=re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template)
    if not alpha:template=template.replace('alphaSource: 1','alphaSource: 0').replace('alphaIsTransparency: 1','alphaIsTransparency: 0')
    dest.write_text(template,encoding='utf-8')
def save_rgba(image,path):
    # PNG filter 0 compresses flat palette-derived RGBA art better than photographic adaptive filters.
    a=np.array(image.convert('RGBA'));h,w,_=a.shape
    raw=b''.join(b'\0'+row.tobytes() for row in a)
    def chunk(tag,data):return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data)&0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))

def pack_backgrounds(config,sources):
    report=read(DOC/'manifest.json') if (DOC/'manifest.json').exists() else {'characters':{},'backgrounds':{}}
    prompts=read(DOC/'prompts.json') if (DOC/'prompts.json').exists() else {}
    for identity,spec in config.items():
        if identity not in sources:continue
        image=ImageOps.fit(Image.open(sources[identity]['path']).convert('RGB'),(1080,1920),method=Image.Resampling.LANCZOS)
        dest=ART/'bg'/f'{identity}.jpg';dest.parent.mkdir(parents=True,exist_ok=True);image.save(dest,quality=82,optimize=True,subsampling=2)
        meta_path=dest.with_suffix('.jpg.meta')
        if not meta_path.exists():
            template=(ART/'bg/village.jpg.meta').read_text(encoding='utf-8');meta_path.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template),encoding='utf-8')
        report['backgrounds'][identity]={'size':[1080,1920],'bytes':dest.stat().st_size,'referenceFrame':spec.get('frame'),'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()};prompts['bg/'+identity]=sources[identity]['prompt']
        print(identity,dest.stat().st_size,'bytes',flush=True)
    write(DOC/'manifest.json',report);write(DOC/'prompts.json',prompts)
def pack(identity,cfg,sources,size=768,colors=256):
    spec=cfg[identity];expressions=['normal','angry'] if spec.get('boss') else EXPRS
    if any(identity+'_'+e not in sources for e in expressions):return False
    base=Image.open(sources[identity+'_normal']['path']).convert('RGBA');box=spec['box'];mask=Image.new('L',base.size);draw=ImageDraw.Draw(mask)
    if 'polygon' in spec:draw.polygon(spec['polygon'],fill=255)
    else:draw.rounded_rectangle(tuple(box),radius=min(box[2]-box[0],box[3]-box[1])*.16,fill=255)
    mask=Image.open(spec['existingMask']).convert('L') if spec.get('existingMask') else mask.filter(ImageFilter.GaussianBlur(1.4));width=size;height=size*3//2;scale=size/base.width;dx=0;dy=round(height*.06-spec.get('headTop',92)*scale) if not spec.get('boss') else 0
    def frame(im):
        out=Image.new('RGBA',(width,height));out.paste(im.resize((width,height),Image.Resampling.LANCZOS),(dx,dy));return out
    images=[]
    for expr in expressions:
        edit=Image.open(sources[identity+'_'+expr]['path']).convert('RGBA')
        if edit.size!=base.size:edit=edit.resize(base.size,Image.Resampling.LANCZOS)
        images.append(frame(base if expr=='normal' else edit if spec.get('lockedEdits') else Image.composite(edit,base,mask)))
    # One color palette for every expression; deterministic quantization cannot alter clothing between variants.
    training=Image.new('RGB',(768,1152));clean=np.array(images[0]);clean[clean[:,:,3]==0,:3]=0;thumb=Image.fromarray(clean);thumb.thumbnail((384,576));training.paste(thumb.convert('RGB'),(0,0))
    fb=(round(box[0]*scale),round(box[1]*scale+dy),round(box[2]*scale),round(box[3]*scale+dy))
    for i,im in enumerate(images):training.paste(im.crop(fb).convert('RGB').resize((192,192)),(384+(i%2)*192,(i//2)*192))
    palette=training.quantize(colors=colors,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE)
    body_palette=images[0].convert('RGB').quantize(colors=64,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE)
    fm=Image.new('L',(width,height));fm.paste(mask.resize((width,height),Image.Resampling.LANCZOS),(dx,dy))
    facial_palette_mask=fm.point(lambda p:255 if p else 0)
    alpha=np.array(images[0].getchannel('A'));alpha[alpha>=248]=255;alpha[alpha<2]=0
    if identity in ['lavashade','treeshade','tideshade']:alpha=np.rint(alpha*.65).astype('uint8')
    elif identity=='mio':alpha=np.rint(alpha*.82).astype('uint8')
    final=[];files=[]
    for expr,im in zip(expressions,images):
        face_rgb=im.convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE).convert('RGB')
        body_rgb=im.convert('RGB').quantize(palette=body_palette,dither=Image.Dither.NONE).convert('RGB')
        rgb=Image.composite(face_rgb,body_rgb,facial_palette_mask);rgba=rgb.convert('RGBA');rgba.putalpha(Image.fromarray(alpha));clean=np.array(rgba);clean[alpha==0,:3]=0;rgba=Image.fromarray(clean);dest=ART/'chars'/f'{identity}_{expr}.png';dest.parent.mkdir(parents=True,exist_ok=True);save_rgba(rgba,dest);meta(dest,True);final.append(rgba);files.append({'expression':expr,'bytes':dest.stat().st_size,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()})
    fm=Image.new('L',(width,height));fm.paste(mask.resize((width,height),Image.Resampling.LANCZOS),(dx,dy));allowed=np.array(fm.filter(ImageFilter.MaxFilter(9)))>0;master=np.array(final[0]);changes=[]
    for expr,im in zip(expressions,final):
        diff=np.any(np.array(im)!=master,axis=2);assert not (diff&~allowed).any(),(identity,expr,'nonface change');assert expr=='normal' or diff.sum()>15,(identity,expr,'missing expression');changes.append({'expression':expr,'changedPixels':int(diff.sum())})
    maskPath=DOC/'masks'/f'{identity}.png';maskPath.parent.mkdir(parents=True,exist_ok=True);fm.save(maskPath,optimize=True)
    before=np.array(images[0])[:,:,:3].astype('float32');after=master[:,:,:3].astype('float32');opaque=alpha>120;error=float(np.abs(before-after)[opaque].mean());assert error<8,(identity,'palette error',error)
    report=read(DOC/'manifest.json') if (DOC/'manifest.json').exists() else {'characters':{},'backgrounds':{}}
    report['characters'][identity]={'size':[width,height],'faceBoxSource':box,'faceMask':f'Docs/story2d_stage2/masks/{identity}.png','headTop':round(spec.get('headTop',92)*scale+dy),'sharedFacePaletteColors':colors,'sharedBodyPaletteColors':64,'meanRgbError':error,'outsideFacePixelsIdentical':True,'changes':changes,'files':files};write(DOC/'manifest.json',report)
    prompts=read(DOC/'prompts.json') if (DOC/'prompts.json').exists() else {};prompts.update({identity+'_'+e:sources[identity+'_'+e]['prompt'] for e in expressions});write(DOC/'prompts.json',prompts)
    fontPath='C:/Windows/Fonts/msjh.ttc';font=ImageFont.truetype(fontPath,18);sheet=Image.new('RGB',(200*(len(expressions)+1),370),'#1c2636');d=ImageDraw.Draw(sheet)
    for i,im in enumerate(final):
        crop=im.crop((max(0,fb[0]-25),max(0,fb[1]-30),min(width,fb[2]+25),min(height,fb[3]+35))).resize((190,250));tile=Image.new('RGBA',crop.size,'#dce5ec');tile.alpha_composite(crop);sheet.paste(tile.convert('RGB'),((i+1)*200+5,40));d.text(((i+1)*200+6,300),expressions[i],font=font,fill='#eee3c8')
    full=final[0].resize((200,300));tile=Image.new('RGBA',full.size,'#dce5ec');tile.alpha_composite(full);sheet.paste(tile.convert('RGB'),(0,40));d.text((8,8),identity,font=font,fill='#f2d28b');d.text((8,342),'image_gen 母圖與臉部編輯；非臉部像素相同。',font=font,fill='#c4d2de');sheet.save(DOC/f'{identity}_expressions.png',optimize=True)
    print(identity,len(expressions),'sprites',sum(x['bytes'] for x in files),'bytes; RGB error',round(error,3),flush=True);return True

def main():
    p=argparse.ArgumentParser();p.add_argument('--sources',required=True);p.add_argument('--config',required=True);p.add_argument('--backgrounds');p.add_argument('--ids',nargs='+');p.add_argument('--size',type=int,default=768,choices=[768,640]);p.add_argument('--colors',type=int,default=256);a=p.parse_args();cfg=read(a.config);sources=read(a.sources);DOC.mkdir(parents=True,exist_ok=True)
    for identity in a.ids or cfg:pack(identity,cfg,sources,a.size,a.colors)
    if a.backgrounds:pack_backgrounds(read(a.backgrounds),sources)
if __name__=='__main__':main()
