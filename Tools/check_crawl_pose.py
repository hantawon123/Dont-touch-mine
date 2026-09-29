"""Render the two opposing crawl poses and check bone lengths/loop closure."""
import bpy, sys, math
from mathutils import Vector
from pathlib import Path
source, output = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=source, use_scripts=False)
s=bpy.context.scene
r=bpy.data.objects["DGN_Armature"]
s.render.engine="BLENDER_WORKBENCH"
s.display.shading.light="STUDIO"
s.display.shading.color_type="SINGLE"
s.display.shading.single_color=(0.65,0.72,0.8)
s.display.shading.show_shadows=True
s.display.shading.show_cavity=True
s.render.resolution_x=800
s.render.resolution_y=650
s.render.resolution_percentage=100
cam=bpy.data.objects.new("CrawlCheck",bpy.data.cameras.new("CrawlCheck"))
s.collection.objects.link(cam)
s.camera=cam
cam.data.type="ORTHO"
cam.data.ortho_scale=3.1
target=r.matrix_world @ Vector((0,0.5,0.15))
cam.location=r.matrix_world @ Vector((2.5,3.5,-3))
cam.rotation_euler=(target-cam.location).to_track_quat("-Z","Y").to_euler()
for frame in (1,10,28,37):
    s.frame_set(frame)
    bpy.context.view_layer.update()
    print("POSE",frame,[(n,tuple(round(v,3) for v in r.pose.bones[n].head)) for n in ("Hips","Leg.L","Leg.R","Foot.L","Foot.R")])
    for n in ("UpperLeg.L","UpperLeg.R","Leg.L","Leg.R"):
        b=r.pose.bones[n]
        assert abs((b.tail-b.head).length-b.bone.length)<0.0001, n
    for n in ('Leg.L', 'Leg.R'):
        q=r.pose.bones[n].rotation_quaternion
        assert abs(q.y)<0.0001 and abs(q.z)<0.0001, 'Knee must hinge on local X'
    if frame in (1,37):
        pose=[b.matrix.copy() for b in r.pose.bones]
        if frame==1: first=pose
        else: assert max(abs(a[i][j]-b[i][j]) for a,b in zip(first,pose) for i in range(4) for j in range(4))<0.0001
    if frame in (10,28):
        s.render.filepath=str(Path(output)/f"pose_{frame}.png")
        bpy.ops.render.render(write_still=True)
print("PASS rigid bone lengths and loop closure")
s.frame_set(10)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=source)
