# Agente meccanico della condizione "Esterno": un attuatore a solenoide su un braccetto,
# che preme il pulsante al posto del partecipante (primo evento visibile, come nelle altre condizioni).
# Da riga di comando:  blender -b --python Blender/costruisci_pistone_tb.py
# Esporta Assets/MODELS/TemporalBinding/Postazione_TB/Pistone.fbx (metri).
#
# Sistema (Blender, Z in alto): origine = centro della base sul tavolo, colonna sull'origine,
# braccio verso -Y, asta verticale a 10 cm dalla colonna. L'oggetto "Asta" ha l'origine in cima:
# in Unity scende lungo -Y locale per premere il tasto.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

try:
    PROGETTO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    PROGETTO = r"D:\unity projects\Esperimenti 2027"
OUT = os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\Postazione_TB")

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials):
    for x in list(coll):
        if x.users == 0: coll.remove(x)

def materiale(nome, colore, metallo=0.0, rugosita=0.5, emissione=None, forza=0.0):
    m = bpy.data.materials.new(nome); m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*colore, 1)
    bsdf.inputs["Metallic"].default_value = metallo
    bsdf.inputs["Roughness"].default_value = rugosita
    if emissione:
        bsdf.inputs["Emission Color"].default_value = (*emissione, 1)
        bsdf.inputs["Emission Strength"].default_value = forza
    return m

def oggetto(nome, bm, mat, parent=None, smooth=True):
    me = bpy.data.meshes.new(nome); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nome, me); bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat)
    for p in me.polygons: p.use_smooth = smooth
    if parent: o.parent = parent
    return o

def scatola(cx, cy, z0, sx, sy, sz, smusso=0.003):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((cx + v.co.x * sx, cy + v.co.y * sy, z0 + (v.co.z + 0.5) * sz))
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=smusso, segments=3, affect='EDGES')
    return bm

def cilindro(cx, cy, z0, z1, r, seg=40):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=z1 - z0,
                          matrix=Matrix.Translation((cx, cy, (z0 + z1) / 2)))
    return bm

grafite = materiale("Pistone_Base", (0.08, 0.085, 0.09), 0.3, 0.5)
alluminio = materiale("Pistone_Alluminio", (0.75, 0.76, 0.78), 1.0, 0.3)
nero = materiale("Pistone_Corpo", (0.02, 0.02, 0.02), 0.2, 0.35)
acciaio = materiale("Pistone_Asta", (0.85, 0.85, 0.87), 1.0, 0.15)
gomma = materiale("Pistone_Gomma", (0.04, 0.04, 0.04), 0.0, 0.8)
led = materiale("Pistone_LED", (0.1, 0.9, 0.3), 0.0, 0.3, (0.1, 1.0, 0.3), 4.0)

SBALZO = 0.10          # distanza colonna -> asta
Z_BRACCIO = 0.165
Z_CORPO = (0.105, 0.150)
Z_PUNTA_RIPOSO = 0.075

radice = bpy.data.objects.new("Pistone", None); bpy.context.scene.collection.objects.link(radice)
oggetto("Base", scatola(0, 0, 0, 0.09, 0.09, 0.028, 0.004), grafite, radice)
oggetto("Colonna", cilindro(0, 0, 0.028, Z_BRACCIO + 0.012, 0.012), alluminio, radice)
oggetto("Braccio", scatola(0, -SBALZO / 2, Z_BRACCIO - 0.010, 0.026, SBALZO + 0.026, 0.020, 0.003), alluminio, radice)
oggetto("Corpo", cilindro(0, -SBALZO, Z_CORPO[0], Z_CORPO[1], 0.017), nero, radice)

# asta: origine in cima (dentro il corpo), scende di 'corsa' in Unity
z_top = Z_CORPO[1] - 0.005
asta = oggetto("Asta", cilindro(0, 0, Z_PUNTA_RIPOSO + 0.006 - z_top, 0, 0.0045), acciaio, radice)
asta.location = (0, -SBALZO, z_top)
punta = oggetto("Punta", cilindro(0, 0, Z_PUNTA_RIPOSO - z_top, Z_PUNTA_RIPOSO + 0.008 - z_top, 0.009), gomma, asta)
# spia di "acceso" sul fronte della base, verso il partecipante (-Y)
bm = cilindro(0, 0, -0.0015, 0.0015, 0.004, 16)
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, -0.0455, 0.014)) @ Matrix.Rotation(math.radians(90), 4, "X"), verts=bm.verts)
oggetto("LED", bm, led, radice)

os.makedirs(OUT, exist_ok=True)
for o in bpy.context.scene.objects: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Pistone.fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, r"Blender\Pistone_TB.blend"))
print("Pistone esportato:", os.path.join(OUT, "Pistone.fbx"), "punta a riposo a", Z_PUNTA_RIPOSO, "m")
