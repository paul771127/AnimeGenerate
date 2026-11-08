"""Two shared 2048 RGB albedo atlases with offline cel light/shadow bands."""
import pathlib,bpy,numpy as np
from texture_io import write_png,byte_rgb
def image(path):
    im=bpy.data.images.load(str(path));im.colorspace_settings.name='Non-Color';a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);a=a.reshape(im.size[1],im.size[0],4)[...,:3]
    return np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4)
def resize(a,w,h):
    im=bpy.data.images.new('tile_resample',a.shape[1],a.shape[0],alpha=False,float_buffer=True);q=np.concatenate((a,np.ones(a.shape[:2]+(1,),np.float32)),axis=2);im.pixels.foreach_set(q.ravel());im.scale(w,h);v=np.empty(w*h*4,np.float32);im.pixels.foreach_get(v);bpy.data.images.remove(im);return v.reshape(h,w,4)[...,:3]
def lin(hex):
    a=np.array([int(hex[i:i+2],16)/255 for i in [1,3,5]],np.float32);return np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4)
def bake(out,reference,stone_source,ground_source):
    source=image(reference);h,w=source.shape[:2];wood=resize(source[h//2:,:w//2],480,480);roof=resize(source[h//2:,w//2:],480,480);plaster=resize(source[:h//2,:w//2],480,480);linen=resize(source[:h//2,w//2:],480,480)
    rock=image(stone_source);rock=resize(rock[rock.shape[0]//2:,:rock.shape[1]//2],480,480);ash=resize(image(ground_source),480,480)
    luminance=rock@np.array([.2126,.7152,.0722]);low,high=np.percentile(luminance,[3,97]);t=np.clip((luminance-low)/max(high-low,.01),0,1)[...,None];rock=lin('#2c3842')*(1-t)+lin('#6f6f6b')*t
    grey=np.mean(linen,axis=2,keepdims=True);grey=grey/np.maximum(grey.mean(),.01);stonegrey=np.mean(rock,axis=2,keepdims=True);stonegrey=stonegrey/np.maximum(stonegrey.mean(),.01)
    yy,xx=np.mgrid[:480,:480];texture=1+.018*np.sin(xx*.13)+.012*np.sin(yy*.08)
    pave=rock*.68+ash*.32;pave*=np.array([1.08,1.03,.96]);iron=stonegrey*lin('#404853');brass=stonegrey*lin('#b18948')
    fire=np.empty((480,480,3),np.float32);flame=.45+.2*np.sin(xx*.024)+.13*np.sin(xx*.06+1.7)
    fire[:]=lin('#d8452a');fire[yy/480<flame+.19]=lin('#ff8a2a');fire[yy/480<flame]=lin('#ffd36a')
    tiles=[[rock,wood,roof,plaster,pave,ash,iron,brass],[linen,grey*lin('#3e7772'),grey*lin('#a3513e'),plaster*1.05,stonegrey*lin('#69afbc'),grey*lin('#6d8454'),fire,stonegrey*lin('#232c34')]]
    paths=[]
    for atlas,name in zip(tiles,['town0_arch_albedo.png','town0_detail_albedo.png']):
        canvas=np.zeros((2048,2048,3),np.float32)
        for index,tile in enumerate(atlas):
            for band,strength in enumerate([.58,1.06]):
                value=np.clip(tile*strength*texture[...,None],0,1);slot=index*2+band;c=slot%4;r=slot//4
                padded=np.pad(value,((16,16),(16,16),(0,0)),mode='edge');canvas[r*512:(r+1)*512,c*512:(c+1)*512]=padded
        write_png(out/name,byte_rgb(canvas),True);paths.append(name)
    return paths
def material(name,path):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;n.clear();tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(path));e=n.new('ShaderNodeEmission');l.new(tex.outputs['Color'],e.inputs['Color']);o=n.new('ShaderNodeOutputMaterial');l.new(e.outputs[0],o.inputs['Surface']);return m
