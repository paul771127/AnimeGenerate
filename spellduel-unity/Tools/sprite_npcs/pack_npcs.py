"""Crop and pack painted NPC frames; no synthesized poses or mirrored views."""
from pathlib import Path
import sys, json, statistics, hashlib, re, uuid
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/sprite_enemies'))
from pack_castle_painted import split, bounds
SOURCE=Path(__file__).parent/'sources'
OUT=ROOT/'Assets/SpellDuel/Resources/SpriteNpcs'
PREVIEW=ROOT/'Docs/npc_2d_preview'
GROUPS={'idle':4,'walk':6,'gesture':4}
IDS='erin xiaotie laoyan cui feng haido lingyin ark xiaomai oldsmith_shade gearmerchant foreman ayan bellsmith jungle_elder jungle_hunter jungle_explorer jungle_outpost jungle_herbalist jungle_guardian jungle_spirit traveler'.split()
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def pack():
 OUT.mkdir(parents=True,exist_ok=True);PREVIEW.mkdir(parents=True,exist_ok=True)
 template=(ROOT/'Assets/SpellDuel/Resources/SpriteEnemies/castle_skeleton.png.meta').read_text()
 report={'baseline':'100d3bc','proportions':'2.5 heads (user override)','generator':'built-in image_gen','npcs':{}}
 for name in IDS:
  if not all((SOURCE/name/(g+'.png')).exists() for g in GROUPS):continue
  frames={};files={};edges=[]
  for g,rows in GROUPS.items():
   cells=split(Image.open(SOURCE/name/(g+'.png')).convert('RGBA'),8,rows)
   if any(bounds(c) is None for c in cells):raise ValueError((name,g,'empty source cell'))
   target=154 if name in ('xiaotie','xiaomai') else 179
   scale=target/statistics.median(bounds(c)[3]-bounds(c)[1] for c in cells[:8])
   scale=min(scale,*[min(230/(bounds(c)[2]-bounds(c)[0]),226/(bounds(c)[3]-bounds(c)[1])) for c in cells])
   frames[g]=[];atlas=Image.new('RGBA',(2048,rows*256))
   for k,c in enumerate(cells):
    b=bounds(c)
    # Ignore faint generated matte residue only for edge-contact diagnosis;
    # retain the source alpha and alpha-16 crop measurement unchanged.
    solid=c.getchannel('A').point(lambda a:255 if a>=64 else 0).getbbox()
    if solid and (solid[0]==0 or solid[1]==0 or solid[2]==c.width or solid[3]==c.height):edges.append([g,k])
    crop=c.crop((max(0,b[0]-1),max(0,b[1]-1),min(c.width,b[2]+1),min(c.height,b[3]+1)))
    crop=crop.convert('RGBa').resize((round(crop.width*scale),round(crop.height*scale)),Image.Resampling.LANCZOS).convert('RGBA')
    f=Image.new('RGBA',(256,256));bottom=229 if name in ('jungle_spirit','oldsmith_shade') else 241
    f.alpha_composite(crop,((256-crop.width)//2,bottom-crop.height));frames[g].append(f)
    atlas.paste(f,((k%8)*256,(k//8)*256))
   dest=OUT/f'{name}_{g}.png';atlas.save(dest,optimize=True)
   meta=Path(str(dest)+'.meta')
   if not meta.exists():meta.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template))
   files[g]={'sha256':digest(dest),'rows':rows}
  contact=Image.new('RGB',(2048,280),(36,37,48));draw=ImageDraw.Draw(contact)
  for d in range(8):
   contact.paste(frames['idle'][d],(d*256,24),frames['idle'][d]);draw.text((d*256+8,6),f'{name} / {d}',fill='white')
  contact.save(PREVIEW/f'{name}_directions.jpg',quality=94)
  for g in ('walk','gesture'):
   gif=[]
   for r in range(GROUPS[g]):
    f=Image.new('RGB',(2048,280),(36,37,48));ImageDraw.Draw(f).text((8,6),f'{name} {g} {r}',fill='white')
    for d in range(8):f.paste(frames[g][r*8+d],(d*256,24),frames[g][r*8+d])
    gif.append(f)
   gif[0].save(PREVIEW/f'{name}_{g}.gif',save_all=True,append_images=gif[1:],duration=180 if g=='walk' else [350,220,550,350],loop=0)
  ref=ROOT/f'Assets/SpellDuel/Resources/Story2D/chars/{name}_normal.png'
  compare=Image.new('RGB',(768,540),(36,37,48));cd=ImageDraw.Draw(compare)
  if ref.exists():
   im=Image.open(ref).convert('RGBA');im.thumbnail((350,500));compare.paste(im,((384-im.width)//2,30),im)
  cd.text((12,8),'Story2D reference' if ref.exists() else 'Original NPC design',fill='white')
  cd.text((396,8),'2.5 heads / field NPC',fill='white')
  im=frames['idle'][0].resize((512,512),Image.Resampling.LANCZOS);compare.paste(im,(260,25),im)
  compare.save(PREVIEW/f'{name}_comparison.jpg',quality=94)
  report['npcs'][name]={'files':files,'source_edge_contacts':edges,'sources':{g:digest(SOURCE/name/(g+'.png')) for g in GROUPS}}
 (PREVIEW/'atlas_manifest.json').write_text(json.dumps(report,indent=2))
 print('Packed',len(report['npcs']),'NPCs')
if __name__=='__main__':pack()
