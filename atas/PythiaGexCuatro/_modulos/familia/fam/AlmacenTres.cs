// AlmacenTres.cs — PythiaGex 4.1, modulo fam (B3d), 08-10-2026.
// TRES_NQ / TRES_NDX / TRES_QQQ = lo que DIBUJA el motor 3.0 que corre adentro de la 4.1 (el clon de atas/PythiaGexTres 3.6.9).
// De donde sale en el clon: GammaHoyTres.GuardarEstela(raiz, hora, L) escribe una linea por cambio de dominantes (o cada 60 s) en
//   %APPDATA%\ATAS\PythiaGex4\estela\estela-<NQ|NDX|QQQ>-<dia UTC>.jsonl   {"t":"...Z","d":[D1,D2,zero],...}
// para NQ (el libro de Rithmic, regla Tres) y para cada capa de CBOE (GammaHoyTresCapas: NDX por base, QQQ por razon, regla Clasica).
// Este almacen lee ESAS lineas (las del propio indicador, nunca las de PythiaGex3) de dos maneras que se pisan sin conflicto:
//   * el gancho del clon: Agregar(capa, linea) con el mismo texto que va al archivo (DISENO_4_1 §2.4.2), y
//   * la cola del archivo (LeerArchivos): al arrancar lee los dos archivos de la sesion y despues solo los bytes nuevos.
// La cuenta es la de la vista previa: extremos_rebote.leer_estela + niveles_estela (vida 300 s NQ, 1500 s NDX/QQQ; d > 15000; la ultima
// linea con t <= minuto; duplicados por t: gana la ultima). Sin referencias a ATAS.
//
// 4.1.2 (08-10-2026, montos en las etiquetas): la linea trae un campo NUEVO "gm":[m1,m2] alineado con d[0]/d[1] = el GEX NETO con signo
// del strike elegido para D1/D2 en M USD por 1 % EN UNIDADES DE LA FAMILIA (la 3.0 cuenta todo con multiplicador 100: NQ x0,2 para llevarlo
// al 20 del NQ, NDX/QQQ x1), con null donde la dominante no es un strike con monto (relleno +-1e6 del tunel por cruces de noche, vigente
// de la histeresis sin strike en el perfil). Lo escribe GammaHoyTres.GuardarEstela con MontosDominantes3/CampoGm de ESTE archivo (la
// convencion de escritura y de lectura en un solo lugar). Las lineas viejas (sin "gm") o con null dan GexM NaN: la paridad con la vista
// previa (que solo mira t y d) no cambia. "g" (|gex| sin signo, x100 en NQ, relleno 1 de noche) NO se usa.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PythiaGexCuatro.Familia
{
    public sealed class AlmacenTres
    {
        public static readonly IReadOnlyDictionary<string, int> VidaS = new Dictionary<string, int> { ["NQ"] = 300, ["NDX"] = 1500, ["QQQ"] = 1500 };

        private sealed class Capa
        {
            public readonly List<long> T = new List<long>();          // ticks UTC, ascendente, unicos
            public readonly List<double> D1 = new List<double>(), D2 = new List<double>();
            public readonly List<double> G1 = new List<double>(), G2 = new List<double>();   // 4.1.2: "gm" alineado con D1/D2 (NaN = sin monto)
            public long Version;
        }

        private readonly Dictionary<string, Capa> _capas = new Dictionary<string, Capa>(StringComparer.OrdinalIgnoreCase);
        private readonly object _llave = new object();
        private readonly Dictionary<string, long> _pos = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);   // ruta -> bytes ya leidos
        private DateTime _ini, _fin;

        public long LineasLeidas { get; private set; }
        public long LineasRotas { get; private set; }

        /// <summary>La ventana de la sesion [ini, fin) (leer_estela descarta lo de afuera).</summary>
        public AlmacenTres(DateTime iniUtc, DateTime finUtc)
        {
            _ini = iniUtc; _fin = finUtc;
            foreach (var c in CatalogoFamilia.CapasTres) _capas[c] = new Capa();
        }

        public long Version(string capa) { lock (_llave) return _capas.TryGetValue(capa, out var c) ? c.Version : 0; }

        /// <summary>Una linea de estela (el texto exacto del archivo). Hilo cualquiera.</summary>
        public bool Agregar(string capa, string linea)
        {
            if (!_capas.ContainsKey(capa ?? "")) return false;
            if (!Parsear(linea, out var t, out var d1, out var d2, out var g1, out var g2)) { LineasRotas++; return false; }
            if (t < _ini || t >= _fin) return false;
            lock (_llave) Poner(_capas[capa], t.Ticks, d1, d2, g1, g2);
            LineasLeidas++;
            return true;
        }

        /// <summary>Lee lo nuevo de estela-&lt;capa&gt;-&lt;d&gt;.jsonl para d en (dia - 1, dia) (como leer_estela), solo los bytes nuevos desde la ultima vez.
        /// Devuelve cuantas lineas nuevas entraron. Comparte el archivo con el escritor del clon (FileShare.ReadWrite).</summary>
        public int LeerArchivos(string carpetaEstela, string dia)
        {
            int n = 0;
            if (string.IsNullOrEmpty(carpetaEstela) || !Directory.Exists(carpetaEstela)) return 0;
            var d0 = DateTime.ParseExact(dia, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var capa in CatalogoFamilia.CapasTres)
                foreach (var d in new[] { d0.AddDays(-1), d0 })
                {
                    var ruta = Path.Combine(carpetaEstela, "estela-" + capa + "-" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
                    n += Cola(ruta, capa);
                }
            return n;
        }

        private int Cola(string ruta, string capa)
        {
            long pos = _pos.TryGetValue(ruta, out var p) ? p : 0;
            long tam;
            try { var fi = new FileInfo(ruta); if (!fi.Exists) return 0; tam = fi.Length; } catch { return 0; }
            if (tam < pos) pos = 0;                                    // lo reescribieron: desde el principio (los t repetidos se pisan)
            if (tam == pos) return 0;
            byte[] buf;
            try
            {
                using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    fs.Seek(pos, SeekOrigin.Begin);
                    buf = new byte[tam - pos];
                    int leidos = 0;
                    while (leidos < buf.Length) { int r = fs.Read(buf, leidos, buf.Length - leidos); if (r <= 0) break; leidos += r; }
                    if (leidos < buf.Length) Array.Resize(ref buf, leidos);
                }
            }
            catch { return 0; }
            int fin = Array.LastIndexOf(buf, (byte)'\n');
            if (fin < 0) return 0;                                      // linea a medio escribir: la proxima vuelta
            _pos[ruta] = pos + fin + 1;
            int n = 0;
            var txt = Encoding.UTF8.GetString(buf, 0, fin + 1);
            foreach (var l in txt.Split('\n'))
                if (l.Length > 0 && Agregar(capa, l.TrimEnd('\r'))) n++;
            return n;
        }

        /// <summary>extremos_rebote.leer_estela, una linea: t (UTC) y d[0], d[1] con 'x if (x and x > 15000) else NaN'.</summary>
        public static bool Parsear(string linea, out DateTime t, out double d1, out double d2) => Parsear(linea, out t, out d1, out d2, out _, out _);

        /// <summary>Lo mismo + 4.1.2: gm[0], gm[1] ALINEADOS con d[0], d[1] (si d[i] se descarta, gm[i] tambien). Sin "gm", null, no numero o
        /// no finito -> NaN. Un "gm" raro nunca rompe la linea (t y d valen igual: la paridad de la vista previa no depende de "gm").</summary>
        public static bool Parsear(string linea, out DateTime t, out double d1, out double d2, out double gm1, out double gm2)
        {
            t = default; d1 = d2 = gm1 = gm2 = double.NaN;
            if (string.IsNullOrWhiteSpace(linea)) return false;
            try
            {
                using (var doc = JsonDocument.Parse(linea))
                {
                    var r = doc.RootElement;
                    if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("t", out var te) || te.ValueKind != JsonValueKind.String) return false;
                    if (!DateTime.TryParse(te.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t)) return false;
                    t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
                    if (r.TryGetProperty("d", out var d) && d.ValueKind == JsonValueKind.Array)
                    {
                        int i = 0;
                        foreach (var x in d.EnumerateArray())
                        {
                            double v = x.ValueKind == JsonValueKind.Number ? x.GetDouble() : double.NaN;
                            v = (v != 0 && v > 15000) ? v : double.NaN;
                            if (i == 0) d1 = v; else if (i == 1) d2 = v; else break;
                            i++;
                        }
                    }
                    if (r.TryGetProperty("gm", out var g) && g.ValueKind == JsonValueKind.Array)
                    {
                        int i = 0;
                        foreach (var x in g.EnumerateArray())
                        {
                            double v = x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var y) && !double.IsNaN(y) && !double.IsInfinity(y) ? y : double.NaN;
                            if (i == 0) gm1 = v; else if (i == 1) gm2 = v; else break;
                            i++;
                        }
                    }
                    if (double.IsNaN(d1)) gm1 = double.NaN;   // alineado con d: la dominante que no vale no tiene monto
                    if (double.IsNaN(d2)) gm2 = double.NaN;
                    return true;
                }
            }
            catch { return false; }
        }

        private static void Poner(Capa c, long t, double d1, double d2, double g1, double g2)
        {
            int n = c.T.Count;
            if (n == 0 || t > c.T[n - 1]) { c.T.Add(t); c.D1.Add(d1); c.D2.Add(d2); c.G1.Add(g1); c.G2.Add(g2); }
            else
            {
                int i = c.T.BinarySearch(t);
                if (i >= 0) { c.D1[i] = d1; c.D2[i] = d2; c.G1[i] = g1; c.G2[i] = g2; }   // mismo t: gana la ultima (drop_duplicates keep="last")
                else { i = ~i; c.T.Insert(i, t); c.D1.Insert(i, d1); c.D2.Insert(i, d2); c.G1.Insert(i, g1); c.G2.Insert(i, g2); }
            }
            c.Version++;
        }

        // ------------------------------------------------------------------ 4.1.2: el monto de las rayas 3.0 (escritura, la usa GammaHoyTres)

        /// <summary>El relleno que pone el tunel por CRUCES de signo (GammaHoyTresTunel.AplicarTunelCruces: techo +1e6, piso -1e6): los cruces no
        /// son strikes, ese numero no es un monto.</summary>
        public const double RellenoTunel3 = 1e6;

        /// <summary>De la cuenta de la 3.0 (GammaHoyNucleo.MULT_INDICE = 100 para todo) a las unidades de la familia (C1: NQ 20, NDX 100, QQQ 100,
        /// ReglasFam.Escala): NQ 0,2; NDX y QQQ 1. NaN para cualquier otra raiz (ES/RTY de la 3.0: la familia no tiene unidad para ellas).</summary>
        public static double FactorMonto3(string raiz)
        {
            if (string.Equals(raiz, "NQ", StringComparison.OrdinalIgnoreCase)) return 20.0 / 100.0;
            if (string.Equals(raiz, "NDX", StringComparison.OrdinalIgnoreCase) || string.Equals(raiz, "QQQ", StringComparison.OrdinalIgnoreCase)) return 1.0;
            return double.NaN;
        }

        /// <summary>El monto de UNA dominante de la 3.0 ("gm"): gex (USD por 1 %, x100, con signo, tal como queda en Lectura.Doms[i].Gex) / 1e6 x
        /// FactorMonto3, SOLO si la dominante es un strike con monto. Todas las reglas que ponen un monto real lo COPIAN de un strike del perfil
        /// (nucleo/centroide: x.GexVol|GexOi del strike elegido; histeresis: s.GexVol|GexOi; tunel por strikes: GexEn; formula: s.GexVol|GexOi),
        /// asi que se exige que el valor sea EXACTAMENTE el GexVol o el GexOi de algun strike del perfil de la misma lectura. NaN si: no es finito,
        /// es 0 (vigente de la histeresis sin strike en el perfil, o GexEn/Gex de la formula sin strike), es el relleno +-1e6 del tunel por cruces,
        /// no esta en el perfil, o la raiz no tiene unidad de familia.</summary>
        public static double MontoDominante3(string raiz, double gex, IReadOnlyList<double> gexVolPerfil, IReadOnlyList<double> gexOiPerfil)
        {
            if (double.IsNaN(gex) || double.IsInfinity(gex) || gex == 0 || Math.Abs(gex) == RellenoTunel3) return double.NaN;
            double f = FactorMonto3(raiz);
            if (double.IsNaN(f)) return double.NaN;
            bool deStrike = false;
            if (gexVolPerfil != null) for (int i = 0; i < gexVolPerfil.Count && !deStrike; i++) deStrike = gexVolPerfil[i] == gex;
            if (gexOiPerfil != null) for (int i = 0; i < gexOiPerfil.Count && !deStrike; i++) deStrike = gexOiPerfil[i] == gex;
            return deStrike ? gex / 1e6 * f : double.NaN;
        }

        /// <summary>Los dos "gm" de una lectura de la 3.0, alineados con d[0]/d[1] de GuardarEstela: doms = Lectura.Doms (Fut, Gex); una dominante
        /// que GuardarEstela escribe como 0 (Fut NaN o &lt;= 0) o que no existe no tiene monto. gexVol/gexOi = Lectura.Perfil[*].GexVol/GexOi.</summary>
        public static (double M1, double M2) MontosDominantes3(string raiz, IReadOnlyList<(double Fut, double Gex)> doms, IReadOnlyList<double> gexVolPerfil, IReadOnlyList<double> gexOiPerfil)
        {
            double M(int i) => doms != null && doms.Count > i && !double.IsNaN(doms[i].Fut) && doms[i].Fut > 0 ? MontoDominante3(raiz, doms[i].Gex, gexVolPerfil, gexOiPerfil) : double.NaN;
            return (M(0), M(1));
        }

        /// <summary>El campo nuevo de la linea de estela, listo para pegar antes de la llave final: ,"gm":[m1,m2] con 1 decimal (como los montos
        /// de la familia) o null (NaN). SIEMPRE dos elementos, alineados con d[0] y d[1].</summary>
        public static string CampoGm(double gm1, double gm2)
        {
            string F(double x) => double.IsNaN(x) || double.IsInfinity(x) ? "null" : x.ToString("0.0", CultureInfo.InvariantCulture);
            return ",\"gm\":[" + F(gm1) + "," + F(gm2) + "]";
        }

        /// <summary>extremos_rebote.niveles_estela para un minuto: la ultima linea con t &lt;= minuto, vigente si minuto - t &lt; vida;
        /// niveles [(d1,"D1"), (d2,"D2")] sin los NaN; null si no hay. Solo minutos de [ini, fin) de la sesion (como la vista previa).
        /// lineaUtc = el t de la linea usada (la edad de TRES_NQ). 4.1.2: Nivel.GexM = el "gm" de esa misma linea (NaN si no tiene).</summary>
        public Nivel[] Niveles(string capa, long clave, out DateTime lineaUtc)
        {
            lineaUtc = default;
            long tn = SesionFamilia.DeClave(clave).Ticks;              // la clave es ms UTC desde 1970 / 60000; Ticks cuenta desde el año 1
            if (tn < _ini.Ticks || tn >= _fin.Ticks) return null;
            lock (_llave)
            {
                if (!_capas.TryGetValue(capa, out var c) || c.T.Count == 0) return null;
                int k = c.T.BinarySearch(tn);
                if (k < 0) k = ~k - 1;
                if (k < 0) return null;
                long vida = (VidaS.TryGetValue(capa, out var v) ? v : 300) * TimeSpan.TicksPerSecond;
                if (tn - c.T[k] >= vida) return null;
                double a = c.D1[k], b = c.D2[k];
                int n = (double.IsNaN(a) ? 0 : 1) + (double.IsNaN(b) ? 0 : 1);
                if (n == 0) return null;
                var r = new Nivel[n]; int q = 0;
                // 4.1.2: el monto de la linea ("gm", ya en unidades de la familia; NaN si no hay), round 1 como Nivel.GexM
                if (!double.IsNaN(a)) r[q++] = new Nivel(NumPy.RoundPy(a, 2), "D1", NumPy.RoundPy(c.G1[k], 1));
                if (!double.IsNaN(b)) r[q++] = new Nivel(NumPy.RoundPy(b, 2), "D2", NumPy.RoundPy(c.G2[k], 1));
                lineaUtc = new DateTime(c.T[k], DateTimeKind.Utc);
                return r;
            }
        }
    }
}
