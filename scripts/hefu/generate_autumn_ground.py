"""Illustrative Luoyang late-autumn grass/soil ground. Synthetic, no aerial data."""
import random,math,struct,zlib
from pathlib import Path
out=Path('/Volumes/ssd/HefuVR-Android/Assets/Hefu/Textures/OutdoorPreview/Illustrative_AutumnGround.png')
W=H=512; rng=random.Random(1124536)
def hashxy(x,y):
 n=(x*73856093^y*19349663^1124536)&0xffffffff
 n^=n>>13;n=n*1274126177&0xffffffff;n^=n>>16
 return (n&65535)/65535.
def smoother(t):return t*t*(3-2*t)
def noise(x,y,f):
 xx=x/f; yy=y/f; ix=int(xx);iy=int(yy);u=smoother(xx-ix);v=smoother(yy-iy)
 a=hashxy(ix,iy)*(1-u)+hashxy(ix+1,iy)*u
 b=hashxy(ix,iy+1)*(1-u)+hashxy(ix+1,iy+1)*u
 return a*(1-v)+b*v
buf=bytearray()
for y in range(H):
 buf.append(0)
 for x in range(W):
  f=noise(x,y,70)*.48+noise(x,y,17)*.30+noise(x,y,5)*.22
  dry= (f-.5)*60
  dust=rng.random()
  stain= -22 if dust <.016 else (23 if dust>.992 else (rng.random()-.5)*10)
  base=(125+dry+stain, 119+dry*.65+stain*.76, 84+dry*.48+stain*.43)
  buf.extend(max(0,min(255,int(v))) for v in base)
def chunk(k,d):return struct.pack('>I',len(d))+k+d+struct.pack('>I',zlib.crc32(k+d)&0xffffffff)
png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',W,H,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(buf,5))+chunk(b'IEND',b'')
out.write_bytes(png)
print('HEFU_AUTUMN_GROUND_TEXTURE_PASS',out,'bytes',len(png))
