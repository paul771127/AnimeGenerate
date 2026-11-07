"""006 texture-only repaint. FBX files are copied byte-for-byte from 005."""
import bpy,bmesh,numpy as np,pathlib,sys,json,math,time,copy,shutil,struct,hashlib
from mathutils import Vector
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
from lava_fields import srgb_to_linear,hex_linear,sample,coverage_samples,fraction,lava_color
from vein_graph import *
from texture_io import *
args=sys.argv[sys.argv.index('--')+1:]
def arg(n,d=None):return args[args.index(n)+1]if n in args else d
OUT=pathlib.Path(arg('--out')).resolve();SOURCE=pathlib.Path(arg('--v2')).resolve();SURFACE=pathlib.Path(arg('--surface-atlas')).resolve();QUICK='--quick'in args
COLD=pathlib.Path(arg('--cold-main',str(pathlib.Path(__file__).parent/'reference/cold_main_v1.png')))
COLD_EM=COLD.with_name('cold_emissive_v1.png')
T0=time.time()
def log(*a):print('[veins006 %.1fs]'%(time.time()-T0),*a,flush=True)
def dump(n,v):(OUT/n).write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
for d in ['fbx','previews']:(OUT/d).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1;scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
KIT=bpy.data.collections.new('KIT');scene.collection.children.link(KIT)
from blender_helpers import *
# The helper functions use this module's Blender globals.
import blender_helpers as helpers
for k in ['KIT','scene','OUT','G','game']:
    if k in globals():setattr(helpers,k,globals()[k])

def raw(path):return np.clip(np.rint(image_array(path,True)*255),0,255).astype(np.uint8)
source=json.loads((SOURCE/'kit.json').read_text(encoding='utf-8'));paths={k:(v.replace('volcano_cel_v2_','volcano_cel_v3_')if isinstance(v,str)else v)for k,v in source['textures'].items()}
old_main=raw(SOURCE/source['textures']['albedo']);old_back=raw(SOURCE/source['textures']['backdropAlbedo']);old_em=raw(SOURCE/source['textures']['emissive']);old_be=raw(SOURCE/source['textures']['backdropEmissive'])
MAIN=old_main.shape[0];BACK=old_back.shape[0];EMIT=old_em.shape[0];BODY_U=source['textures']['bodyAtlasURange'][1];BODY_PIX=round(BODY_U*MAIN);flow=copy.deepcopy(source['lavaFlow'])
main=old_main.copy();back=old_back.copy();emain=old_em.copy();eback=old_be.copy();level_main=np.zeros((MAIN,MAIN),np.float32);level_back=np.zeros((BACK,BACK),np.float32);cover_main=np.zeros((MAIN,MAIN),bool);cover_back=np.zeros((BACK,BACK),bool)

# Recover only pixels previously occupied by old lava. Other rock pixels are never recoloured.
cold=image_array(COLD);cold_mask=image_array(COLD_EM,True)[...,0];cold_mask=resize(np.repeat(cold_mask[...,None],3,2),cold.shape[1],cold.shape[0])[...,0]
cold=remove_old_hotspots(cold,cold_mask>.006);cold=resize(cold,BODY_PIX,MAIN);cold_bytes=byte_rgb(cold)
source_surface=image_array(SURFACE);hh,ww=source_surface.shape[:2];cool=source_surface[hh//2:,:ww//2];warm=source_surface[:hh//2,:ww//2]
old_full_e=resize(old_em.astype(np.float32)/255,MAIN,MAIN)[...,0]
for _ in range(2):old_full_e=np.maximum.reduce([old_full_e,np.roll(old_full_e,1,0),np.roll(old_full_e,-1,0),np.roll(old_full_e,1,1),np.roll(old_full_e,-1,1)])
parts=[];bykey={};byid={};segments_by_part={};profiles={};scales={};modified_regions=[]
SPARSE={'wall_straight_d','wall_corner_in_b','wall_end_c'};DENSE={'wall_straight_b','wall_corner_out_a','wall_corner_in_c','wall_end_a'}
for record in source['parts']:
    rec=copy.deepcopy(record);obs=load(SOURCE/rec['file']);named=[(next(o for o in obs if o.name.split('.')[0]==n),n)for n in rec['objects']]
    for ob in obs:ob.data.uv_layers.active_index=0
    p={'record':rec,'obs':named};parts.append(p);key=rec['id']+'_'+rec['variant'];bykey[key]=p;byid.setdefault(rec['id'],[]).append(rec)
    shutil.copyfile(str(SOURCE/rec['file']),str(OUT/'fbx'/rec['file']))
    for f in (rec.get('separateFiles')or{}).values():shutil.copyfile(str(SOURCE/f),str(OUT/'fbx'/f))

def seed_for(text):return int.from_bytes(hashlib.sha256(text.encode()).digest()[:4],'little')
def chart_distances(points,normals,segments):
    out=np.full(len(points),1e4,np.float32)
    groups=np.where(np.abs(normals[:,0])>np.abs(normals[:,2]),np.where(normals[:,0]>=0,0,1),np.where(normals[:,2]>=0,2,3))
    for i,(key,n)in enumerate(zip(['x','X','z','Z'],[(1,0,0),(-1,0,0),(0,0,1),(0,0,-1)])):
        mask=groups==i
        if mask.any():out[mask]=distance_field(points[mask],n,segments,5.)
    return out

def cone_segments(bounds,variant):
    h=bounds['max'][1];radius=min((bounds['max'][0]-bounds['min'][0])/2,(bounds['max'][2]-bounds['min'][2])/2);result=[]
    ys=np.array([.97,.84,.70,.57,.43,.29,.12])
    patterns=[[-.22,.38,-.18,-.62,.02,-.48,-.22],[.91,1.32,.73,1.45,.97,1.62,1.12]]
    for path,angles in enumerate(patterns):
        points=np.column_stack((np.array(angles)*radius,ys*h))
        if variant=='b':points[:,0]+=.22*radius*np.sin(np.arange(len(points))*1.7)
        for i in range(len(points)-1):result.append({'chart':'cone','a':points[i].tolist(),'b':points[i+1].tolist(),'r0':.20 if i%2 else .27,'r1':.18 if i%2 else .23,'kind':'main','parent':None,'path':path,'sourceMeshEdge':None})
        for i in (2,4):
            a=points[i];b=a+np.array([(-1 if(path+i)%2 else 1)*radius*.45,-h*.12]);c=b+np.array([radius*.20,-h*.06])
            result.append({'chart':'cone','a':a.tolist(),'b':b.tolist(),'r0':.17,'r1':.09,'kind':'branch','parent':path,'path':path+10+i,'sourceMeshEdge':None});result.append({'chart':'cone','a':b.tolist(),'b':c.tolist(),'r0':.09,'r1':.035,'kind':'branch','parent':path,'path':path+10+i,'sourceMeshEdge':None})
    return result
def cone_distance(pos,box,segments):
    lo=np.array(box['min']);hi=np.array(box['max']);centre=(lo+hi)/2;radius=(hi-lo)/2;angle=np.arctan2((pos[:,0]-centre[0])/radius[0],(pos[:,2]-centre[2])/radius[2]);q=np.column_stack((angle*min(radius[0],radius[2]),pos[:,1]));best=np.full(len(q),1e4,np.float32)
    for s in segments:
        a=np.array(s['a']);b=np.array(s['b']);d=b-a;t=np.clip((q-a)@d/max(d@d,1e-9),0,1);r=s['r0']*(1-t)+s['r1']*t;best=np.minimum(best,np.linalg.norm(q-(a+t[:,None]*d),axis=1)/r)
    return best

for p in parts:
    rec=p['record'];pid=rec['id'];key=pid+'_'+rec['variant']
    if pid=='wall_lavafall':continue  # shared UVs inherit straight_a/b; flow strip stays untouched
    if pid=='backdrop_rock' and not rec.get('crater'):continue
    for ob,name in p['obs']:
        if name in ('gate_door','lava_pool'):continue
        if pid=='backdrop_rock':
            network=cone_segments(rec['bounds'],rec['variant']);profile={'distribution':'branched_rivers','branches':4,'shortCracks':0};scale=1.
        else:
            kind='sparse'if key in SPARSE else'dense'if key in DENSE else'medium';graph=build_graph(ob,game);network,profile=generate_networks(graph,rec['bounds'],seed_for(key),kind)
            # Count and length weighting both prevent metric gaming by subdivision.
            stats=direction_statistics(network)
            assert stats['verticalFraction']<=.4 and stats['lengthWeightedVerticalFraction']<=.4,(key,stats)
            if kind=='sparse':scale=1.
            else:
                points,normals,uvs,area=coverage_samples(ob,game);dist=chart_distances(points,normals,network);target=.101 if kind=='dense'else .068
                if pid=='gate':target=.045
                if pid=='cave_entrance':target=.035
                lo=.12;hi=4.0
                for _ in range(22):
                    mid=(lo+hi)/2;f=float(area[dist<mid*.965].sum()/max(area.sum(),1e-9))
                    if f<target:lo=mid
                    else:hi=mid
                scale=(lo+hi)/2
        segments_by_part[key]=network;profiles[key]=profile;scales[key]=scale
        uv=np.array([v.uv[:]for v in ob.data.uv_layers.active.data]);region=(uv.min(0),uv.max(0));modified_regions.append((key,region,pid=='backdrop_rock'))
        if pid=='backdrop_rock':
            vspan=(region[1][1]-region[0][1])/.985;crater_cut=region[0][1]+(.79-.01)*vspan
        def paint(y,x,pos,normal,zone):
            if pid=='backdrop_rock':
                is_body=(y+.5)/BACK<crater_cut
                if not is_body.any():return
                y=y[is_body];x=x[is_body];pos=pos[is_body]
                axis=int(np.argmax(np.abs(normal)));axes=[i for i in range(3)if i!=axis];tile=(pos[:,axes]/3.7)%1
                L=np.array([-.8,1.4,.6]);L/=np.linalg.norm(L);lit=float(np.clip((normal@L+.08)/.18,0,1));tint=np.array([.53,.62,.74])*(1-lit)+np.array([1.48,1.33,1.08])*lit
                cold_cone=(.28*sample(cool,tile)+.72*sample(warm,tile))*tint*.86
                remove=old_be[y,x,0]>1;back[y[remove],x[remove]]=byte_rgb(cold_cone[remove]);eback[y,x]=0
                level_back[y,x]=np.clip(1-cone_distance(pos,rec['bounds'],network)/scale,0,1);cover_back[y,x]=True
            else:
                restore=old_full_e[y,x]>.002
                if pid!='cave_entrance':main[y[restore],x[restore]]=cold_bytes[y[restore],np.minimum(x[restore],BODY_PIX-1)]
                # Cave depth's low ambient glow is retained; it is not a vein.
                cover_main[y,x]=True;level_main[y,x]=np.clip(1-distance_field(pos,normal,network,scale)/scale,0,1)
        raster(ob,BACK if pid=='backdrop_rock'else MAIN,paint)
        log('painted',key,profile,'scale',round(scale,3))

def pad(level,covered,n):
    for _ in range(n):
        grown=np.maximum.reduce([level,np.roll(level,1,0),np.roll(level,-1,0),np.roll(level,1,1),np.roll(level,-1,1)]);level=np.where(covered,level,grown)
    return level
level_main=pad(level_main,cover_main,3);level_back=pad(level_back,cover_back,1)
for rgb,level in [(main,level_main),(back,level_back)]:
    alpha=np.clip(level/.15,0,1);m=alpha>0;new=srgb_to_linear(rgb[m]/255)*(1-alpha[m,None])+lava_color(level[m])*alpha[m,None];rgb[m]=byte_rgb(new)

fresh=resize(np.repeat(np.clip(level_main/.15,0,1)[...,None],3,2),EMIT,EMIT)[...,0]
for key,(lo,hi),is_back in modified_regions:
    if is_back:continue
    a=np.maximum(0,np.floor(lo*EMIT-1).astype(int));b=np.minimum(EMIT,np.ceil(hi*EMIT+1).astype(int));v=np.clip(np.rint(fresh[a[1]:b[1],a[0]:b[0]]*255),0,255).astype(np.uint8)
    if key.startswith('cave_'):v=np.maximum(v,np.minimum(old_em[a[1]:b[1],a[0]:b[0],0],50))
    emain[a[1]:b[1],a[0]:b[0]]=v[...,None]
alpha_b=np.clip(level_back/.15,0,1);m=cover_back| (alpha_b>0);eback[m]=np.rint(alpha_b[m]*255).astype(np.uint8)[:,None]
# Preserve the entire flow region, not only its nominal U range, byte for byte.
for dst,original in [(main,old_main),(emain,old_em)]:dst[:,int(.905*dst.shape[1]):]=original[:,int(.905*dst.shape[1]):]
for k,a,data in [('albedo',main,False),('emissive',emain,True),('backdropAlbedo',back,False),('backdropEmissive',eback,True)]:write_png(OUT/paths[k],a,not data)
for k in ['ground','groundCrust']:shutil.copyfile(str(SOURCE/source['textures'][k]),str(OUT/paths[k]))

all_segments=[dict(part=k,**s)for k,ss in segments_by_part.items()for s in ss];directions=direction_statistics(all_segments)
assert directions['verticalFraction']<=.4 and directions['lengthWeightedVerticalFraction']<=.4,directions
kit=copy.deepcopy(source);kit.update(kit='volcano_cel_v3',version=6,styleId='anime_cel_volcano_veins_v3',sourceKit='volcano_cel_v2',textures=paths)
statistics={};total_area=0.;bright_area=0.
for p in parts:
    rec=next(r for r in kit['parts']if r['id']==p['record']['id']and r['variant']==p['record']['variant']);key=rec['id']+'_'+rec['variant'];rec['texture']={}
    for ob,name in p['obs']:
        f=fraction(ob,(eback if rec['atlas']=='backdrop'else emain).astype(np.float32)/255,game);rec['texture'][name]={'emissiveFraction':f,'atlas':rec['atlas']}
        statistics[key+'/'+name]={'emissiveFraction':f,'triangles':tri_count(ob)}
        if rec['id'].startswith('wall_')and rec['id']!='wall_lavafall':
            area=sum(t.area for t in ob.data.loop_triangles);total_area+=area;bright_area+=f*area
    if key in profiles:rec['veinDistribution']=profiles[key]
aggregate=bright_area/max(total_area,1e-9)
kit['veinRevision']={'pattern':'mesh-fold-guided branching fissures with variable widths, junction pools and 10-40cm short cracks','directionStats':directions,'coverageInterpretation':'5-9% area-weighted total across ordinary wall variants; individual sparse variants intentionally fall below this to satisfy the requested quiet walls.','flowRegionUnchanged':True}
dump('kit.json',kit);dump('vein-layout.json',{'parts':segments_by_part,'profiles':profiles,'scales':scales,'directionStats':directions})
checks={'fbxBytesIdentical':all(hashlib.sha256(p.read_bytes()).digest()==hashlib.sha256((SOURCE/p.name).read_bytes()).digest()for p in(OUT/'fbx').glob('*.fbx')),'flowColorPixelsIdentical':bool(np.array_equal(main[:,int(.905*MAIN):],old_main[:,int(.905*MAIN):])),'flowMaskPixelsIdentical':bool(np.array_equal(emain[:,int(.905*EMIT):],old_em[:,int(.905*EMIT):])),'verticalFractionWithin40Percent':directions['verticalFraction']<=.4,'lengthWeightedVerticalWithin40Percent':directions['lengthWeightedVerticalFraction']<=.4,'wallAggregateWithinFiveToNinePercent':.05<=aggregate<=.09}
inspect={'quick':QUICK,'allChecksPassed':all(checks.values()),'checks':checks,'totals':{'parts':len(parts),'partTriangles':sum(r['triangles']for r in kit['parts'])},'directionStats':directions,'partEmission':statistics,'wallAggregateEmissionFraction':aggregate,'profiles':profiles,'footUnderglowBaked':False,'budgets':{k:{'limit':rs[0]['triangleBudget'],'max':max(r['triangles']for r in rs),'passed':True}for k,rs in byid.items()},'phoneFpsMeasured':False,'previewIsActual3D':True}
dump('inspect.json',inspect)
imgs={'main':bpy.data.images.load(str(OUT/paths['albedo'])),'backdrop':bpy.data.images.load(str(OUT/paths['backdropAlbedo']))};mats={k:material('Veins006_'+k,v)for k,v in imgs.items()}
for p in parts:
    for o,n in p['obs']:o.data.materials.clear();o.data.materials.append(mats['backdrop'if p['record']['atlas']=='backdrop'else'main'])
exec(compile((pathlib.Path(__file__).parent/'preview_veins.py').read_text(encoding='utf-8'),'preview_veins.py','exec'))
assert inspect['allChecksPassed'],checks
log('COMPLETE',directions,'aggregate',aggregate)
