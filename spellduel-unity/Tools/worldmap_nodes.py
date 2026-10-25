"""Art coordinates only. Reserved fifth stages are never enabled in current routes."""
import argparse,re
from prepare_worldmap import ROOT,ART,write,meta
TOWNS=['炎之里','森之里','潮騷港','赤砂城','風沙驛']
# Local coordinates measured on the packaged 2160x1920 landscapes.
LOCAL={1:[(300,1090),(650,1070),(975,1140),(1320,1080),(1680,1130),(1980,1080)],2:[(300,990),(650,1015),(975,1000),(1320,1025),(1680,1010),(1980,1005)],3:[(300,1080),(650,1045),(975,1115),(1320,1070),(1680,1110),(1980,1070)],4:[(300,1100),(650,1060),(975,1120),(1320,1060),(1680,1100),(1980,1100)],5:[(300,1030),(650,1040),(975,1010),(1320,1060),(1680,1010),(1980,1000)]}
def build(regions):
 titles=dict(re.findall(r'^\[stage ([^\]]+)\]\s*(.+)$',(ROOT/'Assets/SpellDuel/Resources/story.txt').read_text(encoding='utf-8'),re.M));nodes={};paths=[]
 for r in regions:
  offset=(r-1)*2160;keys=[f'town:{r-1}']+[f'{r}-{n}' for n in range(1,6)]
  for i,(key,(x,y)) in enumerate(zip(keys,LOCAL[r])):
   nodes[key]={'x':offset+x,'y':y,'label_x':offset+x,'label_y':y-(420 if i==4 else 340),'region':r,'type':'town' if i==0 else 'reserved' if i==5 else 'boss' if i==4 else 'stage','reserved':i==5,'visibleByDefault':i!=5,'name':TOWNS[r-1] if i==0 else '預留第5關' if i==5 else titles[key]}
  for i in range(5):
   a,b=keys[i:i+2];p=nodes[a];q=nodes[b];paths.append({'from':a,'to':b,'points':[[p['x'],p['y']],[round(p['x']+(q['x']-p['x'])*.33),round(p['y']*.67+q['y']*.33)],[round(p['x']+(q['x']-p['x'])*.67),round(p['y']*.33+q['y']*.67)],[q['x'],q['y']]],'enabled':i<4,'layout':'current' if i<4 else 'future-five-stage'})
  if r==2:
   nodes['tower']={'x':offset+1825,'y':1370,'label_x':offset+1950,'label_y':1020,'region':2,'type':'tower','name':'無盡塔','reserved':False,'visibleByDefault':True}
   a=nodes['2-4'];b=nodes['tower'];paths.append({'from':'2-4','to':'tower','points':[[a['x'],a['y']],[offset+1600,1030],[offset+1645,1100],[offset+1605,1170],[offset+1670,1230],[offset+1705,1310],[offset+1760,1340],[b['x'],b['y']]],'enabled':True,'layout':'current','futureBranchFrom':'2-5'})
 for r in regions:
  if r+1 not in regions:continue
  a=f'{r}-4';b=f'town:{r}';p=nodes[a];q=nodes[b];reserve=nodes[f'{r}-5'];seam=r*2160
  paths.append({'from':a,'to':b,'points':[[p['x'],p['y']],[reserve['x'],reserve['y']],[seam-110,reserve['y']],[seam,round((reserve['y']+q['y'])/2)],[seam+120,q['y']],[q['x'],q['y']]],'enabled':True,'layout':'current'})
  paths.append({'from':f'{r}-5','to':b,'points':[[reserve['x'],reserve['y']],[seam-110,reserve['y']],[seam,round((reserve['y']+q['y'])/2)],[seam+120,q['y']],[q['x'],q['y']]],'enabled':False,'layout':'future-five-stage'})
 spec={'coordinateSystem':{'width':10800,'height':1920,'origin':'top-left','regionWidth':2160},'availableRegions':regions,'nodes':nodes,'paths':paths,'integrationNotes':{'currentStagesPerChapter':4,'reservedNodesHidden':True,'filterPaths':'Use only enabled=true for the present four-stage layout. Other edges are future art positions only.','towerBranch':'2-4 now; move to 2-5 only after gameplay adds a fifth stage.'}};write(ART/'nodes.json',spec);meta(ART/'nodes.json')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--regions',nargs='+',type=int,required=True);build(p.parse_args().regions)
