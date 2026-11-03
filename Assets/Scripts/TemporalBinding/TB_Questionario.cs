using System.Collections.Generic;
using System.Linq;

// Questionario di embodiment/agency a fine blocco (da NEW_Questionario_v2, senza le 4 domande su dolore e disgusto).
// Likert 1-7 sullo slider (1 = fortemente in disaccordo, 7 = fortemente d'accordo), conferma con X.
// "____" diventa l'effettore del blocco: "mano" per Mano ed Esterno (si vede la mano), "pinzetta" per Tool.
public static class TB_Questionario
{
    public struct Item { public string id, testo; public Item(string i, string t) { id = i; testo = t; } }

    public const int Min = 1, Max = 7;
    public const string AncoraMin = "Fortemente in disaccordo", AncoraMax = "Fortemente d'accordo";

    public static readonly Item[] Items =
    {
        new Item("D1",  "Ho sentito come se la ____ virtuale fosse la mia mano"),
        new Item("D2",  "Sembrava che la ____ virtuale che vedevo fosse di qualcun altro"),
        new Item("D3",  "Sembrava che potessi avere più di una mano"),
        new Item("D6",  "Sembrava che potessi controllare la ____ virtuale come se fosse la mia mano"),
        new Item("D7",  "I movimenti della ____ virtuale erano causati dai miei movimenti"),
        new Item("D8",  "Sembrava che i movimenti della ____ virtuale influenzassero i miei"),
        new Item("D9",  "Sembrava che la ____ virtuale si muovesse da sola"),
        new Item("D14", "Ho sentito come se la mia mano fosse localizzata dove vedevo la ____ virtuale"),
        new Item("D15", "Mi sono sentito fuori dal mio corpo"),
        new Item("D17", "Sembrava che la mia mano (reale) si stesse trasformando in una ____ virtuale"),
        new Item("D18", "A un certo punto ho avuto l'impressione che la mia mano reale stesse assumendo la postura o la forma della ____ virtuale che vedevo"),
        new Item("D19", "A un certo punto mi è sembrato che la ____ virtuale somigliasse alla mia mano (reale), per forma, tono della pelle o altri aspetti visivi"),
        new Item("D20", "Mi sembrava di indossare abiti diversi da quelli con cui ero arrivato al laboratorio"),
    };

    public static string Effettore(TB_Condizione c) => c.effettore == TB_Effettore.Tool ? "pinzetta" : "mano";

    public static string Testo(Item it, TB_Condizione c) => it.testo.Replace("____", Effettore(c));

    // Ordine casuale ma riproducibile: dipende solo da numero soggetto e blocco (diverso a ogni blocco e soggetto)
    public static List<Item> Ordine(int numeroSoggetto, int blocco)
    {
        var rng = new System.Random(numeroSoggetto * 7919 + blocco * 104729);
        var l = Items.ToList();
        for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
        return l;
    }
}
