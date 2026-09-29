import bpy
import json
from pathlib import Path

P=Path(r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions')
OUT=P/'crawl-cylinder'
bpy.ops.wm.open_mainfile(filepath=str(P/'BasicPlayerCapsule_Bear_CrawlCylinder.blend'), use_scripts=False)
s=bpy.context.scene
r=bpy.data.objects['DGN_Armature']
b=bpy.data.objects['Body']
meshes=[o for o in s.objects if o.type=='MESH']
entries=json.loads((P/'manifest.json').read_text())
names=json.loads((OUT/'corrective-report.json').read_text())['clips']
entries=[a for a in entries if a['clip'] in names]
r.data.pose_position='REST'
for o in meshes:
 sk=o.data.shape_keys
 if sk:
  sk.animation_data_clear()
  for k in sk.key_blocks:k.value=0
 for m in o.modifiers:
  if m.type=='ARMATURE':m.show_viewport=False
 bpy.context.view_layer.update()
 dg=bpy.context.evaluated_depsgraph_get()
 result=bpy.data.meshes.new_from_object(o.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)
 shapes={}
 if sk:
  for k in list(sk.key_blocks)[1:]:
   k.value=1;bpy.context.view_layer.update();ev=o.evaluated_get(dg);me=ev.to_mesh();shapes[k.name]=[v.co.copy() for v in me.vertices];ev.to_mesh_clear();k.value=0
 o.data=result
 for m in list(o.modifiers):
  if m.type=='ARMATURE':m.show_viewport=True
  else:o.modifiers.remove(m)
 if shapes:
  o.shape_key_add(name='Basis')
  for n,coords in shapes.items():
   k=o.shape_key_add(name=n)
   for p,c in zip(k.data,coords):p.co=c
r.data.pose_position='POSE'
for i,a in enumerate(entries):
 r.animation_data.action=bpy.data.actions[a['clip']+'_CurrentBear']
 b.data.shape_keys.animation_data_create().action=bpy.data.actions[a['clip']+'_CurrentBear_Shapes']
 b.data.shape_keys.animation_data.action_slot=b.data.shape_keys.animation_data.action.slots[0]
 s.frame_start=0;s.frame_end=a['end'];s.frame_set(0)
 bpy.ops.object.select_all(action='DESELECT')
 for o in [r,*meshes]:o.hide_set(False);o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/a['file']),use_selection=True,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',armature_nodetype='NULL',primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)
 print('EXPORTED',i+1,'/',len(entries),a['clip'],flush=True)
print('COMPLETE',len(entries),flush=True)
