"""Validate obstacle textures and create asset-only scale previews."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/SpellDuel/Resources/StageArt/Obstacles'
DOC=ROOT/'Docs/stage_art_preview/obstacles'
def load(name):return Image.open(OUT/(name+'.png')).convert('RGBA')
def resize(im,size):return im.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA')
def previews(names):
 sheet=Image.new('RGB',(1600,1480),(35,31,38));d=ImageDraw.Draw(sheet)
 for k,name in enumerate(names):
  x=(k%4)*400;y=(k//4)*370;im=load(name)
  im.thumbnail((370,315),Image.Resampling.LANCZOS)
  sheet.paste(im,(x+(400-im.width)//2,y+28+(315-im.height)//2),im)
  d.text((x+12,y+8),name,fill=(240,220,194))
 sheet.save(DOC/'all_assets.jpg',quality=94)
 w,h=1440,1080;f=880.;horizon=370
 yy,xx=np.mgrid[:h,:w];dx=(xx-w/2)/f;dy=(horizon-yy)/f
 far=np.array(Image.open(ROOT/'Assets/SpellDuel/Resources/StageArt/volcano_0_far.png').convert('RGB'))
 u=(np.arctan(dx)/(2*np.pi)+.09)%1;v=np.clip(1-(3+dy/np.sqrt(1+dx*dx)*32)/22,0,1)
 rgb=far[(v*(far.shape[0]-1)).astype(int),(u*(far.shape[1]-1)).astype(int)].copy()
 tile=np.array(Image.open(ROOT/'Assets/SpellDuel/Resources/StageArt/volcano_0_ground.png').convert('RGB'))
 z=1.5/np.maximum(-dy,.00001);x=dx*z
 floor=tile[((z/2.5%1)*1023).astype(int),((x/2.5%1)*1023).astype(int)]
 rgb[dy<0]=floor[dy<0]
 canvas=Image.fromarray(rgb).convert('RGBA')
 def decal(name,cx,cz,width):
  im=np.array(load(name));uvx=(x-cx)/width+.5;uvz=(z-cz)/width+.5
  mask=(uvx>=0)&(uvx<=1)&(uvz>=0)&(uvz<=1)&(dy<0)
  fg=np.zeros((h,w,4),dtype=np.uint8)
  fg[mask]=im[(uvz[mask]*(im.shape[0]-1)).astype(int),(uvx[mask]*(im.shape[1]-1)).astype(int)]
  canvas.alpha_composite(Image.fromarray(fg))
 def upright(im,cx,cz,worldw,worldh,base=.96,add=False):
  pw,ph=round(f*worldw/cz),round(f*worldh/cz)
  obj=resize(im,(pw,ph));px=round(w/2+cx/cz*f-pw/2);py=round(horizon+1.5/cz*f-base*ph)
  if add:
   layer=Image.new('RGBA',canvas.size);layer.paste(obj,(px,py))
   a=np.asarray(canvas).copy();b=np.asarray(layer)
   a[:,:,:3]=np.minimum(255,a[:,:,:3].astype(int)+b[:,:,:3]).astype('uint8')
   canvas.paste(Image.fromarray(a))
  else:canvas.alpha_composite(obj,(px,py))
 decal('exit_circle',2.9,5.6,2.8)
 decal('volcano_vent_warn',-3,7.5,1.4)
 decal('volcano_rock_rubble',3.7,10,2.4)
 upright(load('volcano_side_0'),3.3,12,2,2)
 upright(load('volcano_duck_1'),0,8.5,4,2)
 upright(load('volcano_vent_erupt').crop((256,0,512,1024)),-3,7.5,1,4,1)
 upright(load('exit_pillar'),2.9,5.6,.6,3.2,1,True)
 upright(load('volcano_jump_0'),0,4.7,4,1)
 enemy=Image.open(ROOT/'Assets/SpellDuel/Resources/SpriteEnemies/arena_imp_idle.png').convert('RGBA').crop((0,0,256,256))
 upright(enemy,-2.1,3.8,1.35,1.35,.94)
 draw=ImageDraw.Draw(canvas)
 draw.rectangle((0,h-52,w,h),fill=(25,23,30))
 draw.text((22,h-33),'VOLCANO OBSTACLES | 4m route | eye 1.5m | asset composite, not an in-game capture',fill='white')
 canvas.convert('RGB').save(DOC/'player_view.jpg',quality=95)
 idle,warn=load('volcano_vent_idle'),load('volcano_vent_warn')
 frames=[]
 for im in [idle,warn]*3:
  bg=Image.new('RGBA',im.size,(57,47,48));bg.alpha_composite(im);frames.append(bg.convert('RGB'))
 frames[0].save(DOC/'vent_warning.gif',save_all=True,append_images=frames[1:],duration=350,loop=0)
 erupt=load('volcano_vent_erupt');frames=[]
 for k in range(4):
  bg=Image.new('RGBA',(256,1024),(35,31,38));bg.alpha_composite(erupt.crop((k*256,0,(k+1)*256,1024)));frames.append(bg.convert('RGB'))
 frames[0].save(DOC/'vent_eruption.gif',save_all=True,append_images=frames[1:],duration=[140,190,190,220],loop=0)
def main():
 specs={**{f'volcano_{t}_{i}':s for t,s in [('jump',(2048,512)),('duck',(2048,1024)),('side',(1024,1024))] for i in range(3)},
 'volcano_vent_idle':(512,512),'volcano_vent_warn':(512,512),'volcano_vent_erupt':(1024,1024),'volcano_rock_fall':(512,512),'volcano_rock_rubble':(512,512),'exit_circle':(1024,1024),'exit_pillar':(256,1024)}
 errors=[];report={'assets':[],'errors':errors}
 for name,size in specs.items():
  p=OUT/(name+'.png');im=Image.open(p);a=np.asarray(im)
  def check(ok,msg):
   if not ok:errors.append(name+': '+msg)
  check(im.size==size,'dimensions')
  meta=p.with_suffix('.png.meta').read_text()
  check(all(v in meta for v in ['textureType: 0','sRGBTexture: 1','alphaIsTransparency: 1','filterMode: 1','wrapU: 1','wrapV: 1','maxTextureSize: 2048']),'import settings')
  rec={'name':name,'size':im.size,'bytes':p.stat().st_size}
  if name!='exit_pillar':
   check(im.mode=='RGBA' and a[:,:,3].min()==0,'transparent background')
   box=im.getchannel('A').getbbox();rec['bounds']=box
   if '_jump_' in name:
    check(box[1]>=512*.55,'too tall for jump')
    check(abs(box[3]-492)<=2,'ground baseline')
   if '_duck_' in name:
    center=a[:,int(size[0]*.35):int(size[0]*.65),3]
    underside=[int(np.flatnonzero(center[:,i]>100)[-1])+1 for i in range(center.shape[1])]
    rec['beam_underside_median']=float(np.median(underside))
    check(abs(rec['beam_underside_median']-563)<=4,'beam underside not at 55%')
    check(float((center[590:950]>16).mean())<.002,'opening not clear')
   if name=='volcano_vent_erupt':
    rec['frames']=[]
    for k in range(4):
     b=im.crop((k*256,0,(k+1)*256,1024)).getchannel('A').getbbox();rec['frames'].append(b)
     check(b is not None and b[3]==1024,f'frame {k} baseline')
    heights=[1024-b[1] for b in rec['frames']]
    check(min(heights[1:3])>max(heights[0],heights[3])*1.25,'eruption phase heights')
  else:
   check(im.mode=='RGB','additive pillar must be opaque black RGB')
   check(float(a[:50].mean())<5,'pillar top not dark')
  report['assets'].append(rec)
 ia=np.array(load('volcano_vent_idle'))[:,:,3]>64;wa=np.array(load('volcano_vent_warn'))[:,:,3]>64
 report['vent_alpha_iou']=float((ia&wa).sum()/(ia|wa).sum())
 if report['vent_alpha_iou']<.98:errors.append('vent state footprints shifted')
 report['total_bytes']=sum(r['bytes'] for r in report['assets'])
 DOC.mkdir(parents=True,exist_ok=True);previews(list(specs))
 (DOC/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 print(f"16 assets; {report['total_bytes']/1048576:.2f} MiB; vent IoU {report['vent_alpha_iou']:.4f}; {len(errors)} errors")
 for e in errors:print(e)
 raise SystemExit(bool(errors))
if __name__=='__main__':main()
