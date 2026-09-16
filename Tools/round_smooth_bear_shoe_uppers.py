"""Round the existing shoe uppers and build a continuous padded ankle lip."""
import bpy, bmesh, math, json, hashlib, struct, sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'source/blender/characters/SmoothBear/Shoes'
COMPACT = '--compact' in sys.argv
LOW_TOP = '--low-top' in sys.argv or COMPACT
VARIANT = 'compact' if COMPACT else ('low-top' if LOW_TOP else 'rounded')
OUT = ROOT / 'artifacts/smooth-bear-unity/shoes' / VARIANT
OUT.mkdir(parents=True, exist_ok=True)
FILE = DEST / ('BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_LowTop.blend' if LOW_TOP else 'BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_Rounded.blend')
if COMPACT:
    FILE = DEST / 'BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_Compact.blend'
bpy.ops.wm.open_mainfile(filepath=str(DEST / 'BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes.blend'), use_scripts=False)
scene = bpy.context.scene
def fingerprint():
    return hashlib.sha256(b''.join(struct.pack('fff', *v.co) for v in bpy.data.objects['Body'].data.vertices)).hexdigest()
original = fingerprint()
# Outer sole -> convex vamp -> broad rolled crown -> inner ankle wall -> cavity floor.
# Close ring spacing around the crown gives a rounded fabric-like lip without a seam.
profile = [
    (.180,-.080,.143,.207,-.026), (.180,-.080,.162,.229,-.024),
    (.180,-.080,.170,.238,-.009), (.180,-.080,.173,.241,.016),
    (.180,-.080,.176,.244,.060), (.179,-.077,.177,.242,.102),
    (.179,-.071,.175,.228,.149), (.179,-.066,.173,.204,.193),
    (.180,-.065,.169,.182,.230), (.180,-.065,.164,.173,.251),
    (.180,-.065,.155,.164,.265), (.180,-.065,.145,.155,.267),
    (.180,-.065,.137,.148,.260), (.180,-.065,.134,.146,.248),
    (.180,-.065,.135,.148,.232), (.177,-.065,.139,.163,.173),
    (.177,-.069,.145,.197,.007), (.177,-.069,.132,.184,.006),
]
if LOW_TOP:
    # Lower ankle opening and a longer forefoot create a visible, gently rising vamp.
    # The toe rounds over at z=.10; the padded collar ends at z=.18.
    profile = [
        (.180,-.114,.143,.246,-.026), (.180,-.114,.162,.269,-.024),
        (.180,-.114,.170,.280,-.009), (.180,-.114,.173,.283,.016),
        (.180,-.112,.177,.286,.051), (.180,-.107,.178,.280,.081),
        (.180,-.094,.174,.261,.111), (.180,-.074,.168,.229,.137),
        (.180,-.054,.160,.192,.158), (.180,-.045,.153,.169,.174),
        (.180,-.043,.146,.158,.182), (.180,-.043,.139,.150,.180),
        (.180,-.043,.134,.145,.171), (.180,-.043,.134,.145,.156),
        (.180,-.050,.136,.156,.133), (.180,-.071,.143,.193,.078),
        (.177,-.096,.149,.239,.007), (.177,-.096,.134,.225,.006),
    ]
if COMPACT:
    # Shorten the toe-to-heel span while retaining the ankle fit and low silhouette.
    # Fuller mid-height toe sections and a gently raised front surface form the dome.
    profile = [
        (.180,-.077,.143,.204,-.026), (.180,-.077,.162,.226,-.024),
        (.180,-.077,.170,.234,-.009), (.180,-.077,.173,.237,.016),
        (.180,-.077,.177,.239,.051), (.180,-.076,.177,.230,.087),
        (.180,-.069,.170,.213,.120), (.180,-.058,.160,.188,.148),
        (.180,-.048,.153,.170,.165), (.180,-.043,.148,.160,.178),
        (.180,-.043,.143,.153,.182), (.180,-.043,.138,.148,.180),
        (.180,-.043,.134,.145,.171), (.180,-.043,.134,.145,.156),
        (.180,-.050,.136,.156,.125), (.180,-.064,.143,.177,.066),
        (.177,-.073,.149,.208,.007), (.177,-.073,.134,.195,.006),
    ]
N = 64
report = {'file':str(FILE), 'design':'Convex padded upper with continuous rolled ankle rim', 'mesh_validation':{}}
if LOW_TOP:
    report['design'] = 'Low collar, long rounded forefoot and clearly visible rising vamp'
    report['collar_height_reduction_percent'] = 29
if COMPACT:
    report['design'] = 'Compact low shoe with rounded toe and subtly domed vamp'
    report['length_reduction_vs_low_top_percent'] = round(100*(1-.478/.572),1)
shoes = []
for side, sign in [('L',1),('R',-1)]:
    obj = bpy.data.objects['Shoes.' + side]
    old_mesh = obj.data
    materials = list(old_mesh.materials)
    vertices = []
    for ring,(cx,cy,rx,ry,z) in enumerate(profile):
        for i in range(N):
            a = math.tau*i/N
            c,s = math.cos(a),math.sin(a)
            height = z
            if COMPACT:
                # Smooth localized fullness across the forefoot, tapering into the collar.
                bump = {5:.003,6:.014,7:.018,8:.006,9:-.004,10:-.007,11:-.007,12:-.005}.get(ring,0)
                height += bump*max(0,-s)**2
            vertices.append((sign*cx+rx*math.copysign(abs(c)**.88,c), cy+ry*math.copysign(abs(s)**.88,s), height))
    faces = [tuple(reversed(range(N)))]
    for j in range(len(profile)-1):
        for i in range(N):
            faces.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
    faces.append(tuple((len(profile)-1)*N+i for i in range(N)))
    mesh = bpy.data.meshes.new('PaddedRoundedShoe_' + side)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj.data = mesh
    for mat in materials: mesh.materials.append(mat)
    for poly in mesh.polygons:
        poly.use_smooth = True
        poly.material_index = 1 if poly.index <= N*2 else 0
    obj.vertex_groups.clear()
    obj.vertex_groups.new(name='Foot.'+side).add(list(range(len(vertices))),1,'REPLACE')
    obj['design'] = report['design']
    if old_mesh.users == 0: bpy.data.meshes.remove(old_mesh)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    assert all(e.is_manifold for e in bm.edges)
    volume = bm.calc_volume()
    assert volume > 0
    bm.free()
    report['mesh_validation'][obj.name] = {'manifold':True,'volume':volume,'vertices':len(vertices),'faces':len(faces),'foot_bone':'Foot.'+side}
    shoes.append(obj)
bpy.ops.object.select_all(action='DESELECT')
for obj in shoes:
    obj.hide_set(False)
    obj.select_set(True)
bpy.context.view_layer.objects.active = shoes[0]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.025)
bpy.ops.object.mode_set(mode='OBJECT')
for obj in shoes:
    assert len(obj.data.uv_layers) == 1
    report['mesh_validation'][obj.name]['uv_layers'] = 1
assert fingerprint() == original
report['body_geometry_unchanged'] = True
scene.render.threads_mode = 'FIXED'
scene.render.threads = 8
scene.cycles.samples = 24
cam = scene.camera
def render(name,pos,target,scale,w,h):
    cam.location = pos
    cam.rotation_euler = (Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale = scale
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.filepath = str(OUT / (name+'.png'))
    bpy.ops.render.render(write_still=True)
render('shoes-'+VARIANT+'-detail',(1.3,-2.7,1.0),(0,-.085,.10),1.02,1100,850)
render('shoes-'+VARIANT+'-side',(3,-.8,.65),(0,-.095,.12),1.02,1000,800)
cam.location = (1.3,-2.7,1.0)
cam.rotation_euler = (Vector((0,-.055,.12))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale = .96
scene.render.resolution_x = 1100
scene.render.resolution_y = 850
scene.render.filepath = str(OUT/('shoes-'+VARIANT+'-detail.png'))
scene['shoe_draft_note'] = report['design'] + '. Separate shoe objects, materials, UVs, and Foot weights retained.'
for text in bpy.data.texts: text.use_module = False
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
(OUT/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('ROUNDED_SHOES_VERIFIED '+str(FILE),flush=True)
