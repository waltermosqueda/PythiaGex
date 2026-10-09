// Program.cs - PRUEBA DE PUNTA A PUNTA de PythiaGex 4.1 fuera de ATAS (08-10-2026).
// Encadena las PIEZAS REALES como HostFamilia (ver HostE2E.cs): CintaFamilia (importa la cinta CSV del MNQ) + LibroNqFamilia (se siembra
// de viva3) + descargador CBOE (B2: lee su archivo, Bajar=false, guardia sin red) + minuteros NQ/NDX/QQQ + CalculoTqqq + MotorFamilia, sobre
// una carpeta TEMPORAL sembrada con COPIAS (AdaptadoresFamilia.Carpeta apunta ahi). Corre la sesion 08-10 completa con el reloj inyectado en
// 2026-10-08 21:59:12 UTC (el mismo instante de la referencia de tqqq_vivo) y compara la FotoFamilia contra
//   atas/_test_cuatro_paridad/referencia/ref-2026-10-08.json (preview_niveles.Motor(vivo=False).historico(), salida + niv)  y
//   atas/_test_cuatro_paridad/tqqq/referencia/tqqq_vivo-2026-10-08T215912Z.json (la salida REAL de tqqq_vivo a las 21:59:12Z).
// NO toca ATAS, NO escribe en %APPDATA%\ATAS (ni PythiaGex4 ni sus logs), NO baja nada. Prioridad BelowNormal.
//
// Escenarios (uno por proceso: los registros de CintaFamilia/LibroNqFamilia/FuenteCboe son estaticos):
//   ny            semilla nueva, Familia41Corregida = true (el default del indicador)
//   ny-reinicio   SIN semilla: la carpeta de 'ny' tal como quedo (cinta .bin propia, muestras NDX/QQQ/TQQQ, niv-*.jsonl): reinicio de ATAS
//   ny-recalculo  como ny-reinicio pero borrando familia\niv-*.jsonl: recalcula todo desde lo persistido (cinta .bin, muestras)
//   utc           semilla nueva, Familia41Corregida = false (paridad exacta con la vista previa)
//   ny-union      DIAGNOSTICO: como ny pero NDX/QQQ sembrados con la UNION de carpetas que leyo la referencia (atribuye las diferencias de 'ny')
//   ny-vivo       VIVO SIMULADO: la sesion 08-10 entra por CintaFamilia.Tick y LibroNqFamilia.AgregarLinea y el motor avanza minuto a minuto
//   ny-extras     (4.1.4) como ny pero con la configuracion de PRODUCCION: el motor con la Replica20 (OpcionesMotorFamilia.Extras). Las 21 series, la
//                 historia, los actuales y las fuentes (sin las claves R20_/DOMS_ ni las fuentes "2.0 ...") tienen que dar IDENTICOS a 'ny'.
// Uso: dotnet bin/Release/e2e.dll <escenario> [--raiz-tmp <dir>]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using PythiaGexCuatro.Cboe;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Ndx;
using PythiaGexCuatro.Familia.Qqq;

namespace E2E
{
    public static class Program
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static StreamWriter _out;
        static readonly Stopwatch _reloj0 = Stopwatch.StartNew();
        static void P(string s = "") { Console.WriteLine(s); _out?.WriteLine(s); _out?.Flush(); }

        const string DIA = "2026-10-08", DIA_ANT = "2026-10-07";
        static readonly DateTime RELOJ = new DateTime(2026, 10, 8, 21, 59, 12, DateTimeKind.Utc);    // = generado_utc de tqqq_vivo 08-10
        static readonly DateTime INI = new DateTime(2026, 10, 7, 22, 0, 0, DateTimeKind.Utc), FIN = new DateTime(2026, 10, 8, 21, 0, 0, DateTimeKind.Utc);
        const string CODIGO = "MNQZ6";

        static readonly HashSet<long> _futDistinto = new HashSet<long>();          // vivo: minutos cuyo fut (a t + 6 s) no es el del historico
        static volatile object _ahoraCaja = INI;
        static DateTime _ahora { get => (DateTime)_ahoraCaja; set => _ahoraCaja = value; }   // el reloj inyectado (en vivo avanza; si no, fijo en RELOJ)

        sealed class Medida { public string Etapa; public double Seg; public long Ws, Pico, Priv, Heap; }
        static readonly List<Medida> _medidas = new List<Medida>();
        static Medida Medir(string etapa, bool gc = false)
        {
            if (gc) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
            var p = Process.GetCurrentProcess(); p.Refresh();
            var m = new Medida { Etapa = etapa, Seg = _reloj0.Elapsed.TotalSeconds, Ws = p.WorkingSet64 >> 20, Pico = p.PeakWorkingSet64 >> 20, Priv = p.PrivateMemorySize64 >> 20, Heap = GC.GetTotalMemory(false) >> 20 };
            _medidas.Add(m);
            P("  [t " + m.Seg.ToString("0.0", Inv).PadLeft(6) + " s] " + etapa.PadRight(44) + " working set " + m.Ws + " MB (pico " + m.Pico + "), privada " + m.Priv + " MB, heap GC " + m.Heap + " MB" + (gc ? " (tras GC completo)" : ""));
            return m;
        }

        public static int Main(string[] args)
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            string esc = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : "ny";
            if (!new[] { "ny", "ny-reinicio", "ny-recalculo", "utc", "ny-union", "ny-vivo", "ny-extras" }.Contains(esc)) { Console.WriteLine("escenario desconocido: " + esc); return 2; }
            int ir = Array.IndexOf(args, "--raiz-tmp");
            string raizTmp = ir >= 0 && ir + 1 < args.Length ? args[ir + 1] : Path.Combine(Path.GetTempPath(), "pg4_e2e");

            var d0 = new DirectoryInfo(AppContext.BaseDirectory);
            while (d0 != null && !File.Exists(Path.Combine(d0.FullName, "e2e.csproj"))) d0 = d0.Parent;
            string aqui = d0?.FullName ?? Directory.GetCurrentDirectory();
            string raiz = Path.GetFullPath(Path.Combine(aqui, "..", "..", ".."));          // ...\PythiaGex
            string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            string res = Path.Combine(aqui, "resultados"); Directory.CreateDirectory(res);

            bool corregida = esc != "utc";
            bool semilla = esc == "ny" || esc == "utc" || esc == "ny-union" || esc == "ny-vivo" || esc == "ny-extras";
            string baseTmp = Path.Combine(raizTmp, esc == "ny-union" || esc == "ny-vivo" || esc == "ny-extras" ? esc : esc.StartsWith("ny") ? "ny" : "utc");
            string datos4 = Path.Combine(baseTmp, "datos4");                                 // = AdaptadoresFamilia.Carpeta (hace de PythiaGex4)
            string logs = Path.Combine(baseTmp, "logs-" + esc);
            int rc = 0;
            using (_out = new StreamWriter(Path.Combine(res, "e2e-" + esc + ".txt"), false, new UTF8Encoding(false)))
            {
                P("PRUEBA DE PUNTA A PUNTA PythiaGex 4.1 fuera de ATAS - escenario '" + esc + "' - " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " UTC (reloj real)");
                P("motor " + MotorFamilia.VERSION + " | " + CalculoTqqq.VERSION + " | reloj inyectado " + RELOJ.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " UTC | modo " + (corregida ? "corregido (NY, default)" : "paridad (UTC fijo)"));
                P("carpeta temporal (hace de %APPDATA%\\ATAS\\PythiaGex4): " + datos4);
                P("proceso: pid " + Environment.ProcessId + ", prioridad " + Process.GetCurrentProcess().PriorityClass + ", " + Environment.ProcessorCount + " nucleos, .NET " + Environment.Version);
                P();
                try { rc = Correr(esc, corregida, semilla, raiz, app, aqui, res, baseTmp, datos4, logs); }
                catch (Exception e) { P("EXCEPCION: " + e); rc = 3; }
            }
            return rc;
        }

        // ==============================================================================================================================
        static int Correr(string esc, bool corregida, bool semilla, string raiz, string app, string aqui, string res, string baseTmp, string datos4, string logs)
        {
            Medir("inicio");
            // ---------------------------------------------------------------- semilla (copias)
            P("== 1. Semilla (copias de solo lectura de los originales; nada se escribe fuera de la carpeta temporal)");
            if (semilla)
            {
                try { if (Directory.Exists(baseTmp)) Directory.Delete(baseTmp, true); } catch (Exception e) { P("  no pude borrar " + baseTmp + ": " + e.Message); }
                var sw = Stopwatch.StartNew(); long bytes = 0; int n = 0;
                string cintaO = Path.Combine(raiz, "profundidad", "estado", "cinta");
                bool vivo = esc == "ny-vivo";   // en vivo: la sesion 08-10 entra por CintaFamilia.Tick y LibroNqFamilia.AgregarLinea, no por la semilla
                foreach (var f in Directory.GetFiles(cintaO, "cinta-NQ-" + DIA_ANT + "*").Concat(vivo ? new string[0] : Directory.GetFiles(cintaO, "cinta-NQ-" + DIA + "*")))
                { bytes += Copiar(f, Path.Combine(datos4, "cinta", Path.GetFileName(f))); n++; }
                foreach (var d in vivo ? new string[0] : new[] { DIA_ANT, DIA })
                { var f = Path.Combine(app, "PythiaGex3", "viva", "viva3-NQ-" + d + ".jsonl"); if (File.Exists(f)) { bytes += Copiar(f, Path.Combine(datos4, "viva", Path.GetFileName(f))); n++; } }
                string cl = Path.Combine(app, "PythiaGex", "cboe-local");
                foreach (var tk in new[] { "NQ", "QQQ", "TQQQ" })
                {
                    for (int k = 7; k >= 0; k--)
                    {
                        var f = Path.Combine(cl, "cadena-" + tk + "-" + RELOJ.Date.AddDays(-k).ToString("yyyy-MM-dd", Inv) + ".jsonl.gz");
                        if (File.Exists(f)) { bytes += Copiar(f, Path.Combine(datos4, "cboe", Path.GetFileName(f))); n++; }
                    }
                    foreach (var f in new[] { Path.Combine(cl, "ultima-" + tk + ".json"), Path.Combine(cl, "cadena-" + tk + ".ultimo") })
                        if (File.Exists(f)) { bytes += Copiar(f, Path.Combine(datos4, "cboe", Path.GetFileName(f))); n++; }
                }
                // TRES_*: en el indicador las escribe el clon de la 3.0 (GuardarEstela, PythiaGex4\estela). Fuera de ATAS el clon no corre: se siembran
                // las estelas de la 3.0 de produccion (PythiaGex3\estela), las mismas que leyo la referencia. NO es una prueba del clon.
                foreach (var capa in CatalogoFamilia.CapasTres) foreach (var d in new[] { DIA_ANT, DIA })
                    { var f = Path.Combine(app, "PythiaGex3", "estela", "estela-" + capa + "-" + d + ".jsonl"); if (File.Exists(f)) { bytes += Copiar(f, Path.Combine(datos4, "estela", Path.GetFileName(f))); n++; } }
                P("  " + n + " archivos, " + (bytes >> 20) + " MB copiados en " + sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s");
                if (esc == "ny-union")
                {
                    // DIAGNOSTICO de atribucion: las cadenas de NDX y QQQ se reemplazan por la UNION de las carpetas que leyo la referencia (libros.fotos_cboe:
                    // PythiaGex\cadenas, PythiaGex2\cadenas, PythiaGex\cboe-local y PythiaGex\cadenas\local-*), lineas CRUDAS sin tocar, una por sello (la de
                    // 'generado' mas temprano, con filas), hasta el 08-10 21:00 UTC. El descargador real las lee igual que las propias.
                    var swU = Stopwatch.StartNew();
                    foreach (var tk in new[] { "NQ", "QQQ" })
                    {
                        foreach (var f in Directory.GetFiles(Path.Combine(datos4, "cboe"), "cadena-" + tk + "-*.jsonl.gz")) File.Delete(f);
                        var (lin, eleg, arch) = SembrarUnion(app, tk, Path.Combine(datos4, "cboe"), RELOJ.Date.AddDays(-7), RELOJ.Date, FIN);
                        P("  [union] " + tk + ": " + arch + " archivos de origen, " + lin + " lineas, " + eleg + " sellos elegidos (generado mas temprano, con filas, <= 08-10 21:00 UTC)");
                    }
                    P("  [union] armada en " + swU.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s (TQQQ queda como la copia de cboe-local: tiene las mismas fotos que la referencia)");
                }
                foreach (var sub in new[] { "cinta", "viva", "cboe", "estela" })
                    if (Directory.Exists(Path.Combine(datos4, sub)))
                        P("    " + sub.PadRight(7) + string.Join(" ", Directory.GetFiles(Path.Combine(datos4, sub)).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal)));
            }
            else
            {
                if (!Directory.Exists(datos4)) { P("  falta la carpeta del escenario 'ny' (correr 'ny' antes): " + datos4); return 2; }
                P("  sin semilla nueva: reinicio sobre lo que dejo 'ny' en " + datos4);
                if (esc == "ny-recalculo")
                    foreach (var f in Directory.GetFiles(Path.Combine(datos4, "familia"), "niv-*.jsonl")) { File.Delete(f); P("  borrado (para forzar el recalculo): " + Path.GetFileName(f)); }
                P("  persistido por 'ny': " + string.Join(" | ", new[] { "cinta", "familia", "cboe" }.Select(s => s + ": " + string.Join(" ", Directory.GetFiles(Path.Combine(datos4, s)).Select(x => Path.GetFileName(x) + " (" + (new FileInfo(x).Length >> 10) + " KB)").OrderBy(x => x, StringComparer.Ordinal)))));
            }
            Medir("semilla lista");

            // ---------------------------------------------------------------- adaptadores (B1), como AsegurarAdaptadoresFamilia
            P(); P("== 2. Adaptadores (B1) como AsegurarAdaptadoresFamilia: CintaFamilia.Para(MNQZ6).Registrar y LibroNqFamilia.Para(NQ).Registrar");
            AdaptadoresFamilia.Carpeta = datos4;
            Directory.CreateDirectory(logs);
            AdaptadoresFamilia.Log = m => { try { File.AppendAllText(Path.Combine(logs, "pythiagex4-adaptadores.log"), DateTime.Now.ToString("HH:mm:ss.fff", Inv) + "  " + m + "\n"); } catch { } };
            var dueno = new object();
            var sw1 = Stopwatch.StartNew();
            var cinta = CintaFamilia.Para(CODIGO, "NQ");
            cinta.Registrar(dueno);
            var libro = LibroNqFamilia.Para("NQ");
            // [E2E-7] LibroNqFamilia.Sembrar mira el reloj REAL (ahora - RetencionHoras): si la sesion 08-10 quedo fuera, se amplia la retencion
            double horasHace = (DateTime.UtcNow - INI.AddHours(-1)).TotalHours;
            if (horasHace > libro.RetencionHoras) { P("  [E2E-7] RetencionHoras " + libro.RetencionHoras + " -> " + Math.Ceiling(horasHace) + " (el reloj real ya esta " + horasHace.ToString("0.0", Inv) + " h despues del inicio de la sesion)"); libro.RetencionHoras = (int)Math.Ceiling(horasHace); }
            libro.Registrar(dueno);
            double tCinta = -1, tLibro = -1;
            while (sw1.Elapsed.TotalSeconds < 600 && (tCinta < 0 || tLibro < 0))
            {
                if (tCinta < 0 && cinta.Estado.Contains("(historia:")) tCinta = sw1.Elapsed.TotalSeconds;
                if (tLibro < 0 && libro.Sembrado) tLibro = sw1.Elapsed.TotalSeconds;
                Thread.Sleep(50);
            }
            P("  cinta lista en " + tCinta.ToString("0.0", Inv) + " s: " + cinta.Estado);
            P("  libro listo en " + tLibro.ToString("0.0", Inv) + " s: " + libro.Estado);
            Medir("cinta y libro NQ cargados");

            // ---------------------------------------------------------------- host (integracion), como FamiliaAsegurarHost
            P(); P("== 3. Host de la familia (replica de HostFamilia, ver HostE2E.cs [E2E-1..6]) y motor en su hilo con el reloj inyectado");
            bool enVivo = esc == "ny-vivo";
            _ahora = enVivo ? INI : RELOJ;
            var host = new HostE2E(cinta, libro, CODIGO, corregida, bajar: false, topeKBps: 1250, tqqqNoche: false, reloj: () => _ahora, logs: logs,
                                   nombreMutex: @"Local\PythiaGex4.e2e." + Environment.ProcessId,
                                   // en vivo el descargador no baja (sin red): su reloj queda al final para que lea el archivo de la sesion entera; en ATAS esas
                                   // fotos llegarian bajadas de a una. La cuenta es causal (generado <= t), asi que tenerlas antes no cambia nada.
                                   relojCboe: enVivo ? () => RELOJ : (Func<DateTime>)null, extras: esc == "ny-extras");
            P("  " + host.Descripcion + " | muestras QQQ sembradas al construir: " + host.MuestrasQqqSembradas);
            var sw2 = Stopwatch.StartNew();
            double tCboe = -1, tListo = -1, tFin = -1;
            FotoFamilia fotoA, foto;
            if (!enVivo)
            {
                host.Arrancar();
                while (sw2.Elapsed.TotalSeconds < 1200)
                {
                    if (tCboe < 0 && (host.Cboe.Motor?.HistoriaCargada ?? false)) { tCboe = sw2.Elapsed.TotalSeconds; Medir("CBOE: historia cargada"); }
                    if (tListo < 0 && host.Motor.MinutosCalculados > 0) tListo = sw2.Elapsed.TotalSeconds;
                    var f = host.Foto;
                    if (tListo >= 0 && !host.Motor.Pendiente && f != null && f.CalculadoUtc == RELOJ && f.Aviso.IndexOf("esperando", StringComparison.Ordinal) < 0 && f.Aviso.IndexOf("calculando", StringComparison.Ordinal) < 0)
                    { tFin = sw2.Elapsed.TotalSeconds; break; }
                    Thread.Sleep(100);
                }
                // una vuelta mas (como el timer de 5 s): la foto no debe cambiar
                fotoA = host.Foto; int llam = host.LlamadasAvanzar; host.Despertar();
                var sw3 = Stopwatch.StartNew(); while (host.LlamadasAvanzar == llam && sw3.Elapsed.TotalSeconds < 30) Thread.Sleep(20);
                foto = host.Foto;
                host.PararMotor();   // los diccionarios del motor (VelasTqqq, Almacen) se leen con el hilo quieto
            }
            else
            {
                // ---- VIVO SIMULADO: la sesion 08-10 entra como en ATAS: cada tick por CintaFamilia.Tick (GANCHO de CintaEvento, en orden de t; relleno
                // anterior al primer vivo + vivo, como si el indicador hubiera corrido desde las 22:00), cada linea viva3 por LibroNqFamilia.AgregarLinea
                // (GANCHO de Viva3.Guardar) cuando el reloj pasa su ts, y el motor avanza minuto a minuto en el segundo 6 (como el timer). CBOE: el
                // descargador real con su archivo (Bajar=false): las fotos ya estan todas (la cuenta es causal: solo usa generado <= t).
                string cintaO = Path.Combine(raiz, "profundidad", "estado", "cinta");
                var vt = CintaFamilia.LeerCsv(Path.Combine(cintaO, "cinta-NQ-" + DIA + ".csv"));
                var rt = CintaFamilia.LeerCsv(Path.Combine(cintaO, "cinta-NQ-" + DIA + "-relleno.csv"));
                long minV = vt.Count > 0 ? vt.Min(x => x.T) : long.MaxValue;
                long iniMs = SesionFamilia.Ms(INI), finMs = SesionFamilia.Ms(FIN);
                var ticks = rt.Where(x => x.T < minV).Concat(vt).Where(x => x.T >= iniMs && x.T < finMs).Select((x, i) => (x.T, x.P, I: i)).OrderBy(x => x.T).ThenBy(x => x.I).Select(x => (x.T, x.P)).ToArray();
                var rxTs = new Regex("\"ts\":\"([^\"]+)\"", RegexOptions.Compiled);
                var lineas = new List<(DateTime Ts, string L)>();
                foreach (var d in new[] { DIA_ANT, DIA })
                    foreach (var l in File.ReadLines(Path.Combine(app, "PythiaGex3", "viva", "viva3-NQ-" + d + ".jsonl")))
                    {
                        var m = rxTs.Match(l.Length > 300 ? l.Substring(0, 300) : l);
                        if (!m.Success || !DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts)) continue;
                        ts = DateTime.SpecifyKind(ts, DateTimeKind.Utc);
                        if (ts >= INI.AddMinutes(-10) && ts < FIN.AddHours(1)) lineas.Add((ts, l));
                    }
                lineas = lineas.Select((x, i) => (x, i)).OrderBy(x => x.x.Ts).ThenBy(x => x.i).Select(x => x.x).ToList();
                P("  vivo: " + ticks.Length + " ticks de la sesion (relleno < primer vivo + vivo) y " + lineas.Count + " lineas viva3 para entrar en vivo (cargados en " + sw2.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s)");
                host.ArrancarSinHilo();
                while (!(host.Cboe.Motor?.HistoriaCargada ?? false) && sw2.Elapsed.TotalSeconds < 300) Thread.Sleep(50);
                tCboe = sw2.Elapsed.TotalSeconds; Medir("CBOE: historia cargada");
                int it = 0, il = 0, pasos = 0, aceptadas = 0;
                var ms = new List<double>(1500); var msTick = Stopwatch.StartNew(); msTick.Stop();
                for (var t = INI.AddSeconds(6); t <= RELOJ; t = t.AddMinutes(1) > RELOJ && t < RELOJ ? RELOJ : t.AddMinutes(1))
                {
                    long tMs = SesionFamilia.Ms(t);
                    msTick.Start();
                    while (it < ticks.Length && ticks[it].T <= tMs) { cinta.Tick(ticks[it].T, ticks[it].P); it++; }
                    msTick.Stop();
                    while (il < lineas.Count && lineas[il].Ts <= t) { if (libro.AgregarLinea(lineas[il].L, "vivo")) aceptadas++; il++; }
                    _ahora = t;
                    var swp = Stopwatch.StartNew();
                    host.Motor.Avanzar(t);
                    ms.Add(swp.Elapsed.TotalMilliseconds); pasos++;
                    if (tListo < 0 && host.Motor.MinutosCalculados > 0) tListo = sw2.Elapsed.TotalSeconds;
                    if (t == RELOJ) break;
                }
                tFin = sw2.Elapsed.TotalSeconds;
                fotoA = host.Foto; host.Motor.Avanzar(RELOJ); foto = host.Foto;
                // diagnostico: el fut de cada minuto en vivo (cierre de la ultima m2 cerrada a t + 6 s) contra el que da la cinta completa (el historico)
                int nFut = 0, nFutDif = 0; var ejFut = new List<string>();
                for (long k = TiempoFam.Clave(INI); k < TiempoFam.Clave(FIN); k++)
                {
                    var r = host.Motor.Almacen.Get(k); if (r == null) continue;
                    nFut++;
                    double fh = cinta.CierreConocido(TiempoFam.DeClave(k));
                    if (!(r.FutMnq == fh)) { nFutDif++; _futDistinto.Add(k); if (ejFut.Count < 40) ejFut.Add(TiempoFam.DeClave(k).ToString("HH:mm", Inv) + " vivo " + F(r.FutMnq) + " historico " + F(fh)); }
                }
                // diagnostico: el precio del MNQ en ts - 900 s con que C8 (QQQ) y C7 (NDX) armaron cada muestra en vivo, contra el de la cinta completa
                {
                    int nq = 0, dq = 0, nanVivo = 0; var ejq = new List<string>();
                    foreach (var fq in host.MinQqq.Fotos)
                    {
                        if (fq.GeneradoUtc < INI.AddDays(-1) || fq.GeneradoUtc > FIN) continue;
                        nq++;
                        double ph = cinta.Precio(fq.TSpotUtc);
                        if (!(fq.Precio == ph || (double.IsNaN(fq.Precio) && double.IsNaN(ph))))
                        { dq++; if (double.IsNaN(fq.Precio)) nanVivo++; if (ejq.Count < 10) ejq.Add(fq.TsUtc.ToString("MM-dd HH:mm:ss", Inv) + (fq.Vivo ? " (viva)" : "") + " vivo " + F(fq.Precio) + " historico " + F(ph)); }
                    }
                    P("  QQQ C8: precio de la muestra (MNQ en ts - 900 s) de " + nq + " fotos desde el 07-10 22:00: " + dq + " distintos del historico (" + nanVivo + " NaN en vivo y con precio en el historico)" + (ejq.Count > 0 ? ": " + string.Join(" | ", ejq) : ""));
                    int nn = 0, dn = 0; var ejn = new List<string>();
                    foreach (var e in host.MinNdx.Conversion.Estados)
                    {
                        if (e.Foto.GeneradoUtc < INI.AddDays(-1) || e.Foto.GeneradoUtc > FIN) continue;
                        nn++;
                        double ph = cinta.Precio(e.TSpotUtc);
                        if (!(e.Precio == ph || (double.IsNaN(e.Precio) && double.IsNaN(ph)))) { dn++; if (ejn.Count < 6) ejn.Add(e.Foto.TsUtc.ToString("MM-dd HH:mm:ss", Inv) + " vivo " + F(e.Precio) + " historico " + F(ph)); }
                    }
                    P("  NDX C7: precio de la muestra de " + nn + " fotos desde el 07-10 22:00: " + dn + " distintos del historico" + (ejn.Count > 0 ? ": " + string.Join(" | ", ejn) : "") + " (C7 se rehace entera cuando la cinta avanza: se recupera)");
                }
                P("  fut por minuto (vivo, a t + 6 s) contra el historico (cinta completa): " + (nFut - nFutDif) + " iguales, " + nFutDif + " distintos de " + nFut + (ejFut.Count > 0 ? ": " + string.Join(" | ", ejFut) : ""));
                var o = ms.OrderBy(x => x).ToList();
                P("  vivo: " + pasos + " pasos de 1 min (" + it + " ticks por Tick en " + msTick.Elapsed.TotalSeconds.ToString("0.00", Inv) + " s = " + (msTick.Elapsed.TotalMilliseconds * 1000 / Math.Max(1, it)).ToString("0.00", Inv) + " us por tick; "
                  + aceptadas + " de " + il + " lineas viva3 aceptadas por AgregarLinea)");
                P("  Avanzar por minuto en vivo: mediana " + o[o.Count / 2].ToString("0.0", Inv) + " ms, p95 " + o[(int)(o.Count * 0.95)].ToString("0.0", Inv) + " ms, max " + o[o.Count - 1].ToString("0.0", Inv) + " ms (el minuto "
                  + SesionFamilia.DeMs(SesionFamilia.Ms(INI.AddSeconds(6)) + ms.IndexOf(o[o.Count - 1]) * 60000L).ToString("HH:mm", Inv) + "), total " + (ms.Sum() / 1000).ToString("0.0", Inv) + " s");
            }
            P("  CBOE historia en " + tCboe.ToString("0.0", Inv) + " s; primer minuto calculado a los " + tListo.ToString("0.0", Inv) + " s; sesion completa a los " + tFin.ToString("0.0", Inv) + " s");
            P("  motor: " + host.Motor.MinutosCalculados + " minutos calculados, " + host.Motor.Almacen.Cuantos + " con libros (cargados del archivo: " + host.Motor.Almacen.Cargados + "), " + host.Motor.TandasIniciadas + " tandas, "
              + host.LlamadasAvanzar + " llamadas a Avanzar (" + host.ErroresAvanzar + " con excepcion), " + host.Motor.LlamadasMinutero + " llamadas a minuteros");
            P("  Avanzar: total " + (host.MsAvanzarTotal / 1000).ToString("0.0", Inv) + " s, maximo " + host.MsAvanzarMax.ToString("0", Inv) + " ms por llamada (presupuesto 2500 ms + TQQQ + historia), "
              + (host.Motor.MinutosCalculados > 0 ? (host.MsAvanzarTotal / host.Motor.MinutosCalculados).ToString("0.0", Inv) + " ms por minuto (todo incluido)" : ""));
            P("  NDX: " + host.MinNdx.Recalculos + " recalculos de C7, zero " + host.MinNdx.CalculosZero + " calculos / " + host.MinNdx.AciertosZero + " aciertos de cache, calentamientos " + host.MinNdx.Calentamientos
              + " | QQQ: " + host.MinQqq.Fotos.Count + " fotos procesadas, zero " + host.MinQqq.CalculosZero + "/" + host.MinQqq.AciertosZero + ", muestras anotadas " + host.MuestrasQqqAnotadas
              + " | NQ: " + host.MinNq.NFotos + " fotos de la sesion, OI " + host.MinNq.OiComo);
            P("  TQQQ: " + host.Motor.VelasTqqq.Count + " velas con conversion | " + host.Tqqq.Estado);
            P("  CBOE: " + host.Cboe.Estado + " | modo dueno " + (host.Cboe.Motor?.EsDueno ?? false) + " | pedidos HTTP intentados: " + host.Http.Llamadas + " (deben ser 0)");
            P("  foto: sesion " + foto?.Sesion + ", calculada " + foto?.CalculadoUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv) + ", instrumento '" + foto?.Instrumento + "', aviso '" + foto?.Aviso + "', " + foto?.HistoriaM2.Count + " velas m2 en la historia, " + foto?.Actuales.Count + " actuales");
            P("  la vuelta extra (timer) " + (ReferenceEquals(fotoA, foto) ? "no publico foto nueva" : "publico otra foto: " + (MismaFoto(fotoA, foto) ? "IGUAL" : "DISTINTA")));
            foreach (var fe in foto.Fuentes) P("    fuente " + fe.Libro.PadRight(5) + " " + fe.Texto + (fe.DatoUtc != default ? " | dato " + fe.DatoUtc.ToString("MM-dd HH:mm:ss", Inv) + "Z" : "") + (fe.Congelada ? " congelada" : "") + (fe.OiOk ? "" : " OI viejo"));
            Medir("motor: sesion completa");
            var memFinal = Medir("motor: sesion completa", gc: true);

            // ---------------------------------------------------------------- comparaciones
            P(); P("== 4. Contra la vista previa (ref-" + DIA + ".json)");
            var refDoc = LeerJson(Path.Combine(raiz, "atas", "_test_cuatro_paridad", "referencia", "ref-" + DIA + ".json"));
            var resumen = new Dictionary<string, object>();
            bool okVelas = CompararVelas(cinta, refDoc.RootElement.GetProperty("salida"), resumen);
            var difMin = CompararMinutos(host.Motor, refDoc.RootElement.GetProperty("niv"), resumen);
            // 4.1.4: con la Replica20 (ny-extras) se compara la foto SIN las series extra ni sus fuentes; las extras se miran aparte (seccion 6b)
            var fotoExtras = foto;
            if (esc == "ny-extras") foto = SinExtras(foto);
            bool okHist = CompararHistoria(foto, refDoc.RootElement.GetProperty("salida"), resumen);
            bool okAct = CompararActuales(foto, refDoc.RootElement.GetProperty("salida"), resumen);
            CompararFuentes(foto, refDoc.RootElement.GetProperty("salida"));

            P(); P("== 5. TQQQ contra tqqq_vivo (laboratorio/tres/tqqq: salida real a las 21:59:12Z, mismo hash que profundidad/pagina/preview_datos/tqqq_vivo.json)");
            var tqDoc = LeerJson(Path.Combine(raiz, "atas", "_test_cuatro_paridad", "tqqq", "referencia", "tqqq_vivo-2026-10-08T215912Z.json"));
            bool okTq = CompararTqqq("motor (cadena real)", foto, host.Motor.VelasTqqq, tqDoc.RootElement, resumen);
            // diagnostico: la misma cadena de fuentes con la s de tqqq_vivo forzada (aisla la s del resto de la cuenta)
            {
                double sRef = tqDoc.RootElement.GetProperty("conv").GetProperty("s").GetDouble();
                var tqF = new CalculoTqqq(new OpcionesTqqq { Corregida = corregida, AnclaNoche = false, RegistrarAnclas = false, Carpeta = null, RutaLog = Path.Combine(logs, "tqqq-sforzada.log"),
                                                             SForzada = new Dictionary<string, double> { [DIA] = sRef } }) { Contrato = AdaptadoresFamilia.ContratoDeCodigo(CODIGO) };
                var ses = SesionFamilia.De(RELOJ, corregida);
                var velas = new Dictionary<long, TqqqVela>();
                long tq0 = SesionFamilia.Ms(ses.RuedaIniUtc) / 120000 * 120000, tq1 = Math.Min(SesionFamilia.Ms(RELOJ) / 120000 * 120000, SesionFamilia.Ms(ses.SiguienteIniUtc) - 120000);
                for (long T = tq0; T <= tq1; T += 120000) { var v = tqF.Vela(T, cinta, host.Cboe); if (v != null) velas[T] = v; }
                P("  -- diagnostico: CalculoTqqq con SForzada = " + sRef.ToString("R", Inv) + " (la s de tqqq_vivo) sobre las MISMAS fuentes (cinta y CBOE de la cadena): " + velas.Count + " velas | " + tqF.Estado);
                CompararTqqq("s forzada (diagnostico)", null, velas, tqDoc.RootElement, null);
            }

            // ---------------------------------------------------------------- volcado para comparar escenarios entre si (reinicio)
            var volcado = Volcar(foto);
            File.WriteAllText(Path.Combine(res, "foto-" + esc + ".txt"), volcado, new UTF8Encoding(false));
            if (esc == "ny-extras")
            {   // 4.1.4: lo que agrega la Replica20 en la configuracion de produccion
                int conExtra = host.Motor.Almacen.Todos.Count(r => r.TieneExtra);
                var hx = fotoExtras.HistoriaM2.Values.SelectMany(e => e.Keys).Where(k => CatalogoFamilia.IndiceExtra(k) >= 0).GroupBy(k => k).Select(g => g.Key + " " + g.Count()).OrderBy(x => x, StringComparer.Ordinal);
                var ax = fotoExtras.Actuales.Where(a => CatalogoFamilia.IndiceExtra(a.Serie) >= 0).Select(a => a.Serie + " " + a.Rol + " " + F(a.Precio) + " K" + F(a.Strike) + " " + F(a.GexM) + "M");
                P(); P("== 6b. Extras (Replica20, como en produccion): " + conExtra + " de " + host.Motor.Almacen.Cuantos + " minutos con series extra; generacion " + (host.Replica?.Generacion ?? -1)
                       + ", resondeos " + (host.Replica?.Resondeos ?? -1) + ", minutos rehechos " + host.Motor.MinutosRehechos);
                P("    velas m2 por serie extra: " + string.Join(" | ", hx));
                P("    actuales extra: " + string.Join(" | ", ax));
                foreach (var fe in fotoExtras.Fuentes.Where(x => x.Libro.StartsWith("2.0 ", StringComparison.Ordinal))) P("    fuente " + fe.Libro + ": " + fe.Texto);
                resumen["extras_minutos"] = conExtra;
            }
            if (esc == "ny-reinicio" || esc == "ny-recalculo" || esc == "ny-vivo" || esc == "ny-extras")
            {
                var pNy = Path.Combine(res, "foto-ny.txt");
                if (File.Exists(pNy))
                {
                    var a = File.ReadAllLines(pNy); var b = volcado.Split('\n').Where(x => x.Length > 0).ToArray();
                    int dif = 0; var ej = new List<string>();
                    var sa = new HashSet<string>(a); var sb = new HashSet<string>(b);
                    foreach (var x in a) if (!sb.Contains(x)) { dif++; if (ej.Count < 6) ej.Add("solo ny:  " + x); }
                    foreach (var x in b) if (!sa.Contains(x)) { dif++; if (ej.Count < 12) ej.Add("solo " + esc + ": " + x); }
                    P(); P("== 6. Reinicio: la FotoFamilia de '" + esc + "' contra la de 'ny' (" + a.Length + " lineas de volcado: historia por vela y serie + actuales + TQQQ): " + (dif == 0 ? "IDENTICA" : dif + " lineas distintas"));
                    foreach (var x in ej) P("    " + x);
                    // por serie (lineas H de la historia): cuantas velas difieren
                    string Ser(string l) { var p = l.Split(' '); return p[0] == "H" && p.Length > 2 ? p[2] : p[0] == "A" && p.Length > 1 ? "actual " + p[1] : "conv TQQQ"; }
                    var porSer = a.Where(x => !sb.Contains(x)).Select(Ser).Concat(b.Where(x => !sa.Contains(x)).Select(Ser)).GroupBy(x => x).OrderBy(g => g.Key, StringComparer.Ordinal);
                    foreach (var g in porSer) P("    " + g.Key.PadRight(24) + g.Count() + " lineas distintas");
                    if (esc == "ny-vivo")
                    {
                        // atribucion vivo vs historico: una vela de una serie de la familia (sin TQQQ ni el zero de NDX, que en vivo rehace su cache cada minuto
                        // por diseno) solo puede diferir si su minuto (o el anterior, que es el que dibuja si falta) tuvo otro fut en vivo
                        var velasDif = a.Where(x => !sb.Contains(x)).Concat(b.Where(x => !sa.Contains(x))).Where(x => x.StartsWith("H ")).Select(x => x.Split(' '))
                                        .Where(p => !p[2].StartsWith("T_") && p[2] != "ZEST_NDX_vol" && p[2] != "ZTP_NDX_vol").Select(p => (T: long.Parse(p[1], Inv), S: p[2])).Distinct().ToList();
                        int sinExplicar = velasDif.Count(v => !(_futDistinto.Contains(v.T / 60000) || _futDistinto.Contains(v.T / 60000 - 1) || _futDistinto.Contains(v.T / 60000 + 1)));
                        P("    familia (sin TQQQ ni zero NDX): " + velasDif.Count + " vela-serie distintas; " + (velasDif.Count - sinExplicar) + " en velas con un minuto de fut distinto en vivo, " + sinExplicar + " sin explicar"
                          + (sinExplicar > 0 ? ": " + string.Join(" ", velasDif.Where(v => !(_futDistinto.Contains(v.T / 60000) || _futDistinto.Contains(v.T / 60000 - 1) || _futDistinto.Contains(v.T / 60000 + 1))).Take(100).Select(v => Hm(v.T) + " " + v.S)) : ""));
                    }
                    resumen["reinicio_lineas_distintas"] = dif;
                }
            }

            // ---------------------------------------------------------------- atribucion (despues de medir: estos dobles cargan mucho en memoria;
            // antes de parar el host: la manija de CBOE parada ya no devuelve fotos)
            if (semilla && esc != "ny-extras")
            {
                P(); P("== 7. Atribucion: fotos de CBOE de la semilla contra las que uso la referencia (union de carpetas, gana la bajada mas temprana)");
                try { Atribuir(host, raiz, app); } catch (Exception e) { P("  atribucion fallo: " + e.Message); }
            }
            host.Parar();
            Medir("host parado");

            // ---------------------------------------------------------------- soltar (persiste la cinta .bin en la carpeta temporal)
            var sw4 = Stopwatch.StartNew();
            cinta.Soltar(dueno); libro.Soltar(dueno);
            P(); P("  cinta soltada (persistio .bin) en " + sw4.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s: " + string.Join(" ", Directory.GetFiles(Path.Combine(datos4, "cinta"), "seg-*").Select(x => Path.GetFileName(x) + " " + (new FileInfo(x).Length >> 10) + " KB")));
            P("  escrito en la carpeta temporal: familia\\ " + string.Join(" ", Directory.GetFiles(Path.Combine(datos4, "familia")).Select(x => Path.GetFileName(x) + " " + (new FileInfo(x).Length >> 10) + " KB")));
            P("  logs: " + string.Join(" ", Directory.GetFiles(logs).Select(x => Path.GetFileName(x) + " " + (new FileInfo(x).Length >> 10) + " KB")));
            foreach (var l in new[] { "pythiagex4-familia.log", "pythiagex4-integracion.log" })
            {
                var p = Path.Combine(logs, l); if (!File.Exists(p)) continue;
                var err = File.ReadAllLines(p).Where(x => x.Contains("ERROR")).ToList();
                P("  " + l + ": " + (err.Count == 0 ? "sin ERROR" : err.Count + " lineas con ERROR, la primera: " + err[0]));
            }
            var pc = Path.Combine(logs, "pythiagex4-cboe.log");
            if (File.Exists(pc)) foreach (var l in File.ReadAllLines(pc).Where(x => x.Contains("historia") || x.Contains("modo") || x.Contains("fallo"))) P("  cboe.log: " + l);

            P(); P("== RESUMEN '" + esc + "'");
            P("  tiempos: semilla+carga cinta " + tCinta.ToString("0.0", Inv) + " s, libro NQ " + tLibro.ToString("0.0", Inv) + " s, historia CBOE " + tCboe.ToString("0.0", Inv) + " s, sesion completa " + tFin.ToString("0.0", Inv) + " s desde el host");
            P("  memoria: pico working set " + _medidas.Max(m => m.Pico) + " MB; heap retenido con todo vivo (tras GC) " + memFinal.Heap + " MB; privada " + memFinal.Priv + " MB");
            P("  velas m2 de la cinta " + (okVelas ? "IGUALES" : "con diferencias") + " | minutos (21 series) " + (difMin == 0 ? "IGUALES" : difMin + " series-minuto distintas") + " | historia de la foto " + (okHist ? "IGUAL" : "con diferencias")
              + " | actuales " + (okAct ? "IGUALES" : "con diferencias") + " | TQQQ " + (okTq ? "IGUAL" : "con diferencias"));
            File.WriteAllText(Path.Combine(res, "e2e-" + esc + ".json"), JsonSerializer.Serialize(new
            {
                escenario = esc, reloj = RELOJ, t_cinta = tCinta, t_libro = tLibro, t_cboe = tCboe, t_sesion = tFin, minutos = host.Motor.MinutosCalculados, con_libros = host.Motor.Almacen.Cuantos,
                pico_ws_mb = _medidas.Max(m => m.Pico), heap_final_mb = memFinal.Heap, http = host.Http.Llamadas, resumen
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        // ==============================================================================================================================
        static long Copiar(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            using var i = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
            using var o = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
            i.CopyTo(o, 1 << 20);
            File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src));
            return o.Length;
        }

        // ------------------------------------------------------------------------------------------------------------------------------
        sealed class Cand { public DateTime Gen; public string Ruta; public long Off; public int Len; public int Idx; }
        static readonly Regex RxTs = new Regex("\"cadena_ts\":\"([^\"]+)\"", RegexOptions.Compiled);
        static readonly Regex RxGen = new Regex("\"generado\":\"([^\"]+)\"", RegexOptions.Compiled);
        static readonly byte[] FilasA = Encoding.ASCII.GetBytes("\"filas\":[["), FilasB = Encoding.ASCII.GetBytes("\"filas\": [[");

        /// <summary>Recorre las lineas de un archivo (gz o plano) sin armar el texto entero: (indice, offset en el archivo plano, largo, bytes).</summary>
        static void Lineas(string ruta, Action<int, long, ReadOnlyMemory<byte>> f)
        {
            bool gz = ruta.EndsWith(".gz", StringComparison.Ordinal);
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
            using Stream s = gz ? new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress) : (Stream)fs;
            var buf = new byte[1 << 22]; int len = 0; long base0 = 0; int idx = 0;
            while (true)
            {
                if (len == buf.Length) Array.Resize(ref buf, buf.Length * 2);
                int k; try { k = s.Read(buf, len, buf.Length - len); } catch (InvalidDataException) { k = 0; }
                if (k <= 0) break;
                len += k; int ini = 0;
                for (int i = 0; i < len; i++)
                {
                    if (buf[i] != (byte)'\n') continue;
                    f(idx++, base0 + ini, new ReadOnlyMemory<byte>(buf, ini, i - ini));
                    ini = i + 1;
                }
                if (ini > 0) { Buffer.BlockCopy(buf, ini, buf, 0, len - ini); len -= ini; base0 += ini; }
            }
            if (len > 0) f(idx, base0, new ReadOnlyMemory<byte>(buf, 0, len));
        }

        /// <summary>La union de las cadenas de un ticker como las lee la referencia (libros.fotos_cboe): un sello -> la linea con filas de 'generado' mas
        /// temprano, de [dia0, dia1] (archivos de [d-1, d]), sin la pausa 21-22 UTC y hasta 'hasta'. Lineas crudas, escritas por dia UTC de 'generado'.</summary>
        static (int Lineas, int Elegidas, int Archivos) SembrarUnion(string app, string archivo, string dest, DateTime dia0, DateTime dia1, DateTime hasta)
        {
            var p1 = Path.Combine(app, "PythiaGex"); var p2 = Path.Combine(app, "PythiaGex2");
            var rutas = new List<string>();
            for (var d = dia0; d <= dia1; d = d.AddDays(1))
            {
                var ds = d.ToString("yyyy-MM-dd", Inv);
                foreach (var c in new[] { Path.Combine(p1, "cadenas"), Path.Combine(p2, "cadenas"), Path.Combine(p1, "cboe-local") })
                { var p = Path.Combine(c, "cadena-" + archivo + "-" + ds + ".jsonl.gz"); if (File.Exists(p)) rutas.Add(p); }
                var pl = Path.Combine(p1, "cadenas", "local-" + archivo + "-" + ds + ".jsonl"); if (File.Exists(pl)) rutas.Add(pl);
            }
            rutas.Sort(StringComparer.Ordinal);
            var mejor = new Dictionary<string, Cand>(StringComparer.Ordinal);
            int nl = 0;
            foreach (var r in rutas)
                Lineas(r, (idx, off, m) =>
                {
                    nl++;
                    var sp = m.Span;
                    var cab = Encoding.UTF8.GetString(sp.Slice(0, Math.Min(500, sp.Length)));
                    var mt = RxTs.Match(cab); var mg = RxGen.Match(cab);
                    if (!mt.Success || !mg.Success) return;
                    if (!DateTimeOffset.TryParse(mg.Groups[1].Value.Replace("\\u002B", "+"), Inv, DateTimeStyles.AssumeUniversal, out var go)) return;
                    var gen = DateTime.SpecifyKind(go.UtcDateTime, DateTimeKind.Utc);
                    var hm = gen.TimeOfDay;
                    if (gen > hasta || gen < dia0 || (hm > TimeSpan.FromHours(21) && hm < TimeSpan.FromHours(22))) return;
                    if (sp.IndexOf(FilasA) < 0 && sp.IndexOf(FilasB) < 0) return;           // sin filas: la referencia no registra el sello
                    string ts = mt.Groups[1].Value;
                    if (mejor.TryGetValue(ts, out var c) && c.Gen <= gen) return;
                    mejor[ts] = new Cand { Gen = gen, Ruta = r, Off = off, Len = sp.Length, Idx = idx };
                });
            // segunda pasada: escribir las elegidas, por dia UTC de 'generado'
            var porDia = mejor.Values.GroupBy(c => c.Gen.Date).ToDictionary(g => g.Key, g => new HashSet<(string, int)>(g.Select(c => (c.Ruta, c.Idx))));
            var esc = new Dictionary<DateTime, (FileStream F, System.IO.Compression.GZipStream Z)>();
            Directory.CreateDirectory(dest);
            try
            {
                foreach (var r in rutas)
                {
                    var queridas = new HashSet<int>(mejor.Values.Where(c => c.Ruta == r).Select(c => c.Idx));
                    if (queridas.Count == 0) continue;
                    var gen = mejor.Values.Where(c => c.Ruta == r).ToDictionary(c => c.Idx, c => c.Gen);
                    Lineas(r, (idx, off, m) =>
                    {
                        if (!queridas.Contains(idx)) return;
                        var dia = gen[idx].Date;
                        if (!esc.TryGetValue(dia, out var w))
                        {
                            var fs = new FileStream(Path.Combine(dest, "cadena-" + archivo + "-" + dia.ToString("yyyy-MM-dd", Inv) + ".jsonl.gz"), FileMode.Create, FileAccess.Write);
                            w = (fs, new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Fastest));
                            esc[dia] = w;
                        }
                        w.Z.Write(m.Span); w.Z.WriteByte((byte)'\n');
                    });
                }
            }
            finally { foreach (var w in esc.Values) { w.Z.Dispose(); w.F.Dispose(); } }
            return (nl, mejor.Count, rutas.Count);
        }

        static JsonDocument LeerJson(string ruta)
        {
            var txt = File.ReadAllText(ruta, Encoding.UTF8);
            txt = Regex.Replace(txt, @"(?<=[\[,:\s])(NaN|-?Infinity)(?=[\],}\s])", "null");
            return JsonDocument.Parse(txt);
        }

        static double Num(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : double.NaN;
        static bool IgualN(double a, double b) => (double.IsNaN(a) && double.IsNaN(b)) || a == b;
        static string F(double v) => double.IsNaN(v) ? "NaN" : v.ToString("R", Inv);
        static string Hm(long ms) => SesionFamilia.DeMs(ms).ToString("MM-dd HH:mm", Inv);

        static bool MismaFoto(FotoFamilia a, FotoFamilia b) => a != null && b != null && Volcar(a) == Volcar(b);

        /// <summary>Volcado canonico de la foto: historia (vela, serie, precios) + actuales + conversion TQQQ (para comparar escenarios).</summary>
        /// <summary>4.1.4: la foto sin las series extra (R20_*, DOMS_*) ni las fuentes de las replicas ("2.0 QQQ", "2.0 NDX"): lo que tiene que dar igual
        /// a una corrida sin la Replica20.</summary>
        static FotoFamilia SinExtras(FotoFamilia f)
        {
            var c = f.Copia();
            var h = new Dictionary<long, IReadOnlyDictionary<string, double[]>>();
            foreach (var kv in f.HistoriaM2)
            {
                var e = kv.Value.Where(x => CatalogoFamilia.IndiceExtra(x.Key) < 0).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
                if (e.Count > 0) h[kv.Key] = e;
            }
            c.HistoriaM2 = h;
            c.Actuales = f.Actuales.Where(a => CatalogoFamilia.IndiceExtra(a.Serie) < 0).ToList();
            c.Fuentes = f.Fuentes.Where(x => !(x.Libro ?? "").StartsWith("2.0 ", StringComparison.Ordinal)).ToList();
            return c;
        }

        static string Volcar(FotoFamilia f)
        {
            var sb = new StringBuilder();
            foreach (var T in f.HistoriaM2.Keys.OrderBy(x => x))
                foreach (var kv in f.HistoriaM2[T].OrderBy(x => x.Key, StringComparer.Ordinal))
                    sb.Append("H ").Append(T).Append(' ').Append(kv.Key).Append(' ').Append(string.Join(",", kv.Value.Select(F))).Append('\n');
            foreach (var a in f.Actuales)
                sb.Append("A ").Append(a.Serie).Append(' ').Append(a.Rol).Append(' ').Append(F(a.Precio)).Append(' ').Append(F(a.Strike)).Append(' ').Append(F(a.GexM)).Append(' ')
                  .Append(a.DatoUtc.ToString("o", Inv)).Append(' ').Append(a.OiViejo).Append('\n');
            sb.Append("T ").Append(F(f.TqS)).Append(' ').Append(F(f.TqC)).Append(' ').Append(f.TqDatoUtc.ToString("o", Inv)).Append('\n');
            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------------------------------------------
        static bool CompararVelas(ICinta cinta, JsonElement sal, Dictionary<string, object> resumen)
        {
            var refV = sal.GetProperty("velas").EnumerateArray().Select(v => (T: v[0].GetInt64(), O: v[1].GetDouble(), H: v[2].GetDouble(), L: v[3].GetDouble(), C: v[4].GetDouble())).ToList();
            var mias = cinta.VelasM2(INI, FIN).ToDictionary(v => v.Ms);
            int ig = 0, dif = 0, soloRef = 0; var ej = new List<string>();
            foreach (var r in refV)
            {
                if (!mias.TryGetValue(r.T, out var m)) { soloRef++; if (ej.Count < 5) ej.Add("solo ref " + Hm(r.T)); continue; }
                if (m.O == r.O && m.H == r.H && m.L == r.L && m.C == r.C) ig++;
                else { dif++; if (ej.Count < 5) ej.Add(Hm(r.T) + " ref " + r.O + "/" + r.H + "/" + r.L + "/" + r.C + " c# " + m.O + "/" + m.H + "/" + m.L + "/" + m.C); }
            }
            var setRef = new HashSet<long>(refV.Select(r => r.T));
            int soloMias = mias.Keys.Count(t => !setRef.Contains(t));
            P("  velas m2 de la cinta (ICinta.VelasM2 de CintaFamilia vs velas de la vista previa): iguales " + ig + ", distintas " + dif + ", solo ref " + soloRef + ", solo c# " + soloMias + " (ref " + refV.Count + ")");
            foreach (var x in ej) P("    " + x);
            resumen["velas"] = new { ig, dif, soloRef, soloMias };
            return dif + soloRef + soloMias == 0;
        }

        /// <summary>Minuto a minuto, las 21 series calculadas (motor.Almacen) contra ref.niv. Devuelve cuantas series-minuto difieren.</summary>
        static int CompararMinutos(MotorFamilia motor, JsonElement niv, Dictionary<string, object> resumen)
        {
            var calc = CatalogoFamilia.Calc;
            var st = calc.ToDictionary(s => s, s => new int[7]);   // exactos, a<=0,01, distintos, solo ref, solo c#, distintos por etiqueta/cantidad, > 1 pt
            var maxD = calc.ToDictionary(s => s, s => 0.0);       // mayor |precio c# - precio ref| entre los comparables (misma etiqueta y cantidad)
            var ej = new List<string>(); int minRef = 0, minMio = 0, ambos = 0;
            var primeraDif = new Dictionary<string, long>();
            for (long k = TiempoFam.Clave(INI); k < TiempoFam.Clave(FIN); k++)
            {
                bool hayRef = niv.TryGetProperty(k.ToString(Inv), out var rr);
                var r = motor.Almacen.Get(k);
                if (hayRef) minRef++; if (r != null) minMio++; if (hayRef && r != null) ambos++;
                for (int s = 0; s < calc.Length; s++)
                {
                    string sid = calc[s];
                    bool hp = hayRef && rr.TryGetProperty(sid, out var lp) && lp.GetArrayLength() > 0;
                    var lm = r?.Series?[s]; bool hc = lm != null && lm.Length > 0;
                    if (!hp && !hc) continue;
                    int c;
                    if (hp && !hc) c = 3; else if (!hp && hc) c = 4;
                    else
                    {
                        rr.TryGetProperty(sid, out lp);
                        if (lp.GetArrayLength() != lm.Length) { c = 2; st[sid][5]++; }
                        else
                        {
                            bool ex = true, tol = true, etq = true; int i = 0; double md = 0;
                            foreach (var x in lp.EnumerateArray())
                            {
                                double pp = x[0].GetDouble(); string pe = x[1].GetString(); double pm = Num(x[2]); var m = lm[i++];
                                if (pe != m.Etq) { ex = tol = etq = false; break; }
                                md = Math.Max(md, Math.Abs(pp - m.Precio));
                                if (pp != m.Precio || !IgualN(pm, m.GexM)) ex = false;
                                if (Math.Abs(pp - m.Precio) > 0.01 + 1e-9 || !((double.IsNaN(pm) && double.IsNaN(m.GexM)) || Math.Abs(pm - m.GexM) <= 0.1 + 1e-9)) tol = false;
                            }
                            if (!etq) st[sid][5]++; else { if (md > maxD[sid]) maxD[sid] = md; if (md > 1.0) st[sid][6]++; }
                            c = ex ? 0 : tol ? 1 : 2;
                        }
                    }
                    st[sid][c]++;
                    if (c >= 2)
                    {
                        if (!primeraDif.ContainsKey(sid)) primeraDif[sid] = k;
                        if (ej.Count < 14)
                        {
                            string py = hayRef && rr.TryGetProperty(sid, out var lp2) ? lp2.ToString() : "-";
                            string cs = hc ? "[" + string.Join(",", lm.Select(m => "[" + F(m.Precio) + "," + m.Etq + "," + F(m.GexM) + "]")) + "]" : "-";
                            ej.Add(Hm(k * 60000) + " " + sid + ": ref " + py + " | c# " + cs);
                        }
                    }
                }
            }
            P("  minutos con niveles: referencia " + minRef + ", c# " + minMio + ", ambos " + ambos);
            P("  " + "serie".PadRight(16) + "exactos".PadLeft(9) + "a<=0,01".PadLeft(9) + "distintos".PadLeft(11) + "solo ref".PadLeft(10) + "solo c#".PadLeft(9) + "  otra etq/cant" + "  > 1 pt" + "  max dif pts" + "   primera diferencia");
            int tot = 0;
            var porSerie = new Dictionary<string, int[]>();
            foreach (var sid in calc)
            {
                var a = st[sid]; porSerie[sid] = a;
                tot += a[2] + a[3] + a[4];
                bool def = CatalogoFamilia.PrendidasPorDefecto.Contains(sid);
                P("  " + (sid + (def ? " *" : "")).PadRight(16) + a[0].ToString(Inv).PadLeft(9) + a[1].ToString(Inv).PadLeft(9) + a[2].ToString(Inv).PadLeft(11) + a[3].ToString(Inv).PadLeft(10) + a[4].ToString(Inv).PadLeft(9)
                  + a[5].ToString(Inv).PadLeft(15) + a[6].ToString(Inv).PadLeft(8) + maxD[sid].ToString("0.00", Inv).PadLeft(13)
                  + "   " + (primeraDif.TryGetValue(sid, out var k0) ? Hm(k0 * 60000) + " UTC" : "-"));
            }
            P("  (* = prendida por defecto en el indicador)");
            foreach (var x in ej) P("    " + x);
            resumen["minutos"] = porSerie;
            return tot;
        }

        /// <summary>FotoFamilia.HistoriaM2 contra salida.historia, en las velas de la referencia (todas las series de la referencia, TRES incluidas).</summary>
        static bool CompararHistoria(FotoFamilia foto, JsonElement sal, Dictionary<string, object> resumen)
        {
            var refH = new Dictionary<(string, long), double[]>();
            var series = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var se in sal.GetProperty("historia").EnumerateObject())
            {
                series.Add(se.Name);
                foreach (var x in se.Value.EnumerateArray()) refH[(se.Name, x[0].GetInt64())] = x[1].EnumerateArray().Select(y => y[0].GetDouble()).ToArray();
            }
            foreach (var e in foto.HistoriaM2.Values) foreach (var s in e.Keys) if (!s.StartsWith("T_", StringComparison.Ordinal)) series.Add(s);
            var velas = sal.GetProperty("velas").EnumerateArray().Select(v => v[0].GetInt64()).ToList();
            var setV = new HashSet<long>(velas);
            P("  historia de la foto por vela m2 (" + velas.Count + " velas de la referencia; precios exactos):");
            P("  " + "serie".PadRight(16) + "iguales".PadLeft(9) + "a<=0,01".PadLeft(9) + "distintas".PadLeft(11) + "solo ref".PadLeft(10) + "solo c#".PadLeft(9));
            bool ok = true; var ej = new List<string>(); var porSerie = new Dictionary<string, int[]>();
            foreach (var sid in series)
            {
                var a = new int[5];
                foreach (var T in velas)
                {
                    bool hp = refH.TryGetValue((sid, T), out var pr); double[] mio = null;
                    bool hc = foto.HistoriaM2.TryGetValue(T, out var e) && e.TryGetValue(sid, out mio);
                    if (!hp && !hc) continue;
                    if (hp && !hc) { a[3]++; if (ej.Count < 10) ej.Add(sid + " " + Hm(T) + " solo ref " + string.Join(",", pr.Select(F))); continue; }
                    if (!hp) { a[4]++; if (ej.Count < 10) ej.Add(sid + " " + Hm(T) + " solo c# " + string.Join(",", mio.Select(F))); continue; }
                    if (pr.Length == mio.Length && pr.Zip(mio, (x, y) => x == y).All(b => b)) a[0]++;
                    else if (pr.Length == mio.Length && pr.Zip(mio, (x, y) => Math.Abs(x - y) <= 0.01 + 1e-9).All(b => b)) a[1]++;
                    else { a[2]++; if (ej.Count < 10) ej.Add(sid + " " + Hm(T) + " ref " + string.Join(",", pr.Select(F)) + " c# " + string.Join(",", mio.Select(F))); }
                }
                // velas de la foto dentro de la sesion que la referencia no tiene
                int extra = foto.HistoriaM2.Count(kv => kv.Key >= SesionFamilia.Ms(INI) && kv.Key < SesionFamilia.Ms(FIN) && !setV.Contains(kv.Key) && kv.Value.ContainsKey(sid));
                a[4] += extra;
                porSerie[sid] = a;
                if (a[1] + a[2] + a[3] + a[4] > 0) ok = false;
                bool def = CatalogoFamilia.PrendidasPorDefecto.Contains(sid);
                P("  " + (sid + (def ? " *" : "")).PadRight(16) + a[0].ToString(Inv).PadLeft(9) + a[1].ToString(Inv).PadLeft(9) + a[2].ToString(Inv).PadLeft(11) + a[3].ToString(Inv).PadLeft(10) + a[4].ToString(Inv).PadLeft(9));
            }
            foreach (var x in ej) P("    " + x);
            var extras = foto.HistoriaM2.Keys.Where(T => T >= SesionFamilia.Ms(INI) && T < SesionFamilia.Ms(FIN) && !setV.Contains(T)).OrderBy(T => T).ToList();
            if (extras.Count > 0)
                P("    velas de la foto que la referencia no lista (la vista previa solo dibuja donde su cinta tiene vela; la 4.1 dibuja cada balde de 2 min de la sesion): "
                  + extras.Count + ": " + string.Join(" ", extras.Select(T => SesionFamilia.DeMs(T).ToString("HH:mm", Inv))));
            resumen["historia"] = porSerie;
            return ok;
        }

        static bool CompararActuales(FotoFamilia foto, JsonElement sal, Dictionary<string, object> resumen)
        {
            var refA = sal.GetProperty("actuales").EnumerateArray().ToList();
            var act = foto.Actuales.Where(a => a.Libro != "TQQQ").ToList();
            int ok = 0, mal = 0; var ej = new List<string>();
            if (refA.Count != act.Count) { mal++; ej.Add("cantidad: ref " + refA.Count + ", c# " + act.Count); }
            for (int i = 0; i < Math.Min(refA.Count, act.Count); i++)
            {
                var pa = refA[i]; var c = act[i];
                double sp = Num(pa.GetProperty("strike")), gp = Num(pa.GetProperty("gex_musd"));
                string dp = pa.GetProperty("dato_utc").ValueKind == JsonValueKind.String ? pa.GetProperty("dato_utc").GetString() : "", dc = c.DatoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
                bool igual = pa.GetProperty("serie").GetString() == c.Serie && pa.GetProperty("rol").GetString() == c.Rol && pa.GetProperty("precio").GetDouble() == c.Precio
                             && IgualN(sp, c.Strike) && IgualN(gp, c.GexM) && dp == dc && pa.GetProperty("oi_viejo").GetBoolean() == c.OiViejo;
                if (igual) ok++;
                else { mal++; if (ej.Count < 10) ej.Add("ref " + pa.GetProperty("serie").GetString() + " " + pa.GetProperty("rol").GetString() + " " + F(pa.GetProperty("precio").GetDouble()) + " k " + F(sp) + " g " + F(gp) + " " + dp + " oiv " + pa.GetProperty("oi_viejo").GetBoolean()
                                                         + " | c# " + c.Serie + " " + c.Rol + " " + F(c.Precio) + " k " + F(c.Strike) + " g " + F(c.GexM) + " " + dc + " oiv " + c.OiViejo); }
            }
            P("  actuales (sin TQQQ): iguales " + ok + ", distintos " + mal + " (ref " + refA.Count + ")");
            foreach (var x in ej) P("    " + x);
            // los prendidos por defecto, con su edad (protocolo: edad ANTES del numero si > 30 min)
            foreach (var a in act.Where(a => CatalogoFamilia.PrendidasPorDefecto.Contains(a.Serie)))
            {
                double edadMin = (RELOJ - a.DatoUtc).TotalMinutes;
                P("    defecto " + a.Serie.PadRight(14) + (edadMin > 30 ? "(dato de hace " + edadMin.ToString("0", Inv) + " min) " : "") + a.Rol.PadRight(7) + " " + a.Precio.ToString("0.00", Inv) + " NQ"
                  + (double.IsNaN(a.Strike) ? "" : " (strike " + a.Strike.ToString("0.##", Inv) + ")") + (double.IsNaN(a.GexM) ? "" : " " + a.GexM.ToString("0.0", Inv) + " M USD") + (a.OiViejo ? " OI viejo" : ""));
            }
            resumen["actuales"] = new { ok, mal };
            return mal == 0;
        }

        static void CompararFuentes(FotoFamilia foto, JsonElement sal)
        {
            var fu = sal.GetProperty("fuentes");
            foreach (var lb in new[] { "NQ", "NDX", "QQQ" })
            {
                var mia = foto.Fuentes.FirstOrDefault(f => f.Libro == lb);
                string rconv = fu.TryGetProperty(lb, out var r) && r.TryGetProperty("conv", out var c) ? c.GetString() : "-";
                string rdato = fu.TryGetProperty(lb, out r) && r.TryGetProperty("dato_utc", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : "-";
                P("  fuente " + lb.PadRight(4) + "ref: " + rconv + " | dato " + rdato);
                P("  " + "".PadRight(11) + "c#:  " + (mia?.Texto ?? "-") + " | dato " + (mia == null ? "-" : mia.DatoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv)) + (mia != null && double.IsFinite(mia.ConvValor) ? " | valor " + F(mia.ConvValor) : ""));
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------
        static bool CompararTqqq(string titulo, FotoFamilia foto, IReadOnlyDictionary<long, TqqqVela> velas, JsonElement tq, Dictionary<string, object> resumen)
        {
            bool ok = true;
            var conv = tq.GetProperty("conv");
            double sR = conv.GetProperty("s").GetDouble(), cR = conv.GetProperty("c").GetDouble();
            TqqqVela ult = null; foreach (var kv in velas.OrderBy(x => x.Key)) if (kv.Value != null) ult = kv.Value;
            double sM = foto != null ? foto.TqS : ult?.S ?? double.NaN, cM = foto != null ? foto.TqC : ult?.C ?? double.NaN;
            P("  [" + titulo + "] conversion final: s " + F(sM) + " (ref " + F(sR) + ")  c " + F(cM) + " (ref " + F(cR) + ")  " + (sM == sR && cM == cR ? "IGUAL" : "DISTINTA"));
            if (!(sM == sR && cM == cR)) ok = false;
            var porSerie = new Dictionary<string, int[]>();
            var ej = new List<string>();
            foreach (var se in tq.GetProperty("historia").EnumerateObject())
            {
                var a = new int[5]; double maxDif = 0;
                var refT = new Dictionary<long, (double[] P, string[] E)>();
                foreach (var x in se.Value.EnumerateArray()) refT[x[0].GetInt64()] = (x[1].EnumerateArray().Select(y => y[0].GetDouble()).ToArray(), x[1].EnumerateArray().Select(y => y[1].GetString()).ToArray());
                var claves = new SortedSet<long>(refT.Keys);
                foreach (var kv in velas) if (kv.Value?.Series != null && kv.Value.Series.TryGetValue(se.Name, out var lv) && lv != null && lv.Length > 0) claves.Add(kv.Key);
                foreach (var T in claves)
                {
                    bool hp = refT.TryGetValue(T, out var r);
                    Nivel[] lm = null; bool hc = velas.TryGetValue(T, out var v) && v?.Series != null && v.Series.TryGetValue(se.Name, out lm) && lm != null && lm.Length > 0;
                    if (hp && !hc) { a[3]++; continue; }
                    if (!hp && hc) { a[4]++; continue; }
                    if (r.P.Length != lm.Length || !r.E.Zip(lm, (x, y) => x == y.Etq).All(b => b)) { a[2]++; if (ej.Count < 6) ej.Add(se.Name + " " + Hm(T) + " ref " + string.Join(",", r.P.Select(F)) + " c# " + string.Join(",", lm.Select(n => F(n.Precio) + n.Etq))); continue; }
                    double md = r.P.Zip(lm, (x, y) => Math.Abs(x - y.Precio)).Max(); if (md > maxDif) maxDif = md;
                    if (md == 0) a[0]++; else if (md <= 0.01 + 1e-9) a[1]++; else { a[2]++; if (ej.Count < 6) ej.Add(se.Name + " " + Hm(T) + " ref " + string.Join(",", r.P.Select(F)) + " c# " + string.Join(",", lm.Select(n => F(n.Precio)))); }
                    // la foto publicada tiene que dibujar lo mismo que la vela
                    if (foto != null && (!foto.HistoriaM2.TryGetValue(T, out var e) || !e.TryGetValue(se.Name, out var ph) || ph.Length != lm.Length || !ph.Zip(lm, (x, y) => x == y.Precio).All(b => b))) { ok = false; if (ej.Count < 8) ej.Add(se.Name + " " + Hm(T) + ": la historia de la foto no coincide con la vela de TQQQ"); }
                }
                porSerie[se.Name] = a;
                if (a[1] + a[2] + a[3] + a[4] > 0) ok = false;
                P("  [" + titulo + "] " + se.Name.PadRight(12) + " iguales " + a[0].ToString(Inv).PadLeft(4) + " | a<=0,01 " + a[1].ToString(Inv).PadLeft(4) + " | distintas " + a[2].ToString(Inv).PadLeft(4) + " | solo ref " + a[3].ToString(Inv).PadLeft(4)
                  + " | solo c# " + a[4].ToString(Inv).PadLeft(4) + " | max dif " + maxDif.ToString("0.00", Inv) + " pts" + (CatalogoFamilia.PrendidasPorDefecto.Contains(se.Name) ? "  (prendida por defecto)" : ""));
            }
            foreach (var x in ej) P("    " + x);
            if (foto != null)
            {
                var refA = tq.GetProperty("actuales").EnumerateArray().ToList();
                var act = foto.Actuales.Where(a => a.Libro == "TQQQ").ToList();
                int ig = 0, mal = 0; var ej2 = new List<string>();
                if (refA.Count != act.Count) { mal++; ej2.Add("cantidad: ref " + refA.Count + " c# " + act.Count); }
                for (int i = 0; i < Math.Min(refA.Count, act.Count); i++)
                {
                    var pa = refA[i]; var c = act[i];
                    string dp = pa.GetProperty("dato_utc").GetString(), dc = c.DatoUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
                    bool igual = pa.GetProperty("serie").GetString() == c.Serie && pa.GetProperty("rol").GetString() == c.Rol && pa.GetProperty("precio").GetDouble() == c.Precio
                                 && IgualN(Num(pa.GetProperty("strike")), c.Strike) && IgualN(Num(pa.GetProperty("gex_musd")), c.GexM) && dp == dc;
                    if (igual) ig++; else { mal++; if (ej2.Count < 8) ej2.Add("ref " + pa.GetProperty("serie").GetString() + " " + pa.GetProperty("rol").GetString() + " " + F(pa.GetProperty("precio").GetDouble()) + " k " + F(Num(pa.GetProperty("strike"))) + " g " + F(Num(pa.GetProperty("gex_musd"))) + " " + dp
                                                                         + " | c# " + c.Serie + " " + c.Rol + " " + F(c.Precio) + " k " + F(c.Strike) + " g " + F(c.GexM) + " " + dc); }
                }
                P("  [" + titulo + "] actuales TQQQ: iguales " + ig + ", distintos " + mal + " (ref " + refA.Count + ")");
                foreach (var x in ej2) P("    " + x);
                if (mal > 0) ok = false;
            }
            if (resumen != null) resumen["tqqq"] = porSerie;
            return ok;
        }

        // ------------------------------------------------------------------------------------------------------------------------------
        /// <summary>Que fotos de NDX/QQQ/TQQQ tenia la referencia (union de carpetas, gana la bajada mas temprana) que la semilla (solo cboe-local)
        /// no tiene, o tiene con otro 'generado'. Solo lectura (los dobles de los arneses de B3b/B3c).</summary>
        static void Atribuir(HostE2E host, string raiz, string app)
        {
            var hasta = FIN;
            var desde = INI.AddDays(-6);
            var porLibro = new List<(string Libro, IReadOnlyList<FotoCadena> Ref, string Origen)>();
            var ndx = ArnesNdx.DobleCboe.ParaSesion(DateTime.SpecifyKind(DateTime.ParseExact(DIA, "yyyy-MM-dd", Inv), DateTimeKind.Utc), app, Path.Combine(raiz, "atas", "_test_cuatro_paridad", "ndx", "entrada"), s => P("  " + s.Trim()));
            porLibro.Add(("NDX", ndx.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue), "entrada congelada del arnes NDX (= fotos de la vista previa)"));
            foreach (var tk in new[] { "QQQ", "TQQQ" })
            {
                var q = new ParidadQqq.FuenteCboeArchivoQqq(app, tk, tk, DIA, hasta);
                porLibro.Add((tk, q.Fotos(tk, DateTime.MinValue, DateTime.MaxValue), q.Resumen));
                GC.Collect();
            }
            foreach (var (lb, refF, origen) in porLibro)
            {
                var mias = host.Cboe.Fotos(lb, desde, hasta.AddSeconds(1)).Where(f => !CalendarioNdx.EntreSesiones(f.GeneradoUtc, true)).ToList();
                var r = refF.Where(f => f.GeneradoUtc >= desde && f.GeneradoUtc <= hasta).ToList();
                var mTs = mias.GroupBy(f => f.TsUtc).ToDictionary(g => g.Key, g => g.Min(f => f.GeneradoUtc));
                var rTs = r.GroupBy(f => f.TsUtc).ToDictionary(g => g.Key, g => g.Min(f => f.GeneradoUtc));
                int soloRef = rTs.Keys.Count(k => !mTs.ContainsKey(k)), soloMia = mTs.Keys.Count(k => !rTs.ContainsKey(k));
                int otroGen = rTs.Count(kv => mTs.TryGetValue(kv.Key, out var g) && Math.Abs((g - kv.Value).TotalSeconds) >= 1);
                var sesR = r.Where(f => f.GeneradoUtc >= INI).GroupBy(f => f.TsUtc).ToDictionary(g => g.Key, g => g.Min(f => f.GeneradoUtc));
                int soloRefSes = sesR.Keys.Count(k => !mTs.ContainsKey(k)), otroGenSes = sesR.Count(kv => mTs.TryGetValue(kv.Key, out var g) && Math.Abs((g - kv.Value).TotalSeconds) >= 1);
                var ejOtro = sesR.Where(kv => mTs.TryGetValue(kv.Key, out var g) && Math.Abs((g - kv.Value).TotalSeconds) >= 1).Take(3).Select(kv => kv.Key.ToString("HH:mm:ss", Inv) + " ref gen " + kv.Value.ToString("HH:mm:ss", Inv) + " semilla " + mTs[kv.Key].ToString("HH:mm:ss", Inv));
                var ejSolo = sesR.Keys.Where(k => !mTs.ContainsKey(k)).OrderBy(k => k).Take(4).Select(k => k.ToString("MM-dd HH:mm:ss", Inv));
                P("  " + lb.PadRight(5) + "[" + Hm(SesionFamilia.Ms(desde)) + " .. 21:00] referencia " + rTs.Count + " sellos, semilla " + mTs.Count + " | solo en la referencia " + soloRef + " (en la sesion 08-10: " + soloRefSes + ")"
                  + ", solo en la semilla " + soloMia + ", mismo sello con otro 'generado' " + otroGen + " (en la sesion: " + otroGenSes + ")");
                P("        referencia: " + origen);
                if (soloRefSes > 0) P("        sellos de la sesion que la semilla no tiene (ej.): " + string.Join(", ", ejSolo));
                if (otroGenSes > 0) P("        otro 'generado' (ej.): " + string.Join(" | ", ejOtro));
            }
        }
    }
}
