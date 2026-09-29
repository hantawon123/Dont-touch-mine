"""Build First 포복 clips: belly on the floor, hands planted, no rifle pose.

Blender --background --python Tools/make_first_prone.py -- IDLE.fbx CRAWL.fbx OUT_DIR
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


idle_path, crawl_path, out_dir = sys.argv[sys.argv.index("--") + 1 :]
out_dir = Path(out_dir)
out_dir.mkdir(parents=True, exist_ok=True)


def sample_basis(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=True, ignore_leaf_bones=False)
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    action = rig.animation_data.action
    first, last = (int(action.frame_range[0]), int(action.frame_range[1]))
    samples = {}
    for frame in range(first, last + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        samples[frame] = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
    return samples, first, last


def apply_world_rotation(bone, world_rot):
    axes = bone.matrix.to_3x3()
    local = axes.inverted() @ world_rot.to_matrix() @ axes
    bone.matrix_basis = bone.matrix_basis @ local.to_4x4()


def aim_bone(bone, desired_dir, weight=1.0):
    current = bone.tail - bone.head
    if current.length < 1e-8 or weight <= 0:
        return
    blended = current.normalized().lerp(desired_dir.normalized(), weight)
    if blended.length < 1e-8:
        return
    apply_world_rotation(bone, current.normalized().rotation_difference(blended.normalized()))


def lerp_matrix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(a_loc.lerp(b_loc, t), a_rot.slerp(b_rot, t), a_scale.lerp(b_scale, t))


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def export_fbx(rig, path, first, last):
    scene = bpy.context.scene
    scene.frame_start = first
    scene.frame_end = last
    scene.frame_set(first)
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    for obj in [rig, *meshes]:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
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
    print("EXPORTED", path, first, last)


crawl_samples, crawl_first, crawl_last = sample_basis(crawl_path)
crawl_span = max(1, crawl_last - crawl_first)
crawl_ref = crawl_samples[crawl_first]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=idle_path, use_anim=True, ignore_leaf_bones=False)
scene = bpy.context.scene
rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
body = bpy.data.objects.get("Body")
scene.frame_set(int(rig.animation_data.action.frame_range[0]))
bpy.context.view_layer.update()
idle_basis = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
for bone in ordered:
    bone.rotation_mode = "QUATERNION"
    bone.matrix_basis = idle_basis[bone.name]
bpy.context.view_layer.update()

# Belly-down: spine points forward (+Z), legs trail backward.
rig.pose.bones["Hips"].matrix_basis = idle_basis["Hips"] @ Matrix.Rotation(math.pi / 2.0, 4, "X")
bpy.context.view_layer.update()
hips = rig.pose.bones["Hips"]
hips.location.y += 0.26 - hips.head.y
bpy.context.view_layer.update()

floor_y = 0.10


def plant_end(control_name, end_name, target_y):
    control = rig.pose.bones[control_name]
    end = rig.pose.bones[end_name]
    lift = target_y - end.head.y
    if abs(lift) < 0.001:
        return
    current = control.tail - control.head
    target = Vector((current.x, current.y + lift, current.z))
    if target.length > 1e-8:
        apply_world_rotation(control, current.normalized().rotation_difference(target.normalized()))
    bpy.context.view_layer.update()


# Face currently points at the floor after the belly-down hip pitch; rotate it forward.
aim_bone(rig.pose.bones["Spine"], Vector((0.0, 0.10, 0.995)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["Neck"], Vector((0.0, 0.22, 0.975)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["Head"], Vector((0.0, 0.18, 0.984)))
bpy.context.view_layer.update()
apply_world_rotation(rig.pose.bones["Neck"], Matrix.Rotation(math.radians(-38.0), 4, "X").to_quaternion())
bpy.context.view_layer.update()
apply_world_rotation(rig.pose.bones["Head"], Matrix.Rotation(math.radians(-8.0), 4, "X").to_quaternion())
bpy.context.view_layer.update()
# ㄴ arms: upper arm down toward the floor, forearm forward along the ground.
aim_bone(rig.pose.bones["UpperArm.L"], Vector((0.28, -0.70, 0.66)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["UpperArm.R"], Vector((-0.28, -0.70, 0.66)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["Arm.L"], Vector((0.12, 0.02, 0.993)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["Arm.R"], Vector((-0.12, 0.02, 0.993)))
bpy.context.view_layer.update()
for side in ("L", "R"):
    if f"Finger_T1.{side}" in rig.pose.bones:
        aim_bone(rig.pose.bones[f"Finger_T1.{side}"], Vector((0.0, -0.08, 0.997)), 0.5)
        bpy.context.view_layer.update()
aim_bone(rig.pose.bones["UpperLeg.L"], Vector((0.12, -0.18, -0.976)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["UpperLeg.R"], Vector((-0.12, -0.18, -0.976)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["Leg.L"], Vector((0.04, -0.22, -0.975)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["Leg.R"], Vector((-0.04, -0.22, -0.975)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["Foot.L"], Vector((0.06, -0.52, -0.85)))
bpy.context.view_layer.update()
aim_bone(rig.pose.bones["Foot.R"], Vector((-0.06, -0.52, -0.85)))
bpy.context.view_layer.update()
for side in ("L", "R"):
    toe = rig.pose.bones.get(f"FootToe1.{side}")
    if toe is not None:
        aim_bone(toe, Vector((0.06 if side == "L" else -0.06, -0.58, -0.81)))
        bpy.context.view_layer.update()
        plant_end(f"Foot.{side}", f"FootToe1.{side}", floor_y)
    else:
        plant_end(f"Leg.{side}", f"Foot.{side}", floor_y)

prone_bind = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
prone_heads = {bone.name: bone.head.copy() for bone in rig.pose.bones}
leg_bones = [
    name for name in prone_bind
    if name.startswith("UpperLeg") or name.startswith("Leg.")
    or name.startswith("Foot.") or name.startswith("FootToe")
]


def crawl_delta(frame, last, reverse=False):
    progress = (frame - 1) / max(1, last - 1)
    if reverse:
        progress = 1.0 - progress
    source = crawl_first + progress * crawl_span
    low = int(math.floor(source))
    high = min(crawl_last, low + 1)
    t = source - low
    low = max(crawl_first, min(crawl_last, low))
    high = max(crawl_first, min(crawl_last, high))
    return low, high, t


def apply_crawl(frame, last, reverse=False, sway=0.0):
    for bone in ordered:
        bone.matrix_basis = prone_bind[bone.name]
    bpy.context.view_layer.update()
    # Both legs extended -> right folds outward and forward -> extended ->
    # left folds outward and forward -> extended, following the illustration.
    cycle = math.sin((frame - 1) / last * math.pi * 2.0) * (-1 if reverse else 1)
    for side in ("L", "R"):
        sign = 1.0 if side == "L" else -1.0
        fold = smooth(max(0.0, cycle * (-sign)))
        # A single bending plane travels with the hip. The thigh opens to
        # 85 degrees; the shin returns behind it, making a real knee hinge.
        hip_angle = math.radians(12 + 73 * fold)
        knee_angle = math.radians(2 + 93 * fold)
        upper = Vector((sign * math.sin(hip_angle), -0.16, -math.cos(hip_angle))).normalized()
        lower_angle = hip_angle - knee_angle
        lower = Vector((sign * math.sin(lower_angle), -0.16, -math.cos(lower_angle))).normalized()
        aim_bone(rig.pose.bones[f"UpperLeg.{side}"], upper)
        bpy.context.view_layer.update()
        # The reference FBX flexes the knee around local -X. Rotate the
        # thigh at the hip so that this hinge follows the outward leg plane;
        # otherwise aiming the shin alone bends the knee sideways around Z.
        thigh = rig.pose.bones[f"UpperLeg.{side}"]
        shin = rig.pose.bones[f"Leg.{side}"]
        shin.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        hinge = -upper.cross(lower).normalized()
        current_axis = shin.matrix.to_3x3().col[0].normalized()
        current_axis = (current_axis - upper * current_axis.dot(upper)).normalized()
        twist = math.atan2(upper.dot(current_axis.cross(hinge)), current_axis.dot(hinge))
        apply_world_rotation(thigh, Matrix.Rotation(twist * fold, 4, upper).to_quaternion())
        bpy.context.view_layer.update()
        shin.matrix_basis = Matrix.Rotation(-knee_angle, 4, 'X')
        bpy.context.view_layer.update()
        rig.pose.bones[f"Foot.{side}"].matrix_basis = idle_basis[f"Foot.{side}"]
        rig.pose.bones[f"FootToe1.{side}"].matrix_basis = idle_basis[f"FootToe1.{side}"]
        bpy.context.view_layer.update()
        aim_bone(rig.pose.bones[f"Foot.{side}"], Vector((sign * 0.45, -0.25, -0.86)))
        bpy.context.view_layer.update()
    bpy.context.view_layer.update()
    phase = math.sin((frame - 1) / last * math.pi * 2.0)
    if reverse:
        phase = -phase

    # Baby crawl: diagonal hand and knee pairs alternate.  The chest stays
    # low, each reaching arm stretches forward, and the opposite knee folds
    # under the hip before pushing back.  No imported humanoid crawl motion is
    # mixed in, which keeps First's short capsule limbs soft and readable.
    for side, sign in (("L", 1.0), ("R", -1.0)):
        reach = phase * sign
        tuck = -reach
        aim_bone(
            rig.pose.bones[f"UpperArm.{side}"],
            Vector((0.30 * sign, -0.62 + 0.16 * reach, 0.72 + 0.16 * reach)),
        )
        bpy.context.view_layer.update()
        aim_bone(
            rig.pose.bones[f"Arm.{side}"],
            Vector((0.10 * sign, -0.04 - 0.08 * reach, 0.995)),
        )
        bpy.context.view_layer.update()
        plant_end(f"Arm.{side}", f"Hand.{side}", floor_y)
    bpy.context.view_layer.update()
    if sway:
        hips = rig.pose.bones["Hips"]
        hips.location.x += phase * sway
        bpy.context.view_layer.update()


# Use the same neutral pose for entering, idling, crawling and leaving prone.
apply_crawl(1, 36)
prone_bind = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}


def make_crawl_correctives():
    """Bake pose-space thigh volume and a broad upward flank follow-through.

    Invert the actual linear skinning transform so FBX/Unity reproduces the
    correction without requiring Blender's preserve-volume skinning mode.
    """
    keys = body.data.shape_keys
    idle_action = rig.animation_data.action
    for k in keys.key_blocks[1:]: k.value = 0
    basis = keys.key_blocks[0]
    mesh_to_rig = rig.matrix_world.inverted() @ body.matrix_world
    rig_to_mesh = mesh_to_rig.inverted()
    for side, frame in (('R', 10), ('L', 28)):
        for k in keys.key_blocks[1:]: k.value = 0
        apply_crawl(frame, 36)
        bpy.context.view_layer.update()
        transforms = {g.index: rig_to_mesh @ rig.pose.bones[g.name].matrix @ rig.data.bones[g.name].matrix_local.inverted() @ mesh_to_rig
                      for g in body.vertex_groups if g.name in rig.pose.bones}
        key = keys.key_blocks.get('Crawl_Follow_'+side) or body.shape_key_add(name='Crawl_Follow_'+side)
        thigh = rig.pose.bones['UpperLeg.'+side]
        rest = thigh.bone
        rest_axis = (rest.tail_local-rest.head_local).normalized()
        axis = (thigh.tail-thigh.head).normalized()
        sign = 1 if side=='L' else -1
        largest = 0.0
        for v, base, point in zip(body.data.vertices, basis.data, key.data):
            weights = [(g.weight, transforms[g.group]) for g in v.groups if g.group in transforms]
            total = sum(w for w,m in weights)
            point.co = base.co
            if total < 1e-8: continue
            skin = Matrix(tuple(tuple(sum(w*m[i][j] for w,m in weights)/total for j in range(4)) for i in range(4)))
            p = mesh_to_rig @ base.co
            posed = mesh_to_rig @ (skin @ base.co)
            correction = Vector((0,0,0))
            # Cross sections of the thigh keep their original radius. Fade
            # continuously into knee and pelvis instead of making a hard cuff.
            along = (p-rest.head_local).dot(rest_axis)/rest.length
            radial = p-rest.head_local-rest_axis*((p-rest.head_local).dot(rest_axis))
            mask = smooth((along-0.08)/0.35) * smooth((1.10-along)/0.35)
            mask *= smooth((sign*p.x-0.025)/0.10)
            mask *= smooth((0.23-radial.length)/0.10)
            if mask>0 and radial.length<0.21:
                now = posed-thigh.head-axis*((posed-thigh.head).dot(axis))
                if now.length>1e-5:
                    amount = max(0.0, min(0.065, radial.length*0.98-now.length))
                    correction += now.normalized()*amount*mask
            # Broad flank displacement follows the pulling thigh upwards;
            # the centre of the belly follows more softly, with no folding.
            side_mask = smooth((sign*p.x+0.10)/0.32)
            belt = smooth((p.y-0.43)/0.20)*smooth((1.08-p.y)/0.30)
            correction += Vector((sign*0.008,0.035,0.012))*side_mask*belt
            corrected = posed+correction
            point.co = skin.inverted_safe() @ (rig_to_mesh @ corrected)
            largest=max(largest,correction.length)
        # Smooth the displacement field (not the base mesh) across the
        # continuous thigh/flank surface, avoiding a visible correction seam.
        neighbors=[set() for v in body.data.vertices]
        for e in body.data.edges:
            a,b=e.vertices
            neighbors[a].add(b); neighbors[b].add(a)
        offsets=[p.co-b.co for p,b in zip(key.data,basis.data)]
        for iteration in range(8):
            offsets=[d.lerp(sum((offsets[j] for j in neighbors[i]),Vector())/len(neighbors[i]),0.5) if neighbors[i] else d for i,d in enumerate(offsets)]
        for p,b,d in zip(key.data,basis.data,offsets): p.co=b.co+d
        key.value=0
        key.slider_max=1
        print('CORRECTIVE',side,'max posed displacement',largest)
    # The common Unity character mesh must contain these same shape keys.
    rig.animation_data.action=idle_action
    first,last=(int(v) for v in idle_action.frame_range)
    for name in ('Crawl_Follow_L','Crawl_Follow_R'):
        key=keys.key_blocks[name]
        for f in (first,last):
            key.value=0
            key.keyframe_insert('value',frame=f)
    export_fbx(rig,out_dir/'FirstPlayerCapsule_Idle_Breathing_2s.fbx',first,last)


make_crawl_correctives()


def write_action(name, last, filler):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create().action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    for frame in range(1, last + 2):
        filler(frame, last)
        key_pose(rig, frame)
        if body and body.data.shape_keys:
            for key in body.data.shape_keys.key_blocks[1:]:
                key.value = 0.0
                if name.startswith('Crawl_') and key.name.startswith('Crawl_Follow_'):
                    phase=math.sin((frame-1)/last*math.pi*2)
                    if name=='Crawl_Back': phase=-phase
                    key.value=smooth(max(0,phase*(1 if key.name.endswith('_R') else -1)))
                key.keyframe_insert("value", frame=frame)
    return action


def idle_fill(frame, last):
    for bone in ordered:
        bone.matrix_basis = prone_bind[bone.name]
    bpy.context.view_layer.update()
    pulse = math.sin((frame - 1) / 60.0 * math.pi * 2.0) * 0.02
    hips = rig.pose.bones["Hips"]
    hips.location.y += pulse * 0.4
    bpy.context.view_layer.update()


def start_fill(frame, last):
    t = smooth((frame - 1) / max(1, last - 1))
    for bone in ordered:
        bone.matrix_basis = lerp_matrix(idle_basis[bone.name], prone_bind[bone.name], t)
    bpy.context.view_layer.update()


def end_fill(frame, last):
    t = smooth((frame - 1) / max(1, last - 1))
    for bone in ordered:
        bone.matrix_basis = lerp_matrix(prone_bind[bone.name], idle_basis[bone.name], t)
    bpy.context.view_layer.update()


clips = [
    ("Prone_Idle", 60, idle_fill),
    ("Crawl_Forward", 36, lambda frame, last: apply_crawl(frame, last)),
    ("Crawl_Back", 36, lambda frame, last: apply_crawl(frame, last, reverse=True)),
    ("Crawl_Left", 36, lambda frame, last: apply_crawl(frame, last, sway=0.03)),
    ("Crawl_Right", 36, lambda frame, last: apply_crawl(frame, last, sway=-0.03)),
    ("Prone_Start", 24, start_fill),
    ("Prone_End", 24, end_fill),
]

for name, last, filler in clips:
    write_action(name, last, filler)
    export_fbx(rig, out_dir / f"FirstPlayerCapsule_{name}.fbx", 1, last + 1)
    if name == "Crawl_Forward":
        bpy.ops.wm.save_as_mainfile(filepath=str(out_dir / "Crawl_Forward.blend"))

print("PRONE_CLIPS", out_dir)
