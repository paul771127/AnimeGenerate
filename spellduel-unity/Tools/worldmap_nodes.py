"""Art-only route coordinates: chapter 1 has four stages, chapters 2-5 have six."""
import argparse,re
from prepare_worldmap import ROOT,ART,write,meta
TOWNS=['炎之里','森之里','潮騷港','赤砂城','風沙驛']
# Original landmarks: town, three encounters, Boss, unused exit space.
ORIGINAL={1:[(300,1090),(650,1070),(975,1140),(1320,1080),(1680,1130),(1980,1080)],2:[(300,990),(650,1015),(975,1000),(1320,1025),(1680,1010),(1980,1005)],3:[(300,1080),(650,1045),(975,1115),(1320,1070),(1680,1110),(1980,1070)],4:[(300,1100),(650,1060),(975,1120),(1320,1060),(1680,1100),(1980,1100)],5:[(300,1030),(650,1040),(975,1010),(1320,1060),(1680,1010),(1980,1000)]}
COUNTS={1:4,2:6,3:6,4:6,5:6}

def road_y(r,x):
 pts=ORIGINAL[r]
 for (a,ay),(b,by) in zip(pts,pts[1:]):
  if a<=x<=b:return round(ay+(by-ay)*(x-a)/(b-a))
 raise ValueError((r,x))

def build(regions):
 titles=dict(re.findall(r'^\[stage ([^\]]+)\]\s*(.+)$',(ROOT/'Assets/SpellDuel/Resources/story.txt').read_text(encoding='utf-8'),re.M))
 nodes={};paths=[]
 def edge(a,b,points=None,enabled=True,layout='current'):
  p,q=nodes[a],nodes[b]
  if points is None:
   points=[[p['x'],p['y']],[round(p['x']+(q['x']-p['x'])*.33),round(p['y']*.67+q['y']*.33)],[round(p['x']+(q['x']-p['x'])*.67),round(p['y']*.33+q['y']*.67)],[q['x'],q['y']]]
  paths.append({'from':a,'to':b,'points':points,'enabled':enabled,'layout':layout})
 for r in regions:
  offset=(r-1)*2160;count=COUNTS[r]
  keys=[f'town:{r-1}']+[f'{r}-{i}' for i in range(1,count+1)]
  # Keep the original Boss landmark at x=1680, not at the old reserved exit.
  positions=ORIGINAL[r][:5] if r==1 else [ORIGINAL[r][0]]+[(x,road_y(r,x)) for x in [570,810,1050,1260,1470,1680]]
  for i,(key,(x,y)) in enumerate(zip(keys,positions)):
   boss=i==count
   label_offset=(420 if boss else 340) if r==1 else (420 if boss else 550 if i==0 else 290 if i%2 else 510)
   nodes[key]={'x':offset+x,'y':y,'label_x':offset+x+(160 if boss and r>1 else 0),'label_y':y-label_offset,'region':r,'type':'town' if i==0 else 'boss' if boss else 'stage','reserved':False,'visibleByDefault':True,'name':TOWNS[r-1] if i==0 else titles[key]}
  for a,b in zip(keys,keys[1:]):edge(a,b)
  if r==1:
   # Chapter 1 is deliberately unchanged, including its hidden art-only exit slot.
   x,y=ORIGINAL[1][5];nodes['1-5']={'x':x,'y':y,'label_x':x,'label_y':y-340,'region':1,'type':'reserved','reserved':True,'visibleByDefault':False,'name':'預留第5關'}
   edge('1-4','1-5',enabled=False,layout='future-five-stage')
  if r==2:
   nodes['tower']={'x':offset+1825,'y':1370,'label_x':offset+1950,'label_y':1020,'region':2,'type':'tower','name':'無盡塔','reserved':False,'visibleByDefault':True}
   a,b=nodes['2-5'],nodes['tower']
   edge('2-5','tower',[[a['x'],a['y']],[offset+1600,1030],[offset+1645,1100],[offset+1605,1170],[offset+1670,1230],[offset+1705,1310],[offset+1760,1340],[b['x'],b['y']]])
 for r in regions:
  if r+1 not in regions:continue
  a,b=f'{r}-{COUNTS[r]}',f'town:{r}';p,q=nodes[a],nodes[b];x,y=ORIGINAL[r][5];x+=(r-1)*2160;seam=r*2160
  edge(a,b,[[p['x'],p['y']],[x,y],[seam-110,y],[seam,round((y+q['y'])/2)],[seam+120,q['y']],[q['x'],q['y']]])
  if r==1:
   edge('1-5',b,[[x,y],[seam-110,y],[seam,round((y+q['y'])/2)],[seam+120,q['y']],[q['x'],q['y']]],enabled=False,layout='future-five-stage')
 spec={'coordinateSystem':{'width':10800,'height':1920,'origin':'top-left','regionWidth':2160},'availableRegions':regions,'nodes':nodes,'paths':paths,'integrationNotes':{'currentStagesPerChapter':6,'chapterStageCounts':{str(k):v for k,v in COUNTS.items()},'reservedNodesHidden':True,'filterPaths':'Use enabled=true; chapter 1 remains four stages. Chapters 2-5 have five normal encounters then Boss stage 6.','towerBranch':'2-5 -> tower is now active; futureBranchFrom has been removed. Runtime tower unlock conditions are owned by Claude.','bossLandmarks':'Boss stage 6 keeps the original large landmark at local x=1680; stage 4/5 are normal route points. Region JPGs are unchanged.'}}
 write(ART/'nodes.json',spec);meta(ART/'nodes.json')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--regions',nargs='+',type=int,required=True);build(p.parse_args().regions)
