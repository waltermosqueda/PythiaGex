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
        Jornadas_(args.Length > 1 ? args[1] : "jornadas.json");
    }

    // 1.5: casos sinteticos de JORNADAS: velas en UTC (como las da ATAS) -> hora NY con la zona de Windows (igual que el
    // indicador) -> lista de jornadas, rangos por parte, "a la vista" y "N atras". Python lo rehace con su propia base de husos.
    static void Jornadas_(string salida)
    {
        var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var sb = new StringBuilder("[");
        int casos = 0;
        int[] marcos = { 1, 1, 1, 5, 30, 60 };
        var inicios = new[] { new DateTime(2026, 3, 5), new DateTime(2026, 10, 28), new DateTime(2026, 11, 23), new DateTime(2026, 11, 18), new DateTime(2026, 7, 1), new DateTime(2026, 12, 30) };
        for (int semilla = 1; semilla <= 120; semilla++)
        {
            var r = new Random(1000 + semilla);
            int marco = marcos[semilla % marcos.Length];
            // arranque en UTC a cualquier minuto (multiplo del marco) de un dia de los de la lista + desplazamiento al azar
            var t = inicios[semilla % inicios.Length].AddMinutes(r.Next(0, 3 * 24 * 60) / marco * marco);
            int dias = 2 + r.Next(5);
            var fin = t.AddDays(dias);
            var horas = new List<DateTime>();
            while (t < fin)
            {
                var h = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t, DateTimeKind.Utc), ny);
                bool cerrado = h.DayOfWeek == DayOfWeek.Saturday || (h.DayOfWeek == DayOfWeek.Friday && h.TimeOfDay >= new TimeSpan(17, 0, 0))
                            || (h.DayOfWeek == DayOfWeek.Sunday && h.TimeOfDay < new TimeSpan(18, 0, 0))
                            || (h.TimeOfDay >= new TimeSpan(17, 0, 0) && h.TimeOfDay < new TimeSpan(18, 0, 0));
                // a veces una vela "imposible" (en el corte o en fin de semana) para probar las reglas de borde
                bool rara = r.NextDouble() < 0.002;
                if ((!cerrado || rara) && r.NextDouble() > 0.03) horas.Add(t);   // 3 % de minutos sin operaciones
                t = t.AddMinutes(marco);
            }
            var js = new List<Jornada>();
            for (int b = 0; b < horas.Count; b++)
                Jornadas.Agregar(js, b, TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(horas[b], DateTimeKind.Utc), ny));
            if (casos++ > 0) sb.Append(",\n");
            sb.Append("{\"semilla\":").Append(semilla).Append(",\"marco\":").Append(marco).Append(",\"utc\":[");
            for (int b = 0; b < horas.Count; b++) { if (b > 0) sb.Append(','); sb.Append('"').Append(horas[b].ToString("yyyy-MM-ddTHH:mm", Inv)).Append('"'); }
            sb.Append("],\"lista\":[");
            for (int k = 0; k < js.Count; k++)
            {
                var j = js[k];
                if (k > 0) sb.Append(',');
                sb.Append('[').Append(j.Clave).Append(',').Append(j.Primera).Append(',').Append(j.Ultima).Append(',').Append(j.NochePrimera).Append(',')
                  .Append(j.NocheUltima).Append(',').Append(j.RthPrimera).Append(',').Append(j.RthUltima).Append(",\"").Append(Jornadas.Rotulo(j.Clave)).Append("\"")
                  .Append(",\"").Append(Jornadas.InicioNominal(j.Clave, Jornadas.ParteCompleta).ToString("yyyy-MM-ddTHH:mm", Inv)).Append("\"")
                  .Append(",\"").Append(Jornadas.InicioNominal(j.Clave, Jornadas.ParteRth).ToString("yyyy-MM-ddTHH:mm", Inv)).Append("\"]");
            }
            sb.Append("],\"vista\":[");
            for (int q = 0; q < 40; q++)
            {
                int n = horas.Count; if (n == 0) break;
                int a = r.Next(n), w = 1 + r.Next(Math.Max(1, n / 2)), parte = r.Next(3);
                int b0 = Math.Max(0, a - w / 2), b1 = Math.Min(n - 1, b0 + w);
                if (q > 0) sb.Append(',');
                sb.Append('[').Append(b0).Append(',').Append(b1).Append(',').Append(parte).Append(',').Append(Jornadas.AlaVista(js, b0, b1, parte)).Append(']');
            }
            // 1.6: "a la vista" con la regla del borde (la mitad de las consultas con la ultima vela a la vista)
            sb.Append("],\"borde\":[");
            for (int q = 0; q < 30; q++)
            {
                int n = horas.Count; if (n == 0) break;
                int ult = n - 1, w = 1 + r.Next(Math.Max(1, n / 2)), parte = r.Next(3);
                int b1 = q % 2 == 0 ? ult : r.Next(n), b0 = Math.Max(0, b1 - w);
                if (q > 0) sb.Append(',');
                sb.Append('[').Append(b0).Append(',').Append(b1).Append(',').Append(ult).Append(',').Append(parte).Append(',')
                  .Append(Jornadas.AlaVistaConBorde(js, b0, b1, ult, parte)).Append(']');
            }
            // 1.7: "al momento": la parte de jornada como estaba a la altura de una vela m cualquiera
            sb.Append("],\"momento\":[");
            for (int q = 0; q < 60; q++)
            {
                int n = horas.Count; if (n == 0) break;
                int m = r.Next(n), parte = r.Next(3);
                bool ok = Jornadas.AlMomento(js, m, parte, out int im, out int dm, out int hm);
                if (q > 0) sb.Append(',');
                sb.Append('[').Append(m).Append(',').Append(parte).Append(',').Append(ok ? im : -1).Append(',').Append(dm).Append(',').Append(hm).Append(']');
            }
            sb.Append("],\"atras\":[");
            for (int q = 0; q <= js.Count + 1; q++) { if (q > 0) sb.Append(','); sb.Append(Jornadas.PorAtras(js, q)); }
            sb.Append("]}");
        }
        sb.Append("]");
        File.WriteAllText(salida, sb.ToString());
        Console.WriteLine("jornadas: " + casos + " casos -> " + salida);
    }
}
