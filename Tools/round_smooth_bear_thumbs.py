"""Round only the SmoothBear thumb tips while preserving skinning and shape deltas."""

import bpy
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "smooth-bear-unity" / "thumb-round"
OUT.mkdir(parents=True, exist_ok=True)
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


def weights(body):
    return [
        sorted((body.vertex_groups[item.group].name, item.weight) for item in vertex.groups)
        for vertex in body.data.vertices
    ]


def round_tip(body, group_name):
    group = body.vertex_groups[group_name]
    vertices = [
        vertex
        for vertex in body.data.vertices
        if any(item.group == group.index and item.weight >= 0.5 for item in vertex.groups)
    ]
    if not vertices:
        raise RuntimeError(f"No vertices weighted to {group_name}.")

    center_x = sum(vertex.co.x for vertex in vertices) / len(vertices)
    center_z = sum(vertex.co.z for vertex in vertices) / len(vertices)
    tip_y = max(vertex.co.y for vertex in vertices)
    radius = 0.022
    base_y = tip_y - radius
    changed = []

    for vertex in vertices:
        radial = math.hypot(vertex.co.x - center_x, vertex.co.z - center_z)
        if vertex.co.y <= base_y or radial >= radius:
            continue

        rounded_y = base_y + math.sqrt(radius * radius - radial * radial)
        delta_y = rounded_y - vertex.co.y
        if abs(delta_y) < 0.000001:
            continue

        vertex.co.y = rounded_y
        if body.data.shape_keys:
            for key in body.data.shape_keys.key_blocks:
                key.data[vertex.index].co.y += delta_y
        changed.append(vertex.index)

    return changed


def main():
    body = bpy.data.objects["Body"]
    rig = bpy.data.objects["DGN_Armature"]
    before_weights = weights(body)
    before_groups = [group.name for group in body.vertex_groups]
    before_shape_deltas = {}
    if body.data.shape_keys:
        basis = body.data.shape_keys.key_blocks[0]
        for key in body.data.shape_keys.key_blocks[1:]:
            before_shape_deltas[key.name] = [
                tuple(key_vertex.co - basis_vertex.co)
                for key_vertex, basis_vertex in zip(key.data, basis.data)
            ]

    changed = {
        "Finger_T2.L": round_tip(body, "Finger_T2.L"),
        "Finger_T2.R": round_tip(body, "Finger_T2.R"),
    }
    body.data.update()

    if weights(body) != before_weights:
        raise RuntimeError("Thumb rounding changed skin weights.")
    if [group.name for group in body.vertex_groups] != before_groups:
        raise RuntimeError("Thumb rounding changed vertex groups.")
    if body.data.shape_keys:
        basis = body.data.shape_keys.key_blocks[0]
        for key in body.data.shape_keys.key_blocks[1:]:
            after = [
                tuple(key_vertex.co - basis_vertex.co)
                for key_vertex, basis_vertex in zip(key.data, basis.data)
            ]
            if after != before_shape_deltas[key.name]:
                raise RuntimeError(f"Thumb rounding changed shape delta {key.name}.")

    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"ARMATURE", "MESH"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = rig

    kwargs = FBX_KW.copy()
    kwargs.update(bake_anim=False, embed_textures=False, path_mode="AUTO")
    bpy.ops.export_scene.fbx(filepath=str(OUT / "SmoothBear.fbx"), **kwargs)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "SmoothBear.blend"), check_existing=False)

    report = {
        "vertex_count": len(body.data.vertices),
        "shape_keys": [key.name for key in body.data.shape_keys.key_blocks] if body.data.shape_keys else [],
        "changed": changed,
    }
    (OUT / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("THUMB_ROUND_READY", report, flush=True)


main()
