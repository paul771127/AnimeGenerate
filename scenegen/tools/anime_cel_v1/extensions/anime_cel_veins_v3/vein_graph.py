"""Geometry-guided branching cracks. All distances and angle statistics use metre charts."""
import math
import numpy as np

def chart_key(normal):
    return ('x' if normal[0]>=0 else 'X') if abs(normal[0])>abs(normal[2]) else ('z' if normal[2]>=0 else 'Z')

def chart_points(points,key):
    return np.asarray(points)[...,[2,1] if key.lower()=='x' else [0,1]]

def build_graph(ob,game):
    pos=np.array([game(v.co) for v in ob.data.vertices],float);adjfaces={}
    for p in ob.data.polygons:
        for edge in p.edge_keys:adjfaces.setdefault(tuple(sorted(edge)),[]).append(p.index)
    result={k:{'points':[],'edges':[],'lookup':{}} for k in ['x','X','z','Z']}
    for edge in ob.data.edges:
        a,b=edge.vertices;faces=adjfaces.get(tuple(sorted((a,b))),[])
        if not faces:continue
        normals=[np.array(game(ob.data.polygons[i].normal)) for i in faces]
        angle=math.acos(float(np.clip(normals[0]@normals[1],-1,1))) if len(normals)>1 else .7
        for key in set(chart_key(n)for n in normals if abs(n[1])<.92):
            points=chart_points(pos[[a,b]],key);length=float(np.linalg.norm(points[1]-points[0]))
            if length<.055 or length>3:continue
            g=result[key];ids=[]
            for point in points:
                q=tuple(np.round(point/.025).astype(int))
                if q not in g['lookup']:g['lookup'][q]=len(g['points']);g['points'].append(point.tolist())
                ids.append(g['lookup'][q])
            if ids[0]==ids[1]:continue
            d=points[1]-points[0];vertical=abs(d[0])<abs(d[1])*math.tan(math.radians(15))
            # Prefer visible geometric folds over diagonals on a flat triangle.
            score=(.18+min(angle,1.5)*2.5)*(.055 if vertical else 1.)
            g['edges'].append({'a':ids[0],'b':ids[1],'score':score,'creaseRadians':angle,'sourceEdge':[int(a),int(b)]})
    for g in result.values():g.pop('lookup',None)
    return result

def generate_networks(graphs,bounds,seed,kind='medium'):
    rng=np.random.default_rng(seed);allsegments=[];height=bounds['max'][1];shorts=0;branches=0
    for key,g in graphs.items():
        points=np.array(g['points'],float);edges=g['edges']
        if len(points)<3 or not edges:continue
        adj={i:[]for i in range(len(points))}
        for i,e in enumerate(edges):adj[e['a']].append((i,e['b']));adj[e['b']].append((i,e['a']))
        used=set();serial=0
        def emit(a,b,width,tag,parent=None,edge=None):
            nonlocal serial
            length=float(np.linalg.norm(b-a))
            if length<.025:return
            # One or two tiny bends stay close to the underlying crease.
            n=max(1,int(math.ceil(length/.35)));d=b-a;perp=np.array([-d[1],d[0]])/length;v=[]
            wobble=float(rng.uniform(-.025,.025))
            for i in range(n+1):v.append(a+d*i/n+perp*wobble*math.sin(math.pi*i/n))
            r0=width*float(rng.uniform(.55,1.55));r1=width*float(rng.uniform(.45,1.45))
            for i in range(n):
                allsegments.append({'chart':key,'a':v[i].tolist(),'b':v[i+1].tolist(),'r0':r0+(r1-r0)*i/n,'r1':r0+(r1-r0)*(i+1)/n,'kind':tag,'parent':parent,'path':serial,'sourceMeshEdge':edge})
            serial+=1
        def walk(start,budget,width,tag,parent=None):
            node=start;route=[node];length=0.;last=None
            for _ in range(24):
                choices=[];weights=[]
                for ei,to in adj[node]:
                    if ei in used or to in route:continue
                    d=points[to]-points[node];ln=float(np.linalg.norm(d))
                    # Long gate pillars otherwise force nearly vertical walks.
                    # Stop at that crease and let another irregular branch take over.
                    if abs(d[0])<abs(d[1])*math.tan(math.radians(18)):continue
                    if points[to,1]>height*.92:continue
                    turn=1. if last is None else .4+1.2*(1-abs(float(last@d/max(np.linalg.norm(last)*ln,1e-8))))
                    low=.35+.65*max(0,1-points[to,1]/max(height,1))
                    choices.append((ei,to,ln));weights.append(edges[ei]['score']*turn*low)
                if not choices:break
                weights=np.array(weights);ei,to,ln=choices[int(rng.choice(len(choices),p=weights/weights.sum()))]
                emit(points[node],points[to],width,tag,parent,edges[ei]['sourceEdge']);used.add(ei);last=points[to]-points[node];node=to;route.append(node);length+=ln
                if length>=budget:break
            return route
        candidates=[i for i,p in enumerate(points) if .10<p[1]<height*.65 and len(adj[i])>=2]
        if not candidates:candidates=list(adj)
        if kind!='sparse':
            for cluster in range(1 if kind=='dense' else 2):
                start=int(rng.choice(candidates));route=walk(start,5.5 if kind=='dense' else 3.,.070 if kind=='dense' else .055,'main')
                if len(route)>2:
                    roots=rng.choice(route[1:-1],size=min(len(route)-2,3 if kind=='dense' else 2),replace=False)
                    for branch,root in enumerate(roots):
                        walk(int(root),float(rng.uniform(.35,2.1)),.035 if branch%2 else .05,'branch',cluster);branches+=1
        # Short 10-40 cm fissures are literal subsections of real geometric edges.
        count=2 if kind=='sparse' else 9 if kind=='dense' else 5
        usable=[e for e in edges if abs(points[e['a'],0]-points[e['b'],0])>math.tan(math.radians(20))*abs(points[e['a'],1]-points[e['b'],1]) and min(points[e['a'],1],points[e['b'],1])<height*.68]
        if not usable:usable=edges
        for _ in range(count):
            e=usable[int(rng.integers(len(usable)))];a=points[e['a']];b=points[e['b']];d=b-a;ln=float(np.linalg.norm(d));length=min(float(rng.uniform(.10,.40)),ln*.90);t=float(rng.uniform(0,max(0,1-length/max(ln,1e-8))))
            emit(a+d*t,a+d*(t+length/max(ln,1e-8)),.018 if kind=='sparse' else float(rng.uniform(.025,.055)),'short',edge=e['sourceEdge']);shorts+=1
    return allsegments,{'distribution':kind,'shortCracks':shorts,'branches':branches}

def distance_field(points,normal,segments,scale=1.):
    key=chart_key(normal);q=chart_points(points,key);result=np.full(len(q),1e4,np.float32)
    if not len(q):return result
    lo=q.min(0);hi=q.max(0)
    for s in segments:
        if s['chart']!=key:continue
        a=np.asarray(s['a']);b=np.asarray(s['b']);r=max(s['r0'],s['r1'])*scale*1.35
        if np.any(np.maximum(a,b)+r<lo)or np.any(np.minimum(a,b)-r>hi):continue
        d=b-a;den=max(float(d@d),1e-9);t=np.clip((q-a)@d/den,0,1);width=(s['r0']*(1-t)+s['r1']*t)
        # Wider/brighter where branches turn or meet.
        width*=1+.22*np.exp(-np.minimum(t,1-t)*12)
        value=np.linalg.norm(q-(a+t[:,None]*d),axis=1)/np.maximum(width,.002)
        result=np.minimum(result,value)
    return result

def direction_statistics(segments):
    count=0;vertical=0;total=0.;vertical_length=0.;angles=[]
    for s in segments:
        d=np.array(s['b'])-np.array(s['a']);length=float(np.linalg.norm(d))
        if length<1e-6:continue
        angle=math.degrees(math.atan2(abs(float(d[0])),abs(float(d[1]))));isvertical=angle<=15
        count+=1;vertical+=int(isvertical);total+=length;vertical_length+=length if isvertical else 0;angles.append(angle)
    return {'segments':count,'verticalSegments':vertical,'verticalFraction':vertical/max(count,1),'lengthWeightedVerticalFraction':vertical_length/max(total,1e-9),'totalLengthM':total,'angleDefinition':'Acute angle from projected local surface vertical in metre-scale H/Y charts; vertical <=15 degrees. Flow sheets and preserved seal/crater pools are not vein segments.','angleBins':[sum(a<=15 for a in angles),sum(15<a<=30 for a in angles),sum(30<a<=60 for a in angles),sum(60<a<=75 for a in angles),sum(75<a<=90 for a in angles)]}
