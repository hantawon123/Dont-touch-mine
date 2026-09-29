"""Add the crouch correction to the existing Unity idle FBX without losing breathing."""
import sys
from pathlib import Path
import bpy

source, destination = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source, use_anim=True, ignore_leaf_bones=False)
body = bpy.data.objects["Body"]
keys = body.data.shape_keys
print("IMPORTED_KEYS", [key.name for key in keys.key_blocks])
key = keys.key_blocks.get("Crouch_Groin_Flat") or body.shape_key_add(name="Crouch_Groin_Flat")
basis = keys.key_blocks[0]
for point, base in zip(key.data, basis.data):
    point.co = base.co
key.value = 0.0
key.keyframe_insert("value", frame=0)
key.keyframe_insert("value", frame=60)
# Keep the lower torso round while the prone leg bones open.  The correction
# expands only the waist belt in depth, so it fills the skinning hollow without
# changing the feet or the standing silhouette.
waist = keys.key_blocks.get("Crawl_Waist_Round") or body.shape_key_add(name="Crawl_Waist_Round")
basis = keys.key_blocks[0]
for point, base in zip(waist.data, basis.data):
    point.co = base.co
    x, y, z = point.co
    belt = max(0.0, 1.0 - abs(y - 0.78) / 0.50)
    width = max(0.0, 1.0 - abs(x) / 0.88)
    if belt and width:
        if abs(z) > 0.02:
            point.co.z += (0.18 if z > 0 else -0.18) * belt * width
        if abs(x) > 0.03:
            point.co.x += (0.07 if x > 0 else -0.07) * belt * width
waist.value = 0.0
waist.keyframe_insert("value", frame=0)
waist.keyframe_insert("value", frame=60)
# Preserve the required two-second idle breathing cycle while adding the
# crouch/crawl correction keys.
belly = keys.key_blocks.get("Belly_Breath")
if belly:
    for frame, value in ((0, 0.0), (24, 100.0), (60, 0.0)):
        belly.value = value
        belly.keyframe_insert("value", frame=frame)
for action in bpy.data.actions:
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag = strip.channelbag(slot)
                if bag:
                    for curve in bag.fcurves:
                        for point in curve.keyframe_points:
                            point.interpolation = "LINEAR"
bpy.ops.object.select_all(action="DESELECT")
for obj in bpy.context.scene.objects:
    if obj.type in {"ARMATURE", "MESH"}:
        obj.select_set(True)
Path(destination).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(filepath=destination, use_selection=True,
    object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=True,
    bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
    bake_anim_step=1, bake_anim_simplify_factor=0,
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', armature_nodetype='NULL',
    primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=False,
    mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
print("EXPORTED", destination)
