# Pulsante e lampadina del paradigma di temporal binding (misure reali, metri).
# Da eseguire in Blender 4.5:  exec(open(r"...\Blender\costruisci_oggetti_tb.py").read())
# Esporta Assets/MODELS/TemporalBinding/Postazione_TB/Lampadina.fbx e Pulsante.fbx.
# Ogni parte è un oggetto separato: in Unity il filamento diventa emissivo e il tasto si abbassa.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

PROGETTO = r"D:\unity projects\Esperimenti 2027"
OUT = os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\Postazione_TB")

def pulisci():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for x in list(coll):
            if x.users == 0: coll.remove(x)

def materiale(nome, colore, metallo=0.0, rugosita=0.5, alpha=1.0, emissione=None):
    m = bpy.data.materials.get(nome) or bpy.data.materials.new(nome)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*colore, 1)
    bsdf.inputs["Metallic"].default_value = metallo
    bsdf.inputs["Roughness"].default_value = rugosita
    bsdf.inputs["Alpha"].default_value = alpha
    if emissione:
        bsdf.inputs["Emission Color"].default_value = (*emissione, 1)
    return m

def oggetto_da_bm(nome, bm, mat, parent=None):
    me = bpy.data.meshes.new(nome); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nome, me); bpy.context.scene.collection.objects.link(o)
    me.materials.append(mat)
    for p in me.polygons: p.use_smooth = True
    if parent: o.parent = parent
    return o

def tornio(profilo, segmenti=48, chiudi_sopra=False, chiudi_sotto=False):
    """Superficie di rivoluzione attorno a Z da una lista di (raggio, z)."""
    bm = bmesh.new()
    anelli = []
    for r, z in profilo:
        anelli.append([bm.verts.new((r * math.cos(2 * math.pi * i / segmenti), r * math.sin(2 * math.pi * i / segmenti), z)) for i in range(segmenti)])
    for a, b in zip(anelli, anelli[1:]):
        for i in range(segmenti):
            j = (i + 1) % segmenti
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if chiudi_sotto: bm.faces.new(list(reversed(anelli[0])))
    if chiudi_sopra: bm.faces.new(anelli[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm

def tubo_lungo(punti, raggio, lati=8):
    """Tubo sottile lungo una polilinea (filo, filamento)."""
    bm = bmesh.new(); anelli = []
    for k, p in enumerate(punti):
        t = (punti[min(k + 1, len(punti) - 1)] - punti[max(k - 1, 0)]).normalized()
        u = t.orthogonal().normalized(); v = t.cross(u)
        anelli.append([bm.verts.new(p + (u * math.cos(2 * math.pi * i / lati) + v * math.sin(2 * math.pi * i / lati)) * raggio) for i in range(lati)])
    for a, b in zip(anelli, anelli[1:]):
        for i in range(lati):
            j = (i + 1) % lati
            bm.faces.new((a[i], a[j], b[j], b[i]))
    return bm

# ======================================================================== lampadina
pulisci()
vetro = materiale("Vetro", (0.95, 0.95, 0.92), 0.0, 0.05, 0.25)
filamento_m = materiale("Filamento", (0.25, 0.22, 0.2), 0.6, 0.4, emissione=(1.0, 0.55, 0.2))
ottone = materiale("Ottone", (0.78, 0.62, 0.35), 1.0, 0.3)
nero = materiale("Plastica_Nera", (0.03, 0.03, 0.03), 0.0, 0.45)
vetro_stelo = materiale("Vetro_Stelo", (0.9, 0.95, 0.95), 0.0, 0.1, 0.5)

radice = bpy.data.objects.new("Lampadina", None); bpy.context.scene.collection.objects.link(radice)

# base d'appoggio e portalampada (la lampadina è verticale sul tavolo)
Z0 = 0.0
base = oggetto_da_bm("Base", tornio([(0.0, Z0), (0.05, Z0), (0.052, Z0 + 0.004), (0.05, Z0 + 0.014), (0.02, Z0 + 0.016), (0.0, Z0 + 0.016)], chiudi_sotto=False), nero, radice)
porta = oggetto_da_bm("Portalampada", tornio([(0.016, 0.016), (0.019, 0.016), (0.019, 0.05), (0.0155, 0.052)]), nero, radice)

# attacco E27 filettato (raggio modulato a spirale)
prof = []
for k in range(41):
    z = 0.046 + k * 0.025 / 40
    prof.append((0.0132 + 0.0007 * math.sin(k / 40 * 2 * math.pi * 6), z))
attacco = oggetto_da_bm("Attacco", tornio([(0.0, 0.044), (0.006, 0.044)] + prof + [(0.0135, 0.073)]), ottone, radice)

# bulbo A60: collo + sfera (diametro 6 cm), altezza totale ~11 cm
prof = [(0.0135, 0.073), (0.015, 0.083)]
cz, R = 0.118, 0.030
for k in range(1, 31):
    a = -math.pi / 2 + math.radians(35) + k * (math.pi / 2 + math.radians(55)) / 30
    prof.append((R * math.cos(a), cz + R * math.sin(a)))
prof.append((0.0, cz + R))
bulbo = oggetto_da_bm("Bulbo_Vetro", tornio(prof), vetro, radice)

# stelo di vetro e supporti del filamento
stelo = oggetto_da_bm("Stelo", tornio([(0.004, 0.073), (0.0045, 0.09), (0.002, 0.105), (0.0, 0.106)]), vetro_stelo, radice)
zf = 0.118
sx, dx = Vector((-0.009, 0, zf)), Vector((0.009, 0, zf))
supporti = [tubo_lungo([Vector((-0.002, 0, 0.1)), Vector((-0.006, 0, 0.11)), sx], 0.00025),
            tubo_lungo([Vector((0.002, 0, 0.1)), Vector((0.006, 0, 0.11)), dx], 0.00025)]
bm = bmesh.new()
for s in supporti:
    me = bpy.data.meshes.new("tmp"); s.to_mesh(me); bm.from_mesh(me); bpy.data.meshes.remove(me); s.free()
oggetto_da_bm("Supporti", bm, materiale("Filo", (0.6, 0.6, 0.6), 1.0, 0.35), radice)

# filamento: spirale tesa tra i due supporti, leggermente incurvata
punti = []
N, spire, rc = 600, 22, 0.0009
for i in range(N + 1):
    t = i / N
    c = sx.lerp(dx, t) + Vector((0, 0, 0.003 * math.sin(math.pi * t)))
    ang = t * spire * 2 * math.pi
    punti.append(c + Vector((0, rc * math.cos(ang), rc * math.sin(ang))))
oggetto_da_bm("Filamento", tubo_lungo(punti, 0.00018, 6), filamento_m, radice)

os.makedirs(OUT, exist_ok=True)
def esporta(nome, radice):
    for o in bpy.context.scene.objects: o.select_set(False)
    radice.select_set(True)
    for c in radice.children_recursive: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, nome), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE')
esporta("Lampadina.fbx", radice)
print("Lampadina: altezza", round(cz + R, 3), "m, parti:", [c.name for c in radice.children])

# ======================================================================== pulsante
pulisci()
grigio = materiale("Pulsante_Base", (0.12, 0.12, 0.13), 0.2, 0.5)
acciaio = materiale("Pulsante_Collare", (0.7, 0.7, 0.72), 1.0, 0.25)
rosso = materiale("Pulsante_Tasto", (0.75, 0.05, 0.04), 0.0, 0.35)
radice = bpy.data.objects.new("Pulsante", None); bpy.context.scene.collection.objects.link(radice)

bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
for v in bm.verts: v.co = Vector((v.co.x * 0.07, v.co.y * 0.07, (v.co.z + 0.5) * 0.025))
bmesh.ops.bevel(bm, geom=bm.edges[:], offset=0.004, segments=4, affect='EDGES')
oggetto_da_bm("Scatola", bm, grigio, radice)
oggetto_da_bm("Collare", tornio([(0.0185, 0.025), (0.023, 0.025), (0.023, 0.029), (0.0185, 0.029)], chiudi_sotto=True), acciaio, radice)
# tasto: origine alla sua base (z=0 locale) -> in Unity si abbassa spostandolo in giù
prof = [(0.0, 0.0), (0.017, 0.0), (0.0172, 0.009)]
for k in range(1, 9):
    a = k / 8 * math.pi / 2
    prof.append((0.0172 * math.cos(a) * 0.98 + 0.0003, 0.009 + 0.003 * math.sin(a)))
prof.append((0.0, 0.012))
tasto = oggetto_da_bm("Tasto", tornio(prof, chiudi_sotto=True), rosso, radice)
tasto.location = (0, 0, 0.025)
esporta("Pulsante.fbx", radice)
print("Pulsante: parti:", [c.name for c in radice.children])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, r"Blender\Postazione_TB.blend"))
