"""Read-only audit of 005 details that 006 must not repaint."""
import pathlib,sys,json,hashlib
import bpy,numpy as np
args=sys.argv[sys.argv.index('--')+1:];run=pathlib.Path(args[args.index('--run')+1])
source=pathlib.Path(json.loads((run/'config.json').read_text(encoding='utf8'))['sourceKit'])
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from lava_fields import coverage_samples,sample
kit=json.loads((run/'kit.json').read_text(encoding='utf8'));old=json.loads((source/'kit.json').read_text(encoding='utf8'))
def rgb(path):
    im=bpy.data.images.load(str(path));im.colorspace_settings.name='Non-Color';a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);return a.reshape(im.size[1],im.size[0],4)[...,:3].copy()
maps={}
for atlas,color,em in [('main','albedo','emissive'),('backdrop','backdropAlbedo','backdropEmissive')]:
    maps[atlas]=[(rgb(source/old['textures'][key]),rgb(run/kit['textures'][key]))for key in [color,em]]
def game(v):return (-v[0],v[2],-v[1])
records=[]
for part in kit['parts']:
    if part['id']not in ['gate','cave_entrance','wall_lavafall']and not part.get('crater'):continue
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(run/'fbx'/part['file']),use_anim=False,use_image_search=False)
    obs=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    for ob in obs:
        ob.data.uv_layers.active_index=0;role=ob.name.split('.')[0]
        if role not in ['gate_door','lava_pool','lava_flow']and not part.get('crater'):continue
        pos,norm,uv,area=coverage_samples(ob,game)
        if part.get('crater'):
            selected=(norm[:,1]>.95)&(pos[:,1]<part['bounds']['max'][1]*.90);uv=uv[selected]
            if not len(uv):continue
            role='crater_pool'
        deltas=[float(np.max(np.abs(sample(a,uv)-sample(b,uv))))for a,b in maps[part['atlas']]]
        assert deltas==[0.,0.],(part['file'],role,deltas)
        records.append({'file':part['file'],'role':role,'samples':len(uv),'albedoMaxDelta':deltas[0],'emissiveMaxDelta':deltas[1]})
flow=kit['lavaFlow'];assert flow==old['lavaFlow']
grounds={k:hashlib.sha256((source/old['textures'][k]).read_bytes()).digest()==hashlib.sha256((run/kit['textures'][k]).read_bytes()).digest()for k in ['ground','groundCrust']};assert all(grounds.values())
assert len(records)==7,records
report={'ok':True,'method':'Read-only FBX UV0 interior surface samples; decoded source and delivered RGB values compared without recolouring.','preservedDetails':records,'lavaFlowDescriptorIdentical':True,'groundFilesByteIdentical':grounds}
(run/'preserved-details-audit.json').write_bytes((json.dumps(report,indent=2)+'\n').encode())
print('PRESERVED_006_DETAILS_PASS',len(records))
