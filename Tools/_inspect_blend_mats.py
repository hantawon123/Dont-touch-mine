import bpy
p='C:/Users/SSAFY/.codex/visualizations/2026/09/14/01a09e00-d780-7f01-a5d2-f47dc854742f/current-bear-all-actions/BasicPlayerCapsule_Bear_CrawlSplit.blend'
bpy.ops.wm.open_mainfile(filepath=p,use_scripts=False)
for o in bpy.context.scene.objects:
 if o.type=='MESH': print(o.name,[(m.name if m else None) for m in o.data.materials],len(o.material_slots),flush=True)
