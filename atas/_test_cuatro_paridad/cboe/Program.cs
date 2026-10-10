// Program.cs — arnes del modulo cboe (B2) de PythiaGex 4.1. Sin ATAS. Prioridad BelowNormal.
// Modos:
//   crudos <salida> <crudo.json.gz>...      construir + medir + linea flaca de cada crudo -> <salida>/<nombre>.cs.json (lo compara comparar_cboe.py)
//   lineas <salida.json> <cadena-*.jsonl.gz>... lee archivos (cboe-local o propios) con el lector del modulo -> resumen por linea
//   py <salida.txt>                          numeros al azar con round()/repr() del modulo (lo compara comparar_cboe.py)
//   hilo <carpeta_trabajo> <crudos_dir>      el hilo entero con un HTTP FALSO que sirve crudos guardados (sin red): dedup, archivo,
//                                            relectura, seguidor por mutex, fallos y espera, parar <= 2 s
//   bajar <salida> [--otra-vez]              UNA bajada REAL por ticker (_NDX, QQQ, TQQQ) por el camino de produccion; guarda el crudo
//                                            comprimido para comparar con Python. Se niega a repetir si ya se hizo (regla: una por ticker).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using PythiaGexCuatro.Cboe;
using PythiaGexCuatro.Familia;

public static class Program
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Main(string[] a)
    {
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
        Thread.CurrentThread.CurrentCulture = Inv;
        if (a.Length == 0) { Console.WriteLine("modos: crudos | lineas | py | hilo | bajar"); return 2; }
        try
        {
            switch (a[0])
            {
                case "crudos": return Crudos(a[1], a.Skip(2).ToArray());
                case "lineas": return Lineas(a[1], a.Skip(2).ToArray());
                case "py": return PyNums(a[1]);
                case "hilo": return Hilo(a[1], a[2]);
                case "bajar": return Bajar(a[1], a.Contains("--otra-vez"));
                case "historia": return Historia(a[1], a[2], double.Parse(a[3], Inv));
                default: Console.WriteLine("modo desconocido"); return 2;
            }
        }
        catch (Exception e) { Console.WriteLine("FALLO: " + e); return 1; }
    }

    static byte[] Descomprimir(byte[] gz)
    {
        if (gz.Length >= 2 && gz[0] == 0x1F && gz[1] == 0x8B)
        {
            using var ms = new MemoryStream(gz);
            using var z = new GZipStream(ms, CompressionMode.Decompress);
            using var o = new MemoryStream();
            z.CopyTo(o);
            return o.ToArray();
        }
        return gz;
    }

    static string IsoUs(DateTime u) => u.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff", Inv) + "+00:00";

    // ------------------------------------------------------------------ crudos

    static int Crudos(string salida, string[] crudos)
    {
        Directory.CreateDirectory(salida);
        foreach (var p in crudos)
        {
            var json = Descomprimir(File.ReadAllBytes(p));
            var sw = Stopwatch.StartNew();
            var cr = CrudoCboe.Leer(json);
            long tLeer = sw.ElapsedMilliseconds; sw.Restart();
            if (!CboeCadena.ParsearTs(cr.Timestamp, out var tsu)) throw new Exception("ts ilegible en " + p);
            var ahora = tsu.AddSeconds(45);                          // como medir_quirk.py: ts + 45 s
            var nombreAhora = Path.GetFileName(p) + ".ahora";
            var cad = CboeCadena.Construir(cr, ahora);
            long tCons = sw.ElapsedMilliseconds; sw.Restart();
            var hoy = DateTime.Now.Date;
            var b = CboeCadena.Medir(cr, hoy);
            long tMed = sw.ElapsedMilliseconds; sw.Restart();
            var linea = CboeCadena.LineaFlaca(cad, cr, b, ahora, null);
            long tLin = sw.ElapsedMilliseconds; sw.Restart();
            var rel = CboeCadena.DeLinea(linea);
            long tRel = sw.ElapsedMilliseconds;
            var sbc = new StringBuilder();
            CboeCadena.EscribirCadena(sbc, cad, 1e9);               // TODAS las filas de construir (<= 14 dias)
            var o = new StringBuilder();
            o.Append("{\"crudo\":"); PyCboe.Str(o, Path.GetFullPath(p));
            o.Append(",\"ahora\":"); PyCboe.Str(o, IsoUs(ahora));
            o.Append(",\"hoy\":"); PyCboe.Str(o, hoy.ToString("yyyy-MM-dd", Inv));
            o.Append(",\"n_contratos\":").Append(cr.N).Append(",\"validos\":").Append(cr.ContratosValidos);
            o.Append(",\"ms\":{\"leer\":").Append(tLeer).Append(",\"construir\":").Append(tCons).Append(",\"medir\":").Append(tMed).Append(",\"linea\":").Append(tLin).Append(",\"releer\":").Append(tRel).Append('}');
            o.Append(",\"linea\":"); PyCboe.Str(o, linea);
            o.Append(",\"cadena_completa\":").Append(sbc);
            o.Append(",\"medir\":");
            if (b == null) o.Append("null");
            else
            {
                o.Append("{\"base\":").Append(PyCboe.Repr(b.Base)).Append(",\"contado\":").Append(PyCboe.Repr(b.Contado)).Append(",\"forward\":").Append(PyCboe.Repr(b.Forward))
                 .Append(",\"residuo\":").Append(PyCboe.Repr(b.Residuo)).Append(",\"residuo_ticks\":").Append(PyCboe.Repr(b.ResiduoTicks))
                 .Append(",\"venc\":\"").Append(b.Venc).Append("\",\"cercano\":\"").Append(b.Cercano).Append("\",\"puntos\":").Append(b.Puntos)
                 .Append(",\"muestras\":").Append(b.MuestrasFut).Append(",\"dispersion\":").Append(PyCboe.Repr(b.DispersionFut)).Append(",\"curva\":[");
                for (int i = 0; i < b.Curva.Count; i++)
                {
                    var c = b.Curva[i];
                    if (i > 0) o.Append(',');
                    o.Append("[\"").Append(c.F).Append("\",").Append(c.Dias).Append(',').Append(PyCboe.Repr(c.Fwd)).Append(',').Append(c.Muestras).Append(',').Append(PyCboe.Repr(c.Disp)).Append(']');
                }
                o.Append("]}");
            }
            o.Append(",\"releida\":");
            if (rel == null) o.Append("null");
            else
            {
                o.Append("{\"generado\":"); PyCboe.Str(o, IsoUs(rel.GeneradoUtc));
                o.Append(",\"t_dato\":"); PyCboe.Str(o, IsoUs(rel.DatoUtc));
                o.Append(",\"spot\":").Append(PyCboe.Repr(rel.Spot)).Append(",\"oi_total\":").Append(PyCboe.Repr(rel.OiTotal)).Append(",\"n_filas\":").Append(rel.NFilas)
                 .Append(",\"prev_day_close\":").Append(rel.Sub == null ? "null" : PyCboe.Repr(rel.Sub.PrevDayClose)).Append('}');
            }
            o.Append('}');
            var dst = Path.Combine(salida, Path.GetFileName(p) + ".cs.json");
            File.WriteAllText(dst, o.ToString(), new UTF8Encoding(false));
            Console.WriteLine(Path.GetFileName(p) + ": " + cr.N + " contratos, filas " + cad.Filas.Length + " (flaca " + (rel?.NFilas ?? 0) + "), venc " + cad.VencF.Length
                              + ", base_cruda " + (b == null ? "null" : PyCboe.Repr(b.Base) + " err " + PyCboe.Repr(b.ResiduoTicks)) + " | ms leer " + tLeer + " construir " + tCons + " medir " + tMed + " linea " + tLin + " releer " + tRel);
        }
        return 0;
    }

    // ------------------------------------------------------------------ lineas

    static int Lineas(string salida, string[] archivos)
    {
        var o = new StringBuilder("{");
        bool p1 = true;
        foreach (var p in archivos)
        {
            var sw = Stopwatch.StartNew();
            var L = CboeArchivo.LeerDia(p, null, out int ileg);
            long ms = sw.ElapsedMilliseconds;
            Console.WriteLine(Path.GetFileName(p) + ": " + L.Count + " lineas leidas, " + ileg + " ilegibles/sin filas, " + ms + " ms");
            if (!p1) o.Append(','); p1 = false;
            PyCboe.Str(o, Path.GetFileName(p)); o.Append(":[");
            for (int i = 0; i < L.Count; i++)
            {
                var x = L[i];
                if (i > 0) o.Append(',');
                double sumaK = 0, sumaVol = 0, sumaIv = 0;
                foreach (var f in x.Filas) { sumaK += f.K * (f.V + 1); sumaVol += f.VolC - f.VolP; sumaIv += f.IvC + 2 * f.IvP; }
                o.Append("{\"generado\":"); PyCboe.Str(o, IsoUs(x.GeneradoUtc));
                o.Append(",\"ts\":"); PyCboe.Str(o, x.CadenaTs);
                o.Append(",\"t_dato\":"); PyCboe.Str(o, IsoUs(x.DatoUtc));
                o.Append(",\"spot\":").Append(double.IsNaN(x.Spot) ? "0.0" : PyCboe.Repr(x.Spot));
                o.Append(",\"oi_total\":").Append(PyCboe.Repr(x.OiTotal));
                o.Append(",\"err\":").Append(double.IsNaN(x.BaseErr) ? "null" : PyCboe.Repr(x.BaseErr));
                o.Append(",\"base_cruda\":").Append(double.IsNaN(x.BaseCruda) ? "null" : PyCboe.Repr(x.BaseCruda));
                o.Append(",\"n_filas\":").Append(x.NFilas);
                o.Append(",\"dias\":[").Append(string.Join(",", x.Dias.Select(PyCboe.Repr))).Append(']');
                o.Append(",\"suma_k\":").Append(PyCboe.Repr(sumaK)).Append(",\"suma_vol\":").Append(PyCboe.Repr(sumaVol)).Append(",\"suma_iv\":").Append(PyCboe.Repr(sumaIv));
                o.Append('}');
            }
            o.Append(']');
        }
        o.Append('}');
        File.WriteAllText(salida, o.ToString(), new UTF8Encoding(false));
        return 0;
    }

    // ------------------------------------------------------------------ round/repr

    static int PyNums(string salida)
    {
        var rnd = new Random(20261008);
        var sb = new StringBuilder();
        var casos = new List<double> { 0.125, 0.375, 2.675, 1.005, 0.5, 1.5, 2.5, -2.5, -0.125, 1e-5, 1e-4, 0.0001234, 1e16, 1e15, 123456789012345678.0, 1.0 / 3, 2.0 / 3,
                                        30725.81, 238.36, 0.8811, 80.6554, 1e-300, 5e-324, 1.7976931348623157e308, 0.03125, 0.09375, 12.345, 99.995, 0.45, 1234.5 };
        for (int i = 0; i < 4000; i++)
        {
            switch (i % 5)
            {
                case 0: casos.Add(Math.Round(rnd.NextDouble() * 40000, rnd.Next(0, 6))); break;           // precios tipo strike/spot
                case 1: casos.Add(rnd.NextDouble()); break;                                                  // iv
                case 2: casos.Add((rnd.Next(0, 2000000) + 0.5) / Math.Pow(10, rnd.Next(1, 6))); break;      // medios exactos o casi
                case 3: casos.Add(rnd.Next(0, 100000) / 8.0); break;                                         // diadicos (empates exactos)
                default: casos.Add((rnd.NextDouble() - 0.5) * Math.Pow(10, rnd.Next(-8, 18))); break;
            }
        }
        foreach (var x in casos)
            sb.Append(x.ToString("R", Inv)).Append('\t').Append(PyCboe.Repr(x)).Append('\t').Append(PyCboe.Repr(PyCboe.Round(x, 1))).Append('\t').Append(PyCboe.Repr(PyCboe.Round(x, 2))).Append('\t').Append(PyCboe.Repr(PyCboe.Round(x, 4))).Append('\n');
        File.WriteAllText(salida, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine(casos.Count + " numeros escritos en " + salida);
        return 0;
    }

    // ------------------------------------------------------------------ hilo con HTTP falso

    sealed class HttpFalso : IHttpCboe
    {
        public readonly Dictionary<string, byte[]> Crudos = new Dictionary<string, byte[]>();
        public readonly Dictionary<string, string> Ts = new Dictionary<string, string>();
        public readonly Dictionary<string, int> Pedidos = new Dictionary<string, int>();
        public volatile int FallarHttp;          // 0 = responde; 500/403 = error HTTP
        public volatile bool FallarDns;
        public int ConPausa;
        public RespuestaHttp Bajar(string simbolo, int trozo, int pausaMs, CancellationToken ct)
        {
            lock (Pedidos) Pedidos[simbolo] = (Pedidos.TryGetValue(simbolo, out var n) ? n : 0) + 1;
            ConPausa = pausaMs;
            var r = new RespuestaHttp { Url = "falso://" + simbolo, Ms = 1 };
            if (FallarDns) { r.Error = "DNS: falso"; r.DnsFallo = true; return r; }
            if (FallarHttp != 0) { r.Http = FallarHttp; r.Error = "HTTP " + FallarHttp; return r; }
            // el crudo con el timestamp de turno (se edita el texto: "timestamp": "...")
            var txt = Encoding.UTF8.GetString(Crudos[simbolo]);
            string ts; lock (Ts) ts = Ts[simbolo];
            int i = txt.IndexOf("\"timestamp\"", StringComparison.Ordinal);
            int q1 = txt.IndexOf('"', txt.IndexOf(':', i) + 1), q2 = txt.IndexOf('"', q1 + 1);
            txt = txt.Substring(0, q1 + 1) + ts + txt.Substring(q2);
            var b = Encoding.UTF8.GetBytes(txt);
            r.Ok = true; r.Http = 200; r.Json = b; r.JsonLargo = b.Length; r.Gz = b; r.GzLargo = b.Length / 7;
            return r;
        }
    }

    static int fallas;
    static void Ver(bool ok, string que) { Console.WriteLine((ok ? "  OK   " : "  MAL  ") + que); if (!ok) fallas++; }

    static int Hilo(string trabajo, string crudosDir)
    {
        if (!Path.GetFileName(Path.GetFullPath(trabajo).TrimEnd('\\', '/')).EndsWith("_prueba_hilo")) { Console.WriteLine("la carpeta de trabajo tiene que terminar en _prueba_hilo"); return 2; }
        if (Directory.Exists(trabajo)) Directory.Delete(trabajo, true);       // carpeta de prueba propia del arnes (se recrea)
        Directory.CreateDirectory(trabajo);
        var carpeta = Path.Combine(trabajo, "cboe");
        var http = new HttpFalso();
        foreach (var (sim, f) in new[] { ("_NDX", "_NDX.json.gz"), ("QQQ", "QQQ.json.gz"), ("TQQQ", "TQQQ.json.gz") })
        {
            http.Crudos[sim] = Descomprimir(File.ReadAllBytes(Path.Combine(crudosDir, f)));
            var cr = CrudoCboe.Leer(http.Crudos[sim]);
            http.Ts[sim] = cr.Timestamp;
        }
        DateTime reloj = DateTime.UtcNow;
        var opc = new OpcionesCboe
        {
            Carpeta = carpeta, Log = Path.Combine(trabajo, "pythiagex4-cboe.log"), Http = http, CadenciaRuedaS = 1, CadenciaFueraS = 1,
            PausaEntreTickersMs = 50, RetrasoInicialS = 0, NombreMutex = @"Local\PythiaGex4.Prueba." + Guid.NewGuid().ToString("N"), SeguirCadaS = 1,
            RetencionDias = 0, EsperaTrasFallosS = 30, EsperaCloudflareS = 30,
        };
        Console.WriteLine("1) dueno: baja los tres, archiva, relee");
        var m1 = new BajadorCboe(opc);
        m1.Arrancar();
        Esperar(() => m1.Ultima("TQQQ") != null && m1.Ultima("NDX") != null && m1.Ultima("QQQ") != null, 60);
        Ver(m1.EsDueno, "el primero es dueno del mutex");
        foreach (var L in new[] { "NDX", "QQQ", "TQQQ" })
        {
            var u = m1.Ultima(L);
            Ver(u != null && u.Filas.Length > 0, L + ": hay foto con " + (u?.Filas.Length ?? 0) + " filas");
            var reg = m1.UltimoRegistro(L);
            Ver(reg != null && reg.Texto != null && reg.Texto.StartsWith("{\"generado\":"), L + ": registro con texto flaco (" + (reg?.Texto?.Length ?? 0) + " car.)");
            Ver(m1.Sub(u) != null && !double.IsNaN(m1.Sub(u).PrevDayClose), L + ": sub con prev_day_close " + (m1.Sub(u)?.PrevDayClose.ToString(Inv) ?? "-"));
            Ver(Math.Abs(u.CierreAnterior - m1.Sub(u).PrevDayClose) < 1e-12, L + ": FotoCadena.CierreAnterior = prev_day_close");
        }
        Thread.Sleep(2500);   // varias vueltas con el MISMO sello: no se anota de nuevo
        int nNdx = m1.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).Count;
        Ver(nNdx == 1, "mismo sello en varias vueltas -> 1 sola foto de NDX (hay " + nNdx + ")");
        int pedidos; lock (http.Pedidos) pedidos = http.Pedidos["_NDX"];
        Ver(pedidos >= 2, "NDX se pidio " + pedidos + " veces (cadencia 1 s de prueba)");
        Ver(http.ConPausa == (int)Math.Round(65536 * 1000.0 / (1250 * 1024.0)), "pausa entre trozos " + http.ConPausa + " ms con trozo 64 KB y tope 1250 KB/s");
        // sello nuevo
        string ts2 = SumarSeg(http.Ts["_NDX"], 60);
        lock (http.Ts) http.Ts["_NDX"] = ts2;
        Esperar(() => m1.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).Count == 2, 20);
        Ver(m1.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).Count == 2, "sello nuevo -> segunda foto de NDX");
        var f1 = m1.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue);
        Ver(ReferenceEquals(f1[0].Filas, f1[1].Filas), "mismo contenido con otro sello -> se comparte el arreglo de filas");
        // sello ANTERIOR (servidor atrasado)
        lock (http.Ts) http.Ts["_NDX"] = SumarSeg(ts2, -600);
        Thread.Sleep(2500);
        Ver(m1.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).Count == 2, "sello anterior al ultimo -> se descarta");
        lock (http.Ts) http.Ts["_NDX"] = ts2;
        // Fotos(desde, hasta)
        var g0 = f1[0].GeneradoUtc; var g1 = f1[1].GeneradoUtc;
        Ver(m1.Fotos("NDX", g0, g1).Count == 1 && m1.Fotos("NDX", g1, g1).Count == 0 && m1.Fotos("NDX", g1, g1.AddTicks(1)).Count == 1, "Fotos(desde, hasta) es [desde, hasta)");
        Ver(m1.Fotos("NQ", DateTime.MinValue, DateTime.MaxValue).Count == 2 && m1.Fotos("_NDX", DateTime.MinValue, DateTime.MaxValue).Count == 2, "alias NQ / _NDX = NDX");
        // seguidor en el mismo proceso (otro hilo = no consigue el mutex)
        Console.WriteLine("2) seguidor: otro motor con el mismo mutex no baja y lee ultima-*.json");
        var opc2 = Clonar(opc); opc2.Log = Path.Combine(trabajo, "pythiagex4-cboe-seguidor.log");
        var http2 = new HttpFalso(); opc2.Http = http2;
        var m2 = new BajadorCboe(opc2);
        m2.Arrancar();
        Esperar(() => m2.HistoriaCargada && m2.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).Count == 2, 20);
        Ver(!m2.EsDueno, "el segundo NO es dueno");
        Ver(m2.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).Count == 2, "el seguidor lee la historia del dueno (2 fotos NDX)");
        lock (http.Ts) http.Ts["QQQ"] = SumarSeg(http.Ts["QQQ"], 75);
        Esperar(() => m2.Fotos("QQQ", DateTime.MinValue, DateTime.MaxValue).Count == 2, 20);
        Ver(m2.Fotos("QQQ", DateTime.MinValue, DateTime.MaxValue).Count == 2, "el seguidor ve la foto nueva de QQQ por ultima-QQQ.json");
        Ver(http2.Pedidos.Count == 0, "el seguidor no pidio nada a CBOE");
        Console.WriteLine("   estado seguidor: " + m2.Estado);
        // fallos
        Console.WriteLine("3) fallos: 3 seguidos -> 300 s; 403 -> 15 min; la ultima buena sigue");
        http.FallarDns = true;
        Thread.Sleep(4000);
        Console.WriteLine("   estado: " + m1.Estado);
        Ver(m1.Estado.Contains("falla"), "el estado dice que falla");
        Ver(m1.Ultima("NDX") != null, "la ultima cadena buena sigue");
        http.FallarDns = false;
        int antes; lock (http.Pedidos) antes = http.Pedidos["_NDX"];
        Thread.Sleep(2500);
        int despues; lock (http.Pedidos) despues = http.Pedidos["_NDX"];
        Ver(despues == antes, "tras 3 fallos NDX no se vuelve a pedir enseguida (espera " + opc.EsperaTrasFallosS + " s en la prueba, 300 en produccion; pedidos " + antes + " -> " + despues + ")");
        // 403: todo espera
        var opc5 = Clonar(opc); opc5.Carpeta = Path.Combine(trabajo, "cboe403"); opc5.Log = Path.Combine(trabajo, "pythiagex4-cboe-403.log");
        opc5.NombreMutex = @"Local\PythiaGex4.Prueba403." + Guid.NewGuid().ToString("N");
        var http5 = new HttpFalso { FallarHttp = 403 }; foreach (var kv in http.Crudos) http5.Crudos[kv.Key] = kv.Value; foreach (var kv in http.Ts) http5.Ts[kv.Key] = kv.Value;
        opc5.Http = http5;
        var m5 = new BajadorCboe(opc5); m5.Arrancar();
        Thread.Sleep(3500);
        int p5; lock (http5.Pedidos) p5 = http5.Pedidos.Values.Sum();
        Ver(p5 == 1, "HTTP 403 -> un solo pedido y todo espera " + opc5.EsperaCloudflareS + " s en la prueba (900 en produccion); pedidos " + p5);
        Console.WriteLine("   estado 403: " + m5.Estado);
        m5.Parar();
        // parar
        var sw = Stopwatch.StartNew();
        m2.Parar();
        Console.WriteLine("   seguidor parado en " + sw.ElapsedMilliseconds + " ms");
        sw.Restart();
        m1.Parar();
        Ver(sw.ElapsedMilliseconds <= 2100, "dueno parado en " + sw.ElapsedMilliseconds + " ms (<= 2 s)");
        // reinicio: relee del disco propio
        Console.WriteLine("4) reinicio: el motor nuevo relee su archivo y no repite sellos");
        var opc3 = Clonar(opc); opc3.Log = Path.Combine(trabajo, "pythiagex4-cboe-2.log");
        var http3 = new HttpFalso(); foreach (var kv in http.Crudos) http3.Crudos[kv.Key] = kv.Value; foreach (var kv in http.Ts) http3.Ts[kv.Key] = kv.Value;
        opc3.Http = http3;
        var m3 = new BajadorCboe(opc3);
        m3.Arrancar();
        Esperar(() => m3.HistoriaCargada, 20);
        Thread.Sleep(2500);
        Ver(m3.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).Count == 2, "NDX: 2 fotos releidas, sin repetir el sello (hay " + m3.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).Count + ")");
        Ver(m3.Fotos("QQQ", DateTime.MinValue, DateTime.MaxValue).Count == 2, "QQQ: 2 fotos releidas");
        var a1 = m1.Ultima("NDX"); var a3 = m3.Ultima("NDX");
        Ver(a1.GeneradoUtc == a3.GeneradoUtc && a1.TsUtc == a3.TsUtc && a1.DatoUtc == a3.DatoUtc && a1.Filas.Length == a3.Filas.Length && a1.OiTotal == a3.OiTotal
            && a1.BaseCruda.Equals(a3.BaseCruda) && a1.Spot == a3.Spot && Enumerable.Range(0, a1.Filas.Length).All(i => a1.Filas[i].Equals(a3.Filas[i])),
            "la foto releida del disco es identica a la del vivo");
        Ver(m3.UltimoRegistro("NDX")?.Texto != null, "el registro releido trae el texto de ultima-NQ.json");
        // poda de filas: con HorasConFilas chico las viejas quedan livianas
        m3.Parar();
        // el corte tambien guarda con filas la rueda habil anterior: para ver fotos livianas se corre el reloj.
        // 4.1.5 (09-10-2026): 4 dias y no 3. Con 3, un VIERNES despues de las 09:00 NY el reloj cae el lunes, su "rueda habil anterior" es HOY (corte el
        // viernes 09:00 NY) y las fotos recien bajadas quedan con filas: la prueba fallaba segun la hora (medido 09-10 16:4x ART; a las 05:16 ART pasaba).
        // Con 4 dias, cualquier dia de la semana, la rueda habil anterior es POSTERIOR a hoy, y el archivo de hoy sigue dentro de DiasHistoria (7).
        var opc4 = Clonar(opc3); opc4.HorasConFilas = 0.0001; var dentroDe3 = DateTime.UtcNow.AddDays(4); opc4.AhoraUtc = () => dentroDe3;
        opc4.Log = Path.Combine(trabajo, "pythiagex4-cboe-3.log"); opc4.Bajar = false;
        var m4 = new BajadorCboe(opc4); m4.Arrancar();
        Esperar(() => m4.HistoriaCargada, 20);
        var fq = m4.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue);
        Ver(fq.Count == 2 && fq[0].Filas.Length == 0 && fq[1].Filas.Length > 0 && fq[0].OiTotal > 0, "fotos viejas livianas (sin filas, con OI total); la ultima anterior al corte conserva filas");
        Console.WriteLine("   estado apagado: " + m4.Estado);
        Ver(m4.Estado.Contains("descarga apagada"), "con Bajar=false el estado dice 'descarga apagada'");
        m4.Parar();
        // archivo propio: formato
        var arch = Directory.GetFiles(carpeta).Select(Path.GetFileName).OrderBy(x => x).ToArray();
        Console.WriteLine("   archivos: " + string.Join(", ", arch));
        Ver(arch.Contains("ultima-NQ.json") && arch.Contains("cadena-NQ.ultimo") && arch.Any(x => x.StartsWith("cadena-NQ-") && x.EndsWith(".jsonl.gz")), "ultima-NQ.json, cadena-NQ.ultimo y cadena-NQ-<dia>.jsonl.gz");
        var lineas = CboeArchivo.LeerDia(Directory.GetFiles(carpeta, "cadena-NQ-*.jsonl.gz")[0], null, out _);
        Ver(lineas.Count == 2, "el .jsonl.gz tiene 2 miembros/lineas de NDX");
        Console.WriteLine(fallas == 0 ? "hilo: TODO OK" : "hilo: " + fallas + " FALLAS");
        return fallas == 0 ? 0 : 1;
    }

    static OpcionesCboe Clonar(OpcionesCboe o) => new OpcionesCboe
    {
        Carpeta = o.Carpeta, Log = o.Log, Bajar = o.Bajar, TopeKBps = o.TopeKBps, TrozoBytes = o.TrozoBytes, BufferSocketBytes = o.BufferSocketBytes,
        VentanaUtcPython = o.VentanaUtcPython, CadenciaRuedaS = o.CadenciaRuedaS, CadenciaFueraS = o.CadenciaFueraS, PausaEntreTickersMs = o.PausaEntreTickersMs,
        RetrasoInicialS = o.RetrasoInicialS, FallosParaEsperar = o.FallosParaEsperar, EsperaTrasFallosS = o.EsperaTrasFallosS, EsperaCloudflareS = o.EsperaCloudflareS, DiasHistoria = o.DiasHistoria, HorasConFilas = o.HorasConFilas, RetencionDias = o.RetencionDias,
        NombreMutex = o.NombreMutex, SeguirCadaS = o.SeguirCadaS, Libros = o.Libros, AhoraUtc = o.AhoraUtc, Http = o.Http,
    };

    static string SumarSeg(string ts, int s)
    {
        CboeCadena.ParsearTs(ts, out var u);
        return u.AddSeconds(s).ToString("yyyy-MM-dd HH:mm:ss", Inv);
    }

    static void Esperar(Func<bool> cond, int seg)
    {
        var sw = Stopwatch.StartNew();
        while (!cond() && sw.Elapsed.TotalSeconds < seg) Thread.Sleep(100);
    }

    // ------------------------------------------------------------------ historia: carga al arrancar (tiempo y memoria)

    static int Historia(string carpeta, string ahoraIso, double horasConFilas)
    {
        var ahora = DateTime.SpecifyKind(DateTime.Parse(ahoraIso, Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long m0 = GC.GetTotalMemory(true);
        var opc = new OpcionesCboe
        {
            Carpeta = carpeta, Log = Path.Combine(Path.GetDirectoryName(carpeta.TrimEnd('\\', '/')), "pythiagex4-cboe-historia.log"), Bajar = false,
            RetrasoInicialS = 0, NombreMutex = @"Local\PythiaGex4.PruebaHistoria." + Guid.NewGuid().ToString("N"), RetencionDias = 0,
            HorasConFilas = horasConFilas, AhoraUtc = () => ahora,
        };
        var sw = Stopwatch.StartNew();
        var m = new BajadorCboe(opc);
        m.Arrancar();
        Esperar(() => m.HistoriaCargada, 600);
        long ms = sw.ElapsedMilliseconds;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long m1 = GC.GetTotalMemory(true);
        Console.WriteLine("historia (ahora " + ahora.ToString("u", Inv) + ", HorasConFilas " + horasConFilas.ToString(Inv) + "): " + ms + " ms, memoria retenida " + ((m1 - m0) / 1048576.0).ToString("0.0", Inv) + " MB");
        foreach (var L in new[] { "NDX", "QQQ", "TQQQ" })
        {
            var f = m.Fotos(L, DateTime.MinValue, DateTime.MaxValue);
            int con = f.Count(x => x.Filas.Length > 0);
            long filas = f.Where(x => x.Filas.Length > 0).Select(x => x.Filas).Distinct().Sum(x => (long)x.Length);
            Console.WriteLine("  " + L + ": " + f.Count + " fotos, " + con + " con filas (" + filas + " filas distintas = " + (filas * 64 / 1048576.0).ToString("0.0", Inv) + " MB), de "
                              + (f.Count > 0 ? f[0].GeneradoUtc.ToString("u", Inv) + " a " + f[f.Count - 1].GeneradoUtc.ToString("u", Inv) : "-")
                              + (f.Count > 0 ? "; primera con filas " + f.First(x => x.Filas.Length > 0).GeneradoUtc.ToString("u", Inv) : ""));
        }
        Console.WriteLine("  estado: " + m.Estado);
        m.Parar();
        GC.KeepAlive(m);
        return 0;
    }

    // ------------------------------------------------------------------ UNA bajada real por ticker

    sealed class HttpUnaVez : IHttpCboe
    {
        private readonly HttpCboe _real;
        public readonly HashSet<string> Hechos = new HashSet<string>();
        public readonly List<string> Informe = new List<string>();
        public HttpUnaVez(int bufSock) { _real = new HttpCboe(bufSock); }
        public RespuestaHttp Bajar(string simbolo, int trozo, int pausaMs, CancellationToken ct)
        {
            lock (Hechos)
            {
                if (!Hechos.Add(simbolo)) return new RespuestaHttp { Error = "arnes: ya se bajo " + simbolo + " una vez (regla: una por ticker)", Http = 0 };
            }
            var r = _real.Bajar(simbolo, trozo, pausaMs, ct);
            lock (Informe) Informe.Add(simbolo + ": http " + r.Http + " url " + r.Url + " gz " + r.GzLargo + " B json " + r.JsonLargo + " B " + r.Ms + " ms"
                                      + (r.SinGzip ? " SIN GZIP" : "") + (r.UsoRespaldo ? " (respaldo)" : "") + (r.Ok ? "" : " ERROR " + r.Error));
            return r;
        }
    }

    static int Bajar(string salida, bool otraVez)
    {
        Directory.CreateDirectory(salida);
        var marca = Path.Combine(salida, "BAJADA_HECHA.txt");
        if (File.Exists(marca) && !otraVez) { Console.WriteLine("ya se hizo la bajada de prueba (" + File.ReadAllText(marca).Trim() + "); no se repite. --otra-vez para forzar."); return 3; }
        File.WriteAllText(marca, DateTime.UtcNow.ToString("o", Inv));
        var crudos = Path.Combine(salida, "crudos");
        Directory.CreateDirectory(crudos);
        var http = new HttpUnaVez(64 * 1024);
        var generados = new Dictionary<string, DateTime>();
        var opc = new OpcionesCboe
        {
            Carpeta = Path.Combine(salida, "cboe"), Log = Path.Combine(salida, "pythiagex4-cboe.log"), Http = http, CadenciaRuedaS = 3600, CadenciaFueraS = 3600,
            PausaEntreTickersMs = 3000, RetrasoInicialS = 0, NombreMutex = @"Local\PythiaGex4.PruebaBajada", RetencionDias = 0,
        };
        opc.AlBajarCrudo = (sim, gz, n, gen) =>
        {
            var b = new byte[n]; Buffer.BlockCopy(gz, 0, b, 0, n);
            File.WriteAllBytes(Path.Combine(crudos, sim + ".json.gz"), b);
            File.WriteAllText(Path.Combine(crudos, sim + ".generado.txt"), IsoUs(gen));
        };
        var sw = Stopwatch.StartNew();
        var m = new BajadorCboe(opc);
        m.Arrancar();
        Esperar(() => { lock (http.Hechos) return http.Hechos.Count == 3; }, 240);
        Esperar(() => m.Ultima("TQQQ") != null || sw.Elapsed.TotalSeconds > 240, 240);
        Thread.Sleep(500);
        m.Parar();
        foreach (var l in http.Informe) Console.WriteLine("  " + l);
        Console.WriteLine("  estado: " + m.Estado);
        Console.WriteLine("  total " + sw.ElapsedMilliseconds + " ms");
        File.AppendAllText(marca, Environment.NewLine + string.Join(Environment.NewLine, http.Informe));
        return 0;
    }
}
