"""Offline art mockups and atlas previews, not Runtime UI or gameplay."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageOps,ImageFilter
from prepare_worldmap import ROOT,ART,DOC,read,write
from preview_village2d import rich
def atlas():
 im=Image.new('RGB',(10800,1920),'#d8d1bc')
 for r in range(1,6):
  p=ART/f'region_{r}.jpg'
  if p.exists():im.paste(Image.open(p),((r-1)*2160,0))
 return im
def plate(im,rect):
 x,y,w,h=rect;src=Image.open(ART/'ui/label_plate.png').convert('RGBA');sw,sh=src.size;xs=[0,64,sw-64,sw];ys=[0,32,sh-32,sh];dx=[0,50,w-50,w];dy=[0,25,h-25,h]
 for i in range(3):
  for j in range(3):im.alpha_composite(src.crop((xs[i],ys[j],xs[i+1],ys[j+1])).resize((dx[i+1]-dx[i],dy[j+1]-dy[j]),Image.Resampling.LANCZOS),(x+dx[i],y+dy[j]))
def mockup(name,offset,hero,visible,selected=None,fogstart=None):
 spec=read(ART/'nodes.json');im=atlas().crop((offset,0,offset+1080,1920)).convert('RGBA');elements=[]
 for path in spec['paths']:
  if path['enabled'] and path['from'] in visible and path['to'] in visible:
   points=[(x-offset,y) for x,y in path['points']];ImageDraw.Draw(im).line(points,fill='#ffe7a2',width=10,joint='curve')
 for key in visible:
  if key not in spec['nodes']:continue
  node=spec['nodes'][key];x=node['x']-offset;y=node['y']
  if not 0<=x<=1080:continue
  kind='town' if node['type']=='town' else 'tower' if key=='tower' else 'boss' if node['type']=='boss' else 'open' if key==selected or key=='1-1' else 'clear';src=Image.open(ART/'ui'/f'node_{kind}.png').convert('RGBA');im.alpha_composite(src,(round(x-src.width/2),round(y-src.height/2)));lx=max(160,min(920,node['label_x']-offset));ly=node['label_y'];plate(im,(round(lx-160),round(ly-48),320,96));d=ImageDraw.Draw(im)
  text=node['name'].split('・')[0]
  if node['type'] in ['town','tower']:rich(d,(lx,ly),text,30,fill='#4c3421',center=True)
  else:rich(d,(lx,ly-20),key,25,fill='#4c3421',center=True);rich(d,(lx,ly+13),text[:11],25,fill='#4c3421',center=True)
  if key==selected:d.ellipse((x-80,y-80,x+80,y+80),outline='#ffe989',width=6)
  elements.append({'id':key,'mapPosition':[node['x'],y],'labelRect':[round(lx-160),round(ly-48),320,96]})
 if fogstart is not None:
  # An opaque unrevealed layer prevents terrain leaking through the transparent cloud fringes.
  m=Image.new('L',im.size);ImageDraw.Draw(m).rectangle((min(1080,fogstart+70),0,1080,1920),fill=255);m=m.filter(ImageFilter.GaussianBlur(45));veil=Image.new('RGBA',im.size,'#e8e6de');im=Image.composite(veil,im,m)
  for i,y in enumerate([-180,480,1120]):
   cloud=Image.open(ART/'ui'/f'fog_{i%3}.png').convert('RGBA');im.alpha_composite(cloud,(fogstart-170,y))
 if hero in spec['nodes']:
  p=spec['nodes'][hero];pic=Image.open(ART/'hero_novice_idle_0.png').convert('RGBA');im.alpha_composite(pic,(p['x']-offset-96,p['y']-256))
 d=ImageDraw.Draw(im)
 def control(id,rect,text,size=30):
  x,y,w,h=rect;d.rounded_rectangle((x,y,x+w,y+h),18,fill='#203342',outline='#e8c78a',width=3);rich(d,(x+w/2,y+h/2),text,size,center=True);elements.append({'id':id,'rect':rect,'text':text})
 control('title',[30,35,570,100],'🗺 大地圖',40);control('gold',[630,35,420,100],'💰 {gold}',30);control('difficulty',[30,160,1020,85],'簡單　／　普通　／　困難',29)
 if selected:
  node=spec['nodes'][selected];d.rounded_rectangle((30,1450,1050,1765),24,fill='#203342',outline='#e8c78a',width=4);rich(d,(65,1480),selected+' '+node['name'],35);rich(d,(65,1540),'推薦等級 {level}　｜　目前選擇',28);control('depart',[65,1610,450,100],'⚔ 出發',34);control('story',[560,1610,450,100],'📖 看故事',34)
 control('character',[30,1800,490,95],'📜 角色',32);control('return',[560,1800,490,95],'🏘 回城鎮',32)
 out=DOC/'mockups';out.mkdir(parents=True,exist_ok=True);im.convert('RGB').save(out/(name+'.jpg'),quality=91,optimize=True);write(out/(name+'.json'),{'canvas':[1080,1920],'origin':'top-left','viewportOffset':[offset,0],'heroNode':hero,'selectedNode':selected,'visibleNodes':sorted(visible),'fogBeginsScreenX':fogstart,'elements':elements,'note':'Art-only example. Keep existing unlock, currency, difficulty and travel behavior; no gameplay implementation.'})
def heroes():
 out=DOC/'heroes';out.mkdir(exist_ok=True)
 for cls in ['novice','swordsman','archer','mage','assassin','priest']:
  files=[ART/f'hero_{cls}_side_{i}.png' for i in range(4)]+[ART/f'hero_{cls}_idle_{i}.png' for i in range(2)]
  if not all(p.exists() for p in files):continue
  sheet=Image.new('RGB',(1152,290),'#ded5c1');d=ImageDraw.Draw(sheet)
  for i,p in enumerate(files):pic=Image.open(p).convert('RGBA');sheet.paste(pic,(i*192,0),pic);d.text((i*192+10,265),p.stem.replace('hero_'+cls+'_',''),fill='#493425')
  sheet.save(out/(cls+'_frames.jpg'),quality=92,optimize=True)
  frames=[]
  for p in files[:4]:
   bg=Image.new('RGBA',(192,256),'#ded5c1');bg.alpha_composite(Image.open(p).convert('RGBA'));frames.append(bg)
  frames[0].save(out/(cls+'_walk.gif'),save_all=True,append_images=frames[1:],duration=150,loop=0,disposal=2)
def main():
 DOC.mkdir(parents=True,exist_ok=True);atlas().resize((5400,960),Image.Resampling.LANCZOS).save(DOC/'full_preview.jpg',quality=93,optimize=True,subsampling=0)
 spec=read(ART/'nodes.json');visible={k for k,n in spec['nodes'].items() if not n['reserved']};mockup('01_start',0,'town:0',{'town:0','1-1'},fogstart=800)
 overview=atlas().convert('RGBA');d=ImageDraw.Draw(overview)
 for path in spec['paths']:
  if path['enabled']:d.line([tuple(p) for p in path['points']],fill='#ffe79b',width=9,joint='curve')
 for key,n in spec['nodes'].items():
  x,y=n['x'],n['y'];d.ellipse((x-18,y-18,x+18,y+18),fill='#626b79' if n['reserved'] else '#ffbb45',outline='#352b22',width=4);text=key+' (reserved)' if n['reserved'] else key+' '+n['name'].split('・')[0];rich(d,(x-110,n['label_y']),text,27,fill='#fff4d2')
 overview.convert('RGB').resize((5400,960),Image.Resampling.LANCZOS).save(DOC/'nodes_preview.jpg',quality=93,optimize=True)
 mockup('03_select_1_3',780,'1-3',{'1-1','1-2','1-3'},selected='1-3',fogstart=950)
 if 'tower' in spec['nodes']:mockup('02_tower_branch',3240,'2-4',{k for k in visible if k.startswith('2-') or k in ['town:1','tower']},fogstart=1040)
 heroes()
if __name__=='__main__':main()
