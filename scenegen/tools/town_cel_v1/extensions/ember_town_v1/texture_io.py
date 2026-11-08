"""Byte-exact RGB PNG output for preserved flow strips and unchanged texture regions."""
import numpy as np,zlib,struct,binascii
def write_png(path,pixels,srgb=True):
    a=np.ascontiguousarray(pixels,dtype=np.uint8);h,w,c=a.shape;assert c==3
    def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',binascii.crc32(kind+data)&0xffffffff)
    compressor=zlib.compressobj(8);pieces=[]
    # Blender pixel buffers start at the bottom; PNG scanlines start at the top.
    for row in a[::-1]:
        row=row.ravel();sub=row.copy();sub[3:]=row[3:]-row[:-3];pieces.append(compressor.compress(b'\x01'+sub.tobytes()))
    pieces.append(compressor.flush());data=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,2,0,0,0))
    if srgb:data+=chunk(b'sRGB',b'\x00')
    data+=chunk(b'IDAT',b''.join(pieces))+chunk(b'IEND',b'');path.write_bytes(data)

def linear_to_srgb(x):
    x=np.clip(x,0,1);return np.where(x<=.0031308,x*12.92,1.055*x**(1/2.4)-.055)

def byte_rgb(linear):return np.clip(np.rint(linear_to_srgb(linear)*255),0,255).astype(np.uint8)

def remove_old_hotspots(rgb,mask):
    result=rgb.copy();unknown=mask.copy();ys,xs=np.nonzero(unknown)
    for _ in range(48):
        if not len(xs):break
        sums=np.zeros((len(xs),3),np.float32);n=np.zeros(len(xs),np.float32)
        for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
            yy=np.clip(ys+dy,0,rgb.shape[0]-1);xx=np.clip(xs+dx,0,rgb.shape[1]-1);valid=~unknown[yy,xx]
            sums+=result[yy,xx]*valid[:,None];n+=valid
        valid=n>0;result[ys[valid],xs[valid]]=sums[valid]/n[valid,None];unknown[ys[valid],xs[valid]]=False
        ys=ys[~valid];xs=xs[~valid]
    return result
