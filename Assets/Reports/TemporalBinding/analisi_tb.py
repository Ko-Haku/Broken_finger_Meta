# Analisi di un soggetto del pilota temporal binding: unisce i file di log (sessione ripresa con bloccoIniziale),
# tiene i trial non di riscaldamento con il ritardo reale entro un frame dal nominale, stime per condizione x delay,
# pendenze, confronti con test per permutazione stratificato per delay, grafico.
import sys, glob, os
import pandas as pd, numpy as np
from scipy import stats
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt

cart = r"D:\unity projects\Esperimenti 2027\Assets\Reports\TemporalBinding"
# uso: py -3 analisi_tb.py <file1.csv> [file2.csv ...] [--out cartella]  (file dello stesso soggetto, in ordine)
args = sys.argv[1:]
out = args[args.index("--out") + 1] if "--out" in args else cart
files = [a for i, a in enumerate(args) if a.endswith(".csv") and (i == 0 or args[i - 1] != "--out")]
files = [f if os.path.isabs(f) else os.path.join(cart, f) for f in files]
base = os.path.basename(files[0]).rsplit("_", 2)[0]
d = pd.concat([pd.read_csv(f).assign(File=os.path.basename(f)) for f in files], ignore_index=True)
d.to_csv(os.path.join(out, base + "_unito.csv"), index=False)

print("Sequenza:", d.OrdineBlocchi.iloc[0])
m = d[(d.Warmup == 0) & (d.Blocco > 0)].copy()
m["Errore_ms"] = m.DelayEffettivo_ms - m.DelayNominale_ms
fps = 1000 / 72
m["DelayOK"] = m.Errore_ms.abs() <= fps          # entro un frame del nominale
m["Zero_frame"] = m.FramePersi == 0
ordine = ["Mano_Attivo", "Mano_Passivo", "Tool_Attivo", "Tool_Passivo", "Esterno"]

print("\n== Qualità per blocco")
q = m.groupby("Condizione").agg(trial=("Stima_ms", "size"), zero_frame=("Zero_frame", "sum"),
                                 delay_entro_1frame=("DelayOK", "sum"), err_med=("Errore_ms", "median"),
                                 err_max=("Errore_ms", lambda x: x.abs().max()), fps_med=("FrameRate", "median"),
                                 rt_med=("RT_risposta_ms", "median"), lat_med=("Latenza_azione_ms", "median"),
                                 mov_mano_med=("MovimentoManoReale_mm", "median")).reindex(ordine)
print(q.round(1).to_string())

v = m[m.DelayOK].copy()
print(f"\nTrial analizzati (delay entro 1 frame): {len(v)} / {len(m)}")
# stime anomale per condizione (> 3 MAD dalla mediana per delay)
def flag(g):
    med = g.Stima_ms.median(); mad = stats.median_abs_deviation(g.Stima_ms, scale="normal")
    return (g.Stima_ms - med).abs() > 3 * max(mad, 1)
v["Anomala"] = v.groupby(["Condizione", "DelayNominale_ms"], group_keys=False).apply(flag)
print("Stime anomale (>3 MAD):", int(v.Anomala.sum()))

print("\n== Stima media (ms) per condizione x delay  [media ± ES, n]")
t = v.groupby(["Condizione", "DelayNominale_ms"]).Stima_ms.agg(["mean", "sem", "median", "size"]).round(0)
print(t.to_string())
piv = v.groupby(["Condizione", "DelayNominale_ms"]).Stima_ms.mean().unstack().reindex(ordine)
piv["Media"] = piv.mean(axis=1)
print("\n", piv.round(0).to_string())

print("\n== Pendenza stima~delay (sensibilità all'intervallo) e intercetta")
for c in ordine:
    g = v[v.Condizione == c]
    r = stats.linregress(g.DelayNominale_ms, g.Stima_ms)
    print(f"{c:13s} pendenza {r.slope:.2f}  intercetta {r.intercept:5.0f}  r={r.rvalue:.2f}  stima-reale media {(g.Stima_ms-g.DelayNominale_ms).mean():+5.0f} ms")

def conf(a, b, nome):
    # differenza media (a-b) su stima, mediata sui delay; test per permutazione stratificato per delay
    A, B = v[v.Condizione == a], v[v.Condizione == b]
    diff = np.mean([A[A.DelayNominale_ms == k].Stima_ms.mean() - B[B.DelayNominale_ms == k].Stima_ms.mean() for k in (150, 450, 750)])
    rng = np.random.default_rng(1); null = []
    sub = pd.concat([A, B])
    for _ in range(5000):
        s = sub.copy()
        s["Condizione"] = s.groupby("DelayNominale_ms").Condizione.transform(lambda x: rng.permutation(x.values))
        AA, BB = s[s.Condizione == a], s[s.Condizione == b]
        null.append(np.mean([AA[AA.DelayNominale_ms == k].Stima_ms.mean() - BB[BB.DelayNominale_ms == k].Stima_ms.mean() for k in (150, 450, 750)]))
    p = (np.sum(np.abs(null) >= abs(diff)) + 1) / (len(null) + 1)
    sd = sub.groupby("DelayNominale_ms").Stima_ms.transform(lambda x: x - x.mean()).std()
    print(f"{nome:42s} {diff:+6.0f} ms   d≈{diff/sd:+.2f}   p(perm)={p:.3f}")

print("\n== Confronti (negativo = intervalli stimati più corti = più binding nel primo)")
conf("Mano_Attivo", "Mano_Passivo", "Mano: Attivo - Passivo (binding intenzionale)")
conf("Tool_Attivo", "Tool_Passivo", "Pinzetta: Attivo - Passivo")
conf("Mano_Attivo", "Esterno", "Mano attivo - Esterno")
conf("Tool_Attivo", "Esterno", "Pinzetta attivo - Esterno")
conf("Mano_Passivo", "Esterno", "Mano passivo - Esterno")
conf("Tool_Passivo", "Esterno", "Pinzetta passivo - Esterno")
conf("Mano_Attivo", "Tool_Attivo", "Attivo: Mano - Pinzetta")
ba_m = piv.loc["Mano_Attivo", "Media"] - piv.loc["Mano_Passivo", "Media"]
ba_t = piv.loc["Tool_Attivo", "Media"] - piv.loc["Tool_Passivo", "Media"]
print(f"Interazione (binding mano - binding pinzetta): {ba_m - ba_t:+.0f} ms")

print("\n== Ancoraggio allo slider: correlazione SliderStart-Stima (residui per delay)")
v["res"] = v.Stima_ms - v.groupby(["Condizione", "DelayNominale_ms"]).Stima_ms.transform("mean")
r = stats.spearmanr(v.SliderStart, v.res); print(f"rho={r.statistic:.2f} p={r.pvalue:.3f}")
print("\n== Deriva nel blocco: correlazione Trial-residuo per condizione")
for c in ordine:
    g = v[v.Condizione == c]; r = stats.spearmanr(g.Trial, g.res); print(f"{c:13s} rho={r.statistic:+.2f} p={r.pvalue:.3f}")

print("\n== Robustezza: solo trial con 0 frame persi")
print(m[m.Zero_frame].groupby("Condizione").Stima_ms.agg(["size", "mean"]).reindex(ordine).round(0).to_string())
print("\n== Robustezza: tutti i trial (anche delay sbagliati), mediane")
print(m.groupby(["Condizione"]).Stima_ms.median().reindex(ordine).to_string())

# grafico
fig, ax = plt.subplots(1, 2, figsize=(11, 4.5))
stili = {"Mano_Attivo": ("#1f6feb", "-"), "Mano_Passivo": ("#1f6feb", "--"), "Tool_Attivo": ("#d1495b", "-"),
         "Tool_Passivo": ("#d1495b", "--"), "Esterno": ("#555555", ":")}
for c in ordine:
    g = v[v.Condizione == c].groupby("DelayNominale_ms").Stima_ms.agg(["mean", "sem"])
    col, ls = stili[c]
    ax[0].errorbar(g.index + (ordine.index(c) - 2) * 8, g["mean"], g["sem"], color=col, ls=ls, marker="o", capsize=3, label=c)
ax[0].plot([100, 800], [100, 800], color="#bbb", lw=0.8, zorder=0)
ax[0].set(xlabel="Intervallo reale (ms)", ylabel="Stima (ms)", title="Stima per condizione (media ± ES)", xticks=[150, 450, 750])
ax[0].legend(fontsize=8)
medie = piv.Media
ax[1].bar(range(5), medie, color=[stili[c][0] for c in ordine], alpha=[1, .5, 1, .5, .6] if False else 0.8)
for i, c in enumerate(ordine):
    g = v[v.Condizione == c]
    ax[1].scatter(np.full(len(g), i) + np.random.uniform(-.25, .25, len(g)), g.Stima_ms, s=6, color="k", alpha=.3)
ax[1].set_xticks(range(5), [c.replace("_", "\n") for c in ordine]); ax[1].set(ylabel="Stima (ms)", title="Stima media sui 3 delay")
plt.tight_layout(); plt.savefig(os.path.join(out, base + "_grafico.png"), dpi=130)
