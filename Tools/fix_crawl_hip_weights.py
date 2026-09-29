"""Keep torso vertices attached to the pelvis across thigh external rotation."""
import bpy, sys
from pathlib import Path
source, destination=sys.argv[sys.argv.index('--')+1:]
if source.endswith('.blend'):
    bpy.ops.wm.open_mainfile(filepath=source, use_scripts=False)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source, use_anim=True)
body=bpy.data.objects['Body']
hips=body.vertex_groups['Hips']
thighs={body.vertex_groups[n].index for n in ('UpperLeg.L','UpperLeg.R')}
count=0
for v in body.data.vertices:
    t=max(0.0,min(1.0,(v.co.y-0.46)/0.22))
    t=t*t*(3-2*t)
    if t==0: continue
    transfer=0.0
    hip_weight=next((g.weight for g in v.groups if g.group==hips.index),0.0)
    for g in list(v.groups):
        if g.group in thighs:
            transfer+=g.weight*t
            body.vertex_groups[g.group].add([v.index],g.weight*(1-t),'REPLACE')
    if transfer>0:
        hips.add([v.index],hip_weight+transfer,'REPLACE')
        count+=1
print('REWEIGHTED',count)
if destination.endswith('.blend'):
    bpy.ops.wm.save_as_mainfile(filepath=destination)
else:
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.data.objects:
        if o.type in {'ARMATURE','MESH'}: o.select_set(True)
    bpy.context.scene.frame_start=0
    bpy.context.scene.frame_end=60
    bpy.ops.export_scene.fbx(filepath=destination,use_selection=True,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',armature_nodetype='NULL',primary_bone_axis='Y',secondary_bone_axis='X',mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)
