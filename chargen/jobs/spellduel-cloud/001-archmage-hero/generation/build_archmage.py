"""Job 001: reference-directed Archmage, derived from the audited CC0 anime head/rig.
All clothing, staff, book, ornament geometry and animation edits are authored here.
"""
import bpy, bmesh, json, math, sys, importlib.util, inspect
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT=Path(r'E:\AI接案\rpg-world-builder')
REPO=Path(r'E:\AI接案\SpellDuel-repo')
WORK=Path(__file__).parent
OUT=REPO/'spellduel-unity/Assets/SpellDuel/Resources/CharKit/archmage_v1'
JOB=REPO/'chargen/jobs/spellduel-cloud/001-archmage-hero'
OUT.mkdir(parents=True,exist_ok=True);(JOB/'previews').mkdir(exist_ok=True)
BASE=ROOT/'assets/characters/base/anime-youth-cc0-v1'
PALETTE=['#39264F','#533775','#251A34','#D3AD68','#E5DBBF','#211D2A','#62432E','#AE50FF','#63BDD8','#A17A45','#BB8C61','#EFE4D6']
def write(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def uvcolor(index):return ((1024+(index%8)*128+64)/2048,((index//8)*128+64)/2048)
def rgba(h):return tuple(int(h[i:i+2],16)/255 for i in (1,3,5))+(1,)

def main():
 bpy.ops.wm.open_mainfile(filepath=str(BASE/'anime_youth.blend'),use_scripts=False)
 rig=bpy.data.objects['AnimeYouthRig'];rig.name='ArchmageRig';rig.data.pose_position='REST'
 for obj in list(bpy.data.objects):
  if obj.type=='MESH' and obj.name not in ('Face_LOD0','Hair001_LOD0','Body_LOD0'):bpy.data.objects.remove(obj,do_unlink=True)
 for action in list(bpy.data.actions):bpy.data.actions.remove(action)
 face=bpy.data.objects['Face_LOD0'];hair=bpy.data.objects['Hair001_LOD0'];hands=bpy.data.objects['Body_LOD0']
 # Keep original fully articulated hands; the civilian garments are not reused.
 if hands.data.shape_keys:hands.shape_key_clear()
 bm=bmesh.new();bm.from_mesh(hands.data)
 bmesh.ops.delete(bm,geom=[f for f in bm.faces if abs(f.calc_center_median().x)<.610],context='FACES')
 bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(hands.data);bm.free()
 hands.name='Hands';face.name='AnimeFace';hair.name='BrownBundledHair'
 # Shorten the actual neck, rather than compensating with an oversized collar.
 for obj in (face,hair):
  if obj.data.shape_keys:obj.shape_key_clear()
  for vertex in obj.data.vertices:vertex.co.z-=.044
 for obj,origin in [(face,(0,.5)),(hair,(.5,.5)),(hands,(0,0))]:
  for loop in obj.data.uv_layers.active.data:loop.uv=loop.uv*.5+Vector(origin)
 for obj in [face,hair,hands]:obj.data.materials.clear()
 image=bpy.data.images.load(str(WORK/'archmage_albedo.png'));image.pack()
 emission=bpy.data.images.load(str(WORK/'archmage_emission.png'));emission.pack()
 mat=bpy.data.materials.new('Archmage_Cel_AlbedoEmission');mat.use_nodes=True;mat.use_backface_culling=True;mat.surface_render_method='DITHERED'
 nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
 output=nodes.new('ShaderNodeOutputMaterial');shader=nodes.new('ShaderNodeBsdfPrincipled')
 tex=nodes.new('ShaderNodeTexImage');tex.image=image;em=nodes.new('ShaderNodeTexImage');em.image=emission
 links.new(tex.outputs['Color'],shader.inputs['Base Color']);links.new(tex.outputs['Alpha'],shader.inputs['Alpha'])
 links.new(em.outputs['Color'],shader.inputs['Emission Color']);shader.inputs['Emission Strength'].default_value=2
 shader.inputs['Roughness'].default_value=.9;links.new(shader.outputs[0],output.inputs['Surface'])
 for obj in [face,hair,hands]:obj.data.materials.append(mat)
 created=[]
 H='J_Bip_C_Hips';S='J_Bip_C_Spine';C='J_Bip_C_Chest';HD='J_Bip_C_Head'
 def bodyweight(p):
  z=p[2]
  if z>1.26:return {C:1}
  if z>1.07:return {S:.65,C:.35}
  return {H:1}
 def mesh(name,verts,faces,color=0,weights=None):
  data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
  obj=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(obj);data.materials.append(mat)
  uv=data.uv_layers.new(name='UVMap')
  for poly in data.polygons:
   for index in poly.loop_indices:uv.data[index].uv=uvcolor(color)
  indices={}
  for i,v in enumerate(verts):
   w=weights(v) if callable(weights) else ({weights:1} if isinstance(weights,str) else weights or bodyweight(v))
   for bone,value in w.items():
    if bone not in indices:indices[bone]=obj.vertex_groups.new(name=bone)
    indices[bone].add([i],value,'REPLACE')
  mod=obj.modifiers.new('Skin','ARMATURE');mod.object=rig;obj.parent=rig
  created.append(obj);return obj
 def tube(name,points,radii,color,weight,segments=8):
  points=[Vector(p) for p in points];verts=[]
  for i,p in enumerate(points):
   tangent=points[min(i+1,len(points)-1)]-points[max(i-1,0)]
   tangent.normalize();axis=tangent.cross(Vector((0,1,0)))
   if axis.length<.01:axis=tangent.cross(Vector((1,0,0)))
   axis.normalize();second=tangent.cross(axis).normalized()
   for j in range(segments):verts.append(tuple(p+radii[i]*(axis*math.cos(j*math.tau/segments)+second*math.sin(j*math.tau/segments))))
  faces=[]
  for i in range(len(points)-1):
   for j in range(segments):a=i*segments+j;b=i*segments+(j+1)%segments;faces.append((a,b,b+segments,a+segments))
  faces.extend([tuple(reversed(range(segments))),tuple((len(points)-1)*segments+j for j in range(segments))])
  return mesh(name,verts,faces,color,weight)
 def line(name,points,color=3,width=.004,weight=None):return tube(name,points,[width]*len(points),color,weight,5)
 def diamond(name,p,size,color=3,weight=None):
  x,y,z=p;return mesh(name,[(x,y,z+size),(x+size*.6,y,z),(x,y,z-size),(x-size*.6,y,z),(x,y-.012,z)],[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],color,weight)
 def ringform(name,rings,color,weight,segments=20,start=0,end=math.tau):
  verts=[]
  for z,rx,ry,yc in rings:
   for j in range(segments+1):
    a=start+(end-start)*j/segments;verts.append((math.sin(a)*rx,yc-math.cos(a)*ry,z))
  faces=[]
  for i in range(len(rings)-1):
   for j in range(segments):a=i*(segments+1)+j;faces.append((a,a+1,a+segments+2,a+segments+1))
  return mesh(name,verts,faces,color,weight)
 # Keep a real anatomical neck after replacing the original civilian body.
 # Front opening is deliberately below the chin; side lapels remain raised.
 def neckweight(p):
  t=max(0,min(1,(p[2]-1.35)/.096))
  return {'J_Bip_C_Neck':1-t*.55,HD:t*.55}
 tube('VisibleSkinnedNeck',[(0,.008,1.34),(0,.007,1.38),(0,-.004,1.446)],[.062,.052,.050],12,neckweight,16)
 # Black base tunic ends below the exposed neck rather than hiding the throat.
 ringform('BlackInnerTunic',[(.92,.145,.105,0),(1.14,.165,.105,0),(1.31,.185,.09,0),(1.365,.075,.060,0)],5,bodyweight)
 # Tailored trousers and boots, without hidden skin surfaces beneath them.
 for side,sgn in [('R',-1),('L',1)]:
  thigh='J_Bip_'+side+'_UpperLeg';shin='J_Bip_'+side+'_LowerLeg';foot='J_Bip_'+side+'_Foot'
  def legw(p,thigh=thigh,shin=shin):return {thigh:1} if p[2]>.57 else {shin:1}
  x=.078*sgn
  tube('BlackTrousers_'+side,[(x,0,.96),(x,0,.78),(x,0,.57),(x,.01,.32),(x,.02,.12)],[.082,.08,.061,.055,.052],5,legw,12)
  tube('LeatherBoot_'+side,[(x,-.055,.045),(x,-.045,.10),(x,.01,.25)],[.076,.070,.059],2,foot,12)
  line('BootGoldTrim_'+side,[(x-.055,-.04,.235),(x,-.055,.245),(x+.055,-.04,.235)],3,.0035,shin)
 # Coat back and sides, with open front, flared ankle-length silhouette.
 def skirtw(p):
  if p[2]>.87:return {H:1}
  side='R' if p[0]<0 else 'L'
  amount=min(.85,(.90-p[2])*1.3)
  return {H:1-amount,'J_Bip_'+side+'_UpperLeg':amount}
 outer=ringform('PurpleSplitLongRobe',[(.18,.34,.22,.025),(.40,.29,.195,.02),(.70,.225,.16,.01),(.98,.175,.12,0)],0,skirtw,28,.47,math.tau-.47)
 for sign in [-1,1]:
  edge=[(sign*.34*math.sin(.47),.025-.22*math.cos(.47),.18),(sign*.29*math.sin(.47),.02-.195*math.cos(.47),.4),(sign*.225*math.sin(.47),.01-.16*math.cos(.47),.7),(sign*.175*math.sin(.47),-.12*math.cos(.47),.98)]
  line('LongGoldFrontBorder',edge,3,.008,skirtw)
  # Ivory inner robe is split too, keeping the black trousers readable.
  mesh('IvoryFrontLining',[(sign*.035,-.128,.98),(sign*.17,-.12,.98),(sign*.28,-.19,.20),(sign*.15,-.205,.18)],[(0,1,2,3)],4,skirtw)
  line('IvoryLongGoldSeam',[(sign*.03,-.133,.98),(sign*.08,-.17,.62),(sign*.15,-.21,.18)],3,.004,skirtw)
 # Torso coat, wide pointed shoulder mantle and standing collar/lapels.
 ringform('PurpleCoatTorso',[(.94,.18,.13,0),(1.13,.185,.13,0),(1.30,.215,.115,0),(1.37,.22,.095,0)],0,bodyweight,24,.48,math.tau-.48)
 for sign,side in [(-1,'R'),(1,'L')]:
  upper='J_Bip_'+side+'_UpperArm';lower='J_Bip_'+side+'_LowerArm'
  def armw(p,upper=upper,lower=lower):return {upper:1} if abs(p[0])<.37 else {lower:1}
  # Full sleeves taper toward wrists; ivory cuff plus gold piping.
  tube('WidePurpleSleeve_'+side,[(sign*.18,0,1.36),(sign*.32,0,1.35),(sign*.48,-.005,1.35),(sign*.59,-.02,1.35)],[.105,.11,.093,.065],0,armw,12)
  tube('IvoryCuff_'+side,[(sign*.535,-.01,1.35),(sign*.585,-.02,1.35)],[.082,.071],4,lower,12)
  tube('GoldCuffBand_'+side,[(sign*.525,-.01,1.35),(sign*.54,-.01,1.35)],[.086,.084],3,lower,12)
  mantle=[(sign*.075,-.07,1.405),(sign*.22,-.10,1.405),(sign*.35,-.09,1.29),(sign*.30,-.145,1.235),(sign*.14,-.16,1.29)]
  mesh('LayeredShoulderMantle_'+side,mantle,[(0,1,2,3,4)],1,{C:.8,upper:.2})
  line('ShoulderGoldPiping_'+side,mantle+[mantle[0]],3,.007,{C:.8,upper:.2})
  diamond('ShoulderCrest_'+side,(sign*.24,-.164,1.315),.027,3,{C:1})
  lapel=[(sign*.065,-.065,1.385),(sign*.14,-.10,1.405),(sign*.19,-.13,1.32),(sign*.07,-.15,1.13),(sign*.015,-.13,1.18)]
  mesh('HighPurpleStandingCollar_'+side,lapel,[(0,1,2,3,4)],0,C)
  line('CollarGoldEdge_'+side,lapel+[lapel[0]],3,.005,C)
  white=[(sign*.026,-.142,1.36),(sign*.06,-.15,1.35),(sign*.10,-.16,1.14),(sign*.025,-.17,.98)]
  mesh('IvoryVInnerLapel_'+side,white,[(0,1,2,3)],4,bodyweight)
  diamond('CollarBlueBrooch_'+side,(sign*.126,-.165,1.305),.033,3,C)
  diamond('CollarGem_'+side,(sign*.126,-.180,1.305),.018,8,C)
 # Chest braided chain: two winding strands.
 for offset in [0,math.pi]:
  points=[]
  for i in range(25):
   t=i/24;points.append((-.12+.24*t,-.185+.003*math.sin(t*math.tau*9+offset),1.295-.055*math.sin(math.pi*t)+.003*math.cos(t*math.tau*9+offset)))
  line('BraidedGoldChestChain',points,3,.005,C)
 # Broad leather waist belt and diagonal baldric with gold buckle.
 ringform('BrownWaistBelt',[(.965,.189,.140,0),(1.025,.184,.137,0)],6,H,24)
 line('DiagonalLeatherBaldric',[(-.145,-.176,1.32),(-.06,-.185,1.18),(.145,-.16,.98)],6,.032,bodyweight)
 diamond('BaldricGoldBuckle',(-.025,-.219,1.16),.039,3,S)
 diamond('WaistGoldBuckle',(.005,-.152,.995),.045,3,H)
 diamond('WaistBlueGem',(.005,-.169,.995),.025,8,H)
 for sign in [-1,1]:
  line('WaistCharmChain',[(sign*.135,-.15,.99),(sign*.145,-.17,.9),(sign*.13,-.18,.84)],3,.003,H)
  diamond('DanglingDiamondCharm',(sign*.13,-.186,.83),.027,3,H)
  diamond('CharmVioletCrystal',(sign*.13,-.20,.83),.014,7,H)
  # Embroidered diamond motifs and branching seams on the coat tails.
  for z,x in [(.34,.23),(.52,.20),(.72,.17)]:
   diamond('RobeDiamondEmbroidery',(sign*x,-.15,z),.023,3,skirtw)
  line('RobeEmbroideredBranch',[(sign*.25,-.16,.25),(sign*.22,-.16,.40),(sign*.20,-.15,.60),(sign*.17,-.14,.82)],3,.0028,skirtw)
 # Dedicated rigid attachment bones; accessory animations counter parent rotation.
 bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
 head_bone=rig.data.edit_bones['J_Bip_C_Head']
 for bone in [head_bone]+list(head_bone.children_recursive):bone.head.z-=.044;bone.tail.z-=.044
 rig.data.edit_bones['J_Bip_C_Neck'].tail.z-=.044
 for name,parent in [('Staff','J_Bip_R_Hand'),('Book','J_Bip_L_Hand')]:
  bone=rig.data.edit_bones.new(name);bone.head=rig.data.edit_bones[parent].head;bone.tail=bone.head+Vector((0,0,.10));bone.parent=rig.data.edit_bones[parent]
 bpy.ops.object.mode_set(mode='OBJECT')
 grip=rig.data.bones['Staff'].head_local.copy();gx,gy,gz=grip
 tube('DarkWoodStaff',[(gx,gy,gz-.80),(gx,gy,gz+.70),(gx,gy,gz+.85)],[.016,.021,.026],6,'Staff',12)
 for dz in [-.58,-.25,.32,.48]:tube('StaffGoldBand',[(gx,gy,gz+dz),(gx,gy,gz+dz+.018)],[.021,.021],3,'Staff',10)
 orb=Vector((gx,gy,gz+.95))
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.085,location=orb)
 temp=bpy.context.object;verts=[tuple(temp.matrix_world@v.co) for v in temp.data.vertices];faces=[tuple(p.vertices) for p in temp.data.polygons];bpy.data.objects.remove(temp,do_unlink=True)
 mesh('VioletEmissiveStaffOrb',verts,faces,7,'Staff')
 for j in range(5):
  a=j*math.tau/5
  points=[orb+Vector((math.cos(a)*r,math.sin(a)*r,z)) for r,z in [(.025,-.16),(.10,-.09),(.12,.04),(.055,.15)]]
  tube('GoldPointedOrbClaw',points,[.016,.02,.012,.0008],3,'Staff',6)
  diamond('StaffAzureInset',tuple(orb+Vector((math.cos(a)*.09,math.sin(a)*.09,-.05))),.018,8,'Staff')
 # Purple gold-bound book, floating just beyond the left hand.
 hand=rig.data.bones['Book'].head_local;bx,by,bz=hand+Vector((.14,-.10,.15))
 def box(name,center,size,color,weight):
  x,y,z=center;w,d,h=[v/2 for v in size];v=[(x+sx*w,y+sy*d,z+sz*h) for sz in [-1,1] for sy in [-1,1] for sx in [-1,1]]
  return mesh(name,v,[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)],color,weight)
 box('MagicBookIvoryPages',(bx,by,bz),(.205,.045,.27),4,'Book')
 for y in [by-.03,by+.03]:
  box('PurpleMagicBookCover',(bx,y,bz),(.23,.012,.295),0,'Book')
  line('BookGoldCoverFrame',[(bx-.10,y-.009,bz-.13),(bx+.10,y-.009,bz-.13),(bx+.10,y-.009,bz+.13),(bx-.10,y-.009,bz+.13),(bx-.10,y-.009,bz-.13)],3,.004,'Book')
 diamond('BookGoldCrest',(bx,by-.045,bz),.055,3,'Book');diamond('BookEmissiveRune',(bx,by-.059,bz),.034,7,'Book')
 for sx in [-1,1]:
  for sz in [-1,1]:diamond('BookCornerGold',(bx+sx*.09,by-.045,bz+sz*.118),.018,3,'Book')
 # Geometric crease normals and shell-independent black outline material.
 for obj in [face,hair,hands]+created:
  for p in obj.data.polygons:p.use_smooth=obj in (face,hair,hands) or ('Trousers' in obj.name) or ('Sleeve' in obj.name)
 # Bring source scale to required 1.72m while retaining a normalized armature.
 for obj in bpy.context.scene.objects:
  if obj.type=='MESH' and obj.data.shape_keys:obj.shape_key_clear()
 feet_z=min(v.co.z for obj in created if obj.name.startswith('LeatherBoot') for v in obj.data.vertices)
 head_z=max(v.co.z for obj in (face,hair) for v in obj.data.vertices)
 scale=1.72/(head_z-feet_z)
 for obj in bpy.context.scene.objects:
  if obj.type=='MESH':
   for v in obj.data.vertices:v.co=(v.co-Vector((0,0,feet_z)))*scale
   if obj.data.shape_keys:
    for key in obj.data.shape_keys.key_blocks:
     for point in key.data:point.co*=scale
 bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
 for bone in rig.data.edit_bones:bone.head=(bone.head-Vector((0,0,feet_z)))*scale;bone.tail=(bone.tail-Vector((0,0,feet_z)))*scale
 bpy.ops.object.mode_set(mode='OBJECT')
 # Reuse audited rotation retargeter with the exact requested animation names.
 spec=importlib.util.spec_from_file_location('source_prep',ROOT/'extensions/character_cc0_v1/prepare_anime_base.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
 humanoid=json.loads((BASE/'template.json').read_text(encoding='utf-8'))['humanoidMapping']
 function=inspect.getsource(module.retarget)
 function=function.replace("dict(idle='Idle_Loop', walk='Walk_Loop', attack='Sword_Attack', hit='Hit_Chest', death='Death01')", "dict(idle='Idle_Loop',walk='Walk_Loop',run='Sprint_Loop',cast='Spell_Simple_Shoot',cast_loop='Spell_Simple_Idle_Loop',hit='Hit_Chest',crouch='Crouch_Idle_Loop',jump='Jump_Start',die='Death01',victory='Dance_Loop')")
 function=function.replace("target_name == 'death'","target_name == 'die'")
 exec(function,module.__dict__)
 records=module.retarget(rig,humanoid)
 # Victory is an authored arm raise, not a renamed dance.
 bpy.data.actions.remove(bpy.data.actions['victory'])
 victory=bpy.data.actions['idle'].copy();victory.name='victory';victory.use_fake_user=True
 for side,sign in [('right',1),('left',-1)]:
  name=humanoid[side+'UpperArm'];path='pose.bones['+json.dumps(name)+'].rotation_quaternion'
  old=[next(f for f in victory.fcurves if f.data_path==path and f.array_index==i) for i in range(4)]
  baseq=Quaternion([f.evaluate(1) for f in old]);rest=rig.data.bones[name].matrix_local.to_quaternion()
  goal=rest.inverted()@Quaternion((0,1,0),sign*.95)@rest
  for f in old:victory.fcurves.remove(f)
  curves=[victory.fcurves.new(path,index=i) for i in range(4)]
  for frame,t in [(1,0),(18,1),(45,1),(75,0)]:
   q=baseq.slerp(goal,t)
   for i,f in enumerate(curves):f.keyframe_points.insert(frame,q[i])
 for rec in records:
  if rec['name']=='victory':rec['sourceClip']='Idle_Loop plus project-authored celebratory arm raise'
 # Resample action timing and author jump, victory and prop counterrotation.
 timings={'cast':1.2,'hit':.4,'jump':.8,'cast_loop':1.6,'victory':2.0}
 for rec in records:
  action=bpy.data.actions[rec['name']];start,end=action.frame_range
  if rec['name'] in timings:
   duration=timings[rec['name']];factor=duration*30/(end-start)
   for fc in action.fcurves:
    for k in fc.keyframe_points:k.co.x=(k.co.x-start)*factor+1;k.handle_left.x=k.co.x;k.handle_right.x=k.co.x
    fc.update()
  rec['loop']=rec['name'] in ['idle','walk','run','cast_loop','crouch'];rec['durationSeconds']=(action.frame_range[1]-action.frame_range[0])/30
  rig.animation_data_create();rig.animation_data.action=action;rig.data.pose_position='POSE'
  extra={name:[action.fcurves.new('pose.bones['+json.dumps(name)+'].rotation_quaternion',index=i) for i in range(4)] for name in ['Staff','Book']}
  book_location=[action.fcurves.new('pose.bones["Book"].location',index=i) for i in range(3)]
  for name in extra:rig.pose.bones[name].rotation_mode='QUATERNION'
  for frame in range(1,int(action.frame_range[1])+1):
   bpy.context.scene.frame_set(frame)
   if rec['name']=='jump':
    bone=rig.pose.bones[humanoid['hips']];delta=rig.data.bones[humanoid['hips']].matrix_local.to_3x3().inverted()@Vector((0,0,.23*math.sin(math.pi*(frame-1)/24)))
    # Add a world-vertical jump arc to existing local hips translation curves.
    for i in range(3):
     fc=next(f for f in action.fcurves if f.data_path=='pose.bones['+json.dumps(humanoid['hips'])+'].location' and f.array_index==i)
     fc.keyframe_points.insert(frame,fc.evaluate(frame)+delta[i])
   for name in extra:
    pb=rig.pose.bones[name];target=rig.data.bones[name].matrix_local.copy();target.translation=pb.matrix.translation
    if name=='Book':
     target.translation=Vector((max(target.translation.x,.32),min(target.translation.y,-.08),min(target.translation.z,1.10)))
     target=target@Matrix.Rotation(.15*math.sin(frame*.12),4,'Z')
    pb.matrix=target;bpy.context.view_layer.update();q=pb.rotation_quaternion.copy()
    for i,fc in enumerate(extra[name]):fc.keyframe_points.insert(frame,q[i],options={'FAST'})
    if name=='Book':
     for i,fc in enumerate(book_location):fc.keyframe_points.insert(frame,pb.location[i],options={'FAST'})
  for curves in extra.values():
   for fc in curves:fc.update()
  for fc in book_location:fc.update()
 rig.animation_data.action=None
 for p in rig.pose.bones:p.rotation_quaternion=Quaternion();p.location=(0,0,0)
 rig.data.pose_position='REST';bpy.context.scene.frame_set(1)
 # Actual statistics include ALL shown accessories; outline is separate.
 bodymeshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 for obj in bodymeshes:obj.data.calc_loop_triangles()
 triangles=sum(len(o.data.loop_triangles) for o in bodymeshes)
 if triangles>15000:raise RuntimeError('Triangle budget exceeded: '+str(triangles))
 black=bpy.data.materials.new('Archmage_BlackOutline');black.diffuse_color=(.012,.008,.018,1);black.use_nodes=True;black.use_backface_culling=True
 black.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.012,.008,.018,1)
 # Cycles does not implement the viewport backface-culling flag: explicitly
 # make backfaces transparent for faithful inverted-hull preview rendering.
 bn=black.node_tree.nodes;bl=black.node_tree.links;geo=bn.new('ShaderNodeNewGeometry');mix=bn.new('ShaderNodeMixShader');transparent=bn.new('ShaderNodeBsdfTransparent')
 bl.new(geo.outputs['Backfacing'],mix.inputs[0]);bl.new(bn.get('Principled BSDF').outputs[0],mix.inputs[1]);bl.new(transparent.outputs[0],mix.inputs[2]);bl.new(mix.outputs[0],bn.get('Material Output').inputs['Surface'])
 for obj in bodymeshes:
  if obj not in [face,hair,hands]:
   bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
 shells=[]
 for obj in bodymeshes:
  if obj==face or obj==hands or 'Chain' in obj.name or 'Gold' in obj.name or 'Charm' in obj.name or 'Embroidery' in obj.name or 'Brooch' in obj.name or 'Gem' in obj.name or 'Inset' in obj.name:continue
  shell=obj.copy();shell.data=obj.data.copy();shell.name='OUTLINE_'+obj.name;bpy.context.scene.collection.objects.link(shell)
  if shell.data.shape_keys:shell.shape_key_clear()
  for v in shell.data.vertices:v.co+=v.normal*.012
  bm=bmesh.new();bm.from_mesh(shell.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(shell.data);bm.free()
  shell.data.materials.clear();shell.data.materials.append(black);shells.append(shell)
 allmeshes=bodymeshes+shells
 # Apply negative/mirrored primitive winding consistently by recalculating connected normals.
 for obj in bodymeshes:
  if obj not in [face,hair,hands]:
   bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
 # Keep the runtime character to two skinned renderers / two materials.
 def join_group(objects,name):
  bpy.ops.object.select_all(action='DESELECT')
  for obj in objects:
   if obj.data.shape_keys:obj.shape_key_clear()
   obj.select_set(True)
  bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
  obj=bpy.context.object;obj.name=name;obj.data.calc_loop_triangles();return obj
 bodymeshes=[join_group(bodymeshes,'ArchmageBody')]
 shells=[join_group(shells,'OUTLINE_Archmage')]
 allmeshes=bodymeshes+shells
 # Zero is outside every NLA strip: export a genuine neutral bind pose.
 rig.animation_data_create();rig.animation_data.action=None
 for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
 for rec in records:
  action=bpy.data.actions[rec['name']];track=rig.animation_data.nla_tracks.new();track.name=rec['name']
  strip=track.strips.new(rec['name'],1,action);strip.extrapolation='NOTHING'
 rig.data.pose_position='POSE';bpy.context.scene.frame_set(0)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for obj in allmeshes:obj.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(OUT/'archmage.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,path_mode='STRIP',embed_textures=False)
 # Preview file retained locally; no .blend/.meta/Prefab placed in game Resources.
 bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'archmage.blend'))
 for p in [WORK/'archmage_albedo.png',WORK/'archmage_emission.png']:
  import shutil;shutil.copyfile(str(p),str(OUT/p.name))
 for obj in allmeshes:obj.data.calc_loop_triangles()
 stats=dict(bodyTriangles=triangles,outlineTriangles=sum(len(o.data.loop_triangles) for o in shells),heightMeters=1.72,feetOrigin=0,boneNames=[b.name for b in rig.data.bones],humanoidMapping=humanoid,animations=records,objects=[dict(name=o.name,triangles=len(o.data.loop_triangles)) for o in bodymeshes])
 write(WORK/'build-stats.json',stats)
 print('ARCHMAGE_EXPORTED',triangles)

if __name__=='__main__':main()
