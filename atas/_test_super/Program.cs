using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PythiaSuper;

// Genera casos sinteticos (con semillas fijas), los suma con SuperCalculos y escribe entrada + salida en JSON.
// La salida la recalcula por su cuenta herramientas/test_superindicador.py.
class Program
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(decimal v) => v.ToString(Inv);

    static BarraFp Vela(Random r, int bar, int tickCentro, int rango, bool huecos, bool empates)
    {
        var b = new BarraFp { Bar = bar, Hora = new DateTime(2026, 11, 23, 14, 0, 0).AddMinutes(bar) };
        b.HoraFin = b.Hora.AddSeconds(59);
        int n = 1 + r.Next(rango);
        b.TickMin = tickCentro - r.Next(n);
        b.Bid = new decimal[n]; b.Ask = new decimal[n]; b.Vol = new decimal[n];
        for (int i = 0; i < n; i++)
        {
            if (huecos && r.NextDouble() < 0.15) continue;   // nivel sin operaciones adentro de la vela
            decimal bi = empates ? 5 * r.Next(0, 4) : r.Next(0, 300);
            decimal a = empates ? 5 * r.Next(0, 4) : r.Next(0, 300);
            decimal e = r.NextDouble() < 0.05 ? r.Next(1, 3) : 0;   // volumen sin agresor (Between)
            b.Bid[i] = bi; b.Ask[i] = a; b.Vol[i] = bi + a + e;
            b.SumBid += bi; b.SumAsk += a; b.SumVol += bi + a + e;
            if (bi + a + e > 0) b.Niveles++;
        }
        b.DeltaAtas = b.SumAsk - b.SumBid; b.VolAtas = b.SumVol;
        return b;
    }

    static void Main(string[] args)
    {
        string salida = args.Length > 0 ? args[0] : "casos.json";
        var sb = new StringBuilder();
        sb.Append("[");
        int caso = 0;
        int[] ks = { 1, 2, 3, 4, 8 };
        int[] pcts = { 70, 68, 50, 100, 1 };
        int[] pasos = { 1, 2, 3 };
        for (int semilla = 1; semilla <= 400; semilla++)
        {
            var r = new Random(semilla);
            int velas = 1 + r.Next(60);
            bool huecos = semilla % 3 == 0, empates = semilla % 4 == 0;
            int centro = 27300 + r.Next(-200, 200);
            var lista = new List<BarraFp>();
            for (int v = 0; v < velas; v++) { centro += r.Next(-6, 7); lista.Add(Vela(r, v, centro, empates ? 6 : 25, huecos, empates)); }
            int k = ks[semilla % ks.Length], pct = pcts[(semilla / 5) % pcts.Length], paso = pasos[semilla % pasos.Length];
            var a = new Acum(k);
            foreach (var b in lista) a.Sumar(b);
            var p = Perfil.DeAcum(a, 0, velas - 1);
            Calculos.AreaDeValor(p, pct, paso);
            if (caso++ > 0) sb.Append(",\n");
            sb.Append("{\"semilla\":").Append(semilla).Append(",\"k\":").Append(k).Append(",\"pct\":").Append(pct).Append(",\"paso\":").Append(paso).Append(",\"velas\":[");
            for (int v = 0; v < lista.Count; v++)
            {
                var b = lista[v];
                if (v > 0) sb.Append(',');
                sb.Append("{\"tick_min\":").Append(b.TickMin).Append(",\"niveles\":[");
                for (int i = 0, q = 0; i < b.Vol.Length; i++)
                {
                    if (b.Vol[i] == 0 && b.Bid[i] == 0 && b.Ask[i] == 0) continue;
                    if (q++ > 0) sb.Append(',');
                    sb.Append('[').Append(b.TickMin + i).Append(',').Append(F(b.Bid[i])).Append(',').Append(F(b.Ask[i])).Append(',').Append(F(b.Vol[i])).Append(']');
                }
                sb.Append("]}");
            }
            sb.Append("],\"salida\":{\"fila_min\":").Append(p.FilaMin).Append(",\"filas\":").Append(p.Filas)
              .Append(",\"sum_bid\":").Append(F(p.SumBid)).Append(",\"sum_ask\":").Append(F(p.SumAsk)).Append(",\"sum_vol\":").Append(F(p.SumVol))
              .Append(",\"poc\":").Append(p.Poc).Append(",\"vah\":").Append(p.Vah).Append(",\"val\":").Append(p.Val).Append(",\"vol_av\":").Append(F(p.VolAv)).Append(",\"bid\":[");
            for (int i = 0; i < p.Filas; i++) { if (i > 0) sb.Append(','); sb.Append(F(p.Bid[i])); }
            sb.Append("],\"ask\":[");
            for (int i = 0; i < p.Filas; i++) { if (i > 0) sb.Append(','); sb.Append(F(p.Ask[i])); }
            sb.Append("],\"vol\":[");
            for (int i = 0; i < p.Filas; i++) { if (i > 0) sb.Append(','); sb.Append(F(p.Vol[i])); }
            sb.Append("]}}");
        }
        sb.Append("]");
        File.WriteAllText(salida, sb.ToString());
        // formato de numeros
        foreach (var v in new decimal[] { 0, 7, -7, 1019, -501, 99999, -99999, 100000, 123456, -123499, -123500, 2.5m, -0.25m })
            Console.WriteLine("NUM " + F(v) + " -> '" + Calculos.Numero(v, true) + "' / '" + Calculos.Numero(v, false) + "' / miles '" + Calculos.Miles(v) + "'");
        Console.WriteLine("casos " + caso + " -> " + salida);
        // piso con negativos
        foreach (var (t, k) in new[] { (0, 4), (3, 4), (4, 4), (-1, 4), (-4, 4), (-5, 4), (27315, 4) })
            Console.WriteLine("PISO " + t + "/" + k + " = " + Acum.Piso(t, k));
    }
}
