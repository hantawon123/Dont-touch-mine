"""Export approved accessories in rest pose without changing their Blender sources."""
import bpy, bmesh, json, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from hold_crawl_torso import FBX_KW
SRC=ROOT/'source/blender/characters/SmoothBear'
OUT=ROOT/'Assets/_Game/Content/Characters/SmoothBear/Wearables'
OUT.mkdir(parents=True,exist_ok=True)
sources={
 'CompactShoes': SRC/'Shoes/BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_Compact.blend',
 'Sparkle': SRC/'Expressions/BasicPlayerCapsule_Bear_SparkleEyes_Printed.blend',
 'Sleepy': SRC/'Expressions/BasicPlayerCapsule_Bear_SleepyEyes_Volume.blend',
 'Fierce': SRC/'Expressions/BasicPlayerCapsule_Bear_FierceEyes_Bigger.blend',
 'Brown': SRC/'Expressions/BasicPlayerCapsule_Bear_BrownGlossyEyes.blend'}
if '--only' in sys.argv:
 selected=sys.argv[sys.argv.index('--only')+1]
 sources={selected:sources[selected]}
for key,path in sources.items():
 bpy.ops.wm.open_mainfile(filepath=str(path),use_scripts=False)
 rig=bpy.data.objects['DGN_Armature'];rig.animation_data_clear();rig.data.pose_position='REST'
 for bone in rig.pose.bones: bone.matrix_basis.identity()
 bpy.context.view_layer.update()
 chosen=[o for o in bpy.data.objects if o.type=='MESH' and (o.name.startswith('Shoes.') if key=='CompactShoes' else (o.name.startswith('Eye_White_') or o.name.startswith('Eye_'+key+'_')))]
 parts=[];export=[]
 for o in chosen:
  if 'SparklePrintUV' in o.data.uv_layers:
   # FBX exports active-render UV first only when it is first in the collection.
   uv=o.data.uv_layers['SparklePrintUV'];coords=[tuple(x.uv) for x in uv.data]
   while len(o.data.uv_layers):o.data.uv_layers.remove(o.data.uv_layers[0])
   uv=o.data.uv_layers.new(name='PrintUV')
   for dst,co in zip(uv.data,coords):dst.uv=co
  graph=bpy.context.evaluated_depsgraph_get()
  mesh=bpy.data.meshes.new_from_object(o.evaluated_get(graph),preserve_all_data_layers=True,depsgraph=graph)
  bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
  used=sorted({p.material_index for p in mesh.polygons});materials=[mesh.materials[i] for i in used]
  indices=[used.index(p.material_index) for p in mesh.polygons];mesh.materials.clear()
  for mat in materials:mesh.materials.append(mat)
  for poly,index in zip(mesh.polygons,indices):poly.material_index=index
  obj=bpy.data.objects.new('Part_'+o.name,mesh);bpy.context.scene.collection.objects.link(obj);obj.matrix_world=o.matrix_world.copy()
  mats=[]
  for index,mat in enumerate(mesh.materials):
   texture='';color=list(mat.diffuse_color);roughness=.5
   if mat.use_nodes:
    bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if bs:color=list(bs.inputs['Base Color'].default_value);roughness=bs.inputs['Roughness'].default_value
    images=[n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
    if images:
     im=images[0];texture=key+'_'+Path(im.name).stem+'.png';im.filepath_raw=str(OUT/texture);im.file_format='PNG';im.save()
   mode=(2 if index==0 else 3) if key=='CompactShoes' else (1 if 'Lid' in o.name and key!='Sparkle' else 0)
   mats.append(dict(name=mat.name,texture=texture,color=color,roughness=roughness,tint=mode))
  parts.append(dict(name=obj.name,bone=('Foot.L' if o.name.endswith('.L') else 'Foot.R') if key=='CompactShoes' else 'Head',materials=mats))
  export.append(obj)
 bpy.ops.object.select_all(action='DESELECT')
 for o in export+[rig]:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 kw=FBX_KW.copy();kw.update(bake_anim=False,embed_textures=False,path_mode='AUTO')
 bpy.ops.export_scene.fbx(filepath=str(OUT/(key+'.fbx')),**kw)
 (OUT/(key+'.json')).write_text(json.dumps(dict(parts=parts),indent=2))
 print('APPROVED_EXPORT',key,len(parts),flush=True)
