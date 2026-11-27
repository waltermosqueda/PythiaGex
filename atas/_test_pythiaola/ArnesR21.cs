using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PythiaLaOla;

// =========================================================================================================================
// LA OLA 1.0 (27-11-2026): EL ARNES CONTRA LA RONDA 21. Corre el MISMO codigo del indicador (GestorOla / CargaOla / TrabajoOla /
// OlaFotos / ClimaRueda) sobre la cinta de una rueda de ES y escribe los ola-*.csv (pivotes, fotos por vela y el clima de cada minuto);
// herramientas/auditar_pythiaola.py los compara con la referencia en Python de la R21 (r21_01_regimen.rasgos_dia + estado_minuto de la
// regla congelada para el clima; los pivotes y estados por segundo de la R19 para la ola y la marea).
// Dos entradas, porque la R19 no lee la cinta igual que ATAS:
//   r19    la cinta como la COMPACTA la R19 (r19_00_inventario.compactar): sin filas duplicadas exactas (se queda la primera) y ordenada
//          por hora de forma estable. Es la MISMA entrada que la referencia: tiene que dar 0 diferencias en todo.
//   cruda  la cinta tal como la entrega ATAS (el orden del archivo, con los duplicados): lo que va a ver el indicador en vivo. Las
//          diferencias contra la R21, si las hay, tienen que explicarse por las filas duplicadas (el auditor lo controla).
// Tres escenarios: 'pasada' (la jornada entera pedida como pasada) con las dos entradas, y 'vivo' con la cruda: el indicador arranca a
// las 11:00 NY con una orden creciendo, el historial llega despues con solape, el bufer se cose y desde ahi sigue orden por orden
// (OnCumulativeTrade + OnUpdateCumulativeTrade con las ordenes creciendo de a prints): el camino de ATAS en vivo.
// Uso: r21 <dir_salida> <AAAA-MM-DD> <cinta_jornada.csv> <cinta_previa.csv> [...]
// =========================================================================================================================
partial class Program
{
    /// <summary>La cinta con cada fila cruda (para sacar duplicados exactos como pandas.drop_duplicates sobre todas las columnas).</summary>
    static List<(string Linea, OrdenCinta O)> LeerCintaConLinea(string ruta)
    {
        var l = new List<(string, OrdenCinta)>(1 << 20);
        using var sr = new StreamReader(ruta, Encoding.ASCII, false, 1 << 20);
        string cab = sr.ReadLine();
        if (cab == null || !cab.StartsWith("t,primero,ultimo,vol,lado", StringComparison.Ordinal)) throw new InvalidDataException("encabezado inesperado en " + ruta);
        string linea; long nl = 1;
        while ((linea = sr.ReadLine()) != null)
        {
            nl++;
            OrdenCinta o;
            try
            {
                ReadOnlySpan<char> r = linea;
                o.T = Hora(Campo(ref r));
                o.Primero = decimal.Parse(Campo(ref r), NumberStyles.Float, Inv);
                o.Ultimo = decimal.Parse(Campo(ref r), NumberStyles.Float, Inv);
                o.Vol = decimal.Parse(Campo(ref r), NumberStyles.Float, Inv);
                o.Lado = int.Parse(Campo(ref r), NumberStyles.AllowLeadingSign, Inv);
                var p = Campo(ref r);
                o.Prints = p.Length > 0 ? int.Parse(p, NumberStyles.None, Inv) : 1;
            }
            catch (Exception e) { throw new InvalidDataException(Path.GetFileName(ruta) + " linea " + nl + ": " + e.Message); }
            l.Add((linea, o));
        }
        return l;
    }

    /// <summary>
    /// La compactacion de la R19: pandas drop_duplicates() (fila ENTERA igual: hora, precios, volumen, lado, prints y la punta antes y
    /// despues; se queda la primera) y despues argsort ESTABLE por la hora. Equivalente y sin guardar 900 mil filas: primero el orden
    /// estable por hora y despues, dentro de cada milisegundo, se descartan las filas ya vistas en ese milisegundo (dos filas iguales
    /// tienen la misma hora, y el orden estable conserva cual vino primero en el archivo).
    /// </summary>
    static List<OrdenCinta> ComoR19(List<(string Linea, OrdenCinta O)> filas, out int duplicados, out int desordenadas)
    {
        desordenadas = 0;
        for (int i = 1; i < filas.Count; i++) if (filas[i].O.T < filas[i - 1].O.T) desordenadas++;
        var idx = Enumerable.Range(0, filas.Count).ToArray();
        if (desordenadas > 0) idx = idx.OrderBy(i => filas[i].O.T).ThenBy(i => i).ToArray();   // estable
        var r = new List<OrdenCinta>(filas.Count);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        DateTime tAct = DateTime.MinValue;
        duplicados = 0;
        foreach (int i in idx)
        {
            var (lin, o) = filas[i];
            if (o.T != tAct) { tAct = o.T; vistos.Clear(); }
            if (!vistos.Add(lin)) { duplicados++; continue; }
            r.Add(o);
        }
        return r;
    }

    static int R21_(string[] a)
    {
        BajaPrioridad();
        if (a.Length < 5) throw new ArgumentException("uso: r21 <dir_salida> <AAAA-MM-DD> <cinta_jornada.csv> <cinta_previa.csv> [...]");
        string dir = a[1], dia = a[2], cintaDia = a[3];
        Directory.CreateDirectory(dir);
        var sw = Stopwatch.StartNew();
        var previas = new List<Barra>();
        for (int i = 4; i < a.Length; i++) previas.AddRange(VelasRthDeArchivo(a[i]));
        previas.Sort((p, q) => p.Ap.CompareTo(q.Ap));
        var filas = LeerCintaConLinea(cintaDia);
        var cruda = filas.Select(f => f.O).ToList();
        var r19 = ComoR19(filas, out int dup, out int des);
        filas = null;
        int clave = int.Parse(dia.Replace("-", ""), Inv);
        Console.WriteLine("r21 " + dia + ": " + (a.Length - 4) + " cintas previas (" + previas.Count + " velas RTH), jornada " + cruda.Count + " ordenes (" + dup
                          + " duplicadas exactas, " + des + " fuera de orden; la R19 usa " + r19.Count + "), lectura " + sw.ElapsedMilliseconds + " ms");
        var lista = new List<string>();
        var difs = new List<string>();
        foreach (var (nombre, os) in new[] { ("r19", r19), ("cruda", cruda) })
        {
            var v = VelasMinuto(os, out int[] b, out _);
            var e = new Escenario { Nombre = "r21-" + nombre + "-pasada", Inst = "ES", O = os, Velas = v, Bdo = b, Grafico = os.Count, Desde = 0, Hasta = 0, HistorialVacio = true };
            var s = new Simulador(e, previas, dir);
            // la vela que abre la jornada siguiente (sin cinta): la viva pasa a ser esa y la rueda pedida queda como pasada
            s.G.Rango(clave, out _, out DateTime hasta);
            var sig = hasta.AddHours(1);
            s.G.Vela(s.Base + v.Count, sig, sig.AddSeconds(1), os[os.Count - 1].Ultimo + 1, os[os.Count - 1].Ultimo);
            s.Latido(0); s.Responder(); s.Integrar();
            s.G.QuererMomento(s.Base + v.Count / 2);
            s.Latido(1); s.Responder(); s.Integrar();
            var c = s.G.Buscar(clave);
            if (c == null || !c.TieneMotor) { difs.Add(nombre + ": la carga de la rueda no quedo con motor"); continue; }
            s.Exportar(c, "");
            lista.Add(s.Csvs[s.Csvs.Count - 1] + "\t" + cintaDia + "\t" + nombre + "\tpasada");
            Resumir(nombre + " pasada", c, difs);
        }
        // vivo: la cruda llega en vivo desde las 11:00 NY (16:00 UTC en invierno), con una orden creciendo en el arranque
        {
            var os = cruda;
            var v = VelasMinuto(os, out int[] b, out _);
            var ini = new DateTime(int.Parse(dia.Substring(0, 4)), int.Parse(dia.Substring(5, 2)), int.Parse(dia.Substring(8, 2)), 11, 0, 0);
            var iniUtc = TimeZoneInfo.ConvertTimeToUtc(ini, ZonaNy);
            int i1 = Buscar(os, iniUtc);
            while (i1 + 1 < os.Count && !(os[i1].T == os[i1 + 1].T && os[i1].Prints >= 2 && os[i1 - 1].T < os[i1].T)) i1++;
            var e = new Escenario
            {
                Nombre = "r21-cruda-vivo", Inst = "ES", O = os, Velas = v, Bdo = b, Grafico = i1 + 1, Desde = i1, Hasta = os.Count,
                RespuestaEn = os[i1].T.AddSeconds(17), IntegrarEn = os[i1].T.AddSeconds(23)
            };
            e.ParcialEnHistorial.Add(i1); e.SoloActualizaciones.Add(i1);
            var s = new Simulador(e, previas, dir);
            s.Correr();
            var c = s.G.Viva;
            if (c == null || c.Clave != clave || !c.TieneMotor) difs.Add("vivo: la viva no es la rueda pedida o no tiene motor");
            else
            {
                s.Exportar(c, "");
                lista.Add(s.Csvs[s.Csvs.Count - 1] + "\t" + cintaDia + "\tcruda\tvivo");
                Resumir("cruda vivo", c, difs);
                if (c.Viejas != 0) difs.Add("vivo: " + c.Viejas + " actualizaciones viejas");
                Console.WriteLine("    vivo: arranque " + os[i1].T.ToString("HH:mm:ss.fff", Inv) + " UTC, " + (os.Count - i1) + " ordenes en vivo; "
                                  + string.Join(" | ", s.Log.Where(x => x.StartsWith("pedida") || x.StartsWith("respuesta"))));
            }
        }
        File.WriteAllText(Path.Combine(dir, "r21-csvs-" + dia + ".txt"), string.Join("\n", lista) + "\n", new UTF8Encoding(false));
        foreach (var x in difs) Console.WriteLine("  DIF " + x);
        Console.WriteLine("r21 " + dia + ": " + lista.Count + " csv en " + dir + ", " + sw.ElapsedMilliseconds + " ms; controles internos " + (difs.Count == 0 ? "OK" : difs.Count + " problema(s)"));
        return difs.Count == 0 ? 0 : 1;
    }

    static void Resumir(string et, CargaOla c, List<string> difs)
    {
        if (c.CortesTarde != 0) difs.Add(et + ": cortes tarde " + c.CortesTarde);
        if (c.CosturaDudosa) difs.Add(et + ": costura dudosa");
        var cl = c.Clima;
        int ten = 0, rot = 0, est = 0, dud = 0;
        if (cl != null) for (int m = 0; m < cl.Cerrados; m++) switch (cl.Minuto(m).Tipo) { case 'T': ten++; break; case 'R': rot++; break; case 'E': est++; break; default: dud++; break; }
        Console.WriteLine("  " + et.PadRight(14) + " X " + c.X.ToString(Inv) + " (" + c.FuenteX + ", mediana " + c.Mediana.ToString(Inv) + ", en double "
                          + ClimaRueda.D(c.MedClima) + ") | " + c.EstadoTexto() + " | pivotes " + c.Ola.Ola.Pivotes.Count + " / " + c.Marea.Ola.Pivotes.Count
                          + " | clima " + (cl == null ? "sin motor" : cl.Cerrados + " minutos" + (cl.Completo ? "" : " INCOMPLETO") + ": tendencia " + ten
                          + ", rotacion " + rot + ", estrecho " + est + ", dudoso " + dud));
        if (cl == null || cl.Cerrados != ClimaRueda.Minutos) difs.Add(et + ": el clima no cerro los 390 minutos (" + (cl?.Cerrados ?? 0) + ")");
    }
}
