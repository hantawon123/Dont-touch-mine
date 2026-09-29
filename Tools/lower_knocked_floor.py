import bpy,sys
src,dst=sys.argv[sys.argv.index('--')+1:];bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=src);r=bpy.data.objects['DGN_Armature'];a=r.animation_data.action;origin=a.frame_range[0];k=bpy.data.objects['Body'].data.shape_keys
for action in (a,k.animation_data.action):
 for layer in action.layers:
  for strip in layer.strips:
   for slot in action.slots:
    bag=strip.channelbag(slot)
    if not bag:continue
    for c in bag.fcurves:
     for p in c.keyframe_points:
      p.co.x-=origin;p.handle_left.x-=origin;p.handle_right.x-=origin
      if c.data_path=='pose.bones["Hips"].location' and c.array_index==1:
       t=max(0,min(1,(p.co.x-30)/14));d=.068*t*t*(3-2*t);p.co.y-=d;p.handle_left.y-=d;p.handle_right.y-=d
s=bpy.context.scene;s.frame_start=0;s.frame_end=66;s.frame_set(0)
bpy.ops.export_scene.fbx(filepath=dst,use_selection=False,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',armature_nodetype='NULL',primary_bone_axis='Y',secondary_bone_axis='X',mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)
