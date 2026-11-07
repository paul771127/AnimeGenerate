"""Deterministic geometry-space lava fields; NumPy is bundled with Blender 4.5."""
import numpy as np

def srgb_to_linear(x):
    x=np.asarray(x,dtype=np.float32)
    return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)

def hex_linear(h):
    return srgb_to_linear([int(h[i:i+2],16)/255 for i in (1,3,5)])

RED=hex_linear('#d8452a'); ORANGE=hex_linear('#ff8a2a'); YELLOW=hex_linear('#ffd36a')

def sample(image,uv):
    h,w=image.shape[:2]; uv=np.asarray(uv)
    xx=np.clip(uv[...,0]*w-.5,0,w-1); yy=np.clip(uv[...,1]*h-.5,0,h-1)
    x0=xx.astype(np.int32); y0=yy.astype(np.int32); x1=np.minimum(x0+1,w-1);y1=np.minimum(y0+1,h-1)
    ax=(xx-x0)[...,None];ay=(yy-y0)[...,None]
    return (image[y0,x0]*(1-ax)+image[y0,x1]*ax)*(1-ay)+(image[y1,x0]*(1-ax)+image[y1,x1]*ax)*ay

def curve_distance(points,normals,bounds,seed=0,kind='wall'):
    p=np.asarray(points);n=np.asarray(normals);lo=np.asarray(bounds['min']);hi=np.asarray(bounds['max']);span=np.maximum(hi-lo,.01)
    side=np.abs(n[...,0])>np.abs(n[...,2]); horizontal=np.where(side,(p[...,2]-lo[2])/span[2],(p[...,0]-lo[0])/span[0]); metres=np.where(side,span[2],span[0])
    y=(p[...,1]-lo[1])/span[1];phase=seed*.73
    result=np.full(y.shape,1e3,dtype=np.float32)
    paths=[(.20,.05,.92),(.50,.03,.59),(.77,.08,.38)] if kind=='wall' else [(.13,.04,.95),(.86,.08,.93),(.50,.63,.87)]
    for i,(centre,start,end) in enumerate(paths):
        x=centre+.038*np.sin(y*5.3+phase+i*1.4)+.018*np.sin(y*12.7+phase*.4+i)
        d=(horizontal-x)*metres
        ey=np.maximum(start-y,0)+np.maximum(y-end,0)
        result=np.minimum(result,np.sqrt(d*d+(ey*span[1])**2))
    return np.where(np.abs(n[...,1])<.94,result,1e3)

def door_level(points,width=.07):
    p=np.asarray(points);x=p[...,0];y=p[...,1]
    diamond=np.abs(np.abs(x)/.62+np.abs(y-1.85)/.92-1)*.48
    axis=np.maximum(np.abs(x),np.maximum(.65-y,y-3.05))
    return np.clip(1-np.minimum(diamond,axis)/width,0,1)

def cone_level(points,bounds,zone=0):
    p=np.asarray(points);lo=np.asarray(bounds['min']);hi=np.asarray(bounds['max']);c=(lo+hi)*.5;r=(hi-lo)*.5
    x=(p[...,0]-c[0])/r[0];z=(p[...,2]-c[2])/r[2];y=p[...,1]/hi[1]
    angle=np.arctan2(x,z);d=np.full(angle.shape,10.,np.float32)
    for phase,theta in [(0.,-.18),(1.7,.80)]:
        path=theta+.07*np.sin(y*8+phase)+.035*np.sin(y*17+phase)
        diff=np.arctan2(np.sin(angle-path),np.cos(angle-path))
        dist=np.abs(diff)*np.maximum(np.sqrt(x*x+z*z),.2)*min(r[0],r[2])
        outside=np.maximum(.08-y,0)+np.maximum(y-.98,0)
        d=np.minimum(d,np.sqrt(dist*dist+(outside*hi[1])**2))
    level=np.clip(1-d/.28,0,1)
    if zone==1:level=np.maximum(level,.64)  # hot rim
    if zone==2:level=np.maximum(level,.38)  # inner crater wall
    if zone==3:level=np.maximum(level,.94)  # liquid crater floor
    return level

def lava_color(level):
    # Three clean cel bands, with only a short transition at their edges.
    t=np.asarray(level)
    color=np.where((t>.44)[...,None],ORANGE,RED)
    color=np.where((t>.78)[...,None],YELLOW,color)
    return color

def coverage_samples(ob,game_vector,samples=8):
    ob.data.calc_loop_triangles(); uv=ob.data.uv_layers.active.data
    tri=ob.data.loop_triangles
    co=np.asarray([[game_vector(ob.data.vertices[v].co) for v in t.vertices] for t in tri],np.float32)
    u=np.asarray([[uv[l].uv[:] for l in t.loops] for t in tri],np.float32)
    n=np.asarray([game_vector(t.normal) for t in tri],np.float32)
    weights=[]
    for i in range(samples):
        for j in range(samples):
            a=(i+.5)/samples;b=(j+.5)/samples
            if a+b>1:a,b=1-a,1-b
            weights.append((1-a-b,a,b))
    bary=np.asarray(weights,np.float32)
    points=np.einsum('sj,tjk->tsk',bary,co).reshape(-1,3)
    coords=np.einsum('sj,tjk->tsk',bary,u).reshape(-1,2)
    normals=np.repeat(n,len(bary),axis=0)
    area=np.repeat(np.asarray([t.area for t in tri],np.float64)/len(bary),len(bary))
    return points,normals,coords,area

def fraction(ob,mask,game_vector):
    _,_,uv,area=coverage_samples(ob,game_vector)
    values=sample(mask[...,None] if mask.ndim==2 else mask,uv)[...,0]
    return float(area[values>.25].sum()/max(area.sum(),1e-9))
