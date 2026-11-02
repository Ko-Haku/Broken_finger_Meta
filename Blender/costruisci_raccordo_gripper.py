# Raccordo metallico tra l'anello al polso dell'avatar (Anello_Polso) e la testa del gripper a ganasce
# del progetto Eseguito_Osservato, che pende verso il basso: flangia sull'anello, tubo che prosegue
# l'avambraccio, snodo trasversale (come un giunto di polso) e piastra sotto lo snodo dove si avvita il gripper.
# Da riga di comando:  blender -b --python Blender/costruisci_raccordo_gripper.py
# Esporta Assets/MODELS/EseguitoOsservato/Raccordo_Gripper.fbx.
#
# Sistema di riferimento (misurato in Unity sull'avatar in posa T, metri):
#   origine = centro della faccia dell'anello verso la mano, Z = asse dell'avambraccio (verso la mano),
#   Y = verso l'alto perpendicolare all'avambraccio. In Unity il pezzo si allinea con gli empty
#   M_O (origine), M_Z (sull'asse), M_Y (in alto) e il gripper si appoggia su M_Base.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

try:
    PROGETTO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    PROGETTO = r"D:\unity projects\Esperimenti 2027"
OUT = os.path.join(PROGETTO, r"Assets\MODELS\EseguitoOsservato")

# ---- misure (da Unity: anello raggio 23-28 mm, base del gripper 62.5 mm avanti l'anello, gripper largo ~77 mm)
R_FLANGIA, SP_FLANGIA = 0.027, 0.006
R_TUBO = 0.017
R_SNODO, SEMI_SNODO = 0.021, 0.027
# distanza lungo l'avambraccio dall'anello all'asse del gripper: lo snodo poggia subito sulla flangia
# (niente tratto di tubo scoperto, il tool resta corto; prima era 62.5 mm, la posizione originale della testa)
L_SNODO = SP_FLANGIA + R_SNODO + 0.004
PIASTRA = (0.054, 0.006, 0.050)   # larghezza (laterale), spessore, profondità (lungo l'avambraccio)

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials):
    for x in list(coll):
        if x.users == 0: coll.remove(x)

def materiale(nome, colore, metallo, rugosita):
    m = bpy.data.materials.new(nome); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*colore, 1); b.inputs["Metallic"].default_value = metallo
    b.inputs["Roughness"].default_value = rugosita
    return m

acciaio = materiale("Raccordo_Acciaio", (0.78, 0.79, 0.81), 1.0, 0.28)
scuro = materiale("Raccordo_Scuro", (0.18, 0.19, 0.2), 0.8, 0.4)

radice = bpy.data.objects.new("Raccordo_Gripper", None); bpy.context.scene.collection.objects.link(radice)

def oggetto(nome, bm, mat, smooth=True):
    me = bpy.data.meshes.new(nome); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nome, me); bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat); o.parent = radice
    for p in me.polygons: p.use_smooth = smooth
    # bordi vivi sulle facce piane
    o.data.set_sharp_from_angle(angle=math.radians(40)) if hasattr(o.data, "set_sharp_from_angle") else None
    return o

def cilindro(r, z0, z1, seg=48, matrice=Matrix()):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=z1 - z0,
                          matrix=matrice @ Matrix.Translation((0, 0, (z0 + z1) / 2)))
    return bm

def smussa(bm, offset=0.0012):
    bmesh.ops.bevel(bm, geom=[e for e in bm.edges if len(e.link_faces) == 2 and
                              e.calc_face_angle(0) > math.radians(60)], offset=offset, segments=2, affect='EDGES')
    return bm

# flangia sull'anello + tubo lungo l'avambraccio
oggetto("Flangia", smussa(cilindro(R_FLANGIA, 0.0, SP_FLANGIA)), acciaio)
oggetto("Tubo", smussa(cilindro(R_TUBO, SP_FLANGIA - 0.001, L_SNODO)), acciaio)
# ghiera tra tubo e snodo
oggetto("Ghiera", smussa(cilindro(R_TUBO + 0.003, L_SNODO - R_SNODO - 0.006, L_SNODO - R_SNODO + 0.002)), scuro)

# snodo trasversale: cilindro con asse laterale (X), centrato sull'asse del tubo
lat = Matrix.Translation((0, 0, L_SNODO)) @ Matrix.Rotation(math.radians(90), 4, 'Y')
oggetto("Snodo", smussa(cilindro(R_SNODO, -SEMI_SNODO, SEMI_SNODO, matrice=lat)), acciaio)
for s in (-1, 1):   # tappi scuri ai lati dello snodo
    oggetto(f"Tappo_{'S' if s < 0 else 'D'}", smussa(cilindro(R_SNODO * 0.62, s * SEMI_SNODO - 0.002, s * SEMI_SNODO + 0.002, matrice=lat)), scuro)

# piastra sotto lo snodo, dove si avvita la testa del gripper
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
y_piastra = -R_SNODO - PIASTRA[1] / 2 + 0.002
for v in bm.verts: v.co = Vector((v.co.x * PIASTRA[0], y_piastra + v.co.y * PIASTRA[1], L_SNODO + v.co.z * PIASTRA[2]))
oggetto("Piastra", smussa(bm, 0.0015), acciaio)
# collo tra snodo e piastra
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
for v in bm.verts: v.co = Vector((v.co.x * 0.03, -R_SNODO * 0.6 + v.co.y * R_SNODO * 0.8, L_SNODO + v.co.z * 0.03))
oggetto("Collo", smussa(bm), acciaio)

# viti: sei sulla flangia, quattro sulla piastra
bm = bmesh.new()
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.0028, radius2=0.0028, depth=0.002,
                          matrix=Matrix.Translation((math.cos(a) * (R_FLANGIA - 0.0045), math.sin(a) * (R_FLANGIA - 0.0045), SP_FLANGIA + 0.001)))
for sx in (-1, 1):
    for sz in (-1, 1):
        bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.0025, radius2=0.0025, depth=0.002,
                              matrix=Matrix.Translation((sx * (PIASTRA[0] / 2 - 0.006), y_piastra - PIASTRA[1] / 2 - 0.001, L_SNODO + sz * (PIASTRA[2] / 2 - 0.006)))
                              @ Matrix.Rotation(math.radians(90), 4, 'X'))
oggetto("Viti", bm, scuro, smooth=False)

# marcatori per l'allineamento in Unity
def marcatore(nome, p):
    e = bpy.data.objects.new(nome, None); bpy.context.scene.collection.objects.link(e)
    e.parent = radice; e.location = p; e.empty_display_size = 0.005
marcatore("M_O", (0, 0, 0)); marcatore("M_Z", (0, 0, 0.05)); marcatore("M_Y", (0, 0.05, 0))
marcatore("M_Base", (0, y_piastra - PIASTRA[1] / 2, L_SNODO))      # faccia inferiore della piastra

os.makedirs(OUT, exist_ok=True)
for o in bpy.context.scene.objects: o.select_set(False)
radice.select_set(True)
for c in radice.children_recursive: c.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Raccordo_Gripper.fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, r"Blender\Raccordo_Gripper.blend"))
print("Raccordo esportato, base del gripper a", round(y_piastra - PIASTRA[1] / 2, 4), "m sotto l'asse,", L_SNODO, "m avanti l'anello")
