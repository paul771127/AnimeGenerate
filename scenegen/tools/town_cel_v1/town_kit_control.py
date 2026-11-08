"""Additive SpellDuel town kit API; existing control.py/cel_kit_control.py stay intact."""
import argparse,pathlib,json,hashlib,os,sys,subprocess,time,struct,math
ROOT=pathlib.Path(__file__).resolve().parent;BACKEND=ROOT/'extensions/ember_town_v1';STYLE='ember_village_cel_v1'
sys.path.insert(0,str(BACKEND))
from town_spec import plan
class RequestError(Exception):
    def __init__(self,code,message):self.code=code;super(RequestError,self).__init__(message)
def read(p):return json.loads(pathlib.Path(p).read_text(encoding='utf-8-sig'))
def write(p,v):p=pathlib.Path(p);p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def resolve(p,base):
    if not isinstance(p,str)or not p.strip():raise RequestError('invalid_request','Expected a path string')
    p=pathlib.Path(p);return(p if p.is_absolute()else base/p).resolve()
def png(p):
    b=pathlib.Path(p).read_bytes()[:24]
    if b[:8]!=b'\x89PNG\r\n\x1a\n':raise RequestError('invalid_config','Expected PNG: '+str(p))
    return struct.unpack('>II',b[16:24])
def config(req,base):
    if('config'in req)==('configFile'in req):raise RequestError('invalid_request','Provide exactly one config/configFile')
    if 'configFile'in req:p=resolve(req['configFile'],base);cfg=read(p);cb=p.parent
    else:cfg=dict(req['config']);cb=base
    if cfg.get('styleId',STYLE)!=STYLE:raise RequestError('invalid_config','Unsupported town styleId')
    if cfg.get('quality','mobile')not in ['mobile','draft']:raise RequestError('invalid_config','quality must be mobile/draft')
    result={'styleId':STYLE,'quality':cfg.get('quality','mobile')}
    for key in ['sourceKit','materialReference','stoneSource']:
        path=resolve(cfg[key],cb);result[key]=str(path)
        if key=='sourceKit':
            kit=read(path/'kit.json')
            if kit.get('version')!=6 or kit.get('styleId')!='anime_cel_volcano_veins_v3':raise RequestError('invalid_config','007 requires volcano_cel_v3, kit.version=6')
            for name in ['backdrop_rock_a.fbx','backdrop_rock_b.fbx','backdrop_rock_c.fbx','backdrop_rock_d.fbx',kit['textures']['backdropAlbedo'],kit['textures']['ground']]:
                if not(path/name).is_file():raise RequestError('invalid_config','Missing source '+name)
        else:
            w,h=png(path)
            if w!=h or w<1024:raise RequestError('invalid_config','Square source atlas >=1024 required')
    return result
def inspect(folder):
    if not folder.is_dir():raise RequestError('output_missing',str(folder))
    for name in ['kit.json','town_layout.json','inspect.json','previews/blender_spawn_view.png','previews/blender_smith_inside.png','previews/blender_shop_inside.png','previews/blender_guild_inside.png','previews/blender_east_exit.png']:
        if not(folder/name).is_file():raise RequestError('invalid_output','Missing '+name)
    kit=read(folder/'kit.json');rep=read(folder/'inspect.json')
    if not rep.get('ok')or rep.get('quick')or not all(rep.get('checks',{}).values())or not rep.get('exportAudit',{}).get('ok'):raise RequestError('invalid_output','Full generation and independent FBX audit must pass')
    names=['kit.json','town_layout.json']+list(kit['textures'].values())+['fbx/'+p['file']for p in kit['assets']]
    inventory=[]
    for name in sorted(names):
        p=(folder/name).resolve()
        try:p.relative_to(folder.resolve())
        except ValueError:raise RequestError('invalid_output','Unsafe output '+name)
        if not p.is_file():raise RequestError('invalid_output','Missing '+name)
        inventory.append({'file':name,'bytes':p.stat().st_size,'sha256':sha(p)})
    return {'valid':True,'outputFolder':str(folder),'statistics':kit['triangles'],'layoutChecks':rep['layout']['checks'],'assetInventory':inventory,'assetBytes':sum(x['bytes']for x in inventory),'phoneFpsMeasured':False}
def respond(req,base=None):
    base=pathlib.Path(base or'.').resolve();r={'apiVersion':1,'requestId':req.get('requestId',''),'ok':False}
    try:
        if req.get('apiVersion')!=1:raise RequestError('unsupported_version','apiVersion must be 1')
        action=req.get('action')
        if action=='capabilities':res={'apiVersion':1,'generator':'ember_town_kit','styleIds':[STYLE],'actions':['capabilities','plan','validate','generate','inspect'],'units':'metres, Y-up, +Z; 2D coordinates [x,z]','townId':0,'constraints':{'radius':13,'doorMinimum':1.8,'clearRoomMinimum':[4,4],'ceilingMinimum':3,'roadMinimum':2,'townTriangleLimit':60000,'buildingTriangleLimit':6000,'sharedAtlasSizes':[2048,2048,1024]},'coreApiChanged':False}
        elif action=='inspect':res=inspect(resolve(req.get('outputFolder'),base))
        elif action in ['plan','validate','generate']:
            cfg=config(req,base);layout,checks=plan();res={'styleId':STYLE,'buildings':len(layout['buildings']),'npcs':len(layout['npcs']),'routes':len(layout['paths']),'layout':layout,'layoutChecks':checks,'sourceKitSha256':sha(pathlib.Path(cfg['sourceKit'])/'kit.json'),'materialReferenceSha256':sha(cfg['materialReference']),'stoneSourceSha256':sha(cfg['stoneSource'])}
            if action=='validate':res['valid']=True
            if action=='generate':
                folder=resolve(req.get('outputFolder'),base)
                try:folder.mkdir(parents=True,exist_ok=False)
                except FileExistsError:raise RequestError('output_exists',str(folder))
                write(folder/'config.json',cfg);write(folder/'status.json',{'status':'running','requestId':r['requestId']})
                blender=resolve(req['blender'],base)if'blender'in req else pathlib.Path(os.environ.get('BLENDER_PATH',str(ROOT/'tools/blender-4.5.15-windows-x64/blender.exe')))
                builder=BACKEND/'town_builder.py';audit=BACKEND/'validate_town_exports.py'
                if not blender.is_file()or not builder.is_file()or not audit.is_file():
                    write(folder/'status.json',{'status':'failed','error':'blender_missing','requestId':r['requestId']})
                    raise RequestError('blender_missing','Blender or backend missing')
                argv=[str(blender),'-b','--python-exit-code','1','--python',str(builder),'--','--out',str(folder),'--source-kit',cfg['sourceKit'],'--reference',cfg['materialReference'],'--stone-source',cfg['stoneSource']]
                if cfg['quality']=='draft':argv.append('--quick')
                auditargv=[str(blender),'-b','--python-exit-code','1','--python',str(audit),'--','--run',str(folder)]
                write(folder/'build-command.json',{'argv':argv,'auditArgv':auditargv,'sources':{p.name:sha(p)for p in BACKEND.glob('*.py')}});start=time.time()
                try:
                    with(folder/'generation.log').open('wb')as log:
                        for command in [argv]+([auditargv]if cfg['quality']=='mobile'else[]):
                            p=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=max(1,req.get('timeoutSeconds',1200)-(time.time()-start)))
                            if p.returncode:raise RequestError('generation_failed','Blender exited '+str(p.returncode))
                    res=inspect(folder)if cfg['quality']=='mobile'else{'draft':True,'outputFolder':str(folder)}
                    write(folder/'status.json',{'status':'complete','requestId':r['requestId'],'elapsedSeconds':round(time.time()-start,2),'configSha256':sha(folder/'config.json')})
                except(RequestError,subprocess.TimeoutExpired)as ex:
                    write(folder/'status.json',{'status':'failed','error':str(ex)})
                    if isinstance(ex,subprocess.TimeoutExpired):raise RequestError('timeout','Town generation timed out')
                    raise
        else:raise RequestError('unknown_action',str(action))
        r.update(ok=True,result=res)
    except RequestError as ex:r['error']={'code':ex.code,'message':str(ex)}
    except(KeyError,TypeError,ValueError,OSError)as ex:r['error']={'code':'invalid_request','message':str(ex)}
    return r
def main():
    p=argparse.ArgumentParser();p.add_argument('--request',required=True);p.add_argument('--response');args=p.parse_args();src=pathlib.Path(args.request).resolve();result=respond(read(src),src.parent)
    if args.response:write(args.response,result)
    print(json.dumps(result,ensure_ascii=True));return 0 if result['ok']else 2
if __name__=='__main__':sys.exit(main())
