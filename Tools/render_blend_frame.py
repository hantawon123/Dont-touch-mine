import sys

import bpy

frame = int(sys.argv[-2])
output_path = sys.argv[-1]

bpy.context.scene.frame_set(frame)
bpy.context.view_layer.update()

exec(compile(open("Tools/render_blender_preview.py", "rb").read(), "Tools/render_blender_preview.py", "exec"))
