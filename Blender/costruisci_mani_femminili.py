# Mani femminili realistiche (dump OBJ senza scheletro né materiali) -> modelli riggati come le LM.
# Da eseguire in Blender 4.5:  exec(open(r"...\Blender\costruisci_mani_femminili.py").read())
#  1. allineamento sulla mano maschile LM (stima iniziale + ICP rigido con scala)
#  2. pesi trasferiti dalla mano maschile (stesso scheletro LM: R_Wrist, R_Finger_*_A/B/C, ...)
#  3. texture della pelle LM "cotte" sulle UV della mano femminile (albedo, normal, roughness)
#  4. export Assets/MODELS/TemporalBinding/ManiFemminili/ManoF_R.fbx e ManoF_L.fbx (stessa gerarchia di Hand_04/Hand_03)
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, kdtree

PROGETTO = r"D:\unity projects\Esperimenti 2027"
SRC = os.path.join(PROGETTO, r"Blender\sorgenti\mani_femminili")
LM = os.path.join(PROGETTO, r"Assets\LMRealisticHands")
OUT = os.path.join(PROGETTO, r"Assets\MODELS\TemporalBinding\ManiFemminili")
TEX = 2048

def pulisci():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images):
        for x in list(coll):
            if x.users == 0: coll.remove(x)

def importa_obj(nome):
    prima = set(bpy.data.objects); bpy.ops.wm.obj_import(filepath=os.path.join(SRC, nome + ".obj"))
    return [o for o in bpy.data.objects if o not in prima][0]

def mondo(o): return [o.matrix_world @ v.co for v in o.data.vertices]
def centro(p): return sum(p, Vector()) / len(p)

def umeyama(src, dst):
    """Similitudine (s, R, t) che porta src su dst ai minimi quadrati."""
    n = len(src); ms, md = centro(src), centro(dst)
    C = Matrix(((0.0,) * 3,) * 3); vs = 0.0
    for a, b in zip(src, dst):
        da, db = a - ms, b - md; vs += da.length_squared
        for i in range(3):
            for j in range(3): C[i][j] += db[i] * da[j]
    # SVD 3x3 via decomposizione polare iterativa (sufficiente per rotazioni)
    Rm = C.copy()
    for _ in range(60):
        Rm = (Rm + Rm.inverted().transposed()) * 0.5
    if Rm.determinant() < 0:
        Rm = -Rm
    s = sum((Rm.transposed() @ C)[i][i] for i in range(3)) / vs
    return s, Rm, md - s * (Rm @ ms)

def icp(F, ref_pts, iterazioni=40, campioni=4000):
    kd = kdtree.KDTree(len(ref_pts))
    for i, p in enumerate(ref_pts): kd.insert(p, i)
    kd.balance()
    random.seed(3)
    for it in range(iterazioni):
        P = mondo(F); P = random.sample(P, min(campioni, len(P)))
        Q = [kd.find(p)[0] for p in P]
        err = sum((p - q).length for p, q in zip(P, Q)) / len(P)
        s, Rm, t = umeyama(P, Q)
        F.matrix_world = (Matrix.Translation(t) @ (Rm.to_4x4() @ Matrix.Scale(s, 4))) @ F.matrix_world
    return err

def riempi_mancanti(img, nome):
    """I raggi della cottura che non trovano la mano maschile (punte delle dita, unghie più lunghe,
    pieghe) lasciano pixel neri: li si riempie con il valore tipico della pelle (normale piatta)."""
    import numpy as np
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32); img.pixels.foreach_get(px); px = px.reshape(-1, 4)
    buio = px[:, :3].max(axis=1) < 0.02
    if nome == "Normal":
        px[buio, :3] = (0.5, 0.5, 1.0)
    else:
        validi = px[~buio, :3]
        px[buio, :3] = np.median(validi, axis=0) if len(validi) else 0.5
    px[:, 3] = 1.0
    img.pixels.foreach_set(px.ravel()); img.update()
    print(f"  {nome}: riempiti {int(buio.sum())} pixel senza dato ({100 * buio.mean():.1f}%)")

def costruisci(lato):
    pulisci()
    fbx = {'R': "Hand_04.fbx", 'L': "Hand_03.fbx"}[lato]
    prima = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(LM, "Models", fbx))
    nuovi = [o for o in bpy.data.objects if o not in prima]
    arm = [o for o in nuovi if o.type == 'ARMATURE'][0]; maschio = [o for o in nuovi if o.type == 'MESH'][0]
    p = lato
    def hb(n): return arm.matrix_world @ arm.data.bones[n].head_local
    polso_m, medio_m = hb(f'{p}_Wrist'), hb(f'{p}_Finger_Middle_C')
    gomito_m = hb(f'{p}_Forearm')

    # mano + avambraccio femminili (sinistra nel modello; la destra è la sinistra specchiata)
    mano, avamb = importa_obj("model_2"), importa_obj("model_3")
    # mano e avambraccio usavano due texture diverse con le stesse UV: si separano (mano a sinistra,
    # avambraccio a destra di un'immagine 2:1), altrimenti nella cottura si scrivono sugli stessi pixel
    for o, u0 in ((mano, 0.0), (avamb, 0.5)):
        uv = o.data.uv_layers.active.data
        for l in uv: l.uv = (u0 + (l.uv.x % 1.0) * 0.5, l.uv.y % 1.0)
    for o in bpy.context.selected_objects: o.select_set(False)
    mano.select_set(True); avamb.select_set(True); bpy.context.view_layer.objects.active = mano
    bpy.ops.object.join(); F = mano; F.name = f"ManoF_{lato}"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # il modello estratto è una mano SINISTRA: si specchia per ottenere la destra
    # (verificato affiancandola alla Hand_04 LM: senza specchio il pollice era dal lato opposto)
    if lato == 'R':
        F.data.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
        bm = bmesh.new(); bm.from_mesh(F.data); bmesh.ops.reverse_faces(bm, faces=bm.faces); bm.to_mesh(F.data); bm.free()

    # stima iniziale: polso = giunzione mano/avambraccio, asse verso le dita, scala sulla lunghezza
    V = mondo(F)
    nm = 6570                                     # dopo il join: prima i vertici della mano, poi l'avambraccio
    pm = centro(V[:nm]); pa = centro(V[nm:])
    dir_f = (pm - pa).normalized()
    punta_f = max(V, key=lambda v: (v - pa).dot(dir_f))
    polso_f = centro([v for v in V[:nm] if (v - pa).dot(dir_f) < min((u - pa).dot(dir_f) for u in V[:nm]) + 0.01])
    dir_m = (medio_m - polso_m).normalized()
    s = (medio_m - polso_m).length * 1.25 / (punta_f - polso_f).length
    Rm = dir_f.rotation_difference(dir_m).to_matrix().to_4x4()
    F.matrix_world = Matrix.Translation(polso_m) @ Rm @ Matrix.Scale(s, 4) @ Matrix.Translation(-polso_f)
    # quattro rotazioni attorno all'asse: tiene quella con ICP migliore
    ref = mondo(maschio)
    base = F.matrix_world.copy(); migliore = None
    for k in range(4):
        F.matrix_world = Matrix.Translation(polso_m) @ Matrix.Rotation(k * math.pi / 2, 4, dir_m) @ Matrix.Translation(-polso_m) @ base
        e = icp(F, ref, iterazioni=12, campioni=1500)
        if migliore is None or e < migliore[0]: migliore = (e, F.matrix_world.copy())
    F.matrix_world = migliore[1]
    err = icp(F, ref, iterazioni=40, campioni=5000)
    bpy.ops.object.select_all(action='DESELECT'); F.select_set(True); bpy.context.view_layer.objects.active = F
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    print(f"{lato}: ICP errore medio {err*1000:.1f} mm")

    # il modello estratto è spezzato in isole con vertici doppi (o quasi: < 0.1 mm) lungo le cuciture
    # delle UV, tra mano e avambraccio e alla base del pollice: saldati, altrimenti i due lati di ogni
    # cucitura prendono pesi diversi e piegando le dita la pelle si apre (a 0.1 mm i bordi aperti vanno a zero)
    bm = bmesh.new(); bm.from_mesh(F.data); n0 = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    aperti = sum(1 for e in bm.edges if e.is_boundary)
    bm.to_mesh(F.data); bm.free(); F.data.update()
    print(f"{lato}: saldati {n0 - len(F.data.vertices)} vertici doppi, bordi aperti rimasti {aperti}")

    # pesi dalla mano maschile
    for g in maschio.vertex_groups: F.vertex_groups.new(name=g.name)
    dt = F.modifiers.new("PesiLM", 'DATA_TRANSFER'); dt.object = maschio
    dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
    dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
    bpy.ops.object.modifier_apply(modifier=dt.name)
    # pesi levigati: niente salti dove le due mani differiscono
    bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
    bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.5, repeat=4)
    bpy.ops.object.mode_set(mode='OBJECT')

    # scheletro adattato alla mano femminile: ogni articolazione va dove la mesh passa da un osso
    # al successivo (vertici con entrambi i pesi > 0.25), così le dita ruotano nei punti giusti
    gi = {g.name: g.index for g in F.vertex_groups}
    VW = [(F.matrix_world @ v.co, {g.group: g.weight for g in v.groups}) for v in F.data.vertices]
    def giunto(a, b):
        sel = [p for p, w in VW if w.get(gi.get(a, -1), 0) > 0.25 and w.get(gi.get(b, -1), 0) > 0.25]
        return centro(sel) if len(sel) >= 6 else None
    catene = [(f'{p}_Forearm', f'{p}_Wrist')]
    for f in ('Thumb', 'Index', 'Middle', 'Ring', 'Pinky'):
        catene += [(f'{p}_Wrist', f'{p}_Finger_{f}_A'), (f'{p}_Finger_{f}_A', f'{p}_Finger_{f}_B'), (f'{p}_Finger_{f}_B', f'{p}_Finger_{f}_C')]
    nuovi_giunti = {b: giunto(a, b) for a, b in catene}
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT'); eb = arm.data.edit_bones; Ai = arm.matrix_world.inverted(); spostati = 0
    for b, g in nuovi_giunti.items():
        if g is None or b not in eb: continue
        e = eb[b]; L = e.length; d = (e.tail - e.head).normalized()
        e.use_connect = False; e.head = Ai @ g; spostati += 1
        figli = [c for c in e.children]
        if not figli: e.tail = e.head + d * L                     # falange distale: conserva la lunghezza
    for b, g in nuovi_giunti.items():                              # le code puntano al giunto successivo
        if g is None or b not in eb: continue
        for c in eb[b].children:
            if nuovi_giunti.get(c.name) is not None and len(eb[b].children) == 1:
                eb[b].tail = eb[c.name].head
    bpy.ops.object.mode_set(mode='OBJECT')
    print(f"{lato}: {spostati} articolazioni adattate alla mano femminile")
    # pesi finali per diffusione del calore dalle ossa (bone heat) sulla geometria femminile:
    # i pesi trasferiti mescolano dita vicine perché le dita femminili sono più aperte
    for g in list(F.vertex_groups): F.vertex_groups.remove(g)
    bpy.ops.object.select_all(action='DESELECT'); F.select_set(True); arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    vuoti = sum(1 for v in F.data.vertices if not v.groups)
    print(f"{lato}: pesi automatici, vertici senza peso: {vuoti}")

    # texture: cuoce la pelle LM (albedo / normal / roughness) sulle UV femminili
    mat_m = maschio.data.materials[0]
    imgs = {}
    for nome, cs in (("Albedo", "sRGB"), ("Normal", "Non-Color"), ("Roughness", "Non-Color")):
        img = bpy.data.images.new(f"ManoF_{nome}", TEX * 2, TEX); img.colorspace_settings.name = cs; imgs[nome] = img
    mat = bpy.data.materials.new("Pelle_F"); mat.use_nodes = True; nt = mat.node_tree
    tn = nt.nodes.new("ShaderNodeTexImage")
    F.data.materials.clear(); F.data.materials.append(mat)
    # materiale maschile con le texture LM (per la cottura)
    mm = bpy.data.materials.new("Pelle_M_bake"); mm.use_nodes = True; mnt = mm.node_tree
    mb = next(n for n in mnt.nodes if n.type == "BSDF_PRINCIPLED")
    def t(path, cs):
        n = mnt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(path, check_existing=True); n.image.colorspace_settings.name = cs; return n
    tx = os.path.join(LM, "Textures", "Hands_Mat_02")
    mnt.links.new(t(os.path.join(tx, "Hands_Albedo_02.png"), "sRGB").outputs["Color"], mb.inputs["Base Color"])
    mnt.links.new(t(os.path.join(tx, "Hands_Roughness_02.png"), "Non-Color").outputs["Color"], mb.inputs["Roughness"])
    nmm = mnt.nodes.new("ShaderNodeNormalMap")
    mnt.links.new(t(os.path.join(tx, "Hands_Normal_02.png"), "Non-Color").outputs["Color"], nmm.inputs["Color"])
    mnt.links.new(nmm.outputs["Normal"], mb.inputs["Normal"])
    maschio.data.materials.clear(); maschio.data.materials.append(mm)
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 4; sc.cycles.device = 'CPU'
    bk = sc.render.bake; bk.use_selected_to_active = True; bk.cage_extrusion = 0.03; bk.max_ray_distance = 0.0
    # margine largo ed esteso: nei livelli di mipmap più bassi il fondo non deve filtrare lungo le cuciture delle dita
    bk.margin = 64; bk.margin_type = 'EXTEND'
    bpy.ops.object.select_all(action='DESELECT'); maschio.select_set(True); F.select_set(True); bpy.context.view_layer.objects.active = F
    for nome, tipo in (("Albedo", 'DIFFUSE'), ("Roughness", 'ROUGHNESS'), ("Normal", 'NORMAL')):
        tn.image = imgs[nome]; nt.nodes.active = tn
        if tipo == 'DIFFUSE':
            bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
        bpy.ops.object.bake(type=tipo)
        riempi_mancanti(imgs[nome], nome)
        imgs[nome].filepath_raw = os.path.join(OUT, f"ManoF_{nome}.png"); imgs[nome].file_format = 'PNG'
        os.makedirs(OUT, exist_ok=True); imgs[nome].save()
    # materiale finale con le texture cotte
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    nt.nodes.remove(tn)
    def tf(nome):
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = imgs[nome]; return n
    nt.links.new(tf("Albedo").outputs["Color"], b.inputs["Base Color"])
    nt.links.new(tf("Roughness").outputs["Color"], b.inputs["Roughness"])
    nmf = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tf("Normal").outputs["Color"], nmf.inputs["Color"]); nt.links.new(nmf.outputs["Normal"], b.inputs["Normal"])
    for pl in F.data.polygons: pl.use_smooth = True

    bpy.data.objects.remove(maschio, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); F.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"ManoF_{lato}.fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=False, mesh_smooth_type='FACE')
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROGETTO, rf"Blender\ManoF_{lato}.blend"))

for lato in ('R', 'L'):
    costruisci(lato)
