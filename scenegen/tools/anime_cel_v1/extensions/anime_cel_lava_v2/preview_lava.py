"""Executed by cel_lava_builder.py after kit export; genuine geometry previews."""
for p in parts:
    for ob,_ in p['obs']:ob.hide_render=True;ob.hide_viewport=True
PREVIEW=bpy.data.collections.new('PREVIEW');scene.collection.children.link(PREVIEW);placed=[]
scene.render.engine='BLENDER_EEVEE_NEXT';scene.eevee.taa_render_samples=16 if QUICK else 40
scene.render.resolution_percentage=45 if QUICK else 100
world=bpy.data.worlds.new('Cel005Sky');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(*hex_linear('#cca894'),1)
cam=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.sensor_fit='HORIZONTAL';cam.data.sensor_width=36;cam.data.clip_end=250
def look(position,target,hfov):
    cam.location=G(*position);cam.rotation_euler=(G(*target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=18/math.tan(math.radians(hfov)/2)
def render(name,w=1600,h=900):
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/'previews'/(name+'.png'));bpy.ops.render.render(write_still=True)
def place(ob,x,y,z,yaw=0,scale=1):
    c=ob.copy();c.hide_render=False;c.hide_viewport=False;PREVIEW.objects.link(c);c.location=G(x,y,z);c.rotation_euler=(0,0,math.radians(-yaw));c.scale=(scale,scale,scale);placed.append(c);return c
def put(key,x,z,yaw=0,scale=1):return [place(o,x,0,z,yaw,scale) for o,_ in bykey[key]['obs']]
def clear():
    for o in placed:bpy.data.objects.remove(o,do_unlink=True)
    placed.clear()
def preview_mesh(name,v,f,uv=None,mat=None):
    coords=uv or [[(0,0)]*len(face) for face in f];o=make_mesh(name,v,f,coords);link(o,PREVIEW)
    if mat:o.data.materials.append(mat)
    placed.append(o);return o
ash=material('GroundAsh',bpy.data.images.load(str(OUT/paths['ground'])))
def floor(x0,x1,z0,z1):
    return preview_mesh('COL_CelFloor',[(x0,0,z0),(x1,0,z0),(x1,0,z1),(x0,0,z1)],[(0,1,2,3)],[[(x0/3,z0/3),(x1/3,z0/3),(x1/3,z1/3),(x0/3,z1/3)]],ash)
def sky():
    n=32;rows=10;v=[];f=[]
    for j in range(rows):
        a=-.12+j*(math.pi/2+.12)/(rows-1)
        for i in range(n):
            t=2*math.pi*i/n;v.append((130*math.cos(a)*math.cos(t),130*math.sin(a),30+130*math.cos(a)*math.sin(t)))
    for j in range(rows-1):
        for i in range(n):f.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    o=preview_mesh('ENV_CelSkyDome',v,f);at=o.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    lo=hex_linear('#cca894');hi=hex_linear('#5c94ad')
    for poly in o.data.polygons:
        for li,vi in zip(poly.loop_indices,poly.vertices):
            t=np.clip((v[vi][1]+4)/55,0,1);c=lo*(1-t)+hi*t;at.data[li].color=(*c,1)
    o.data.color_attributes.active_color=at
    m=bpy.data.materials.new('Sky005');m.use_nodes=True;nd=m.node_tree.nodes;lk=m.node_tree.links;nd.clear();a=nd.new('ShaderNodeVertexColor');a.layer_name='Color';e=nd.new('ShaderNodeEmission');lk.new(a.outputs['Color'],e.inputs['Color']);out=nd.new('ShaderNodeOutputMaterial');lk.new(e.outputs[0],out.inputs[0]);o.data.materials.append(m)
    return o

# Every logical category, all variants together.
for pid,records in byid.items():
    clear();total=sum(r['size'][0] for r in records)+1.4*(len(records)-1);x=-total/2;h=max(r['size'][1] for r in records);depth=max(r['size'][2] for r in records)
    for r in records:
        mid=(r['bounds']['min'][0]+r['bounds']['max'][0])/2
        put(pid+'_'+r['variant'],x+r['size'][0]/2-mid,0);x+=r['size'][0]+1.4
    d=max(total,h*1.8)*1.22+depth
    look((-.24*d,.38*d+h*.25,d),(0,h*.42,0),54);render('part_'+pid,1440,810)
clear();floor(-40,40,-14,85);sky()
# The cel-v1 camera and approach are preserved. The fall replaces one ordinary wall.
for j,z in enumerate((1,5,9)):
    put('wall_lavafall_a' if j==1 else 'wall_straight_'+'abcd'[j%4],-2.8,z,90)
    put('wall_lavafall_b' if j==2 else 'wall_straight_'+'bcda'[j%4],2.8,z,-90)
put('wall_end_a',-2.7,12,90);put('wall_end_b',2.8,12,-90)
for j,z in enumerate((17,21,25,29,33)):put('wall_straight_'+'abcd'[j%4],-7,z,90)
for j,z in enumerate((21,25,29,33)):put('wall_straight_'+'dcba'[j%4],15,z,-90)
put('gate_a',1.2,24,180);put('cave_entrance_a',1.2,38,180)
put('backdrop_rock_a',-.8,61,180);put('backdrop_rock_b',22,76,160)
put('backdrop_rock_c',-22,61,145,.75);put('backdrop_rock_d',26,57,-135,.8)
look((0,1.5,-3),(.1,1.5,17),76);render('player_view')
look((2.2,1.5,16),(4.5,3,38),80);render('clearing_view')
look((-16,12,-10),(4,2,25),66);render('hero_view')
look((1.2,1.5,30),(1.2,1.7,41),70);render('cave_view')
demo_tris=sum(tri_count(o) for o in placed);inspect['totals']['demoTriangles']=demo_tris
export(placed,OUT/'level.fbx')
bpy.ops.export_scene.gltf(filepath=str(OUT/'level.glb'),export_format='GLB',use_selection=True,export_yup=True,export_materials='EXPORT',export_vertex_color='ACTIVE')
glb=OUT/'level.glb';raw=glb.read_bytes();length,kind=struct.unpack_from('<II',raw,12);doc=json.loads(raw[20:20+length])
for m in doc.get('materials',[]):
    factor=m.pop('emissiveFactor',[1,1,1]);tex=m.pop('emissiveTexture',None);m['pbrMetallicRoughness']={'baseColorFactor':factor+[1],'metallicFactor':0,'roughnessFactor':1}
    if tex:m['pbrMetallicRoughness']['baseColorTexture']=tex
    m.setdefault('extensions',{})['KHR_materials_unlit']={};m['doubleSided']=m.get('name')=='Sky005'
doc.setdefault('extensionsUsed',[]).append('KHR_materials_unlit');enc=json.dumps(doc,separators=(',',':')).encode('utf-8');enc+=b' '*((-len(enc))%4);tail=raw[20+length:];glb.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(enc)+len(tail))+struct.pack('<II',len(enc),kind)+enc+tail)
dump('level.json',{'schemaVersion':1,'styleId':'anime_cel_volcano_lava_v2','units':'metres','up':'+Y','sourceGameSpawn':[0,1.5,-3],'glbSpawn':[0,1.5,-3],'glbCoordinateNote':'glTF positions = (-gameX,gameY,gameZ); FBX uses the established Unity Y-up/+Z convention.','camera':{'position':[0,1.5,-3],'target':[.1,1.5,17],'horizontalFov':76},'demoTriangles':demo_tris,'lavaFlow':flow,'dynamicFlowNote':'GLB preview is static; host shader applies the documented V scroll. The Unity verification project supplies an example flow shader.','gameplay':'visual example; game supplies collision, hazards, movement and tracking'})
dump('inspect.json',inspect);look((0,1.5,-3),(.1,1.5,17),76);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'level.blend'))
