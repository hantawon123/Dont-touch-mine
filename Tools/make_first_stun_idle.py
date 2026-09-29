"""Bake Stun_Idle: loop the final Stun_Start pose with soft breathing.

Belly_Breath mirrors Idle (0→100→0). A small chest/hip lift keeps the
back-fall body from looking frozen.

blender --background --python Tools/make_first_stun_idle.py -- FIRST_DIR
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
LAST = 60
NAME = "Stun_Idle"
SOURCE = FIRST / "FirstPlayerCapsule_Stun_Start.fbx"


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def pose_at(rig, frame):
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    return {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}


def breath_t(frame):
    # Same phase as Idle: empty → full mid → empty, looping at 0/LAST.
    return math.sin(math.pi * frame / LAST)


source = load(SOURCE)
last = int(source.animation_data.action.frame_range[1])
hold = pose_at(source, last)

rig = load(FIRST / "FirstPlayerCapsule_Idle.fbx")
keys = bpy.data.objects["Body"].data.shape_keys
rig.animation_data.action = bpy.data.actions.new(NAME)
keys.animation_data_clear()
if keys.animation_data is None:
    keys.animation_data_create()
keys.animation_data.action = bpy.data.actions.new(f"{NAME}_shapes")

for frame in range(LAST + 1):
    amount = breath_t(frame)
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis = hold[bone.name]
    bpy.context.view_layer.update()
    # On the back, inhale lifts the torso off the floor a little (+Y in bake space).
    hips = rig.pose.bones["Hips"]
    hips.location.y += 0.012 * amount
    if "Spine" in rig.pose.bones:
        spine = rig.pose.bones["Spine"]
        spine.location.y += 0.006 * amount
    bpy.context.view_layer.update()
    for bone in rig.pose.bones:
        for prop in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(prop, frame=frame)
    for key in keys.key_blocks[1:]:
        if key.name == "Belly_Breath":
            key.value = 100.0 * amount
        else:
            key.value = 0.0
        key.keyframe_insert("value", frame=frame)

scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 0
scene.frame_end = LAST
scene.frame_set(0)
bpy.ops.export_scene.fbx(
    filepath=str(FIRST / f"FirstPlayerCapsule_{NAME}.fbx"),
    use_selection=False,
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=False,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1,
    bake_anim_simplify_factor=0,
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    armature_nodetype="NULL",
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    mesh_smooth_type="FACE",
    path_mode="COPY",
    embed_textures=True,
)
print("BAKED", NAME, LAST, "breath")
