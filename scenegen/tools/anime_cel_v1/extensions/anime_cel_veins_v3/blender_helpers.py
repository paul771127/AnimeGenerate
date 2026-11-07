import bpy,numpy as np,math
from mathutils import Vector
from lava_fields import srgb_to_linear
def G(x,y,z):return Vector((-x,-z,y))

def game(v):return (-v[0],v[2],-v[1])

def link(o,col=None):
    col=col or KIT
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


def material(name,image):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;n.clear();t=n.new('ShaderNodeTexImage');t.image=image;e=n.new('ShaderNodeEmission');l.new(t.outputs['Color'],e.inputs['Color']);o=n.new('ShaderNodeOutputMaterial');l.new(e.outputs[0],o.inputs[0]);return m

def export(obs,path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.hide_viewport=False;o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False,use_custom_props=False)
