# Costruisce gli avatar del paradigma di temporal binding (Avatar_Mano / Avatar_Tool).
# Da eseguire in Blender 4.5:  exec(open(r"...\Blender\costruisci_avatar_tb.py").read())
#
# Passi:
#  1. importa avatar_stumped 1.fbx e lo specchia in X (l'FBX si apre specchiato in Blender
#     rispetto a Unity: senza questo passo il "braccio destro" sarebbe il sinistro anatomico)
#  2. innesta i bracci LM (Hand_04 destro, Hand_03 sinistro) sugli avambracci dell'avatar con
#     allineamento anatomico (palmo in giù, pollice in avanti), mano a grandezza reale
#  3. allunga l'avambraccio LM sotto la manica e allarga il polsino della maglia quanto serve
#     a contenerlo (l'avambraccio mantiene la sua forma naturale)
#  4. rimuove il braccio originale dell'avatar oltre la manica
#  5. porta le ossa delle dita Mixamo sulle articolazioni LM e rinomina i pesi LM -> Mixamo
#  6. Avatar_Tool: stesso braccio destro tagliato al polso e chiuso con un moncherino
#  7. esporta i due FBX in Assets/MODELS/TemporalBinding/Avatar_TB
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

PROGETTO = r"D:\unity projects\Esperimenti 2027"
AVATAR = os.path.join(PROGETTO, r"Assets\prefabs\avatars\avatar_stumped 1.fbx")
# Variante delle mani: M = LM maschili (Hand_04/Hand_03), F = femminili (costruisci_mani_femminili.py,
# stesso scheletro e stessi gruppi di pesi LM). Da riga di comando: set TB_MANI=F prima di lanciare Blender.
# La variante F esporta Avatar_ManoF.fbx / Avatar_ToolF.fbx (le maschili restano intatte).
MANI = os.environ.get("TB_MANI", "M").upper()
if MANI == "F":
    LM = {'Right': os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\ManiFemminili\ManoF_R.fbx"),
          'Left': os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\ManiFemminili\ManoF_L.fbx")}
else:
    LM = {'Right': os.path.join(PROGETTO, r"Assets\LMRealisticHands\Models\Hand_04.fbx"),
          'Left': os.path.join(PROGETTO, r"Assets\LMRealisticHands\Models\Hand_03.fbx")}
SUFFISSO = "F" if MANI == "F" else ""
OUT = os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\Avatar_TB")
DITA = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']
ALLUNGA = 0.04          # m di avambraccio LM aggiunti sotto la manica
MARGINE_MANICA = 1.08   # il polsino deve essere almeno l'8% più largo dell'avambraccio
NB = 24                 # settori angolari per i profili radiali

# ---------------------------------------------------------------- utilità
def hb(arm, n):
    return arm.matrix_world @ arm.data.bones[n].head_local

def frame(f, lat):
    f = f.normalized(); up = f.cross(lat).normalized(); lat = up.cross(f).normalized()
    return Matrix((f, lat, up)).transposed()

def mesh_mondo(o):
    dg = bpy.context.evaluated_depsgraph_get()
    return [o.matrix_world @ v.co for v in o.evaluated_get(dg).to_mesh().vertices]

def cancella_vertici(o, cond):
    bm = bmesh.new(); bm.from_mesh(o.data)
    kill = [v for v in bm.verts if cond(o.matrix_world @ v.co)]
    bmesh.ops.delete(bm, geom=kill, context='VERTS')
    bm.to_mesh(o.data); bm.free(); o.data.update()
    return len(kill)

class Polare:
    """Coordinate (lungo l'asse, raggio, angolo) attorno all'asse gomito->polso."""
    def __init__(self, gomito, polso):
        self.e = gomito; self.ax = (polso - gomito).normalized()
        ref = Vector((0, 0, 1)); self.u = (ref - self.ax * ref.dot(self.ax)).normalized(); self.v = self.ax.cross(self.u)
    def __call__(self, p):
        d = p - self.e; a = d.dot(self.ax); r = d - self.ax * a
        return a, r.length, math.atan2(r.dot(self.v), r.dot(self.u)) % (2 * math.pi)
    def da(self, a, r, th):
        return self.e + self.ax * a + (self.u * math.cos(th) + self.v * math.sin(th)) * r

# ---------------------------------------------------------------- 1. avatar specchiato
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials):
    for x in list(coll):
        if x.users == 0: coll.remove(x)
bpy.ops.import_scene.fbx(filepath=AVATAR)
A = bpy.data.objects['Armature']
for o in bpy.context.scene.objects: o.select_set(False)
for o in [A] + list(A.children): o.select_set(True)
bpy.context.view_layer.objects.active = A
bpy.ops.transform.resize(value=(-1, 1, 1), center_override=(0, 0, 0))
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
for o in A.children:          # la scala negativa ribalta le normali
    if o.type == 'MESH':
        bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.to_mesh(o.data); bm.free()
TOPS = bpy.data.objects['Tops']; BODY = bpy.data.objects['Body']
bracci = {}

for lato in ('Right', 'Left'):
    s = -1 if lato == 'Right' else 1          # lato anatomico in X dopo lo specchio
    p = lato[0]                               # prefisso ossa LM: R_ / L_
    # ------------------------------------------------------------ 2. innesto anatomico
    prima = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=LM[lato])
    nuovi = set(bpy.data.objects) - prima
    L = [o for o in nuovi if o.type == 'ARMATURE'][0]; me = [o for o in nuovi if o.type == 'MESH'][0]
    wl = hb(L, f'{p}_Wrist')
    Fl = frame(hb(L, f'{p}_Finger_Middle_A') - wl, hb(L, f'{p}_Finger_Index_A') - hb(L, f'{p}_Finger_Pinky_A'))
    gomito = hb(A, f'mixamorig_{lato}ForeArm'); wa = hb(A, f'mixamorig_{lato}Hand')
    Fa = frame(wa - gomito, Vector((0, -1, 0)))      # dita lungo l'avambraccio, indice in avanti (-Y)
    L.matrix_world = Matrix.Translation(wa) @ (Fa @ Fl.transposed()).to_4x4() @ Matrix.Translation(-wl) @ L.matrix_world
    bpy.context.view_layer.update()
    curl = (hb(L, f'{p}_Finger_Middle_C') - hb(L, f'{p}_Finger_Middle_A')).normalized().z
    assert curl < 0, f"{lato}: palmo non rivolto in basso (curl z={curl:.2f})"

    # ------------------------------------------------------------ 3a. allunga sotto la manica
    mw = me.matrix_world; mwi = mw.inverted()
    xs = [(mw @ v.co).x * s for v in me.data.vertices]; bordo = min(xs)
    for v in me.data.vertices:
        q = mw @ v.co; ax = q.x * s
        if ax < bordo + 0.035:
            k = min(1.0, max(0.0, ((bordo + 0.035) - ax) / 0.03)); q.x -= s * ALLUNGA * k; v.co = mwi @ q
    me.data.update()

    # ------------------------------------------------------------ 3b. allarga il polsino
    pol = Polare(gomito, wa)
    braccio = {}
    for q in (mw @ v.co for v in me.data.vertices):
        a, r, th = pol(q); k = (int(a * 100), int(th / (2 * math.pi) * NB))
        braccio[k] = max(braccio.get(k, 0), r)
    bm = bmesh.new(); bm.from_mesh(TOPS.data); tw = TOPS.matrix_world; twi = tw.inverted()
    manica = [v for v in bm.verts if (tw @ v.co).x * s > 0.38]
    amax = max(pol(tw @ v.co)[0] for v in manica)
    spinti = 0
    for v in manica:
        a, r, th = pol(tw @ v.co)
        if a < 0: continue
        ai = int(a * 100); ti = int(th / (2 * math.pi) * NB)
        rb = max([braccio.get((ai + da, (ti + dt) % NB), 0) for da in (-1, 0, 1) for dt in (-1, 0, 1)])
        serve = rb * MARGINE_MANICA
        if r < serve:
            v.co = twi @ pol.da(a, serve, th); spinti += 1
    bm.to_mesh(TOPS.data); bm.free(); TOPS.data.update()

    # ------------------------------------------------------------ 4. via il braccio originale
    fine_manica = max((tw @ v.co).x * s for v in TOPS.data.vertices if (tw @ v.co).x * s > 0.38)
    tolti = cancella_vertici(BODY, lambda q: q.x * s > fine_manica - 0.03)

    # ------------------------------------------------------------ 5. ossa dita e pesi
    LMh = {b.name: hb(L, b.name) for b in L.data.bones}
    Ai = A.matrix_world.inverted()
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = A; A.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); eb = A.data.edit_bones
    for f in DITA:
        d = (LMh[f'{p}_Finger_{f}_C'] - LMh[f'{p}_Finger_{f}_B']).normalized()
        punta = LMh[f'{p}_Finger_{f}_C'] + d * 0.022
        pts = [LMh[f'{p}_Finger_{f}_A'], LMh[f'{p}_Finger_{f}_B'], LMh[f'{p}_Finger_{f}_C'], punta]
        for i in range(3):
            b = eb[f'mixamorig_{lato}Hand{f}{i + 1}']; b.use_connect = False; b.head = Ai @ pts[i]; b.tail = Ai @ pts[i + 1]
        for nome, h0, h1 in ((f'mixamorig_{lato}Hand{f}4', punta, punta + d * 0.01),
                             (f'mixamorig_{lato}Hand{f}4_end', punta + d * 0.01, punta + d * 0.02)):
            b = eb.get(nome)
            if b: b.use_connect = False; b.head = Ai @ h0; b.tail = Ai @ h1
    h = eb[f'mixamorig_{lato}Hand']; h.head = Ai @ LMh[f'{p}_Wrist']; h.tail = Ai @ LMh[f'{p}_Finger_Middle_A']
    bpy.ops.object.mode_set(mode='OBJECT')
    ren = {f'{p}_Forearm': f'mixamorig_{lato}ForeArm', f'{p}_Wrist': f'mixamorig_{lato}Hand', f'{p}_Arm': f'mixamorig_{lato}Arm'}
    for f in DITA:
        for i, c in enumerate('ABC'): ren[f'{p}_Finger_{f}_{c}'] = f'mixamorig_{lato}Hand{f}{i + 1}'
    for g in me.vertex_groups:
        if g.name in ren: g.name = ren[g.name]
    m = me.matrix_world.copy(); me.parent = A; me.matrix_world = m
    for md in me.modifiers:
        if md.type == 'ARMATURE': md.object = A
    bpy.data.objects.remove(L, do_unlink=True)
    me.name = f'Braccio_LM_{p}'
    bracci[lato] = me

    # ------------------------------------------------------------ 5b. osso di torsione dell'avambraccio
    # Peso crescente dal gomito (0) al polso (1): in Unity ruota quanto la mano rispetto
    # all'avambraccio, così la pronosupinazione si distribuisce lungo l'avambraccio (come il radio).
    nome_tw = f'mixamorig_{lato}ForeArmTwist'
    bpy.context.view_layer.objects.active = A; A.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); eb = A.data.edit_bones
    tw_b = eb.get(nome_tw) or eb.new(nome_tw)
    tw_b.head = Ai @ (gomito + (wa - gomito) * 0.5); tw_b.tail = Ai @ wa
    tw_b.parent = eb[f'mixamorig_{lato}ForeArm']; tw_b.use_connect = False; tw_b.use_deform = True
    bpy.ops.object.mode_set(mode='OBJECT')
    g_fa = me.vertex_groups[f'mixamorig_{lato}ForeArm']; g_tw = me.vertex_groups.get(nome_tw) or me.vertex_groups.new(name=nome_tw)
    lung = (wa - gomito).length; ax = (wa - gomito).normalized(); mw = me.matrix_world
    def smooth(e0, e1, x):
        t = min(1.0, max(0.0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
    for v in me.data.vertices:
        wf = next((g.weight for g in v.groups if g.group == g_fa.index), 0.0)
        if wf <= 0: continue
        quota = smooth(0.15, 0.95, ((mw @ v.co) - gomito).dot(ax) / lung)
        g_fa.add([v.index], wf * (1 - quota), 'REPLACE'); g_tw.add([v.index], wf * quota, 'REPLACE')

    # ------------------------------------------------------------ 5c. la manica eredita i pesi del braccio
    # Nell'ultima fascia della manica i pesi della maglia diventano quelli del braccio LM
    # sottostante: pelle e stoffa si deformano allo stesso modo e il braccio non buca la manica.
    from mathutils import kdtree
    vs = [mw @ v.co for v in me.data.vertices]
    kd = kdtree.KDTree(len(vs))
    for i, q in enumerate(vs): kd.insert(q, i)
    kd.balance()
    gn_lm = {g.index: g.name for g in me.vertex_groups}
    tw_ = TOPS.matrix_world
    for nome in set(gn_lm.values()):
        if nome not in TOPS.vertex_groups: TOPS.vertex_groups.new(name=nome)
    fascia = 0.045
    for v in TOPS.data.vertices:
        q = tw_ @ v.co
        if q.x * s < 0.38: continue
        a = pol(q)[0]
        k = smooth(amax - fascia, amax - 0.015, a)
        if k <= 0: continue
        _, i, _ = kd.find(q)
        lm_w = {gn_lm[g.group]: g.weight for g in me.data.vertices[i].groups}
        orig = {TOPS.vertex_groups[g.group].name: g.weight for g in v.groups}
        for nome in set(orig) | set(lm_w):
            w = orig.get(nome, 0.0) * (1 - k) + lm_w.get(nome, 0.0) * k
            if w > 1e-4: TOPS.vertex_groups[nome].add([v.index], w, 'REPLACE')
            elif nome in orig: TOPS.vertex_groups[nome].remove([v.index])

    # ------------------------------------------------------------ 5d. via la pelle nascosta sotto la manica
    nascosti = cancella_vertici(me, lambda q: 0 <= pol(q)[0] < amax - 0.03)
    print(f"{lato}: polsino allargato su {spinti} vertici, braccio avatar rimosso {tolti} vertici, "
          f"pelle nascosta rimossa {nascosti} vertici")

# un solo materiale pelle per i due bracci (in Unity -> Pelle_LM)
pelle = bracci['Right'].data.materials[0]; pelle.name = 'Pelle_F' if MANI == "F" else 'AfroAmerican'
bracci['Left'].data.materials[0] = pelle

# ---------------------------------------------------------------- 6. moncherino destro
src = bracci['Right']
st = src.copy(); st.data = src.data.copy(); st.name = 'Moncherino_LM_R'
bpy.context.scene.collection.objects.link(st); st.parent = A; st.matrix_world = src.matrix_world.copy()
mw = st.matrix_world; mwi = mw.inverted()
# Il polso è aperto e "cucito" dentro l'anello cromato (maingripper) che in Unity è solidale
# all'avambraccio: faccia superiore dell'anello sul polso, l'anello si estende verso il gomito
# per la sua altezza, la pelle finisce qualche mm dentro l'anello (bordo nascosto dal disco).
# Il moncherino è rigido: niente pesi sulla mano (che si flette col polso tracciato).
ALTEZZA_ANELLO = 0.0168 / 0.8      # altezza dell'anello in Unity / scala dell'avatar in Unity
DENTRO_ANELLO = 0.004 / 0.8        # quanto la pelle entra nell'anello
taglio = hb(A, 'mixamorig_RightHand').x + ALTEZZA_ANELLO - DENTRO_ANELLO
bm = bmesh.new(); bm.from_mesh(st.data); dl = bm.verts.layers.deform.verify()
bmesh.ops.delete(bm, geom=[v for v in bm.verts if (mw @ v.co).x < taglio], context='VERTS')
g_mano_vecchio = st.vertex_groups['mixamorig_RightHand'].index
g_tw = st.vertex_groups['mixamorig_RightForeArmTwist'].index
for v in bm.verts:            # peso della mano -> osso di torsione (come l'anello in Unity)
    dv = v[dl]
    if g_mano_vecchio in dv:
        w = dv[g_mano_vecchio]; del dv[g_mano_vecchio]
        dv[g_tw] = dv.get(g_tw, 0.0) + w
bm.to_mesh(st.data); bm.free()
for g in list(st.vertex_groups):
    if 'RightHand' in g.name: st.vertex_groups.remove(g)      # nessun peso su mano e dita

# ---------------------------------------------------------------- 7. export
for m in bpy.data.materials:
    if m.name.endswith('.001') and m.name[:-4] not in bpy.data.materials: m.name = m.name[:-4]
def esporta(nome, escludi):
    for o in bpy.context.scene.objects: o.select_set(False)
    objs = [A] + [o for o in A.children if o.name != escludi]
    for o in objs: o.hide_set(False); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, nome), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=False, mesh_smooth_type='FACE')
    return sorted(o.name for o in objs if o.type == 'MESH')
os.makedirs(OUT, exist_ok=True)
print(f"Avatar_Mano{SUFFISSO}:", esporta(f"Avatar_Mano{SUFFISSO}.fbx", 'Moncherino_LM_R'))
print(f"Avatar_Tool{SUFFISSO}:", esporta(f"Avatar_Tool{SUFFISSO}.fbx", 'Braccio_LM_R'))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, rf"Blender\Avatar_TB{SUFFISSO}.blend"))
