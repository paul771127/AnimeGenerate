import bpy,math,json
from pathlib import Path
from mathutils import Vector
W=Path(__file__).parent;R=Path(r'E:\AI接案\SpellDuel-repo');J=R/'chargen/jobs/spellduel-cloud/001-archmage-hero/previews'
bpy.ops.wm.open_mainfile(filepath=str(W/'archmage.blend'),use_scripts=False)
rig=bpy.data.objects['ArchmageRig'];rig.data.pose_position='POSE'
for track in rig.animation_data.nla_tracks:track.mute=True
rig.animation_data.action=bpy.data.actions['idle'];bpy.context.scene.frame_set(30)
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT'
for obj in bpy.data.objects:
 if obj.type=='MESH' and obj.name.startswith('OUTLINE_'):obj.visible_shadow=False
black=bpy.data.materials['Archmage_BlackOutline'];black.use_backface_culling=True
if hasattr(black,'use_backface_culling_shadow'):black.use_backface_culling_shadow=True
# Real two-band lighting for model previews. Unity must use the supplied shader contract.
mat=bpy.data.materials['Archmage_Cel_AlbedoEmission'];n=mat.node_tree.nodes;l=mat.node_tree.links
albedo=next(x for x in n if x.type=='TEX_IMAGE' and 'albedo' in x.image.name);em=next(x for x in n if x.type=='TEX_IMAGE' and 'emission' in x.image.name)
diff=n.new('ShaderNodeBsdfDiffuse');rgb=n.new('ShaderNodeShaderToRGB');ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.interpolation='CONSTANT'
ramp.color_ramp.elements[0].color=(.58,.55,.68,1);ramp.color_ramp.elements[1].position=.45;ramp.color_ramp.elements[1].color=(1,1,1,1)
l.new(diff.outputs[0],rgb.inputs[0]);l.new(rgb.outputs[0],ramp.inputs[0]);mul=n.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1
l.new(albedo.outputs['Color'],mul.inputs[1]);l.new(ramp.outputs['Color'],mul.inputs[2]);add=n.new('ShaderNodeMixRGB');add.blend_type='ADD';add.inputs[0].default_value=1
l.new(mul.outputs[0],add.inputs[1]);l.new(em.outputs['Color'],add.inputs[2]);emit=n.new('ShaderNodeEmission');l.new(add.outputs[0],emit.inputs['Color'])
alpha=n.new('ShaderNodeMixShader');tr=n.new('ShaderNodeBsdfTransparent');l.new(albedo.outputs['Alpha'],alpha.inputs[0]);l.new(tr.outputs[0],alpha.inputs[1]);l.new(emit.outputs[0],alpha.inputs[2]);l.new(alpha.outputs[0],n.get('Material Output').inputs[0])
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.25,.28,.36,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.7
for location,power,size in [((2,-4,4),500,4),((-3,1,3),350,3)]:
 bpy.ops.object.light_add(type='AREA',location=location);bpy.context.object.data.energy=power;bpy.context.object.data.size=size
scene.render.resolution_x=800;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.2
for name,loc,target in [('front',(0,-5,1.1),(0,0,.95)),('back',(0,5,1.1),(0,0,.95)),('side',(5,0,1.1),(0,0,.95)),('third_person_back_3m_h1.5m',(0,3,1.5),(0,-1,.85))]:
 cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
 cam.data.type='PERSP' if name.startswith('third') else 'ORTHO';cam.data.lens=40
 scene.render.filepath=str(J/(name+'.png'));bpy.ops.render.render(write_still=True)
cam.data.type='ORTHO';cam.location=(2.3,-5,1.6);cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler()
for name in ['walk','run','cast','cast_loop','hit','crouch','jump','die','victory']:
 action=bpy.data.actions[name];rig.animation_data.action=action;scene.frame_set(round(sum(action.frame_range)/2))
 scene.render.filepath=str(J/('pose_'+name+'.png'));bpy.ops.render.render(write_still=True)
print('PREVIEWS_COMPLETE')
