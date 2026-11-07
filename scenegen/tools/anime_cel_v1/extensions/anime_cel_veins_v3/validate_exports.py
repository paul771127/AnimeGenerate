"""Independent, read-only FBX/GLB acceptance audit for work order 006."""
import bpy,bmesh,numpy as np,pathlib,sys,json,struct,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
from lava_fields import fraction
from vein_graph import direction_statistics
args=sys.argv[sys.argv.index('--')+1:];run=pathlib.Path(args[args.index('--run')+1]).resolve()
config=json.loads((run/'config.json').read_text(encoding='utf-8'));source=pathlib.Path(config['sourceKit'])
kit=json.loads((run/'kit.json').read_text(encoding='utf-8'));report=json.loads((run/'inspect.json').read_text(encoding='utf-8'))
def game(v):return (-v[0],v[2],-v[1])
def mask(path):
    im=bpy.data.images.load(str(path));im.colorspace_settings.name='Non-Color';a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);return a.reshape(im.size[1],im.size[0],4)[...,0].copy()
main=mask(run/kit['textures']['emissive']);back=mask(run/kit['textures']['backdropEmissive'])
sourcekit=json.loads((source/'kit.json').read_text(encoding='utf8'))
assert np.array_equal(main[:,int(.905*main.shape[1]):],mask(source/sourcekit['textures']['emissive'])[:,int(.905*main.shape[1]):]),'Exported flow mask pixels changed'
def rgb(path):
    im=bpy.data.images.load(str(path));im.colorspace_settings.name='Non-Color';a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);return a.reshape(im.size[1],im.size[0],4)[...,:3].copy()
a=rgb(run/kit['textures']['albedo']);b=rgb(source/sourcekit['textures']['albedo'])
assert np.array_equal(a[:,int(.905*a.shape[1]):],b[:,int(.905*b.shape[1]):]),'Exported flow colour pixels changed'
del a,b
records=[];coverage={}
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False,use_image_search=False)
    obs=[o for o in bpy.data.objects if o.type=='MESH']
    for o in obs:o.data.uv_layers.active_index=0
    return obs
def stat(obs):
    p=np.array([game(o.matrix_world@v.co) for o in obs for v in o.data.vertices]);tris=0;zero=0
    for o in obs:
        o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);zero+=sum(t.area<1e-8 for t in o.data.loop_triangles)
        assert o.data.uv_layers.active is not None,o.name
        assert all(-1e-6<=v.uv.x<=1.000001 and -1e-6<=v.uv.y<=1.000001 for v in o.data.uv_layers.active.data),o.name
        assert o.matrix_world.translation.length<1e-5,o.name
    return {'triangles':tris,'zeroAreaTriangles':zero,'bounds':{'min':p.min(0).tolist(),'max':p.max(0).tolist()},'objects':[o.name for o in obs]}
for part in kit['parts']:
    obs=load(run/'fbx'/part['file']);s=stat(obs);key=part['id']+'_'+part['variant']
    assert s['triangles']==part['triangles'] and s['triangles']<=part['triangleBudget'],key
    assert s['zeroAreaTriangles']==0,key
    delta=max(abs(s['bounds'][e][i]-part['bounds'][e][i])for e in ['min','max']for i in range(3));assert delta<.0016,(key,delta)
    assert abs(s['bounds']['min'][1])<.02,key
    r={'file':part['file'],'maximumBoundsDelta':delta,**s}
    if part['id'].startswith('wall_') and part['id']!='wall_lavafall':
        f=fraction(obs[0],main,game);coverage[key]=f
    if part['id']=='wall_lavafall':
        flow=next(o for o in obs if o.name=='lava_flow')
        z=max(game(flow.matrix_world@v.co)[2]for v in flow.data.vertices);assert z<=.60001,(key,z)
        # Physical flow direction: the ribbon's top has smaller V than its bottom.
        data=[(game(flow.matrix_world@flow.data.vertices[loop.vertex_index].co)[1],flow.data.uv_layers.active.data[i].uv.y)for i,loop in enumerate(flow.data.loops)]
        y=np.array([a for a,b in data]);v=np.array([b for a,b in data]);valid=y>.10
        corr=float(np.corrcoef(y[valid],v[valid])[0,1]);assert corr<-.98,(key,corr)
        r.update(lavaFlowMaxForwardM=z,flowYVCorrelation=corr,lavaFlowNamed=True)
    if part['id']=='gate':assert {'gate_frame','gate_door'}.issubset(set(s['objects']))
    if part.get('crater'):
        ob=obs[0];bm=bmesh.new();bm.from_mesh(ob.data);bm.transform(ob.matrix_world);bm.normal_update();tree=BVHTree.FromBMesh(bm)
        lo=part['bounds']['min'];hi=part['bounds']['max'];cx=(lo[0]+hi[0])/2;cz=(lo[2]+hi[2])/2
        origin=Vector((-cx,-hi[2]-6,hi[1]*.42));direction=Vector((0,1,0));hit,normal,idx,dist=tree.ray_cast(origin,direction)
        assert hit is not None and normal.y<-.15,(key,'outer slope is back-facing',normal)
        origin=Vector((-cx,-cz,hi[1]+5));hit,normal,idx,dist=tree.ray_cast(origin,Vector((0,0,-1)))
        assert hit is not None and normal.z>.95 and hit.z<hi[1]*.90,(key,'crater floor is not open/upward',normal)
        r.update(coneOuterFacesOutward=True,craterFloorFacesUpward=True,craterDepthM=hi[1]-hit.z);bm.free()
    records.append(r)
    for fname in (part.get('separateFiles')or{}).values():
        extra=stat(load(run/'fbx'/fname));assert extra['zeroAreaTriangles']==0;records.append({'file':fname,**extra})
cones=[p for p in kit['parts']if p.get('crater')]
assert len(cones)>=2 and all(p.get('lavaRiverCount',0)>=1 for p in cones)
raw=(run/'level.glb').read_bytes();length=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+length]);assert all('KHR_materials_unlit'in m.get('extensions',{})for m in doc['materials'])
assert all('bufferView'in x for x in doc.get('images',[]))
identical={p.name:hashlib.sha256(p.read_bytes()).hexdigest()==hashlib.sha256((source/p.name).read_bytes()).hexdigest()for p in(run/'fbx').glob('*.fbx')}
assert len(identical)==26 and all(identical.values()),identical
layout=json.loads((run/'vein-layout.json').read_text(encoding='utf8'));directions=direction_statistics([s for ss in layout['parts'].values()for s in ss])
assert directions['verticalFraction']<=.4 and directions['lengthWeightedVerticalFraction']<=.4
assert .05<=report['wallAggregateEmissionFraction']<=.09
audit={'ok':True,'fbxByteIdentityAgainst005':identical,'allMeshUvPivotNamesAndTrianglesUnchanged':True,'independentlyRecomputedDirections':directions,'blender':bpy.app.version_string,'logicalParts':len(kit['parts']),'fbxFiles':len(records),'uniquePartTriangles':sum(p['triangles']for p in kit['parts']),'allExportedTriangleInstances':sum(r['triangles']for r in records),'boundsPreserved':True,'uvWithin01':True,'originsZero':True,'noZeroAreaFaces':True,'wallLavaCoverage':coverage,'crateredCones':len(cones),'flowChildrenAndUvDirectionVerified':True,'glbExplicitlyUnlit':True,'glbImagesEmbedded':True,'records':records}
report['exportAudit']=audit
(run/'export-audit.json').write_bytes((json.dumps(audit,indent=2)+'\n').encode('utf-8'))
(run/'inspect.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
print('VEINS006_EXPORT_AUDIT_PASS',audit['uniquePartTriangles'],len(records))
