import sys

import bpy

output_path = sys.argv[-1]

scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = 60
scene.frame_step = 2
scene.render.fps = 30

exec(compile(open("Tools/render_blender_preview.py", "rb").read(), "Tools/render_blender_preview.py", "exec"))

scene.frame_start = 1
scene.frame_end = 60
scene.frame_step = 2
scene.render.filepath = output_path
scene.render.image_settings.file_format = "FFMPEG"
scene.render.ffmpeg.format = "MPEG4"
scene.render.ffmpeg.codec = "H264"
scene.render.ffmpeg.constant_rate_factor = "MEDIUM"

bpy.ops.render.render(animation=True)
print("RENDERED_MP4", output_path)
