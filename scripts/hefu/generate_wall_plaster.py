"""Repeatable low-contrast tileable plaster colour and tangent-space normal.
Unit texture dimensions: 512 per metre (in game UVs), no actual home images."""
import math,random,struct,zlib
from pathlib import Path
P=Path('/Volumes/ssd/HefuVR-Android/Assets/Hefu/Textures/WallPBR')
W=H=512
seed=29301
def hashxy(x,y):
 n=(x*374761393+y*668265263+seed*911)&0xffffffff
 n^=n>>13;n=n*1274126177&0xffffffff;n^=n>>16
 return (n&65535)/65535.
def smooth(t):return t*t*(3-2*t)
def noise(x,y,k):
 fx=x/k;fy=y/k;u=int(math.floor(fx));v=int(math.floor(fy));a=smooth(fx-u);b=smooth(fy-v)
 q00=hashxy(u,v);q10=hashxy(u+1,v);q01=hashxy(u,v+1);q11=hashxy(u+1,v+1)
 return (q00*(1-a)+q10*a)*(1-b)+(q01*(1-a)+q11*a)*b
# periodic wrap for tile continuity using four toroidal sample offsets
def tile(x,y,k):
 a=x/W;b=y/H
 return (noise(x,y,k)*(1-a)*(1-b)+noise(x-W,y,k)*a*(1-b)+
         noise(x,y-H,k)*(1-a)*b+noise(x-W,y-H,k)*a*b)
# map height
height=[[0.0]*W for _ in range(H)]
for y in range(H):
 for x in range(W):
  grain=tile(x,y,34)*.42+tile(x,y,10)*.35+tile(x,y,3)*.23
  height[y][x]=grain

def png_rgb(path,rows):
 body=bytearray()
 for row in rows:
  body.append(0)
  for pixel in row:body.extend(max(0,min(255,int(ch))) for ch in pixel)
 def chunk(k,v):return struct.pack('>I',len(v))+k+v+struct.pack('>I',zlib.crc32(k+v)&0xffffffff)
 out=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',W,H,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(body,5))+chunk(b'IEND',b'')
 path.write_bytes(out);print('HEFU_PLASTER_PNG',path,len(out))
rows=[];normals=[]
for y in range(H):
 row=[];normal=[]
 for x in range(W):
  v=height[y][x]-.5
  # base white so wall's Neutral tint remains the sole palette controller
  row.append((251+v*14,249+v*13,244+v*13))
  dx=height[y][(x+1)%W]-height[y][(x-1)%W]
  dy=height[(y+1)%H][x]-height[(y-1)%H][x]
  n=(-dx*.62,-dy*.62,1.0);length=math.sqrt(sum(a*a for a in n))
  normal.append(((n[0]/length*.5+.5)*255,(n[1]/length*.5+.5)*255,(n[2]/length*.5+.5)*255))
 rows.append(row);normals.append(normal)
png_rgb(P/'IvoryPlaster_BaseColor.png',rows)
png_rgb(P/'IvoryPlaster_Normal.png',normals)
