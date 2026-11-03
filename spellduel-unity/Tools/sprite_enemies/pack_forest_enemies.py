"""Mechanically crop and pack painted FF sprites. Never synthesize poses or mirror.
Source drawings/prompt JSON are retained; output matches SpriteEnemyView hi atlas.
"""
from pathlib import Path
import hashlib, json, statistics, uuid, re, sys
from PIL import Image, ImageDraw
from pack_castle_painted import split, bounds
ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path(__file__).parent/'forest_boss_sources'
OUT=ROOT/'Assets/SpellDuel/Resources/SpriteEnemies'
PREVIEW=ROOT/'Docs/forest_2d_preview'
IDS=['forest_'+a[0] for a in json.loads((SOURCE/'forest_actors.json').read_text(encoding='utf-8-sig'))]
IDS=[n for n in IDS if all((SOURCE/n/(g+'.png')).exists() for g in ('idle','walk','combat','reaction'))]
GROUPS={'idle':4,'walk':6,'combat':5,'reaction':6}
ACTIONS={'idle':('idle',0,4),'walk':('walk',0,6),'windup':('combat',0,2),'attack':('combat',2,3),'hurt':('reaction',0,2),'die':('reaction',2,4)}
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def height(im):
 b=bounds(im);return b[3]-b[1]
def sources(name):
 groups={}; factors={}
 for g,rows in GROUPS.items():
  groups[g]=split(Image.open(SOURCE/name/(g+'.png')).convert('RGBA'),8,rows)
  scale=204/statistics.median(height(c) for c in groups[g][:8])
  factors[g]=[scale,scale]
 return groups,factors

def pack():
 PREVIEW.mkdir(parents=True,exist_ok=True)
 report={'baseline':'cd2b8b4','generator':'built-in image_gen','cell':256,'directions':list(range(8)),'enemies':{}}
 roster=Image.new('RGB',(max(1,len(IDS))*256,560),(28,25,39));rd=ImageDraw.Draw(roster)
 template=(OUT/'castle_skeleton.png.meta').read_text()
 for ni,name in enumerate(IDS):
  groups,factors=sources(name)
  def factor(g,k):return factors[g][2] if k%8==7 and len(factors[g])==3 else factors[g][(k%8)//4]
  safety=min([1]+[min(232/((bounds(c)[2]-bounds(c)[0])*factor(g,k)),(212 if name in ('bat','ghost') else 228)/(height(c)*factor(g,k))) for g,cells in groups.items() for k,c in enumerate(cells)])
  frames={}; source_edges=[]
  for g,cells in groups.items():
   frames[g]=[]
   for k,im in enumerate(cells):
    b=bounds(im);scale=factor(g,k)*safety
    if b[0]==0 or b[1]==0 or b[2]==im.width or b[3]==im.height:source_edges.append([g,k])
    crop=im.crop((max(0,b[0]-1),max(0,b[1]-1),min(im.width,b[2]+1),min(im.height,b[3]+1)))
    size=(max(1,round(crop.width*scale)),max(1,round(crop.height*scale)))
    crop=crop.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA')
    cell=Image.new('RGBA',(256,256));bottom=241
    if name in ('bat','ghost') and not (g=='reaction' and k//8>=3):bottom-=12
    cell.alpha_composite(crop,((256-size[0])//2,bottom-size[1]));frames[g].append(cell)
  files={}
  for action,(g,start,count) in ACTIONS.items():
   atlas=Image.new('RGBA',(2048,count*256))
   for r in range(count):
    for d in range(8):atlas.paste(frames[g][(start+r)*8+d],(d*256,r*256))
   dest=OUT/f'{name}_{action}.png';atlas.save(dest,optimize=True)
   meta=Path(str(dest)+'.meta')
   if not meta.exists():meta.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template))
   files[action]={'sha256':digest(dest),'bytes':dest.stat().st_size,'rows':count}
  contact=Image.new('RGB',(2048,280),(28,25,39));draw=ImageDraw.Draw(contact)
  for d in range(8):
   contact.paste(frames['idle'][d],(d*256,24),frames['idle'][d]);draw.text((d*256+8,6),f'{name} / {d}',fill='white')
  contact.save(PREVIEW/f'{name}_directions.jpg',quality=94)
  allframes=Image.new('RGB',(2048,21*270),(28,25,39));ad=ImageDraw.Draw(allframes);y=0
  for g,rows in GROUPS.items():
   for r in range(rows):
    ad.text((4,y+2),f'{g} {r}',fill='white')
    for d in range(8):allframes.paste(frames[g][r*8+d],(d*256,y+14),frames[g][r*8+d])
    y+=270
  allframes.save(PREVIEW/f'{name}_all_frames.jpg',quality=88)
  for action,g,rows in [('walk','walk',6),('attack','combat',5)]:
   gif=[]
   for r in range(rows):
    f=Image.new('RGB',(1536,208),(28,25,39));fd=ImageDraw.Draw(f);fd.text((6,4),f'{name} {action} {r}',fill='white')
    for d in range(8):
     c=frames[g][r*8+d].resize((192,192),Image.Resampling.LANCZOS);f.paste(c,(d*192,16),c)
    gif.append(f)
   gif[0].save(PREVIEW/f'{name}_{action}.gif',save_all=True,append_images=gif[1:],duration=150 if action=='walk' else [170,380,130,130,250],loop=0)
  roster.paste(frames['idle'][0],(ni*256,300),frames['idle'][0]);rd.text((ni*256+8,280),name,fill='white')
  report['enemies'][name]={'files':files,'scale_safety':safety,'source_edge_contacts':source_edges,'sources':{p.name:digest(p) for p in sorted((SOURCE/name).glob('*.png'))}}
 rd.text((4,3),'FOREST ENEMY ROSTER',fill='white');rd.text((4,288),'MANGA FANTASY',fill='white');roster.crop((0,280,max(1,len(IDS))*256,560)).save(PREVIEW/'roster.jpg',quality=95)
 grid=Image.new('RGB',(6*256,((len(IDS)+5)//6)*280),(28,25,39))
 for i in range(len(IDS)):
  grid.paste(roster.crop((i*256,280,(i+1)*256,560)),((i%6)*256,(i//6)*280))
 grid.save(PREVIEW/'roster_grid.jpg',quality=95)
 (PREVIEW/'atlas_manifest.json').write_text(json.dumps(report,indent=2))
 print('Packed',len(IDS),'forest enemies,',len(IDS)*6,'atlases,',len(IDS)*168,'cells')
if __name__=='__main__':pack()
