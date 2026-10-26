"""Package image_gen dialogue masters + face edits using the established locked-pixel pipeline."""
import argparse,json,re,uuid,hashlib
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/SpellDuel/Resources'
DOC=ROOT/'Docs/pending_preview'
CONFIG={
'alchemist_tanyi':(480,260,700,440),'alchemist_qingluo':(480,230,720,450),'alchemist_xi':(480,240,730,450),
'alchemist_sailin':(470,180,720,410),'alchemist_tuoling':(480,210,750,450),'crafter_laochui':(430,180,710,495),
'lingyin':(480,275,735,490),'ark':(440,180,730,425),'xiaomai':(450,300,745,550),
'oldsmith_shade':(420,220,690,495),'gearmerchant':(545,290,765,475),'clockwork':(555,310,770,480),'heartcore':(550,745,790,990),
}
def meta(dest,template):
 p=Path(str(dest)+'.meta')
 if not p.exists():p.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template.read_text(encoding='utf-8')),encoding='utf-8')
def pack(sources):
 DOC.mkdir(parents=True,exist_ok=True);(DOC/'masks').mkdir(exist_ok=True)
 report=json.loads((DOC/'characters.json').read_text(encoding='utf-8')) if (DOC/'characters.json').exists() else {}
 prompts=json.loads((DOC/'prompts.json').read_text(encoding='utf-8')) if (DOC/'prompts.json').exists() else {}
 for identity,box in CONFIG.items():
  exprs=['normal','angry','surprised'] if identity in ('ark','clockwork') else ['normal','serious','surprised'] if identity in ('lingyin','xiaomai','oldsmith_shade','gearmerchant','heartcore') else ['normal','smile','serious']
  if not all(identity+'_'+e in sources for e in exprs):continue
  master=Image.open(sources[identity+'_normal']['path']).convert('RGBA');mask=Image.new('L',master.size);ImageDraw.Draw(mask).rounded_rectangle(box,radius=16,fill=255);mask=mask.filter(ImageFilter.GaussianBlur(1.4))
  a=np.array(master.getchannel('A'));head_rows=np.where((a[:700,int(master.width*.25):int(master.width*.8)]>=245).sum(axis=1)>5)[0];top=int(head_rows[0]) if len(head_rows) else 92
  scale=768/master.width;dy=round(1152*.06-top*scale)
  def frame(im):
   canvas=Image.new(im.mode,(768,1152));canvas.paste(im.resize((768,1152),Image.Resampling.LANCZOS),(0,dy));return canvas
  m=frame(mask);allowed=np.array(m.filter(ImageFilter.MaxFilter(9)))>0
  imgs=[]
  for e in exprs:
   edit=Image.open(sources[identity+'_'+e]['path']).convert('RGBA').resize(master.size,Image.Resampling.LANCZOS)
   imgs.append(frame(master if e=='normal' else Image.composite(edit,master,mask)))
  train=Image.new('RGB',(768,1152));thumb=imgs[0].copy();thumb.thumbnail((384,576));train.paste(thumb.convert('RGB'),(0,0))
  cropbox=(round(box[0]*scale),round(box[1]*scale+dy),round(box[2]*scale),round(box[3]*scale+dy))
  for i,im in enumerate(imgs):train.paste(im.crop(cropbox).convert('RGB').resize((192,192)),(384+(i%2)*192,(i//2)*192))
  palette=train.quantize(colors=256,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE)
  alpha=np.array(imgs[0].getchannel('A'));alpha[alpha>=248]=255;alpha[alpha<2]=0
  if identity=='oldsmith_shade':alpha=np.rint(alpha*.65).astype('uint8')
  records=[];base=None
  for e,im in zip(exprs,imgs):
   out=im.convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE).convert('RGBA');out.putalpha(Image.fromarray(alpha));pixels=np.array(out);pixels[alpha==0,:3]=0
   if base is None:base=pixels.copy()
   diff=np.any(pixels!=base,axis=2);assert not (diff&~allowed).any(),(identity,e,'body drift')
   assert e=='normal' or diff.sum()>15,(identity,e,'identical expression')
   dest=ART/'Story2D/chars'/f'{identity}_{e}.png';Image.fromarray(pixels).save(dest,optimize=True);meta(dest,ART/'Story2D/chars/erin_normal.png.meta')
   records.append({'expression':e,'changedPixels':int(diff.sum()),'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()});prompts[identity+'_'+e]=sources[identity+'_'+e]['prompt']
  m.save(DOC/'masks'/f'{identity}.png',optimize=True)
  report[identity]={'expressions':exprs,'size':[768,1152],'bodyPixelsLocked':True,'faceBoxSource':box,'topShift':dy,'files':records}
 (DOC/'characters.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(DOC/'prompts.json').write_text(json.dumps(prompts,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('Packed',len(report),'characters with locked body pixels.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('sources');args=p.parse_args();pack(json.loads(Path(args.sources).read_text(encoding='utf-8-sig')))
