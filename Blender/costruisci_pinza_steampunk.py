# Nuovo tool: artiglio steampunk tagliato sotto l'ingranaggio intermedio (linea rossa).
# Tiene solo la gabbia a molla (riquadro rosso, model_15) e l'artiglio (mozzo + 2 dita).
# Da eseguire in Blender 4.5:  exec(open(r"...\Blender\costruisci_pinza_steampunk.py").read())
#
# Sistema del tool (Blender, Z = asse del braccio):
#   origine = occhiello della gabbia (innesto dentro l'anello cromato), gabbia lungo +Z,
#   mozzo in cima, dita con bisettrice lungo +Z, asse del mozzo = Y.
#   Dito_Sup / Dito_Inf: empty sul centro del mozzo, ruotando attorno a Y le dita si chiudono.
import bpy, glob, os, math
from mathutils import Vector, Matrix

PROGETTO = r"D:\unity projects\Esperimenti 2027"
SRC = os.path.join(PROGETTO, r"Blender\sorgenti\pinza_steampunk")
OUT = os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\Pinza_Steampunk")
SCALA = 0.0026                     # unità della sorgente -> metri (gabbia ~6 cm)
PARTI = {'model_15': 'Gabbia', 'model_32': 'Mozzo',
         'model_29': 'Sup_Prossimale', 'model_28': 'Sup_Distale',
         'model_24': 'Inf_Prossimale', 'model_23': 'Inf_Distale'}

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for x in list(coll):
        if x.users == 0: coll.remove(x)
for nome in PARTI:
    bpy.ops.wm.obj_import(filepath=os.path.join(SRC, nome + ".obj"))
ob = {PARTI[o.name]: o for o in bpy.context.scene.objects if o.name in PARTI}
for k, o in ob.items(): o.name = k

def punti(o): return [o.matrix_world @ v.co for v in o.data.vertices]
def centro(p): return sum(p, Vector()) / len(p)
def applica(o, M):
    o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)   # porta tutto in coordinate mondo
    o.data.transform(M)

# --- artiglio: mozzo nell'origine, bisettrice delle dita -> +Z (rotazione attorno a Y, asse del mozzo)
H = centro(punti(ob['Mozzo']))
punta_sup = max(punti(ob['Sup_Distale']), key=lambda q: (q - H).length)
punta_inf = max(punti(ob['Inf_Distale']), key=lambda q: (q - H).length)
bis = ((punta_sup - H).normalized() + (punta_inf - H).normalized())
bis = Vector((bis.x, 0, bis.z)).normalized()
ang = math.atan2(bis.x, bis.z)            # angolo da +Z verso +X
R = Matrix.Rotation(-ang, 4, 'Y')
for k in ('Mozzo', 'Sup_Prossimale', 'Sup_Distale', 'Inf_Prossimale', 'Inf_Distale'):
    applica(ob[k], R @ Matrix.Translation(-H))
raggio_mozzo = max((p).length for p in punti(ob['Mozzo']))

# --- gabbia: capovolta (occhiello verso il braccio), occhiello nell'origine, lungo +Z
g = ob['Gabbia']
gp = punti(g); gc = centro(gp); ztop = max(p.z for p in gp)
applica(g, Matrix.Rotation(math.pi, 4, 'X') @ Matrix.Translation(-Vector((gc.x, gc.y, ztop))))
L = max(p.z for p in punti(g))
# mozzo in cima allo stelo (lo stelo entra nel bordo del mozzo)
alza = Matrix.Translation((0, 0, L + raggio_mozzo * 0.55))
for k in ('Mozzo', 'Sup_Prossimale', 'Sup_Distale', 'Inf_Prossimale', 'Inf_Distale'):
    ob[k].data.transform(alza)

# --- i giunti tra prossimale e distale erano nel mucchio senza posizione: avvicina il distale
#     lungo l'asse del dito finché tocca il prossimale (dita continue)
for prox, dist in (('Sup_Prossimale', 'Sup_Distale'), ('Inf_Prossimale', 'Inf_Distale')):
    pp, dp = punti(ob[prox]), punti(ob[dist])
    asse = (centro(dp) - centro(pp)).normalized()
    gap = min(p.dot(asse) for p in dp) - max(p.dot(asse) for p in pp)
    ob[dist].data.transform(Matrix.Translation(-asse * (gap + 0.15)))
    print(f"{dist}: avvicinato di {gap:.2f} unità")

# --- scala reale e gerarchia
S = Matrix.Scale(SCALA, 4)
for o in ob.values(): o.data.transform(S)
hub_z = (L + raggio_mozzo * 0.55) * SCALA
radice = bpy.data.objects.new("PinzaSteampunk", None); bpy.context.scene.collection.objects.link(radice)
for nome_dito, parti in (('Dito_Sup', ('Sup_Prossimale', 'Sup_Distale')), ('Dito_Inf', ('Inf_Prossimale', 'Inf_Distale'))):
    e = bpy.data.objects.new(nome_dito, None); bpy.context.scene.collection.objects.link(e)
    e.parent = radice; e.location = (0, 0, hub_z)
    for k in parti:
        ob[k].data.transform(Matrix.Translation((0, 0, -hub_z)))   # coordinate relative al pivot
        ob[k].parent = e
for k in ('Gabbia', 'Mozzo'): ob[k].parent = radice
bpy.context.view_layer.update()

# --- materiale con le texture PBR della sorgente
m = bpy.data.materials.new("SteamPunk"); m.use_nodes = True; nt = m.node_tree
bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
def tex(nome, cs):
    img = bpy.data.images.load(os.path.join(SRC, nome)); img.colorspace_settings.name = cs
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = img; return n
nt.links.new(tex("SteamPunk_Robot_Hand_Color.png", "sRGB").outputs["Color"], bsdf.inputs["Base Color"])
nt.links.new(tex("SteamPunk_Robot_Hand_Roughness.png", "Non-Color").outputs["Color"], bsdf.inputs["Roughness"])
nm = nt.nodes.new("ShaderNodeNormalMap")
nt.links.new(tex("SteamPunk_Robot_Hand_Normal.png", "Non-Color").outputs["Color"], nm.inputs["Color"])
nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
bsdf.inputs["Metallic"].default_value = 0.85
for o in ob.values():
    o.data.materials.clear(); o.data.materials.append(m)
    for p in o.data.polygons: p.use_smooth = True

tutte = [p for o in ob.values() for p in punti(o)]
print("PinzaSteampunk: lunghezza %.1f cm, mozzo a %.1f cm, apertura dita %.0f°"
      % (max(p.z for p in tutte) * 100, hub_z * 100,
         math.degrees((punta_sup - H).angle(punta_inf - H))))
os.makedirs(OUT, exist_ok=True)
for o in bpy.context.scene.objects: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "PinzaSteampunk.fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, r"Blender\Pinza_Steampunk.blend"))
