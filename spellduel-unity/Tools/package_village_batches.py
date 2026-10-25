"""Package imagegen source manifests kept outside the repository; no Runtime edits."""
import argparse,json,subprocess,sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter,ImageOps
import prepare_village2d as p
ROOT=p.ROOT; SOURCE=ROOT.parents[1]; CHARS=ROOT/'Assets/SpellDuel/Resources/Story2D/chars'
def merged(pattern):
    out={}
    for f in SOURCE.glob(pattern):out.update(p.read(f))
    return out
def package(data,kind,size=None):
    f=SOURCE/'village2d_package_current.json';p.write(f,data)
    args=[sys.executable,str(Path(p.__file__)),'--sources',str(f),'--kind',kind]
    if size:args+=['--size',*map(str,size)]
    subprocess.run(args,check=True)
def main():
    a=argparse.ArgumentParser();a.add_argument('batch',type=int);n=a.parse_args().batch
    if n==2:
        package({'in_smith_flame':p.read(SOURCE/'village2d_bg_sources.json')['in_smith_flame']},'bg')
        package(merged('village2d_pose_*.json'),'pose')
        staff=p.read(SOURCE/'village2d_staff_sources.json');package({'smith_normal':staff['smith_normal']},'pose')
        anvils=merged('village2d_anvil_?.json');package(anvils,'anvil')
        # Lock stationary lower anvil/block pixels to master across all frames.
        base=Image.open(p.ART/'anvil_0.png').convert('RGBA')
        for i in range(1,6):
            path=p.ART/f'anvil_{i}.png';im=Image.open(path).convert('RGBA');im.paste(base.crop((0,192,512,384)),(0,192));im.save(path,optimize=True)
        p.DOC.joinpath('batch2').mkdir(parents=True,exist_ok=True)
        frames=[Image.open(p.ART/f'anvil_{i}.png').convert('RGBA') for i in range(6)]
        frames[0].save(p.DOC/'batch2/anvil.gif',save_all=True,append_images=frames[1:]*1,duration=[83,83,83,84,83,84],loop=0,disposal=2)
    if n in [3,4]:
        keys=['shop','guild'] if n==3 else ['dungeon','training','bounty','watch','warp','gate']
        bg=p.read(SOURCE/'village2d_bg_sources.json');package({f'in_{k}_flame':bg[f'in_{k}_flame'] for k in keys},'bg')
        staff=p.read(SOURCE/'village2d_staff_sources.json');edits=merged('village2d_staff_*_*.json')
        ids=['clerk'] if n==3 else ['guide','instructor','bountyofficer','announcer']
        boxes={'clerk':(490,255,674,449),'guide':(470,210,672,387),'instructor':(480,195,676,405),'bountyofficer':(508,214,702,416),'announcer':(472,268,666,451)}
        for id in ids:
            base=Image.open(staff[id+'_normal']['path']).convert('RGBA');mask=Image.new('L',base.size);ImageDraw.Draw(mask).rounded_rectangle(boxes[id],radius=30,fill=255);mask=mask.filter(ImageFilter.GaussianBlur(1.4));out={id+'_normal':staff[id+'_normal']}
            for exp in ['smile','surprised']:
                key=id+'_'+exp;s=edits[key];face=Image.open(s['path']).convert('RGBA').resize(base.size);im=Image.composite(face,base,mask);im.putalpha(base.getchannel('A'));dest=SOURCE/(key+'_locked.png');im.save(dest);out[key]={'path':str(dest),'prompt':s['prompt']}
            package(out,'pose')
            md=p.DOC/'staff_masks';md.mkdir(exist_ok=True);mask.resize((768,1152),Image.Resampling.LANCZOS).save(md/(id+'.png'))
        if n==4:
            data=merged('village2d_ui_*.json')
            for k,v in data.items():package({k:v},'ui',{'name_plate':(384,96),'list_frame':(512,768),'list_row':(640,160),'list_row_sel':(640,160),'speech_bubble':(512,320),'btn_big':(640,200),'btn_big_press':(640,200)}[k])
    if n==5:
        maps=merged('village2d_map_*.json');maps={k:v for k,v in maps.items() if k.startswith('map_') and k!='map_flame'};package(maps,'map')
        npcs=merged('village2d_npc_*.json');package({k:v for k,v in npcs.items() if k not in ['npc_laoyan','npc_xiaotie','npc_mila']},'npc')
if __name__=='__main__':main()
