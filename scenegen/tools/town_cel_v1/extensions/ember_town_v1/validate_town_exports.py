"""Independent read-only 007 FBX audit: exported meshes, not builder claims."""
import bpy,bmesh,pathlib,sys,json,math,struct,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
args=sys.argv[sys.argv.index('--')+1:];run=pathlib.Path(args[args.index('--run')+1]);kit=json.loads((run/'kit.json').read_text(encoding='utf8'));layout=json.loads((run/'town_layout.json').read_text(encoding='utf8'));report=json.loads((run/'inspect.json').read_text(encoding='utf8'))
def G(p):return Vector((-p[0],-p[2],p[1]))
def game(p):return(-p[0],p[2],-p[1])
def png(path):
    b=path.read_bytes()[:24];assert b[:8]==b'\x89PNG\r\n\x1a\n';return list(struct.unpack('>II',b[16:24]))
records=[];buildings={b['file']:b for b in layout['buildings']};doors={};ceiling={};clear={};material_slots={}
for asset in kit['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(run/'fbx'/asset['file']),use_anim=False,use_image_search=False);obs=[o for o in bpy.data.objects if o.type=='MESH'];vertices=[];faces=[];tris=0;bad=0
    for ob in obs:
        ob.data.calc_loop_triangles();tris+=len(ob.data.loop_triangles);bad+=sum(t.area<1e-10 for t in ob.data.loop_triangles)
        if asset['atlas']!='backdrop':
            used=sorted(set(p.material_index for p in ob.data.polygons));names=[m.name if m else ''for m in ob.data.materials]
            for index in used:assert names[index].startswith('TownArchitecture'if index==0 else'TownDetails'),(asset['file'],ob.name,names)
            material_slots[asset['file']+'/'+ob.name]=names
        assert ob.matrix_world.translation.length<1e-5,(asset['file'],'nonzero pivot')
        uv=ob.data.uv_layers[0].data;assert all(-1e-6<=q.uv.x<=1.000001 and -1e-6<=q.uv.y<=1.000001 for q in uv)
        off=len(vertices);vertices.extend(ob.matrix_world@v.co for v in ob.data.vertices);faces.extend(tuple(off+i for i in p.vertices)for p in ob.data.polygons)
    assert tris==asset['triangles'],(asset['file'],tris,asset['triangles']);assert not bad,(asset['file'],bad)
    points=np.array([game(v)for v in vertices]);bounds={'min':points.min(0).tolist(),'max':points.max(0).tolist()}
    if 'bounds'in asset:
        delta=max(abs(bounds[e][i]-asset['bounds'][e][i])for e in ['min','max']for i in range(3));assert delta<.002,(asset['file'],delta)
    records.append({'file':asset['file'],'triangles':tris,'zeroAreaFaces':bad,'uv01':True,'originZero':True,'bounds':bounds})
    b=buildings.get(asset['file'])
    if b and b['enterable']:
        tree=BVHTree.FromPolygons(vertices,faces,all_triangles=False);w,d=b['size'];gaps=[]
        for x in np.arange(-w/2,w/2+.001,.025):
            hit=tree.ray_cast(G((float(x),1.5,d/2+.5)),G((0,0,-1)),1.)[0]
            if hit is None:gaps.append(float(x))
        # Find contiguous passable interval containing the door centre.
        runs=[]
        for x in gaps:
            if not runs or x-runs[-1][-1]>.027:runs.append([x])
            else:runs[-1].append(x)
        opening=next((r for r in runs if min(r)<=.03 and max(r)>=-.03),[]);width=(max(opening)-min(opening)+.025)if opening else 0
        assert width+1e-4>=b['door']['width']-.05,(b['id'],'measured door too narrow',width)
        doors[b['id']]={'declared':b['door']['width'],'measuredAtEyeHeight':round(width,4),'sampleStep':.025}
        hit=tree.ray_cast(G((0,.1,0)),G((0,1,0)),10)[0];height=game(hit)[1]if hit else 99
        assert 3<=height<10,(b['id'],'ceiling missing/too low',height);ceiling[b['id']]=height
        # Search occupied space above every point of the free walking rectangle.
        cw,ch=b['clearWalkSize'];cz=b['clearWalkCenter'][1];checks=0
        for x in np.linspace(-cw/2,cw/2,15):
            for z in np.linspace(cz-ch/2,cz+ch/2,15):
                hit=tree.ray_cast(G((float(x),.06,float(z))),G((0,1,0)),1.8)[0]
                assert hit is None,(b['id'],'occupied free floor',x,z,game(hit)if hit else None);checks+=1
        clear[b['id']]={'points':checks,'rectangle':b['clearWalkSize'],'standingClearanceVerified':True}
textures={name:png(run/name)for name in kit['textures'].values()};assert sorted(textures.values())==[[1024,1024],[2048,2048],[2048,2048]]
byfile={r['file']:r['triangles']for r in records};files=[b['file']for b in layout['buildings']]+[p['file']for p in layout['props']+layout['ground']+layout['environment']]+[layout['gate']['file'],layout['perimeter']['file']];total=sum(byfile[f]for f in files)
assert total==kit['triangles']['instancedTown'] and total<=60000
assert all(byfile[b['file']]<=6000 for b in layout['buildings'])
audit={'ok':True,'blender':bpy.app.version_string,'models':len(records),'instancedTownTriangles':total,'measuredDoors':doors,'measuredCeilingUnderside':ceiling,'freeFloorSamples':clear,'textures':textures,'materialSlotsVerified':material_slots,'records':records}
report['exportAudit']=audit;(run/'export-audit.json').write_bytes((json.dumps(audit,indent=2)+'\n').encode());(run/'inspect.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('TOWN007_EXPORT_AUDIT_PASS',total,len(records))
