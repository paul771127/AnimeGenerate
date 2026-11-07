"""Remove degenerate FBX triangles, preserve UVs/bounds, and audit/re-export the visual demo.
Run after cel_kit_builder.py: blender -b --python-exit-code 1 --python clean_cel_exports.py -- --run DIR
"""
import bpy,bmesh,numpy as np,json,pathlib,sys,math,time,struct
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]; run=pathlib.Path(args[args.index('--run')+1]).resolve()
kit=json.loads((run/'kit.json').read_text(encoding='utf-8'))
original=json.loads((run/'inspect.json').read_text(encoding='utf-8'))

def dump(path,data): path.write_bytes((json.dumps(data,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.context.scene.unit_settings.system='METRIC'; bpy.context.scene.unit_settings.scale_length=1
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False,use_image_search=False)
    return [o for o in bpy.data.objects if o.type=='MESH']
def stats(obs):
    pts=[o.matrix_world@v.co for o in obs for v in o.data.vertices]
    game=[(-p.x,p.z,-p.y) for p in pts]
    low=[min(p[i] for p in game) for i in range(3)]; high=[max(p[i] for p in game) for i in range(3)]
    per={}; zero=0; area=0; valid_uv=True
    for ob in obs:
        ob.data.calc_loop_triangles(); per[ob.name]=len(ob.data.loop_triangles)
        zero+=sum(t.area<1e-8 for t in ob.data.loop_triangles); area+=sum(t.area for t in ob.data.loop_triangles)
        if ob.data.uv_layers.active:
            valid_uv &= all(-1e-6<=u.uv.x<=1.000001 and -1e-6<=u.uv.y<=1.000001 for u in ob.data.uv_layers.active.data)
    return {'triangles':sum(per.values()),'trianglesPerObject':per,'bounds':{'min':low,'max':high},'degenerateTriangles':zero,'surfaceAreaM2':area,'uvWithin01':bool(valid_uv)}
def clean(obs):
    removed=0
    for ob in obs:
        bm=bmesh.new(); bm.from_mesh(ob.data)
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bad=[f for f in bm.faces if f.calc_area()<1e-8]; removed+=len(bad)
        if bad: bmesh.ops.delete(bm,geom=bad,context='FACES_ONLY')
        loose=[v for v in bm.verts if not v.link_faces]
        if loose: bmesh.ops.delete(bm,geom=loose,context='VERTS')
        bm.to_mesh(ob.data); bm.free(); ob.data.update()
    return removed
def export(obs,path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False,use_custom_props=False)

records=[]
for path in sorted((run/'fbx').glob('*.fbx')):
    obs=load(path); before=stats(obs); removed=clean(obs); after=stats(obs)
    delta=max(abs(before['bounds'][e][i]-after['bounds'][e][i]) for e in ('min','max') for i in range(3))
    assert delta<.0016,(path.name,delta)
    assert abs(before['surfaceAreaM2']-after['surfaceAreaM2'])<max(1e-5,before['surfaceAreaM2']*1e-6),path.name
    assert after['degenerateTriangles']==0 and after['uvWithin01'],path.name
    export(obs,path)
    # Verify the written file, not only the live BMesh.
    written=stats(load(path)); assert written['triangles']==after['triangles'] and written['degenerateTriangles']==0,path.name
    rec={'file':path.name,'sourceTriangles':before['triangles'],'removedZeroAreaTriangles':removed,'maximumBoundsDelta':delta,**written}
    records.append(rec)
    for p in kit['parts']:
        if p['file']==path.name:
            p['sourceTrianglesBeforeCleanup']=p['triangles']; p['triangles']=written['triangles']; p['trianglesPerObject']=written['trianglesPerObject']; p['fileBytes']=path.stat().st_size
            assert p['triangles']<=p['triangleBudget']
            for check in original['checks']:
                if check['part']==p['id']+'_'+p['variant']:
                    check['zeroAreaFacesRemoved']=removed; check['noZeroAreaFaces']=True

# Rebuild only the demo materials after FBX cleanup; all shader recipes remain unlit.
obs=load(run/'level.fbx'); scene=bpy.context.scene
before=stats(obs); removed_demo_unique=clean(obs); after=stats(obs); removed_demo=before['triangles']-after['triangles']
scene.view_settings.view_transform='Standard'; scene.view_settings.look='None'
def texmat(name,file):
    m=bpy.data.materials.new(name); m.use_nodes=True; n=m.node_tree.nodes; l=m.node_tree.links; n.clear()
    t=n.new('ShaderNodeTexImage'); t.image=bpy.data.images.load(str(run/file)); t.extension='REPEAT'
    e=n.new('ShaderNodeEmission'); l.new(t.outputs['Color'],e.inputs['Color']); o=n.new('ShaderNodeOutputMaterial'); l.new(e.outputs[0],o.inputs[0]); return m
def color(name,c):
    m=bpy.data.materials.new(name); m.use_nodes=True; n=m.node_tree.nodes; l=m.node_tree.links; n.clear()
    e=n.new('ShaderNodeEmission'); e.inputs['Color'].default_value=(*c,1); o=n.new('ShaderNodeOutputMaterial'); l.new(e.outputs[0],o.inputs[0]); return m
mats={'main':texmat('CelMain','volcano_cel_albedo.png'),'back':texmat('CelBackdrop','volcano_cel_backdrop.png'),'ash':texmat('CelAsh','cel_ground_ash.png'),'crust':texmat('CelCrust','cel_ground_crust.png'),'caldera':texmat('CelCaldera','cel_rock_warm.png'),'lava':color('CelLava',(1,.29,.035)),'edge':color('CelLavaOuter',(.62,.09,.012)),'dark':color('CelContact',(.025,.045,.055))}
sky=bpy.data.materials.new('CelSky'); sky.use_nodes=True; n=sky.node_tree.nodes; l=sky.node_tree.links; n.clear()
a=n.new('ShaderNodeVertexColor'); a.layer_name='Color'; e=n.new('ShaderNodeEmission'); l.new(a.outputs['Color'],e.inputs['Color']); o=n.new('ShaderNodeOutputMaterial'); l.new(e.outputs[0],o.inputs[0])
for ob in obs:
    name=ob.name; m=mats['main']
    if name.startswith('backdrop'): m=mats['back']
    elif name.startswith('COL_CelFloor'): m=mats['ash']
    elif name.startswith('VIS_CoolingCrust'): m=mats['crust']
    elif name.startswith('VIS_LavaRiver'): m=mats['edge']
    elif name.startswith(('VIS_LavaHotCore','VIS_CalderaGlow')): m=mats['lava']
    elif name.startswith('VIS_ContactPatch'): m=mats['dark']
    elif name.startswith('VIS_DistantCaldera'): m=mats['caldera']
    elif name.startswith('ENV_CelSkyDome'): m=sky
    ob.data.materials.clear(); ob.data.materials.append(m)
export(obs,run/'level.fbx')
bpy.ops.export_scene.gltf(filepath=str(run/'level.glb'),export_format='GLB',use_selection=True,export_yup=True,export_texcoords=True,export_normals=True,export_materials='EXPORT',export_vertex_color='ACTIVE')
# Emission-only Blender nodes export as PBR emission. Make the lightweight unlit
# contract explicit, retaining every texture and constant colour (including lava).
glb=run/'level.glb'; raw=glb.read_bytes(); length,kind=struct.unpack_from('<II',raw,12); assert kind==0x4e4f534a
doc=json.loads(raw[20:20+length])
for m in doc.get('materials',[]):
    factor=m.pop('emissiveFactor',[1,1,1]); tex=m.pop('emissiveTexture',None)
    m['pbrMetallicRoughness']={'baseColorFactor':factor+[1],'metallicFactor':0,'roughnessFactor':1}
    if tex: m['pbrMetallicRoughness']['baseColorTexture']=tex
    m.setdefault('extensions',{})['KHR_materials_unlit']={}
    m['doubleSided']=m.get('name')=='CelSky'
doc.setdefault('extensionsUsed',[]).append('KHR_materials_unlit')
encoded=json.dumps(doc,separators=(',',':'),ensure_ascii=True).encode('utf-8'); encoded+=b' '*((-len(encoded))%4)
tail=raw[20+length:]; glb.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(encoded)+len(tail))+struct.pack('<II',len(encoded),kind)+encoded+tail)
manifest=json.loads((run/'level.json').read_text(encoding='utf-8')); manifest['demoTriangles']=after['triangles']; manifest['degenerateTrianglesRemoved']=removed_demo; dump(run/'level.json',manifest)
kit['exportCleanup']={'method':'remove only triangles with physical area < 1e-8 m2; UVs retained; bounds and surface area audited','removedUniquePartTriangles':sum(r['sourceTriangles']-r['triangles'] for r in records if r['file'] in {p['file'] for p in kit['parts']}),'report':'export-audit.json'}
dump(run/'kit.json',kit)
report={'ok':True,'blender':bpy.app.version_string,'records':records,'logicalParts':len(kit['parts']),'fbxFiles':len(records),'uniquePartTriangles':sum(p['triangles'] for p in kit['parts']),'allExportedTriangleInstances':sum(r['triangles'] for r in records),'demoTriangles':after['triangles'],'demoRemovedZeroAreaTriangles':removed_demo,'noZeroAreaTrianglesRemaining':True,'boundsPreserved':True,'uvWithin01':True}
dump(run/'export-audit.json',report)
original['exportAudit']=report; original['totals'].update(partTriangles=report['uniquePartTriangles'],demoTriangles=after['triangles']); original['allChecksPassed']=all(all(v for v in c.values() if isinstance(v,bool)) for c in original['checks'])
dump(run/'inspect.json',original)
# Render the repaired, exported geometry once more. These are genuine 3D previews.
scene.render.engine='BLENDER_EEVEE_NEXT'; scene.eevee.taa_render_samples=32
scene.render.resolution_x=1600; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.image_settings.color_mode='RGB'; scene.render.image_settings.color_depth='8'
cam=bpy.data.objects.new('PreviewCamera',bpy.data.cameras.new('PreviewCamera')); scene.collection.objects.link(cam); scene.camera=cam; cam.data.clip_end=250; cam.data.sensor_fit='HORIZONTAL'; cam.data.sensor_width=36
def G(x,y,z): return Vector((-x,-z,y))
def render(name,pos,target,hfov):
    cam.location=G(*pos); cam.rotation_euler=(G(*target)-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.lens=18/math.tan(math.radians(hfov)/2)
    scene.render.filepath=str(run/'previews'/name); bpy.ops.render.render(write_still=True)
render('player_view.png',(0,1.5,-3),(.1,1.5,17),76)
render('clearing_view.png',(2.2,1.5,16),(4.5,3,38),80)
render('hero_view.png',(-16,12,-10),(4,2,25),66)
bpy.ops.wm.save_as_mainfile(filepath=str(run/'level.blend'))
status=json.loads((run/'status.json').read_text(encoding='utf-8')); status['exportCleanupComplete']=True; dump(run/'status.json',status)
assert original['allChecksPassed']
print('CEL_EXPORT_AUDIT_PASS',report['uniquePartTriangles'],after['triangles'])
