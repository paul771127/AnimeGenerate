"""Small mesh authoring primitives. Authoring input is game Y-up/+Z."""
import math,pathlib,bpy,bmesh,numpy as np
from mathutils import Vector
MATERIALS={'stone':(0,0),'wood':(0,1),'roof':(0,2),'plaster':(0,3),'pave':(0,4),'ash':(0,5),'iron':(0,6),'brass':(0,7),'cloth':(1,0),'teal':(1,1),'red':(1,2),'paper':(1,3),'glass':(1,4),'leaf':(1,5),'fire':(1,6),'coal':(1,7)}
def G(p):return Vector((-p[0],-p[2],p[1]))
def game(v):return Vector((-v[0],v[2],-v[1]))
def rgba(h):
    a=np.array([int(h[i:i+2],16)/255 for i in [1,3,5]])
    return tuple(np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4))+(1,)
class Draft:
    def __init__(self,name):self.name=name;self.v=[];self.f=[];self.keys=[];self.uv=[]
    def face(self,points,key,normal=None,shade=None):
        p=[Vector(x)for x in points];n=(p[1]-p[0]).cross(p[2]-p[0])
        if n.length<1e-9:return
        if normal is not None and n.dot(Vector(normal))<0:p.reverse();n=-n
        n.normalize();offset=len(self.v);self.v.extend(tuple(x)for x in p)
        # G is a reflection; reverse loops to preserve outward-facing normals.
        self.f.append(tuple(offset+i for i in reversed(range(len(p)))));self.keys.append(key)
        light=Vector((-.5,.9,.45)).normalized();band=int(n.dot(light)>.12)if shade is None else shade
        if key=='fire':band=1
        _,tile=MATERIALS[key];slot=tile*2+band;col=slot%4;row=slot//4
        coords=np.array(p);axis=int(np.argmax(np.abs(n)));axes=[i for i in range(3)if i!=axis]
        # Keep height in texture V for vertical walls and timber posts.
        if 1 in axes:axes=[axes[1],axes[0]]if axes[0]==1 else axes
        q=coords[:,axes];lo=q.min(0);span=np.maximum(q.max(0)-lo,.001);q=(q-lo)/span
        uvs=[((col*512+16+q[i,0]*480)/2048,(row*512+16+q[i,1]*480)/2048)for i in range(len(q))]
        self.uv.append(list(reversed(uvs)))
    def box(self,loc,size,key='wood',yaw=0):
        x,y,z=loc;sx,sy,sz=[v/2 for v in size];c,s=math.cos(math.radians(yaw)),math.sin(math.radians(yaw))
        p=[(-sx,-sy,-sz),(sx,-sy,-sz),(sx,-sy,sz),(-sx,-sy,sz),(-sx,sy,-sz),(sx,sy,-sz),(sx,sy,sz),(-sx,sy,sz)]
        p=[(x+c*a+s*d,y+b,z-s*a+c*d)for a,b,d in p]
        for f in [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]:self.face([p[i]for i in f],key)
    def beam(self,a,b,width=.12,key='wood',depth=None):
        a,b=Vector(a),Vector(b);v=(b-a).normalized();ref=Vector((0,0,1))if abs(v.z)<.9 else Vector((1,0,0));u=v.cross(ref).normalized()*width/2;w=u.normalized().cross(v).normalized()*(depth or width)/2
        p=[a-u-w,a+u-w,a+u+w,a-u+w,b-u-w,b+u-w,b+u+w,b-u+w]
        for f in [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]:self.face([p[i]for i in f],key)
    def loft(self,rings,key='stone',n=10,cx=0,cz=0,cap=True):
        p=[(cx+rx*math.cos(i*2*math.pi/n),y,cz+rz*math.sin(i*2*math.pi/n))for y,rx,rz in rings for i in range(n)]
        for j in range(len(rings)-1):
            for i in range(n):
                f=[p[j*n+i],p[(j+1)*n+i],p[(j+1)*n+(i+1)%n],p[j*n+(i+1)%n]]
                middle=np.mean(np.array(f),axis=0);self.face(f,key,(middle[0]-cx,0,middle[2]-cz))
        if cap:
            self.face(p[:n],key,(0,-1,0));self.face(p[-n:],key,(0,1,0))
    def cylinder(self,pos,radius,height,key='wood',n=10,r2=None):
        self.loft([(pos[1]-height/2,radius,radius),(pos[1]+height/2,radius if r2 is None else r2,radius if r2 is None else r2)],key,n,pos[0],pos[2])
    def ellipsoid(self,pos,size,key='stone',n=10,rows=6):
        rings=[]
        for j in range(rows+1):
            a=-math.pi/2+.015+(math.pi-.03)*j/rows;rings.append((pos[1]+math.sin(a)*size[1]/2,max(.003,math.cos(a)*size[0]/2),max(.003,math.cos(a)*size[2]/2)))
        self.loft(rings,key,n,pos[0],pos[2])
    def arch(self,cx,bottom,cz,width,height,depth,key='stone',segments=9,thickness=.25):
        radius=width/2;spring=bottom+height-radius
        for side in [-1,1]:self.box((cx+side*(radius+thickness/2),(bottom+spring)/2,cz),(thickness,spring-bottom,depth),key)
        for i in range(segments):
            a=i*math.pi/segments;b=(i+1)*math.pi/segments;inner=[(cx+radius*math.cos(t),spring+radius*math.sin(t))for t in [a,b]];outer=[(cx+(radius+thickness)*math.cos(t),spring+(radius+thickness)*math.sin(t))for t in [a,b]]
            face=[inner[0],outer[0],outer[1],inner[1]]
            for zz,norm in [(cz-depth/2,(0,0,-1)),(cz+depth/2,(0,0,1))]:self.face([(x,y,zz)for x,y in face],key,norm)
            for j in range(4):
                k=(j+1)%4;self.face([(face[j][0],face[j][1],cz-depth/2),(face[k][0],face[k][1],cz-depth/2),(face[k][0],face[k][1],cz+depth/2),(face[j][0],face[j][1],cz+depth/2)],key)
    def object(self,materials,collection=None):
        me=bpy.data.meshes.new(self.name);me.from_pydata([G(p)for p in self.v],[],self.f);me.update();uv=me.uv_layers.new(name='UVMap')
        for mat in materials:me.materials.append(mat)
        for face,key,coords in zip(me.polygons,self.keys,self.uv):
            face.material_index=MATERIALS[key][0]
            for li,q in zip(face.loop_indices,coords):uv.data[li].uv=q
        # Clipping circular ground tiles can leave collinear polygon corners.
        # Freeze triangulation and discard only zero-area triangles before counting/export.
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bad=[f for f in bm.faces if f.calc_area()<1e-10]
        if bad:bmesh.ops.delete(bm,geom=bad,context='FACES_ONLY')
        isolated=[v for v in bm.verts if not v.link_faces]
        if isolated:bmesh.ops.delete(bm,geom=isolated,context='VERTS')
        # Unity orders imported submeshes by first material use. Put architecture
        # faces first so every shared prop keeps the documented atlas slot order.
        bm.faces.sort(key=lambda face:face.material_index)
        bm.normal_update();bm.to_mesh(me);bm.free();me.update()
        ob=bpy.data.objects.new(self.name,me);(collection or bpy.context.scene.collection).objects.link(ob);return ob
def triangles(ob):ob.data.calc_loop_triangles();return len(ob.data.loop_triangles)
def export(obs,path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.hide_viewport=False;o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False,use_custom_props=False)
def roof(draft,w,d,eave=3.3,peak=5.0,key='roof',style=0):
    half=w/2+.45;front=d/2+.45;back=-d/2-.4
    # Each side is a curved, thick roof slab; lowest eave turns gently upwards.
    profile=[(0,peak),(.34,peak-.45),(.70,eave+.28),(1,eave+.24)]
    for side in [-1,1]:
        points=[(side*t*half,y)for t,y in profile]
        for j in range(len(points)-1):
            x0,y0=points[j];x1,y1=points[j+1]
            for a,b in [(back,front)]:
                p=[(x0,y0,a),(x0,y0,b),(x1,y1,b),(x1,y1,a)];draft.face(p,key,(0,1,0));draft.face([(x,y-.13,z)for x,y,z in p],'wood',(0,-1,0))
                for zz in [a,b]:draft.beam((x0,y0-.08,zz),(x1,y1-.08,zz),.18,'wood',.17)
        draft.beam((side*half,eave+.18,back),(side*half,eave+.18,front),.17,'wood')
        # Front gable braces and short upturned ends read at phone scale.
        draft.beam((0,peak-.16,front),(side*half,eave+.12,front),.14,'wood')
        draft.beam((side*half*.73,eave+.03,front),(side*half*.98,eave+.35,front),.11,'brass')
    draft.beam((0,peak+.05,back-.08),(0,peak+.05,front+.08),.19,'brass')
    for z in np.linspace(back,front,10):draft.cylinder((0,peak+.11,float(z)),.13,.11,'roof',8)

def lantern(d,x,y,z,scale=1):
    d.loft([(y-.32*scale,.16*scale,.16*scale),(y-.23*scale,.23*scale,.23*scale),(y+.23*scale,.23*scale,.23*scale),(y+.32*scale,.16*scale,.16*scale)],'fire',8,x,z)
    for yy in [-.29,.29]:d.box((x,y+yy*scale,z),(.42*scale,.08*scale,.42*scale),'wood')
    for a in range(4):
        t=(a+.5)*math.pi/2;d.beam((x+math.cos(t)*.26*scale,y-.28*scale,z+math.sin(t)*.26*scale),(x+math.cos(t)*.26*scale,y+.28*scale,z+math.sin(t)*.26*scale),.035*scale,'iron')
    d.beam((x,y+.32*scale,z),(x,y+.57*scale,z),.025*scale,'brass')
def barrel(d,x=0,z=0):
    d.loft([(0,.32,.32),(.12,.37,.37),(.72,.37,.37),(.9,.31,.31)],'wood',10,x,z)
    for y in [.12,.7]:d.cylinder((x,y,z),.379,.08,'iron',10)
def crate(d,x=0,z=0,scale=1):
    d.box((x,.42*scale,z),(.85*scale,.84*scale,.85*scale),'wood')
    for yy in [.09,.75]:
        for zz in [-.44,.44]:d.box((x,yy*scale,z+zz*scale),(.92*scale,.1*scale,.06*scale),'iron')
    for zz in [-.45,.45]:d.beam((x-.36*scale,.12*scale,z+zz*scale),(x+.36*scale,.72*scale,z+zz*scale),.075*scale,'wood')
def bottle(d,x,y,z,kind='glass',scale=1):
    d.loft([(y,.11*scale,.11*scale),(y+.27*scale,.13*scale,.13*scale),(y+.34*scale,.055*scale,.055*scale),(y+.44*scale,.055*scale,.055*scale)],kind,8,x,z)
    d.cylinder((x,y+.46*scale,z),.06*scale,.06*scale,'wood',8)
def table(d,x,y,z,w=2.,depth=.7):
    d.box((x,y,z),(w,.14,depth),'wood')
    for sx in [-1,1]:
        for sz in [-1,1]:d.box((x+sx*(w/2-.14),y/2,z+sz*(depth/2-.1)),(.15,y,.15),'wood')
def sign(d,key,x,y,z):
    d.box((x,y,z),(1.2,.75,.1),'wood');d.box((x,y,z+.065),(1.05,.60,.035),'teal'if key in ['guild','shop']else'red')
    zz=z+.094
    if key=='smith':
        d.box((x,y+.02,zz),(.58,.15,.03),'paper');d.box((x+.05,y-.14,zz),(.28,.23,.03),'paper');d.face([(x-.31,y+.09,zz+.02),(x-.50,y+.12,zz+.02),(x-.31,y-.05,zz+.02)],'paper',(0,0,1))
    elif key=='shop':bottle(d,x,y-.2,zz,'paper',.8)
    elif key=='guild':
        for side in [-1,1]:d.box((x+side*.16,y,zz),(.29,.36,.04),'paper',side*-7)
        d.box((x,y,zz+.03),(.035,.37,.03),'brass')
    else:
        d.cylinder((x,y,zz),.18,.07,'brass',8)
