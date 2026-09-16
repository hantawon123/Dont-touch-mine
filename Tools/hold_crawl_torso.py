"""Hold the crawl torso still, including the chest attached to the arms.

Strips arm/shoulder weights from the torso capsule only (arm lobes keep
their weights). Rebuilds Crawl_Follow_L/R to cancel leftover thigh drag.

blender --background --python Tools/hold_crawl_torso.py
"""
from __future__ import annotations

import json
import math
import shutil
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[1]
UNITY = ROOT / "Assets" / "Scenes" / "CharacterTest" / "First"
ART = Path(
    r"C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions"
)
DEST = ART / "crawl-torso-hold"
BACKUP = ART / "before-crawl-torso-hold"
MANIFEST = ART / "manifest.json"

TORSO = {"Hips", "Spine", "Neck", "Head"}
ARM_PREFIXES = ("Finger_", "Hand.", "Arm.", "UpperArm.", "Shoulder.")
LEG_PREFIXES = ("UpperLeg.", "Leg.", "Foot", "FootToe")
FBX_KW = dict(
    use_selection=True,
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
    use_armature_deform_only=False,
    mesh_smooth_type="FACE",
    path_mode="COPY",
    embed_textures=True,
)


def smooth(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def import_fbx(path: Path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    rig = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    body = next(obj for obj in bpy.data.objects if obj.type == "MESH" and "Body" in obj.name)
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    return rig, body, meshes


def skin_from_names(vertex, body, transforms, names):
    weights = []
    for group in vertex.groups:
        if group.weight < 1e-8 or group.group not in transforms:
            continue
        name = body.vertex_groups[group.group].name
        if names is not None and name not in names:
            continue
        weights.append((group.weight, transforms[group.group]))
    total = sum(weight for weight, _ in weights)
    if total < 1e-8:
        return None
    return Matrix(
        tuple(
            tuple(sum(weight * matrix[i][j] for weight, matrix in weights) / total for j in range(4))
            for i in range(4)
        )
    )


def thigh_keep(co: Vector, rig) -> float:
    """1 on the hanging thigh tube, 0 on the hip wall."""
    best = 0.0
    for side, sign in (("L", 1.0), ("R", -1.0)):
        bone = rig.data.bones[f"UpperLeg.{side}"]
        head, axis = bone.head_local, (bone.tail_local - bone.head_local).normalized()
        along = (co - head).dot(axis) / max(bone.length, 1e-5)
        radial = (co - head - axis * (co - head).dot(axis)).length
        thigh = smooth((along - 0.12) / 0.14) * smooth((0.52 - co.y) / 0.08)
        thigh *= smooth((0.22 - radial) / 0.10) * smooth((sign * co.x - 0.12) / 0.10)
        best = max(best, thigh)
    return best


def limb_mask(co: Vector, rig) -> float:
    """1 on hanging thigh/arm tubes that must keep moving."""
    return max(thigh_keep(co, rig), arm_keep(co, rig))


def arm_keep(co: Vector, rig) -> float:
    """1 on the real arm/hand, 0 on the chest wall."""
    best = smooth((abs(co.x) - 0.55) / 0.12)
    for side, sign in (("L", 1.0), ("R", -1.0)):
        for name in (f"UpperArm.{side}", f"Arm.{side}"):
            bone = rig.data.bones[name]
            head, axis = bone.head_local, (bone.tail_local - bone.head_local).normalized()
            along = (co - head).dot(axis) / max(bone.length, 1e-5)
            radial = (co - head - axis * (co - head).dot(axis)).length
            tube = smooth((along - 0.04) / 0.12) * smooth((1.4 - along) / 0.25)
            tube *= smooth((0.18 - radial) / 0.08)
            tube *= smooth((sign * co.x - 0.38) / 0.14)
            best = max(best, tube)
    return best


def isolate_chest(body, rig):
    hips = body.vertex_groups["Hips"]
    spine = body.vertex_groups["Spine"]
    touched = 0
    moved = 0.0
    for vertex in body.data.vertices:
        capsule = 1.0 - arm_keep(vertex.co, rig)
        if capsule < 0.08:
            continue
        take_total = 0.0
        for group in list(vertex.groups):
            name = body.vertex_groups[group.group].name
            weight = group.weight
            if weight < 1e-6:
                continue
            if not any(name.startswith(prefix) for prefix in ARM_PREFIXES):
                continue
            take = weight * capsule
            if take < 1e-5:
                continue
            body.vertex_groups[group.group].add([vertex.index], weight - take, "REPLACE")
            take_total += take
        if take_total < 1e-5:
            continue
        hips_share = 0.15 + 0.55 * smooth((0.95 - vertex.co.y) / 0.45)
        hips.add([vertex.index], take_total * hips_share, "ADD")
        spine.add([vertex.index], take_total * (1.0 - hips_share), "ADD")
        touched += 1
        moved += take_total
    print(f"ISOLATE_CHEST verts={touched} weight_moved={moved:.3f}", flush=True)


def isolate_flank(body, rig):
    hips = body.vertex_groups["Hips"]
    spine = body.vertex_groups["Spine"]
    touched = 0
    moved = 0.0
    for vertex in body.data.vertices:
        capsule = 1.0 - thigh_keep(vertex.co, rig)
        if capsule < 0.08:
            continue
        take_total = 0.0
        for group in list(vertex.groups):
            name = body.vertex_groups[group.group].name
            weight = group.weight
            if weight < 1e-6:
                continue
            if not any(name.startswith(prefix) for prefix in LEG_PREFIXES):
                continue
            take = weight * capsule
            if take < 1e-5:
                continue
            body.vertex_groups[group.group].add([vertex.index], weight - take, "REPLACE")
            take_total += take
        if take_total < 1e-5:
            continue
        hips_share = 0.35 + 0.45 * smooth((0.90 - vertex.co.y) / 0.40)
        hips.add([vertex.index], take_total * hips_share, "ADD")
        spine.add([vertex.index], take_total * (1.0 - hips_share), "ADD")
        touched += 1
        moved += take_total
    print(f"ISOLATE_FLANK verts={touched} weight_moved={moved:.3f}", flush=True)


def isolate_torso(body, rig):
    isolate_chest(body, rig)


def rebuild_follow(body, rig, side: str, frame: int):
    keys = body.data.shape_keys
    basis = keys.key_blocks[0]
    for key in keys.key_blocks[1:]:
        key.value = 0.0
    if keys.animation_data:
        keys.animation_data_clear()
    scene = bpy.context.scene
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    mesh_to_rig = rig.matrix_world.inverted() @ body.matrix_world
    rig_to_mesh = mesh_to_rig.inverted()
    transforms = {
        group.index: rig_to_mesh
        @ rig.pose.bones[group.name].matrix
        @ rig.data.bones[group.name].matrix_local.inverted()
        @ mesh_to_rig
        for group in body.vertex_groups
        if group.name in rig.pose.bones
    }
    key = keys.key_blocks[f"Crawl_Follow_{side}"]
    sign = 1.0 if side == "L" else -1.0
    largest = 0.0
    for vertex, base, point in zip(body.data.vertices, basis.data, key.data):
        point.co = base.co
        rest = mesh_to_rig @ base.co
        hold = (1.0 - limb_mask(base.co, rig))
        hold *= smooth((sign * rest.x + 0.02) / 0.16)
        hold *= smooth((rest.y - 0.42) / 0.12) * smooth((1.42 - rest.y) / 0.16)
        if hold < 0.05:
            continue
        full = skin_from_names(vertex, body, transforms, None)
        torso = skin_from_names(vertex, body, transforms, TORSO)
        if full is None or torso is None:
            continue
        posed = mesh_to_rig @ (full @ base.co)
        wanted = mesh_to_rig @ (torso @ base.co)
        mixed = posed.lerp(wanted, hold)
        point.co = full.inverted_safe() @ (rig_to_mesh @ mixed)
        largest = max(largest, (mixed - posed).length)
    key.value = 0.0
    print(f"HOLD {side} frame {frame} max {largest:.4f}", flush=True)
    return [point.co.copy() for point in key.data]


def export_current(rig, meshes, path: Path, start: int, end: int):
    scene = bpy.context.scene
    scene.frame_start = start
    scene.frame_end = end
    scene.frame_set(start)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [rig, *meshes]:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(path), **FBX_KW)


def apply_follow_geometry(body, left, right):
    keys = body.data.shape_keys
    basis = keys.key_blocks[0]
    for key in keys.key_blocks[1:]:
        if key.name not in {"Crawl_Follow_L", "Crawl_Follow_R"}:
            for point, base in zip(key.data, basis.data):
                if key.name == "Crawl_Waist_Round":
                    point.co = base.co
    for point, co in zip(keys.key_blocks["Crawl_Follow_L"].data, left):
        point.co = co
    for point, co in zip(keys.key_blocks["Crawl_Follow_R"].data, right):
        point.co = co


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    BACKUP.mkdir(parents=True, exist_ok=True)
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    files = ["FirstPlayerCapsule_Idle.fbx"] + [
        entry["file"] for entry in manifest if "Crawl" in entry["clip"]
    ]
    ends = {entry["file"]: entry["end"] for entry in manifest}

    for name in files:
        backup = BACKUP / name
        source = UNITY / name
        if backup.exists():
            shutil.copy2(backup, source)

    rig, body, meshes = import_fbx(UNITY / "FirstPlayerCapsule_Crawl_Forward.fbx")
    isolate_torso(body, rig)
    right = rebuild_follow(body, rig, "R", 10)
    left = rebuild_follow(body, rig, "L", 28)
    apply_follow_geometry(body, left, right)

    # Quick posed check at the original pinch rest location.
    keys = body.data.shape_keys
    if keys.animation_data:
        keys.animation_data_clear()
    target = Vector((-0.313, 0.605, -0.091))
    index = min(range(len(body.data.vertices)), key=lambda i: (body.data.vertices[i].co - target).length)
    scene = bpy.context.scene
    for label, frame, fr, fl in (("bare", 10, 0.0, 0.0), ("hold", 10, 1.0, 0.0), ("neutral", 1, 0.0, 0.0)):
        scene.frame_set(frame)
        for key in keys.key_blocks[1:]:
            key.value = 0.0
        keys.key_blocks["Crawl_Follow_R"].value = fr
        keys.key_blocks["Crawl_Follow_L"].value = fl
        bpy.context.view_layer.update()
        evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        print(f"PINCH {label} {tuple(round(x, 3) for x in mesh.vertices[index].co)}", flush=True)
        evaluated.to_mesh_clear()

    installed = 0
    for name in files:
        source = UNITY / name
        backup = BACKUP / name
        if not backup.exists():
            shutil.copy2(source, backup)
        rig, body, meshes = import_fbx(source)
        isolate_torso(body, rig)
        apply_follow_geometry(body, left, right)
        action = rig.animation_data.action if rig.animation_data else None
        start = int(action.frame_range[0]) if action else 0
        end = ends.get(name, int(action.frame_range[1]) if action else 36)
        dest = DEST / name
        export_current(rig, meshes, dest, start, end)
        shutil.copy2(dest, source)
        installed += 1
        print("INSTALLED", installed, "/", len(files), name, flush=True)
    print("DONE", installed, flush=True)


if __name__ == "__main__":
    main()
