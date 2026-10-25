"""Validate world-map assets and current/future route graph independently of gameplay."""
import argparse,json,re,hashlib
from PIL import Image
from prepare_worldmap import ART,DOC,ROOT,read,write
CLASSES=['novice','swordsman','archer','mage','assassin','priest']
def main():
 p=argparse.ArgumentParser();p.add_argument('--partial',action='store_true');partial=p.parse_args().partial;pending=[];checked=[]
 def require(path):
  if path.exists():return True
  if partial:pending.append(path.relative_to(ROOT).as_posix());return False
  raise AssertionError(('missing',path))
 def image(p,size,alpha=False):
  if not require(p):return
  im=Image.open(p);assert im.size==size,(p,im.size,size)
  if alpha:assert im.mode=='RGBA' and im.getchannel('A').getextrema()[0]==0 and im.getchannel('A').getextrema()[1]>200,(p,'alpha')
  else:assert im.mode=='RGB',p
  m=p.with_suffix(p.suffix+'.meta');assert m.exists(),m;t=m.read_text();assert 'nPOTScale: 0' in t and 'enableMipMap: 0' in t,p
  if alpha:assert 'alphaSource: 1' in t,p
  if size==(2160,1920):assert 'maxTextureSize: 4096' in t,p
  checked.append(p.relative_to(ROOT).as_posix())
 spec=read(ART/'nodes.json')
 for r in range(1,6):
  if not partial or r in spec['availableRegions']:image(ART/f'region_{r}.jpg',(2160,1920))
 for key in ['locked','open','clear','boss','boss_clear','town','tower']:image(ART/'ui'/f'node_{key}.png',(128,128) if key in ['locked','open','clear'] else (160,160),True)
 image(ART/'ui/label_plate.png',(512,128),True)
 t=(ART/'ui/label_plate.png.meta').read_text();assert 'textureType: 8' in t and 'spriteBorder: {x: 64, y: 32, z: 64, w: 32}' in t,'label 9slice'
 for i in range(3):image(ART/'ui'/f'fog_{i}.png',(1024,1024),True)
 for i in range(4):image(ART/'ui'/f'stars_{i}.png',(96,32),True)
 for c in CLASSES:
  for pose,count in [('side',4),('idle',2)]:
   hashes=[]
   for i in range(count):
    path=ART/f'hero_{c}_{pose}_{i}.png';image(path,(192,256),True)
    if path.exists():
     im=Image.open(path);box=im.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox();assert box and box[3]<=253 and box[3]>=246,(path,'feet baseline');hashes.append(hashlib.sha256(im.tobytes()).hexdigest())
   assert len(hashes)==len(set(hashes)),(c,pose,'duplicated frames')
 spec=read(ART/'nodes.json');nodes=spec['nodes'];paths=spec['paths'];regions=spec['availableRegions']
 assert spec['coordinateSystem']=={'width':10800,'height':1920,'origin':'top-left','regionWidth':2160}
 for r in range(1,6):
  if r not in regions:
   if partial:pending.append('region '+str(r)+' nodes');continue
   raise AssertionError(('missing region nodes',r))
  for k in [f'town:{r-1}']+[f'{r}-{i}' for i in range(1,6)]:assert k in nodes,k
 for key,n in nodes.items():
  assert 0<=n['x']<10800 and 0<=n['y']<1920 and 0<=n['label_x']<10800 and 0<=n['label_y']<1920,(key,n)
  assert (n['region']-1)*2160<=n['x']<n['region']*2160,(key,'wrong region')
  if key.endswith('-5'):assert n['reserved'] and not n['visibleByDefault'],key
 graph={k:[] for k in nodes};edges=set()
 for path in paths:
  a,b=path['from'],path['to'];assert a in nodes and b in nodes,(a,b);points=path['points'];assert 3<=len(points)<=8,(a,b)
  assert points[0]==[nodes[a]['x'],nodes[a]['y']] and points[-1]==[nodes[b]['x'],nodes[b]['y']],(a,b,'endpoint')
  assert all(0<=x<10800 and 0<=y<1920 for x,y in points),(a,b,'point bounds')
  marker=(a,b,path['layout']);assert marker not in edges,marker;edges.add(marker)
  if path['enabled']:
   assert not nodes[a]['reserved'] and not nodes[b]['reserved'],(a,b,'reserved active');graph[a].append(b)
 seen={'town:0'};todo=['town:0']
 while todo:
  for k in graph[todo.pop()]:
   if k not in seen:seen.add(k);todo.append(k)
 assert {k for k,n in nodes.items() if not n['reserved']}<=seen,'current route disconnected'
 if 2 in regions:assert ('2-4','tower','current') in edges and nodes['tower']['y']>nodes['2-4']['y'],'tower branch'
 guids=[]
 for m in ART.rglob('*.meta'):
  match=re.search(r'^guid: ([0-9a-f]{32})$',m.read_text(),re.M);assert match,m;guids.append(match[1])
 assert len(guids)==len(set(guids)),'duplicate GUID'
 if not partial:
  for name in ['01_start','02_tower_branch','03_select_1_3']:
   path=DOC/'mockups'/(name+'.jpg');assert path.exists() and Image.open(path).size==(1080,1920),path
   layout=read(DOC/'mockups'/(name+'.json'));assert not any(nodes[k]['reserved'] for k in layout['visibleNodes']),name
   texts={e.get('text') for e in layout['elements']};assert {'🗺 大地圖','📜 角色','🏘 回城鎮'}<=texts,name
   if name=='03_select_1_3':assert {'⚔ 出發','📖 看故事'}<=texts,name
  assert Image.open(DOC/'full_preview.jpg').size==(5400,960)
  seams=read(DOC/'seams/validation.json');assert len(seams)==4 and all(s['afterMeanEdgeDelta']<s['beforeMeanEdgeDelta'] for s in seams),'seam continuity'
  for c in CLASSES:assert (DOC/'heroes'/f'{c}_frames.jpg').exists() and (DOC/'heroes'/f'{c}_walk.gif').exists(),c
 write(DOC/'validation.json',{'complete':not pending,'imagesChecked':len(checked),'checked':checked,'nodes':len(nodes),'paths':len(paths),'activeReachableNodes':sorted(seen),'reservedNodes':sorted(k for k,n in nodes.items() if n['reserved']),'pending':pending});print(len(checked),'images;',len(nodes),'nodes;',len(paths),'paths;',len(pending),'pending')
if __name__=='__main__':main()
