"""Create reusable low-poly leafy trees for mobile/Quest exterior placeholders.
Not a LiDAR scan, no location-specific real-world buildings or tree species.
"""
import bpy, math, random
from mathutils import Vector
from pathlib import Path
D=Path('/Volumes/ssd/HefuVR-Android/Assets/Hefu/Models/OutdoorPreview')
D.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
r=random.Random(20261025)
def material(name,rgb,rough=0.8):
 mat=bpy.data.materials.new(name);mat.diffuse_color=(*rgb,1);mat.use_nodes=True
 n=mat.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*rgb,1);n.inputs['Roughness'].default_value=rough
 return mat
bark=material('Bark_Oak',(0.25,0.16,0.105)); twig=material('Bark_Secondary',(0.28,0.19,0.13))
leaves=[material('Foliage_DarkSage',(0.20,0.30,0.21)),material('Foliage_Olive',(0.32,0.41,0.27)),material('Foliage_Sunlit',(0.39,0.48,0.30)),material('Foliage_Deep',(0.16,0.27,0.20))]
def tree(name,seed,flatten):
 rr=random.Random(seed);created=[]
 # Ground z=0 Blender. Unity imports FBX Y-up via exporter axis.
 bpy.ops.mesh.primitive_cone_add(vertices=9,radius1=.22,radius2=.13,depth=2.7,location=(0,0,1.35));t=bpy.context.object;t.name=name+'_Trunk';t.data.materials.append(bark);created.append(t)
 for i in range(4):
  a=2*math.pi*i/4+rr.uniform(-.25,.25);h=1.45+i*.22
  bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=.1,radius2=.047,depth=1.3,location=(.48*math.cos(a),.48*math.sin(a),h+.25));c=bpy.context.object;c.rotation_euler[1]=.64;c.rotation_euler[2]=a;c.name=name+'_Branch';c.data.materials.append(twig);created.append(c)
 for j in range(18):
  theta=(j*2.39996)%(2*math.pi);z=2.25+rr.uniform(-.1,1.85)
  radius=rr.uniform(.25,1.05)*(.95 if z<3.7 else .60)
  x=math.cos(theta)*radius;y=math.sin(theta)*radius
  bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=(x,y,z))
  o=bpy.context.object;o.name=name+'_LeafClump';o.scale=(rr.uniform(.40,.69),rr.uniform(.35,.65),rr.uniform(.28,.50));o.data.materials.append(leaves[(j+seed)%len(leaves)]);created.append(o)
 for o in bpy.context.selected_objects:o.select_set(False)
 for o in created:o.select_set(True)
 bpy.context.view_layer.objects.active=created[0]
 bpy.ops.object.join()
 joined=bpy.context.object;joined.name=name;joined.location=(0,0,0)
 return joined
for n,seed in [('OutdoorOliveTree_A',112),('OutdoorOliveTree_B',219),('OutdoorOliveTree_C',347)]:
 o=tree(n,seed,1)
 for p in bpy.context.selected_objects:p.select_set(False)
 o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(D/(n+'.fbx')),use_selection=True,apply_unit_scale=True,
  axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,
  use_mesh_modifiers=True,mesh_smooth_type='FACE')
 o.hide_set(True)
# Save original procedural Blender scene for refinements.
for o in bpy.data.objects:o.hide_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(D/'OutdoorTrees_Source.blend'))
print('HEFU_BLENDER_OUTDOOR_TREES_PASS', [(p.name,p.stat().st_size) for p in D.glob('*.fbx')])
