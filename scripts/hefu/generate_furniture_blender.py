"""Hefu mobile P1 preview sculpt. 1 unit = 1m; origin at old furniture
visual's renderer bounds center, not the world origin. Each FBX is visual-only.
Blender coords B(x,-UnityZ,UnityY); exporter sets Unity Y-up. The Unity Editor
checks shape bounds before switching renderers, so safe fallback remains.
"""
import bpy,math,random
from pathlib import Path
P=Path('/Volumes/ssd/HefuVR-Android/Assets/Hefu/Models/InteriorRefinement');P.mkdir(parents=True,exist_ok=True)
def mat(name,c,rough=.5,metal=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=metal
 return m
matOlive=mat('Hefu_LeatherOlive',(.29,.36,.29),.48)
matShadow=mat('Hefu_LeatherDark',(.22,.29,.24),.53)
matCream=mat('Hefu_FabricCream',(.78,.75,.67),.89)
matGraphite=mat('Hefu_FabricGraphite',(.10,.115,.12),.58)
matWood=mat('Hefu_Walnut',(.37,.215,.15),.55)
matGold=mat('Hefu_BrushedBrass',(.60,.45,.21),.36,.60)

def uv(u):return (u[0],-u[2],u[1])
def clear():bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def box(name,center,dims,material,bevel=.012,segs=3):
 bpy.ops.mesh.primitive_cube_add(size=1,location=uv(center));o=bpy.context.object;o.name=name
 o.dimensions=uv(dims);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel>0:
  modifier=o.modifiers.new('WearSoftenedEdges','BEVEL');modifier.width=bevel;modifier.segments=segs
  modifier.affect='EDGES'
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=modifier.name)
 o.data.materials.append(material)
 for poly in o.data.polygons:poly.use_smooth=(bevel>=.022)
 return o

def piping(name,pts,r,material):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=8;c.bevel_depth=r;c.bevel_resolution=2
 spline=c.splines.new('POLY');spline.points.add(len(pts)-1)
 for i,p in enumerate(pts):spline.points[i].co=(*uv(p),1)
 o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');return bpy.context.object

def join_and_export(name):
 bpy.ops.object.select_all(action='SELECT');pieces=[x for x in bpy.context.scene.objects if x.type=='MESH']
 bpy.context.view_layer.objects.active=pieces[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
 # Mesh vertices already in blender global coords; base origin zero. Keep UVs.
 bpy.context.scene.cursor.location=(0,0,0)
 bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.export_scene.fbx(filepath=str(P/(name+'.fbx')),use_selection=True,
  object_types={'MESH'},bake_anim=False,apply_unit_scale=True,axis_forward='-Z',axis_up='Y')
 print('HEFU_BLENDER_P1',name,'verts',len(o.data.vertices),'polys',len(o.data.polygons),'size',tuple(round(q,4) for q in o.dimensions))

clear()
# Three upholstered cushions with a gentle crest, differentiated base/frame.
box('Sofa_DeepShadowBase',(0,-.185,0),(.80,.17,2.23),matShadow,.05,4)
box('Sofa_BackShell',(.312,.205,0),(.22,.60,2.25),matOlive,.085,5)
for i in range(3):
 z=(i-1)*.742
 box(f'Sofa_SeatPillow_{i}',(-.085,.09,z),(.70,.205,.71),matOlive,.077,5)
 box(f'Sofa_BackPillow_{i}',(.172,.328,z),(.238,.466,.705),matOlive,.086,5)
 # Piping follows the visible upper edge without adding distinct colliders.
 piping(f'Sofa_SeatStitch_{i}',[(-.405,.175,z-.29),(-.35,.190,z-.32),(.12,.183,z-.32),(.19,.17,z-.27)],.0035,matShadow)
for i,z in enumerate([-1.00,1.00]):
 box('Sofa_ArmCore_'+str(i),(-.020,.155,z),(.81,.355,.235),matOlive,.090,5)
 box('Sofa_ArmCap_'+str(i),(-.095,.315,z),(.58,.117,.225),matOlive,.048,4)
for i,x in enumerate([-.265,.27]):
 for j,z in enumerate([-.99,.99]):box(f'Sofa_BlackLeg_{i}_{j}',(x,-.295,z),(.052,.116,.056),matGraphite,.013,2)
box('Sofa_CreamThrowPillow',(-.08,.361,-.38),(.19,.286,.27),matCream,.068,5)
box('Sofa_DarkThrowPillow',(-.08,.366,.36),(.19,.278,.258),matGraphite,.071,5)
join_and_export('HefuP1_SoftSofa')

clear()
# Walnut dual-drawer coffee table: .70w x .96l x .51h centered about y=0.
box('Coffee_WalnutTop',(0,.220,0),(.70,.054,1.01),matWood,.027,4)
box('Coffee_WalnutCase',(0,.010,0),(.665,.360,.958),matWood,.015,4)
for z in [-.244,.244]:
 box('Coffee_DrawerFace',(-.342,.019,z),(.021,.316,.464),matWood,.005,2)
 box('Coffee_BrassPull',(-.358,.060,z),(.024,.020,.122),matGold,.009,4)
 box('Coffee_PullFootA',(-.349,.052,z-.055),(.025,.030,.012),matGold,.004,2)
 box('Coffee_PullFootB',(-.349,.052,z+.055),(.025,.030,.012),matGold,.004,2)
for x in [-.27,.27]:
 for z in [-.39,.39]:
  box('Coffee_WoodLeg',(x,-.201,z),(.050,.160,.050),matWood,.009,3)
  box('Coffee_BrassFoot',(x,-.26,z),(.052,.038,.052),matGold,.007,3)
# Wood finish not geometric clutter; existing decorations remain in Unity scene.
join_and_export('HefuP1_WalnutCoffee')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'HefuP1_Furniture_Source.blend'))
