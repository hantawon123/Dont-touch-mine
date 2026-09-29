import os
import bpy

OUTPUT_PATH = "/Users/barleymilk/Projects/ssafy/S15P21D205/Assets/Scenes/CharacterTest/FromBlender/BlenderPreview.fbx"

scene = bpy.context.scene
scene.frame_set(scene.frame_start)

armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
for obj in bpy.data.objects:
    obj.select_set(False)

for armature in armatures:
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.animation_data_clear()
    armature.select_set(False)

for obj in bpy.data.objects:
    if obj.type in {"MESH", "ARMATURE"}:
        obj.select_set(True)
    else:
        obj.select_set(False)

os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)

bpy.ops.export_scene.fbx(
    filepath=OUTPUT_PATH,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    axis_forward="-Z",
    axis_up="Y",
    apply_scale_options="FBX_SCALE_NONE",
    add_leaf_bones=False,
    bake_anim=False,
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
)

print("EXPORTED", OUTPUT_PATH)
