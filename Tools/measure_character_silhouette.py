import bpy, sys, json
argv = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=argv[0])
body = bpy.data.objects["Body"]
SCALE = 0.85
arm_kw = ("Shoulder","UpperArm","Arm","Hand","Finger")
gi_arm = set()
for g in body.vertex_groups:
    n = g.name
    if any(n.startswith(k) or n.startswith("Volume_"+k) for k in arm_kw):
        gi_arm.add(g.index)
bands = {}
for v in body.data.vertices:
    dom, domw = None, -1.0
    for g in v.groups:
        if g.weight > domw:
            domw, dom = g.weight, g.group
    if dom in gi_arm:
        continue
    w = body.matrix_world @ v.co
    b = round((w.z*SCALE)//0.1*0.1, 1)
    d = bands.setdefault(b, {"maxabsx":0.0,"maxy":-9.0,"miny":9.0,"n":0})
    d["maxabsx"] = max(d["maxabsx"], abs(w.x)*SCALE)
    d["maxy"] = max(d["maxy"], w.y*SCALE); d["miny"] = min(d["miny"], w.y*SCALE)
    d["n"] += 1
out = {}
for k in sorted(bands):
    d = bands[k]
    halfdepth = max(abs(d["maxy"]), abs(d["miny"]))
    out[f"{k:.1f}"] = {"halfW_x": round(d["maxabsx"],3), "fwd(-y)": round(-d["miny"],3),
                       "back(+y)": round(d["maxy"],3), "circumradius": round(max(d["maxabsx"],halfdepth),3), "n": d["n"]}
print("JSON_START"); print(json.dumps(out, indent=0)); print("JSON_END")
