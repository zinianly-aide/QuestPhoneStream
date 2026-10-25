"""A tileable illustrated ivory linen weave; synthetic pixels from style palette.
No user photography embedded; 512 squared 8-bit RGB PNG, deterministic.
"""
import math, random,struct,zlib
from pathlib import Path
W=H=512;rng=random.Random(20261025);buf=bytearray()
for y in range(H):
 buf.append(0)
 for x in range(W):
  warp=math.sin((x+.28*math.sin(y*.35))*math.pi/3.3)*2.1
  woof=math.sin((y+.13*math.sin(x*.52))*math.pi/4.2)*1.9
  grit=(rng.random()-.5)*4.0
  ripple=math.sin(x*.045+math.sin(y*.022))*1.3
  t=warp+woof+grit+ripple
  buf.extend(max(0,min(255,int(c+t))) for c in (228,219,202))
def chk(k,v):return struct.pack('>I',len(v))+k+v+struct.pack('>I',zlib.crc32(k+v)&0xffffffff)
img=b'\x89PNG\r\n\x1a\n'+chk(b'IHDR',struct.pack('>IIBBBBB',W,H,8,2,0,0,0))+chk(b'IDAT',zlib.compress(buf,5))+chk(b'IEND',b'')
p=Path('/Volumes/ssd/HefuVR-Android/Assets/Hefu/Textures/WarmInterior/Linen_IvoryWeave_512.png');p.write_bytes(img);print('HEFU_LINEN_READY',p,len(img))
