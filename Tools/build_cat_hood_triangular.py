"""Build a clean cat hood + thick triangular cone ears.

blender --background --python Tools/build_cat_hood_triangular.py -- INPUT.blend
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bone_parent(obj, rig, bone_name):
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    bone = rig.pose.bones[bone_name]
    obj.matrix_parent_inverse = (rig.matrix_world @ bone.matrix).inverted()
    obj.matrix_world = world


def make_shell(material):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=48,
        ring_count=28,
        location=(0.0, 0.02, 1.55),
        scale=(0.40, 0.36, 0.40),
    )
    shell = bpy.context.object
    shell.name = "Hood"
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth()

    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=32,
        ring_count=20,
        location=(0.0, -0.38, 1.50),
        scale=(0.22, 0.17, 0.21),
    )
    cutter = bpy.context.object
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    activate(shell)
    boolean = shell.modifiers.new("FaceHole", "BOOLEAN")
    boolean.operation = "DIFFERENCE"
    boolean.operand_type = "OBJECT"
    boolean.object = cutter
    if hasattr(boolean, "solver"):
        boolean.solver = "FLOAT"
    bpy.ops.object.modifier_apply(modifier=boolean.name)
    bpy.data.objects.remove(cutter, do_unlink=True)

    solid = shell.modifiers.new("Solidify", "SOLIDIFY")
    solid.thickness = 0.02
    solid.offset = 0.0
    solid.use_even_offset = True
    sub = shell.modifiers.new("Subsurf", "SUBSURF")
    sub.levels = 1
    sub.render_levels = 1

    if material is not None:
        shell.data.materials.clear()
        shell.data.materials.append(material)
    return shell


def make_triangle_ear(side, material):
    """Thick triangular ear from a 3-sided cone (pyramid-like)."""
    sign = 1.0 if side == "L" else -1.0

    # Cone with 3 sides = triangular base silhouette; tip = ear tip
    bpy.ops.mesh.primitive_cone_add(
        vertices=3,
        radius1=0.11,
        radius2=0.0,
        depth=0.17,
        end_fill_type="NGON",
        location=(0.0, 0.0, 0.0),
    )
    ear = bpy.context.object
    ear.name = f"HoodEar_{side}"
    bpy.ops.object.shade_smooth()

    # Flatten front-back a bit so side view is thin-ish but still solid
    ear.scale = (1.05, 0.55, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # Soften edges
    bevel = ear.modifiers.new("SoftEdge", "BEVEL")
    bevel.width = 0.012
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bpy.ops.object.modifier_apply(modifier=bevel.name)

    sub = ear.modifiers.new("Sub", "SUBSURF")
    sub.levels = 1
    sub.render_levels = 1
    bpy.ops.object.modifier_apply(modifier=sub.name)

    # Orient tip up, lean outward, face slightly forward
    ear.location = (sign * 0.27, -0.02, 1.95)
    ear.rotation_euler = (
        math.radians(8.0),
        math.radians(sign * -20.0),
        math.radians(sign * 12.0),
    )
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    if material is not None:
        ear.data.materials.clear()
        ear.data.materials.append(material)
    return ear


def render_preview(output_path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 800
    scene.render.filepath = str(output_path)
    if scene.world:
        scene.world.color = (0.12, 0.11, 0.12)

    bpy.ops.object.light_add(type="AREA", location=(2.0, -2.0, 3.0))
    key = bpy.context.object
    key.data.energy = 400
    key.data.size = 2.5

    bpy.ops.object.camera_add(location=(0.9, -2.15, 1.78))
    cam = bpy.context.object
    target = Vector((0.0, 0.0, 1.72))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 50
    scene.camera = cam
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.objects.remove(key, do_unlink=True)


def cleanup_old_hood_parts():
    keep_mats = []
    for name in list(bpy.data.objects.keys()):
        if name in (
            "Hood",
            "Hood_OldBumpy",
            "HoodShell",
            "HoodEar_L",
            "HoodEar_R",
            "HoodEar.L",
            "HoodEar.R",
        ):
            obj = bpy.data.objects[name]
            if obj.data.materials and not keep_mats:
                keep_mats.append(obj.data.materials[0])
            bpy.data.objects.remove(obj, do_unlink=True)
    return keep_mats[0] if keep_mats else None


def build(blend_path: Path):
    bpy.ops.wm.open_mainfile(filepath=str(blend_path), use_scripts=False)
    rig = bpy.data.objects["DGN_Armature"]

    material = None
    for candidate in ("Hood", "Hood_OldBumpy"):
        obj = bpy.data.objects.get(candidate)
        if obj and obj.data.materials:
            material = obj.data.materials[0]
            break
    if material is None:
        material = bpy.data.materials.get("MAT_Hood")

    cleanup_old_hood_parts()

    hood = make_shell(material)
    ear_l = make_triangle_ear("L", material)
    ear_r = make_triangle_ear("R", material)

    bone_parent(hood, rig, "Head")
    bone_parent(ear_l, rig, "Head")
    bone_parent(ear_r, rig, "Head")

    preview = blend_path.with_name(blend_path.stem + "_tri_ears.png")
    render_preview(preview)
    bpy.ops.wm.save_mainfile(filepath=str(blend_path), compress=False)
    print(
        "BUILT",
        "Hood",
        len(hood.data.vertices),
        "EarL",
        len(ear_l.data.vertices),
        "EarR",
        len(ear_r.data.vertices),
        "SAVED",
        blend_path,
    )
    print("PREVIEW", preview)


if __name__ == "__main__":
    build(Path(sys.argv[sys.argv.index("--") + 1]))
