using System;

// Tipi condivisi del paradigma di temporal binding (stima dell'intervallo, Kong et al. 2024).
public enum TB_Effettore { Mano, Tool, Nessuno }
public enum TB_Agency { Attivo, Passivo, Esterno }

[Serializable]
public struct TB_Condizione
{
    public TB_Effettore effettore;
    public TB_Agency agency;

    public TB_Condizione(TB_Effettore e, TB_Agency a) { effettore = e; agency = a; }

    public string Nome => agency == TB_Agency.Esterno ? "Esterno" : $"{effettore}_{agency}";

    // Le 5 condizioni del pilota: 2x2 (Mano/Tool x Attivo/Passivo) + evento esterno.
    public static readonly TB_Condizione[] Tutte =
    {
        new TB_Condizione(TB_Effettore.Mano, TB_Agency.Attivo),
        new TB_Condizione(TB_Effettore.Mano, TB_Agency.Passivo),
        new TB_Condizione(TB_Effettore.Tool, TB_Agency.Attivo),
        new TB_Condizione(TB_Effettore.Tool, TB_Agency.Passivo),
        new TB_Condizione(TB_Effettore.Nessuno, TB_Agency.Esterno),
    };
}

public class TB_Trial
{
    public int blocco;
    public int indice;
    public bool warmup;
    public TB_Condizione condizione;
    public int delayNominaleMs;

    // misurati
    public double tFixOff;          // fine del mirino = segnale di via
    public double tEvento;          // primo evento: chiusura (mano/tool) o pressione automatica (esterno)
    public double tLuce;            // accensione effettiva della lampadina
    public double tSliderOn;
    public double tRisposta;
    public int stimaMs = -1;
    public int sliderStart;
    public float movimentoManoRealeMm; // solo passivo/esterno: quanto si è mossa la mano reale
    public int clipIndex = -1;          // clip cinematica usata nel passivo

    public double DelayEffettivoMs => (tLuce - tEvento) * 1000.0;
    public double LatenzaAzioneMs => (tEvento - tFixOff) * 1000.0;
    public double RtRispostaMs => (tRisposta - tSliderOn) * 1000.0;
}
