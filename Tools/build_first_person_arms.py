"""Cut the SmoothBear body down to its two arms and export a first-person arms FBX.

Runs on a COPY of the approved source (never on SmoothBear.blend itself):

    blender -b source/blender/characters/SmoothBear/FirstPerson/SmoothBear_FirstPersonArms_work.blend \
        --python Tools/build_first_person_arms.py

Output
- source/blender/characters/SmoothBear/FirstPerson/SmoothBear_FirstPersonArms.blend  (editable result)
- Assets/_Game/Content/Resources/FirstPerson/SmoothBear_FirstPersonArms.fbx  (Resources.Load 경로 FirstPerson/SmoothBear_FirstPersonArms)
- <scratch>/first-person-arms-report.json

The armature is exported whole so the existing Unity clips (DGN_Armature/Hips/Spine/... paths) drive it.
"""
import bmesh, bpy, json, os, sys
from pathlib import Path

ROOT = Path(bpy.data.filepath).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'Tools'))
from hold_crawl_torso import FBX_KW  # noqa: E402  team-shared Unity FBX settings

OUT_BLEND = ROOT / 'source/blender/characters/SmoothBear/FirstPerson/SmoothBear_FirstPersonArms.blend'
OUT_FBX = ROOT / 'Assets/_Game/Content/Resources/FirstPerson/SmoothBear_FirstPersonArms.fbx'
REPORT = Path(os.environ.get('FP_ARMS_REPORT', str(ROOT / 'first-person-arms-report.json')))
ARM_GROUPS = {'Shoulder', 'UpperArm', 'Arm', 'Hand', 'Finger_M1', 'Finger_M2', 'Finger_T1', 'Finger_T2',
              'Volume_UpperArm', 'Volume_Arm'}
MIN_ARM_WEIGHT = 0.3   # below this the vertex is torso/head skin that merely blends into the arm
CUT_OFFSET = 0.02      # metres outboard of the UpperArm root along the arm axis: the shoulder cut plane


def main():
    assert 'SmoothBear.blend' not in bpy.data.filepath, 'refusing to run on the original source file'
    rig = bpy.data.objects['DGN_Armature']
    body = bpy.data.objects['Body']

    # Rest pose, no animation: the cut must happen on the bind shape.
    rig.animation_data_clear()
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()

    # Drop everything that is not body+rig (hood, eyes, mouth, cameras, lights).
    for obj in list(bpy.data.objects):
        if obj not in (rig, body):
            bpy.data.objects.remove(obj, do_unlink=True)

    # Shape keys belong to the belly/groin; arms do not need them.
    if body.data.shape_keys:
        body.shape_key_clear()

    before = len(body.data.vertices)
    bm = bmesh.new()
    bm.from_mesh(body.data)
    mw = body.matrix_world
    group_names = {g.index: g.name for g in body.vertex_groups}
    deform = bm.verts.layers.deform.verify()

    def arm_weight(v):
        return sum(w for gi, w in v[deform].items() if group_names[gi].rsplit('.', 1)[0] in ARM_GROUPS)

    # One cut plane per arm, perpendicular to the upper arm and just outboard of its root, so the
    # open end is a clean ellipse at the shoulder instead of a ragged weight boundary.
    planes = []
    for side in ('L', 'R'):
        bone = rig.data.bones['UpperArm.' + side]
        head = rig.matrix_world @ bone.head_local
        axis = ((rig.matrix_world @ bone.tail_local) - head).normalized()
        planes.append((head + axis * CUT_OFFSET, axis))

    # 1) Drop torso/head skin by weight, leaving two arm islands with ragged shoulder flaps.
    doomed = [v for v in bm.verts if arm_weight(v) < MIN_ARM_WEIGHT]
    bmesh.ops.delete(bm, geom=doomed, context='VERTS')
    # 2) Slice each island with its shoulder plane (world space) and discard the inboard side,
    #    so the open end is one clean planar loop.
    for (origin, axis), sign in zip(planes, (1, -1)):
        island = [v for v in bm.verts if (mw @ v.co).x * sign > 0]
        edges = {e for v in island for e in v.link_edges}
        faces = {f for v in island for f in v.link_faces}
        local_origin = mw.inverted() @ origin
        local_axis = (mw.inverted().to_3x3() @ axis).normalized()
        bmesh.ops.bisect_plane(bm, geom=island + list(edges) + list(faces), dist=1e-5,
                               plane_co=local_origin, plane_no=local_axis, clear_inner=True, clear_outer=False)
    # 3) Cap the two shoulder openings.
    boundary = [e for e in bm.edges if e.is_boundary]
    caps = bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    for poly in body.data.polygons:
        poly.use_smooth = True

    body.name = 'Arms'
    body.data.name = 'Arms'

    # Export only rig + arms with the team's Unity settings, no animation baked.
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.data.pose_position = 'POSE'
    OUT_FBX.parent.mkdir(parents=True, exist_ok=True)
    kwargs = FBX_KW.copy()
    kwargs.update(bake_anim=False, embed_textures=False, path_mode='AUTO')
    bpy.ops.export_scene.fbx(filepath=str(OUT_FBX), **kwargs)

    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND), check_existing=False)

    xs = [(mw @ v.co).x for v in body.data.vertices]
    zs = [(mw @ v.co).z for v in body.data.vertices]
    report = {
        'vertices_before': before, 'vertices_after': len(body.data.vertices),
        'cap_faces': len(caps.get('faces', [])), 'boundary_edges': len(boundary),
        'x_range': [round(min(xs), 3), round(max(xs), 3)], 'z_range': [round(min(zs), 3), round(max(zs), 3)],
        'vertex_groups': [g.name for g in body.vertex_groups],
        'materials': [m.name for m in body.data.materials if m],
        'fbx': str(OUT_FBX.relative_to(ROOT)), 'fbx_bytes': OUT_FBX.stat().st_size,
        'blend': str(OUT_BLEND.relative_to(ROOT)),
    }
    REPORT.write_text(json.dumps(report, indent=1))
    print('REPORT', json.dumps(report))


main()
