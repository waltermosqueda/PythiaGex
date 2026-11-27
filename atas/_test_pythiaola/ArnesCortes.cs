using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PythiaLaOla;

// MODO "cortes" del arnes (La Ola 1.0b, 27-11-2026): el DetectorCortes (atas/PythiaOla/OlaCortes.cs) sobre el LOG REAL de ATAS de un dia y una
// cinta en vivo SIMULADA (una orden cada 250 ms salvo en los huecos que se le pasan), con el mismo ritmo que el indicador: el log se lee cada
// 2 s y el detector se revisa cada 250 ms. Imprime cada re-pedido (hora, motivo, corte) y al final los totales.
//   cortes <app_AAAAMMDD.log> <horas_AR_a_sumar_para_UTC> <fin_cinta_utc HH:mm:ss> [<hueco_desde_utc HH:mm:ss.fff>-<hueco_hasta_utc> ...]
// Ejemplo (27-11): cortes app_20261127.log 3 18:15:00 15:46:53.9-15:47:20 15:50:48-15:50:56 16:06:00-16:06:30
partial class Program
{
    static int Cortes_(string[] a)
    {
        string log = a[1]; double masHoras = double.Parse(a[2], CultureInfo.InvariantCulture);
        var lineas = new List<(DateTime T, string L)>();
        DateTime dia = DateTime.MinValue;
        foreach (var l in File.ReadLines(log))
        {
            if (l.Length < 23 || !DateTime.TryParseExact(l.Substring(0, 23), "yyyy-MM-dd HH:mm:ss,fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) continue;
            t = t.AddHours(masHoras); if (dia == DateTime.MinValue) dia = t.Date;
            lineas.Add((t, l));
        }
        DateTime H(string s) => dia.Add(TimeSpan.ParseExact(s, new[] { @"hh\:mm\:ss\.FFF", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture));
        DateTime finCinta = H(a[3]);
        var huecos = new List<(DateTime D, DateTime Hh)>();
        for (int i = 4; i < a.Length; i++) { var p = a[i].Split('-'); huecos.Add((H(p[0]), H(p[1]))); }
        var cortes = lineas.FindAll(x => DetectorCortes.EsCorte(x.L));
        Console.WriteLine("log: " + lineas.Count + " lineas con hora; con corte: " + cortes.Count);
        foreach (var c in cortes) Console.WriteLine("   " + c.T.ToString("HH:mm:ss.fff") + " UTC  " + (c.L.Length > 23 ? c.L.Substring(24, Math.Min(90, c.L.Length - 24)) : ""));
        var d = new DetectorCortes();
        DateTime ini = lineas[0].T, fin = lineas[lineas.Count - 1].T.AddMinutes(10);
        int li = 0; DateTime proxLectura = ini; var pend = new System.Text.StringBuilder();
        int repedidos = 0;
        for (var t = ini; t <= fin; t = t.AddMilliseconds(250))
        {
            bool enHueco = t >= finCinta; foreach (var h in huecos) if (t >= h.D && t < h.Hh) enHueco = true;
            if (!enHueco) d.Evento(t);
            while (li < lineas.Count && lineas[li].T <= t) { pend.Append(lineas[li].L).Append('\n'); li++; }
            if (t >= proxLectura) { proxLectura = t.AddSeconds(2); if (pend.Length > 0) { d.Log(pend.ToString(), t); pend.Clear(); } }
            var ny = t.AddHours(-5);   // EST (sin horario de verano en noviembre)
            bool enRueda = ny.DayOfWeek != DayOfWeek.Saturday && ny.DayOfWeek != DayOfWeek.Sunday && ny.TimeOfDay >= new TimeSpan(9, 30, 0) && ny.TimeOfDay < new TimeSpan(16, 0, 0);
            if (d.Revisar(t, enRueda, true))
            {
                repedidos++;
                Console.WriteLine("RE-PEDIDO " + repedidos + " a las " + t.ToString("HH:mm:ss.fff") + " UTC (" + t.AddHours(-masHoras).ToString("HH:mm:ss") + " del log) | corte "
                                  + d.UltimoCorte.ToString("HH:mm:ss.fff") + " UTC | motivo: " + d.UltimoMotivo);
                d.FijarAviso("corte de datos");
            }
        }
        Console.WriteLine("total: cortes vistos " + d.Vistos + ", re-pedidos " + d.Repedidos + ", pendiente al final: " + (d.Pendiente == DateTime.MinValue ? "no" : d.Pendiente.ToString("HH:mm:ss")));
        return 0;
    }
}
