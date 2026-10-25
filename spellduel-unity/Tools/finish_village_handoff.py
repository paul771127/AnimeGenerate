"""Generate Unity metadata, art-only integration manifests and preview indexes."""
import json,re,uuid,hashlib
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFilter
from prepare_village2d import ROOT,ART,DOC,read,write
def textmeta(p):
 m=Path(str(p)+'.meta')
 if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
def main():
 cast=read(ART/'cast.json')
 for id,role in {'clerk':'道具店店員','guide':'副本嚮導','instructor':'訓練教官','bountyofficer':'懸賞官','announcer':'觀戰播報員'}.items():
  if (ROOT/f'Assets/SpellDuel/Resources/Story2D/chars/{id}_normal.png').exists():cast[id]={'role':role,'expressions':['normal','smile','surprised'],'poseVariants':False}
 cast['smith']['role']='鐵匠師父';cast['xiaotie']['role']='小鐵';write(ART/'cast.json',cast)
 borders={'name_plate':[60,26,60,26],'list_frame':[70,50,70,50],'list_row':[70,45,70,45],'list_row_sel':[70,45,70,45],'speech_bubble':[70,95,110,50],'btn_big':[70,50,70,50],'btn_big_press':[70,50,70,50]}
 specs={}
 for key,border in borders.items():
  p=ART/'ui'/(key+'.png')
  if not p.exists():continue
  # Trim export padding once using the visible opaque core, then retain a small alpha margin.
  im=Image.open(p).convert('RGBA');bounds=im.getchannel('A').point(lambda v:255 if v>160 else 0).getbbox()
  if bounds and not (DOC/'ui_prepared.json').exists():
   x,y,r,b=bounds;bounds=(max(0,x-4),max(0,y-4),min(im.width,r+4),min(im.height,b+4));im=im.crop(bounds).resize(im.size,Image.Resampling.LANCZOS)
   if key in ['list_frame','list_row','list_row_sel']:
    # Retain generated edge art; make only the blank stretchable center translucent.
    alpha=im.getchannel('A');d=ImageDraw.Draw(alpha);d.rectangle((border[0],border[3],im.width-border[2],im.height-border[1]),fill=175);im.putalpha(alpha)
   im.save(p,optimize=True)
  m=Path(str(p)+'.meta');s=m.read_text();s=s.replace('textureType: 0','textureType: 8')
  if 'spriteBorder:' not in s:s+='  spriteMode: 1\n  spritePixelsToUnits: 100\n  spritePivot: {x: 0.5, y: 0.5}\n  spriteBorder: {x: %d, y: %d, z: %d, w: %d}\n'%tuple(border)
  m.write_text(s,encoding='utf-8');specs[key]={'size':list(im.size),'borderLTRB':[border[0],border[3],border[2],border[1]],'unityBorderXY ZW':'left,bottom,right,top','safeTextPadding':border,'nineSlice':True}
 if specs:write(ART/'ui/slices.json',specs);write(DOC/'ui_prepared.json',{'trimmed':True})
 for p in ART.rglob('*'):
  if p.suffix in ['.json','.txt']:textmeta(p)
 # Expression overview, posed smiths and small-resident lineup.
 out=DOC/'staff';out.mkdir(exist_ok=True)
 for id,spec in cast.items():
  exprs=spec['expressions'];sheet=Image.new('RGB',(256*len(exprs),430),'#ddd5c5');d=ImageDraw.Draw(sheet)
  for i,e in enumerate(exprs):
   p=ROOT/f'Assets/SpellDuel/Resources/Story2D/chars/{id}_{e}.png'
   if not p.exists():continue
   im=ImageOps.contain(Image.open(p).convert('RGBA'),(250,385));sheet.paste(im,(i*256+(256-im.width)//2,10),im);d.text((i*256+15,400),e,fill='#30281f')
  sheet.save(out/f'{id}_expressions.jpg',quality=88,optimize=True)
 assets=read(DOC/'assets.json')
 for key,v in assets.items():
  p=ROOT/key
  if p.exists():v.update(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
 write(DOC/'assets.json',assets)
if __name__=='__main__':main()
