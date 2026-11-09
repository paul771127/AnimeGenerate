"""Job 002: animation-only extension of the approved job 001 source geometry."""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

WORK=Path(__file__).parent
REPO=Path(r'E:\AI接案\SpellDuel-repo')
OUT=REPO/'spellduel-unity/Assets/SpellDuel/Resources/CharKit/archmage_v1'
SOURCE=WORK.parent/'archmage001/archmage.blend'
NEW=['walk_back','walk_left','walk_right','run_back','run_left','run_right','cast_staff','cast_staff_loop']
OLD=['idle','walk','run','cast','cast_loop','hit','crouch','jump','die','victory']

def fingerprint(action):
 data=[(f.data_path,f.array_index,[(list(k.co),list(k.handle_left),list(k.handle_right),k.interpolation) for k in f.keyframe_points]) for f in action.fcurves]
 return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()

def geometry_fingerprint():
 data=[]
 for obj in sorted([o for o in bpy.data.objects if o.type=='MESH'],key=lambda o:o.name):
  data.append((obj.name,[tuple(v.co) for v in obj.data.vertices],[tuple(p.vertices) for p in obj.data.polygons],
               [[tuple(x.uv) for x in layer.data] for layer in obj.data.uv_layers],
               [[(g.group,g.weight) for g in v.groups] for v in obj.data.vertices]))
 return hashlib.sha256(repr(data).encode()).hexdigest()

def main():
 bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
 rig=bpy.data.objects['ArchmageRig'];rig.data.pose_position='POSE'
 for t in rig.animation_data.nla_tracks:t.mute=True
 before={name:fingerprint(bpy.data.actions[name]) for name in OLD};geo_before=geometry_fingerprint()
 rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
 for b in rig.pose.bones:b.rotation_mode='QUATERNION'
 def path(name,kind='rotation_quaternion'):return 'pose.bones['+json.dumps(name)+'].'+kind
 def clean(action,names,locations=()):
  paths={path(n) for n in names}|{path(n,'location') for n in locations}
  for f in list(action.fcurves):
   if f.data_path in paths:action.fcurves.remove(f)
 def channels(action,names,kind):
  return {n:[action.fcurves.new(path(n,kind),index=i) for i in range(4 if kind=='rotation_quaternion' else 3)] for n in names}
 def key(curves,frame,value):
  for i,f in enumerate(curves):
   p=f.keyframe_points.insert(frame,float(value[i]),options={'FAST'});p.interpolation='LINEAR'
 def finish(action,loop):
  for f in action.fcurves:
   f.update()
   if loop:
    first=f.evaluate(1);end=round(action.frame_range[1])
    for k in f.keyframe_points:
     if abs(k.co.x-end)<.001:k.co.y=first
    f.update()
 def clone(base,name,frames):
  action=bpy.data.actions[base].copy();action.name=name;action.use_fake_user=True
  low,high=action.frame_range;factor=(frames-1)/(high-low)
  for f in action.fcurves:
   for k in f.keyframe_points:
    k.co.x=1+(k.co.x-low)*factor;k.handle_left.x=k.co.x;k.handle_right.x=k.co.x
   f.update()
  return action
 def set_world(name,position,rotation):
  pb=rig.pose.bones[name];pb.matrix=Matrix.Translation(position)@rotation.to_matrix().to_4x4();bpy.context.view_layer.update()
 def two_bone(upper,lower,end,target,pole,end_rotation):
  hip=rig.pose.bones[upper].matrix.translation.copy()
  l1=(rest[lower].translation-rest[upper].translation).length
  l2=(rest[end].translation-rest[lower].translation).length
  delta=Vector(target)-hip;distance=max(.01,min(delta.length,l1+l2-.001));axis=delta.normalized()
  target=hip+axis*distance;across=Vector(pole)-axis*Vector(pole).dot(axis)
  if across.length<.01:across=Vector((0,-1,0))
  across.normalize();along=(l1*l1-l2*l2+distance*distance)/(2*distance)
  knee=hip+axis*along+across*math.sqrt(max(.00001,l1*l1-along*along))
  for name,start,tip in [(upper,hip,knee),(lower,knee,target)]:
   direction=rig.data.bones[name].tail_local-rig.data.bones[name].head_local
   rotation=direction.normalized().rotation_difference((tip-start).normalized())@rest[name].to_quaternion()
   set_world(name,start,rotation)
  set_world(end,target,end_rotation)
 def prop(frame,rotation,quats,locations):
  hand=rig.pose.bones['J_Bip_R_Hand'];palm=hand.matrix@Vector((0,.032,0))
  set_world('Staff',palm,rotation@rest['Staff'].to_quaternion())
  key(quats['Staff'],frame,rig.pose.bones['Staff'].rotation_quaternion)
  key(locations['Staff'],frame,rig.pose.bones['Staff'].location)
 def coherent_keys(qs,last,frame):
  for name,curves in qs.items():
   q=rig.pose.bones[name].rotation_quaternion.copy()
   if name in last and q.dot(last[name])<0:q.negate()
   last[name]=q.copy();key(curves,frame,q)
 records=[]
 for name in NEW[:6]:
  running=name.startswith('run');frames=21 if running else 41
  action=clone('run' if running else 'walk',name,frames)
  legs=['J_Bip_'+s+'_'+b for s in ['L','R'] for b in ['UpperLeg','LowerLeg','Foot']]
  hip='J_Bip_C_Hips';clean(action,legs+['Staff'],[hip,'Staff'])
  qs=channels(action,legs,'rotation_quaternion');props=channels(action,['Staff'],'rotation_quaternion');ls=channels(action,[hip,'Staff'],'location')
  direction=Vector((0,1,0)) if name.endswith('back') else Vector((1 if name.endswith('left') else -1,0,0))
  stride=(.32 if running else .24) if name.endswith('back') else (.20 if running else .16)
  last={};rig.animation_data.action=action
  for frame in range(1,frames+1):
   phase=(frame-1)/(frames-1);bpy.context.scene.frame_set(frame)
   vertical=-.026+( .018 if running else .006)*math.cos(phase*math.tau*2)
   offset=rest[hip].to_3x3().inverted()@Vector((0,0,vertical));rig.pose.bones[hip].location=offset
   key(ls[hip],frame,offset);bpy.context.view_layer.update()
   for side,shift in [('L',0),('R',.5)]:
    p=(phase+shift)%1;lift=0
    if p<.6:travel=.5-p/.6
    else:
     t=(p-.6)/.4;travel=-.5+t*t*(3-2*t);lift=(.075 if running else .045)*math.sin(math.pi*t)
    foot='J_Bip_'+side+'_Foot';target=rest[foot].translation+direction*(stride*travel)+Vector((0,0,lift))
    if name.endswith(('left','right')):target.x=max(.018,target.x) if side=='L' else min(-.018,target.x)
    two_bone('J_Bip_'+side+'_UpperLeg','J_Bip_'+side+'_LowerLeg',foot,target,(0,-1,0),rest[foot].to_quaternion())
   coherent_keys(qs,last,frame)
   prop(frame,Quaternion((1,0,0),.06),props,ls)
  finish(action,True)
  records.append(dict(name=name,frames=frames,seconds=(frames-1)/30,loop=True,localUnityTravel={'back':[0,0,-1],'left':[-1,0,0],'right':[1,0,0]}[name.split('_')[1]],method='authored two-bone IK foot cycle; upper-body gait copied from existing '+('run' if running else 'walk')))
 # Right-hand staff casting: left arm and floating book remain in the idle pose.
 for name,frames,loop in [('cast_staff',25,False),('cast_staff_loop',49,True)]:
  action=clone('idle',name,frames);arm=['J_Bip_R_UpperArm','J_Bip_R_LowerArm','J_Bip_R_Hand']
  clean(action,arm+['Staff'],['Staff']);qs=channels(action,arm,'rotation_quaternion');pq=channels(action,['Staff'],'rotation_quaternion');ls=channels(action,['Staff'],'location')
  rig.animation_data.action=action;bpy.context.scene.frame_set(1)
  idle_hand=rig.pose.bones['J_Bip_R_Hand'].matrix.translation.copy();idle_rotation=rig.pose.bones['J_Bip_R_Hand'].matrix.to_quaternion()
  last={}
  for frame in range(1,frames+1):
   phase=(frame-1)/(frames-1);bpy.context.scene.frame_set(frame)
   if loop:
    target=Vector((-.22,-.25,1.40+.012*math.sin(phase*math.tau)))
    lean=math.radians(27+2*math.sin(phase*math.tau));amount=1
   else:
    # Anticipation -> straight forward release -> short hold -> natural recovery.
    if phase<.25:amount=(phase/.25)*.3
    elif phase<.55:amount=.3+.7*((phase-.25)/.30)
    elif phase<.72:amount=1
    else:amount=1-(phase-.72)/.28
    target=idle_hand.lerp(Vector((-.17,-.405,1.27)),amount)
    lean=math.radians(78)*amount
   hand_rotation=Quaternion((1,0,0),lean)@idle_rotation
   two_bone(arm[0],arm[1],arm[2],target,(-1,0,-.35),hand_rotation)
   coherent_keys(qs,last,frame)
   prop(frame,Quaternion((1,0,0),lean),pq,ls)
  finish(action,loop)
  records.append(dict(name=name,frames=frames,seconds=(frames-1)/30,loop=loop,method='project-authored right-arm IK and palm attachment; staff orientation animated forward/up; idle left-arm retained'))
 after={name:fingerprint(bpy.data.actions[name]) for name in OLD}
 assert before==after,'An original action was modified'
 assert geo_before==geometry_fingerprint(),'Geometry/UV/skin data changed'
 # All new actions keep Root/rig translation stationary and only bob Hips vertically.
 for name in NEW:
  for fc in bpy.data.actions[name].fcurves:
   assert fc.data_path not in ['location','rotation_euler','rotation_quaternion'],(name,fc.data_path)
   assert not fc.data_path.startswith('pose.bones["Root"]'),name
 rig.animation_data.action=None
 for pb in rig.pose.bones:pb.rotation_quaternion=Quaternion();pb.location=(0,0,0)
 for t in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(t)
 for name in OLD+NEW:
  action=bpy.data.actions[name];track=rig.animation_data.nla_tracks.new();track.name=name
  strip=track.strips.new(name,1,action);strip.extrapolation='NOTHING'
 bpy.context.scene.render.fps=30;bpy.context.scene.frame_set(0)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for obj in bpy.data.objects:
  if obj.type=='MESH':obj.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(OUT/'archmage.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,path_mode='STRIP',embed_textures=False)
 bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'archmage002.blend'))
 report=dict(newClips=records,oldActionFingerprints=before,oldActionsUnchanged=before==after,geometryHashBefore=geo_before,geometryHashAfter=geometry_fingerprint(),geometryUnchanged=geo_before==geometry_fingerprint(),rootTransformChannelsAbsent=True)
 (WORK/'animation-inspect.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
 print('JOB_002_EXPORTED',len(OLD+NEW),'clips')

if __name__=='__main__':main()
