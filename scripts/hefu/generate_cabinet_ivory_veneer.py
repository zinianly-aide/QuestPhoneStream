"""Deterministic 512px physically subtle lacquered pale oak veneer texture.
Synthetic reusable 0.48m tile; avoid screenshot/photo projection."""
import math,random,struct,zlib
from pathlib import Path
rng=random.Random(33501);N=512;p=Path('/Volumes/ssd/HefuVR-Android/Assets/Hefu/Textures/CabinetPBR/IvoryVeneer_48cm.png')
buf=bytearray()
seams=[.55*math.sin(x*.040)+.31*math.sin(x*.012) for x in range(N)]
for y in range(N):
 buf.append(0)
 for x in range(N):
  g=.74*math.sin(2*math.pi*(x+seams[y])/38)+.38*math.sin(2*math.pi*(x+3*math.sin(y*.023))/10.6)
  w=.65*math.sin(x*.022+y*.005)+g+ (rng.random()-.5)*1.65
  buf.extend(max(0,min(255,round(c+w))) for c in (235,229,214))
def chunk(t,x):return struct.pack('>I',len(x))+t+x+struct.pack('>I',zlib.crc32(t+x)&0xffffffff)
p.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',N,N,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(buf,5))+chunk(b'IEND',b''))
print('HEFU_CABINET_IVORY_TEXTURE_READY',p,p.stat().st_size)
