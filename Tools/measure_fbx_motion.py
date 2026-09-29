import sys

import bpy

fbx_path = sys.argv[-1]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=fbx_path)

exec(compile(open("Tools/measure_breathing_motion.py", "rb").read(), "Tools/measure_breathing_motion.py", "exec"))
