"""Mechanically crop and pack painted FF sprites. Never synthesize poses or mirror.
Source drawings/prompt JSON are retained; output matches SpriteEnemyView hi atlas.
"""
from pathlib import Path
import hashlib, json, statistics, uuid, re
from PIL import Image, ImageDraw
from pack_castle_painted import split, bounds
ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path(__file__).parent/'ff_sources'
OUT=ROOT/'Assets/SpellDuel/Resources/SpriteEnemies'
PREVIEW=ROOT/'Docs/castle_2d_preview/ff_redesign'
IDS=['skeleton','knight','archer','ghost','bat','pumpkin','gargoyle','mage']
GROUPS={'idle':4,'walk':6,'combat':5,'reaction':6}
ACTIONS={'idle':('idle',0,4),'walk':('walk',0,6),'windup':('combat',0,2),'attack':('combat',2,3),'hurt':('reaction',0,2),'die':('reaction',2,4)}
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def height(im):
 b=bounds(im);return b[3]-b[1]
def sources(name):
 groups={}; factors={}; target=176 if name=='bat' else 204
 for g,rows in GROUPS.items():
  halves=[split(Image.open(SOURCE/name/f'{g}_{h}.png').convert('RGBA'),4,rows) for h in range(2)]
  groups[g]=[halves[d//4][r*4+d%4] for r in range(rows) for d in range(8)]
  factors[g]=[target/statistics.median(height(c) for c in hs[:4]) for hs in halves]
 # Independently drawn front-left fixes: fixed reading order, no pixel flip.
 for key,layout,slices in [('left_a',(3,3),[('idle',0,4),('combat',4,5)]),('left_b',(3,4),[('walk',0,6),('reaction',6,6)])]:
  p=SOURCE/name/(key+'.png')
  if not p.exists():continue
  cells=split(Image.open(p).convert('RGBA'),*layout)
  for g,start,count in slices:
   # Same scale for the full animation sequence, including collapsed poses.
   scale=target/statistics.median(height(cells[k]) for k in range(start,min(start+2,start+count)))
   for r in range(count):groups[g][r*8+7]=cells[start+r]
   factors[g].append(scale)
 return groups,factors

def pack():
 PREVIEW.mkdir(parents=True,exist_ok=True)
 report={'baseline':'b62cfc6','generator':'built-in image_gen','cell':256,'directions':list(range(8)),'enemies':{}}
 roster=Image.new('RGB',(8*256,560),(28,25,39));rd=ImageDraw.Draw(roster)
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
   dest=OUT/f'castle_{name}_{action}.png';atlas.save(dest,optimize=True)
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
  old=Image.open(OUT/f'castle_{name}.png').convert('RGBA').crop((0,0,128,128)).resize((256,256),Image.Resampling.NEAREST)
  roster.paste(old,(ni*256,20),old);roster.paste(frames['idle'][0],(ni*256,300),frames['idle'][0]);rd.text((ni*256+8,280),name,fill='white')
  report['enemies'][name]={'files':files,'fallback_sha256':digest(OUT/f'castle_{name}.png'),'scale_safety':safety,'source_edge_contacts':source_edges,'sources':{p.name:digest(p) for p in sorted((SOURCE/name).glob('*.png'))}}
 rd.text((4,3),'BEFORE / CHIBI',fill='white');rd.text((4,288),'AFTER / MANGA FANTASY',fill='white');roster.save(PREVIEW/'before_after.jpg',quality=95)
 (PREVIEW/'atlas_manifest.json').write_text(json.dumps(report,indent=2))
 print('Packed 48 action atlases, 1344 individually drawn animation cells.')
if __name__=='__main__':pack()
