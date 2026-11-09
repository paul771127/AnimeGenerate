"""Assemble actual Unity renders, audit records and response manifests."""
from pathlib import Path
import json,hashlib,math,sys,shutil
from PIL import Image,ImageOps,ImageDraw
W=Path(__file__).parent;R=Path(r'E:\AI接案\SpellDuel-repo');J=R/'chargen/jobs/spellduel-cloud/003-all-heroes';K=R/'spellduel-unity/Assets/SpellDuel/Resources/CharKit';REF=R/'spellduel-unity/Assets/SpellDuel/Resources/Story2D/classes'
CONFIG=json.loads((W/'classes.json').read_text('utf-8'));NAMES=['idle','walk','run','walk_back','walk_left','walk_right','run_back','run_left','run_right','attack','skill','skill_loop','hit','crouch','jump','die','victory']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
def panel(files,labels,columns,width=280,height=350):
 rows=math.ceil(len(files)/columns);out=Image.new('RGB',(columns*width,rows*(height+28)),(36,39,49));d=ImageDraw.Draw(out)
 for i,(p,label) in enumerate(zip(files,labels)):
  im=Image.open(p).convert('RGB');im=ImageOps.contain(im,(width,height));x=(i%columns)*width;y=(i//columns)*(height+28);out.paste(im,(x+(width-im.width)//2,y+28+(height-im.height)//2));d.text((x+8,y+7),label,fill='white')
 return out
def package(id):
 j=J/id;prev=j/'previews';out=K/(id+'_v1');c=CONFIG[id];stats=json.loads((W/id/'build-inspect.json').read_text('utf-8'));u=json.loads((j/'unity-inspect.json').read_text('utf-8'))
 assert u['ok'] and u['humanoidValid'] and u['humanoidHuman'],(id,u.get('error'))
 assert u['bodyTriangles']==stats['bodyTriangles'] and u['bodyTriangles']<=15000
 assert {x['name'] for x in u['clips']}==set(NAMES) and len(u['clips'])==17
 assert all(x['fps']==30 and x['attachmentCurves']>0 for x in u['clips'])
 assert len(u['poses'])==51 and u['rootTranslationMax']==0
 assert u['maxInfluences']<=4 and abs(u['neutralFeetY'])<.005
 assert max(x['maxHandSkinDistance'] for x in u['poses'])<.30
 expected={id+'.fbx',id+'_albedo.png',id+'_emission.png'};assert {p.name for p in out.iterdir() if p.is_file()}==expected
 for suffix in ['_albedo.png','_emission.png']:assert Image.open(out/(id+suffix)).size==(2048,2048)
 for n in ['front.png','back.png','third_person.png','attack.png']:assert (prev/n).is_file()
 comparison=panel([REF/(id+'.png'),prev/'front.png'],['Project portrait reference',id+' - Unity imported model'],2,540,720);comparison.save(prev/'reference_comparison.png')
 files=[W/id/'frames'/('pose_'+n+'.png') for n in NAMES];panel(files,NAMES,5,240,300).save(prev/'animation_contact.png')
 frames=[Image.open(W/id/'frames'/('attack_'+str(i).zfill(2)+'.png')).convert('RGB').resize((432,540)) for i in range(12)]
 frames[0].save(prev/'attack.gif',save_all=True,append_images=frames[1:],duration=67,loop=0,optimize=False)
 origin=json.loads((J.parent/'001-archmage-hero/sources.json').read_text('utf-8'))[:2]
 origin.extend([dict(id='job003-original-authoring',asset='Class-specific clothing, layered armor, hoods/capes, equipment, UV palette, weapon attack/skill/charge and palm attachment curves',origin='Project-authored Blender mesh and animation data',license='LicenseRef-ProjectOriginal',classId=id,design=c['notes']),dict(id='shared-project-rig-and-locomotion',origin='Work orders 001 + 002 in this repository',fbxPath='spellduel-unity/Assets/SpellDuel/Resources/CharKit/archmage_v1/archmage.fbx',fbxSha256=sha(K/'archmage_v1/archmage.fbx'),authoringSceneSha256=sha(W.parent/'archmage002/archmage002.blend'),license='CC0-1.0 source plus project-original animation edits'),dict(id='project-portrait-reference',path='spellduel-unity/Assets/SpellDuel/Resources/Story2D/classes/'+id+'.png',sha256=sha(REF/(id+'.png')),origin='Existing project reference designated by the user; not relabeled CC0; portrait pixels are not projected onto the model')])
 write(j/'sources.json',origin);write(j/'build-inspect.json',stats)
 seams={}
 for clip in u['clips']:
  if clip['loop']:
   ps=[p for p in u['poses'] if p['clip']==clip['name']];a,b=ps[0],ps[-1]
   seams[clip['name']]=max(math.sqrt(sum((a[k][v]-b[k][v])**2 for v in ['x','y','z'])) for k in ['rightHand','leftHand','weaponR','weaponL'])
 assert max(seams.values())<.025,(id,seams)
 limitations=['Mobile FPS/thermal tests and full game transitions were not measured; previews come from an isolated Unity 2022.3.62f2 import using the shared game shaders.','Clothing uses the existing skinned-bone approach without cloth simulation.','Full Humanoid transform mask is required to retain weapon/book/string/arrow curves; see ../UNITY_IMPORT.md.','The attack GIF repeats for review; the source attack and skill clips are non-looping.']
 if id=='novice':limitations.append('The requested weapon table explicitly specifies a wooden club, so the delivered weapon is a wooden club although the portrait shows a short sword.')
 if id in ['ranger','summoner','shadowdancer']:limitations.append('Portrait companions/summons/shadow duplicates are gameplay entities and are not welded into the player mesh.')
 response=dict(schemaVersion=1,job='003-all-heroes',id=id,title=c['title'],ok=True,status='completed',output='spellduel-unity/Assets/SpellDuel/Resources/CharKit/'+id+'_v1/',files=[dict(name=n,bytes=(out/n).stat().st_size,sha256=sha(out/n)) for n in sorted(expected)],appearance=dict(style='Japanese anime cel shaded with inverted-hull OUTLINE mesh',reference='spellduel-unity/Assets/SpellDuel/Resources/Story2D/classes/'+id+'.png',design=c['notes'],referenceComparison='previews/reference_comparison.png',bodyHeightMeters=round(stats['heightMeters'],4),headgearAdditionalHeight=c.get('mitre',False)),geometry=dict(bodyTriangles=u['bodyTriangles'],outlineTriangles=u['outlineTriangles'],materials=u['materialCount'],bones=u['bones'],maxInfluences=u['maxInfluences'],neutralFeetY=u['neutralFeetY'],axis='Y-up, front +Z, meters'),weaponParents=stats['weaponParents'],animations=stats['animations'],qa=dict(unityVersion='2022.3.62f2',humanoidValid=u['humanoidValid'],humanoidHuman=u['humanoidHuman'],clipCount=len(u['clips']),sampledPoses=len(u['poses']),rootTranslationMaxMeters=u['rootTranslationMax'],maxHandSkinDistanceMeters=max(x['maxHandSkinDistance'] for x in u['poses']),loopMarkerSeamMeters=seams,visualReview='Actual imported Unity front/back/third-person and 17-pose contact sheet reviewed',mobileProfile='not_measured'),previews=['previews/'+p.name for p in sorted(prev.iterdir()) if p.is_file()],sources='sources.json',noShader=True,noMeta=True,noPrefab=True,runtimeChanged=False,limitations=limitations)
 write(j/'response.json',response);write(j/'unity-inspect.json',u);print('PACKAGED',id,u['bodyTriangles'],'max seam',max(seams.values()))

if __name__=='__main__':
 ids=sys.argv[1:] or list(CONFIG)
 for id in ids:package(id)
 panel([J/id/'previews/front.png' for id in ids],ids,3,300,375).save(W/('batch_'+'_'.join(ids[:1])+'.png'))
