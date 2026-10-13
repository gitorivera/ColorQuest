import bpy, os, math
from mathutils import Vector

OUT = r"C:\Users\gitor\Documents\Unity\ColorQuest\BlenderAssets"
TEX = os.path.join(OUT, "Textures")
RADIUS = 0.4809
NAMES = ["01_Estrella", "02_Corazon", "03_CaritaFeliz", "04_Sandia", "05_LunaEstrellas",
         "06_Pez", "07_Flor", "08_Monstruo", "09_Arcoiris", "10_Panda",
         "11_Helado", "12_Mariposa", "13_Cohete", "14_Gatito", "15_Rana"]

if "ColorQuest_Esferas" in bpy.data.scenes:
    sc = bpy.data.scenes["ColorQuest_Esferas"]
    for o in list(sc.objects): bpy.data.objects.remove(o, do_unlink=True)
else:
    sc = bpy.data.scenes.new("ColorQuest_Esferas")
bpy.context.window.scene = sc
col = sc.collection

for i, n in enumerate(NAMES):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=RADIUS)
    o = bpy.context.object
    o.name = f"Esfera_{n}"; o.data.name = f"Esfera_{n}"; me = o.data
    bpy.ops.object.shade_smooth()
    o.location = ((i % 5 - 2) * 1.25 * 0 , (i % 5 - 2) * 1.25, (1.4, 0.0, -1.4)[i // 5])
    # material
    mname = f"Mat_Esfera_{n}"
    mat = bpy.data.materials.get(mname) or bpy.data.materials.new(mname)
    mat.use_nodes = True
    nt = mat.node_tree
    for nd in list(nt.nodes): nt.nodes.remove(nd)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage")
    img = bpy.data.images.load(os.path.join(TEX, f"Esfera_{n}.png"), check_existing=True)
    img.reload(); img.colorspace_settings.name = "sRGB"
    tex.image = img
    bsdf.inputs["Roughness"].default_value = 0.28
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    me.materials.append(mat)

# preview: camara en +X mirando -X (ve el motivo 1) y luz
def mk_cam(name, loc):
    cam = bpy.data.objects.get(name) or bpy.data.objects.new(name, bpy.data.cameras.new(name))
    if cam.name not in sc.objects: col.objects.link(cam)
    cam.location = loc
    d = Vector((0, 0, 0)) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 7.2
    return cam
cam1 = mk_cam("Cam_Frente", (10, 0, 0)); cam2 = mk_cam("Cam_Dorso", (-10, 0, 0))
sun = bpy.data.objects.new("Sol", bpy.data.lights.new("Sol", 'SUN')); col.objects.link(sun)
sun.data.energy = 3.0; sun.rotation_euler = (math.radians(40), math.radians(20), math.radians(60))
fill = bpy.data.objects.new("Relleno", bpy.data.lights.new("Relleno", 'SUN')); col.objects.link(fill)
fill.data.energy = 1.2; fill.rotation_euler = (math.radians(120), 0, math.radians(-120))
w = bpy.data.worlds.get("MundoPreview") or bpy.data.worlds.new("MundoPreview")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.82, 0.88, 0.95, 1)
sc.world = w
sc.render.engine = 'BLENDER_EEVEE'
sc.view_settings.view_transform = 'Standard'
sc.render.resolution_x = 1800; sc.render.resolution_y = 1100
sc.render.image_settings.file_format = 'PNG'
sc.camera = cam1
os.makedirs(os.path.join(OUT, "Previews"), exist_ok=True)
sc.render.filepath = os.path.join(OUT, "Previews", "preview_frente.png")
bpy.ops.render.render(write_still=True)
sc.camera = cam2
sc.render.filepath = os.path.join(OUT, "Previews", "preview_dorso.png")
bpy.ops.render.render(write_still=True)
print("ok")
