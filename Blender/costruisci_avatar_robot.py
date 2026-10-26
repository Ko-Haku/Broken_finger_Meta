# Avatar_Robot: avatar TB con il braccio robotico ("Robotic Power Arm") al posto del braccio destro LM.
# Da eseguire in Blender 4.5 DOPO costruisci_avatar_tb.py (ne riusa la scena):
#   exec(open(r"...\Blender\costruisci_avatar_robot.py").read())
# Il robot è una mano destra (dorso = copertura). Pesi rigidi per pezzo meccanico:
# avambraccio -> ForeArm, palmo -> Hand, dita -> falange più vicina; ossa delle dita sulle articolazioni.
import bpy, bmesh, glob, math, os
from mathutils import Vector, Matrix

PROGETTO = r"D:\unity projects\Esperimenti 2027"
SRC = os.path.join(PROGETTO, r"Blender\sorgenti\braccio_robotico")
OUT = os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\Avatar_TB")
MATERIALE = {0: 'cover', 1: 'parts', 2: 'parts', 3: 'parts', 4: 'parts', 5: 'parts', 6: 'parts',
             7: 'details', 8: 'details', 9: 'details', 10: 'details', 11: 'details', 12: 'parts2', 13: 'parts2'}
# geometria della sorgente (unità originali, Z = asse del braccio, dita verso -Z, dorso verso -Y)
Z_POLSO, Z_MCP, Z_PUNTA = -46.0, -74.0, -98.0
COLONNE = {'Index': 9.5, 'Middle': 3.0, 'Ring': -3.5, 'Pinky': -9.0}   # x delle dita
X_POLLICE = 11.5

A = bpy.data.objects['Armature']
def hb(n): return A.matrix_world @ A.data.bones[n].head_local

# ---------------------------------------------------------------- import e materiali
def materiale(nome):
    m = bpy.data.materials.get("Robot_" + nome)
    if m: return m
    m = bpy.data.materials.new("Robot_" + nome); m.use_nodes = True; nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    def tex(suf, cs):
        p = os.path.join(SRC, f"arm_{nome}_{suf}.png")
        if not os.path.exists(p): return None
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(p, check_existing=True)
        n.image.colorspace_settings.name = cs; return n
    nt.links.new(tex("albedo", "sRGB").outputs["Color"], b.inputs["Base Color"])
    nt.links.new(tex("roughness", "Non-Color").outputs["Color"], b.inputs["Roughness"])
    nt.links.new(tex("metalness", "Non-Color").outputs["Color"], b.inputs["Metallic"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tex("normal", "Non-Color").outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    e = tex("emissive", "sRGB")
    if e: nt.links.new(e.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 1.0
    return m

prima = set(bpy.data.objects)
for i in range(14):
    bpy.ops.wm.obj_import(filepath=os.path.join(SRC, f"model_{i}.obj"))
    o = [x for x in bpy.data.objects if x not in prima][0]; prima.add(o)
    o.data.materials.clear(); o.data.materials.append(materiale(MATERIALE[i]))
pezzi_obj = [o for o in bpy.data.objects if o.name.startswith("model_")]
for o in bpy.context.selected_objects: o.select_set(False)
for o in pezzi_obj: o.select_set(True)
bpy.context.view_layer.objects.active = pezzi_obj[0]
bpy.ops.object.join()
R = bpy.context.view_layer.objects.active; R.name = "Braccio_Robot_R"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# ---------------------------------------------------------------- punti chiave nella sorgente
V = [v.co.copy() for v in R.data.vertices]
def centro(sel): return sum(sel, Vector()) / max(1, len(sel))
polso_r = centro([p for p in V if abs(p.z - Z_POLSO) < 2 and abs(p.x) < 11])
mcp = {f: centro([p for p in V if abs(p.x - x) < 2.2 and abs(p.z - Z_MCP) < 2]) for f, x in COLONNE.items()}
punte = {}
for f, x in COLONNE.items():          # punta reale: parte più bassa della colonna (il mignolo è più corto)
    col = [p for p in V if abs(p.x - x) < 2.2 and p.z < Z_MCP]
    zmin = min(p.z for p in col)
    punte[f] = centro([p for p in col if p.z < zmin + 2])
print("lunghezza dita (unità sorgente):", {f: round((mcp[f] - punte[f]).length, 1) for f in COLONNE})
# pollice: asse principale dei suoi vertici
P = [p for p in V if p.x > X_POLLICE and Z_MCP - 14 < p.z < -58]
pc = centro(P); cov = Matrix(((0,)*3,)*3)
for p in P:
    d = p - pc
    for i in range(3):
        for j in range(3): cov[i][j] += d[i] * d[j]
ax = Vector((0, 0, -1))
for _ in range(30): ax = (cov @ ax).normalized()
if ax.z > 0: ax = -ax                                    # dal palmo verso la punta
s_ = [(p - pc).dot(ax) for p in P]
pollice_base, pollice_punta = pc + ax * min(s_), pc + ax * max(s_)

# ---------------------------------------------------------------- allineamento anatomico e scala
def frame(f, lat):
    f = f.normalized(); up = f.cross(lat).normalized(); lat = up.cross(f).normalized()
    return Matrix((f, lat, up)).transposed()
wa = hb('mixamorig_RightHand'); gomito = hb('mixamorig_RightForeArm')
Fl = frame(mcp['Middle'] - polso_r, mcp['Index'] - mcp['Pinky'])
Fa = frame(wa - gomito, Vector((0, -1, 0)))
lung_mano = (hb('mixamorig_RightHandMiddle1') - wa).length          # mano LM già montata sull'avatar
s = lung_mano / (mcp['Middle'] - polso_r).length
M = Matrix.Translation(wa) @ (Fa @ Fl.transposed()).to_4x4() @ Matrix.Scale(s, 4) @ Matrix.Translation(-polso_r)
R.data.transform(M); R.data.update()
T = lambda p: M @ p
dorso = T(centro([p for p in V if p.y < -12]))
assert dorso.z > wa.z, "il dorso del robot non è rivolto in alto: mano specchiata?"
print(f"robot: scala {s:.5f}, mano {lung_mano*100:.1f} cm, avambraccio {(T(Vector((0,0,25))) - wa).length*100:.0f} cm")

# ---------------------------------------------------------------- via il braccio destro LM / moncherino
for n in ('Braccio_LM_R', 'Moncherino_LM_R'):
    o = bpy.data.objects.get(n)
    if o: bpy.data.objects.remove(o, do_unlink=True)

# ---------------------------------------------------------------- ossa delle dita sulle articolazioni del robot
catene = {}
for f in COLONNE:
    b, t = T(mcp[f]), T(punte[f])
    catene[f] = [b.lerp(t, k) for k in (0.0, 0.36, 0.66, 1.0)]
pb, pt = T(pollice_base), T(pollice_punta)
catene['Thumb'] = [pb.lerp(pt, k) for k in (0.0, 0.4, 0.72, 1.0)]
Ai = A.matrix_world.inverted()
for o in bpy.context.selected_objects: o.select_set(False)
bpy.context.view_layer.objects.active = A; A.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); eb = A.data.edit_bones
for f, pts in catene.items():
    for i in range(3):
        b = eb[f'mixamorig_RightHand{f}{i + 1}']; b.use_connect = False; b.head = Ai @ pts[i]; b.tail = Ai @ pts[i + 1]
    d = (pts[3] - pts[2]).normalized()
    for nome, h0, h1 in ((f'mixamorig_RightHand{f}4', pts[3], pts[3] + d * 0.01), (f'mixamorig_RightHand{f}4_end', pts[3] + d * 0.01, pts[3] + d * 0.02)):
        b = eb.get(nome)
        if b: b.use_connect = False; b.head = Ai @ h0; b.tail = Ai @ h1
h = eb['mixamorig_RightHand']; h.head = Ai @ wa; h.tail = Ai @ catene['Middle'][0]
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- pesi rigidi per pezzo meccanico
gruppi = {}
def gruppo(n):
    if n not in gruppi: gruppi[n] = R.vertex_groups.new(name=n)
    return gruppi[n]
def dist_segmento(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared)); return (p - (a + ab * t)).length
assi = (wa - gomito).normalized()
bm = bmesh.new(); bm.from_mesh(R.data); bm.verts.ensure_lookup_table(); visti = set(); assegna = {}
for v in bm.verts:
    if v.index in visti: continue
    stack = [v]; comp = []
    while stack:
        x = stack.pop()
        if x.index in visti: continue
        visti.add(x.index); comp.append(x.index)
        for e in x.link_edges: stack.append(e.other_vert(x))
    c = centro([bm.verts[i].co for i in comp])
    lungo = (c - wa).dot(assi)                                  # >0 oltre il polso (mano)
    if lungo < 0:
        osso = 'mixamorig_RightForeArm'
    else:
        osso = 'mixamorig_RightHand'; migliore = None
        for f, pts in catene.items():
            for i in range(3):
                dd = dist_segmento(c, pts[i], pts[i + 1])
                if migliore is None or dd < migliore[0]: migliore = (dd, f'mixamorig_RightHand{f}{i + 1}')
        # nel palmo (prima delle nocche) resta sulla mano, salvo il pollice
        if migliore and (migliore[0] < 0.012 * (lung_mano / 0.085)):
            osso = migliore[1]
    assegna.setdefault(osso, []).extend(comp)
bm.free()
for osso, idx in assegna.items(): gruppo(osso).add(idx, 1.0, 'REPLACE')
print("pezzi per osso:", {k.replace('mixamorig_RightHand', ''): len(v) for k, v in assegna.items()})
mod = R.modifiers.new("Armature", 'ARMATURE'); mod.object = A
R.parent = A; R.matrix_parent_inverse = A.matrix_world.inverted()

# ---------------------------------------------------------------- export
for o in bpy.context.scene.objects: o.select_set(False)
objs = [A] + list(A.children)
for o in objs: o.hide_set(False); o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Avatar_Robot.fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=False, mesh_smooth_type='FACE')
print("Avatar_Robot:", sorted(o.name for o in objs if o.type == 'MESH'))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, r"Blender\Avatar_Robot.blend"))
