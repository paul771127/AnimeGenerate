"""Export chibi frames with one class-wide height and a centered lower-body anchor."""
import hashlib
import numpy as np
from PIL import Image
from prepare_worldmap import ART,DOC,sources,read,write,record
def main():
 manifest=read(DOC/'assets.json');report={}
 for cls in ['novice','swordsman','archer','mage','assassin','priest']:
  data=sources(f'worldmap_hero_{cls}_*.json');prepared={};height=200
  for key,s in data.items():
   im=Image.open(s['path']).convert('RGBA');bbox=im.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox();im=im.crop(bbox);a=np.array(im.getchannel('A'))[round(im.height*.85):].astype(float);cx=float((a*np.arange(im.width)[None,:]).sum()/a.sum());height=min(height,int(91*im.height/max(cx,im.width-cx)));prepared[key]=(im,cx,s)
  report[cls]={'height':height,'anchor':[96,252],'method':'bottom 15% alpha centroid; common stature across six frames; retain whole weapon and cape'}
  for key,(im,cx,s) in prepared.items():
   scale=height/im.height;small=im.resize((round(im.width*scale),height),Image.Resampling.LANCZOS);out=Image.new('RGBA',(192,256));out.alpha_composite(small,(96-round(cx*scale),252-height));p=ART/(key+'.png');out.save(p,optimize=True);record(p,s,manifest)
 write(DOC/'hero_anchors.json',report);write(DOC/'assets.json',manifest)
if __name__=='__main__':main()
