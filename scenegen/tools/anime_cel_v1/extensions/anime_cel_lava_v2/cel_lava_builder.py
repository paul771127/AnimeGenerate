"""SpellDuel 005: preserve the cel-v1 rock bake; bake geometry-space lava veins.
No orange wall-foot ambient term is baked. A dedicated main-atlas U strip serves lava_flow.
"""
import bpy,bmesh,numpy as np,pathlib,sys,json,math,time,copy,shutil,struct
from mathutils import Vector
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
from lava_fields import *
args=sys.argv[sys.argv.index('--')+1:]
def arg(n,d=None):return args[args.index(n)+1] if n in args else d
OUT=pathlib.Path(arg('--out')).resolve();SOURCE=pathlib.Path(arg('--v2')).resolve();SURFACE=pathlib.Path(arg('--surface-atlas')).resolve();QUICK='--quick'in args
MAIN=1024 if QUICK else 4096;BACK=512 if QUICK else 1024;EMIT=512 if QUICK else 1024
BODY_PIX=int(MAIN*.90);BODY_U=BODY_PIX/MAIN;FLOW_U=(.912,.988)
T0=time.time()
def log(*a):print('[lava005 %.1fs]'%(time.time()-T0),*a,flush=True)
def G(x,y,z):return Vector((-x,-z,y))
def game(v):return (-v[0],v[2],-v[1])
def dump(name,data):(OUT/name).write_bytes((json.dumps(data,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
for d in ('fbx','previews'): (OUT/d).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
KIT=bpy.data.collections.new('KIT');scene.collection.children.link(KIT)
def link(o,col=KIT):
    for c in list(o.users_collection):c.objects.unlink(o)
    col.objects.link(o);return o
def image_array(path,data=False):
    im=bpy.data.images.load(str(path));im.colorspace_settings.name='Non-Color' if data else 'sRGB'
    a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);a=a.reshape(im.size[1],im.size[0],4)
    return a[...,:3] if data else srgb_to_linear(a[...,:3])
def resize(a,w,h):
    im=bpy.data.images.new('technical_resample',a.shape[1],a.shape[0],alpha=False,float_buffer=True)
    q=np.concatenate((a,np.ones(a.shape[:2]+(1,),np.float32)),axis=2);im.pixels.foreach_set(q.ravel());im.scale(w,h)
    out=np.empty(w*h*4,np.float32);im.pixels.foreach_get(out);bpy.data.images.remove(im);return out.reshape(h,w,4)[...,:3]
def save(name,a,data=False):
    if a.ndim==2:a=np.repeat(a[...,None],3,axis=2)
    h,w=a.shape[:2];im=bpy.data.images.new(name,w,h,alpha=False,float_buffer=True);im.colorspace_settings.name='Non-Color' if data else 'sRGB'
    q=np.concatenate((a,np.ones((h,w,1),np.float32)),axis=2);im.pixels.foreach_set(np.ascontiguousarray(q,dtype=np.float32).ravel())
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGB';scene.render.image_settings.color_depth='8';scene.render.image_settings.compression=95
    scene.view_settings.view_transform='Raw' if data else 'Standard';im.save_render(str(OUT/name),scene=scene);scene.view_settings.view_transform='Standard'
    bpy.data.images.remove(im);return bpy.data.images.load(str(OUT/name))
def tri_count(o):o.data.calc_loop_triangles();return len(o.data.loop_triangles)
def bounds(obs):
    p=np.asarray([game(v.co) for o in obs for v in o.data.vertices]);return {'min':p.min(0).tolist(),'max':p.max(0).tolist()}
def load(path):
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False,use_image_search=False)
    obs=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    for o in obs:link(o)
    return obs
def make_mesh(name,verts,faces,uv_faces,zones=None):
    me=bpy.data.meshes.new(name);me.from_pydata([G(*p) for p in verts],[],faces);me.update()
    layer=me.uv_layers.new(name='UVMap')
    for poly,coords in zip(me.polygons,uv_faces):
        for li,uv in zip(poly.loop_indices,coords):layer.data[li].uv=uv
    if zones is not None:
        at=me.attributes.new('lava_zone','INT','FACE')
        for x,v in zip(at.data,zones):x.value=v
    ob=bpy.data.objects.new(name,me);KIT.objects.link(ob);return ob

source=json.loads((SOURCE/'kit.json').read_text(encoding='utf-8'));parts=[];bykey={}
src_main=image_array(SOURCE/source['textures']['albedo']);src_e=image_array(SOURCE/source['textures']['emissive'],True)[...,0]
src_back=image_array(SOURCE/source['textures']['backdropAlbedo'])
main=np.empty((MAIN,MAIN,3),np.float32);main[:]=hex_linear('#2c3842');main[:,:BODY_PIX]=resize(src_main,BODY_PIX,MAIN)
base_e=np.zeros((MAIN,MAIN),np.float32);base_e[:,:BODY_PIX]=resize(np.repeat(src_e[...,None],3,2),BODY_PIX,MAIN)[...,0]
back=resize(src_back,BACK,BACK);back_level=np.zeros((BACK,BACK),np.float32)
main_level=np.zeros((MAIN,MAIN),np.float32);main_coverage=np.zeros((MAIN,MAIN),bool);back_coverage=np.zeros((BACK,BACK),bool)

def cone(rec,rect):
    lo=np.array(rec['bounds']['min']);hi=np.array(rec['bounds']['max']);span=hi-lo;n=40;verts=[];faces=[];uv=[];zones=[]
    # Open crater: outer slope, rim, inner wall and recessed molten floor.
    rad=[1.,.73,.31,.25,.20,.16];height=[0.,.38,.88,1.,.96,.84];vrow=[.01,.34,.70,.79,.87,.97]
    for j,(r,y) in enumerate(zip(rad,height)):
        for i in range(n):
            a=2*math.pi*i/n;rr=r*(1+.055*math.sin(a*3+.3)+.025*math.sin(a*7))
            yy=y*hi[1] if j!=3 else hi[1]-(.06+.045*math.sin(a*5))*hi[1]
            if j==0:yy=0
            verts.append((rr*math.sin(a),yy,rr*math.cos(a)))
    pp=np.array(verts);pp[:,0]=lo[0]+(pp[:,0]-pp[:,0].min())/(pp[:,0].max()-pp[:,0].min())*span[0];pp[:,2]=lo[2]+(pp[:,2]-pp[:,2].min())/(pp[:,2].max()-pp[:,2].min())*span[2]
    pp[3*n,1]=hi[1];verts=pp.tolist();verts.append(((lo[0]+hi[0])/2,hi[1]*.84,(lo[2]+hi[2])/2));centre=len(verts)-1
    u0,v0,u1,v1=rect
    def U(u,v):return (u0+(u1-u0)*u,v0+(v1-v0)*v)
    for j in range(5):
        for i in range(n):
            k=(i+1)%n;faces.append((j*n+i,j*n+k,(j+1)*n+k,(j+1)*n+i));uv.append([U(i/n,vrow[j]),U((i+1)/n,vrow[j]),U((i+1)/n,vrow[j+1]),U(i/n,vrow[j+1])]);zones.append(1 if j==3 else 2 if j==4 else 0)
    for i in range(n):
        faces.append((5*n+i,5*n+(i+1)%n,centre));uv.append([U(i/n,.97),U((i+1)/n,.97),U((i+.5)/n,.995)]);zones.append(3)
    # sin(theta),cos(theta) runs clockwise in Blender after G(). Reverse both
    # geometry and UV loop order so Unity back-face culling sees the outer slope.
    return make_mesh('backdrop_rock_'+rec['variant'],verts,[tuple(reversed(f)) for f in faces],[list(reversed(u)) for u in uv],zones)

log('loading protected cel-v1 source kit')
for rec0 in source['parts']:
    rec=copy.deepcopy(rec0);obs=load(SOURCE/rec['file']);named=[]
    for name in rec['objects']:named.append((next(o for o in obs if o.name.split('.')[0]==name),name))
    if rec['id']=='backdrop_rock' and rec['variant'] in ('a','b'):
        old=obs[0];coords=np.array([x.uv[:] for x in old.data.uv_layers.active.data]);a=coords.min(0)+.003;b=coords.max(0)-.003
        bpy.data.objects.remove(old,do_unlink=True);ob=cone(rec,(a[0],a[1],b[0],b[1]));named=[(ob,'backdrop_rock_'+rec['variant'])]
        rec['geometryStyle']='cratered_volcano_cone';rec['lavaRiverCount']=2;rec['crater']=True
    p={'record':rec,'obs':named};parts.append(p);bykey[rec['id']+'_'+rec['variant']]=p
    for ob,name in named:
        ob['part_key']=rec['id']+'_'+rec['variant'];ob['object_role']=name
        if rec['id']!='backdrop_rock':
            old=ob.data.uv_layers.active;src=ob.data.uv_layers.new(name='SourceUV')
            for d,v in zip(src.data,old.data):d.uv=v.uv
            ob.data.uv_layers.active=old
            for d in old.data:d.uv.x*=BODY_U

def lava_ribbon(base,variant):
    box=bounds([base]);top=box['max'][1]*.90;rows=18;verts=[];faces=[];uv=[]
    for i in range(rows):
        t=i/(rows-1);y=top*(1-t)+.08*t;cx=.12*math.sin(t*6+(0 if variant=='a' else 1));w=.23+.07*math.sin(t*5+.3)**2
        for side in (-1,1):
            x=cx+side*w/2;hit,loc,_,_=base.ray_cast(G(x,y,8),G(0,0,-1))
            z=game(loc)[2]+.028 if hit else .28
            verts.append((x,y,min(.595,z)))
        if i:
            faces.append((2*i-2,2*i-1,2*i+1,2*i));uv.append([(FLOW_U[0],(i-1)/(rows-1)*.84),(FLOW_U[1],(i-1)/(rows-1)*.84),(FLOW_U[1],t*.84),(FLOW_U[0],t*.84)])
    start=len(verts);n=16;verts.append((0,.035,.40))
    for i in range(n):
        a=2*math.pi*i/n;verts.append((.42*math.cos(a),.035,.40+.20*math.sin(a)))
    for i in range(n):
        ids=(start,start+i+1,start+(i+1)%n+1);faces.append(ids)
        uv.append([(FLOW_U[0]+(FLOW_U[1]-FLOW_U[0])*(verts[k][0]/.84+.5),.85+.14*(verts[k][2]-.20)/.4) for k in ids])
    return make_mesh('lava_flow',verts,faces,uv)
for variant in ('a','b'):
    src=bykey['wall_straight_'+variant];base=src['obs'][0][0].copy();KIT.objects.link(base);base.name='wall_rock';flow=lava_ribbon(base,variant)
    rec=copy.deepcopy(src['record']);rec.update(id='wall_lavafall',file='wall_lavafall_'+variant+'.fbx',triangleBudget=700,objects=['wall_rock','lava_flow'],children=['lava_flow'],separateFiles=None,basePart='wall_straight_'+variant)
    p={'record':rec,'obs':[(base,'wall_rock'),(flow,'lava_flow')],'skip_paint':True};parts.append(p);bykey['wall_lavafall_'+variant]=p

# Cave floor flow stays inside the existing cave footprint; only the lip extends to +0.6m.
cave=bykey['cave_entrance_a'];verts=[];faces=[];uv=[];rows=24
for i in range(rows):
    t=i/(rows-1);z=-5.6+t*6.2;cx=.16*math.sin(z*1.3);w=.25+.20*(1-t)
    verts.extend([(cx-w,.04,z),(cx+w,.04,z)])
    if i:faces.append((2*i-2,2*i-1,2*i+1,2*i));uv.append([(FLOW_U[0],(i-1)/(rows-1)),(FLOW_U[1],(i-1)/(rows-1)),(FLOW_U[1],t),(FLOW_U[0],t)])
cave['obs'].append((make_mesh('lava_pool',verts,faces,uv),'lava_pool'));cave['record']['children']=['lava_pool']

def raster(ob,size,callback):
    ob.data.calc_loop_triangles();layer=ob.data.uv_layers.active.data;zones=ob.data.attributes.get('lava_zone')
    for t in ob.data.loop_triangles:
        uv=np.array([layer[i].uv[:] for i in t.loops],np.float64)*size-.5
        xmin=max(0,int(math.ceil(uv[:,0].min())));xmax=min(size-1,int(math.floor(uv[:,0].max())));ymin=max(0,int(math.ceil(uv[:,1].min())));ymax=min(size-1,int(math.floor(uv[:,1].max())))
        if xmin>xmax or ymin>ymax:continue
        v0=uv[1]-uv[0];v1=uv[2]-uv[0];det=v0[0]*v1[1]-v1[0]*v0[1]
        if abs(det)<1e-9:continue
        yy,xx=np.mgrid[ymin:ymax+1,xmin:xmax+1];dx=xx-uv[0,0];dy=yy-uv[0,1]
        a=(dx*v1[1]-v1[0]*dy)/det;b=(v0[0]*dy-dx*v0[1])/det;ok=(a>=-1e-6)&(b>=-1e-6)&(a+b<=1.000001)
        if not ok.any():continue
        weights=np.stack((1-a-b,a,b),-1)[ok];co=np.array([game(ob.data.vertices[k].co) for k in t.vertices]);pts=weights@co
        callback(yy[ok],xx[ok],pts,np.array(game(t.normal)),zones.data[t.polygon_index].value if zones else 0)

widths={};calibration={}
for p in parts:
    rec=p['record'];pid=rec['id'];variant=rec['variant'];seed=ord(variant)-97
    if p.get('skip_paint') or pid=='backdrop_rock':continue
    for ob,name in p['obs']:
        if name=='lava_pool':continue
        box=bounds([ob]);kind='wall' if pid.startswith('wall_') else 'frame'
        width=.10
        if pid.startswith('wall_'):
            points,normals,uvs,area=coverage_samples(ob,game);dist=curve_distance(points,normals,box,seed,'wall');old_uv=uvs.copy();old_uv[:,0]/=BODY_U;old=sample(src_e[...,None],old_uv)[...,0]
            lo=.008;hi=.70
            for _ in range(24):
                mid=(lo+hi)/2;f=float(area[(dist<mid*.97)|(old>.25)].sum()/max(area.sum(),1e-9))
                if f<.078:lo=mid
                else:hi=mid
            width=(lo+hi)/2;widths[pid+'_'+variant]=width;calibration[pid+'_'+variant]={'outerHalfWidthMetres':width,'targetFraction':.078}
        def paint(y,x,pos,normal,zone):
            level=np.zeros(len(x),np.float32)
            if pid.startswith('wall_'):level=np.clip(1-curve_distance(pos,normal,box,seed,'wall')/width,0,1)
            elif pid=='gate' and name=='gate_door':level=door_level(pos)
            elif pid=='gate':level=np.clip(1-curve_distance(pos,normal,box,seed,'frame')/.11,0,1)
            elif pid=='cave_entrance':
                inside=np.clip((-pos[:,2]-1.4)/4.8,0,1)*np.clip((2.2-np.abs(pos[:,0]))/.5,0,1)*np.clip((3.4-pos[:,1])/1.9,0,1)
                warm=inside*.30;main[y,x]=main[y,x]*(1-warm[:,None])+ORANGE*.32*warm[:,None]
                base_e[y,x]=np.maximum(base_e[y,x],inside*.12)
            main_level[y,x]=np.maximum(main_level[y,x],level);main_coverage[y,x]=True
        raster(ob,MAIN,paint)
        log('painted',pid,variant,name,'half-width',round(width,3))

# Cone rock comes from the same original surface artwork and the same two-band light idea.
source_surface=image_array(SURFACE);hh,ww=source_surface.shape[:2];cool=source_surface[hh//2:,:ww//2];warm=source_surface[:hh//2,:ww//2]
L=np.array(game(G(-.8,1.4,.6).normalized()));L=L/np.linalg.norm(L)
for variant in ('a','b'):
    p=bykey['backdrop_rock_'+variant];ob=p['obs'][0][0];box=bounds([ob])
    def paint_cone(y,x,pos,normal,zone):
        ax=int(np.argmax(np.abs(normal)));axes=[i for i in range(3) if i!=ax];uv=(pos[:,axes]/3.7)%1
        r=.28*sample(cool,uv)+.72*sample(warm,uv)
        lit=float(np.clip((normal@L+.08)/.18,0,1));tint=np.array([.53,.62,.74])*(1-lit)+np.array([1.48,1.33,1.08])*lit
        back[y,x]=r*tint*.86
        back_level[y,x]=cone_level(pos,box,zone);back_coverage[y,x]=True
    raster(ob,BACK,paint_cone)
    log('painted cone',variant)

def pad(level,covered,steps=3):
    for _ in range(steps):
        v=np.maximum.reduce([level,np.roll(level,1,0),np.roll(level,-1,0),np.roll(level,1,1),np.roll(level,-1,1)])
        level=np.where(covered,level,v)
    return level
padding=max(1,int(round(MAIN/1024)))
# Calibrate against the actual filtered atlas, not only the analytic curve field.
# Padding and low-resolution emission masks otherwise inflate the apparent width.
for iteration in range(5):
    padded=pad(main_level,main_coverage,padding)
    candidate=np.maximum(base_e,np.clip(padded/.12,0,1))
    small=resize(np.repeat(candidate[...,None],3,2),EMIT,EMIT)[...,0]
    phone=resize(np.repeat(small[...,None],3,2),512,512)[...,0]
    adjusted=False
    for key,width in list(widths.items()):
        ob=bykey[key]['obs'][0][0];f=fraction(ob,small,game);fp=fraction(ob,phone,game)
        if .064<=f<=.096 and .064<=fp<=.099:continue
        adjusted=True;uv=np.array([v.uv[:] for v in ob.data.uv_layers.active.data]);a=np.maximum(0,np.floor(uv.min(0)*MAIN-padding-1).astype(int));b=np.minimum(MAIN,np.ceil(uv.max(0)*MAIN+padding+2).astype(int))
        main_level[a[1]:b[1],a[0]:b[0]]=0
        widths[key]=width*float(np.clip(min(.075/max(f,1e-5),.085/max(fp,1e-5)),.50,1.4));box=bounds([ob]);seed=ord(bykey[key]['record']['variant'])-97
        def repaint(y,x,pos,normal,zone):main_level[y,x]=np.clip(1-curve_distance(pos,normal,box,seed,'wall')/widths[key],0,1)
        raster(ob,MAIN,repaint)
    log('atlas coverage calibration',iteration,'adjusted',adjusted)
    if not adjusted:break
main_level=pad(main_level,main_coverage,padding);back_level=pad(back_level,back_coverage,2)
alpha=np.clip(main_level/.12,0,1);main=main*(1-alpha[...,None])+lava_color(main_level)*alpha[...,None];emit=np.maximum(base_e,alpha)
aa=np.clip(back_level/.12,0,1);back=back*(1-aa[...,None])+lava_color(back_level)*aa[...,None];back_emit=aa
# A vertically periodic flow swatch occupies the reserved U strip, not a new atlas.
x0=int(.905*MAIN);xx=np.linspace(0,1,MAIN-x0,dtype=np.float32)[None,:];vv=np.linspace(0,1,MAIN,dtype=np.float32)[:,None]
flow_level=np.clip(1-np.abs(xx-.5+.075*np.sin(vv*math.tau)+.025*np.sin(vv*math.tau*3))/ .56,0,1)
main[:,x0:]=lava_color(flow_level);emit[:,x0:]=.75+.25*flow_level
em_small=resize(np.repeat(emit[...,None],3,2),EMIT,EMIT)[...,0]
be_small=resize(np.repeat(back_emit[...,None],3,2),BACK,BACK)[...,0]
paths={'albedo':'volcano_cel_v2_albedo.png','backdropAlbedo':'volcano_cel_v2_backdrop_albedo.png','emissive':'volcano_cel_v2_emissive.png','backdropEmissive':'volcano_cel_v2_backdrop_emissive.png','ground':'cel_ground_ash.png','groundCrust':'cel_ground_crust.png'}
imgs={'main':save(paths['albedo'],main),'backdrop':save(paths['backdropAlbedo'],back)}
save(paths['emissive'],em_small,True);save(paths['backdropEmissive'],be_small,True)
for key in ('ground','groundCrust'):shutil.copyfile(str(SOURCE/source['textures'][key]),str(OUT/paths[key]))
log('atlases written')

def material(name,image):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;n.clear();t=n.new('ShaderNodeTexImage');t.image=image;e=n.new('ShaderNodeEmission');l.new(t.outputs['Color'],e.inputs['Color']);o=n.new('ShaderNodeOutputMaterial');l.new(e.outputs[0],o.inputs[0]);return m
mats={k:material('Lava005_'+k,v) for k,v in imgs.items()}
for p in parts:
    for ob,name in p['obs']:ob.data.materials.clear();ob.data.materials.append(mats['backdrop' if p['record']['id']=='backdrop_rock' else 'main'])
def ensure_name(o,n):
    other=bpy.data.objects.get(n)
    if other is not None and other is not o:other.name=n+'_other'
    o.name=n
def export(obs,path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.hide_viewport=False;o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False,use_custom_props=False)

checks=[];glow={};kit_parts=[]
phone_mask=resize(np.repeat(em_small[...,None],3,2),512,512)[...,0]
for p in parts:
    rec=p['record'];obs=[o for o,_ in p['obs']]
    for o,name in p['obs']:ensure_name(o,name)
    rec['sourceBaseTriangles']=rec['triangles'];rec.pop('sourceTrianglesBeforeCleanup',None)
    if 'colour'in rec:rec['sourceV1ColourStatistics']=rec.pop('colour')
    rec['objects']=[n for _,n in p['obs']];rec['children']=[n for _,n in p['obs'][1:]];rec['trianglesPerObject']={n:tri_count(o) for o,n in p['obs']};rec['triangles']=sum(rec['trianglesPerObject'].values());rec['bounds']=bounds(obs);rec['size']=[rec['bounds']['max'][i]-rec['bounds']['min'][i] for i in range(3)]
    rec['maxFaceAreaM2']=max(poly.area for ob in obs for poly in ob.data.polygons)
    rec['texture']={};atlas='backdrop' if rec['id']=='backdrop_rock' else 'main';rec['atlas']=atlas
    for o,n in p['obs']:
        f=fraction(o,be_small if atlas=='backdrop' else em_small,game)
        rec['texture'][n]={'emissiveFraction':f,'atlas':atlas}
        if rec['id'].startswith('wall_') and rec['id']!='wall_lavafall':glow[rec['id']+'_'+rec['variant']]={'surfaceFraction':f,'phone512Fraction':fraction(o,phone_mask,game),'outerHalfWidthMetres':widths[rec['id']+'_'+rec['variant']]}
    path=OUT/'fbx'/rec['file'];export(obs,path);rec['fileBytes']=path.stat().st_size
    if rec['id']=='gate':
        rec['separateFiles']={}
        for o,n in p['obs']:
            name=n+'_'+rec['variant']+'.fbx';export([o],OUT/'fbx'/name);rec['separateFiles'][n]=name
    ck={'part':rec['id']+'_'+rec['variant'],'trianglesWithinBudget':rec['triangles']<=rec['triangleBudget'],'uvWithin01':all(-1e-6<=v.uv.x<=1.000001 and -1e-6<=v.uv.y<=1.000001 for o in obs for v in o.data.uv_layers.active.data),'pivotOnGround':abs(rec['bounds']['min'][1])<.02}
    if rec['id'].startswith('wall_') and rec['id']!='wall_lavafall':
        ck['veinsWithinSixToTenPercent']=.06<=glow[rec['id']+'_'+rec['variant']]['surfaceFraction']<=.10
        ck['phone512VeinsWithinSixToTenPercent']=.06<=glow[rec['id']+'_'+rec['variant']]['phone512Fraction']<=.10
    if rec['id']=='wall_lavafall':
        rec['flowChild']='lava_flow';rec['maximumLavaProtrusionM']=max(game(v.co)[2] for o,n in p['obs'] if n=='lava_flow' for v in o.data.vertices)
        ck['flowLipWithin06m']=rec['maximumLavaProtrusionM']<=.60001;ck['lavaFlowChildPresent']='lava_flow'in rec['objects']
    checks.append(ck);kit_parts.append(rec)

flow={'childName':'lava_flow','atlas':'main','uRange':list(FLOW_U),'vRange':[0,1],'direction':'+V is downstream (top to bottom, then toward the pool lip)','recommendedSpeedVPerSecond':.18,'shaderSampling':'float2(uv.x, frac(uv.y - timeSeconds * 0.18))','unityMaterialOffsetYPerSecond':-.18,'wrap':'Repeat V only; keep U inside the reserved strip','hostNote':'Use one shared scrolling material for lava_flow children; the existing StageKit shader currently does not transform UVs and needs the small flow-UV branch.'}
kit=copy.deepcopy(source);kit.update(kit='volcano_cel_v2',version=5,styleId='anime_cel_volcano_lava_v2',parts=kit_parts,sourceKit='volcano_cel_v1',textures={**paths,'albedoSize':MAIN,'backdropAlbedoSize':BACK,'emissiveSize':EMIT,'backdropEmissiveSize':BACK,'colorSpace':'albedo/ground sRGB; both emission masks linear Non-Color','bodyAtlasURange':[0,BODY_U],'flowAtlasURange':list(FLOW_U)},lavaFlow=flow)
kit['placement']['wall_lavafall']='Same base mesh, pivot and seams as wall_straight_a/b; lava_flow is a separate child. Pool lip is at most local +Z=0.6m.'
kit['material']={'pipeline':'Unity 2022.3 Built-in, no .mat/.meta/URP files','recipe':'Unlit main/backdrop albedo, separate linear emission masks; preserve game shader pulse and underglow. No wall-foot orange ambient term baked. lava_flow scrolls V in its reserved main-atlas U strip.','backdropGlow':'volcano_cel_v2_backdrop_emissive.png; lava colors are also present in albedo if the host leaves backdrop emission disabled.'}
kit['notes']=['Source cel-v1 rock colour/light bake preserved outside the new lava marks.','Wall veins are measured over physical surface area with 64 stratified samples per triangle, not UV gutters.','No global orange foot lighting is baked; the host game already supplies it.','Two cratered cone replacements preserve source bounds/pivots; two original backdrop silhouettes remain.','No new AI image generation; reuses cel-v1 original painted artwork.','Phone frame rate/heat not measured.']
kit.pop('exportCleanup',None)
byid={}
for r in kit_parts:byid.setdefault(r['id'],[]).append(r)
inspect={'allChecksPassed':all(all(v for v in c.values() if isinstance(v,bool)) for c in checks),'quick':QUICK,'checks':checks,'lavaCoverage':glow,'coverageMethod':'64 stratified UV samples/triangle weighted by physical area; linear mask >0.25; UV gutters excluded','footUnderglowBaked':False,'originalV1RockBakePreserved':True,'flow':flow,'budgets':{k:{'limit':rs[0]['triangleBudget'],'max':max(r['triangles'] for r in rs),'passed':all(r['triangles']<=r['triangleBudget'] for r in rs)}for k,rs in byid.items()},'totals':{'parts':len(kit_parts),'partTriangles':sum(r['triangles'] for r in kit_parts)},'phoneFpsMeasured':False,'previewIsActual3D':True}
dump('kit.json',kit);dump('inspect.json',inspect)
log('models exported; coverage',glow)

# Preview code uses the same meshes/materials and is also saved as a real level.
exec(compile((pathlib.Path(__file__).parent/'preview_lava.py').read_text(encoding='utf-8'),'preview_lava.py','exec'))
assert inspect['allChecksPassed'] or QUICK,checks
log('COMPLETE',inspect['allChecksPassed'])
