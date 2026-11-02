# Pulsante su asta per il temporal binding: il tasto sta sospeso all'altezza delle dita
# con il gomito appoggiato al tavolo e si schiaccia con un pinch (un dito sopra il tasto,
# l'altro sotto il piattello). Il pistone della condizione Esterno è montato sulla stessa asta.
# Da riga di comando:  blender -b --python Blender/costruisci_pulsante_asta_tb.py
# Esporta in Assets/MODELS/TemporalBinding/Postazione_TB/:
#   Pulsante_Asta.fbx  origine = centro della base sul tavolo, colonna sull'origine, braccio verso -Y
#                      (verso il partecipante). "Testa" (empty) = piano superiore del piattello
#                      sull'asse della colonna: in Unity sale/scende per regolare l'altezza.
#                      "Tasto" ha l'origine alla sua base: si abbassa lungo -Z (Blender) / -Y (Unity).
#   Pistone_Asta.fbx   origine = stesso punto della Testa: in Unity diventa figlio della Testa.
#                      "Asta" ha l'origine in cima e scende per toccare il tasto.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

try:
    PROGETTO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    PROGETTO = r"D:\unity projects\Esperimenti 2027"
OUT = os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\Postazione_TB")

# ---- misure (metri)
SBALZO = 0.085          # colonna -> centro del tasto
ALTEZZA = 0.22          # altezza di default del piattello sopra il tavolo (regolabile in Unity)
Z_FISSA = 0.070         # cima del tubo fisso
CORSA_REG = 0.30        # lunghezza del tubo interno sotto la testa: altezza regolabile ~0.09-0.35
R_PIATTO = 0.021
SP_PIATTO = 0.006
R_TASTO = 0.0135
H_TASTO = 0.008
Z_PUNTA_RIPOSO = 0.034  # punta del pistone a riposo sopra il piattello (tasto alto 8 mm)

def pulisci():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials):
        for x in list(coll):
            if x.users == 0: coll.remove(x)

def materiale(nome, colore, metallo=0.0, rugosita=0.5, emissione=None, forza=0.0):
    m = bpy.data.materials.get(nome) or bpy.data.materials.new(nome)
    m.use_nodes = True
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

def vuoto(nome, parent=None, loc=(0, 0, 0)):
    o = bpy.data.objects.new(nome, None); bpy.context.scene.collection.objects.link(o)
    o.empty_display_size = 0.02
    if parent: o.parent = parent
    o.location = loc
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

def tornio(profilo, cx=0.0, cy=0.0, segmenti=48, chiudi_sopra=False, chiudi_sotto=False):
    """Superficie di rivoluzione attorno all'asse Z passante per (cx, cy) da una lista di (raggio, z)."""
    bm = bmesh.new(); anelli = []
    for r, z in profilo:
        anelli.append([bm.verts.new((cx + r * math.cos(2 * math.pi * i / segmenti), cy + r * math.sin(2 * math.pi * i / segmenti), z))
                       for i in range(segmenti)])
    for a, b in zip(anelli, anelli[1:]):
        for i in range(segmenti):
            j = (i + 1) % segmenti
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if chiudi_sotto and profilo[0][0] > 0: bm.faces.new(list(reversed(anelli[0])))
    if chiudi_sopra and profilo[-1][0] > 0: bm.faces.new(anelli[-1])
    # gli anelli a raggio zero (poli) collassano in un vertice: niente quad degeneri
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm

def esporta(nome, radice):
    for o in bpy.context.scene.objects: o.select_set(False)
    radice.select_set(True)
    for c in radice.children_recursive: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, nome), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE')

os.makedirs(OUT, exist_ok=True)

# ======================================================================== pulsante su asta
pulisci()
grafite = materiale("Asta_Base", (0.08, 0.085, 0.09), 0.3, 0.5)
alluminio = materiale("Asta_Alluminio", (0.75, 0.76, 0.78), 1.0, 0.3)
acciaio = materiale("Pulsante_Collare", (0.7, 0.7, 0.72), 1.0, 0.25)
grigio = materiale("Pulsante_Base", (0.12, 0.12, 0.13), 0.2, 0.5)
gomma = materiale("Asta_Gomma", (0.04, 0.04, 0.04), 0.0, 0.85)
rosso = materiale("Pulsante_Tasto", (0.75, 0.05, 0.04), 0.0, 0.35)

radice = vuoto("Pulsante_Asta")
oggetto("Base", scatola(0, 0, 0, 0.10, 0.10, 0.016, 0.004), grafite, radice)
oggetto("Piedini", scatola(0, 0, -0.0015, 0.095, 0.095, 0.0015, 0.0005), gomma, radice)
oggetto("Colonna_Fissa", cilindro(0, 0, 0.016, Z_FISSA, 0.010), alluminio, radice)
oggetto("Ghiera", tornio([(0.0076, Z_FISSA - 0.010), (0.0135, Z_FISSA - 0.010), (0.0135, Z_FISSA), (0.0076, Z_FISSA)]), grafite, radice)
# pomello di bloccaggio della ghiera, sul lato opposto al partecipante
bm = cilindro(0, 0, -0.004, 0.004, 0.0045, 20)
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0.017, Z_FISSA - 0.005)) @ Matrix.Rotation(math.radians(90), 4, "X"), verts=bm.verts)
oggetto("Pomello", bm, grafite, radice)

testa = vuoto("Testa", radice, (0, 0, ALTEZZA))
# tubo interno che scorre nel fisso: sempre abbastanza lungo da entrarci per tutta la regolazione
oggetto("Colonna_Mobile", cilindro(0, 0, -CORSA_REG, 0.008, 0.0075), alluminio, testa)
oggetto("Morsetto", scatola(0, 0, -SP_PIATTO - 0.004, 0.022, 0.022, SP_PIATTO + 0.010, 0.003), grafite, testa)
oggetto("Braccio", scatola(0, -SBALZO / 2, -SP_PIATTO, 0.014, SBALZO, SP_PIATTO, 0.002), grafite, testa)
oggetto("Piattello", tornio([(0.0, -SP_PIATTO), (R_PIATTO - 0.0015, -SP_PIATTO), (R_PIATTO, -SP_PIATTO + 0.0015),
                             (R_PIATTO, -0.0015), (R_PIATTO - 0.0015, 0.0), (0.0, 0.0)], 0, -SBALZO), grigio, testa)
# appoggio in gomma sotto il piattello: dice dove va il dito di sotto
oggetto("Appoggio", tornio([(0.0, -SP_PIATTO - 0.0012), (0.015, -SP_PIATTO - 0.0012), (0.016, -SP_PIATTO - 0.0004),
                            (0.016, -SP_PIATTO + 0.0002)], 0, -SBALZO, chiudi_sotto=False), gomma, testa)
oggetto("Collare", tornio([(R_TASTO + 0.0008, 0.0), (R_TASTO + 0.0035, 0.0), (R_TASTO + 0.0035, 0.0025), (R_TASTO + 0.0008, 0.0025)],
                          0, -SBALZO, chiudi_sotto=True), acciaio, testa)
prof = [(0.0, 0.0), (R_TASTO, 0.0), (R_TASTO, H_TASTO - 0.0025)]
for k in range(1, 9):
    a = k / 8 * math.pi / 2
    prof.append((R_TASTO * math.cos(a) * 0.97 + 0.0004, H_TASTO - 0.0025 + 0.0025 * math.sin(a)))
prof.append((0.0, H_TASTO))
tasto = oggetto("Tasto", tornio(prof, chiudi_sotto=True), rosso, testa)
tasto.location = (0, -SBALZO, 0)

esporta("Pulsante_Asta.fbx", radice)
print("Pulsante_Asta: parti", sorted(o.name for o in radice.children_recursive))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, r"Blender\Pulsante_Asta_TB.blend"))

# ======================================================================== pistone sull'asta
pulisci()
grafite = materiale("Pistone_Base", (0.08, 0.085, 0.09), 0.3, 0.5)
alluminio = materiale("Pistone_Alluminio", (0.75, 0.76, 0.78), 1.0, 0.3)
nero = materiale("Pistone_Corpo", (0.02, 0.02, 0.02), 0.2, 0.35)
acciaio = materiale("Pistone_Asta", (0.85, 0.85, 0.87), 1.0, 0.15)
gomma = materiale("Pistone_Gomma", (0.04, 0.04, 0.04), 0.0, 0.8)
led = materiale("Pistone_LED", (0.1, 0.9, 0.3), 0.0, 0.3, (0.1, 1.0, 0.3), 4.0)

Z_BRACCIO = 0.072
Z_CORPO = (0.046, 0.080)
radice = vuoto("Pistone_Asta")
# prolunga della colonna sopra il morsetto del pulsante + morsetto del pistone
oggetto("Prolunga", cilindro(0, 0, 0.004, Z_BRACCIO + 0.014, 0.0075), alluminio, radice)
oggetto("Morsetto", scatola(0, 0, Z_BRACCIO - 0.012, 0.022, 0.022, 0.026, 0.003), grafite, radice)
oggetto("Braccio", scatola(0, -SBALZO / 2, Z_BRACCIO - 0.002, 0.018, SBALZO + 0.010, 0.012, 0.002), alluminio, radice)
oggetto("Corpo", cilindro(0, -SBALZO, Z_CORPO[0], Z_CORPO[1], 0.0145), nero, radice)
z_top = Z_CORPO[1] - 0.004
asta = oggetto("Asta", cilindro(0, 0, Z_PUNTA_RIPOSO + 0.005 - z_top, 0, 0.004), acciaio, radice)
asta.location = (0, -SBALZO, z_top)
oggetto("Punta", cilindro(0, 0, Z_PUNTA_RIPOSO - z_top, Z_PUNTA_RIPOSO + 0.007 - z_top, 0.0075), gomma, asta)
bm = cilindro(0, 0, -0.0015, 0.0015, 0.003, 16)
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, -SBALZO - 0.0145, Z_CORPO[1] - 0.008)) @ Matrix.Rotation(math.radians(90), 4, "X"), verts=bm.verts)
oggetto("LED", bm, led, radice)

esporta("Pistone_Asta.fbx", radice)
print("Pistone_Asta: parti", sorted(o.name for o in radice.children_recursive), "punta a riposo", Z_PUNTA_RIPOSO, "m sopra il piattello")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, r"Blender\Pistone_Asta_TB.blend"))
