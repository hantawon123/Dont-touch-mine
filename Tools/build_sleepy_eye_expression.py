"""Separate half-lidded, unimpressed eyes with a printed rounded-square pupil."""
import bpy,bmesh,math,json,hashlib,struct,sys
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'source/blender/characters/SmoothBear/Expressions'
FIERCE_LIFT_MORE='--fierce-lift-more' in sys.argv
FIERCE_LIFT='--fierce-lift' in sys.argv or FIERCE_LIFT_MORE
FIERCE_REFINED='--fierce-refined' in sys.argv or FIERCE_LIFT
FIERCE='--fierce' in sys.argv or FIERCE_REFINED
REFINED='--refined' in sys.argv or FIERCE
LABEL='Fierce' if FIERCE else 'Sleepy'
OUT=ROOT/'artifacts/smooth-bear-unity/expressions'/('fierce' if FIERCE else ('sleepy-refined' if REFINED else 'sleepy'));OUT.mkdir(parents=True,exist_ok=True)
FILE=DEST/('BasicPlayerCapsule_Bear_FierceEyes.blend' if FIERCE else ('BasicPlayerCapsule_Bear_SleepyEyes_Volume.blend' if REFINED else 'BasicPlayerCapsule_Bear_SleepyEyes.blend'))
if FIERCE_REFINED:
    OUT=ROOT/'artifacts/smooth-bear-unity/expressions/fierce-refined';OUT.mkdir(parents=True,exist_ok=True)
    FILE=DEST/'BasicPlayerCapsule_Bear_FierceEyes_LargeIris.blend'
fierce_slope=9 if FIERCE_REFINED else 13
upper_offset=-.009 if FIERCE_REFINED else .004
if FIERCE_LIFT:
    OUT=ROOT/'artifacts/smooth-bear-unity/expressions/fierce-raised';OUT.mkdir(parents=True,exist_ok=True)
    FILE=DEST/'BasicPlayerCapsule_Bear_FierceEyes_LargeIris_Raised.blend'
    upper_offset=-.005
if FIERCE_LIFT_MORE:
    OUT=ROOT/'artifacts/smooth-bear-unity/expressions/fierce-raised-more';OUT.mkdir(parents=True,exist_ok=True)
    FILE=DEST/'BasicPlayerCapsule_Bear_FierceEyes_LargeIris_RaisedMore.blend'
    upper_offset=-.001
iris_scale=1.4 if FIERCE_REFINED else 1.
bpy.ops.wm.open_mainfile(filepath=str(DEST/'BasicPlayerCapsule_Bear_SparkleEyes_Printed.blend'),use_scripts=False)
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature'];rig.data.pose_position='REST';bpy.context.view_layer.update()
def fingerprint(obj):return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in obj.data.vertices)).hexdigest()
keep={n:fingerprint(bpy.data.objects[n]) for n in ['Body','Hood','Shoes.L','Shoes.R']}
old=bpy.data.collections['EXPRESSION - Sparkle Printed Liner']
for obj in list(old.objects):bpy.data.objects.remove(obj,do_unlink=True)
old.name='EXPRESSION - '+LABEL+' Eyelids';collection=old
def linear(h):
    a=np.array([int(h[i:i+2],16)/255 for i in (1,3,5)])
    return np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4)
N=1536;axis=(np.arange(N,dtype=np.float32)+.5)/N*2-1;x,z=np.meshgrid(axis,axis)
pixels=np.ones((N,N,4),dtype=np.float32);pixels[:,:,:3]=linear('#FAFAF5')
# Rounded rectangular pupils, partly concealed by the lowered upper lids.
px=x*.0937462;pz=z*.0958043-(.002 if FIERCE else .006)
corner=.021 if REFINED else .012
qx=np.abs(px)-(.034-corner);qz=np.abs(pz)-(.046-corner)
distance=np.sqrt(np.maximum(qx,0)**2+np.maximum(qz,0)**2)+np.minimum(np.maximum(qx,qz),0)-corner
if FIERCE:
    distance=(np.sqrt((px/(.0355*iris_scale))**2+(pz/(.037*iris_scale))**2)-1)*.0355*iris_scale
mask=np.clip(.5-distance*N/1.0,0,1)[...,None]
pixels[:,:,:3]=pixels[:,:,:3]*(1-mask)+linear('#15151B')*mask
if REFINED:
    shine_x=.021 if FIERCE_REFINED else .016
    shine_z=-.022 if FIERCE_REFINED else -.012
    shine=np.sqrt(((px-shine_x)/.0043)**2+((pz-shine_z)/.0048)**2)
    alpha=np.clip((1-shine)*N/10+.5,0,1)[...,None]*mask
    pixels[:,:,:3]=pixels[:,:,:3]*(1-alpha)+linear('#FFFDF4')*alpha
texture_name='SleepyEyes_RoundedGlint_BaseColor' if REFINED else 'SleepyEyes_Print_BaseColor'
if FIERCE:texture_name='FierceEyes_Print_BaseColor'
if FIERCE_REFINED:texture_name='FierceEyes_LargeIris_BaseColor'
image=bpy.data.images.new(texture_name,width=N,height=N,alpha=True)
image.pixels.foreach_set(pixels.ravel());image.update();image.filepath_raw=str(DEST/(texture_name+'.png'));image.file_format='PNG';image.save();image.pack()
skin=bpy.data.objects['Body'].data.materials[0].copy();skin.name='MAT_Eyelids_Skin'
skin['color_note']='Match the current body colour when customizing the body.'
created=[];eye_specs={}
for side in ['L','R']:
    white=bpy.data.objects['Eye_White_'+side]
    points=[white.matrix_world@v.co for v in white.data.vertices]
    lo=Vector(tuple(min(p[k] for p in points) for k in range(3)));hi=Vector(tuple(max(p[k] for p in points) for k in range(3)))
    center=(lo+hi)/2
    # Reuse the approved planar print UV and original eye surface.
    material=bpy.data.materials.new('MAT_Eye_'+LABEL+'_Print_'+side);material.use_nodes=True
    nodes=material.node_tree.nodes;links=material.node_tree.links;shader=nodes.get('Principled BSDF')
    tex=nodes.new('ShaderNodeTexImage');tex.image=image
    uv=nodes.new('ShaderNodeUVMap');uv.uv_map='SparklePrintUV';links.new(uv.outputs['UV'],tex.inputs['Vector']);links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    shader.inputs['Roughness'].default_value=.48
    white.data.materials.append(material);index=len(white.data.materials)-1
    for poly in white.data.polygons:
        poly.material_index=index if sum(points[i].y for i in poly.vertices)/len(poly.vertices)<center.y else 0
    white['expression']=LABEL+' printed pupil with lowered skin lids'
    lower_cut=center.z-(.046 if REFINED else .056)
    for kind,cut,upper in [('Upper',center.z+upper_offset,True),('Lower',lower_cut,False)]:
        mesh=bpy.data.meshes.new(f'{LABEL}_{kind}Lid_{side}')
        # Very close-fitting skin shell over the shared white eye; a genuine lowered lid.
        vertices=[center+(p-center)*1.018 for p in points]
        mesh.from_pydata(vertices,[],[tuple(p.vertices) for p in white.data.polygons]);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh)
        if REFINED:
            # A denser curved lid supports a smooth padded profile without moving its rim.
            bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True)
            radius=(hi-lo)*.5*1.018
            # Preserve the authored sclera shape: it is not a mathematical ellipsoid.
            # Midpoints stay on the original mesh faces rather than being reprojected.
        sign=1 if center.x>0 else -1
        slope=sign*math.tan(math.radians(fierce_slope)) if FIERCE and upper else 0
        if FIERCE and not upper:
            for vertex in bm.verts:vertex.co.z-=.006*((vertex.co.x-center.x)/radius.x)**2
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(center.x,0,cut),plane_no=(-slope,0,1),clear_inner=upper,clear_outer=not upper)
        if FIERCE and not upper:
            for vertex in bm.verts:vertex.co.z+=.006*((vertex.co.x-center.x)/radius.x)**2
        if REFINED:
            extreme=center.z+radius.z if upper else center.z-radius.z
            for vertex in bm.verts:
                d=vertex.co-center
                local_cut=cut+slope*d.x+(.006*(d.x/radius.x)**2 if FIERCE and not upper else 0)
                t=max(0,min(1,(vertex.co.z-local_cut)/(extreme-local_cut)))
                front=max(0,min(1,-d.y/radius.y))
                across=max(0,1-(d.x/radius.x)**2)
                # The rim remains seated on the eye; fullness peaks inside the skin patch.
                vertex.co.y-=.012*math.sin(math.pi*t)**.8*front*across
            bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(mesh);bm.free();mesh.update()
        assert len(mesh.polygons)>0
        obj=bpy.data.objects.new(f'Eye_{LABEL}_{kind}Lid_'+side,mesh);collection.objects.link(obj);mesh.materials.append(skin)
        for poly in mesh.polygons:poly.use_smooth=True
        solid=obj.modifiers.new('Soft eyelid edge','SOLIDIFY');solid.thickness=.0012;solid.offset=-1
        bevel=obj.modifiers.new('Rounded eyelid rim','BEVEL');bevel.width=.001;bevel.segments=3;bevel.limit_method='ANGLE'
        obj.vertex_groups.new(name='Head').add(list(range(len(mesh.vertices))),1,'REPLACE')
        arm=obj.modifiers.new('Follow Head','ARMATURE');arm.object=rig
        obj.parent=rig;obj.matrix_parent_inverse=rig.matrix_world.inverted();obj['expression']=LABEL;created.append(obj)
    eye_specs[side]={'upper_edge_z':center.z+upper_offset,'lower_edge_z':lower_cut,'visible_white_height':center.z+upper_offset-lower_cut,'pupil':'printed rounded rectangle','pupil_corner_radius':corner,'upper_right_glint':REFINED,'padded_lid_profile':REFINED,'eyelashes':0}
    if FIERCE:
        eye_specs[side].update({'upper_lid_slope_degrees':fierce_slope,'iris_diameter_scale':iris_scale,'inner_corner_lowered':True,'curved_lower_rim':True,'pupil':'printed round pupil','pupil_corner_radius':None})
rig.data.pose_position='POSE';bpy.context.view_layer.update()
assert all(fingerprint(bpy.data.objects[n])==v for n,v in keep.items())
scene.render.threads_mode='FIXED';scene.render.threads=8;scene.cycles.samples=24;cam=scene.camera
def render(name,pos,target,scale,w,h):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render(LABEL.lower()+'-eyes-front',(0,-5,1.88),(0,0,1.64),.99,1050,950)
if not FIERCE_LIFT:
    render(LABEL.lower()+'-eyes-angle',(1.8,-5,1.9),(0,0,1.64),1.05,1000,900)
bpy.ops.object.select_all(action='DESELECT')
for obj in created:obj.select_set(True)
bpy.context.view_layer.objects.active=created[0]
readme=bpy.data.texts.new('SLEEPY_EYES_README')
readme.write('Sleepy / Unimpressed expression\nSkin-coloured upper lids cover approximately the upper half of each eye; lower lids make a short rounded white opening.\nDark rounded rectangular pupils are printed on the original sclera using the packed SleepyEyes_Print_BaseColor image.\nEXPRESSION - Sleepy Eyelids contains editable upper/lower lids following Head.\nEyelid MAT_Eyelids_Skin should track the body colour in future customization.\nApproved sparkle expression file, compact shoes and body are unchanged.\nThis is a separate Blender draft.\n')
if REFINED:
    readme.write('Refined version: fuller upper/lower skin pads, lower edge raised by .010, pupil corner radius .021, small upper-right printed highlight.\nTexture: SleepyEyes_RoundedGlint_BaseColor (packed).\n')
if FIERCE:
    readme.name='FIERCE_EYES_README';readme.clear()
    readme.write('Fierce expression\nUpper lids slope down 13 degrees toward the nose; lower lids have a subtly curved opening.\nRound pupils and small highlights are printed directly on the shared sclera with packed FierceEyes_Print_BaseColor.\nApproved padded eyelid volume, eye spacing, body, hood, mouth and compact shoes are retained.\nEXPRESSION - Fierce Eyelids contains editable lids following Head.\nSeparate Blender expression; approved sparkle and sleepy files are unchanged.\n')
if FIERCE_REFINED:
    readme.clear()
    readme.write('Fierce expression / Large Iris revision\nUpper lid slope reduced from 13 to 9 degrees, edge lowered by .013.\nPrinted iris diameter enlarged 40 percent; small upper-right highlights remain visible beneath the lids.\nPacked texture: FierceEyes_LargeIris_BaseColor.\nEditable padded lids follow Head. Body, hood, mouth, lower lids and compact shoes retain their established design.\nPrevious approved expression files remain available. Blender-only draft.\n')
if FIERCE_LIFT:
    readme.write('Raised-lid revision: upper edge raised .004 from the Large Iris version, keeping its 9-degree slope and iris size.\n')
if FIERCE_LIFT_MORE:
    readme.write('RaisedMore: upper edge raised another .004 above Raised, total .008 above Large Iris. All other expression parameters unchanged.\n')
for t in bpy.data.texts:t.use_module=False
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
(OUT/'report.json').write_text(json.dumps({'file':str(FILE),'expression':LABEL,'eye_specs':eye_specs,'body_hood_shoes_unchanged':True,'pupil_is_surface_print':True,'eyelid_objects':[o.name for o in created]},indent=2),encoding='utf-8')
print('SLEEPY_EYES_VERIFIED '+str(FILE),flush=True)
