// Program.cs — arnes de B-pos (PythiaGex 4.1.2, 08-10-2026): cambios por nivel (CambiosFamilia) con los datos REALES.
// Fuentes (SOLO LECTURA): %APPDATA%\ATAS\PythiaGex4\{viva\viva3-NQ-*.jsonl, cboe\cadena-*.jsonl.gz, familia\niv-2026-10-09-MNQZ6.jsonl} se COPIAN
// a una carpeta temporal y se leen de ahi con las piezas de produccion (LibroNqFamilia.AgregarLinea, BajadorCboe con Bajar=false,
// AlmacenNiveles.Parsear, SalidaFamilia.Actuales). Pruebas (las minimas del diseño + controles):
//   P1 saltos de OI reales (NQ/NDX/QQQ/TQQQ) y CERO saltos falsos (separacion: fraccion maxima fuera de los saltos).
//   P2 monto reconstruido: el perfil "de ahora" de CambiosFamilia da el GexM del niv en TODOS los niveles de libro de la noche (foto, S y t bien).
//   P3 identidad de linealidad: Δ por fotoΔ == Perfil(ahora) - Perfil(ahora con la columna de antes) a 1e-9 relativo (vol y OI, noche y rueda).
//   P4 cadena congelada => 0 exacto.   P5 volumen reiniciado => NaN.   P6 caso a mano (NQ, muro C, Δvol 15 min x gamma de ahora).
//   P7 balde FAM = suma de libros (contra ReglasFam.Fusion).   P8 TQQQ (TqqqPerfil) de punta a punta.   P9 tiempo por llamada < 50 ms.
//   SIM "esta noche": los minutos de niv-2026-10-09 con libros, anotados como en el host; ejemplos concretos.
// 09-10-2026 (arreglos de la revision adversarial):
//   P1 ademas: cada dia habil de NY tiene su salto en NDX/QQQ/TQQQ, cero cambios sin confirmar, pares con menos de 20 claves medidos.
//   P1b libro flaco (TQQQ adelgazado): salto con pocas claves confirmado; con muy pocas, NaN; la regla de antes lo perdia (DOS publicaciones).
//   P5 volumen de otro dia de negociacion => NaN (NQ por sesion de CME, NDX por fecha NY del dato); P5d reinicio del mismo dia => NaN.
//   P10 ventanas de volumen = HORA DEL DATO (noche).   P11 apertura 10-01..10-08: cero ventanas validas con la foto de antes de otro dia.
//   P7 FAM: OI solo con publicaciones de la misma noche; volumen sin ventana agregada si las de los libros no coinciden.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using PythiaGexCuatro.Cboe;
using PythiaGexCuatro.Familia;

namespace PruebaPosiciones
{
    /// <summary>ILibroNq con retencion de produccion (30 h contadas desde la foto mas nueva), sobre un libro cargado entero.</summary>
    sealed class LibroRecortado : ILibroNq
    {
        private readonly ILibroNq _b; private readonly DateTime _desde;
        public LibroRecortado(ILibroNq b, DateTime desde) { _b = b; _desde = desde; }
        public IReadOnlyList<FotoCadena> Fotos(DateTime d, DateTime h) => _b.Fotos(d < _desde ? _desde : d, h);
        public FotoCadena Ultima() => _b.Ultima();
        public long Version => _b.Version;
    }

    static class Program
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly StringBuilder Sal = new StringBuilder();
        static int _fallas, _pruebas;
        static void P(string s = "") { Console.WriteLine(s); Sal.AppendLine(s); }
        static void Ok(string nombre, bool ok, string detalle)
        {
            _pruebas++; if (!ok) _fallas++;
            P((ok ? "[OK]    " : "[FALLA] ") + nombre + (string.IsNullOrEmpty(detalle) ? "" : " — " + detalle));
        }
        static string F(double v, string fmt = "0.###") => double.IsNaN(v) ? "NaN" : v.ToString(fmt, Inv);
        static string H(DateTime t) => t == DateTime.MinValue || t == default ? "-" : t.ToString("MM-dd HH:mm:ss", Inv) + "Z";

        static int Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = Inv;
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            var swTodo = Stopwatch.StartNew();
            string aqui = AppContext.BaseDirectory;
            string dirArnes = Path.GetFullPath(Path.Combine(aqui, "..", ".."));
            string tmp = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "pg4_posiciones");
            string origen = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex4");
            string logs = Path.Combine(tmp, "logs");
            Directory.CreateDirectory(logs);
            foreach (var f in Directory.GetFiles(logs)) { try { File.Delete(f); } catch { } }    // logs de esta corrida solamente (carpeta temporal propia)
            AdaptadoresFamilia.Log = s => { try { File.AppendAllText(Path.Combine(logs, "adaptadores.log"), s + "\n"); } catch { } };
            AdaptadoresFamilia.Carpeta = Path.Combine(tmp, "datos4");

            P("=== arnes posiciones (B-pos 4.1.2) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " — " + CambiosFamilia.VERSION);
            P("origen (solo lectura): " + origen);
            P("copia de trabajo:      " + Path.Combine(tmp, "datos4"));
            // ---------------------------------------------------------------- copia de los datos (solo lectura del origen)
            string datos = Path.Combine(tmp, "datos4");
            var nivOrigen = Path.Combine(origen, "familia", "niv-2026-10-09-MNQZ6.jsonl");
            Copiar(nivOrigen, Path.Combine(datos, "familia", "niv-2026-10-09-MNQZ6.jsonl"));   // primero el niv: las fuentes copiadas despues son al menos tan nuevas
            foreach (var f in Directory.GetFiles(Path.Combine(origen, "viva"), "viva3-NQ-2026-10-0*.jsonl")) Copiar(f, Path.Combine(datos, "viva", Path.GetFileName(f)));
            foreach (var f in Directory.GetFiles(Path.Combine(origen, "cboe")))
            {
                var n = Path.GetFileName(f);
                if (n.StartsWith("cadena-") || n.StartsWith("ultima-") || n == "cierres.json") Copiar(f, Path.Combine(datos, "cboe", n));
            }

            // ---------------------------------------------------------------- el niv de esta noche
            var minutos = new List<RegistroMinuto>();
            foreach (var l in File.ReadAllLines(Path.Combine(datos, "familia", "niv-2026-10-09-MNQZ6.jsonl")))
            {
                var r = AlmacenNiveles.Parsear(l);
                if (r != null && r.TieneLibros) minutos.Add(r);
            }
            minutos = minutos.OrderBy(r => r.Clave).ToList();
            var tFin = TiempoFam.DeClave(minutos[minutos.Count - 1].Clave).AddMinutes(1);
            P("niv-2026-10-09-MNQZ6: " + minutos.Count + " minutos con libros, de " + H(TiempoFam.DeClave(minutos[0].Clave)) + " a " + H(TiempoFam.DeClave(minutos[minutos.Count - 1].Clave))
              + " (con NQ: " + minutos.Count(m => m.MetaDe("NQ") != null) + ")");

            // ---------------------------------------------------------------- fuentes
            var sw = Stopwatch.StartNew();
            var nqTodo = new LibroNqFamilia("NQ") { RetencionHoras = 1000 };
            int lineas = 0, buenas = 0;
            foreach (var f in Directory.GetFiles(Path.Combine(datos, "viva"), "viva3-NQ-*.jsonl").OrderBy(x => x, StringComparer.Ordinal))
                foreach (var l in File.ReadLines(f)) { lineas++; if (nqTodo.AgregarLinea(l, "archivo")) buenas++; }
            var nqU = nqTodo.Ultima();
            var nq30 = new LibroRecortado(nqTodo, nqU.TsUtc.AddHours(-30));
            P("libro NQ (LibroNqFamilia.AgregarLinea): " + buenas + " fotos de " + lineas + " lineas, " + H(nqTodo.Fotos(DateTime.MinValue, DateTime.MaxValue)[0].TsUtc) + " a " + H(nqU.TsUtc)
              + " (" + sw.ElapsedMilliseconds + " ms); la simulacion usa las ultimas 30 h (RetencionHoras de produccion)");
            sw.Restart();
            var cbTodo = AbrirCboe(Path.Combine(datos, "cboe"), logs, "todo", 0, 9, tFin);
            var cbProd = AbrirCboe(Path.Combine(datos, "cboe"), logs, "prod", 30, 7, tFin);
            foreach (var lb in new[] { "NDX", "QQQ", "TQQQ" })
            {
                var a = cbTodo.Fotos(lb, DateTime.MinValue, DateTime.MaxValue); var b = cbProd.Fotos(lb, DateTime.MinValue, DateTime.MaxValue);
                P("CBOE " + lb + ": " + a.Count + " fotos (" + a.Count(x => x.Filas.Length > 0) + " con filas, historia completa) | produccion a las " + H(tFin) + ": "
                  + b.Count + " fotos, " + b.Count(x => x.Filas.Length > 0) + " con filas (corte " + H(cbProd.Corte(tFin)) + ")");
            }
            P("fuentes CBOE cargadas en " + sw.ElapsedMilliseconds + " ms");
            P();

            try
            {
                PruebaSaltos(nqTodo, cbTodo);
                PruebaLibroFlaco(cbTodo);
                var sim = Simulacion(minutos, nq30, cbProd, logs, out var ejemplos);
                PruebaMontoYLinealidad(sim);
                PruebaLadosQueSeAnulan();
                PruebaCongelada(sim);
                PruebaHoraDelDato(sim);
                PruebaReinicio(nqTodo, cbTodo);
                PruebaSinR1(nqTodo);
                PruebaCasoAMano(sim, nq30, cbProd);
                PruebaFam(sim);
                PruebaMismaNoche(nqTodo, cbTodo);
                PruebaRueda(cbTodo, logs);
                PruebaApertura(cbTodo, logs);
                PruebaTqqq(cbTodo, logs);
                PruebaTiempo(sim, minutos, nq30, cbProd, logs);
                P();
                P("================ SIMULACION \"esta noche\" (niv-2026-10-09-MNQZ6, fuentes como en produccion) ================");
                foreach (var e in ejemplos) P(e);
            }
            catch (Exception e) { Ok("excepcion del arnes", false, e.ToString()); }
            finally { try { cbTodo.Parar(); cbProd.Parar(); } catch { } }

            P();
            P("=== " + (_pruebas - _fallas) + " de " + _pruebas + " pruebas en verde (" + _fallas + " fallas) en " + swTodo.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s");
            try
            {
                Directory.CreateDirectory(Path.Combine(dirArnes, "resultados"));
                File.WriteAllText(Path.Combine(dirArnes, "resultados", "posiciones.txt"), Sal.ToString(), new UTF8Encoding(false));
            }
            catch { }
            return _fallas == 0 ? 0 : 1;
        }

        static void Copiar(string de, string a)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(a));
            using var src = new FileStream(de, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var dst = new FileStream(a, FileMode.Create, FileAccess.Write);
            src.CopyTo(dst);
        }

        static BajadorCboe AbrirCboe(string carpeta, string logs, string nombre, double horasConFilas, int dias, DateTime ahora)
        {
            var b = new BajadorCboe(new OpcionesCboe
            {
                Carpeta = carpeta, Log = Path.Combine(logs, "cboe-" + nombre + ".log"), Bajar = false, HorasConFilas = horasConFilas, DiasHistoria = dias,
                NombreMutex = "", AhoraUtc = () => ahora, RetrasoInicialS = 0, SeguirCadaS = 3600,
            });
            b.Arrancar();
            var sw = Stopwatch.StartNew();
            while (!b.HistoriaCargada && sw.Elapsed.TotalSeconds < 600) Thread.Sleep(50);
            if (!b.HistoriaCargada) throw new Exception("CBOE " + nombre + ": la historia no cargo en 10 min");
            return b;
        }

        // ================================================================== P1 saltos de OI
        static void PruebaSaltos(LibroNqFamilia nq, BajadorCboe cb)
        {
            P("---- P1 saltos de OI (historia completa, sin presupuesto) ----");
            var ventanas = new Dictionary<string, (double Ini, double Fin)>
            {   // hora UTC del dia en que se aceptan (medido por el lector: NQ 01:24-01:52, NDX 06:26-06:36, QQQ 07:31-09:12; TQQQ sin medir)
                ["NQ"] = (0.5, 3.0), ["NDX"] = (6.0, 7.0), ["QQQ"] = (7.0, 9.5), ["TQQQ"] = (5.0, 13.5),
            };
            var esperados = new Dictionary<string, string[]>
            {
                ["NQ"] = new[] { "2026-10-07 01:24", "2026-10-08 01:30", "2026-10-09 01:48" },
            };
            foreach (var lb in new[] { "NQ", "NDX", "QQQ", "TQQQ" })
            {
                var fotos = lb == "NQ" ? nq.Fotos(DateTime.MinValue, DateTime.MaxValue) : cb.Fotos(lb, DateTime.MinValue, DateTime.MaxValue);
                var det = new RegimenesOi(lb);
                double maxFuera = 0; DateTime tMaxFuera = default; int pares = 0;
                int paresFlacos = 0, flacosConCambio = 0; double maxFracFlaco = 0;      // pares SIN salto con menos de 20 claves comunes
                Dictionary<ClaveFila, double> prev = null; FotoCadena prevF = null;
                foreach (var f in fotos)
                {
                    int n0 = det.Saltos.Count;
                    det.Procesar(f, lb == "NQ" ? f.TsUtc : f.GeneradoUtc);
                    bool salto = det.Saltos.Count > n0;
                    if (f.Filas.Length == 0) continue;
                    if (prevF != null && ReferenceEquals(prevF.Filas, f.Filas)) { prevF = f; continue; }
                    var cur = ClavesCambios.Mapa(f, true);
                    if (prev != null && !salto)
                    {
                        int com = 0, dist = 0;
                        foreach (var kv in cur) { if (!(kv.Value > 0) || !prev.TryGetValue(kv.Key, out var pv) || !(pv > 0)) continue; com++; if (pv != kv.Value) dist++; }
                        pares++;
                        if (com > 0 && (double)dist / com > maxFuera) { maxFuera = (double)dist / com; tMaxFuera = f.TsUtc; }
                        if (com < 20) { paresFlacos++; if (dist > 0) flacosConCambio++; if (com > 0) maxFracFlaco = Math.Max(maxFracFlaco, (double)dist / com); }
                    }
                    prev = cur; prevF = f;
                }
                var conf = det.Saltos.Where(x => !x.Dudoso).ToList();
                var dud = det.Saltos.Where(x => x.Dudoso).ToList();
                int fuera = 0; var porDia = new Dictionary<DateTime, int>();
                foreach (var x in det.Saltos)
                {
                    double h = x.TsUtc.TimeOfDay.TotalHours;
                    bool enVentana = h >= ventanas[lb].Ini && h <= ventanas[lb].Fin;
                    if (!x.Dudoso)
                    {
                        if (!enVentana) fuera++;
                        porDia[x.TsUtc.Date] = porDia.TryGetValue(x.TsUtc.Date, out var c) ? c + 1 : 1;
                    }
                    P("   " + lb + (x.Dudoso ? " CAMBIO SIN CONFIRMAR " : " salto ") + H(x.TsUtc) + " (foto anterior " + H(x.AntesUtc) + "): " + x.Distintas + " de " + x.Comunes
                      + " claves cambiaron (" + F(100 * x.Fraccion, "0.0") + " %)" + (x.Flaco ? " [regla de libro flaco]" : "") + " — " + TiempoFam.ANy(x.TsUtc).ToString("HH:mm", Inv) + " NY"
                      + (enVentana || x.Dudoso ? "" : "  <-- FUERA DE LA VENTANA ESPERADA"));
                }
                bool unoPorDia = porDia.Values.All(c => c == 1);
                string faltan = "";
                if (esperados.TryGetValue(lb, out var esp))
                    foreach (var e in esp)
                    {
                        var te = DateTime.SpecifyKind(DateTime.ParseExact(e, "yyyy-MM-dd HH:mm", Inv), DateTimeKind.Utc);
                        if (!conf.Any(x => Math.Abs((x.TsUtc - te).TotalMinutes) <= 10)) faltan += " " + e;
                    }
                // CBOE/TQQQ: la OCC publica cada dia habil antes de la apertura -> UN salto confirmado por dia habil de NY cubierto por la historia,
                // y antes de las 09:30 NY (si no, la salvaguarda PublicacionCboeEsperada daria NaN en la rueda con datos buenos)
                string faltanHab = ""; int diasHab = 0; double horaMaxNy = 0;
                if (lb != "NQ")
                {
                    foreach (var x in conf) horaMaxNy = Math.Max(horaMaxNy, TiempoFam.ANy(x.TsUtc).TimeOfDay.TotalHours);
                    var cf = fotos.Where(f => f.Filas.Length > 0).ToList();
                    var d0 = TiempoFam.ANy(cf[0].TsUtc).Date; var ult = TiempoFam.ANy(cf[cf.Count - 1].TsUtc);
                    for (var d = d0.AddDays(1); d <= ult.Date; d = d.AddDays(1))
                    {
                        if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || TiempoFam.EsFeriadoBolsa(d)) continue;
                        if (ult < d.AddHours(9.5)) continue;          // la historia no llega a la hora limite de ese dia
                        diasHab++;
                        int k = conf.Count(x => TiempoFam.ANy(x.TsUtc).Date == d);
                        if (k != 1) faltanHab += " " + d.ToString("MM-dd", Inv) + "(" + k + ")";
                    }
                }
                int minCom = conf.Count > 0 ? conf.Min(x => x.Comunes) : 0;
                Ok("P1 " + lb + ": saltos en la ventana medida y uno por dia, cero falsos, cero sin confirmar" + (lb != "NQ" ? ", uno por dia habil de NY antes de las 09:30 NY" : ""),
                   fuera == 0 && unoPorDia && faltan == "" && conf.Count > 0 && dud.Count == 0 && faltanHab == "" && (lb == "NQ" || (diasHab > 0 && horaMaxNy < 9.5)),
                   conf.Count + " saltos (el de menos claves: " + minCom + " comunes); sin confirmar " + dud.Count + "; fuera de ventana " + fuera + "; dias con mas de uno " + porDia.Count(kv => kv.Value > 1)
                   + (faltan != "" ? "; FALTAN" + faltan : "") + (lb != "NQ" ? "; dias habiles cubiertos " + diasHab + (faltanHab != "" ? " SIN SU SALTO:" + faltanHab : " todos con su salto") + "; el mas tardio a las " + F(horaMaxNy, "0.00") + " h NY" : "")
                   + "; separacion: fuera de los saltos la fraccion maxima de claves que cambian es " + F(100 * maxFuera, "0.00") + " % (" + H(tMaxFuera) + ", " + pares + " pares)"
                   + "; pares sin salto con menos de 20 claves comunes: " + paresFlacos + " (con algun cambio " + flacosConCambio + ", fraccion maxima " + F(100 * maxFracFlaco, "0.0") + " %)");
            }
        }

        // ================================================================== P1b libro flaco (TQQQ adelgazado a mano)
        /// <summary>La foto con solo las filas de los strikes elegidos (K en milesimas). Mismo arreglo de filas para el mismo arreglo de origen
        /// (la noche de CBOE repite las filas por referencia y el detector lo usa).</summary>
        static FotoCadena Flaca(FotoCadena f, HashSet<long> ks, Dictionary<FilaCadena[], FilaCadena[]> cache)
        {
            if (!cache.TryGetValue(f.Filas, out var nf)) { nf = f.Filas.Where(x => ks.Contains((long)Math.Round(x.K * 1000.0))).ToArray(); cache[f.Filas] = nf; }
            return new FotoCadena { Libro = f.Libro, GeneradoUtc = f.GeneradoUtc, TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot, Dias = f.Dias, Filas = nf,
                                    EsFuturo = f.EsFuturo, OiTotal = f.OiTotal, CierreAnterior = f.CierreAnterior };
        }

        static void PruebaLibroFlaco(BajadorCboe cb)
        {
            P();
            P("---- P1b libro flaco: TQQQ real adelgazado a mano desde el 10-07 12:00Z (antes, completo: el salto del 10-07 con 40 claves se ve con las dos reglas) ----");
            var todas = cb.Fotos("TQQQ", Utc("2026-10-06 12:00:00"), Utc("2026-10-08 15:00:01")).Where(f => f.Filas.Length > 0).ToList();
            var det0 = new RegimenesOi("TQQQ");
            foreach (var f in todas) det0.Procesar(f, f.GeneradoUtc);
            var s8 = det0.Saltos.LastOrDefault(x => !x.Dudoso && x.TsUtc.Date == new DateTime(2026, 10, 8));
            var s7 = det0.Saltos.LastOrDefault(x => !x.Dudoso && x.TsUtc.Date == new DateTime(2026, 10, 7));
            if (s8 == null || s7 == null) { Ok("P1b TQQQ libro flaco", false, "sin los saltos reales de TQQQ del 10-07 / 10-08"); return; }
            var fA = todas.Last(f => f.TsUtc == s8.AntesUtc); var fD = todas.First(f => f.TsUtc == s8.TsUtc);
            var mA = ClavesCambios.Mapa(fA, true); var mD = ClavesCambios.Mapa(fD, true);
            var porK = new Dictionary<long, int>();
            foreach (var kv in mD) if (kv.Value > 0 && mA.TryGetValue(kv.Key, out var a) && a > 0) porK[kv.Key.K1000] = (porK.TryGetValue(kv.Key.K1000, out var c) ? c : 0) + 1;
            var orden = porK.Keys.OrderBy(k => Math.Abs(k / 1000.0 - fD.Spot)).ThenBy(k => k).ToList();
            HashSet<long> Elegir(int minimo) { var h = new HashSet<long>(); int n = 0; foreach (var k in orden) { if (n >= minimo) break; h.Add(k); n += porK[k]; } return h; }
            var corte = Utc("2026-10-07 12:00:00"); var t15 = Utc("2026-10-08 15:00:00");
            P("   salto real del 10-08: " + H(s8.TsUtc) + " contra " + H(s8.AntesUtc) + ", " + s8.Distintas + " de " + s8.Comunes + " claves; el del 10-07: " + H(s7.TsUtc) + ", " + s7.Comunes + " claves");
            foreach (var (nombre, minimo) in new[] { ("flaco", 10), ("muy flaco", 3) })
            {
                var ks = Elegir(minimo);
                var cache = new Dictionary<FilaCadena[], FilaCadena[]>(ReferenceEqualityComparer.Instance);
                var seq = todas.Select(f => f.TsUtc < corte ? f : Flaca(f, ks, cache)).ToList();
                var det = new RegimenesOi("TQQQ");
                var detViejo = new RegimenesOi("TQQQ", 20, 0.20, int.MaxValue, 2.0, 2.0);     // la regla de antes: solo |C| >= 20 y >= 20 %
                foreach (var f in seq) { det.Procesar(f, f.GeneradoUtc); detViejo.Procesar(f, f.GeneradoUtc); }
                var ahora = seq.Last(f => f.TsUtc <= t15);
                var r = new CambiosLibro { Libro = "TQQQ", EsTqqq = true, Ahora = ahora, AhoraOriginal = ahora, TUtc = t15, S = ahora.Spot, Escala = 1 };
                r.Indice = ClavesCambios.Indexar(ahora, null); r.Base = CambiosFamilia.Valuar(r, ahora);
                var v = new CambioVentana { Minutos = 0 }; CambiosFamilia.CompararOi(r, v, det);
                var x8 = det.Saltos.LastOrDefault(x => x.TsUtc == s8.TsUtc);
                int com = x8?.Comunes ?? -1;
                string strikes = string.Join(",", ks.OrderBy(k => k).Select(k => F(k / 1000.0)));
                if (nombre == "flaco")
                {
                    // R0/R1 = las dos publicaciones de verdad: OI de la foto de ahora y el de la foto anterior al salto, clave por clave
                    var mAhora = ClavesCambios.Mapa(ahora, true); var mAntes = ClavesCambios.Mapa(Flaca(fA, ks, cache), true);
                    int malR = 0, nR = 0;
                    foreach (var kv in mAhora) { if (!v.Valida || !mAntes.TryGetValue(kv.Key, out var a0)) continue; nR++; if (v.R0.Oi[kv.Key] != kv.Value || v.R1.Oi[kv.Key] != a0) malR++; }
                    Ok("P1b TQQQ " + nombre + " (strikes " + strikes + "; " + com + " claves comunes en el salto del 10-08): salto CONFIRMADO por la regla de libro flaco y ΔOI = publicacion del 10-08 menos la del 10-07",
                       x8 != null && x8.Flaco && !x8.Dudoso && com >= 8 && com < 20 && v.Valida && v.R0.InicioUtc == s8.TsUtc && v.R1.InicioUtc == s7.TsUtc && nR > 0 && malR == 0,
                       "salto " + (x8 == null ? "NO VISTO" : H(x8.TsUtc) + " flaco=" + x8.Flaco) + "; ΔOI valida=" + v.Valida + (v.Valida ? " (" + H(v.HastaUtc) + " vs " + H(v.DesdeUtc) + "), " + nR + " claves con R0/R1 = OI de las dos publicaciones, " + malR + " distintas" : ", " + v.Nota));
                    // la regla de antes pierde el salto del 10-08: R0 sigue con el OI nuevo encima y R1 es la de hace dos noches
                    var vViejo = new CambioVentana { Minutos = 0 }; CambiosFamilia.CompararOi(r, vViejo, detViejo);
                    var vSinGuarda = new CambioVentana { Minutos = 0 }; CambiosFamilia.CompararOi(r, vSinGuarda, detViejo, TimeSpan.FromHours(24));
                    Ok("P1b regla de antes (|C| >= 20): pierde el salto del 10-08; la salvaguarda de publicacion lo deja en NaN (sin ella daba ΔOI de DOS publicaciones con las fechas del salto anterior)",
                       !detViejo.Saltos.Any(x => x.TsUtc == s8.TsUtc) && !vViejo.Valida && vViejo.Nota.StartsWith("sin la publicacion de OI del 10-08") && vSinGuarda.Valida && vSinGuarda.HastaUtc == s7.TsUtc,
                       "con salvaguarda: " + vViejo.Nota + " | sin salvaguarda (el codigo de antes): " + (vSinGuarda.Valida ? "ΔOI publicado como " + H(vSinGuarda.HastaUtc) + " vs " + H(vSinGuarda.DesdeUtc)
                       + (vSinGuarda.R1.InicioEsSalto ? "" : " o antes") + " con el OI del 10-08 adentro de R0" : "NaN: " + vSinGuarda.Nota));
                }
                else
                    Ok("P1b TQQQ " + nombre + " (strikes " + strikes + "; " + com + " claves comunes): cambio SIN CONFIRMAR -> ΔOI NaN con nota (ni salto inventado ni R0 pisado en silencio)",
                       x8 != null && x8.Dudoso && com >= 1 && com < 8 && !v.Valida && v.Nota.Contains("pocas para confirmar"),
                       "evento " + (x8 == null ? "NO VISTO" : H(x8.TsUtc) + " dudoso=" + x8.Dudoso) + "; valida=" + v.Valida + ", " + v.Nota);
            }
            // la salvaguarda con los datos reales: 10-08 15:00Z con el detector completo -> valida; la hora limite no se dispara con publicaciones a tiempo
            var rr = new CambiosLibro { Libro = "TQQQ", EsTqqq = true, Ahora = todas.Last(f => f.TsUtc <= t15), TUtc = t15, Escala = 1 };
            rr.AhoraOriginal = rr.Ahora; rr.S = rr.Ahora.Spot; rr.Indice = ClavesCambios.Indexar(rr.Ahora, null); rr.Base = CambiosFamilia.Valuar(rr, rr.Ahora);
            var vr = new CambioVentana { Minutos = 0 }; CambiosFamilia.CompararOi(rr, vr, det0);
            Ok("P1b TQQQ real (sin adelgazar) 10-08 15:00Z: ΔOI valido " + H(vr.HastaUtc) + " vs " + H(vr.DesdeUtc), vr.Valida && vr.HastaUtc == s8.TsUtc && vr.DesdeUtc == s7.TsUtc,
               "valida=" + vr.Valida + (string.IsNullOrEmpty(vr.Nota) ? "" : ", " + vr.Nota) + "; publicacion esperada a esa hora: " + CambiosFamilia.PublicacionCboeEsperada(t15, new TimeSpan(9, 30, 0)).ToString("MM-dd", Inv)
               + "; a las 13:29Z (09:29 NY) del 10-08: " + CambiosFamilia.PublicacionCboeEsperada(Utc("2026-10-08 13:29:00"), new TimeSpan(9, 30, 0)).ToString("MM-dd", Inv)
               + "; el lunes 10-05 a las 12:00Z: " + CambiosFamilia.PublicacionCboeEsperada(Utc("2026-10-05 12:00:00"), new TimeSpan(9, 30, 0)).ToString("MM-dd ddd", Inv));
        }

        // ================================================================== SIMULACION "esta noche"
        sealed class Paso
        {
            public RegistroMinuto U;
            public FotoFamilia Motor, Anotada;
            public List<NivelActual> Originales;
            public double Ms, MsLog; public bool Recalculo; public int Gc2;
            public Dictionary<string, CambiosLibro> Res = new Dictionary<string, CambiosLibro>();
            public bool Pendiente;
        }

        static List<Paso> Simulacion(List<RegistroMinuto> minutos, ILibroNq nq, IFuenteCboe cb, string logs, out List<string> ejemplos)
        {
            P();
            P("---- SIMULACION: Anotar en cada minuto con libros (presupuesto de historia 25 ms como en produccion) ----");
            var cam = new CambiosFamilia(nq, cb, new OpcionesCambios { RutaLog = Path.Combine(logs, "pythiagex4-cambios.log"), Corregida = true });
            var pasos = new List<Paso>();
            int primeroOi = -1;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();      // la basura de P1/P1b no es de produccion: que no caiga en el tiempo medido
            foreach (var u in minutos)
            {
                var act = SalidaFamilia.Actuales(u, null, null);
                var foto = new FotoFamilia { Sesion = "2026-10-09", CalculadoUtc = TiempoFam.DeClave(u.Clave).AddSeconds(6), Actuales = act.AsReadOnly(), Fuentes = Array.Empty<FuenteEstado>() };
                int rc0 = cam.Recalculos, g2 = GC.CollectionCount(2);
                var sw = Stopwatch.StartNew();
                var an = cam.Anotar(foto, u, null);
                double ms = sw.Elapsed.TotalMilliseconds;
                var p = new Paso { U = u, Motor = foto, Anotada = an, Originales = act, Ms = ms, Recalculo = cam.Recalculos > rc0, Pendiente = cam.HistoriaPendiente, Gc2 = GC.CollectionCount(2) - g2 };
                p.MsLog = p.Recalculo ? cam.MsUltimoLog : 0;
                foreach (var lb in new[] { "NQ", "NDX", "QQQ" }) { var r = cam.Resultado(lb); if (r != null) p.Res[lb] = r; }
                if (primeroOi < 0 && !cam.HistoriaPendiente) primeroOi = pasos.Count;
                pasos.Add(p);
            }
            P("   " + pasos.Count + " minutos anotados; la historia de OI quedo leida en el minuto " + (primeroOi + 1) + " (" + H(TiempoFam.DeClave(pasos[Math.Max(0, primeroOi)].U.Clave)) + ")"
              + "; log en " + Path.Combine(logs, "pythiagex4-cambios.log"));
            // la foto del motor no se toca
            int tocados = pasos.Sum(p => p.Originales.Count(n => n.CambioVolM != null || n.CambioCobertura != null || !string.IsNullOrEmpty(n.CambioNota)));
            int distintos = pasos.Sum(p => p.Anotada.Actuales.Where((n, i) => !ReferenceEquals(n, p.Originales[i])).Count());
            Ok("Anotar no modifica los niveles del motor (publica copias)", tocados == 0 && distintos == pasos.Sum(p => p.Originales.Count),
               "niveles del motor con campos de cambio: " + tocados + "; copias: " + distintos + " de " + pasos.Sum(p => p.Originales.Count));

            ejemplos = new List<string>();
            var horas = new[] { "22:02", "23:30", "01:07", "01:30", "01:47", "01:49", "02:15" };
            var elegidos = new List<Paso>();
            foreach (var h in horas)
            {
                var p = pasos.FirstOrDefault(x => TiempoFam.DeClave(x.U.Clave).ToString("HH:mm", Inv) == h);
                if (p != null) elegidos.Add(p);
            }
            elegidos.Add(pasos[pasos.Count - 1]);
            foreach (var p in elegidos.Distinct()) ejemplos.AddRange(Ejemplo(p));
            return pasos;
        }

        static IEnumerable<string> Ejemplo(Paso p)
        {
            var l = new List<string>();
            l.Add("");
            l.Add("-- minuto " + H(TiempoFam.DeClave(p.U.Clave)) + " (libros " + string.Join(",", p.U.Libros) + "; Anotar " + F(p.Ms, "0.0") + " ms" + (p.Pendiente ? "; historia de OI pendiente" : "") + ")");
            foreach (var e in p.Anotada.CambiosEstado) l.Add("   estado: " + e);
            l.Add("   " + "serie".PadRight(15) + "rol".PadRight(8) + "strike".PadLeft(10) + "GexM".PadLeft(9) + "  Δ5m".PadLeft(10) + "  Δ15m".PadLeft(10) + "  Δ30m".PadLeft(10) + "  cob15".PadLeft(7)
                  + "  ΔOI día".PadLeft(10) + "  cobOI".PadLeft(7) + "  OI hasta/desde · nota");
            foreach (var n in p.Anotada.Actuales)
            {
                if (n.Tipo != "MUROS" && n.Tipo != "MAJORS") continue;
                string oi = n.Fuente == "oi" ? H(n.CambioOiHastaUtc) + " vs " + H(n.CambioOiDesdeUtc) : "";
                l.Add("   " + n.Serie.PadRight(15) + n.Rol.PadRight(8) + F(n.Strike, "0.##").PadLeft(10) + F(n.GexM, "0.0").PadLeft(9)
                      + F(n.CambioVolM[0], "0.000").PadLeft(10) + F(n.CambioVolM[1], "0.000").PadLeft(10) + F(n.CambioVolM[2], "0.000").PadLeft(10)
                      + F(n.CambioCobertura[1], "0.00").PadLeft(7) + F(n.CambioOiDiaM, "0.000").PadLeft(10) + F(n.CambioCobertura[3], "0.00").PadLeft(7)
                      + "  " + oi + (string.IsNullOrEmpty(n.CambioNota) ? "" : " · " + n.CambioNota));
            }
            // ventanas reales (hora del DATO; entre parentesis los sellos)
            foreach (var lb in CambiosFamilia.Libros)
                if (p.Res.TryGetValue(lb, out var r) && r.Ahora != null)
                    for (int w = 0; w < 3; w++)
                    {
                        var v = r.Vol[w];
                        l.Add("   " + lb + " ventana " + v.Minutos + " min: " + (v.Valida ? "dato " + H(v.DesdeUtc) + " -> " + H(v.HastaUtc) + " (" + F(v.SegReal, "0") + " s; sellos " + H(v.Antes.TsUtc) + " -> " + H(r.Ahora.TsUtc)
                              + ")" + (v.Congelada ? " congelada" : "") + ", " + v.Comparadas + " de " + v.Presentes + " claves comparadas, " + v.Distintas + " con volumen nuevo, " + v.Negativas + " bajaron" : "NaN: " + v.Nota));
                    }
            return l;
        }

        // ================================================================== P2 + P3 (noche): monto y linealidad
        static void PruebaMontoYLinealidad(List<Paso> pasos)
        {
            P();
            P("---- P2 monto reconstruido / P3 linealidad (noche) ----");
            int niveles = 0, malos = 0; string primerMalo = "";
            int casos = 0, casosOi = 0, violan = 0; double maxRel = 0; string peor = "";
            foreach (var p in pasos)
            {
                foreach (var n in p.Anotada.Actuales)
                {
                    if ((n.Tipo != "MUROS" && n.Tipo != "MAJORS") || n.Libro == "familia") continue;
                    if (!p.Res.TryGetValue(n.Libro, out var r) || r.Base == null) continue;
                    char lado = n.Rol == "muro C" ? 'C' : n.Rol == "muro P" ? 'P' : 'N';
                    int ib = ClavesCambios.Buscar(r.Base.K, n.Strike, 0.006);
                    double m = ib < 0 ? double.NaN : NumPy.RoundPy(r.Base.Valor(r.Base.K[ib], lado, n.Fuente == "oi") * r.Escala / 1e6, 1);
                    niveles++;
                    if (!(m == n.GexM)) { malos++; if (primerMalo == "") primerMalo = H(TiempoFam.DeClave(p.U.Clave)) + " " + n.Serie + " " + n.Rol + " K " + F(n.Strike) + ": " + F(m) + " vs GexM " + F(n.GexM); }
                }
                if (!p.Recalculo) continue;
                foreach (var r in p.Res.Values)
                {
                    if (r.Ahora == null) continue;
                    foreach (var v in r.Vol.Concat(new[] { r.Oi }))
                    {
                        if (v == null || !v.Valida) continue;
                        bool oi = v.Minutos == 0;
                        // 4.1.2 (principal, 09-10): en CBOE/TQQQ con la MISMA hora del dato el cambio es 0 por diseño (CBOE alterna versiones de la
                        // misma cadena: no es volumen operado); ahi la identidad no aplica y lo prueba P4 (0 exacto).
                        if (!oi && r.Libro != "NQ" && v.Antes != null && v.Antes.DatoUtc == r.Ahora.DatoUtc) continue;
                        var (rel, ok) = Linealidad(r, v, oi);
                        if (oi) casosOi++; else casos++;
                        if (!ok) violan++;
                        if (rel > maxRel) { maxRel = rel; peor = r.Libro + " " + (oi ? "OI" : v.Minutos + " min") + " " + H(TiempoFam.DeClave(p.U.Clave)); }
                    }
                }
            }
            Ok("P2 GexM de cada nivel de libro == perfil de ahora de CambiosFamilia (round 1)", malos == 0 && niveles > 0, niveles + " niveles, " + malos + " distintos" + (primerMalo != "" ? " (primero: " + primerMalo + ")" : ""));
            Ok("P3 linealidad noche: Δ(fotoΔ) == Perfil(ahora) - Perfil(ahora con la columna de antes), 1e-9 rel", violan == 0 && casos + casosOi > 0,
               casos + " ventanas de volumen y " + casosOi + " de OI; error relativo maximo " + maxRel.ToString("0.###E+0", Inv) + (peor != "" ? " (" + peor + ")" : ""));
        }

        /// <summary>Perfil(ahora) - Perfil(ahora con la columna de antes en las claves comparadas) contra v.Delta, por K. (error relativo maximo, ok)</summary>
        static (double, bool) Linealidad(CambiosLibro r, CambioVentana v, bool oi)
        {
            var F = r.Ahora.Filas; var ix = r.Indice; int n = F.Length;
            var oiC = new double[n]; var oiP = new double[n]; var vC = new double[n]; var vP = new double[n];
            Dictionary<ClaveFila, double> antes = oi ? v.R1.Oi : (ReferenceEquals(v.Antes.Filas, F) ? null : ClavesCambios.Mapa(v.Antes, false));
            for (int i = 0; i < n; i++)
            {
                oiC[i] = F[i].OiC; oiP[i] = F[i].OiP; vC[i] = F[i].VolC; vP[i] = F[i].VolP;
                double a;
                if (ix.HayC[i])
                {
                    double ah = oi ? F[i].OiC : F[i].VolC;
                    if (antes == null) a = ah; else if (!antes.TryGetValue(ix.C[i], out a)) a = ah;
                    if (!oi && a > ah) a = ah;                         // clave con volumen menor: fuera (como CompararVolumen)
                    if (oi) oiC[i] = a; else vC[i] = a;
                }
                if (ix.HayP[i])
                {
                    double ah = oi ? F[i].OiP : F[i].VolP;
                    if (antes == null) a = ah; else if (!antes.TryGetValue(ix.P[i], out a)) a = ah;
                    if (!oi && a > ah) a = ah;
                    if (oi) oiP[i] = a; else vP[i] = a;
                }
            }
            var pB = CambiosFamilia.Valuar(r, ClavesCambios.ConColumnas(r.Ahora, oiC, oiP, vC, vP));
            var pA = r.Base;
            double maxRel = 0; bool ok = true;
            var ks = new SortedSet<double>(pA.K); foreach (var k in pB.K) ks.Add(k); foreach (var k in v.Delta.K) ks.Add(k);
            foreach (var k in ks)
                foreach (var lado in new[] { 'C', 'P' })
                {
                    double a = pA.Valor(k, lado, oi), b = pB.Valor(k, lado, oi), d = v.Delta.Valor(k, lado, oi);
                    double esc = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), 1e-300);
                    double err = Math.Abs((a - b) - d);
                    double rel = err / esc;
                    if (err > 1e-9 * esc && err > 1e-6) ok = false;      // 1e-6 = 1e-12 M USD: ruido de cero
                    if (esc > 1e-3 && rel > maxRel) maxRel = rel;
                }
            return (maxRel, ok);
        }

        // ================================================================== P4 congelada
        /// <summary>P3c (4.1.4): la fotoΔ con un cambio IGUAL de calls y puts y la misma IV (el caso real de NQ OI 10-09 05:59:55Z, K 31.160: +9 y +9,
        /// IV 0,206258) tiene que dar el cambio de cada lado; con el neto de las series (porLado = false) se anulaba y el strike no aportaba.</summary>
        static void PruebaLadosQueSeAnulan()
        {
            P();
            P("---- P3c fotoΔ con calls y puts que se anulan en el neto (4.1.4) ----");
            var t = new DateTime(2026, 10, 9, 6, 0, 0, DateTimeKind.Utc);
            var f = new FotoCadena { Libro = "NQ", GeneradoUtc = t.AddSeconds(-5), TsUtc = t.AddSeconds(-5), DatoUtc = t.AddSeconds(-5), Spot = 31146.5, Dias = new[] { 0.5834, 3.5834 }, EsFuturo = true,
                                     Filas = new[] { new FilaCadena(31160, 0, 9, 9, 0.206258, 0.206258, 0, 0), new FilaCadena(31150, 0, 3, 1, 0.21, 0.20, 0, 0) } };
            var r = new CambiosLibro { Libro = "NQ", TUtc = t, S = 31146.5, Escala = 0.2, Ahora = f, AhoraOriginal = f };
            var neto = CambiosFamilia.Valuar(r, f); var lado = CambiosFamilia.Valuar(r, f, porLado: true);
            double c = lado.Valor(31160, 'C', true), pp = lado.Valor(31160, 'P', true);
            Ok("P3c fotoΔ +9 calls / +9 puts con la misma IV en K 31.160: el cambio de cada lado (C " + F(c / 1e6, "0.00") + " M, P " + F(pp / 1e6, "0.00") + " M; con el neto: K "
               + (ClavesCambios.Buscar(neto.K, 31160, 1e-9) < 0 ? "fuera" : "adentro") + ")",
               c > 0 && pp < 0 && Math.Abs(c + pp) < 1e-6 * Math.Abs(c) && ClavesCambios.Buscar(neto.K, 31160, 1e-9) < 0 && lado.Valor(31150, 'C', true) == neto.Valor(31150, 'C', true), "");
        }

        static void PruebaCongelada(List<Paso> pasos)
        {
            P();
            P("---- P4 cadena congelada (CBOE de noche) => 0 exacto ----");
            int n = 0, ceros = 0, conNota = 0; string malo = "";
            foreach (var p in pasos)
                foreach (var a in p.Anotada.Actuales)
                {
                    if ((a.Libro != "NDX" && a.Libro != "QQQ") || a.Fuente != "vol" || (a.Tipo != "MUROS" && a.Tipo != "MAJORS")) continue;
                    for (int w = 0; w < 3; w++)
                    {
                        n++;
                        double v = a.CambioVolM[w];
                        if (v == 0.0 && BitConverter.DoubleToInt64Bits(v) == 0) ceros++;
                        else if (malo == "") malo = H(TiempoFam.DeClave(p.U.Clave)) + " " + a.Serie + " " + a.Rol + " " + (w * 10 + 5) + " min: " + F(v, "R") + " (" + a.CambioNota + ")";
                    }
                    if (a.CambioNota == "cadena congelada") conNota++;
                }
            Ok("P4 NDX/QQQ por volumen de noche: CambioVolM == 0,0 exacto (+0) con nota \"cadena congelada\"", n > 0 && ceros == n,
               ceros + " de " + n + " valores en 0 exacto; " + conNota + " niveles con la nota" + (malo != "" ? "; primero distinto: " + malo : ""));
        }

        // ================================================================== P10 ventana de volumen = hora del DATO
        static void PruebaHoraDelDato(List<Paso> pasos)
        {
            P();
            P("---- P10 ventana del volumen = HORA DEL DATO de las dos fotos (no el sello de CBOE) ----");
            int n = 0, malos = 0, cboe = 0, invalidasConHora = 0; string malo = "", ej = ""; double offMin = double.MaxValue, offMax = double.MinValue;
            foreach (var p in pasos)
                foreach (var a in p.Anotada.Actuales)
                {
                    if (a.Libro == "familia" || a.Libro == "TQQQ" || a.Fuente != "vol" || (a.Tipo != "MUROS" && a.Tipo != "MAJORS")) continue;
                    if (!p.Res.TryGetValue(a.Libro, out var r) || r.Ahora == null) continue;
                    for (int w = 0; w < 3; w++)
                    {
                        var v = r.Vol[w];
                        if (v == null || !v.Valida) { if (a.CambioVolHastaUtc[w] != DateTime.MinValue || a.CambioVolDesdeUtc[w] != DateTime.MinValue) invalidasConHora++; continue; }
                        n++;
                        bool ok = a.CambioVolHastaUtc[w] == r.Ahora.DatoUtc && a.CambioVolDesdeUtc[w] == v.Antes.DatoUtc
                                  && a.CambioVolSegReal[w] == (r.Ahora.DatoUtc - v.Antes.DatoUtc).TotalSeconds;
                        if (a.Libro == "NQ") ok &= r.Ahora.DatoUtc == r.Ahora.TsUtc;            // NQ: dato = sello
                        else
                        {
                            cboe++;
                            double off = (r.Ahora.TsUtc - r.Ahora.DatoUtc).TotalSeconds;
                            offMin = Math.Min(offMin, off); offMax = Math.Max(offMax, off);
                            if (ej == "" && w == 1) ej = H(TiempoFam.DeClave(p.U.Clave)) + " " + a.Serie + " " + a.Rol + " 15 min: ventana " + H(a.CambioVolDesdeUtc[w]) + " -> " + H(a.CambioVolHastaUtc[w])
                                                      + " (sellos " + H(v.Antes.TsUtc) + " -> " + H(r.Ahora.TsUtc) + "), " + F(a.CambioVolSegReal[w], "0") + " s, " + a.CambioNota;
                        }
                        if (!ok) { malos++; if (malo == "") malo = H(TiempoFam.DeClave(p.U.Clave)) + " " + a.Serie + " " + a.Rol + " w" + w + ": " + H(a.CambioVolDesdeUtc[w]) + " -> " + H(a.CambioVolHastaUtc[w]); }
                    }
                }
            Ok("P10 CambioVolDesdeUtc/HastaUtc/SegReal = hora del dato de las fotos de antes y de ahora (NQ: dato = sello), NaN sin hora", n > 0 && cboe > 0 && malos == 0 && invalidasConHora == 0,
               n + " ventanas validas (" + cboe + " de CBOE, sello - dato de la foto de ahora " + F(offMin, "0") + " a " + F(offMax, "0") + " s esta noche); " + malos + " mal" + (malo != "" ? " (" + malo + ")" : "")
               + "; invalidas con hora " + invalidasConHora + "; ej. " + ej);
        }

        // ================================================================== P5 reinicio
        static void PruebaReinicio(LibroNqFamilia nq, BajadorCboe cb)
        {
            P();
            P("---- P5 volumen reiniciado => NaN ----");
            // (a) NQ: la primera foto de la sesion nueva (10-08 22:00:30, vol total 1) contra la ultima de la anterior (21:07:52, vol total 18093)
            var fa = nq.Fotos(Utc("2026-10-08 22:00:00"), Utc("2026-10-08 22:01:00")).FirstOrDefault();
            var fb = nq.Fotos(Utc("2026-10-08 21:00:00"), Utc("2026-10-08 21:10:00")).LastOrDefault();
            if (fa == null || fb == null) { Ok("P5a NQ reinicio", false, "faltan fotos 10-08 21:07 / 22:00"); }
            else
            {
                var r = new CambiosLibro { Libro = "NQ", Ahora = fa, AhoraOriginal = fa, TUtc = Utc("2026-10-08 22:01:00"), S = fa.FuturoFoto, Escala = 0.2 };
                r.Indice = ClavesCambios.Indexar(fa, r.TUtc); r.Base = CambiosFamilia.Valuar(r, fa);
                var v = new CambioVentana { Minutos = 30 };
                CambiosFamilia.CompararVolumen(r, v, fb, ClavesCambios.Mapa(fb, false));
                Ok("P5a NQ " + H(fa.TsUtc) + " contra " + H(fb.TsUtc) + ": NaN \"volumen de otro dia de negociacion\" (sesion de CME)", !v.Valida && v.Nota.StartsWith("volumen de otro dia de negociacion"),
                   "valida=" + v.Valida + ", " + v.Nota);
            }
            // (d) reinicio dentro del MISMO dia (red de seguridad): la foto de antes con todo el volumen 5 contratos mas alto
            var fx = nq.Fotos(Utc("2026-10-09 02:00:00"), Utc("2026-10-09 02:02:00")).FirstOrDefault();
            if (fx == null) { Ok("P5d NQ reinicio del mismo dia", false, "falta la foto de 10-09 02:00"); }
            else
            {
                var X = fx.Filas;
                var antes = ClavesCambios.ConColumnas(fx, X.Select(x => x.OiC).ToArray(), X.Select(x => x.OiP).ToArray(), X.Select(x => x.VolC + 5).ToArray(), X.Select(x => x.VolP + 5).ToArray());
                var r = new CambiosLibro { Libro = "NQ", Ahora = fx, AhoraOriginal = fx, TUtc = fx.TsUtc, S = fx.FuturoFoto, Escala = 0.2 };
                r.Indice = ClavesCambios.Indexar(fx, r.TUtc); r.Base = CambiosFamilia.Valuar(r, fx);
                var v = new CambioVentana { Minutos = 15 };
                CambiosFamilia.CompararVolumen(r, v, antes, ClavesCambios.Mapa(antes, false));
                Ok("P5d NQ mismo dia con el volumen de antes MAS ALTO en todas las claves: NaN \"volumen reiniciado\"", !v.Valida && v.Nota.StartsWith("volumen reiniciado"), "valida=" + v.Valida + ", " + v.Nota);
            }
            // (b) NDX: primera foto con dato del dia 10-08 contra la de 15 min antes (cadena congelada de la vispera) con la regla de la ventana
            var ndx = cb.Fotos("NDX", Utc("2026-10-08 13:30:00"), Utc("2026-10-08 14:30:00"));
            var nueva = ndx.FirstOrDefault(f => f.Filas.Length > 0 && f.DatoUtc >= Utc("2026-10-08 13:30:00"));
            if (nueva == null) { Ok("P5b NDX reinicio", false, "sin la primera foto con dato del 10-08"); }
            else
            {
                var antes = FotosCambios.Antes(null, cb, "NDX", nueva.TsUtc.AddMinutes(-15), nueva.GeneradoUtc, 450);
                var r = new CambiosLibro { Libro = "NDX", Ahora = nueva, AhoraOriginal = nueva, TUtc = nueva.GeneradoUtc, S = nueva.Spot, Escala = 1 };
                r.Indice = ClavesCambios.Indexar(nueva, r.TUtc); r.Base = CambiosFamilia.Valuar(r, nueva);
                var v = new CambioVentana { Minutos = 15 };
                CambiosFamilia.CompararVolumen(r, v, antes, ClavesCambios.Mapa(antes, false));
                Ok("P5b NDX " + H(nueva.TsUtc) + " (dato " + H(nueva.DatoUtc) + ") contra " + H(antes.TsUtc) + " (dato " + H(antes.DatoUtc) + "): NaN \"volumen de otro dia de negociacion\"",
                   !v.Valida && v.Nota.StartsWith("volumen de otro dia de negociacion"), "valida=" + v.Valida + ", " + v.Nota);
            }
        }

        static DateTime Utc(string s) => DateTime.SpecifyKind(DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", Inv), DateTimeKind.Utc);

        // ================================================================== P5c sin la publicacion anterior en memoria (el lunes de NQ: 30 h)
        static void PruebaSinR1(LibroNqFamilia nq)
        {
            // memoria que empieza DESPUES del ultimo salto (10-09 01:48:56): un solo OI visto -> NaN con nota, nunca 0
            var fotos = nq.Fotos(Utc("2026-10-09 01:50:00"), DateTime.MaxValue);
            var det = new RegimenesOi("NQ");
            foreach (var f in fotos) det.Procesar(f, f.TsUtc);
            var ahora = fotos[fotos.Count - 1];
            var r = new CambiosLibro { Libro = "NQ", Ahora = ahora, AhoraOriginal = ahora, TUtc = ahora.TsUtc, S = ahora.FuturoFoto, Escala = 0.2 };
            r.Indice = ClavesCambios.Indexar(ahora, r.TUtc); r.Base = CambiosFamilia.Valuar(r, ahora);
            var v = new CambioVentana { Minutos = 0 };
            CambiosFamilia.CompararOi(r, v, det);
            Ok("P5c NQ sin la publicacion anterior en memoria (como el lunes): NaN con nota", !v.Valida && det.R1 == null && v.Nota.StartsWith("OI sin la sesion anterior"),
               fotos.Count + " fotos desde 01:50Z, saltos vistos " + det.Saltos.Count + "; valida=" + v.Valida + ", " + v.Nota);
        }

        // ================================================================== P6 caso a mano
        static void PruebaCasoAMano(List<Paso> pasos, ILibroNq nq, IFuenteCboe cb)
        {
            P();
            P("---- P6 caso a mano: NQ muro C, calls, gamma de ahora x Δvol 15 min ----");
            // el ultimo minuto con muro C de NQ por volumen en 31100 CON volumen nuevo en 15 min (si no, el ultimo muro C de NQ con cambio != 0)
            Paso p = null; NivelActual n = null;
            for (int i = pasos.Count - 1; i >= 0 && n == null; i--)
            {
                var c = pasos[i].Anotada.Actuales.FirstOrDefault(x => x.Serie == "MUROS_NQ_vol" && x.Rol == "muro C" && Math.Abs(x.Strike - 31100) < 0.01 && x.CambioVolM[1] > 0);
                if (c != null) { p = pasos[i]; n = c; }
            }
            if (n == null)
                for (int i = pasos.Count - 1; i >= 0 && n == null; i--)
                {
                    var c = pasos[i].Anotada.Actuales.FirstOrDefault(x => x.Serie == "MUROS_NQ_vol" && x.Rol == "muro C" && x.CambioVolM[1] > 0);
                    if (c != null) { p = pasos[i]; n = c; }
                }
            if (n == null) { Ok("P6 caso a mano", false, "no hay muro C de NQ con cambio de 15 min en la noche"); return; }
            var r = p.Res["NQ"]; var v = r.Vol[1];
            var t = TiempoFam.DeClave(p.U.Clave);
            double S = r.S, K = n.Strike, suma = 0;
            double env = FilasHoy.Envejecer(r.Ahora.GeneradoUtc, t);
            var de = r.Ahora.Dias.Select(d => d - env).ToArray();
            double mas = de.Where(x => x >= 0).DefaultIfEmpty(0).Min(), tope = Math.Max(1.0, mas + 0.01);
            var mapaAntes = ClavesCambios.Mapa(v.Antes, false);
            P("   minuto " + H(t) + ", S " + F(S, "0.00") + ", foto ahora " + H(r.Ahora.TsUtc) + ", antes " + H(v.Antes.TsUtc) + " (" + F(v.SegReal, "0") + " s); strike " + F(K));
            foreach (var x in r.Ahora.Filas)
            {
                if (Math.Abs(x.K - K) > 1e-9 || !(x.IvC > 0)) continue;
                double dv = de[x.V];
                if (!(dv >= 0 && dv <= tope)) continue;
                var clave = new ClaveFila(x.K, ClavesCambios.Venc30(r.Ahora, x.V), 1);
                if (!mapaAntes.TryGetValue(clave, out var volAntes)) { P("   venc " + clave.VencUtc.ToString("MM-dd HH:mm", Inv) + "Z: sin la clave en la foto de antes (fuera)"); continue; }
                double T = Math.Max(dv, 1.0 / 1440.0) / 365.0;
                double g = GammaFam.B76(S, K, T, x.IvC);
                double dVol = x.VolC - volAntes;
                if (dVol < 0) { P("   venc " + clave.VencUtc.ToString("MM-dd HH:mm", Inv) + "Z: el volumen bajo (fuera)"); continue; }
                double aporte = g * dVol * 100.0 * S * S * 0.01 * 0.2 / 1e6;
                suma += aporte;
                P("   venc " + clave.VencUtc.ToString("MM-dd HH:mm", Inv) + "Z: iv " + F(x.IvC, "0.######") + ", T " + F(T * 365, "0.#####") + " d, gamma " + g.ToString("0.#######E+0", Inv)
                  + ", vol " + F(volAntes, "0") + " -> " + F(x.VolC, "0") + " (Δ " + F(dVol, "0") + "), aporte " + F(aporte, "0.######") + " M");
            }
            double rel = Math.Abs(suma - n.CambioVolM[1]) / Math.Max(1e-12, Math.Abs(suma));
            Ok("P6 NQ muro C " + F(K) + " a mano (B76 x Δvol 15 min x S^2 x 0,2 / 1e6) == CambioVolM[15 min]", suma > 0 && rel < 1e-12,
               "a mano " + F(suma, "0.#########") + " M, CambiosFamilia " + F(n.CambioVolM[1], "0.#########") + " M (monto del muro " + F(n.GexM, "0.0") + " M; cobertura " + F(n.CambioCobertura[1], "0.000") + ")");
        }

        // ================================================================== P7 FAM
        static void PruebaFam(List<Paso> pasos)
        {
            P();
            P("---- P7 balde FAM = suma de los libros (contra ReglasFam.Fusion sobre los perfiles Δ) ----");
            int n = 0, malos = 0; string primero = ""; double maxRel = 0; string ej = "";
            int nocheNaN = 0, nocheOk = 0, alineadas = 0, desalineadas = 0, malosV = 0; string ejNoche = "", ejV = "", primeroV = "";
            foreach (var p in pasos)
            {
                foreach (var a in p.Anotada.Actuales.Where(x => x.Serie == "FAM_MUROS_vol" || x.Serie == "FAM_MUROS_oi"))
                {
                    bool oi = a.Fuente == "oi";
                    char lado = a.Rol == "muro C" ? 'C' : 'P';
                    for (int w = 0; w < (oi ? 1 : 3); w++)
                    {
                        double mio = oi ? a.CambioOiDiaM : a.CambioVolM[w];
                        // independiente: LibroMinuto con los Δ por K (+-3 % de fut, Fut = K + conv | K x razon) -> ReglasFam.Fusion
                        var bms = new List<LibroMinuto>(); bool falta = false; var vs = new List<CambioVentana>();
                        foreach (var lb in CambiosFamilia.Libros)
                        {
                            if (!p.Res.TryGetValue(lb, out var r) || r.Ahora == null) { falta = true; break; }
                            var v = oi ? r.Oi : r.Vol[w];
                            if (v == null || !v.Valida) { falta = true; break; }
                            vs.Add(v);
                            var meta = p.U.MetaDe(lb);
                            var ks = new List<int>();
                            for (int i = 0; i < r.Base.K.Length; i++)
                            {
                                double fu = lb == "QQQ" ? r.Base.K[i] * meta.Conv : r.Base.K[i] + meta.Conv;
                                if (Math.Abs(fu - p.U.FutMnq) <= p.U.FutMnq * 0.03) ks.Add(i);
                            }
                            var bm = new LibroMinuto { Libro = lb, Mult = lb == "NQ" ? 20 : 100, PorRazon = lb == "QQQ", Conv = meta.Conv, FutMnq = p.U.FutMnq,
                                Fut = new double[ks.Count], K = new double[ks.Count], GvC = new double[ks.Count], GvP = new double[ks.Count], GoC = new double[ks.Count], GoP = new double[ks.Count] };
                            for (int j = 0; j < ks.Count; j++)
                            {
                                double k = r.Base.K[ks[j]];
                                bm.K[j] = k; bm.Fut[j] = lb == "QQQ" ? k * meta.Conv : k + meta.Conv;
                                bm.GvC[j] = v.Delta.Valor(k, 'C', false); bm.GvP[j] = v.Delta.Valor(k, 'P', false);
                                bm.GoC[j] = v.Delta.Valor(k, 'C', true); bm.GoP[j] = v.Delta.Valor(k, 'P', true);
                            }
                            bms.Add(bm);
                        }
                        // OI: la familia exige que las publicaciones vigentes (R0) sean de la misma noche (sesion de CME), y las anteriores con salto visto
                        bool noche = true;
                        if (!falta && oi)
                        {
                            var s0 = vs.Select(x => TiempoFam.Sesion(x.R0.InicioUtc)).Distinct().Count();
                            var s1 = vs.Where(x => x.R1.InicioEsSalto && !x.R1.Dudoso).Select(x => TiempoFam.Sesion(x.R1.InicioUtc)).Distinct().Count();
                            noche = s0 == 1 && s1 <= 1;
                            if (!noche)
                            {
                                nocheNaN++;
                                if (!double.IsNaN(mio) || !a.CambioNota.StartsWith("publicaciones de distinta noche") || a.CambioOiHastaUtc != default) { malos++; if (primero == "") primero = H(TiempoFam.DeClave(p.U.Clave)) + " FAM OI de distinta noche publicado: " + F(mio, "R") + " " + a.CambioNota; }
                                if (ejNoche == "") ejNoche = H(TiempoFam.DeClave(p.U.Clave)) + " " + a.Serie + " " + a.Rol + ": " + a.CambioNota;
                            }
                            else nocheOk++;
                        }
                        // volumen: ventana agregada solo si las de los libros coinciden (+-90 s); si no, MinValue y la nota con la de cada libro
                        if (!falta && !oi)
                        {
                            DateTime dmin = vs.Min(x => x.DesdeUtc), dmax = vs.Max(x => x.DesdeUtc), hmin = vs.Min(x => x.HastaUtc), hmax = vs.Max(x => x.HastaUtc);
                            bool al = (dmax - dmin).TotalSeconds <= 90 && (hmax - hmin).TotalSeconds <= 90;
                            bool okV = al ? a.CambioVolDesdeUtc[w] == dmin && a.CambioVolHastaUtc[w] == hmax
                                          : a.CambioVolDesdeUtc[w] == DateTime.MinValue && a.CambioVolHastaUtc[w] == DateTime.MinValue && a.CambioNota.Contains("ventana de cada libro hasta la hora de su dato");
                            okV &= a.CambioVolSegReal[w] == vs.Max(x => x.SegReal);
                            if (al) alineadas++; else desalineadas++;
                            if (!okV) { malosV++; if (primeroV == "") primeroV = H(TiempoFam.DeClave(p.U.Clave)) + " " + a.Serie + " w" + w + ": " + H(a.CambioVolDesdeUtc[w]) + " -> " + H(a.CambioVolHastaUtc[w]) + " · " + a.CambioNota; }
                            if (!al && ejV == "") ejV = H(TiempoFam.DeClave(p.U.Clave)) + " " + a.Serie + " " + a.Rol + ": " + a.CambioNota;
                        }
                        double ref_ = double.NaN;
                        if (!falta && noche)
                        {
                            var fam = ReglasFam.Fusion(bms, p.U.FutMnq);
                            int ib = Array.FindIndex(fam.K, k => Math.Abs(k - a.Precio) < 1e-6);
                            ref_ = ib < 0 ? 0.0 : (lado == 'C' ? (oi ? fam.GoC[ib] : fam.GvC[ib]) : (oi ? fam.GoP[ib] : fam.GvP[ib])) / 1e6;
                        }
                        n++;
                        bool ok = double.IsNaN(ref_) ? double.IsNaN(mio) : Math.Abs(mio - ref_) <= 1e-9 * Math.Max(1e-9, Math.Abs(ref_)) || Math.Abs(mio - ref_) < 1e-12;
                        if (!double.IsNaN(ref_) && Math.Abs(ref_) > 1e-9) maxRel = Math.Max(maxRel, Math.Abs(mio - ref_) / Math.Abs(ref_));
                        if (!ok) { malos++; if (primero == "") primero = H(TiempoFam.DeClave(p.U.Clave)) + " " + a.Serie + " " + a.Rol + " " + F(a.Precio) + ": " + F(mio, "R") + " vs " + F(ref_, "R"); }
                        if (ej == "" && !double.IsNaN(mio) && mio != 0) ej = H(TiempoFam.DeClave(p.U.Clave)) + " " + a.Serie + " " + a.Rol + " balde " + F(a.Precio) + " (" + (oi ? "OI" : (CambiosVentanas.Min[w] + " min")) + "): " + F(mio, "0.####") + " M = Fusion " + F(ref_, "0.####") + " M";
                    }
                }
            }
            Ok("P7 FAM_MUROS vol/oi: cambio del balde == ReglasFam.Fusion de los Δ por libro (OI: NaN si las publicaciones son de distinta noche)", n > 0 && malos == 0,
               n + " valores; " + malos + " distintos" + (primero != "" ? " (primero: " + primero + ")" : "") + "; error relativo maximo " + maxRel.ToString("0.###E+0", Inv) + (ej != "" ? "; ej. " + ej : ""));
            Ok("P7b FAM OI de esta noche: publicaciones de distinta noche por libro -> NaN con nota y sin fechas", nocheNaN > 0 && malos == 0,
               "distinta noche " + nocheNaN + ", misma noche " + nocheOk + " (FAM_MUROS_oi existe recien despues del salto de NQ de las 01:48Z: el caso de la misma noche se prueba en P7d); ej. " + ejNoche);
            Ok("P7c FAM volumen: ventana agregada solo si las de los libros coinciden; si no, sin horas y la de cada libro en la nota; SegReal = la mayor", malosV == 0 && alineadas + desalineadas > 0,
               "coinciden " + alineadas + ", no coinciden " + desalineadas + ", mal " + malosV + (primeroV != "" ? " (" + primeroV + ")" : "") + "; ej. " + ejV);
        }

        // ================================================================== P7d misma noche con los regimenes reales
        static void PruebaMismaNoche(LibroNqFamilia nq, BajadorCboe cb)
        {
            P();
            P("---- P7d FAM OI: MismaNoche con los regimenes REALES (historia completa) en la rueda del 10-08 y en la noche del 10-09 ----");
            foreach (var (hora, esperado) in new[] { ("2026-10-08 15:00:00", true), ("2026-10-09 02:00:00", false), ("2026-10-08 07:00:00", false), ("2026-10-08 10:00:00", true) })
            {
                var t = Utc(hora);
                var l = new List<(string Libro, CambioVentana V)>(); var txt = new List<string>();
                foreach (var lb in CambiosFamilia.Libros)
                {
                    var det = new RegimenesOi(lb);
                    var fotos = lb == "NQ" ? nq.Fotos(DateTime.MinValue, t) : cb.Fotos(lb, DateTime.MinValue, t);
                    foreach (var f in fotos) det.Procesar(f, lb == "NQ" ? f.TsUtc : f.GeneradoUtc);
                    l.Add((lb, new CambioVentana { R0 = det.R0, R1 = det.R1 }));
                    txt.Add(lb + " " + (det.R0 == null ? "-" : H(det.R0.InicioUtc)) + " vs " + (det.R1 == null ? "-" : H(det.R1.InicioUtc) + (det.R1.InicioEsSalto ? "" : " o antes")));
                }
                bool m = CambiosFamilia.MismaNoche(l);
                Ok("P7d " + hora + "Z: MismaNoche = " + esperado, m == esperado, string.Join("; ", txt));
            }
        }

        // ================================================================== P3 en la rueda (NDX/QQQ con volumen nuevo de verdad)
        static void PruebaRueda(BajadorCboe cb, string logs)
        {
            P();
            P("---- P3b linealidad y Δ en la rueda (NDX/QQQ 10-08 15:00 UTC; S = spot de la foto, solo para la identidad) ----");
            var t = Utc("2026-10-08 15:00:00");
            var conv = new PythiaGexCuatro.Familia.Qqq.ConversionQqq(new PythiaGexCuatro.Familia.Qqq.OpcionesQqq { Corregida = true });
            foreach (var lb in new[] { "NDX", "QQQ" })
            {
                var (orig, us) = FotosCambios.AhoraCboe(cb, lb, t, true, conv, out var mot);
                if (us == null) { Ok("P3b " + lb, false, mot); continue; }
                var r = new CambiosLibro { Libro = lb, Ahora = us, AhoraOriginal = orig, TUtc = t, S = us.Spot, PorRazon = lb == "QQQ", Escala = 1 };
                r.Indice = ClavesCambios.Indexar(us, t); r.Base = CambiosFamilia.Valuar(r, us);
                double maxRel = 0; bool todoOk = true; var txt = new List<string>();
                foreach (var W in CambiosVentanas.Min)
                {
                    var antes = FotosCambios.Antes(null, cb, lb, us.TsUtc.AddMinutes(-W), orig.GeneradoUtc, Math.Max(120, W * 30));
                    if (antes == null) { todoOk = false; txt.Add(W + " min: sin foto de antes"); continue; }
                    var v = new CambioVentana { Minutos = W, HastaUtc = us.DatoUtc, DesdeUtc = antes.DatoUtc, Antes = antes, SegReal = (us.DatoUtc - antes.DatoUtc).TotalSeconds };
                    CambiosFamilia.CompararVolumen(r, v, antes, ClavesCambios.Mapa(antes, false));
                    if (!v.Valida) { todoOk = false; txt.Add(W + " min NaN: " + v.Nota); continue; }
                    var (rel, ok) = Linealidad(r, v, false);
                    todoOk &= ok; maxRel = Math.Max(maxRel, rel);
                    // el strike con mayor Δ de calls
                    int im = -1; for (int i = 0; i < v.Delta.K.Length; i++) if (im < 0 || v.Delta.VC[i] > v.Delta.VC[im]) im = i;
                    txt.Add(W + " min (dato " + H(antes.DatoUtc) + " -> " + H(us.DatoUtc) + ", sellos " + H(antes.TsUtc) + " -> " + H(us.TsUtc) + ", " + v.Distintas + " claves con volumen nuevo de " + v.Comparadas
                            + (im >= 0 ? "; mayor Δ calls en " + F(v.Delta.K[im]) + ": " + F(v.Delta.VC[im] / 1e6, "0.###") + " M" : "") + ")");
                }
                Ok("P3b " + lb + " en la rueda: linealidad 1e-9 rel en las 3 ventanas", todoOk, "error relativo maximo " + maxRel.ToString("0.###E+0", Inv) + "; " + string.Join("; ", txt));
            }
        }

        // ================================================================== P11 apertura: foto de antes de otro dia
        static void PruebaApertura(BajadorCboe cb, string logs)
        {
            P();
            P("---- P11 apertura 10-01..10-08 (NDX/QQQ/TQQQ, fotos de 13:00 a 15:30Z): CERO ventanas validas con la foto de antes de otro dia; ventana = hora del dato ----");
            var cam = new CambiosFamilia(null, cb, new OpcionesCambios { RutaLog = Path.Combine(logs, "pythiagex4-cambios-apertura.log") });
            var dias = new[] { "2026-10-01", "2026-10-02", "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08" };
            string NyDia(DateTime t) => TiempoFam.ANy(t).ToString("yyyy-MM-dd", Inv);
            foreach (var lb in new[] { "NDX", "QQQ", "TQQQ" })
            {
                int tot = 0, val = 0, otroDia = 0, malHora = 0, congeladas = 0; var notas = new SortedDictionary<string, int>(StringComparer.Ordinal);
                string malo = "", caso = "", ej = ""; double offMin = double.MaxValue, offMax = double.MinValue; bool casoOk = lb != "NDX";
                var sw = Stopwatch.StartNew();
                foreach (var dia in dias)
                {
                    var fotos = cb.Fotos(lb, Utc(dia + " 13:00:00"), Utc(dia + " 15:30:00")).Where(f => f.Filas.Length > 0).ToList();
                    foreach (var orig in fotos)
                    {
                        var us = lb == "TQQQ" ? orig : FotosCambios.ASegundos(orig);
                        var r = new CambiosLibro { Libro = lb, EsTqqq = lb == "TQQQ", Ahora = us, AhoraOriginal = orig, TUtc = us.GeneradoUtc, S = us.Spot, PorRazon = lb == "QQQ", Escala = 1 };
                        r.Indice = ClavesCambios.Indexar(us, lb == "TQQQ" ? (DateTime?)null : us.GeneradoUtc); r.Base = CambiosFamilia.Valuar(r, us);
                        foreach (var W in CambiosVentanas.Min)
                        {
                            var v = cam.Ventana(r, W);
                            tot++;
                            bool esCaso = lb == "NDX" && W == 30 && orig.TsUtc == Utc("2026-10-01 14:17:28");
                            if (esCaso) { caso = "NDX 30 min, ahora sello 10-01 14:17:28Z (dato " + H(us.DatoUtc) + "), antes " + (v.Antes == null ? "-" : "sello " + H(v.Antes.TsUtc) + " dato " + H(v.Antes.DatoUtc)) + ": " + (v.Valida ? "VALIDA" : "NaN " + v.Nota); casoOk = !v.Valida && v.Nota.StartsWith("volumen de otro dia de negociacion"); }
                            if (!v.Valida)
                            {
                                string k = v.Nota; int i = k.IndexOf(" ("); if (i > 0) k = k.Substring(0, i); i = k.IndexOf(" de hace"); if (i > 0) k = k.Substring(0, i);
                                notas[k] = notas.TryGetValue(k, out var c) ? c + 1 : 1;
                                continue;
                            }
                            val++;
                            if (v.Congelada) congeladas++;
                            if (NyDia(v.Antes.DatoUtc) != NyDia(us.DatoUtc)) { otroDia++; if (malo == "") malo = "otro dia: ahora " + H(orig.TsUtc) + " dato " + H(us.DatoUtc) + ", antes dato " + H(v.Antes.DatoUtc) + " (" + W + " min)"; }
                            if (v.HastaUtc != us.DatoUtc || v.DesdeUtc != v.Antes.DatoUtc || v.SegReal != (us.DatoUtc - v.Antes.DatoUtc).TotalSeconds)
                            { malHora++; if (malo == "") malo = "hora: " + H(v.DesdeUtc) + " -> " + H(v.HastaUtc) + " para dato " + H(v.Antes.DatoUtc) + " -> " + H(us.DatoUtc); }
                            if (!v.Congelada) { double off = (orig.TsUtc - us.DatoUtc).TotalSeconds; offMin = Math.Min(offMin, off); offMax = Math.Max(offMax, off); }
                            if (ej == "" && !v.Congelada && W == 15 && dia == "2026-10-08" && orig.TsUtc >= Utc(dia + " 14:58:00"))
                                ej = "10-08 sello " + H(orig.TsUtc) + ", 15 min: ventana publicada (dato) " + H(v.DesdeUtc) + " -> " + H(v.HastaUtc) + "; sellos " + H(v.Antes.TsUtc) + " -> " + H(orig.TsUtc);
                        }
                    }
                }
                Ok("P11 " + lb + " apertura: 0 ventanas validas con la foto de antes de otro dia de negociacion, todas con la hora del dato" + (lb == "NDX" ? " (y el caso de la revision en NaN)" : ""),
                   val > 0 && otroDia == 0 && malHora == 0 && casoOk,
                   tot + " ventanas, " + val + " validas (" + congeladas + " congeladas), otro dia " + otroDia + ", hora mal " + malHora + (malo != "" ? " (" + malo + ")" : "")
                   + "; sello - dato en las validas con datos de la rueda: " + F(offMin, "0") + " a " + F(offMax, "0") + " s; NaN: " + string.Join(", ", notas.Select(kv => kv.Key + " " + kv.Value))
                   + (caso != "" ? "; " + caso : "") + (ej != "" ? "; ej. " + ej : "") + "; " + sw.ElapsedMilliseconds + " ms");
            }
        }

        // ================================================================== P8 TQQQ de punta a punta
        static void PruebaTqqq(BajadorCboe cb, string logs)
        {
            P();
            P("---- P8 TQQQ (TqqqPerfil) de punta a punta: vela sintetica de la rueda del 10-08 ----");
            var t = Utc("2026-10-08 15:01:00");
            var ses = TqqqSesion.De(t, true, false);
            var fotos = cb.Fotos("TQQQ", ses.FotosIniUtc, ses.FotosFinUtc).Where(f => f.Filas.Length > 0).ToList();
            long T = TiempoFam.Ms(t) / 120000L * 120000L;
            var f0 = fotos.LastOrDefault(f => TiempoFam.Ms(f.GeneradoUtc) <= T + 120000L);
            if (f0 == null) { Ok("P8 TQQQ", false, "sin fotos de TQQQ en la rueda del 10-08"); return; }
            var vela = new TqqqVela { AperturaM2Ms = T, DatoUtc = f0.DatoUtc, S = 124.2, C = 21000 };
            var nv = TqqqPerfil.Niveles(f0);
            var act = new List<NivelActual>();
            foreach (var id in new[] { "T_DOMS_vol", "T_MUROS_vol", "T_MUROS_oi" })
                if (nv.Series.TryGetValue(id, out var l))
                    foreach (var x in l)
                        act.Add(new NivelActual { Serie = id, Libro = "TQQQ", Fuente = id.EndsWith("_oi") ? "oi" : "vol", Tipo = id.StartsWith("T_DOMS") ? "DOMS" : "MUROS", Rol = x.Rol,
                                                  Precio = 124.2 * x.K + 21000, Strike = x.K, GexM = x.GexM, DatoUtc = f0.DatoUtc });
            var cam = new CambiosFamilia(null, cb, new OpcionesCambios { RutaLog = Path.Combine(logs, "pythiagex4-cambios-tqqq.log"), PresupuestoHistoriaMs = 0 });
            var foto = new FotoFamilia { Sesion = "2026-10-08", Actuales = act.AsReadOnly(), Fuentes = Array.Empty<FuenteEstado>() };
            var an = cam.Anotar(foto, null, vela);
            var r = cam.Resultado("TQQQ");
            if (r?.Ahora == null) { Ok("P8 TQQQ", false, r?.Nota ?? "sin resultado"); return; }
            int malosMonto = 0, malosD = 0, n = 0, malHora = 0; var txt = new List<string>();
            foreach (var v in r.Vol) if (v != null && v.Valida && (v.HastaUtc != f0.DatoUtc || v.DesdeUtc != v.Antes.DatoUtc)) malHora++;
            foreach (var a in an.Actuales)
            {
                char lado = a.Rol == "muro C" ? 'C' : a.Rol == "muro P" ? 'P' : 'N';
                bool oi = a.Fuente == "oi";
                double m = TqqqNum.PyRound(r.Base.Valor(a.Strike, lado, oi) / 1e6, 2);
                if (!(m == a.GexM)) malosMonto++;
                // a mano: TqqqPerfil.Calcular(ahora) - TqqqPerfil.Calcular(ahora con la columna de antes)
                for (int w = 0; w < (oi ? 1 : 3); w++)
                {
                    var v = oi ? r.Oi : r.Vol[w];
                    double mio = oi ? a.CambioOiDiaM : a.CambioVolM[w];
                    if (v == null || !v.Valida) { if (!double.IsNaN(mio)) malosD++; txt.Add(a.Serie + " " + a.Rol + " " + F(a.Strike) + (oi ? " OI" : " " + CambiosVentanas.Min[w] + " min") + ": NaN (" + v?.Nota + ")"); continue; }
                    var (rel, ok) = Linealidad(r, v, oi);
                    n++;
                    if (!ok) malosD++;
                    txt.Add(a.Serie + " " + a.Rol + " " + F(a.Strike) + (oi ? " OI " + H(v.HastaUtc) + " vs " + H(v.DesdeUtc) : " " + CambiosVentanas.Min[w] + " min") + ": " + F(mio, "0.####") + " M (GexM " + F(a.GexM, "0.00") + ")");
                }
            }
            Ok("P8 TQQQ: GexM reconstruido (round 2), linealidad e identidad por nivel; ventanas con la hora del dato", malosMonto == 0 && malosD == 0 && n > 0 && malHora == 0,
               "foto " + H(f0.TsUtc) + " dato " + H(f0.DatoUtc) + "; " + an.Actuales.Count + " niveles; montos distintos " + malosMonto + "; cambios mal " + malosD + " de " + n
               + "; ventanas con hora distinta de la del dato " + malHora + " (15 min: " + (r.Vol[1].Valida ? H(r.Vol[1].DesdeUtc) + " -> " + H(r.Vol[1].HastaUtc) : r.Vol[1].Nota) + ")");
            foreach (var s in txt.Take(12)) P("   " + s);
            foreach (var e in an.CambiosEstado) P("   estado: " + e);
        }

        // ================================================================== P9 tiempo
        static void PruebaTiempo(List<Paso> pasos, List<RegistroMinuto> minutos, ILibroNq nq, IFuenteCboe cb, string logs)
        {
            P();
            P("---- P9 tiempo por llamada (datos reales) ----");
            var ms = pasos.Select(p => p.Ms).OrderBy(x => x).ToList();
            var recal = pasos.Where(p => p.Recalculo && !p.Pendiente).Select(p => p.Ms).OrderBy(x => x).ToList();
            double p50 = ms[ms.Count / 2], max = ms[ms.Count - 1];
            // aciertos de cache: la misma foto otra vez (como el hilo cada 5 s sin minuto nuevo)
            var cam = new CambiosFamilia(nq, cb, new OpcionesCambios { RutaLog = Path.Combine(logs, "pythiagex4-cambios-cache.log"), PresupuestoHistoriaMs = 0 });
            var u = minutos[minutos.Count - 1];
            var foto = new FotoFamilia { Actuales = SalidaFamilia.Actuales(u, null, null).AsReadOnly(), Fuentes = Array.Empty<FuenteEstado>() };
            var sw = Stopwatch.StartNew(); cam.Anotar(foto, u, null); double primera = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); cam.Anotar(foto, u, null); double igual = sw.Elapsed.TotalMilliseconds;
            var foto2 = new FotoFamilia { Actuales = SalidaFamilia.Actuales(u, null, null).AsReadOnly(), Fuentes = Array.Empty<FuenteEstado>() };
            sw.Restart(); cam.Anotar(foto2, u, null); double otraFoto = sw.Elapsed.TotalMilliseconds;
            // la cuenta = la llamada menos la escritura de la linea del log (E/S del disco: un antivirus o el disco ocupado la estiran sin que la
            // cuenta cambie) y sin las llamadas con una recoleccion de gen 2 adentro (pausa del GC de todo el proceso): las dos se informan aparte
            var pMax = pasos.OrderByDescending(x => x.Ms).First();
            var cuenta = pasos.Where(x => x.Gc2 == 0).Select(x => x.Ms - x.MsLog).OrderBy(x => x).ToList();
            double maxCuenta = cuenta.Count > 0 ? cuenta[cuenta.Count - 1] : double.NaN, maxLog = pasos.Max(x => x.MsLog);
            int lentas = pasos.Count(x => x.Ms >= 50), conGc = pasos.Count(x => x.Gc2 > 0);
            Ok("P9 la cuenta de cada llamada < 50 ms (con el presupuesto de historia de 25 ms del arranque); la llamada entera < 50 ms en el 99 %", maxCuenta < 50 && lentas <= pasos.Count / 100,
               pasos.Count + " llamadas: mediana " + F(p50, "0.00") + " ms, maxima " + F(max, "0.00") + " ms (" + H(TiempoFam.DeClave(pMax.U.Clave)) + ": log " + F(pMax.MsLog, "0.0") + " ms, GC gen2 " + pMax.Gc2
               + "); la cuenta sola (sin el log, " + cuenta.Count + " llamadas sin GC gen2): maxima " + F(maxCuenta, "0.00") + " ms; escritura del log maxima " + F(maxLog, "0.0") + " ms; llamadas >= 50 ms: " + lentas
               + "; con GC gen2: " + conGc + "; recalculos ya con la historia leida: mediana "
               + F(recal.Count > 0 ? recal[recal.Count / 2] : double.NaN, "0.00") + " ms, maxima " + F(recal.Count > 0 ? recal[recal.Count - 1] : double.NaN, "0.00") + " ms");
            P("   sin presupuesto, leyendo TODA la historia de OI en una llamada (lo que el presupuesto reparte en el arranque): " + F(primera, "0.0") + " ms; misma foto otra vez (cache): "
              + F(igual, "0.000") + " ms; foto nueva del motor, mismo minuto: " + F(otraFoto, "0.00") + " ms");
        }
    }
}
