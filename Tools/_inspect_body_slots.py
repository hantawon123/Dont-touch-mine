import bpy
p='C:/Users/SSAFY/.codex/visualizations/2026/09/14/01a09e00-d780-7f01-a5d2-f47dc854742f/current-bear-all-actions/BasicPlayerCapsule_Bear_CrawlSplit.blend'
bpy.ops.wm.open_mainfile(filepath=p,use_scripts=False)
o=bpy.data.objects['Body']; from collections import Counter
print('slots',[(i,m.name if m else None) for i,m in enumerate(o.data.materials)])
print('poly',Counter(p.material_index for p in o.data.polygons))
