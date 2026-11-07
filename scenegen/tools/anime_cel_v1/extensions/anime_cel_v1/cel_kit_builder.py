"""Replayable mobile anime/cel volcano kit builder.
CLI: blender -b --python cel_kit_builder.py -- --out NEW_DIR --v2 SOURCE_KIT --surface-atlas SOURCE_PNG [--quick]
Derived from the established v3 FBX importer/packer/exporter; rendering and cel material are new.
No runtime light, SSAO, outline, normal map or volume is needed for the baked kit.
"""
import bpy, bmesh, sys, os, json, math, time, shutil
from mathutils import Vector
import numpy as np

_argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(name, default=None):
    return _argv[_argv.index(name) + 1] if name in _argv else default
OUT = os.path.abspath(arg('--out', 'kit-v3-out')); V2DIR = os.path.abspath(arg('--v2')); TILES = ''
GROUND = arg('--ground'); CODEX = arg('--codex'); QUICK = '--quick' in _argv; FWD = -1.0
ATLAS = 1024 if QUICK else 4096; BACK = 512 if QUICK else 1024; EMIS = 256 if QUICK else 1024
AO_SAMPLES = 4 if QUICK else 16; PREVIEW_SCALE = 40 if QUICK else 100
for d in ('fbx', 'previews'):
    os.makedirs(os.path.join(OUT, d), exist_ok=True)
T0 = time.time()
def log(*a):
    print('[cel-kit %6.1fs]' % (time.time() - T0), *a, flush=True)
def G(x, y, z):
    return Vector((FWD * x, FWD * z, y))
def to_game(v):
    return Vector((FWD * v.x, v.z, FWD * v.y))
def yaw_to_euler(yaw_deg):
    return (0.0, 0.0, math.radians(-yaw_deg))

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1.0
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
KIT = bpy.data.collections.new('KIT'); scene.collection.children.link(KIT)
def link(ob, coll=None):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    (coll or KIT).objects.link(ob); return ob
def set_active(ob, extra=()):
    bpy.ops.object.select_all(action='DESELECT')
    for o in extra:
        o.select_set(True)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
def tri_count(ob):
    return sum(max(0, len(p.vertices) - 2) for p in ob.data.polygons)
def load_img(path, noncolor=False):
    im = bpy.data.images.load(path)
    if noncolor:
        im.colorspace_settings.name = 'Non-Color'
    return im

# ----------------------------------------------------------------------------- import the 002 meshes unchanged
v2kit = json.load(open(os.path.join(V2DIR, 'kit.json'), encoding='utf-8'))
def import_fbx(path, coll):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=False, use_image_search=False)
    new = [o for o in bpy.data.objects if o not in before and o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in new:
        o.select_set(True)
    bpy.context.view_layer.objects.active = new[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in new:
        link(o, coll)
    return new
def smooth_by_angle(ob, deg=45.0):
    bm = bmesh.new(); bm.from_mesh(ob.data)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        e.smooth = (len(e.link_faces) != 2) or (e.calc_face_angle(0.0) <= math.radians(deg))
    bm.to_mesh(ob.data); bm.free(); ob.data.update()
PARTS = []
log('importing 002 meshes')
for rec in v2kit['parts']:
    objs = import_fbx(os.path.join(V2DIR, rec['file']), KIT)
    named = []
    for nm in rec['objects']:
        ob = next(o for o in objs if o.name.split('.')[0] == nm)
        named.append((ob, nm))
    for ob, nm in named:
        smooth_by_angle(ob, 45.0)
        ob['part_id'] = rec['id']
        if rec.get('opening'):
            ob['lint'] = rec['opening']['height']
    PARTS.append(dict(id=rec['id'], variant=rec['variant'], objects=named, budget=rec['triangleBudget'], pivot=rec['pivot'], v2=rec))
ALL_OBJS = [ob for p in PARTS for ob, _ in p['objects']]
log('parts:', len(PARTS), 'objects:', len(ALL_OBJS), 'triangles:', sum(tri_count(o) for o in ALL_OBJS))
for p in PARTS:
    glow = 1.0 if p['id'].startswith('wall') else 0.6 if p['id'] == 'gate' else 0.5 if p['id'] == 'cave_entrance' else 0.0
    for ob, nm in p['objects']:
        ob.color = (0.0 if nm == 'gate_door' else glow, 1.0 if nm == 'gate_door' else 0.0, 1.0 if p['id'] == 'cave_entrance' else 0.0, 1.0)
        ob['kit_uv_weight'] = {'cave_entrance': 0.45, 'gate': 0.85}.get(p['id'], 1.0)
MAIN = [ob for ob in ALL_OBJS if ob['part_id'] != 'backdrop_rock']
BACKS = [ob for ob in ALL_OBJS if ob['part_id'] == 'backdrop_rock']

# ----------------------------------------------------------------------------- UV: smart project + shelf packing per atlas
def surface_area(ob):
    return sum(p.area for p in ob.data.polygons)
def shelf_pack(items, scale):
    order = sorted(items, key=lambda r: -r[1]); x = y = shelf_h = 0.0; out = {}; gap = 0.003
    for ob, side in order:
        s = side * scale
        if s > 1 - 2 * gap:
            return None
        if x + s + gap > 1:
            x = 0.0; y += shelf_h + gap; shelf_h = 0.0
        if y + s + gap > 1:
            return None
        out[ob] = (x + gap, y + gap, s - gap); x += s + gap; shelf_h = max(shelf_h, s)
    return out
def pack(objs, clamp=(0.45, 2.2)):
    rects = []
    for ob in objs:
        set_active(ob); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.015, area_weight=0.0, correct_aspect=True, scale_to_bounds=True)
        bpy.ops.object.mode_set(mode='OBJECT')
        rects.append([ob, math.sqrt(max(surface_area(ob), 0.05)) * ob.get('kit_uv_weight', 1.0)])
    ref = sorted(r[1] for r in rects)[len(rects) // 2]
    for r in rects:
        r[1] = max(min(r[1], ref * clamp[1]), ref * clamp[0])
    lo, hi, best = 0.0001, 10.0, None
    for _ in range(48):
        mid = (lo + hi) / 2; res = shelf_pack(rects, mid)
        if res:
            best, lo = res, mid
        else:
            hi = mid
    for ob, (u0, v0, s) in best.items():
        for l in ob.data.uv_layers.active.data:
            l.uv = (u0 + l.uv.x * s, v0 + l.uv.y * s)
    return best
log('unwrapping')
RECT = pack(MAIN); RECT.update(pack(BACKS, (0.6, 1.6)))
log('packed main %d objects, backdrop %d objects' % (len(MAIN), len(BACKS)))


# Technical atlas extraction, resampling and texture baking take place in Blender.
SURFACE_ATLAS = os.path.abspath(arg('--surface-atlas'))
source_img = load_img(SURFACE_ATLAS)
def pixels(im):
    a=np.empty(im.size[0]*im.size[1]*4, np.float32); im.pixels.foreach_get(a)
    return a.reshape(im.size[1], im.size[0],4)
def put_pixels(im,a):
    im.pixels.foreach_set(np.ascontiguousarray(a,dtype=np.float32).ravel()); im.update()
def save_png(im,path):
    scene.render.image_settings.file_format='PNG'; scene.render.image_settings.color_mode='RGB'
    scene.render.image_settings.color_depth='8'; scene.render.image_settings.compression=70
    im.save_render(path,scene=scene)
src=pixels(source_img); hh,ww=src.shape[:2]; tiles=[]
for name,xx,yy in [('rock_cool',0,1),('ground_ash',1,1),('rock_warm',0,0),('ground_crust',1,0)]:
    sub=src[yy*(hh//2):(yy+1)*(hh//2),xx*(ww//2):(xx+1)*(ww//2)].copy()
    # Loaded byte PNG pixels are sRGB; Blender floating-point image buffers are linear.
    # Convert once here to avoid brightening the source twice during baking.
    sub[...,:3]=np.where(sub[...,:3]<=0.04045,sub[...,:3]/12.92,((sub[...,:3]+0.055)/1.055)**2.4)
    im=bpy.data.images.new(name,sub.shape[1],sub.shape[0],alpha=False,float_buffer=True)
    im.colorspace_settings.name='sRGB'; put_pixels(im,sub); im.scale(1024,1024)
    save_png(im,os.path.join(OUT,'cel_'+name+'.png')); tiles.append(im)
mat=bpy.data.materials.new('AnimeCelBake'); mat.use_nodes=True
nt=mat.node_tree; nodes=nt.nodes; links=nt.links
nodes.clear()
def N(t, **kw):
    n = nodes.new(t)
    for k, val in kw.items():
        setattr(n, k, val)
    return n
def _plug(sock, val):
    if val is None:
        return
    if isinstance(val, (int, float, tuple)):
        sock.default_value = val
    else:
        links.new(val, sock)
def MATH(op, a, b=None, c=None, clamp=False):
    n = N('ShaderNodeMath', operation=op, use_clamp=clamp)
    for i, s in enumerate((a, b, c)):
        _plug(n.inputs[i], s)
    return n.outputs[0]
def MR(v, a, b, c=0.0, d=1.0, smooth=False):
    n = N('ShaderNodeMapRange'); n.interpolation_type = 'SMOOTHSTEP' if smooth else 'LINEAR'; n.clamp = True
    _plug(n.inputs['Value'], v)
    n.inputs['From Min'].default_value = a; n.inputs['From Max'].default_value = b; n.inputs['To Min'].default_value = c; n.inputs['To Max'].default_value = d
    return n.outputs['Result']
def MIX(fac, a, b):
    n = N('ShaderNodeMix'); n.data_type = 'RGBA'; n.clamp_factor = True
    _plug(n.inputs['Factor'], fac); _plug(n.inputs[6], a); _plug(n.inputs[7], b); return n.outputs[2]
def CMUL(a, b):
    n = N('ShaderNodeMix'); n.data_type = 'RGBA'; n.blend_type = 'MULTIPLY'; n.inputs['Factor'].default_value = 1.0
    _plug(n.inputs[6], a); _plug(n.inputs[7], b); return n.outputs[2]
def NOISE(vec, scale, detail=2.0, offset=(0, 0, 0)):
    vm = N('ShaderNodeVectorMath', operation='ADD'); links.new(vec, vm.inputs[0]); vm.inputs[1].default_value = offset
    n = N('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
    links.new(vm.outputs[0], n.inputs['Vector']); return n

tc=N('ShaderNodeTexCoord'); geo=N('ShaderNodeNewGeometry'); oi=N('ShaderNodeObjectInfo')
P=tc.outputs['Object']
sp=N('ShaderNodeSeparateXYZ'); links.new(P,sp.inputs[0])
gx=MATH('MULTIPLY',sp.outputs['X'],-1.0); gy=sp.outputs['Z']; gz=MATH('MULTIPLY',sp.outputs['Y'],-1.0)
col=N('ShaderNodeSeparateColor'); links.new(oi.outputs['Color'],col.inputs[0]); cR,cG,cB=col.outputs[0],col.outputs[1],col.outputs[2]
def box_tile(im,metres):
    mp=N('ShaderNodeMapping'); mp.inputs['Scale'].default_value=(1/metres,)*3
    links.new(P,mp.inputs['Vector'])
    tex=N('ShaderNodeTexImage',projection='BOX',projection_blend=0.22,extension='REPEAT',interpolation='Linear'); tex.image=im
    links.new(mp.outputs[0],tex.inputs['Vector']); return tex.outputs['Color']
# Low-frequency painted materials; no fine procedural noise is added to the albedo.
cool=box_tile(tiles[0],3.0); warm=box_tile(tiles[2],3.7)
up=N('ShaderNodeSeparateXYZ'); links.new(geo.outputs['Normal'],up.inputs[0])
rock=MIX(MR(up.outputs['Z'],0.05,0.8,0.72,0.94,smooth=True),cool,warm)
dot=N('ShaderNodeVectorMath',operation='DOT_PRODUCT'); links.new(geo.outputs['Normal'],dot.inputs[0]); dot.inputs[1].default_value=G(-0.8,1.4,0.6).normalized()
lam=dot.outputs['Value']
# Two broad light bands with a short smooth transition; no per-frame lighting is required.
band=MR(lam,-0.08,0.10,smooth=True)
shade=MIX(band,(0.53,0.62,0.74,1),(1.48,1.33,1.08,1))
base=CMUL(rock,shade)
rim=MATH('MULTIPLY',MR(geo.outputs['Pointiness'],0.54,0.67,smooth=True),MR(up.outputs['Z'],0.05,0.65,smooth=True))
base=MIX(MATH('MULTIPLY',rim,0.18),base,CMUL(rock,(1.55,1.46,1.20,1)))
# Sparse small cracks restricted to the foot; no large bright web on the walls.
warp=N('ShaderNodeVectorMath',operation='SCALE'); links.new(NOISE(P,0.6,1,(1,2,3)).outputs['Color'],warp.inputs[0]); warp.inputs['Scale'].default_value=0.4
pw=N('ShaderNodeVectorMath',operation='ADD'); links.new(P,pw.inputs[0]); links.new(warp.outputs[0],pw.inputs[1])
vor=N('ShaderNodeTexVoronoi',feature='DISTANCE_TO_EDGE'); vor.inputs['Scale'].default_value=0.7; links.new(pw.outputs[0],vor.inputs['Vector'])
crack=MATH('MULTIPLY',MR(vor.outputs['Distance'],0.019,0.008,smooth=True),MATH('GREATER_THAN',NOISE(P,0.8,1,(4,4,9)).outputs['Fac'],0.69))
e_ground=MATH('MULTIPLY',MATH('MULTIPLY',cR,crack),MR(gy,0.05,0.55,1,0,smooth=True))
lint=N('ShaderNodeAttribute',attribute_type='OBJECT',attribute_name='lint')
e_door=MATH('MULTIPLY',cG,MATH('MAXIMUM',MR(MATH('ABSOLUTE',gx),1.07,1.13,smooth=True),MR(MATH('SUBTRACT',lint.outputs['Fac'],gy),0.14,0.05,smooth=True)))
e_door=MATH('MULTIPLY',e_door,MATH('GREATER_THAN',MATH('ABSOLUTE',gz),0.08))
e_cave=MATH('MULTIPLY',cB,MATH('MULTIPLY',MR(gz,-3,-6,smooth=True),MR(gy,0.1,1.1,1,0)))
e_cave=MATH('MULTIPLY',e_cave,MATH('MULTIPLY',crack,MATH('LESS_THAN',MATH('ABSOLUTE',gx),1.5)))
emis=MATH('MAXIMUM',e_ground,MATH('MAXIMUM',e_door,e_cave),clamp=True)
base=MIX(MR(emis,0.05,0.7),base,(1.0,0.30,0.045,1))
mode=N('ShaderNodeValue'); mode.outputs[0].default_value=0
ec=N('ShaderNodeCombineColor'); links.new(emis,ec.inputs[0]); links.new(emis,ec.inputs[1]); links.new(emis,ec.inputs[2])
final=MIX(mode.outputs[0],base,ec.outputs[0]); emsh=N('ShaderNodeEmission'); links.new(final,emsh.inputs['Color'])
out=N('ShaderNodeOutputMaterial'); links.new(emsh.outputs[0],out.inputs[0]); target=N('ShaderNodeTexImage')
for ob in ALL_OBJS: ob.data.materials.clear(); ob.data.materials.append(mat)
for i,p in enumerate(PARTS):
    for ob,_ in p['objects']: ob.location=G((i%6)*42,0,(i//6)*42)
bpy.ops.mesh.primitive_plane_add(size=400,location=(100,100,-0.002)); ground=bpy.context.object; ground.name='BakeGround'
world=bpy.data.worlds.new('CelWorld'); scene.world=world; world.use_nodes=True
wbg=world.node_tree.nodes['Background']; wbg.inputs[0].default_value=(0.13,0.20,0.24,1); wbg.inputs[1].default_value=0.8
world.light_settings.distance=1.2
scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.use_denoising=False
bk=scene.render.bake; bk.margin=8 if QUICK else 20; bk.margin_type='EXTEND'; bk.use_clear=True; bk.target='IMAGE_TEXTURES'
def new_img(name,size,data=False):
    im=bpy.data.images.new(name,size,size,alpha=True,float_buffer=True); im.colorspace_settings.name='Non-Color' if data else 'sRGB'; return im
def bake(objs,im,typ,samples):
    target.image=im; nodes.active=target; scene.cycles.samples=samples
    set_active(objs[0],objs); bpy.ops.object.bake(type=typ); log('baked',typ,im.name)
imgs={}
for key,obs,sz in [('main',MAIN,ATLAS),('back',BACKS,BACK)]:
    c=new_img('CelColor_'+key,sz); a=new_img('CelAO_'+key,sz,True)
    mode.outputs[0].default_value=0; bake(obs,c,'EMIT',2); bake(obs,a,'AO',AO_SAMPLES)
    imgs[key]={'color':c,'ao':a,'size':sz}
ie=new_img('CelEmit',EMIS,True); mode.outputs[0].default_value=1; bake(MAIN,ie,'EMIT',2)
emf=pixels(ie)[...,0]; albedo={}; colour_report={}; part_tex={}
em_big=np.kron(emf,np.ones((ATLAS//EMIS,ATLAS//EMIS),np.float32))
def srgb(x): return np.where(x<=0.0031308,x*12.92,1.055*np.maximum(x,0)**(1/2.4)-0.055)
def hx(x): return '#%02x%02x%02x'%tuple(int(np.clip(round(float(v)*255),0,255)) for v in x)
for key in ('main','back'):
    d=imgs[key]; c=pixels(d['color']); a=pixels(d['ao'])[...,0]; mask=c[...,3]>0.5
    # AO is intentionally restrained, so the cel shadow remains readable instead of black.
    rgb=c[...,:3]*(0.78+0.22*np.clip(a,0,1))[...,None]
    if key=='back': rgb=rgb*0.66+np.array([0.035,0.09,0.115],np.float32)*0.34
    if key=='main': rgb=np.where((em_big>0.25)[...,None],c[...,:3],rgb)
    rgb[~mask]=(0.025,0.04,0.055)
    im=new_img('CelAlbedo_'+key,d['size']); put_pixels(im,np.concatenate((np.clip(rgb,0,1),np.ones(rgb.shape[:2]+(1,),np.float32)),axis=2)); albedo[key]=im
    for ob in (MAIN if key=='main' else BACKS):
        u,v,s=RECT[ob]; sz=d['size']; x0,x1=int(u*sz),int((u+s)*sz); y0,y1=int(v*sz),int((v+s)*sz)
        vals=srgb(rgb[y0:y1,x0:x1][mask[y0:y1,x0:x1]])
        if vals.size: colour_report[ob.name]={'avg':hx(vals.mean(0)),'lit90':hx(np.percentile(vals,90,axis=0)),'note':'new cel palette; obsolete v3 brown colour limits intentionally superseded; style is reviewed visually'}
img_emissive=ie
ALB_PATH=os.path.join(OUT,'volcano_cel_albedo.png'); BACK_PATH=os.path.join(OUT,'volcano_cel_backdrop.png'); EMI_PATH=os.path.join(OUT,'volcano_cel_emissive.png')
save_png(albedo['main'],ALB_PATH); save_png(albedo['back'],BACK_PATH)
# Data masks must not receive a display/sRGB transfer when saving.
scene.view_settings.view_transform='Raw'; save_png(ie,EMI_PATH); scene.view_settings.view_transform='Standard'
def uv_area(ob):
    uv=ob.data.uv_layers.active.data; total=0
    for poly in ob.data.polygons:
        pts=[uv[i].uv for i in poly.loop_indices]
        for i in range(1,len(pts)-1): total+=abs((pts[i].x-pts[0].x)*(pts[i+1].y-pts[0].y)-(pts[i+1].x-pts[0].x)*(pts[i].y-pts[0].y))/2
    return total
m_small=(pixels(imgs['main']['color'])[...,3]>0.5)[::ATLAS//EMIS,::ATLAS//EMIS]
def surface_emissive_fraction(ob,mask):
    # Uniform triangle samples weighted by physical surface area exclude UV gutters.
    # A UV-rectangle pixel ratio is misleading for sparsely packed parts.
    ob.data.calc_loop_triangles(); uv=ob.data.uv_layers.active.data
    coords=np.array([[uv[li].uv[:] for li in t.loops] for t in ob.data.loop_triangles],np.float32)
    area=np.array([t.area for t in ob.data.loop_triangles],np.float64)
    bary=[]
    for i in range(8):
        for j in range(8):
            a=(i+.5)/8; b=(j+.5)/8
            if a+b>1: a,b=1-a,1-b
            bary.append((1-a-b,a,b))
    pts=np.einsum('sj,tjk->tsk',np.array(bary,np.float32),coords)
    x=np.clip((pts[...,0]*mask.shape[1]).astype(int),0,mask.shape[1]-1)
    y=np.clip((pts[...,1]*mask.shape[0]).astype(int),0,mask.shape[0]-1)
    ratio=(mask[y,x]>.25).mean(axis=1)
    return float((ratio*area).sum()/max(area.sum(),1e-9))
for p in PARTS:
    for ob,nm in p['objects']:
        u,v,s=RECT[ob]; is_main=ob in MAIN; sz=ATLAS if is_main else BACK
        frac=0
        if is_main:
            x0,x1=int(u*EMIS),int((u+s)*EMIS); y0,y1=int(v*EMIS),int((v+s)*EMIS)
            frac=surface_emissive_fraction(ob,emf)
        part_tex['%s_%s/%s'%(p['id'],p['variant'],nm)]={'emissiveFraction':round(frac,4),'texelsPerMetre':round(math.sqrt(uv_area(ob)*sz*sz/max(surface_area(ob),1e-6)),1),'atlas':'main' if is_main else 'backdrop'}
STATS_TEX={'emissiveTexelFraction':float(((emf>0.25)&m_small).sum()/max(1,m_small.sum())),'surfaceGlowMeasurement':'64 stratified UV samples per triangle, weighted by physical triangle area; threshold 0.25 in the linear emission mask. Atlas gutter pixels are excluded.','perObject':part_tex,'colourCheck':colour_report}
def preview_material(name,alb,emi=None):
    pm=bpy.data.materials.new(name); pm.use_nodes=True; nd=pm.node_tree.nodes; lk=pm.node_tree.links; nd.clear()
    t=nd.new('ShaderNodeTexImage'); t.image=alb; e=nd.new('ShaderNodeEmission'); lk.new(t.outputs['Color'],e.inputs['Color'])
    o=nd.new('ShaderNodeOutputMaterial'); lk.new(e.outputs[0],o.inputs[0]); return pm
pm_main=preview_material('CelMain',load_img(ALB_PATH),load_img(EMI_PATH,True)); pm_back=preview_material('CelBackdrop',load_img(BACK_PATH))
tiles=[load_img(os.path.join(OUT,'cel_'+name+'.png')) for name in ('rock_cool','ground_ash','rock_warm','ground_crust')]
for ob in ALL_OBJS: ob.data.materials[0]=pm_main if ob in MAIN else pm_back
log('cel atlases ready')

def bounds(objs):
    mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
    for ob in objs:
        for v in ob.data.vertices:
            p = to_game(v.co)
            mn.x = min(mn.x, p.x); mn.y = min(mn.y, p.y); mn.z = min(mn.z, p.z); mx.x = max(mx.x, p.x); mx.y = max(mx.y, p.y); mx.z = max(mx.z, p.z)
    return mn, mx
def ensure_name(ob, name):
    other = bpy.data.objects.get(name)
    if other is not None and other is not ob:
        other.name = name + '_swap'
    ob.name = name
def export(objs, fname):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    fpath = os.path.join(OUT, 'fbx', fname)
    bpy.ops.export_scene.fbx(filepath=fpath, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH'},
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', use_tspace=False, add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', embed_textures=False, use_custom_props=False)
    return fpath
log('exporting FBX')
kit_parts = []; checks = []
for p in PARTS:
    objs = [ob for ob, _ in p['objects']]
    for ob in objs:
        ob.location = (0, 0, 0)
    for ob, nm in p['objects']:
        ensure_name(ob, nm)
    fname = '%s_%s.fbx' % (p['id'], p['variant']); fpath = export(objs, fname)
    extra = {nm: os.path.basename(export([ob], '%s_%s.fbx' % (nm, p['variant']))) for ob, nm in p['objects']} if len(objs) > 1 else None
    mn, mx = bounds(objs); tris = {nm: tri_count(ob) for ob, nm in p['objects']}; total = sum(tris.values())
    uv_ok = all(0.0 <= l.uv.x <= 1.0 and 0.0 <= l.uv.y <= 1.0 for ob in objs for l in ob.data.uv_layers.active.data)
    v2 = p['v2']
    rec = dict(id=p['id'], variant=p['variant'], file=fname, triangles=total, trianglesPerObject=tris, triangleBudget=p['budget'],
               bounds=dict(min=[round(mn.x, 3), round(mn.y, 3), round(mn.z, 3)], max=[round(mx.x, 3), round(mx.y, 3), round(mx.z, 3)]),
               size=[round(mx.x - mn.x, 3), round(mx.y - mn.y, 3), round(mx.z - mn.z, 3)], pivot=p['pivot'], objects=[nm for _, nm in p['objects']],
               children=[nm for _, nm in p['objects'][1:]], separateFiles=extra, atlas='backdrop' if p['id'] == 'backdrop_rock' else 'main',
               topLine=v2.get('topLine'), opening=v2.get('opening'), maxFaceAreaM2=v2.get('maxFaceAreaM2'), fileBytes=os.path.getsize(fpath),
               texture={nm: part_tex['%s_%s/%s' % (p['id'], p['variant'], nm)] for _, nm in p['objects']},
               colour={nm: colour_report.get(nm + ('_' + p['variant'] if p['id'] == 'gate' else ''), colour_report.get('%s_%s' % (p['id'], p['variant']))) for _, nm in p['objects']})
    same_mesh = abs(total - v2['triangles']) <= 3 and all(abs(rec['size'][i] - v2['size'][i]) < 0.01 for i in range(3))   # FBX round trip may drop a degenerate triangle
    ck = dict(part='%s_%s' % (p['id'], p['variant']), trianglesWithinBudget=total <= p['budget'], sameMeshAsSource=same_mesh, uvWithin01=uv_ok, pivotOnGround=abs(mn.y) < 0.02)
    if p['id'] != 'cave_entrance':
        ck['emissiveFractionOk'] = all(rec['texture'][nm]['emissiveFraction'] <= 0.05 for nm in rec['texture'] if nm != 'gate_door')
    checks.append(ck); kit_parts.append(rec)
log('exported', len(kit_parts), 'parts')


# Actual 3D preview assembly: all geometry below is also exported in level.glb.
for ob in ALL_OBJS: ob.hide_render=True; ob.hide_viewport=True
ground.hide_render=True
PREV=bpy.data.collections.new('PREVIEW'); scene.collection.children.link(PREV)
scene.render.engine='BLENDER_EEVEE_NEXT'; scene.eevee.taa_render_samples=16 if QUICK else 48
scene.render.resolution_percentage=PREVIEW_SCALE
wbg.inputs[0].default_value=(0.055,0.14,0.18,1); wbg.inputs[1].default_value=1
sun_d=bpy.data.lights.new('Sun','SUN'); sun_d.energy=2.0; sun_d.color=(1,.84,.62); sun_d.angle=math.radians(8)
sun=bpy.data.objects.new('Sun',sun_d); scene.collection.objects.link(sun)
def sun_from(x,y,z): sun.rotation_euler=G(-x,-y,-z).to_track_quat('-Z','Y').to_euler()
cam_d=bpy.data.cameras.new('Cam'); cam=bpy.data.objects.new('Cam',cam_d); scene.collection.objects.link(cam); scene.camera=cam
cam_d.sensor_fit='HORIZONTAL'; cam_d.sensor_width=36; cam_d.clip_end=250
placed=[]
def place(ob, x, y, z, yaw=0.0):
    c = ob.copy(); c.hide_render = False; c.hide_viewport = False; PREV.objects.link(c)
    c.location = G(x, y, z); c.rotation_euler = yaw_to_euler(yaw); placed.append(c); return c
def clear_placed():
    for c in placed:
        bpy.data.objects.remove(c, do_unlink=True)
    placed.clear()
def look(cam_pos, tgt, hfov_deg):
    cam.location = G(*cam_pos); d = G(*tgt) - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); cam_d.lens = 18.0 / math.tan(math.radians(hfov_deg) / 2)
def render(name, w=1600, h=900):
    scene.render.resolution_x = w; scene.render.resolution_y = h
    scene.render.filepath = os.path.join(OUT, 'previews', name + '.png'); bpy.ops.render.render(write_still=True); log('rendered', name)

PART_OBJ = {('%s_%s' % (p['id'], p['variant'])): [ob for ob, _ in p['objects']] for p in PARTS}
log('rendering part sheets'); sun_from(1.0, 1.35, 1.0)
by_id = {}
for rec in kit_parts:
    by_id.setdefault(rec['id'], []).append(rec)
for pid, recs in by_id.items():
    clear_placed()
    widths = [r['size'][0] for r in recs]; depth = max(r['size'][2] for r in recs); height = max(r['size'][1] for r in recs)
    gap = 1.0 + 0.1 * max(widths); total_w = sum(widths) + gap * (len(recs) - 1); x = -total_w / 2
    for r, w in zip(recs, widths):
        for ob in PART_OBJ['%s_%s' % (pid, r['variant'])]:
            place(ob, x + w / 2 - (r['bounds']['min'][0] + w / 2), 0, 0)
        x += w + gap
    extent = max(total_w, height * 1.6, depth); dist = extent * 0.5 / math.tan(math.radians(27)) * 1.35 + depth
    dirv = Vector((-0.5, 0.42 + 0.25 * (height > 6), 1.0)).normalized()
    look((dirv.x * dist, dirv.y * dist + height * 0.35, dirv.z * dist), (0, height * 0.42, -depth * 0.25), 54)
    render('part_' + pid, 1440, 810)


def mesh(name,verts,faces,uv=None,matl=None):
    me=bpy.data.meshes.new(name); me.from_pydata([G(*v) for v in verts],[],faces); me.update()
    ob=bpy.data.objects.new(name,me); PREV.objects.link(ob)
    if uv:
        layer=me.uv_layers.new()
        for poly in me.polygons:
            for li,vi in zip(poly.loop_indices,poly.vertices): layer.data[li].uv=uv[vi]
    if matl: me.materials.append(matl)
    placed.append(ob); return ob
def tilemat(name,image):
    m=bpy.data.materials.new(name); m.use_nodes=True; nd=m.node_tree.nodes; lk=m.node_tree.links; nd.clear()
    t=nd.new('ShaderNodeTexImage'); t.image=image; t.extension='REPEAT'
    e=nd.new('ShaderNodeEmission'); lk.new(t.outputs['Color'],e.inputs['Color']); out=nd.new('ShaderNodeOutputMaterial'); lk.new(e.outputs[0],out.inputs[0]); return m
gmat=tilemat('CelAshGround',tiles[1]); crustmat=tilemat('CelCooledCrust',tiles[3])
def flatmat(name,c):
    m=bpy.data.materials.new(name); m.use_nodes=True; nd=m.node_tree.nodes; lk=m.node_tree.links; nd.clear(); e=nd.new('ShaderNodeEmission'); e.inputs['Color'].default_value=(*c,1); o=nd.new('ShaderNodeOutputMaterial'); lk.new(e.outputs[0],o.inputs[0]); return m
lavam=flatmat('LavaCore',(1.0,.29,.035)); lavaedge=flatmat('LavaOuter',(.62,.09,.012)); bankm=flatmat('DarkCoolingBank',(.025,.045,.055)); dustm=flatmat('AshWarmPatch',(.19,.20,.19))
def sky_dome():
    vs=[]; fs=[]; longitudes=32; rings=9; centre=(4,-5,25)
    for j in range(rings):
        elevation=-.1+j*(math.pi/2+.1)/(rings-1)
        for i in range(longitudes):
            a=2*math.pi*i/longitudes
            vs.append((centre[0]+130*math.cos(elevation)*math.cos(a),centre[1]+130*math.sin(elevation),centre[2]+130*math.cos(elevation)*math.sin(a)))
    for j in range(rings-1):
        for i in range(longitudes):
            k=j*longitudes+i; nxt=j*longitudes+(i+1)%longitudes
            fs.append((k,nxt,nxt+longitudes,k+longitudes))
    ob=mesh('ENV_CelSkyDome',vs,fs)
    colors=ob.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    for poly in ob.data.polygons:
        for li,vi in zip(poly.loop_indices,poly.vertices):
            t=max(0,min(1,(vs[vi][1]+5)/110))
            low=np.array([.085,.19,.21]); high=np.array([.018,.055,.095]); c=low*(1-t)+high*t
            colors.data[li].color=(*c,1)
    m=bpy.data.materials.new('CelSkyGradient'); m.use_nodes=True; nd=m.node_tree.nodes; lk=m.node_tree.links; nd.clear()
    attr=nd.new('ShaderNodeVertexColor'); attr.layer_name='Color'; e=nd.new('ShaderNodeEmission'); lk.new(attr.outputs['Color'],e.inputs['Color']); o=nd.new('ShaderNodeOutputMaterial'); lk.new(e.outputs[0],o.inputs[0]); ob.data.materials.append(m)
    return ob
def floor(x0,x1,z0,z1,y=0,matl=None):
    return mesh('COL_CelFloor',[(x0,y,z0),(x1,y,z0),(x1,y,z1),(x0,y,z1)],[(0,1,2,3)],[(x0/3,z0/3),(x1/3,z0/3),(x1/3,z1/3),(x0/3,z1/3)],matl or gmat)
def put(key,x,z,yaw=0,y=0):
    return [place(ob,x,y,z,yaw) for ob in PART_OBJ[key]]
def shadow_blob(x,z,rx,rz,alpha=.32):
    # Opaque low-poly contact patch. Exported geometry, no preview-only SSAO.
    verts=[(x,0.004,z)]+[(x+math.cos(a)*rx,0.004,z+math.sin(a)*rz) for a in np.linspace(0,2*math.pi,14,endpoint=False)]
    return mesh('VIS_ContactPatch',verts,[(0,i+1,(i+1)%14+1) for i in range(14)],matl=bankm)
def cooled_ribbon():
    vs=[]; fs=[]; n=56
    for i in range(n):
        z=-8+i*0.8; centre=9.4+1.2*math.sin(z*.14); half=1.15+.22*math.sin(z*.32)
        vs.extend([(centre-half,.006,z),(centre+half,.006,z)])
        if i: fs.append((2*i-2,2*i-1,2*i+1,2*i))
    mesh('VIS_LavaRiver',vs,fs,matl=lavaedge)
    inner=[]
    for i in range(n):
        z=-8+i*.8; c=9.4+1.2*math.sin(z*.14); w=(1.15+.22*math.sin(z*.32))*.72
        inner.extend([(c-w,.008,z),(c+w,.008,z)])
    mesh('VIS_LavaHotCore',inner,fs,matl=lavam)
    # Rounded, irregular cooling islands avoid a repeated diamond pattern.
    rng=np.random.default_rng(719)
    for i in range(0,n-2,2):
        z=-8+i*.8; cx=9.4+1.2*math.sin(z*.14)+float(rng.uniform(-.65,.65))
        rx=float(rng.uniform(.22,.70)); rz=float(rng.uniform(.30,.85)); pts=[]; uv=[]
        for j in range(10):
            a=2*math.pi*j/10; k=float(rng.uniform(.75,1.15)); xx=cx+math.cos(a)*rx*k; zz=z+math.sin(a)*rz*k
            pts.append((xx,.014,zz)); uv.append((xx/3,zz/3))
        mesh('VIS_CoolingCrust',pts,[tuple(range(10))],uv,crustmat)
def distant_caldera():
    # A physical stylised caldera, deliberately low frequency; no texture-only mountain.
    rng=np.random.default_rng(1207); vs=[]; fs=[]; n=28
    for level in range(4):
        for i in range(n):
            a=2*math.pi*i/n; r=[24,18,9.0,6.5][level]*(1+float(rng.uniform(-.12,.12)))
            h=[0,15,35,31][level]+float(rng.uniform(-1.2,1.2))
            vs.append((28+r*math.cos(a),h,91+r*math.sin(a)))
    for level in range(3):
        for i in range(n): fs.append((level*n+i,level*n+(i+1)%n,(level+1)*n+(i+1)%n,(level+1)*n+i))
    ob=mesh('VIS_DistantCaldera',vs,fs)
    uv=ob.data.uv_layers.new()
    for poly in ob.data.polygons:
        for li,vi in zip(poly.loop_indices,poly.vertices): uv.data[li].uv=((vs[vi][0]-4)/18,vs[vi][1]/18)
    ob.data.materials.append(tilemat('CalderaBasalt',tiles[2]))
    ring=[(28+5.8*math.cos(a),31,91+5.8*math.sin(a)) for a in np.linspace(0,2*math.pi,n,endpoint=False)]
    mesh('VIS_CalderaGlow',ring,[tuple(range(n))],matl=lavam)
def scene_build():
    floor(-38,38,-14,64)
    sky_dome()
    # Four-metre modular approach opening onto a wider combat clearing.
    for j,z in enumerate((1,5,9)):
        put('wall_straight_'+'abcd'[j%4],-2.8,z,90)
        put('wall_straight_'+'bcda'[j%4],2.8,z,-90)
    put('wall_end_a',-2.7,12,90); put('wall_end_b',2.8,12,-90)
    for j,z in enumerate((17,21,25,29,33)):
        put('wall_straight_'+'abcd'[j%4],-7,z,90)
    for j,z in enumerate((21,25,29,33)):
        put('wall_straight_'+'dcba'[j%4],15,z,-90)
    put('gate_a',1.2,24,0)
    put('cave_entrance_a',1.2,38,0)
    # Broken silhouettes, scene rhythm and distant shape hierarchy.
    for key,x,z,sc,yaw in [('a',-18,45,.65,25),('b',22,48,.75,-30),('c',-12,67,.95,10),('d',16,68,1.,-15),('a',-27,65,.8,20)]:
        for o in put('backdrop_rock_'+key,x,z,yaw): o.scale=(sc,sc,sc)
    # Small physical rocks from the same kit. Shared mesh/material, no extra textures.
    rng=np.random.default_rng(20261107)
    for x,z in [(-1.9,2),(2,6),(-2,9),(5.8,16),(-5.5,20),(4.2,23),(-3.9,29),(12,30),(6,34),(-4.5,36)]:
        sc=float(rng.uniform(.08,.18))
        for o in put('backdrop_rock_'+'abcd'[int(rng.integers(0,4))],x,z,float(rng.uniform(-180,180))): o.scale=(sc,sc,sc*.6)
    cooled_ribbon()
    distant_caldera()
    # Cool environment and warm focal glows are in textures, not realtime lights.
    for x,z in [(-2.6,1),(2.6,6),(-6.8,20),(14.5,25),(.0,36)]: shadow_blob(x,z,.38,1.8)

log('rendering new level')
clear_placed(); sun_from(-.8,1.4,.6); scene_build()
look((0,1.5,-3),(0.1,1.5,17),76); render('player_view',1600,900)
look((2.2,1.5,16),(4.5,3.0,38),80); render('clearing_view',1600,900)
look((-16,12,-10),(4,2.0,25),66); render('hero_view',1600,900)
# Save and export the actual sample layout; do not package cameras or Blender lights.
demo_tri=sum(tri_count(o) for o in placed if o.type=='MESH')
bpy.ops.object.select_all(action='DESELECT')
for ob in placed:
    if ob.type=='MESH': ob.select_set(True)
bpy.context.view_layer.objects.active=next(ob for ob in placed if ob.type=='MESH')
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'level.glb'),export_format='GLB',use_selection=True,export_yup=True,export_texcoords=True,export_normals=True,export_materials='EXPORT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'level.fbx'),use_selection=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False,use_custom_props=False)
scene_manifest={'schemaVersion':1,'styleId':'anime_cel_volcano_v1','units':'metres','up':'+Y','glbCoordinateNote':'Blender exporter Y-up: game X and Z are mirrored by the established kit importer convention; exact source-to-GLB transform is diag(-1,1,1) from G(x,y,z) -> glTF. Use listed glb positions, not unconverted source game points.','glbSpawn':[-0.0,1.5,-3.0],'sourceGameSpawn':[0,1.5,-3],'glbForward':[0,0,1],'floorY':0,'clearWidthMetres':4.0,'demoTriangles':demo_tri,'renderedFromActualGeometry':True,'runtimeLighting':'baked albedo; no realtime lamps, outline or volume pass','routeSourceGame':[[0,0,-3],[0,0,9],[1,0,18],[1.2,0,24],[1.2,0,38]],'gameplay':'visual layout only; consuming game supplies controllers, collision generation and triggers','lavaRiver':'visible effect geometry; consuming game must mark its hazard region if using the example layout'}
def dump(name,data):
    with open(os.path.join(OUT,name),'wb') as f: f.write((json.dumps(data,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
dump('level.json',scene_manifest)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'level.blend'))
clear_placed()
for ob in PART_OBJ['wall_straight_a']: place(ob,0,0,0,0)
floor(-4,4,-2,8)
look((-.8,1.5,3.3),(-.2,1.7,.0),66); render('wall_closeup',1600,900)
for p in PARTS:
    for ob,nm in p['objects']:
        rec=next(r for r in kit_parts if r['id']==p['id'] and r['variant']==p['variant'])
        if nm in colour_report: rec['colour'][nm]=colour_report[nm]
kit={'kit':'volcano_cel_v1','version':4,'styleId':'anime_cel_volcano_v1','units':'metres','up':'+Y','forward':'+Z','gridCell':4.0,'pivot':v2kit['pivot'],'placement':v2kit['placement'],'sourceKit':os.path.basename(V2DIR),'parts':kit_parts,
     'textures':{'albedo':os.path.basename(ALB_PATH),'albedoSize':ATLAS,'backdropAlbedo':os.path.basename(BACK_PATH),'backdropAlbedoSize':BACK,'emissive':os.path.basename(EMI_PATH),'emissiveSize':EMIS,'ground':'cel_ground_ash.png','groundCrust':'cel_ground_crust.png','colorSpace':'albedo and ground sRGB; emissive linear data','uvRange':'0..1 for part atlases; ground repeats every 3m','style':'clean painted basalt, cool shadows, warm highlights, two broad baked light bands, restrained AO, sparse basal glow'},
     'material':{'pipeline':'Unity Built-in compatible; no .mat or URP shader files','recipe':'Use Unlit/Texture with main atlas on wall/gate/cave; backdrop atlas on backdrop_rock; optional emissive mask for host glow. Albedo already includes cel shading. Do not multiply by a second directional lighting model.','rotationNote':'baked key light is part-local; globally consistent shadows across rotated instances require a host cel shader or scene bake'},
     'interfaces':{'partIds':'same as volcano_v3','gate':'gate_frame/gate_door and separateFiles retained','demo':'level.glb + level.json; no gameplay scripts','builder':'cel_kit_control.py API v1 extension, independent from control.py whole-scene API'},
     'notes':['Original anime-inspired environment assets; no characters, game UI or copied game assets.','New style intentionally supersedes work order 003 brown palette and exact 2D obstacle colour matching. Existing obstacle art is not restyled by this kit.','Per-part footprints, origins and triangle budgets retained.','Phone frame time, thermal behaviour and camera tracking overhead have not been measured.']}
dump('kit.json',kit)
by_id={}
for r in kit_parts: by_id.setdefault(r['id'],[]).append(r)
inspect={'allChecksPassed':all(all(v for k,v in c.items() if isinstance(v,bool)) for c in checks),'checks':checks,'budgets':{k:{'max':max(r['triangles'] for r in rs),'limit':rs[0]['triangleBudget'],'passed':all(r['triangles']<=r['triangleBudget'] for r in rs)} for k,rs in by_id.items()},'textureStats':STATS_TEX,'totals':{'parts':len(kit_parts),'partTriangles':sum(r['triangles'] for r in kit_parts),'demoTriangles':demo_tri},'phoneFpsMeasured':False,'previewIsActual3D':True,'quick':QUICK}
dump('inspect.json',inspect)
log('complete; all checks passed:',inspect['allChecksPassed'])
