# Braccio robotico: confine avambraccio/mano e pesi sfumati alle giunture.
# I ~5000 pezzi del robot sono attaccati ognuno a un solo osso (costruisci_avatar_robot.py).
#  - Polso: il manicotto del polso (placche e snodo) sta tra la fine del guscio dell'avambraccio e il perno
#    dell'osso Hand. La regola "prima del perno = avambraccio" lo lasciava sull'avambraccio: muovendo la mano
#    restava fermo e aperto ("pinne"). Il confine giusto è la fine del guscio: oltre, tutto va con la mano;
#    solo una fascia stretta sul bordo del guscio è sfumata.
#  - Dita: in una fascia stretta attorno a ogni articolazione i vertici passano gradualmente dal padre al figlio.
# Uso: chiamato da costruisci_avatar_robot.py prima dell'export, oppure da solo su Avatar_Robot.blend:
#   exec(open(r"...\Blender\robot_pesi_giunture.py").read())   (riesporta l'FBX e salva il .blend)
import bpy, bmesh, os
from mathutils import Vector

FASCIA_POLSO = 0.008      # m, mezza larghezza della sfumatura sul bordo del guscio dell'avambraccio
RAGGIO_GUSCIO = 0.04      # m, i pezzi più grandi di così prima del polso sono il guscio dell'avambraccio
FASCIA_DITO = 0.005       # m, alle nocche e alle articolazioni delle dita
RAGGIO_DITO = 0.014       # m, distanza massima dall'asse del dito


def componenti(R):
    bm = bmesh.new(); bm.from_mesh(R.data); bm.verts.ensure_lookup_table()
    visti = set(); out = []
    for v in bm.verts:
        if v.index in visti: continue
        st = [v]; c = []
        while st:
            x = st.pop()
            if x.index in visti: continue
            visti.add(x.index); c.append(x.index)
            for e in x.link_edges: st.append(e.other_vert(x))
        out.append(c)
    bm.free()
    return out


def sfuma_giunture(R, A):
    W = A.matrix_world
    ossa = A.data.bones
    def testa(n): return W @ ossa[n].head_local
    def coda(n): return W @ ossa[n].tail_local
    pre = 'mixamorig_Right'
    wa = testa(pre + 'Hand')
    asse = (wa - testa(pre + 'ForeArm')).normalized()

    giunture = []           # (punto, direzione, padre, figlio, fascia, raggio)
    for f in ('Thumb', 'Index', 'Middle', 'Ring', 'Pinky'):
        for k in range(1, 4):
            figlio = f'{pre}Hand{f}{k}'
            padre = pre + 'Hand' if k == 1 else f'{pre}Hand{f}{k - 1}'
            d = (coda(figlio) - testa(figlio)).normalized()
            giunture.append((testa(figlio), d, padre, figlio, FASCIA_DITO, RAGGIO_DITO))

    def liscia(x): x = min(1.0, max(0.0, x)); return x * x * (3 - 2 * x)

    idx = {g.index: g.name for g in R.vertex_groups}
    gruppo = {g.name: g for g in R.vertex_groups}
    def g(n): return gruppo.get(n) or R.vertex_groups.new(name=n)
    mw = R.matrix_world
    V = R.data.vertices
    avamb, mano = pre + 'ForeArm', pre + 'Hand'
    def rigido(v): return idx[max(v.groups, key=lambda e: e.weight).group] if v.groups else None
    def lungo(p): return (p - wa).dot(asse)

    # ---- confine del polso = fine del guscio dell'avambraccio
    comps = componenti(R)
    fine_guscio = None
    for c in comps:
        pts = [mw @ V[i].co for i in c]
        cen = sum(pts, Vector()) / len(pts)
        if lungo(cen) < 0 and max((p - cen).length for p in pts) > RAGGIO_GUSCIO:
            m = max(lungo(p) for p in pts)
            fine_guscio = m if fine_guscio is None else max(fine_guscio, m)
    confine = (fine_guscio if fine_guscio is not None else 0.0) + 0.002
    print(f"fine del guscio dell'avambraccio: {confine * 100:.1f} cm dal perno del polso")

    # ---- pezzi tra avambraccio e mano: rigidi sull'osso giusto secondo il confine
    polso_rigido = {}
    for c in comps:
        if any(rigido(V[i]) not in (avamb, mano) for i in c): continue
        cen = sum((mw @ V[i].co for i in c), Vector()) / len(c)
        b = mano if lungo(cen) > confine else avamb
        for i in c: polso_rigido[i] = b

    n_polso = n_dita = 0
    for v in V:
        if not v.groups: continue
        p = mw @ v.co
        pesi = None
        if v.index in polso_rigido:
            t = lungo(p) - confine
            if abs(t) < FASCIA_POLSO:
                w = liscia((t + FASCIA_POLSO) / (2 * FASCIA_POLSO))
                pesi = {avamb: 1 - w, mano: w}; n_polso += 1
            else:
                pesi = {polso_rigido[v.index]: 1.0}
        if pesi is None:
            rigido_v = rigido(v)
            for J, d, padre, figlio, fascia, raggio in giunture:
                if rigido_v not in (padre, figlio): continue
                s = (p - J).dot(d)
                if abs(s) >= fascia or ((p - J) - d * s).length >= raggio: continue
                w = liscia((s + fascia) / (2 * fascia))
                pesi = {padre: 1 - w, figlio: w}; n_dita += 1
                break
        if pesi is None: continue
        for e in list(v.groups): R.vertex_groups[e.group].remove([v.index])
        for n, w in pesi.items():
            if w > 1e-4: g(n).add([v.index], w, 'REPLACE')
    print(f"pesi sfumati: polso {n_polso} vertici, dita {n_dita} vertici")


if globals().get('SOLO_FUNZIONE') is not True:
    A = bpy.data.objects['Armature']; R = bpy.data.objects['Braccio_Robot_R']
    for pb in A.pose.bones:                       # export sempre dalla posa di riposo
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    sfuma_giunture(R, A)
    progetto = os.path.dirname(os.path.dirname(bpy.data.filepath))
    out = os.path.join(progetto, r"Assets\MODELS\TemporalBinding\Avatar_TB\Avatar_Robot.fbx")
    for o in bpy.context.scene.objects: o.select_set(False)
    objs = [A] + list(A.children)
    for o in objs: o.hide_set(False); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=False, mesh_smooth_type='FACE')
    bpy.ops.wm.save_mainfile()
    print("Avatar_Robot riesportato:", out)
