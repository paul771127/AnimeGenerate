"""007 complete town kit. No realtime light, normal map, particle or transparent VFX."""
import bpy,sys,pathlib,json,math,shutil,time,copy,numpy as np
from mathutils import Vector
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from town_spec import plan,transform,rect,facing
from town_mesh import *
from town_textures import bake,material
args=sys.argv[sys.argv.index('--')+1:]
def arg(n,d=None):return args[args.index(n)+1]if n in args else d
OUT=pathlib.Path(arg('--out'));SOURCE=pathlib.Path(arg('--source-kit'));REFERENCE=pathlib.Path(arg('--reference'));STONE=pathlib.Path(arg('--stone-source'));QUICK='--quick'in args
T0=time.time()
def log(*a):print('[town007 %.1fs]'%(time.time()-T0),*a,flush=True)
def dump(name,v):(OUT/name).write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
for d in ['fbx','previews']:(OUT/d).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1;scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
layout,layout_report=plan();layout['sky']={'horizon':'#cca894','zenith':'#5c94ad','mode':'use_existing_game_sky','newRuntimeTexture':False}
sourcekit=json.loads((SOURCE/'kit.json').read_text(encoding='utf8'));tex=bake(OUT,REFERENCE,STONE,SOURCE/sourcekit['textures']['ground']);mats=[material('TownArchitecture',OUT/tex[0]),material('TownDetails',OUT/tex[1])]
shutil.copyfile(str(SOURCE/sourcekit['textures']['backdropAlbedo']),str(OUT/'town0_backdrop_albedo.png'));backmat=material('TownBackdrop',OUT/'town0_backdrop_albedo.png')
COL=bpy.data.collections.new('ASSET_LIBRARY');scene.collection.children.link(COL);assets={};inventory=[]
def save_asset(file,drafts,atlas='architecture'):
    obs=[d.object(mats,COL)for d in drafts if d.f];export(obs,OUT/'fbx'/file)
    coords=np.array([game(v.co)for o in obs for v in o.data.vertices]);tris=sum(triangles(o)for o in obs)
    rec={'file':file,'triangles':tris,'objects':[o.name for o in obs],'atlas':atlas,'materialSlots':['architecture','details'],'bounds':{'min':coords.min(0).tolist(),'max':coords.max(0).tolist()},'pivot':[0,0,0]}
    assets[file]=obs;inventory.append(rec);log(file,tris)
    return obs
def footing(d,x,z):
    d.box((x,.15,z),(.50,.30,.50),'stone');d.box((x,1.73,z),(.25,3.12,.25),'wood')
    d.box((x,.36,z),(.32,.12,.32),'brass')
def room_shell(b):
    id=b['id'];w,depth=b['size'];door=b['door']['width'];d=Draft('BASE_'+id);r=Draft('ROOF_'+id);x=w/2;z=depth/2
    d.box((0,.01,0),(w,.02,depth),'wood')
    for i in range(9):d.box((-x+.2+(w-.4)*i/8,.026,0),(.035,.012,depth-.25),'iron')
    for side in [-1,1]:
        d.box((side*(x-.1),1.65,0),(.20,3.3,depth),'stone'if id in ['smith','dungeon']else'plaster')
        d.box((side*(x-.10),.27,0),(.22,.52,depth),'stone')
        for zz in [-z+.18,0,z-.16]:
            d.box((side*(x-.12),1.76,zz),(.24,3.1,.22),'wood')
        d.box((side*(x-.10),2.85,0),(.25,.16,depth),'wood')
        for yy in [1.1,2.2]:d.box((side*(x-.08),yy,0),(.25,.12,depth),'wood')
        # Outside window with opened wooden shutters; no view blocker inside the room.
        if id!='dungeon':
            for zz in [-.50,.50]:d.box((side*(x+.045),1.9,zz),(.07,.95,.50),'wood',side*8)
            d.box((side*(x+.02),1.9,0),(.06,.70,.34),'teal')
        front_width=(w-door)/2
        d.box((side*(door/2+front_width/2),1.65,z-.1),(front_width,3.3,.2),'stone'if id=='dungeon'else'wood')
        footing(d,side*(x-.2),z-.20);footing(d,side*(x-.2),-z+.2)
        d.beam((side*(x-.2),2.45,z-.18),(side*(x-.88),3.28,z-.18),.16,'wood')
    d.box((0,1.65,-z+.1),(w,3.3,.2),'stone'if id in ['smith','dungeon']else'plaster')
    for xx in np.linspace(-x+.2,x-.2,5):d.box((float(xx),1.75,-z+.2),(.16,3.1,.18),'wood')
    for yy in [.32,2.85]:d.box((0,yy,-z+.18),(w,.18,.23),'wood')
    d.box((0,3.47,z-.12),(w+.16,.32,.33),'wood');d.box((0,3.4,-z+.1),(w,.22,.25),'wood')
    # Plain, continuous ceiling/soffit gives genuine shelter and >=3.3m clearance.
    r.box((0,3.39,0),(w,.14,depth),'wood');roof(r,w,depth,3.5,5.2 if id=='guild'else 4.95)
    if id=='house_chief':
        d.arch(0,0,z+.03,door,3.3,.25,'wood',11,.12)
        # Parked door leaves are visible, while the whole 2.6m opening stays free.
        for side in [-1,1]:d.box((side*(door/2+.62),1.5,z+.06),(.9,2.85,.09),'wood')
    sign(d,id,0,3.08,z+.28)
    if id in ['shop','guild']:
        for side in [-1,1]:
            d.box((side*(x-.75),2.2,z+.08),(.54,1.05,.035),'teal'if id=='shop'else'red')
            d.box((side*(x-.75),1.78,z+.105),(.48,.05,.02),'brass')
    return d,r
def smith(b):
    d,r=room_shell(b);w,depth=b['size']
    # Basalt furnace, hot inset mouth, bellows and hood. All warmth is baked color.
    d.box((-1.55,.48,-2.86),(1.45,.96,.68),'stone');d.box((-1.55,1.2,-2.52),(.95,1.02,.045),'fire');d.arch(-1.55,.72,-2.45,1.02,1.40,.23,'brass',9,.13)
    d.loft([(1.6,.72,.44),(2.45,.48,.35),(3.2,.35,.32)],'iron',8,-1.55,-2.88)
    r.box((-1.55,4.7,-2.75),(.75,3.0,.8),'stone');r.box((-1.55,6.2,-2.75),(1.05,.23,1.05),'stone');r.box((-1.55,6.02,-2.34),(.50,.18,.03),'coal')
    for x in [-1.83,-1.27]:r.box((x,5.6,-2.32),(.12,.65,.04),'iron')
    d.box((.65,.42,-1.5),(.80,.84,.65),'wood');d.box((.65,.87,-1.5),(1.34,.18,.70),'iron');d.box((.60,1.07,-1.5),(1.05,.20,.50),'iron')
    d.face([(-.03,1.15,-1.22),(-.5,1.12,-1.22),(-.03,.98,-1.22)],'iron',(0,0,1));d.box((.7,.83,-1.165),(.38,.28,.03),'brass')
    d.box((1.96,.9,-2.65),(.42,1.8,1.0),'wood')
    for i in range(4):
        x=1.8+(i%2)*.29;z=-2.9+(i//2)*.45;d.beam((x,.6,z),(x,2.0,z),.05,'iron');d.box((x,1.7,z),(.32,.08,.12),'iron')
    d.box((-2.13,.37,-1.6),(.58,.70,.65),'wood');d.box((-2.13,.74,-1.6),(.49,.12,.54),'coal')
    return d,r
def shop(b):
    d,r=room_shell(b);table(d,0,1.0,-1.5,3.5,.7);d.box((0,.57,-1.5),(3.45,.83,.55),'wood');d.box((0,.56,-1.12),(3.1,.40,.035),'teal')
    for yy in [.5,1.25,2.05]:
        d.box((0,yy,-3.10),(4.6,.12,.25),'wood')
        for i in range(9):bottle(d,-2.0+i*.5,yy+.07,-3.06,'glass'if i%2 else'leaf',.6)
    for i in range(5):bottle(d,-1.2+i*.55,1.08,-1.5,'glass'if i%2 else'fire',.7)
    for x in [-1.55,1.55]:d.box((x,1.12,-1.36),(.45,.16,.32),'paper',-12)
    # Deep scalloped canvas awning adds a distinct silhouette without transparency.
    for i in range(8):
        x=-2.5+i*.71;r.box((x,3.15,3.85),(.72,.10,1.25),'teal'if i%2 else'cloth')
        r.face([(x-.36,3.1,4.48),(x+.36,3.1,4.48),(x+.30,2.84,4.48),(x,2.77,4.48),(x-.30,2.84,4.48)],'teal'if i%2 else'cloth',(0,0,1))
    return d,r
def guild(b):
    d,r=room_shell(b);table(d,0,1.0,-1.5,2.5,.7);d.box((0,.55,-1.48),(2.4,.85,.58),'wood');d.box((0,.6,-1.12),(.70,.48,.03),'red')
    for x in [-2.0,2.0]:
        for yy in [.55,1.3,2.15]:
            d.box((x,yy,-3.1),(.95,.11,.25),'wood')
            for i in range(5):d.box((x-.33+i*.16,yy+.26,-3.05),(.12,.42,.18),'paper'if i%2 else'teal')
    d.box((0,2.13,-3.14),(1.65,1.15,.035),'paper')
    for i in range(6):d.beam((-.65+i*.22,1.72,-3.10),(-.4+i*.12,2.48,-3.10),.025,'brass')
    d.box((-.4,1.1,-1.5),(.66,.08,.36),'paper',8);d.cylinder((.65,1.19,-1.5),.10,.22,'brass',8)
    r.beam((0,5.2,2.8),(0,5.85,2.8),.075,'brass');r.ellipsoid((0,5.65,2.8),(.5,.5,.15),'brass',8,4)
    return d,r
def chief(b):
    d,r=room_shell(b);table(d,0,1.0,-1.85,2.1,.7);d.box((0,.55,-1.85),(2.0,.82,.5),'wood')
    for x in [-1.9,1.9]:
        d.box((x,.4,-2.65),(.65,.8,.95),'wood');d.box((x,.84,-2.65),(.66,.12,.96),'red')
    d.box((0,1.5,-3.1),(4.6,2.1,.23),'wood');d.box((.6,2.08,-2.96),(.65,.85,.04),'paper');d.beam((.6,1.73,-2.92),(.6,2.4,-2.92),.055,'red')
    d.box((-.5,1.1,-1.85),(.6,.08,.36),'paper');d.cylinder((.55,1.22,-1.85),.14,.26,'brass',10)
    r.box((1.7,4.7,-2.5),(.58,1.7,.65),'stone');r.box((1.7,5.56,-2.5),(.78,.16,.85),'stone')
    return d,r
def dungeon(b):
    d,r=room_shell(b);z=b['size'][1]/2
    d.arch(0,.05,z+.03,4.4,3.8,.35,'stone',12,.18)
    # A real sheltered forecourt; the dark rear portal is a game interaction facade.
    d.box((0,1.45,-2.43),(2.1,2.85,.045),'coal');d.arch(0,0,-2.42,2.0,2.9,.14,'stone',9,.25)
    for side in [-1,1]:d.beam((side*.75,.03,-2.16),(side*.75,.03,-.9),.025,'iron')
    return d,r
def simple_building(b):
    id=b['id'];w,dep=b['size'];d=Draft('BASE_'+id);r=Draft('ROOF_'+id)
    if id.startswith('house_')or id=='tool_shed':
        d.box((0,.12,0),(w,.24,dep),'stone');d.box((0,1.5,0),(w-.12,2.8,dep-.12),'plaster')
        for sx in [-1,1]:
            for sz in [-1,1]:d.box((sx*(w/2-.1),1.55,sz*(dep/2-.1)),(.16,3.,.16),'wood')
        for yy in [.40,2.65]:d.box((0,yy,dep/2),(w,.14,.12),'wood')
        d.box((0,1.08,dep/2+.035),(.82,1.95,.08),'wood');d.box((-.70,1.85,dep/2+.06),(.40,.55,.04),'teal');d.box((.69,1.85,dep/2+.06),(.40,.55,.04),'teal')
        for side in [-1,1]:d.box((side*.70,1.85,dep/2+.09),(.035,.58,.03),'brass')
        roof(r,w,dep,2.95,3.95)
    elif id=='landmark':
        d.box((0,1.7,0),(2,3.4,2),'stone')
        for yy in [.28,1.9,3.3,6.65]:d.box((0,yy,0),(2.25,.22,2.25),'wood'if yy>3 else'stone')
        for sx in [-1,1]:
            for sz in [-1,1]:
                d.box((sx*.8,4.95,sz*.8),(.23,3.4,.23),'wood');d.beam((sx*.8,3.5,sz*.8),(-sx*.8,4.2,sz*.8),.14,'wood')
        d.box((0,6.4,0),(2.2,.25,.3),'wood')
        d.loft([(4.4,.69,.69),(4.55,.62,.62),(5.1,.45,.45),(5.8,.28,.28),(6.0,.15,.15)],'brass',16)
        d.cylinder((0,4.45,0),.705,.10,'brass',16);d.ellipsoid((0,4.3,0),(.18,.32,.18),'iron',8,4);d.beam((.24,.5,.21),(.24,5.5,.21),.035,'cloth')
        roof(r,2.4,2.4,6.7,8.1);r.beam((0,8.15,0),(0,9.1,0),.07,'brass');r.ellipsoid((0,8.8,0),(.36,.55,.18),'brass',8,4)
    elif id=='training':
        for x in [-2.,2.]:
            for z in [-1.95,.55]:d.box((x,.65,z),(.14,1.3,.14),'wood')
            for yy in [.5,1.0]:d.beam((x,yy,-1.95),(x,yy,.55),.07,'wood')
        for yy in [.5,1.0]:d.beam((-2.,yy,-1.95),(2.,yy,-1.95),.07,'wood')
        for x in [-1.05,1.05]:
            d.cylinder((x,.78,-1.2),.2,.72,'cloth',8);d.ellipsoid((x,1.38,-1.2),(.36,.38,.34),'cloth',8,4);d.beam((x-.38,1.04,-1.2),(x+.38,1.04,-1.2),.08,'wood');d.box((x,.12,-1.2),(.6,.24,.6),'stone')
        d.cylinder((0,.013,0),2.08,.026,'ash',32)
    elif id=='bounty':
        for x in [-1.15,1.15]:d.box((x,1.2,0),(.18,2.4,.2),'wood')
        d.box((0,1.35,0),(2.55,1.55,.20),'wood')
        for row in range(2):
            for col in range(4):
                x=-.86+col*.57;y=.94+row*.69;d.box((x,y,.12),(.44,.55,.025),'paper',-4+col*3)
                for i in range(3):d.box((x,y+.14-i*.1,.14),(.28-i*.03,.018,.012),'iron')
        roof(r,2.6,.55,2.25,2.7)
    elif id=='watch':
        for i in range(3):
            z=-.55+i*.53;h=.72-i*.2;d.box((0,h/2,z),(3,h,.5),'stone');d.box((0,h+.04,z),(2.9,.08,.47),'wood')
        for x in [-1.42,1.42]:
            d.box((x,1.75,-.65),(.11,2.0,.12),'wood');d.box((x,2.0,-.65),(.44,.6,.04),'red')
    return d,r

for b in layout['buildings']:
    fn={'smith':smith,'shop':shop,'guild':guild,'house_chief':chief,'dungeon':dungeon}.get(b['id'],simple_building)
    drafts=fn(b);obs=save_asset(b['file'],drafts);b['objects']=[o.name for o in obs];b['roofObjects']=[o.name for o in obs if o.name.startswith('ROOF_')]

def props():
    d=Draft('PROP_crate');crate(d);save_asset('prop_crate.fbx',[d]);d=Draft('PROP_barrel');barrel(d);save_asset('prop_barrel.fbx',[d])
    d=Draft('PROP_lantern');lantern(d,0,0,0,1);save_asset('prop_lantern.fbx',[d])
    d=Draft('PROP_brazier');d.loft([(0,.20,.20),(.2,.25,.25),(.65,.17,.17),(.82,.30,.30),(1,.34,.34)],'iron',10);d.loft([(.93,.25,.25),(1.15,.18,.18),(1.40,.07,.07)],'fire',8);save_asset('prop_brazier.fbx',[d])
    d=Draft('PROP_ash');d.ellipsoid((0,.08,0),(1.5,.16,1.1),'ash',10,4);save_asset('prop_ash.fbx',[d])
    d=Draft('PROP_well');d.loft([(0,.62,.62),(.55,.62,.62),(.68,.69,.69)],'stone',12,cap=False);d.cylinder((0,.12,0),.52,.025,'glass',12)
    for x in [-.55,.55]:d.box((x,1.25,0),(.12,1.7,.13),'wood')
    d.beam((-.7,1.65,0),(.7,1.65,0),.12,'wood');d.beam((0,.4,0),(0,1.7,0),.025,'cloth');roof(d,1.3,.8,2.0,2.55);save_asset('prop_well.fbx',[d])
    d=Draft('PROP_mila_cart');d.box((0,.6,0),(1.4,.20,2.6),'wood')
    for x in [-.65,.65]:d.box((x,.95,0),(.12,.58,2.6),'wood')
    for z in [-1.23,1.23]:d.box((0,.95,z),(1.4,.58,.12),'wood')
    for x in [-.82,.82]:
        for z in [-.88,.88]:
            # Wheel rings and spokes lie in the Y/Z plane.
            for i in range(10):
                a=i*math.pi/5;b=(i+1)*math.pi/5;d.beam((x,.48+math.sin(a)*.39,z+math.cos(a)*.39),(x,.48+math.sin(b)*.39,z+math.cos(b)*.39),.10,'wood')
            for i in range(5):
                a=i*math.pi/5;d.beam((x,.48+math.sin(a)*.34,z+math.cos(a)*.34),(x,.48-math.sin(a)*.34,z-math.cos(a)*.34),.035,'brass')
    for x in [-.58,.58]:d.box((x,1.7,1.04),(.08,1.5,.08),'wood')
    for i in range(4):d.box((-.55+i*.36,2.45,.45),(.37,.08,2.0),'cloth'if i%2 else'red')
    save_asset('prop_mila_cart.fbx',[d])
props()

# Shared mounted props: one reusable lantern FBX, one crate and one barrel FBX.
for building in layout['buildings']:
    if building['enterable']:
        for side in [-1,1]:
            pos=transform([side*(building['size'][0]/2-.55),building['size'][1]/2+.20],building['pos'],building['yaw'])
            layout['props'].append({'id':building['id']+'_lamp_'+str(side),'file':'prop_lantern.fbx','pos':pos,'height':2.7,'yaw':building['yaw'],'solid':0,'scale':.7,'mountOwner':building['id'],'atlas':'architecture'})
    elif building['id'].startswith('house_'):
        pos=transform([.62,building['size'][1]/2+.23],building['pos'],building['yaw'])
        layout['props'].append({'id':building['id']+'_lamp','file':'prop_lantern.fbx','pos':pos,'height':2.5,'yaw':building['yaw'],'solid':0,'scale':.42,'mountOwner':building['id'],'atlas':'architecture'})
for side in [-1,1]:
    pos=transform([side*1.6,.38],[13,5],90)
    layout['props'].append({'id':'gate_lamp_'+str(side),'file':'prop_lantern.fbx','pos':pos,'height':2.6,'yaw':90,'solid':0,'scale':.7,'mountOwner':'east_gate','atlas':'architecture'})
for id,file,local,scale in [('cart_crate','prop_crate.fbx',[0,-.55],.6),('cart_barrel','prop_barrel.fbx',[.3,.62],1)]:
    pos=transform(local,[5,3.4],90);layout['props'].append({'id':id,'file':file,'pos':pos,'height':.72,'yaw':90,'solid':0,'scale':scale,'mountOwner':'mila_cart','atlas':'architecture'})

def polygon_ground(d,poly,y,key):d.face([(x,y,z)for x,z in poly],key,(0,1,0))
def disk_tiles(d,cx,cz,radius,y,key,step=1.75):
    clip=[[cx+radius*math.cos(i*math.pi/48),cz+radius*math.sin(i*math.pi/48)]for i in range(96)]
    def cross(a,b,c):return(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    def clipped(poly):
        for a,b in zip(clip,clip[1:]+clip[:1]):
            if not poly:break
            result=[];prev=poly[-1];pv=cross(a,b,prev)
            for cur in poly:
                cv=cross(a,b,cur)
                if(cv>=0)!=(pv>=0):
                    t=pv/(pv-cv);result.append([prev[k]+t*(cur[k]-prev[k])for k in [0,1]])
                if cv>=0:result.append(cur)
                prev=cur;pv=cv
            poly=result
        return poly
    n=math.ceil(radius/step)
    for ix in range(-n,n):
        for iz in range(-n,n):
            poly=clipped([[cx+ix*step,cz+iz*step],[cx+(ix+1)*step,cz+iz*step],[cx+(ix+1)*step,cz+(iz+1)*step],[cx+ix*step,cz+(iz+1)*step]])
            if len(poly)>2:polygon_ground(d,poly,y,key)
d=Draft('GROUND_ash');disk_tiles(d,0,5,13,0,'ash');save_asset('ground_town.fbx',[d]);d=Draft('GROUND_paths');disk_tiles(d,0,5,3.5,.009,'pave')
for path in layout['paths']:
    for a,b in zip(path,path[1:]):
        v=Vector((b[0]-a[0],b[1]-a[1]));ln=v.length
        if ln<.01:continue
        side=Vector((-v.y,v.x)).normalized();steps=max(1,int(math.ceil(ln/1.8)))
        for k in range(steps):
            p=Vector(a)+v*k/steps;q=Vector(a)+v*(k+1)/steps
            polygon_ground(d,[p-side,q-side,q+side,p+side],.011,'pave')
    for x,z in path[1:-1]:
        polygon_ground(d,[[x+math.cos(j*math.pi/6),z+math.sin(j*math.pi/6)]for j in range(12)],.012,'pave')
save_asset('ground_paths.fbx',[d]);d=Draft('GROUND_warp')
for i in range(24):
    a=i*math.pi/12;b=(i+1)*math.pi/12;d.face([(.85*math.cos(a),.018,.85*math.sin(a)),(.95*math.cos(a),.018,.95*math.sin(a)),(.95*math.cos(b),.018,.95*math.sin(b)),(.85*math.cos(b),.018,.85*math.sin(b))],'brass',(0,1,0))
for i in range(6):
    a=i*math.pi/3;b=a+2*math.pi/3;d.beam((.65*math.cos(a),.022,.65*math.sin(a)),(.65*math.cos(b),.022,.65*math.sin(b)),.025,'glass')
save_asset('ground_warp.fbx',[d])
d=Draft('ENV_perimeter');start=math.asin(1.7/13)
for i in range(38):
    a=start+(2*math.pi-2*start)*i/38;b=start+(2*math.pi-2*start)*(i+1)/38
    p=(13*math.cos(a),.35,5+13*math.sin(a));q=(13*math.cos(b),.35,5+13*math.sin(b));d.beam(p,q,.7,'stone',.22)
    for yy in [.90,1.24]:d.beam((p[0],yy,p[2]),(q[0],yy,q[2]),.07,'wood')
    d.box((p[0],.65,p[2]),(.16,1.3,.16),'wood')
save_asset('env_perimeter.fbx',[d]);d=Draft('ENV_east_gate')
for x in [-1.6,1.6]:
    d.box((x,1.65,0),(.4,3.3,.5),'stone');d.box((x,.24,0),(.4,.48,.7),'stone')
d.box((0,3.5,0),(3.9,.35,.6),'wood');roof(d,3.2,.7,3.75,4.4);save_asset('env_east_gate.fbx',[d])
d=Draft('ENV_mine_entrance');d.box((0,1.7,-.5),(5.5,3.4,1.0),'stone');d.box((0,1.4,.03),(2.9,2.8,.04),'coal');d.arch(0,0,.1,3.,3.2,.55,'stone',12,.3)
for x in [-1.3,1.3]:d.box((x,1.5,.4),(.15,3.0,.16),'wood');d.beam((x*.7,.06,.4),(x*.7,.06,4),.06,'iron')
d.box((0,2.8,.4),(2.8,.18,.2),'wood');save_asset('env_mine_entrance.fbx',[d])
d=Draft('ENV_outside_ground');disk_tiles(d,0,5,65,-.035,'ash',step=5.)
save_asset('ground_outside.fbx',[d]);layout['environment'].append({'id':'outside_ground','file':'ground_outside.fbx','pos':[0,0],'yaw':0,'scale':1,'atlas':'architecture','solid':False,'outsideTown':True})
for old,new in [('backdrop_rock_a.fbx','env_volcano_a.fbx'),('backdrop_rock_b.fbx','env_volcano_b.fbx'),('backdrop_rock_c.fbx','env_cliff_a.fbx'),('backdrop_rock_d.fbx','env_cliff_b.fbx')]:
    shutil.copyfile(str(SOURCE/old),str(OUT/'fbx'/new));before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(SOURCE/old),use_anim=False,use_image_search=False);obs=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
    for o in obs:
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.data.uv_layers.active_index=0;o.data.materials.clear();o.data.materials.append(backmat)
    src=next(r for r in sourcekit['parts']if r['file']==old);rec=copy.deepcopy(src);rec.update(file=new,atlas='backdrop',materialSlots=['backdrop']);inventory.append(rec);assets[new]=obs
copies=[b['file']for b in layout['buildings']]+[p['file']for p in layout['props']]+[p['file']for p in layout['ground']]+[layout['gate']['file'],layout['perimeter']['file']]+[e['file']for e in layout['environment']]
byfile={p['file']:p for p in inventory};total=sum(byfile[f]['triangles']for f in copies);per_building={b['id']:byfile[b['file']]['triangles']for b in layout['buildings']}
assert total<=60000,(total,per_building);assert max(per_building.values())<=6000,per_building
kit={'version':1,'kit':'town0_cel_v1','town':0,'styleId':'ember_village_cel_v1','sourceStyle':'volcano_cel_v3','units':'metres','up':'+Y','forward':'+Z','textures':{'architecture':tex[0],'details':tex[1],'backdrop':'town0_backdrop_albedo.png'},'materialRule':'Built-in Unlit/Texture. Main mesh slots: 0 architecture, 1 details. Backdrop uses only backdrop. No .mat required.','layout':'town_layout.json','assets':inventory,'triangles':{'instancedTown':total,'uniqueFiles':sum(x['triangles']for x in inventory),'buildings':per_building},'previewNpcMarkersShipped':False,'runtimeLights':0,'transparentMaterials':0}
inspect={'ok':True,'quick':QUICK,'layout':layout_report,'triangleCounts':kit['triangles'],'checks':dict(layout_report['checks'],totalTrianglesWithin60000=total<=60000,singleBuildingWithin6000=max(per_building.values())<=6000,shared2048AtlasCountTwo=True,backdrop1024=True),'textureSizes':{tex[0]:[2048,2048],tex[1]:[2048,2048],'town0_backdrop_albedo.png':[1024,1024]},'phoneFpsMeasured':False,'runtimeLights':0,'allModelsActual3D':True}
dump('town_layout.json',layout);dump('kit.json',kit);dump('inspect.json',inspect)
# Private actual-3D preview marker: the game supplies its own characters.
d=Draft('PREVIEW_NPC_marker')
d.box((0,.85,0),(.42,.62,.27),'teal');d.ellipsoid((0,1.42,0),(.31,.36,.29),'paper',8,4)
for side in [-1,1]:d.beam((side*.13,.6,0),(side*.14,.10,.03),.12,'iron');d.beam((side*.27,1.07,0),(side*.32,.65,.03),.10,'cloth')
marker=d.object(mats);export([marker],OUT/'preview_npc.fbx');bpy.data.objects.remove(marker,do_unlink=True)
exec(compile((pathlib.Path(__file__).parent/'town_preview.py').read_text(encoding='utf8'),'town_preview.py','exec'))
log('COMPLETE',total,'triangles',len(inventory),'models')
