"""Generate illustrative equirectangular sky; no external geolocation implied."""
import math,random,struct,zlib
from pathlib import Path
out=Path('/Volumes/ssd/HefuVR-Android/Assets/Hefu/Textures/OutdoorPreview/Illustrative_DaylightSky.png')
W,H=1280,640
seed=1047
rng=random.Random(seed)
def hashnoise(x,y):
 x=int(x);y=int(y);z=(x*73428767+y*9123677+seed*91337)&0xffffffff
 z^=z>>13;z=z*1274126177&0xffffffff;z^=z>>16
 return (z&65535)/65535.
def smooth(a,b,t):
 v=max(0.,min(1.,(t-a)/(b-a)));return v*v*(3-2*v)
def value_noise(x,y,s):
 x/=s;y/=s
 ix=int(math.floor(x));iy=int(math.floor(y));fx=x-ix;fy=y-iy
 fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
 a=hashnoise(ix,iy)*(1-fx)+hashnoise(ix+1,iy)*fx
 b=hashnoise(ix,iy+1)*(1-fx)+hashnoise(ix+1,iy+1)*fx
 return a*(1-fy)+b*fy
p=bytearray()
for y in range(H):
 t=y/(H-1);line=bytearray()
 for x in range(W):
  u=x/W
  # 2:1 equirect horizon t=0.5. Warm early-afternoon sky gradient.
  above=max(0,1-t*2)
  h=smooth(.16,.58,abs(t-.50))
  if t<.50: base=(int(205-109*above),int(223-82*above),int(237-56*above))
  else: base=(195,210,213)
  if t<.54:
   f=.55*value_noise(x,y,120)+.3*value_noise(x,y,50)+.15*value_noise(x,y,18)
   cloud=smooth(.58,.78,f)*(1-smooth(.31,.56,abs(t-.39))) * .78
   base=tuple(int(base[c]*(1-cloud)+(244,240,229)[c]*cloud) for c in range(3))
  # Horizon warm air scattering, not city-specific panorama.
  haze=(1-smooth(.0,.18,abs(t-.49)))*.38
  base=tuple(int(base[c]*(1-haze)+(222,214,195)[c]*haze) for c in range(3))
  line.extend(max(0,min(255,v)) for v in base)
 p.append(0);p.extend(line)
def chunk(k,d):return struct.pack('>I',len(d))+k+d+struct.pack('>I',zlib.crc32(k+d)&0xffffffff)
data=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',W,H,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(p,6))+chunk(b'IEND',b'')
out.write_bytes(data)
print('HEFU_SKY_TEXTURE_OK',out,out.stat().st_size)
