"""Executed by the town builder. Actual geometry previews, no concept-image substitution."""
for obs in assets.values():
    for o in obs:o.hide_render=True
PREVIEW=bpy.data.collections.new('PREVIEW');scene.collection.children.link(PREVIEW);placed=[]
def instance(file,entry):
    for o in assets[file]:
        c=o.copy();c.data=o.data.copy();c.hide_render=False;c.hide_viewport=False;PREVIEW.objects.link(c);p=entry.get('pos',[0,0]);c.location=G((p[0],entry.get('height',0),p[1]));c.rotation_euler=(0,0,math.radians(-entry.get('yaw',0)));scale=entry.get('scale',1);c.scale=(scale,scale,scale);placed.append(c)
for b in layout['buildings']:instance(b['file'],b)
for p in layout['props']+layout['ground']+layout['environment']:instance(p['file'],p)
instance(layout['perimeter']['file'],layout['perimeter']);instance(layout['gate']['file'],layout['gate'])
before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(OUT/'preview_npc.fbx'),use_anim=False,use_image_search=False);markers=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
for o in markers:
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);indices=[p.material_index for p in o.data.polygons];o.data.materials.clear()
    for m in mats:o.data.materials.append(m)
    for poly,index in zip(o.data.polygons,indices):poly.material_index=min(index,1)
    o.hide_render=True
for n in layout['npcs']:
    for o in markers:
        c=o.copy();c.data=o.data.copy();c.hide_render=False;PREVIEW.objects.link(c);c.location=G((n['pos'][0],.02,n['pos'][1]));c.rotation_euler=(0,0,math.radians(-n['yaw']));placed.append(c)
# Sky uses vertex color only; the consuming game already has this gradient.
d=Draft('ENV_TownSky');n=48;rows=12
for j in range(rows-1):
    aa=-.12+j*(math.pi/2+.11)/(rows-1);bb=-.12+(j+1)*(math.pi/2+.11)/(rows-1)
    for i in range(n):
        a=i*2*math.pi/n;b=(i+1)*2*math.pi/n
        p=[(100*math.cos(t)*math.cos(phi),100*math.sin(t),5+100*math.cos(t)*math.sin(phi))for t,phi in [(aa,a),(aa,b),(bb,b),(bb,a)]]
        d.face(p,'stone',tuple(-v for v in p[0]))
sky=d.object([mats[0]],PREVIEW);attrs=sky.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');lo=np.array(rgba('#cca894')[:3]);hi=np.array(rgba('#5c94ad')[:3])
for poly in sky.data.polygons:
    for li in poly.loop_indices:
        y=game(sky.data.vertices[sky.data.loops[li].vertex_index].co)[1];t=np.clip(y/60,0,1);attrs.data[li].color=(*((1-t)*lo+t*hi),1)
m=bpy.data.materials.new('SkyGradient');m.use_nodes=True;nd=m.node_tree.nodes;lk=m.node_tree.links;nd.clear();v=nd.new('ShaderNodeVertexColor');v.layer_name='Color';e=nd.new('ShaderNodeEmission');lk.new(v.outputs['Color'],e.inputs['Color']);out=nd.new('ShaderNodeOutputMaterial');lk.new(e.outputs[0],out.inputs[0]);sky.data.materials.clear();sky.data.materials.append(m);placed.append(sky)
export([sky],OUT/'preview_sky.fbx')
scene.render.engine='BLENDER_EEVEE_NEXT';scene.eevee.taa_render_samples=12 if QUICK else 32;scene.render.resolution_percentage=45 if QUICK else 100
world=bpy.data.worlds.new('TownSky');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=rgba('#cca894')
cam=bpy.data.objects.new('TownPreviewCamera',bpy.data.cameras.new('TownCamera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.sensor_fit='HORIZONTAL';cam.data.sensor_width=36;cam.data.clip_end=220;cam.data.clip_start=.05
def view(name,position,target,hfov,w=1600,h=900):
    cam.location=G(position);cam.rotation_euler=(G(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=18/math.tan(math.radians(hfov)/2);scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/'previews'/('blender_'+name+'.png'));bpy.ops.render.render(write_still=True)
view('overview',(22,27,-23),(0,1,5),51,2048,1400);view('spawn_view',(0,1.5,0),(0,1.5,10),80)
for id in ['smith','shop','guild']:
    b=next(x for x in layout['buildings']if x['id']==id);p=transform([0,b['size'][1]/2+.35],b['pos'],b['yaw']);t=transform([0,-2.0],b['pos'],b['yaw']);view(id+'_inside',(p[0],1.5,p[1]),(t[0],1.5,t[1]),83)
view('east_exit',(2,1.5,5),(19,1.8,5),80)
export(placed,OUT/'town_preview.fbx');bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'town.blend'))
dump('preview-cameras.json',{'overview':{'position':[22,27,-23],'target':[0,1,5],'hfov':51},'spawn':{'position':[0,1.5,0],'target':[0,1.5,10],'hfov':80},'east':{'position':[2,1.5,5],'target':[19,1.8,5],'hfov':80},'humanMarkersArePreviewOnly':True})
