"""Illustrated layout handoff, design pixels only; does not alter game UI."""
import json
from PIL import Image,ImageDraw,ImageOps
from preview_village2d import ROOT,ART,DOC,NAMES,rich
CHARS=ROOT/'Assets/SpellDuel/Resources/Story2D/chars'
def frame(im,asset,rect):
 path=ART/'ui'/(asset+'.png')
 if not path.exists():return False
 src=Image.open(path).convert('RGBA');x,y,w,h=rect;sw,sh=src.size
 l=r=min(70,sw//4);t=b=min(50,sh//4)
 if asset=='speech_bubble':r=110;b=95
 xs=[0,l,sw-r,sw];ys=[0,t,sh-b,sh];dx=[0,min(l,w//3),w-min(r,w//3),w];dy=[0,min(t,h//3),h-min(b,h//3),h]
 for i in range(3):
  for j in range(3):
   tile=src.crop((xs[i],ys[j],xs[i+1],ys[j+1])).resize((dx[i+1]-dx[i],dy[j+1]-dy[j]),Image.Resampling.LANCZOS);im.alpha_composite(tile,(x+dx[i],y+dy[j]))
 return True
STAFF={'smith':'xiaotie','shop':'clerk','guild':'guildmaster','dungeon':'guide','training':'instructor','bounty':'bountyofficer','watch':'announcer','warp':'guide','gate':'guide'}
ROWS={
 'smith':[('晨光長劍 +3','👕 穿著中　稀有'),('旅人護甲 +2','👕 穿著中　精良'),('灰燼短杖 +0','未穿著　普通'),('霧林長弓 +1','未穿著　稀有')],
 'shop':[('生命藥水','恢復生命　💰 {price}'),('魔力藥水','恢復魔力　💰 {price}'),('解毒藥劑','解除中毒　💰 {price}')],
 'guild':[('劍士','以劍守住前路'),('獵人','看準破綻，一箭命中'),('法師','讓元素回應你的呼喚'),('刺客','在陰影中掌握先機'),('牧師','用光守護同行的人')],
 'dungeon':[('灰燼礦坑','Lv {level}　{players} 人'),('記名林深處','Lv {level}　{players} 人'),('沉船礁窟','Lv {level}　{players} 人')],
 'training':[('閃避挑戰','🥇 每日挑戰'),('輸出挑戰','🥈 每日挑戰'),('跑步挑戰','🥉 每日挑戰'),('綜合挑戰','🏅 每日挑戰')],
 'bounty':[('熔岩巨像','懸賞金　💰 {reward}'),('腐化樹王','懸賞金　💰 {reward}'),('深海巨妖','懸賞金　💰 {reward}')],
 'warp':[('炎之里','已到訪'),('森之里','已到訪'),('潮騷港','尚未到訪　🔒')],
 'gate':[('✔ 灰燼裡的晨鐘','已通關　建議 Lv {level}'),('▶ 山路上的回聲','目前關卡　建議 Lv {level}'),('🔒 巨像的鐘舌','尚未開放　建議 Lv {level}')],
 'watch':[]}
def make(key):
 bg=ART/f'in_{key}_flame.jpg'
 if not bg.exists():return
 im=Image.open(bg).convert('RGBA');im.alpha_composite(Image.new('RGBA',im.size,(12,17,29,65)));entries=[]
 def box(id,rect,text='',kind='panel',size=28,selected=False):
  x,y,w,h=rect;entries.append({'id':id,'rect':rect,'type':kind,'text':text});d=ImageDraw.Draw(im)
  asset='btn_big' if kind=='button' else 'list_row_sel' if selected else 'list_row' if kind=='row' else 'speech_bubble' if id=='speech' else 'list_frame'
  if not frame(im,asset,rect):d.rounded_rectangle((x,y,x+w,y+h),18,fill=(18,31,46,205) if kind!='button' else (153,73,28,245),outline='#f6d08c' if selected or kind=='button' else '#a59070',width=4 if selected else 2)
  d=ImageDraw.Draw(im)
  if id=='speech':fill='#513525'
  else:fill='#fff4dd'
  if text:rich(d,(x+w//2,y+h//2) if kind=='button' else (x+30,y+20),text,size,fill=fill,center=kind=='button')
 def label(x,y,text,size=28):rich(ImageDraw.Draw(im),(x,y),text,size)
 box('header',[30,45,1020,115]);label(55,65,NAMES[key],40);label(770,80,'💰 {gold}',28)
 box('return',[270,1795,540,92],'🏘 回城鎮','button',32)
 # All functional content is deliberately confined to left60%; NPC never covers buttons.
 top=300
 if key=='smith':
  label(45,205,'要強化的裝備',38);box('enhance',[680,205,360,185],'','button',34);label(715,225,'⚒ 強化',34);label(715,275,'💰 {cost}',27);label(715,322,'成功率 {rate}%',27)
  entries[-1]['text']='⚒ 強化（💰 {cost}・成功率 {rate}%）'
  box('stats',[680,420,360,230]);label(700,450,'強化前 → 強化後',28);label(700,510,'{before} → {after}',26)
 elif key=='shop':
  box('buy_tab',[40,195,285,85],'買道具','button',32,True);box('sell_tab',[335,195,285,85],'賣裝備','button',32)
 elif key=='guild':
  box('trial',[680,205,360,260],'轉職試煉', 'button',32);label(700,285,'✔ 已通過／挑戰',27);label(45,210,'選擇你的職業',36)
 elif key=='watch':
  box('roomcode',[40,220,580,160],'房間代碼',size=32);label(80,285,'{roomCode}',52)
  for i,s in enumerate(['1','2','3','4','5','6','7','8','9','清除','0','⌫']):box('key_'+str(i),[40+(i%3)*195,420+(i//3)*160,180,140],s,'button',46)
  box('spectate',[40,1100,580,165],'👀 去看台','button',34);label(58,1160,'（輸入房間代碼）',28);entries[-1]['text']='👀 去看台（輸入房間代碼）'
 else:label(45,215,{'dungeon':'選擇副本','training':'每日挑戰','bounty':'接下今天的委託','warp':'選擇目的地','gate':'這一章的旅程'}[key],36)
 if ROWS[key]:
  heights={'smith':210,'shop':270,'guild':230,'dungeon':340,'training':230,'bounty':340,'warp':330,'gate':350};step=heights[key];bottom=min(1730,top+step*len(ROWS[key])+30)
  box('scroll',[30,top-10,610,bottom-top+10]);entries[-1].update({'axis':'vertical','clip':True})
  for i,(title,sub) in enumerate(ROWS[key]):
   y=top+i*step;box('row_'+str(i),[45,y,580,step-15],kind='row',selected=i==0);label(65,y+20,title,31);label(65,y+76,sub,25)
   if key=='shop':box('buy_'+str(i),[310,y+150,290,85],'💰 {price} 購買','button',25)
   if key=='guild':
    cid=['swordsman','archer','mage','assassin','priest'][i];src=ROOT/f'Assets/SpellDuel/Resources/Story2D/classes/{cid}.png'
    if src.exists():
     pic=Image.open(src).convert('RGBA');pic=pic.crop((int(pic.width*.3),int(pic.height*.02),int(pic.width*.7),int(pic.height*.3)));pic.thumbnail((120,120));im.alpha_composite(pic,(480,y+10))
    box('choose_'+str(i),[65,y+140,260,65],'選這個','button',26)
   if key=='dungeon':
    box('solo_'+str(i),[65,y+150,535,75],'⚔ 單人進入','button',28);box('party_'+str(i),[65,y+235,535,75],'組隊進入','button',28)
   if key=='training':box('challenge_'+str(i),[65,y+132,535,70],'挑戰','button',28)
   if key=='bounty':
    box('accept_'+str(i),[65,y+224,535,80],'🔥 接下懸賞，出發討伐','button',26)
    boss=['golem','treeking','kraken'][i];src=CHARS/(boss+'_normal.png')
    if src.exists():pic=Image.open(src).convert('RGBA').crop((180,70,570,460));pic.thumbnail((140,140));im.alpha_composite(pic,(440,y+65))
   if key=='warp':box('warp_'+str(i),[65,y+220,535,80],'🌀 傳送' if i<2 else '🔒 尚未到訪','button',29)
   if key=='gate':
    box('depart_'+str(i),[65,y+160,260,75],'⚔ 出發','button',29);box('story_'+str(i),[340,y+160,260,75],'📖 故事','button',29)
 if key=='training':
  box('tutorial',[40,1370,580,110],'🎓 再上一次教學','button',29);box('walkthrough',[40,1500,580,110],'🧭 重看村子導覽','button',29)
 speech={'smith':'火候到了，交給我吧！','shop':'旅途的補給準備好了。','guild':'選擇屬於你的道路。','dungeon':'岔路很多，記得互相照應。','training':'準備好了？先穩住呼吸。','bounty':'請確認目標再出發。','watch':'輸入朋友的房間代碼吧！','warp':'去過的地方，都記在這裡。','gate':'晨鐘會為歸來的人響起。'}[key]
 box('speech',[675,730,365,190]);rich(ImageDraw.Draw(im),(705,760),speech[:9],28,fill='#513525');rich(ImageDraw.Draw(im),(705,805),speech[9:],28,fill='#513525')
 npc=CHARS/(STAFF[key]+'_normal.png')
 if npc.exists():pic=ImageOps.contain(Image.open(npc).convert('RGBA'),(390,730));im.alpha_composite(pic,(660+(390-pic.width)//2,990));entries.append({'id':'npc','rect':[660,990,390,730],'type':'portrait','asset':npc.relative_to(ROOT).as_posix()})
 if key=='smith':pic=Image.open(ART/'anvil_0.png').resize((365,274));im.alpha_composite(pic,(675,1460));entries.append({'id':'anvil','rect':[675,1460,365,274],'type':'animation','frames':6,'loops':3,'durationMs':1500})
 batch=2 if key=='smith' else 3 if key in ['shop','guild'] else 4;out=DOC/f'batch{batch}';out.mkdir(parents=True,exist_ok=True);im.convert('RGB').save(out/(key+'_mockup.jpg'),quality=90,optimize=True)
 annotated=im.copy();d=ImageDraw.Draw(annotated)
 for i,e in enumerate(entries):
  x,y,w,h=e['rect'];d.rectangle((x,y,x+w,y+h),outline='#70f7ed',width=2);rich(d,(x+5,y+h-24),e['id'],16,fill='#70f7ed')
 annotated.convert('RGB').save(out/(key+'_layout.jpg'),quality=90,optimize=True)
 (out/(key+'_layout.json')).write_text(json.dumps({'canvas':[1080,1920],'origin':'top-left','units':'design-pixels','illustrativeDataOnly':True,'runtimeConditions':'Preserve all existing visibility, eligibility and enabled conditions.','elements':entries},ensure_ascii=False,indent=2),encoding='utf-8')
if __name__=='__main__':
 for key in STAFF:make(key)
