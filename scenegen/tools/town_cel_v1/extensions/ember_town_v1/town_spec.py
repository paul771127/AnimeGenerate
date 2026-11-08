"""SpellDuel 007 world-space layout, collision and deterministic navigation plan.
Metres; Y-up in models, [x,z] in this data, yaw=0 faces +Z.
"""
import math,heapq,json
PLAZA=(0.,5.);RADIUS=13.
def transform(p,pos,yaw):
    a=math.radians(yaw);c,s=math.cos(a),math.sin(a)
    return [round(pos[0]+c*p[0]+s*p[1],5),round(pos[1]-s*p[0]+c*p[1],5)]
def facing(pos):return math.degrees(math.atan2(-pos[0],5-pos[1]))%360
def rect(x,z,w,d):return [[x-w/2,z-d/2],[x+w/2,z-d/2],[x+w/2,z+d/2],[x-w/2,z+d/2]]
def area(poly):return abs(sum(a[0]*b[1]-b[0]*a[1]for a,b in zip(poly,poly[1:]+poly[:1])))/2
def inside(p,poly):
    yes=False;x,z=p
    for a,b in zip(poly,poly[1:]+poly[:1]):
        if(a[1]>z)!=(b[1]>z)and x<(b[0]-a[0])*(z-a[1])/(b[1]-a[1])+a[0]:yes=not yes
    return yes
def edge_distance(p,a,b):
    dx,dz=b[0]-a[0],b[1]-a[1];t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dz)/max(dx*dx+dz*dz,1e-12)))
    return math.hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dz)
def distance(p,poly):return 0. if inside(p,poly)else min(edge_distance(p,a,b)for a,b in zip(poly,poly[1:]+poly[:1]))
def shell(w,d,door,t=.2):
    x,z=w/2,d/2;q=door/2
    return [[-x,z],[-x,-z],[x,-z],[x,z],[q,z],[q,z-t],[x-t,z-t],[x-t,-z+t],[-x+t,-z+t],[-x+t,z-t],[-q,z-t],[-q,z]]

def make_layout():
    l={'version':1,'schema':'spellduel.town-layout.v1','town':0,'name':'炎之里','styleId':'ember_village_cel_v1','units':'metres','up':'+Y','forward':'+Z','plaza':[0,5],'radius':13,'spawn':[0,0],'spawnYaw':0,'plazaClearRadius':3.5,'exits':[{'id':'east','pos':[13,5],'halfWidth':1.4,'directionYaw':90}],'warp':[5,.5],'buildings':[],'props':[],'npcs':[],'paths':[],'pathInfo':[],'colliders':[],'environment':[],'coordinateNote':'Every polygon, door, sign and NPC position is in village world coordinates. FBX geometry is building-local; apply pos/yaw once. Subtract colliders from floor walkable polygons. NPCs remain owned by the game.'}
    def collision(id,owner,poly,low=0,high=3.3):
        rec={'id':id,'owner':owner,'polygon':poly,'minY':low,'maxY':high};l['colliders'].append(rec);return id
    definitions=[('smith',[-7,8],5.6,6.8,True,4.6),('shop',[7.8,9.2],5.6,6.8,True,4.6),('guild',[0,13.5],5.8,6.8,True,4.8),('house_chief',[0,-3.6],5.6,6.8,True,2.6),('dungeon',[-8.7,1.7],5.4,5.2,True,4.0),('training',[9,.4],4.2,4.2,False,4.2),('bounty',[-5.0,-4.2],2.6,.6,False,2.6),('watch',[4.8,-4.4],3.0,1.6,False,3.),('landmark',[-6.5,13.0],2.3,2.3,False,0),('house_a',[-8,-2.7],2.4,2.4,False,0),('house_b',[7.9,-3.0],2.2,2.2,False,0),('house_c',[6.0,14.2],2.4,2.4,False,0),('tool_shed',[-4.7,15.6],1.8,1.5,False,0)]
    by={}
    for id,pos,w,d,enter,door in definitions:
        yaw=facing(pos);yaw=0. if id in ['house_chief','bounty','watch']else yaw
        file='bld_belltower.fbx'if id=='landmark'else'bld_'+id+'.fbx'
        b={'id':id,'file':file,'pos':pos,'yaw':round(yaw,5),'enterable':enter,'size':[w,d],'footprint':[transform(p,pos,yaw)for p in rect(0,0,w,d)],'solid':[],'solidPolygons':[],'colliderIds':[],'walkable':[],'interior':[],'door':None,'sign':transform([0,d/2+.05],pos,yaw)[:1]+[3.0]+transform([0,d/2+.05],pos,yaw)[1:]}
        def add(label,poly,height=3.3):
            world=[transform(p,pos,yaw)for p in poly];b['solidPolygons'].append(world);b['colliderIds'].append(collision(id+':'+label,id,world,0,height));return world
        if enter:
            b['solid']=add('walls',shell(w,d,door));b['interior']=[transform(p,pos,yaw)for p in rect(0,0,w-.4,d-.4)];b['walkable']=b['interior'][:]
            clear_centre=0. if id=='dungeon'else .9 if id=='house_chief'else 1.2
            clear=rect(0,clear_centre,4.0,4.0)
            b.update(clearWalkRectangle=[transform(p,pos,yaw)for p in clear],clearWalkSize=[4.0,4.0],clearWalkCenter=[0,clear_centre],ceilingHeight=3.3,door={'pos':transform([0,d/2],pos,yaw),'width':door,'height':2.65,'yaw':round(yaw,5),'approach':transform([0,d/2+.1],pos,yaw)},semiIndoor=id=='dungeon',interiorFloorY=.02)
            if id=='smith':
                add('anvil',rect(.65,-1.5,1.35,.7),1.1);add('forge',rect(-1.55,-2.86,1.45,.68),2.5);add('tool_rack',rect(1.95,-2.65,.45,1.0),2.1);add('coal_bin',rect(-2.13,-1.6,.58,.65),.8)
            elif id in ['shop','guild','house_chief']:
                width=3.5 if id=='shop'else 2.5 if id=='guild'else 2.1
                add('counter',rect(0,-1.85 if id=='house_chief'else -1.5,width,.7),1.05);add('shelves',rect(0,-3.1,w-1.0,.25),2.4)
                if id=='house_chief':
                    for side in [-1,1]:add('seat'+str(side),rect(side*1.9,-2.65,.65,.95),.9)
            b['floorArea']=area(b['interior']);b['walkableArea']=b['floorArea']-sum(area(p)for p in b['solidPolygons'][1:]);b['clearWalkArea']=area(b['clearWalkRectangle'])
        elif id=='training':
            b['solid']=add('back_rail',rect(0,-1.95,4.2,.18),1.2)
            for x in [-2.0,2.0]:add('side_rail'+str(x),rect(x,-.7,.18,2.5),1.2)
            for x in [-1.05,1.05]:add('dummy'+str(x),rect(x,-1.2,.7,.7),1.7)
            b['door']={'pos':transform([0,d/2],pos,yaw),'width':4.2,'yaw':round(yaw,5)}
        else:
            b['solid']=add('body',rect(0,0,w,d),9.2 if id=='landmark'else 3.4)
            if id in ['bounty','watch']:b['door']={'pos':transform([0,2.7 if id=='bounty'else 3.0],pos,yaw),'width':2.4,'yaw':round(yaw,5)}
        l['buildings'].append(b);by[id]=b
    def npc(role,name,bid,local,anchor,inside_id=True):
        b=by[bid];p=transform(local,b['pos'],b['yaw']);a=transform(anchor,b['pos'],b['yaw']);l['npcs'].append({'role':role,'name':name,'pos':p,'yaw':b['yaw'],'inside':bid if inside_id else None,'facility':bid,'conversation':{'playerPos':a,'distance':round(math.dist(p,a),4)if hasattr(math,'dist')else round(math.hypot(p[0]-a[0],p[1]-a[1]),4),'range':[1.2,2.2]},'wander':{'enabled':False,'region':bid if inside_id else None},'homeLocal':local})
    npc('smith','鐵匠','smith',[.65,-2.65],[.65,-.75]);npc('story:小鐵','見習鐵匠・小鐵','smith',[-1.45,-1.9],[-1.45,0])
    npc('shop','店員','shop',[0,-2.65],[0,-.75]);npc('guild','公會長','guild',[0,-2.65],[0,-.75]);npc('story:老焰','村長・老焰','house_chief',[-.65,-2.6],[-.65,-.7])
    npc('dungeon','嚮導','dungeon',[0,-1.6],[0,.3]);npc('training','教官','training',[0,.2],[0,2.1],False);npc('bounty','懸賞官','bounty',[0,.8],[0,2.7],False);npc('watch','播報員','watch',[0,1.1],[0,3.0],False)
    # A common cart model, outside the clear centre; merchant stands beside it.
    cart={'id':'mila_cart','file':'prop_mila_cart.fbx','pos':[5.0,3.4],'yaw':90,'solid':0,'solidPolygon':rect(5.0,3.4,2.6,1.4),'scale':1,'atlas':'architecture'};l['props'].append(cart);cart['colliderId']=collision('mila_cart','mila_cart',cart['solidPolygon'],0,2.4)
    l['npcs'].append({'role':'story:米菈','name':'旅行商人・米菈','pos':[3.4,3.4],'yaw':270,'inside':None,'facility':'mila_cart','conversation':{'playerPos':[1.5,3.4],'distance':1.9,'range':[1.2,2.2]},'wander':{'enabled':False,'region':None}})
    # Shared props sit out of circulation or are mounted above heads.
    props=[('well','prop_well.fbx',[4.2,12.1],0,.72),('braziers_w','prop_brazier.fbx',[-6.5,11.1],0,.35),('braziers_e','prop_brazier.fbx',[3.5,15.7],0,.35),('crate_a','prop_crate.fbx',[-10.23,10.47],30,.67),('crate_b','prop_crate.fbx',[-11.02,8.63],-10,.67),('barrel_a','prop_barrel.fbx',[6.3,11.8],0,.39),('ash_a','prop_ash.fbx',[-10.2,11.7],0,0),('ash_b','prop_ash.fbx',[10.3,10.8],0,0)]
    for id,file,pos,yaw,r in props:
        p={'id':id,'file':file,'pos':pos,'yaw':yaw,'solid':r,'scale':1,'atlas':'architecture'};l['props'].append(p)
        if r:p['colliderId']=collision(id,id,[[pos[0]+math.cos(i*math.pi/4)*r,pos[1]+math.sin(i*math.pi/4)*r]for i in range(8)],0,2.3 if id=='well'else 1.0)
    # Fence segments leave a real, unobstructed 2.8m gate aperture at east.
    start=math.asin(1.7/13);n=38
    for i in range(n):
        a=start+(2*math.pi-2*start)*i/n;b=start+(2*math.pi-2*start)*(i+1)/n
        p=[(13*math.cos(a),5+13*math.sin(a)),(13*math.cos(b),5+13*math.sin(b))];mid=[(p[0][j]+p[1][j])/2 for j in [0,1]];length=math.hypot(p[1][0]-p[0][0],p[1][1]-p[0][1]);yaw=math.degrees(math.atan2(p[1][0]-p[0][0],p[1][1]-p[0][1]))
        poly=[transform(q,mid,yaw)for q in rect(0,0,.24,length+.025)];collision('fence:'+str(i),'perimeter',poly,0,1.3)
    l['perimeter']={'file':'env_perimeter.fbx','pos':[0,0],'yaw':0,'radius':13,'eastGapWidth':2.8,'colliderIds':[c['id']for c in l['colliders']if c['owner']=='perimeter']}
    l['gate']={'file':'env_east_gate.fbx','pos':[13,5],'yaw':90,'doorWidth':2.8,'clearHeight':3.3}
    for side in [-1,1]:collision('east_gate:'+str(side),'east_gate',rect(13,5+side*1.6,.7,.4),0,3.3)
    l['ground']=[{'file':'ground_town.fbx','pos':[0,0],'yaw':0},{'file':'ground_paths.fbx','pos':[0,0],'yaw':0},{'file':'ground_warp.fbx','pos':[5,.5],'yaw':0}]
    l['environment']=[{'id':'mine','file':'env_mine_entrance.fbx','pos':[19,5],'yaw':270,'scale':1,'atlas':'architecture','solid':False,'outsideTown':True},
        {'id':'volcano_a','file':'env_volcano_a.fbx','pos':[30,42],'yaw':-15,'scale':1.1,'atlas':'backdrop','solid':False,'outsideTown':True},
        {'id':'volcano_b','file':'env_volcano_b.fbx','pos':[-36,45],'yaw':25,'scale':.85,'atlas':'backdrop','solid':False,'outsideTown':True}]
    for i,(x,z,scale)in enumerate([(-26,7,1.2),(-14,30,1.2),(14,30,1.2),(26,14,1.2)]):l['environment'].append({'id':'cliff_'+str(i),'file':'env_cliff_'+('a'if i%2==0 else'b')+'.fbx','pos':[x,z],'yaw':facing([x,z]),'scale':scale,'atlas':'backdrop','solid':False,'outsideTown':True})
    l['landmarkState']={'building':'landmark','bell':'silent','activationEvent':'chapter1_boss_defeated','audioOwnedBy':'game'}
    return l

def clearance(point,colliders):return min(distance(point,c['polygon'])for c in colliders)
def segment_clear(a,b,colliders,margin,include_boundary=True):
    ln=math.hypot(b[0]-a[0],b[1]-a[1]);steps=max(1,int(math.ceil(ln/.12)))
    for i in range(steps+1):
        p=[a[j]+(b[j]-a[j])*i/steps for j in [0,1]]
        if include_boundary and math.hypot(p[0],p[1]-5)>13-margin+.02:return False
        if clearance(p,colliders)<margin-1e-5:return False
    return True
def route(start,end,colliders,margin=1.03,boundary=True):
    step=.25;lo=(-14,-9);hi=(14,19)
    def grid(p):return(round((p[0]-lo[0])/step),round((p[1]-lo[1])/step))
    def point(q):return[lo[0]+q[0]*step,lo[1]+q[1]*step]
    begin=grid(start);finish=grid(end);cache={}
    def free(q):
        if q not in cache:
            p=point(q);cache[q]=(not boundary or math.hypot(p[0],p[1]-5)<=13-margin+.02)and clearance(p,colliders)>=margin
        return cache[q]
    if segment_clear(start,end,colliders,margin,boundary):return [list(start),list(end)]
    # The exact endpoint may be between grid centres; connect to nearby clear cells.
    targets=set()
    for dx in range(-2,3):
        for dz in range(-2,3):
            q=(finish[0]+dx,finish[1]+dz)
            if free(q)and segment_clear(point(q),end,colliders,margin,boundary):targets.add(q)
    if not targets:raise ValueError('No route endpoint clearance: '+str(end)+' / '+str(clearance(end,colliders)))
    queue=[(0,0,begin)];cost={begin:0};parent={}
    while queue:
        _,g,q=heapq.heappop(queue)
        if g>cost[q]+1e-8:continue
        if q in targets:
            path=[list(end),point(q)]
            while q in parent:q=parent[q];path.append(point(q))
            path.append(list(start));path.reverse();simple=[path[0]];i=0
            while i<len(path)-1:
                j=len(path)-1
                while j>i+1 and not segment_clear(path[i],path[j],colliders,margin,boundary):j-=1
                simple.append(path[j]);i=j
            return simple
        for dx,dz in [(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)]:
            n=(q[0]+dx,q[1]+dz)
            if not free(n):continue
            if dx and dz and(not free((q[0]+dx,q[1]))or not free((q[0],q[1]+dz))):continue
            ng=g+math.hypot(dx,dz)*step
            if ng<cost.get(n,1e9):cost[n]=ng;parent[n]=q;heur=math.hypot(point(n)[0]-end[0],point(n)[1]-end[1]);heapq.heappush(queue,(ng+heur,ng,n))
    raise ValueError('No connected route '+str(start)+' -> '+str(end))

def plan():
    l=make_layout();coll=l['colliders'];report={'buildings':{},'npcs':{},'paths':{},'checks':{}}
    for b in l['buildings']:
        if b['enterable']:
            assert min(b['clearWalkSize'])>=4 and b['ceilingHeight']>=3 and b['door']['width']>=1.8
            # Actual clear rectangle checked against furniture and walls, not just a label.
            centre=[sum(p[j]for p in b['clearWalkRectangle'])/4 for j in [0,1]]
            for i in range(13):
                for j in range(13):
                    a=b['clearWalkRectangle'];p=[a[0][k]+(a[1][k]-a[0][k])*i/12+(a[3][k]-a[0][k])*j/12 for k in [0,1]]
                    assert clearance(p,coll)>.015,(b['id'],'clear rectangle blocked',p,clearance(p,coll))
            report['buildings'][b['id']]={'doorWidth':b['door']['width'],'ceilingHeight':b['ceilingHeight'],'interiorFloorArea':b['floorArea'],'walkableAreaAfterSolids':b['walkableArea'],'clearRectangleSize':b['clearWalkSize'],'clearWalkArea':b['clearWalkArea']}
    for n in l['npcs']:
        if n['inside']:
            b=next(b for b in l['buildings']if b['id']==n['inside']);assert inside(n['pos'],b['interior']),(n['role'],'outside room')
        assert clearance(n['pos'],coll)>.16,(n['role'],'NPC intersects solid',clearance(n['pos'],coll))
        assert 1.2<=n['conversation']['distance']<=2.2
        assert clearance(n['conversation']['playerPos'],coll)>.3,(n['role'],'interaction spot blocked')
        b=next((b for b in l['buildings']if b['id']==n['facility']),None)
        door=b['door']['pos']if b and b['door']else n['conversation']['playerPos']
        report['npcs'][n['role']]={'inside':n['inside'],'distanceToDoor':math.hypot(n['pos'][0]-door[0],n['pos'][1]-door[1]),'conversationDistance':n['conversation']['distance'],'solidClearance':clearance(n['pos'],coll)}
    destinations=[('plaza',[0,5])]+[(b['id'],b['door']['pos'])for b in l['buildings']if b['door']]+[('warp',l['warp']),('mila_cart',[1.5,3.4]),('east',[13,5])]
    for id,end in destinations:
        start=[0,0]if id=='plaza'else[0,5];path=route(start,end,coll,1.03,id!='east');l['paths'].append(path);l['pathInfo'].append({'id':id,'width':2.0,'pathIndex':len(l['paths'])-1})
        minimum=min(clearance([a[j]+(b[j]-a[j])*t/20 for j in [0,1]],coll)for a,b in zip(path,path[1:])for t in range(21))
        report['paths'][id]={'width':2.0,'minimumCentreClearance':minimum,'length':sum(math.hypot(b[0]-a[0],b[1]-a[1])for a,b in zip(path,path[1:]))}
    l['interactionRoutes']=[]
    for n in l['npcs']:
        if not n['inside']:continue
        b=next(b for b in l['buildings']if b['id']==n['inside']);r=route(b['door']['pos'],n['conversation']['playerPos'],coll,.31)
        l['interactionRoutes'].append({'npc':n['role'],'points':r,'playerRadius':.3})
    # The plaza's 3.5m disk has no physical scenery or NPC occupant.
    plaza_clear=min(clearance([math.cos(a*math.pi/40)*r,5+math.sin(a*math.pi/40)*r],coll)for r in [0,1,2,3,3.5]for a in range(80));assert plaza_clear>.0,plaza_clear
    report['minimumRoadWidth']=min(p['width']for p in l['pathInfo']);report['plazaBoundarySolidClearance']=plaza_clear
    report['checks']={'doorsAtLeast1_8m':True,'interiorClearRectangleAtLeast4By4':True,'ceilingsAtLeast3m':True,'npcIndoorMembership':True,'npcInteractionDistances':True,'allFacilityRoadsConnected':True,'roadWidthAtLeast2m':True,'interiorPlayerRoutesConnected':True,'plazaClear':True}
    return l,report

if __name__=='__main__':
    import pathlib,sys
    l,r=plan();dest=pathlib.Path(sys.argv[1])if len(sys.argv)>1 else pathlib.Path('.')
    dest.mkdir(parents=True,exist_ok=True)
    for name,v in [('town_layout.json',l),('layout-inspect.json',r)]:(dest/name).write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
    print('TOWN007_LAYOUT_PASS',len(l['buildings']),len(l['npcs']),len(l['paths']))
