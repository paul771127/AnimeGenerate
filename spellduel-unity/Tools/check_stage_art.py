"""Asset-only QA and player-view composites; does not modify runtime/gameplay."""
from pathlib import Path
import json,re
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/SpellDuel/Resources/StageArt'
DOC=ROOT/'Docs/stage_art_preview/volcano'
SIZES={'far':(4096,512),'mid':(4096,384),'ground':(1024,1024),'decals':(2048,2048),'props':(2048,2048)}
def sample(im,u,v,repeat_v=False):
 a=np.asarray(im.convert('RGBA'))
 x=(u%1*(im.width-1)).astype(int)
 y=((v%1 if repeat_v else np.clip(v,0,1))*(im.height-1)).astype(int)
 return a[y,x].copy()
def over(bg,fg):
 a=fg[:,:,3:4]/255
 bg[:,:,:3]=np.uint8(bg[:,:,:3]*(1-a)+fg[:,:,:3]*a)
 return bg
def player_view(v):
 w,h=1280,900;f=w/(2*np.tan(np.radians(40)))
 yy,xx=np.mgrid[:h,:w];dx=(xx-w/2)/f;dy=(h*.38-yy)/f;length=np.sqrt(1+dx*dx)
 angle=np.arctan2(dx,1)
 # View from center of arena; low eye level. Heading chosen per variant.
 heading=[.12,.63,.82][v]
 far=Image.open(OUT/f'volcano_{v}_far.png')
 a=sample(far,angle/(2*np.pi)+heading,1-(3+dy/length*32)/22)
 mid=Image.open(OUT/f'volcano_{v}_mid.png')
 ymid=1-(1.5+dy/length*22)/13
 m=sample(mid,angle/(2*np.pi)+heading,ymid)
 m[(ymid<0)|(ymid>1),3]=0;a=over(a,m)
 ground=Image.open(OUT/f'volcano_{v}_ground.png')
 z=1.5/np.maximum(-dy,.0001);x=dx*z
 floor=sample(ground,x/2.5,z/2.5,True)
 floor[(dy>=0)|(z>32),3]=0;a=over(a,floor)
 canvas=Image.fromarray(a)
 atlas=Image.open(OUT/f'volcano_{v}_props.png').convert('RGBA')
 def billboard(cell,x,z,height):
  size=round(f*height/z);obj=cell.convert('RGBa').resize((size,size),Image.Resampling.LANCZOS).convert('RGBA')
  px=round(w/2+x/z*f-size/2);base=round(h*.38+1.5/z*f)
  canvas.alpha_composite(obj,(px,base-round(size*.96)))
 for idx,x,z in [(0,-5.3,8),(3,5.1,8.8),(1,-4.9,6.3),(6,5.6,6.7)]:
  cx=(idx%4)*512;cy=(idx//4)*512
  billboard(atlas.crop((cx,cy,cx+512,cy+512)),x,z,3)
 enemy=Image.open(ROOT/'Assets/SpellDuel/Resources/SpriteEnemies/arena_imp_idle.png').convert('RGBA').crop((0,0,256,256))
 billboard(enemy,0,4.4,1.55)
 d=ImageDraw.Draw(canvas)
 d.rectangle((0,h-43,w,h),fill=(24,21,29,235))
 d.text((22,h-29),f'VOLCANO {v} | '+['LAVA VALLEY','CRATER RIM','ABANDONED MINE'][v]+' | ASSET COMPOSITE - NOT AN IN-GAME CAPTURE',fill='white')
 canvas.convert('RGB').save(DOC/f'volcano_{v}_player_view.jpg',quality=94)
def main():
 errors=[];report={'files':[],'errors':errors}
 for v in range(3):
  for layer,size in SIZES.items():
   p=OUT/f'volcano_{v}_{layer}.png'
   if not p.exists():errors.append(f'Missing {p.name}');continue
   im=Image.open(p);a=np.array(im);meta=p.with_suffix('.png.meta').read_text()
   def check(ok,msg):
    if not ok:errors.append(p.name+': '+msg)
   check(im.size==size,'wrong dimensions')
   check('textureType: 0' in meta and 'sRGBTexture: 1' in meta and 'filterMode: 1' in meta,'import settings')
   maximum=4096 if layer in ('far','mid') else 2048
   check(f'maxTextureSize: {maximum}' in meta,'max texture size')
   wrap=0 if layer in ('far','mid','ground') else 1
   check(all(f'wrap{k}: {wrap}' in meta for k in 'UVW'),'wrap mode')
   if layer in ('far','ground'):check(im.mode=='RGB' or a[:,:,3].min()==255,'not opaque')
   else:check(im.mode=='RGBA' and a[:,:,3].min()==0 and a[:,:,3].max()>240,'missing alpha')
   rec={'name':p.name,'size':im.size,'bytes':p.stat().st_size}
   if layer in ('far','mid','ground'):
    rec['seam_x_max_delta']=int(np.abs(a[:,0].astype(int)-a[:,-1]).max())
    check(rec['seam_x_max_delta']==0,'horizontal seam mismatch')
   if layer=='ground':
    rec['seam_y_max_delta']=int(np.abs(a[0].astype(int)-a[-1]).max())
    check(rec['seam_y_max_delta']==0,'vertical seam mismatch')
    tile=im.resize((384,384));proof=Image.new('RGB',(768,768))
    for x in (0,384):
     for y in (0,384):proof.paste(tile,(x,y))
    proof.save(DOC/f'volcano_{v}_ground_repeat.jpg',quality=92)
   if layer in ('props','decals'):
    rec['cells']=[]
    for k in range(16):
     c=im.crop(((k%4)*512,(k//4)*512,(k%4+1)*512,(k//4+1)*512));b=c.getchannel('A').getbbox()
     check(b is not None,f'empty cell {k}')
     if b:
      check(b[0]>=8 and b[2]<=504 and b[1]>=8 and b[3]<=504,f'cell {k} gutter')
      if layer=='props':check(abs(b[3]-491)<=1,f'cell {k} baseline')
     rec['cells'].append({'index':k,'bounds':b})
   report['files'].append(rec)
  player_view(v)
 report['total_bytes']=sum(x['bytes'] for x in report['files'])
 (DOC/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 print(f"{len(report['files'])} textures, {report['total_bytes']/1048576:.2f} MiB, {len(errors)} errors")
 for e in errors:print(e)
 raise SystemExit(bool(errors))
if __name__=='__main__':main()
