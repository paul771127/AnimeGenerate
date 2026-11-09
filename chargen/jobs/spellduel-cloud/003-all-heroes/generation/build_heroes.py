"""Job 003. Authored outfits/weapons on the audited CC0 head and job 002 rig.
Blender 4.5, meters. No generated portrait is substituted for an actual model.
"""
import bpy,bmesh,json,math,sys,hashlib
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
W=Path(__file__).parent;R=Path(r'E:\AI接案\SpellDuel-repo')
BASE=Path(r'E:\AI接案\rpg-world-builder\assets\characters\base\anime-youth-cc0-v1\anime_youth.blend')
SOURCE=W.parent/'archmage002/archmage002.blend'
CONFIG=json.loads((W/'classes.json').read_text('utf-8'))
CLIPS=['idle','walk','run','walk_back','walk_left','walk_right','run_back','run_left','run_right','attack','skill','skill_loop','hit','crouch','jump','die','victory']
LOOPS={'idle','walk','run','walk_back','walk_left','walk_right','run_back','run_left','run_right','skill_loop','crouch'}
H='J_Bip_C_Hips';C='J_Bip_C_Chest';S='J_Bip_C_Spine';HD='J_Bip_C_Head'

class Hero:
 def __init__(self,id):
  self.id=id;self.c=CONFIG[id];self.out=R/f'spellduel-unity/Assets/SpellDuel/Resources/CharKit/{id}_v1';self.out.mkdir(parents=True,exist_ok=True)
  self.local=W/id;self.local.mkdir(exist_ok=True);self.made=[]
  bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
  for a in list(bpy.data.actions):bpy.data.actions.remove(a)
  with bpy.data.libraries.load(str(SOURCE),link=False) as (src,dst):dst.objects=['ArchmageRig']
  self.rig=dst.objects[0];bpy.context.scene.collection.objects.link(self.rig);self.rig.name=id+'_Rig';self.rig.data.pose_position='REST'
  self.actions={a.name:a for a in bpy.data.actions}
  for t in list(self.rig.animation_data.nla_tracks):self.rig.animation_data.nla_tracks.remove(t)
  self.rig.animation_data.action=None
  with bpy.data.libraries.load(str(BASE),link=False) as (src,dst):dst.objects=['Face_LOD0','Hair001_LOD0','Body_LOD0']
  self.face,self.hair,self.hands=dst.objects
  for o in dst.objects:bpy.context.scene.collection.objects.link(o)
  original=next(m.object for m in self.face.modifiers if m.type=='ARMATURE')
  a=original.data.bones[H].head_local.z-original.data.bones['J_Bip_R_Foot'].head_local.z
  b=self.rig.data.bones[H].head_local.z-self.rig.data.bones['J_Bip_R_Foot'].head_local.z
  self.scale=b/a;self.floor=original.data.bones['J_Bip_R_Foot'].head_local.z-self.rig.data.bones['J_Bip_R_Foot'].head_local.z/self.scale
  for o in (self.face,self.hair,self.hands):
   if o.data.shape_keys:o.shape_key_clear()
  bm=bmesh.new();bm.from_mesh(self.hands.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if abs(f.calc_center_median().x)<.610],context='FACES');bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(self.hands.data);bm.free()
  for o,origin in [(self.face,(0,.5)),(self.hair,(.5,.5)),(self.hands,(0,0))]:
   # Appended objects have not yet been evaluated by the dependency graph.
   # The audited source meshes store their 180-degree facing correction in basis.
   sourceMatrix=o.matrix_basis.copy()
   for v in o.data.vertices:
    p=sourceMatrix@v.co
    if o!=self.hands:p.z-=.044
    v.co=self.D(p)
   for uv in o.data.uv_layers.active.data:uv.uv=uv.uv*.5+Vector(origin)
   o.parent=self.rig;o.matrix_world=Matrix.Identity(4)
   for m in o.modifiers:
    if m.type=='ARMATURE':m.object=self.rig
  if original!=self.rig and original.name in bpy.data.objects:bpy.data.objects.remove(original,do_unlink=True)
  # Discard unused legacy prop channels; all new attachments are palm-anchored below.
  for a in self.actions.values():
   for f in list(a.fcurves):
    if '"Staff"' in f.data_path or '"Book"' in f.data_path:a.fcurves.remove(f)
  bpy.context.view_layer.objects.active=self.rig;bpy.ops.object.mode_set(mode='EDIT')
  for name in ['Staff','Book']:
   if name in self.rig.data.edit_bones:self.rig.data.edit_bones.remove(self.rig.data.edit_bones[name])
  self.weapons={'Weapon_R':'J_Bip_R_Hand'}
  if self.c['kind'] in ['bow','longbow','dual_dagger','dual_blade']:self.weapons['Weapon_L']='J_Bip_L_Hand'
  if self.c['kind']=='sword_shield':self.weapons['Shield']='J_Bip_L_Hand'
  if id=='bishop':self.weapons['Book']='J_Bip_L_Hand'
  if self.c['kind'] in ['bow','longbow']:self.weapons.update(BowNock='J_Bip_R_Hand',Arrow='J_Bip_R_Hand')
  for name,parent in self.weapons.items():
   bone=self.rig.data.edit_bones.new(name);bone.head=self.rig.data.edit_bones[parent].head;bone.tail=bone.head+Vector((0,0,.10));bone.parent=self.rig.data.edit_bones[parent]
  bpy.ops.object.mode_set(mode='OBJECT')
  for p in self.rig.pose.bones:p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion();p.location=(0,0,0);p.scale=(1,1,1)
  self.rest={b.name:b.matrix_local.copy() for b in self.rig.data.bones}
  self.material();self.clothes();self.weapon_geometry();self.normalize();self.animations();self.finish()

 def D(self,p):return (Vector(p)-Vector((0,0,self.floor)))*self.scale
 def U(self,p):return Vector(p)/self.scale+Vector((0,0,self.floor))
 def uv(self,i):return ((1024+(i%8)*128+64)/2048,((i//8)*128+64)/2048)
 def material(self):
  self.mat=bpy.data.materials.new(self.id+'_Albedo');self.mat.use_nodes=True;self.mat.use_backface_culling=True
  n=self.mat.node_tree.nodes;l=self.mat.node_tree.links;n.clear();o=n.new('ShaderNodeOutputMaterial');p=n.new('ShaderNodeBsdfPrincipled')
  al=n.new('ShaderNodeTexImage');al.image=bpy.data.images.load(str(self.out/(self.id+'_albedo.png')));al.image.pack()
  em=n.new('ShaderNodeTexImage');em.image=bpy.data.images.load(str(self.out/(self.id+'_emission.png')));em.image.pack()
  l.new(al.outputs['Color'],p.inputs['Base Color']);l.new(al.outputs['Alpha'],p.inputs['Alpha']);l.new(em.outputs['Color'],p.inputs['Emission Color']);p.inputs['Emission Strength'].default_value=1.5;p.inputs['Roughness'].default_value=.85;l.new(p.outputs[0],o.inputs[0])
  for obj in (self.face,self.hair,self.hands):obj.data.materials.clear();obj.data.materials.append(self.mat)

 def bodyw(self,p):return {C:1} if p[2]>1.26 else ({S:.65,C:.35} if p[2]>1.07 else {H:1})
 def skirtw(self,p):
  a=min(.7,max(0,(.9-p[2])*1.05));side='R' if p[0]<0 else 'L';return {H:1-a,'J_Bip_'+side+'_UpperLeg':a}
 def mesh(self,name,verts,faces,color=0,weights=None):
  data=bpy.data.meshes.new(name);data.from_pydata([self.D(v) for v in verts],[],faces);data.update();o=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(o);data.materials.append(self.mat);uv=data.uv_layers.new(name='UVMap')
  for p in data.polygons:
   for ix in p.loop_indices:uv.data[ix].uv=self.uv(color)
  groups={}
  for i,v in enumerate(verts):
   w=weights(v) if callable(weights) else ({weights:1} if isinstance(weights,str) else weights or self.bodyw(v))
   for bone,value in w.items():
    if bone not in groups:groups[bone]=o.vertex_groups.new(name=bone)
    groups[bone].add([i],value,'REPLACE')
  m=o.modifiers.new('Skin','ARMATURE');m.object=self.rig;o.parent=self.rig;self.made.append(o);return o
 def tube(self,name,points,radii,color=0,weight=None,segments=10):
  points=[Vector(p) for p in points];vs=[]
  for i,p in enumerate(points):
   t=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized();a=t.cross(Vector((0,1,0)))
   if a.length<.01:a=t.cross(Vector((1,0,0)))
   a.normalize();b=t.cross(a).normalized()
   for j in range(segments):vs.append(p+radii[i]*(a*math.cos(j*math.tau/segments)+b*math.sin(j*math.tau/segments)))
  fs=[]
  for i in range(len(points)-1):
   for j in range(segments):a=i*segments+j;b=i*segments+(j+1)%segments;fs.append((a,b,b+segments,a+segments))
  fs.extend([tuple(reversed(range(segments))),tuple((len(points)-1)*segments+j for j in range(segments))]);return self.mesh(name,vs,fs,color,weight)
 def line(self,name,points,color=3,width=.004,weight=None):return self.tube(name,points,[width]*len(points),color,weight,5)
 def ring(self,name,rings,color=0,weight=None,segments=24,start=0,end=math.tau):
  vs=[(math.sin(a)*rx,y-math.cos(a)*ry,z) for z,rx,ry,y in rings for a in [start+(end-start)*j/segments for j in range(segments+1)]]
  fs=[]
  for i in range(len(rings)-1):
   for j in range(segments):a=i*(segments+1)+j;fs.append((a,a+1,a+segments+2,a+segments+1))
  return self.mesh(name,vs,fs,color,weight)
 def box(self,name,p,size,color=0,weight=None):
  x,y,z=p;w,d,h=[v/2 for v in size];vs=[(x+sx*w,y+sy*d,z+sz*h) for sz in [-1,1] for sy in [-1,1] for sx in [-1,1]]
  return self.mesh(name,vs,[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)],color,weight)
 def diamond(self,name,p,size,color=3,weight=None):
  x,y,z=p;return self.mesh(name,[(x,y,z+size),(x+size*.6,y,z),(x,y,z-size),(x-size*.6,y,z),(x,y-.012,z)],[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],color,weight)
 def symbol(self,p,size=.035,weight=C,kind=None,color=3):
  x,y,z=p;kind=kind or self.c.get('motif')
  if kind=='cross':
   self.box('CrossUpright',(x,y,z),(.014,.008,size*2),color,weight);self.box('CrossArms',(x,y-.002,z+size*.35),(size*1.35,.012,.014),color,weight)
  elif kind=='leaf':
   self.mesh('EmbossedLeaf',[(x,y,z-size),(x-size*.5,y-.005,z),(x+size*.1,y,z+size),(x+size*.5,y-.005,z+.002)],[(0,1,2,3)],color,weight);self.line('LeafVein',[(x,y-.008,z-size),(x,y-.01,z+size)],4,.002,weight)
  elif kind=='stitch':
   for i in range(4):self.line('HandStitch',[(x-.008,y,z+i*.012),(x+.008,y,z+i*.012+.004)],9,.0018,weight)
  elif kind=='cloud':
   pts=[(x+size*math.cos(t*.3),y,z+size*.5*math.sin(t*.3)) for t in range(20)];self.line('CloudEmbroidery',pts,8,.0025,weight)
  else:self.diamond('DiamondEmbroidery',p,size,color,weight)

 def clothes(self):
  c=self.c;length=c['length'];kind=c['kind'];robe=kind in ['staff','holy_staff','fire_staff','bishop_staff','holy_spear'];dark=2
  self.tube('AnatomicalNeck',[(0,.008,1.34),(0,.007,1.38),(0,-.004,1.446)],[.062,.052,.050],12,lambda p:{'J_Bip_C_Neck':.65,HD:.35},16)
  self.ring('FittedUndershirt',[(.91,.149,.109,0),(1.14,.167,.11,0),(1.31,.185,.095,0),(1.365,.075,.062,0)],2,self.bodyw)
  self.ring('TailoredTunic',[(.94,.18,.13,0),(1.13,.185,.13,0),(1.30,.215,.115,0),(1.36,.21,.09,0)],0,self.bodyw,24,.46,math.tau-.46)
  # Open split front with a different silhouette/hem for each class.
  flare=.34 if robe else (.28 if c.get('cape') else .235)
  self.ring('SplitCoatSkirt',[(length,flare,.20,.015),((length+.98)/2,(flare+.18)/2,.164,.01),(.98,.18,.128,0)],0,self.skirtw,28,.5,math.tau-.5)
  for sign in [-1,1]:
   hem=length+(.045 if c.get('ragged') and sign==1 else 0)
   panel=[(sign*.025,-.143,.98),(sign*.152,-.13,.98),(sign*(flare-.04),-.18,hem+.04),(sign*.095,-.205,hem)]
   self.mesh('LayeredFrontPanel',panel,[(0,1,2,3)],4 if (robe or kind in ['sword','sword_shield','long_sword']) else 1,self.skirtw)
   self.line('FrontPanelBorder',panel+[panel[0]],3,.004,self.skirtw)
   self.line('CoatTailPiping',[(sign*.085,-.141,.96),(sign*.12,-.18,(length+.96)/2),(sign*flare*.47,-.193,length)],3,.005,self.skirtw)
   for z in [length+.12,(length+.96)/2]:self.symbol((sign*(.11+(1-z)*.14),-.207,z),.027,self.skirtw)
   if c.get('ragged'):
    for j in range(4):
     x=sign*(.05+j*.045);self.mesh('FrayedHemTip',[(x,-.19,length+.05),(x+sign*.045,-.19,length+.07),(x+sign*.015,-.20,length-.025*(j%2+1))],[(0,1,2)],0,self.skirtw)
  for side,sgn in [('R',-1),('L',1)]:
   up='J_Bip_'+side+'_UpperLeg';lo='J_Bip_'+side+'_LowerLeg';foot='J_Bip_'+side+'_Foot';x=.078*sgn
   self.tube('FittedTrousers_'+side,[(x,0,.96),(x,0,.78),(x,0,.57),(x,.01,.32),(x,.02,.12)],[.082,.08,.061,.055,.052],5,lambda p,u=up,l=lo:{u:1} if p[2]>.57 else {l:1},12)
   self.tube('LeatherBoot_'+side,[(x,-.055,.045),(x,-.045,.10),(x,.01,.29)],[.076,.070,.059],6 if kind=='club' else 2,foot,12)
   self.tube('BootCuff_'+side,[(x,.01,.26),(x,.01,.29)],[.063,.064],6,lo,10)
   self.line('BootSeam_'+side,[(x,-.066,.13),(x,-.06,.24)],3,.0028,lo)
   upper='J_Bip_'+side+'_UpperArm';lower='J_Bip_'+side+'_LowerArm';sleeve=c.get('sleeves');guarded=c.get('bracers') or c.get('armor') or sleeve=='rolled';end=.38 if sleeve=='short' else (.40 if guarded else .59)
   if sleeve=='short' or guarded:
    self.tube('VisibleForearm_'+side,[(sgn*.36,0,1.35),(sgn*.50,-.01,1.35),(sgn*.59,-.02,1.35)],[.056,.048,.043],12 if sleeve=='short' else (4 if sleeve=='rolled' else 2),lambda p,u=upper,l=lower:{u:1} if abs(p[0])<.38 else {l:1},12)
   self.tube('TailoredSleeve_'+side,[(sgn*.18,0,1.36),(sgn*.30,0,1.35),(sgn*end,-.012,1.35)],[.105,.11 if sleeve=='wide' else .091,.082 if sleeve=='wide' else .06],0 if sleeve!='rolled' else 4,lambda p,u=upper,l=lower:{u:1} if abs(p[0])<.37 else {l:1},12)
   if sleeve!='short' and not guarded:self.tube('Cuff_'+side,[(sgn*.535,-.01,1.35),(sgn*.585,-.02,1.35)],[.083 if sleeve=='wide' else .063,.071 if sleeve=='wide' else .058],4 if robe else 6,lower,12)
   if c.get('bracers') or c.get('armor') or sleeve=='rolled':
    self.tube('ForearmGuard_'+side,[(sgn*.43,-.008,1.35),(sgn*.50,-.012,1.35),(sgn*.57,-.02,1.35)],[.078,.074,.057],9 if c.get('armor') else 6,lower,10)
    for j in range(3):self.tube('BracerBand_'+side,[(sgn*(.44+j*.045),-.008,1.35),(sgn*(.45+j*.045),-.008,1.35)],[.078-j*.004,.078-j*.004],3,lower,8)
   if c.get('armor') and (not c.get('asymmetric') or side=='L'):
    for j in range(3):
     p=[(sgn*(.17+j*.043),-.025,1.43-j*.032),(sgn*(.23+j*.05),-.10,1.40-j*.035),(sgn*(.31+j*.04),-.078,1.32-j*.032),(sgn*(.30+j*.045),.075,1.32-j*.032),(sgn*(.20+j*.04),.09,1.40-j*.03)]
     self.mesh('LayeredPauldron_'+side,p,[(0,1,2,3,4)],9 if self.id!='inquisitor' else 3,{C:.45,upper:.55});self.line('PauldronRim',p+[p[0]],3,.006,{C:.45,upper:.55})
   if c.get('fullArmor'):
    self.tube('SilverGreave_'+side,[(x,-.005,.20),(x,-.02,.42),(x,-.02,.52)],[.065,.075,.068],9,lo,10)
    self.diamond('KneePlate',(x,-.094,.56),.073,3,lo);self.diamond('KneeIvoryInset',(x,-.108,.56),.048,4,lo)
  # Shoulder mantles, folded hood, draped back cloak or long scarf: authored separately.
  for sign in [-1,1]:
   lapel=[(sign*.06,-.072,1.38),(sign*.14,-.10,1.40),(sign*.19,-.138,1.31),(sign*.06,-.165,1.10),(sign*.015,-.145,1.17)]
   self.mesh('FoldedCollar',lapel,[(0,1,2,3,4)],0 if not c.get('scarf') else 8,C);self.line('CollarPiping',lapel+[lapel[0]],3,.004,C)
   inner=[(sign*.022,-.147,1.36),(sign*.056,-.154,1.345),(sign*.09,-.17,1.15),(sign*.023,-.18,1.0)]
   self.mesh('VFrontLining',inner,[(0,1,2,3)],4 if not c.get('scarf') else 8,self.bodyw)
   if c.get('mantle') or c.get('cape'):
    p=[(sign*.065,-.075,1.415),(sign*.23,-.095,1.40),(sign*.34,-.07,1.30),(sign*.29,-.15,1.25),(sign*.14,-.169,1.30)]
    self.mesh('ShoulderMantle',p,[(0,1,2,3,4)],1,{C:.8,'J_Bip_'+('R' if sign<0 else 'L')+'_UpperArm':.2});self.line('MantleHem',p+[p[0]],3,.005,C);self.symbol((sign*.23,-.172,1.32),.032,C)
  if c.get('hood'):
   self.ring('FoldedHoodCowl',[(1.32,.21,.123,.044),(1.40,.19,.15,.04),(1.46,.105,.10,.05)],0,C,22,.88,math.tau-.88)
   self.line('HoodRim',[(.19*math.sin(a),.04-.15*math.cos(a),1.40) for a in [1+i*(math.tau-2)/20 for i in range(21)]],3,.004,C)
  if c.get('cape'):
   verts=[]
   for z,width,y in [(1.37,.19,.10),(1.14,.25,.18),(.86,.31,.195),(max(.22,length-.15),.39,.24)]:
    for j in range(9):
     x=(-1+j/4)*width;verts.append((x,y+.025*math.cos(j*math.pi/2),z+(.025*(j%2) if c.get('ragged') and z<.7 else 0)))
   fs=[(i*9+j,i*9+j+1,(i+1)*9+j+1,(i+1)*9+j) for i in range(3) for j in range(8)]
   self.mesh('DrapedBackCloak',verts,fs,0,lambda p:{C:.8,H:.2} if p[2]>1.0 else {C:.35,H:.65})
   self.line('CapeBottomBorder',verts[-9:],3,.005,{H:.7,C:.3})
   for sign in [-1,1]:self.line('CapeEdge',[(sign*w,y+.025,z) for z,w,y in [(1.37,.19,.1),(1.14,.25,.18),(.86,.31,.195),(max(.22,length-.15),.39,.24)]],3,.004,{C:.5,H:.5})
  if c.get('scarf'):
   self.ring('WrappedScarf',[(1.34,.083,.07,0),(1.39,.092,.074,0),(1.425,.075,.065,0)],8,C,20)
   for sign in [-1,1]:
    p=[(sign*.06,.11,1.39),(sign*.15,.16,1.32),(sign*.30,.23,1.01),(sign*.43,.27,.91),(sign*.32,.26,.86),(sign*.22,.20,1.00)]
    self.mesh('LongScarfTail',p,[(0,1,2,3,4,5)],8,{C:.6,H:.4})
  if c.get('sash'):
   self.ring('ClothWaistSash',[(.95,.19,.145,0),(1.04,.183,.14,0)],8,H,24)
   for sign in [-1,1]:self.mesh('WaistRibbon',[(sign*.12,-.15,1.0),(sign*.18,-.14,.97),(sign*.29,-.16,.56),(sign*.21,-.18,.62)],[(0,1,2,3)],8,self.skirtw)
  self.ring('LeatherWaistBelt',[(.97,.19,.142,0),(1.02,.186,.14,0)],6,H,24)
  self.line('DiagonalBaldric',[(-.15,-.18,1.33),(-.07,-.19,1.20),(.145,-.17,.98)],6,.022,self.bodyw)
  if not robe:self.line('CrossedChestHarness',[(.16,-.173,1.34),(.05,-.19,1.22),(-.145,-.168,1.0)],6,.018,self.bodyw)
  self.diamond('WaistBuckle',(0,-.164,.995),.035,3,H);self.diamond('ChestBuckle',(-.035,-.218,1.18),.029,3,S)
  for sign in [-1,1]:
   self.box('LeatherBeltPouch',(sign*.167,-.035,.91),(.075,.09,.12),6,H);self.box('PouchFlap',(sign*.17,-.086,.938),(.083,.013,.048),6,H);self.diamond('PouchStud',(sign*.17,-.098,.93),.01,3,H)
  if c.get('book'):
   if self.id=='bishop':self.make_book(self.U(self.rig.data.bones['Book'].head_local)+Vector((.06,-.07,.12)),'Book',.21)
   else:self.make_book((-.135,-.18,.90),H,.13)
  if robe or c.get('fullArmor'):
   self.line('MantleGoldChain',[(-.13+.26*i/24,-.185,1.30-.055*math.sin(math.pi*i/24)) for i in range(25)],3,.0045,C);self.symbol((0,-.195,1.255),.044,C)
  if c.get('quiver'):
   self.tube('BackLeatherQuiver',[(.17,.20,1.06),(.15,.20,1.42)],[.055,.062],6,C,12)
   self.tube('QuiverRim',[(.15,.20,1.40),(.15,.20,1.44)],[.066,.066],3,C,10)
   for j in range(5):
    x=.11+j*.02;top=1.60+.012*(j%2);self.line('QuiverArrow',[(x,.2,1.29),(x-.025,.20,top)],6,.004,C)
    self.mesh('ArrowFletching',[(x-.028,.20,top),(x-.05,.20,top-.065),(x-.027,.20,top-.09),(x-.006,.20,top-.045)],[(0,1,2,3)],0,C)
  if c.get('eyepiece'):
   self.tube('AimingMonocular',[(.065,-.115,1.56),(.065,-.145,1.56)],[.027,.03],3,HD,12);self.tube('Lens',[(.065,-.147,1.56),(.065,-.15,1.56)],[.023,.023],8,HD,12)
  if c.get('mitre'):
   self.mesh('PointedCeremonialMitre',[(-.115,-.075,1.65),(.115,-.075,1.65),(.095,-.06,1.76),(0,-.035,1.91),(-.095,-.06,1.76),(-.115,.08,1.65),(.115,.08,1.65),(0,.08,1.84)],[(0,1,2,3,4),(5,7,6),(0,5,6,1),(1,6,7,3,2),(3,7,5,0,4)],4,HD)
   self.line('MitreGoldEdge',[(-.115,-.084,1.65),(-.095,-.069,1.76),(0,-.044,1.91),(.095,-.069,1.76),(.115,-.084,1.65)],3,.007,HD)
   self.mesh('PurpleMitreInset',[(-.06,-.086,1.67),(0,-.055,1.84),(.06,-.086,1.67)],[(0,1,2)],2,HD);self.symbol((0,-.096,1.73),.04,HD,'cross')

 def make_book(self,p,bone,size):
  x,y,z=p;self.box('GrimoirePages',p,(size*.8,.04,size),4,bone)
  for dy in [-.026,.026]:
   self.box('GrimoireCover',(x,y+dy,z),(size*.9,.012,size*1.1),2,bone)
   self.line('BookGoldFrame',[(x-size*.39,y+dy-.01,z-size*.48),(x+size*.39,y+dy-.01,z-size*.48),(x+size*.39,y+dy-.01,z+size*.48),(x-size*.39,y+dy-.01,z+size*.48),(x-size*.39,y+dy-.01,z-size*.48)],3,.003,bone)
  self.symbol((x,y-.044,z),size*.2,bone)

 def blade(self,bone,length=.70,curved=False,dagger=False):
  x,y,z=self.U(self.rig.data.bones[bone].head_local);width=.033 if dagger else .045
  self.tube('LeatherWeaponGrip',[(x,y,z-.105),(x,y,z+.06)],[.019,.019],6,bone,10)
  self.diamond('Pommel',(x,y,z-.13),.028,3,bone)
  self.box('SwordCrossguard',(x,y,z+.075),(.18 if not dagger else .115,.038,.027),3,bone)
  for sign in [-1,1]:self.diamond('GuardGem',(x+sign*(.076 if not dagger else .046),y-.022,z+.076),.018,8,bone)
  vs=[]
  for dz,w,off in [(.095,width,0),(length*.70,width*.73,.045 if curved else 0)]:
   vs.extend([(x-w+off,y,z+dz),(x+off,y-.017,z+dz),(x+w+off,y,z+dz),(x+off,y+.017,z+dz)])
  vs.append((x+(.115 if curved else 0),y,z+length))
  fs=[(3,2,1,0)]+[(j,(j+1)%4,(j+1)%4+4,j+4) for j in range(4)]+[(j+4,(j+1)%4+4,8) for j in range(4)]
  self.mesh('FacetedSteelBlade',vs,fs,9,bone)
  self.line('WeaponEnergyEdge',[(x-width,y-.002,z+.11),(x-width*.73+(.045 if curved else 0),y-.002,z+length*.70),(x+(.115 if curved else 0),y,z+length)],7,.0035,bone)
 def weapon_geometry(self):
  c=self.c;k=c['kind'];r='Weapon_R';x,y,z=self.U(self.rig.data.bones[r].head_local)
  if k in ['sword','sword_shield','long_sword','ninja_sword','dual_dagger','dual_blade']:
   self.blade(r,.92 if k=='long_sword' else (.42 if k=='dual_dagger' else .67),k=='dual_blade',k=='dual_dagger')
   if k.startswith('dual'):self.blade('Weapon_L',.42 if k=='dual_dagger' else .62,k=='dual_blade',k=='dual_dagger')
  elif k=='club':
   self.tube('WoodenClub',[(x,y,z-.12),(x,y,z+.13),(x+.02,y,z+.52),(x+.01,y,z+.63)],[.02,.025,.045,.03],6,r,10)
   for dz in [-.06,-.02,.02,.06]:self.tube('ClubGripBinding',[(x,y,z+dz),(x,y,z+dz+.014)],[.023,.023],4,r,8)
   for sign in [-1,1]:self.line('WoodGrain',[(x+sign*.023,y-.025,z+.17),(x+sign*.030,y-.033,z+.4),(x+sign*.02,y-.022,z+.57)],10,.002,r)
  elif k in ['bow','longbow']:
   bone='Weapon_L';x,y,z=self.U(self.rig.data.bones[bone].head_local);size=.76 if k=='longbow' else .63
   pts=[(x,y+.20-.20*math.sin(math.pi*i/12),z-size+2*size*i/12) for i in range(13)]
   self.tube('CurvedBowLimbs',pts,[.014+.014*math.sin(math.pi*i/12) for i in range(13)],6 if k=='bow' else 0,bone,8)
   self.line('BowGoldRidge',[(px,py-.026,pz) for px,py,pz in pts],3,.007,bone)
   self.tube('BowGrip',[(x,y,z-.07),(x,y,z+.07)],[.033,.033],6,bone,10)
   for sign in [-1,1]:
    self.diamond('BowLimbGoldOrnament',(x,y+.075,z+sign*size*.62),.07,3,bone)
    self.diamond('BowLimbGem',(x,y+.06,z+sign*size*.62),.034,8,bone)
   # Nock is a separate skinned bone: the two strings actually follow the draw hand.
   nock='BowNock';nockp=self.U(self.rig.data.bones[nock].head_local)
   for sign in [-1,1]:
    endpoint=Vector((x,y+.20,z+sign*size));middle=nockp
    self.tube('FlexibleBowString',[endpoint,middle],[.0025,.0025],4,lambda p,e=endpoint,m=middle:{bone:1} if (Vector(p)-e).length<(Vector(p)-m).length else {nock:1},4)
   ar=self.U(self.rig.data.bones['Arrow'].head_local)
   self.tube('NockedArrow',[ar,ar+Vector((0,0,.67))],[.0038,.0038],6,'Arrow',6)
   self.diamond('ArrowTip',ar+Vector((0,0,.69)),.028,9,'Arrow')
   for off in [-.01,.01]:self.mesh('ArrowFeather',[ar+Vector((off,0,0)),ar+Vector((off-.015,0,.045)),ar+Vector((off,0,.085)),ar+Vector((off+.015,0,.045))],[(0,1,2,3)],0,'Arrow')
   # Weapon_R is intentionally the draw-hand socket; the bow is in Weapon_L.
  else:
   self.tube('StaffShaft',[(x,y,z-.72),(x,y,z+.68)],[.018,.022],6,r,12)
   for dz in [-.60,-.24,.26,.53]:self.tube('StaffMetalBand',[(x,y,z+dz),(x,y,z+dz+.023)],[.024,.024],3,r,10)
   top=Vector((x,y,z+.83))
   if k in ['staff','fire_staff']:
    self.sphere('EmissiveFocus',top,.075,7,r)
    for j in range(4):
     a=j*math.tau/4;pts=[top+Vector((math.cos(a)*radius,math.sin(a)*radius,dz)) for radius,dz in [(.025,-.17),(.095,-.07),(.12,.055),(.025,.18)]];self.tube('TwistedFocusClaw',pts,[.015,.020,.014,.002],3,r,7)
    if k=='fire_staff':
     for sign in [-1,1]:self.tube('SculptedFlame',[(x+sign*.04,y,z+.84),(x+sign*.07,y,z+.94),(x+sign*.04,y,z+1.04),(x+sign*.065,y,z+1.12)],[.04,.033,.022,.0005],7,r,7)
   else:
    radius=.13 if k!='bishop_staff' else .145
    pts=[top+Vector((radius*math.sin(a),0,radius*math.cos(a))) for a in [i*math.tau/32 for i in range(33)]];self.line('OpenworkSacredRing',pts,3,.012,r)
    self.symbol(top+Vector((0,-.018,0)),.095,r,'cross',3)
    self.diamond('SacredEmissiveHeart',top+Vector((0,-.036,0)),.045,7,r)
    for j in range(8):
     a=j*math.tau/8;self.line('SunburstRay',[top+Vector((math.sin(a)*radius,0,math.cos(a)*radius)),top+Vector((math.sin(a)*(radius+.065),0,math.cos(a)*(radius+.065)))],3,.007,r)
    if k=='holy_spear':
     self.diamond('GildedSpearhead',top+Vector((0,0,.21)),.16,3,r);self.diamond('SpearInlay',top+Vector((0,-.016,.21)),.105,7,r)
  if k=='sword_shield':
   bone='Shield';x,y,z=self.U(self.rig.data.bones[bone].head_local)
   # Front of shield is -Y in the neutral authored geometry.
   p=[(x-.19,y-.07,z+.34),(x+.19,y-.07,z+.34),(x+.22,y-.07,z+.12),(x+.15,y-.07,z-.19),(x,y-.10,z-.37),(x-.15,y-.07,z-.19),(x-.22,y-.07,z+.12)]
   self.mesh('KiteShield',p+[(x,y-.11,z+.03)],[(i,(i+1)%7,7) for i in range(7)],4,bone);self.line('ShieldGoldRim',p+[p[0]],3,.014,bone)
   self.mesh('ShieldBlueQuarter',[(x,y-.125,z+.31),(x+.16,y-.10,z+.31),(x+.17,y-.10,z+.10),(x,y-.13,z-.22)],[(0,1,2,3)],0,bone);self.symbol((x,y-.14,z+.04),.13,bone,'cross')
  if self.id=='ninja':
   for i in range(3):self.diamond('BeltKunai',(.13+i*.028,-.16,.91),.065,9,H)
 def sphere(self,name,p,r,color,bone):
  bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=r,location=p);o=bpy.context.object;vs=[o.matrix_world@v.co for v in o.data.vertices];fs=[tuple(p.vertices) for p in o.data.polygons];bpy.data.objects.remove(o,do_unlink=True);return self.mesh(name,vs,fs,color,bone)

 def normalize(self):
  low=min(v.co.z for o in self.made if o.name.startswith('LeatherBoot_') for v in o.data.vertices);high=max(v.co.z for v in self.hair.data.vertices);factor=1.72/(high-low)
  for o in [self.face,self.hair,self.hands]+self.made:
   for v in o.data.vertices:v.co=(v.co-Vector((0,0,low)))*factor
  bpy.context.view_layer.objects.active=self.rig;bpy.ops.object.mode_set(mode='EDIT')
  positions=[(b.name,(b.head-Vector((0,0,low)))*factor,(b.tail-Vector((0,0,low)))*factor) for b in self.rig.data.edit_bones]
  for name,head,tail in positions:self.rig.data.edit_bones[name].head=head;self.rig.data.edit_bones[name].tail=tail
  bpy.ops.object.mode_set(mode='OBJECT')
  for a in self.actions.values():
   for f in a.fcurves:
    if f.data_path.endswith('.location'):
     for k in f.keyframe_points:k.co.y*=factor;k.handle_left.y*=factor;k.handle_right.y*=factor
     f.update()
  self.floor+=low/self.scale;self.scale*=factor;self.rest={b.name:b.matrix_local.copy() for b in self.rig.data.bones}

 def path(self,n,kind='rotation_quaternion'):return 'pose.bones['+json.dumps(n)+'].'+kind
 def clean(self,a,names,types=('rotation_quaternion','location','scale')):
  paths={self.path(n,k) for n in names for k in types}
  for f in list(a.fcurves):
   if f.data_path in paths:a.fcurves.remove(f)
 def chans(self,a,names,kind='rotation_quaternion'):
  return {n:[a.fcurves.new(self.path(n,kind),index=i) for i in range(4 if kind=='rotation_quaternion' else 3)] for n in names}
 def key(self,curves,f,v):
  for i,c in enumerate(curves):k=c.keyframe_points.insert(f,float(v[i]),options={'FAST'});k.interpolation='LINEAR'
 def world(self,n,p,q):self.rig.pose.bones[n].matrix=Matrix.Translation(p)@q.to_matrix().to_4x4();bpy.context.view_layer.update()
 def ik(self,side,target,pole,rotation):
  names=['J_Bip_'+side+'_'+x for x in ['UpperArm','LowerArm','Hand']];upper,lower,end=names
  root=self.rig.pose.bones[upper].matrix.translation.copy();l1=(self.rest[lower].translation-self.rest[upper].translation).length;l2=(self.rest[end].translation-self.rest[lower].translation).length
  d=Vector(target)-root;distance=max(.01,min(d.length,l1+l2-.001));axis=d.normalized();target=root+axis*distance;across=Vector(pole)-axis*Vector(pole).dot(axis)
  if across.length<.01:across=Vector((0,-1,0))
  across.normalize();along=(l1*l1-l2*l2+distance*distance)/(2*distance);elbow=root+axis*along+across*math.sqrt(max(.00001,l1*l1-along*along))
  for n,start,tip in [(upper,root,elbow),(lower,elbow,target)]:
   direction=self.rig.data.bones[n].tail_local-self.rig.data.bones[n].head_local;q=direction.normalized().rotation_difference((tip-start).normalized())@self.rest[n].to_quaternion();self.world(n,start,q)
  self.world(end,target,rotation)
 def clone(self,base,name,frames):
  a=self.actions[base].copy();a.name=name;a.use_fake_user=True;lo,hi=a.frame_range
  for f in a.fcurves:
   for k in f.keyframe_points:k.co.x=1+(k.co.x-lo)*(frames-1)/(hi-lo);k.handle_left.x=k.co.x;k.handle_right.x=k.co.x
   f.update()
  return a
 def seal(self,a,loop):
  end=round(a.frame_range[1])
  for f in a.fcurves:
   f.update()
   if loop:
    v=f.evaluate(1)
    for k in f.keyframe_points:
     if abs(k.co.x-end)<.001:k.co.y=v
    f.update()
 def animations(self):
  rig=self.rig;rig.data.pose_position='POSE';rig.animation_data.action=self.actions['idle'];bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
  self.idleHands={s:rig.pose.bones['J_Bip_'+s+'_Hand'].matrix.copy() for s in ['R','L']}
  self.propsIdle={};kind=self.c['kind'];bow=kind in ['bow','longbow'];dual=kind.startswith('dual')
  # All attachment targets are sampled from the same palm point used by job 002.
  for n,parent in self.weapons.items():
   hand=rig.pose.bones[parent];palm=hand.matrix@Vector((0,.032,0));q=Quaternion()
   if n=='Weapon_R' and kind in ['sword','sword_shield','long_sword','ninja_sword','dual_dagger','dual_blade','club']:q=Quaternion((1,0,0),math.radians(165))
   if n=='Weapon_L' and dual:q=Quaternion((1,0,0),math.radians(170))
   if n=='Weapon_L' and bow:q=Quaternion((0,1,0),math.radians(-8))
   target=Matrix.Translation(palm)@(q@self.rest[n].to_quaternion()).to_matrix().to_4x4();self.propsIdle[n]=hand.matrix.inverted()@target
  records=[];result={}
  for name in CLIPS:
   combat=name in ['attack','skill','skill_loop'];frames=25 if name=='attack' else (31 if name=='skill' else 49)
   a=self.clone('idle',name,frames) if combat else self.actions[name]
   result[name]=a;frames=round(a.frame_range[1]);loop=name in LOOPS;rig.animation_data.action=a
   armnames=['J_Bip_'+s+'_'+b for s in ['R','L'] for b in ['UpperArm','LowerArm','Hand']]
   if combat:self.clean(a,armnames)
   self.clean(a,self.weapons)
   qs=self.chans(a,list(self.weapons)+(armnames if combat else []));ls=self.chans(a,list(self.weapons),'location');ss=self.chans(a,['Arrow'],'scale') if bow else {}
   last={}
   for frame in range(1,frames+1):
    t=(frame-1)/max(1,frames-1);bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
    # Attack anticipates, releases at ~55%, then returns; skill has a longer windup.
    if combat:
     hold=name=='skill_loop';peak=.58 if name=='attack' else .66
     if hold:amount=1
     elif t<.22:amount=.22*(t/.22)
     elif t<peak:amount=.22+.78*(t-.22)/(peak-.22)
     elif t<peak+.1:amount=1
     else:amount=max(0,1-(t-peak-.1)/(1-peak-.1))
     if bow:
      # Left hand holds the bow ahead, right hand draws the string toward cheek.
      left=self.idleHands['L'].translation.lerp(Vector((.24,-.43,1.28)),amount)
      right=self.idleHands['R'].translation.lerp(Vector((-.08,-.10,1.44)),amount)
      self.ik('L',left,(1,0,-.3),Quaternion((1,0,0),.1)@self.idleHands['L'].to_quaternion());self.ik('R',right,(-1,0,-.3),Quaternion((0,1,0),-.3)@self.idleHands['R'].to_quaternion())
     elif kind in ['staff','fire_staff','holy_staff','bishop_staff','holy_spear']:
      sacred=kind in ['holy_staff','bishop_staff','holy_spear'];raised=hold or sacred or name=='skill'
      target=Vector((-.22,-.25,1.40+.01*math.sin(t*math.tau))) if raised else Vector((-.17,-.405,1.27))
      q=Quaternion((1,0,0),math.radians(27 if raised else 78)*amount)
      self.ik('R',self.idleHands['R'].translation.lerp(target,amount),(-1,0,-.35),q@self.idleHands['R'].to_quaternion())
     else:
      # Hand path and blade orientation form a readable weapon slash/thrust.
      if kind=='dual_dagger':target=Vector((-.13,-.43,1.22));angle=math.radians(82)*amount
      elif hold:target=Vector((-.20,-.22,1.41+.008*math.sin(t*math.tau)));angle=math.radians(-22)
      else:
       swing=math.sin(math.pi*min(1,t/(.72 if name=='attack' else .82)))
       target=Vector((-.39+.63*min(1,t/.72),-.29-.13*swing,1.32-.17*min(1,t/.72)));angle=math.radians(-65+185*min(1,t/.72))*amount
      self.ik('R',self.idleHands['R'].translation.lerp(target,amount),(-1,-.2,-.1),Quaternion((1,0,0),angle)@self.idleHands['R'].to_quaternion())
      if dual:
       target=Vector((.22,-.24,1.30+.008*math.sin(t*math.tau))) if hold else (Vector((.12,-.40,1.22)) if kind=='dual_dagger' else Vector((.33-.5*t,-.35,1.28-.1*t)))
       self.ik('L',self.idleHands['L'].translation.lerp(target,amount),(1,-.2,-.1),Quaternion((1,0,0),-angle)@self.idleHands['L'].to_quaternion())
    for n,parent in self.weapons.items():
     if n in ['BowNock','Arrow']:continue
     hand=rig.pose.bones[parent];target=hand.matrix@self.propsIdle[n]
     if combat and n=='Weapon_R' and not bow:
      if kind in ['staff','fire_staff','holy_staff','bishop_staff','holy_spear']:
       lean=math.radians(27 if (name!='attack' or kind not in ['staff','fire_staff']) else 78)*amount
      elif kind=='dual_dagger':lean=math.radians(82)*amount+math.radians(165)*(1-amount)
      else:lean=angle if name=='skill_loop' else math.radians(165)*(1-amount)+angle
      q=Quaternion((1,0,0),lean)
      if kind in ['dual_blade','long_sword'] and name!='skill_loop':q=Quaternion((0,0,1),math.radians(140)*(t-.5)*amount)@q
      target=Matrix.Translation(hand.matrix@Vector((0,.032,0)))@(q@self.rest[n].to_quaternion()).to_matrix().to_4x4()
     if bow and n=='Weapon_L':
      # Keep bow vertical through drawing, grip at the left palm.
      target=Matrix.Translation(hand.matrix@Vector((0,.032,0)))@self.rest[n].to_quaternion().to_matrix().to_4x4()
     self.rig.pose.bones[n].matrix=target;bpy.context.view_layer.update()
    if bow:
     left=rig.pose.bones['Weapon_L'].matrix.translation;right=rig.pose.bones['J_Bip_R_Hand'].matrix@Vector((0,.032,0));draw=(amount if combat else 0)
     # In neutral bow geometry the grip is -0.2 in design Y from the string.
     neutral=left+Vector((0,.20*self.scale,0));nock=neutral.lerp(right,draw)
     direction=(left-nock).normalized() if draw>.05 else Vector((0,-1,0));q=Vector((0,0,1)).rotation_difference(direction)
     for n in ['BowNock','Arrow']:self.world(n,nock,(q if n=='Arrow' else Quaternion())@self.rest[n].to_quaternion())
     hidden=combat and name!='skill_loop' and .70<t<.88
     rig.pose.bones['Arrow'].scale=(.001,.001,.001) if hidden else (1,1,1);self.key(ss['Arrow'],frame,rig.pose.bones['Arrow'].scale)
    for n,curves in qs.items():
     q=rig.pose.bones[n].rotation_quaternion.copy()
     if n in last and last[n].dot(q)<0:q.negate()
     last[n]=q.copy();self.key(curves,frame,q)
    for n,curves in ls.items():self.key(curves,frame,rig.pose.bones[n].location)
   self.seal(a,loop);records.append(dict(name=name,seconds=(frames-1)/30,frames=frames,fps=30,loop=loop,inPlace=True))
  self.result=result;self.records=records
  for name,a in self.actions.items():
   if a not in result.values():a.use_fake_user=False

 def finish(self):
  body=[self.face,self.hair,self.hands]+self.made
  for obj in body:
   if obj not in [self.face,self.hair,self.hands]:
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
   obj.data.calc_loop_triangles()
   for p in obj.data.polygons:p.use_smooth=obj in [self.face,self.hair,self.hands] or any(x in obj.name for x in ['Sleeve','Trousers','Neck'])
  triangles=sum(len(o.data.loop_triangles) for o in body)
  if triangles>15000:raise RuntimeError(f'{self.id}: body triangle budget {triangles}')
  black=bpy.data.materials.new(self.id+'_Outline');black.use_nodes=True;black.use_backface_culling=True;black.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.01,.008,.016,1)
  shells=[]
  for o in body:
   if o in [self.face,self.hands] or any(s in o.name for s in ['Piping','Border','Embroidery','Seam','GoldRidge','String','Arrow','Buckle','Stud','Chain','Rim','Edge','Grain','FocusClaw','MetalBand','Stitch','Cross','Gem','Lining','Claw','Ray','Blade']):continue
   shell=o.copy();shell.data=o.data.copy();shell.name='OUTLINE_'+o.name;bpy.context.scene.collection.objects.link(shell)
   for v in shell.data.vertices:v.co+=v.normal*.008
   bm=bmesh.new();bm.from_mesh(shell.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(shell.data);bm.free();shell.data.materials.clear();shell.data.materials.append(black);shells.append(shell)
  def join(objects,name):
   bpy.ops.object.select_all(action='DESELECT')
   for o in objects:o.select_set(True)
   bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=name;o.data.calc_loop_triangles();return o
  hairHeight=max(v.co.z for v in self.hair.data.vertices)
  bodyobj=join(body,self.id+'_Body');shellobj=join(shells,'OUTLINE_'+self.id)
  self.rig.animation_data.action=None
  for t in list(self.rig.animation_data.nla_tracks):self.rig.animation_data.nla_tracks.remove(t)
  for n in CLIPS:
   a=self.result[n];track=self.rig.animation_data.nla_tracks.new();track.name=n;strip=track.strips.new(n,1,a);strip.extrapolation='NOTHING'
  for p in self.rig.pose.bones:p.rotation_quaternion=Quaternion();p.location=(0,0,0);p.scale=(1,1,1)
  self.rig.data.pose_position='POSE';bpy.context.scene.render.fps=30;bpy.context.scene.frame_set(0)
  bpy.ops.object.select_all(action='DESELECT')
  for o in [self.rig,bodyobj,shellobj]:o.select_set(True)
  bpy.context.view_layer.objects.active=self.rig
  # Assert root channels are absent rather than silently baking horizontal travel.
  for n,a in self.result.items():
   for f in a.fcurves:
    assert f.data_path not in ['location','rotation_euler','rotation_quaternion'] and not f.data_path.startswith('pose.bones["Root"]'),(n,f.data_path)
  bpy.ops.export_scene.fbx(filepath=str(self.out/(self.id+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,path_mode='STRIP',embed_textures=False)
  bpy.ops.wm.save_as_mainfile(filepath=str(self.local/(self.id+'.blend')))
  stats=dict(id=self.id,title=self.c['title'],bodyTriangles=triangles,outlineTriangles=len(shellobj.data.loop_triangles),boneCount=len(self.rig.data.bones),heightMeters=hairHeight,baseBodyHeightMeters=1.72,hasHeadgear=self.c.get('mitre',False),scale=self.scale,footOffsetDesign=self.floor,animations=self.records,bones=[b.name for b in self.rig.data.bones],weaponParents=self.weapons,rootChannelsAbsent=True,design=self.c['notes'])
  (self.local/'build-inspect.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2)+'\n','utf-8');print('HERO_EXPORTED',self.id,triangles,len(self.records),flush=True)

if __name__=='__main__':
 ids=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(CONFIG)
 for id in ids:Hero(id)
