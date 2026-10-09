// FotoNq.cs — PythiaGex 4.1, modulo Familia, libro NQ (B3a, 08-10-2026).
// La foto del libro NQ (Rithmic, CadenaApi del clon de la 3.0) como la arma la vista previa (preview_niveles.foto_viva3):
//   * filas crudas = las de Viva3.Json (ConPuntas && IV > 0), con los formatos de texto de viva3: K "0.##", dias "0.#####", oi "0.#",
//     iv "0.######", vol "0.#" (FilaViva.ComoViva3 hace ese ida y vuelta para que la foto en memoria sea IGUAL a la archivada).
//   * dias = sorted(set(round(dias, 4))) sobre TODAS las filas crudas (tambien las que despues se descartan: cuentan para el horizonte).
//   * se descarta la fila si iv <= 0 o (oi <= 0 y vol <= 0); una fila por (K, indice de dias) en el orden de la PRIMERA aparicion:
//     el call pisa oi_c/iv_c/vol_c y el put oi_p/iv_p/vol_p.
//   * generado = ts (segundos enteros), es_futuro = true, Spot = FuturoFoto = futuro. Sin futuro (<= 0) o sin filas crudas: null.
//     Si todas las filas se descartan, la foto EXISTE con Filas vacias (en Python tapa a la anterior y el minuto queda sin libro).
// Tambien lee una linea de viva3 (arnes y rebobinado), aceptando NaN/Infinity como json.loads de Python.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCuatro.Familia
{
    /// <summary>Una fila cruda del libro vivo de NQ (una opcion: call o put).</summary>
    public readonly struct FilaViva
    {
        public readonly double K, Dias, Oi, Iv, Vol; public readonly bool EsCall;
        public FilaViva(double k, double dias, bool esCall, double oi, double iv, double vol) { K = k; Dias = dias; EsCall = esCall; Oi = oi; Iv = iv; Vol = vol; }

        /// <summary>La misma fila pasada por el texto de viva3 (Viva3.Json): asi la foto armada en vivo es identica a la archivada.</summary>
        public static FilaViva ComoViva3(double k, double dias, bool esCall, double oi, double iv, double vol)
        {
            var inv = CultureInfo.InvariantCulture;
            double P(double v, string fmt) => double.Parse(v.ToString(fmt, inv), NumberStyles.Float, inv);
            return new FilaViva(P(k, "0.##"), P(dias, "0.#####"), esCall, P(oi, "0.#"), P(iv, "0.######"), P(vol, "0.#"));
        }
    }

    public static class FotoNq
    {
        /// <summary>preview_niveles.foto_viva3 sin el parseo: ts (se trunca al segundo), futuro y filas crudas -> FotoCadena "NQ".</summary>
        public static FotoCadena Armar(DateTime tsUtc, double futuro, IReadOnlyList<FilaViva> filas)
        {
            if (!(futuro > 0) || filas == null || filas.Count == 0) return null;
            var ts = new DateTime(tsUtc.Ticks - tsUtc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
            // dias = sorted(set(round(dias, 4))) de TODAS las filas crudas
            var set = new HashSet<double>();
            var dr = new double[filas.Count];
            for (int i = 0; i < filas.Count; i++) { dr[i] = NumFam.PyRound(filas[i].Dias, 4); set.Add(dr[i]); }
            var dias = new List<double>(set);
            dias.Sort();
            var idxDia = new Dictionary<double, int>(dias.Count);
            for (int i = 0; i < dias.Count; i++) idxDia[dias[i]] = i;
            // por (K, idx): [K, idx, oi_c, oi_p, iv_c, iv_p, vol_c, vol_p] en orden de primera aparicion
            var orden = new List<(double K, int V)>();
            var por = new Dictionary<(double, int), double[]>();
            for (int i = 0; i < filas.Count; i++)
            {
                var x = filas[i];
                if (x.Iv <= 0 || (x.Oi <= 0 && x.Vol <= 0)) continue;
                int v = idxDia[dr[i]];
                var key = (x.K, v);
                if (!por.TryGetValue(key, out var e)) { e = new double[6]; por[key] = e; orden.Add(key); }
                if (x.EsCall) { e[0] = x.Oi; e[2] = x.Iv; e[4] = x.Vol; }
                else { e[1] = x.Oi; e[3] = x.Iv; e[5] = x.Vol; }
            }
            var fs = new FilaCadena[orden.Count];
            double oiTot = 0;
            for (int i = 0; i < orden.Count; i++)
            {
                var e = por[orden[i]];
                fs[i] = new FilaCadena(orden[i].K, orden[i].V, e[0], e[1], e[2], e[3], e[4], e[5]);
            }
            for (int i = 0; i < fs.Length; i++) oiTot += fs[i].OiC;
            for (int i = 0; i < fs.Length; i++) oiTot += fs[i].OiP;
            return new FotoCadena
            {
                Libro = "NQ", GeneradoUtc = ts, TsUtc = ts, DatoUtc = ts, Spot = futuro, FuturoFoto = futuro,
                Dias = dias.ToArray(), Filas = fs, EsFuturo = true, OiTotal = oiTot,
            };
        }

        /// <summary>
        /// Una linea de viva3 -> foto, con el filtro de ventana [ini, fin] (ambos incluidos, como foto_viva3). null si la linea no sirve.
        /// nFilasCrudas = len(filas) de la linea (la vista previa deduplica por ts quedandose con la de MAS filas crudas).
        /// </summary>
        public static FotoCadena DesdeLineaViva3(string linea, DateTime iniUtc, DateTime finUtc, out int nFilasCrudas)
        {
            nFilasCrudas = 0;
            if (linea == null) return null;
            var l = linea.Trim();
            if (l.Length < 40 || !l.EndsWith("}", StringComparison.Ordinal)) return null;
            try
            {
                int p = l.IndexOf("\"ts\":\"", StringComparison.Ordinal);
                if (p < 0) return null;
                p += 6;
                int q = l.IndexOf('"', p);
                if (q < 0) return null;
                var ts = DateTime.SpecifyKind(DateTime.ParseExact(l.Substring(p, q - p), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), DateTimeKind.Utc);
                if (ts < iniUtc || ts > finUtc) return null;
                double fut = 0;
                int pf = l.IndexOf("\"futuro\":", StringComparison.Ordinal);
                if (pf >= 0)
                {
                    int i = pf + 9;
                    if (!LeerNumero(l, ref i, out fut)) fut = 0;      // null / texto: "or 0"
                }
                var filas = new List<FilaViva>();
                int pa = l.IndexOf("\"filas\":", StringComparison.Ordinal);
                if (pa >= 0)
                {
                    int i = pa + 8;
                    Saltar(l, ref i);
                    if (i < l.Length && l[i] == '[')
                    {
                        i++;
                        var vals = new List<double>(12);
                        while (true)
                        {
                            Saltar(l, ref i);
                            if (i >= l.Length) return null;
                            if (l[i] == ']') { i++; break; }
                            if (l[i] == ',') { i++; continue; }
                            if (l[i] != '[') return null;
                            i++; vals.Clear();
                            while (true)
                            {
                                Saltar(l, ref i);
                                if (i >= l.Length) return null;
                                if (l[i] == ']') { i++; break; }
                                if (l[i] == ',') { i++; continue; }
                                if (!LeerNumero(l, ref i, out double v)) return null;
                                vals.Add(v);
                            }
                            nFilasCrudas++;
                            if (vals.Count < 8) return null;                    // Python levantaria IndexError: la linea no sirve
                            filas.Add(new FilaViva(vals[0], vals[1], vals[2] >= 0.5, vals[3], vals[4], vals[7]));
                        }
                    }
                }
                return Armar(ts, fut, filas);
            }
            catch { return null; }
        }

        private static void Saltar(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++; }

        /// <summary>Un numero JSON (o NaN / Infinity / -Infinity, como json.loads). false si no hay numero (ej. null).</summary>
        private static bool LeerNumero(string s, ref int i, out double v)
        {
            v = double.NaN;
            Saltar(s, ref i);
            int a = i;
            if (string.CompareOrdinal(s, i, "NaN", 0, 3) == 0) { i += 3; v = double.NaN; return true; }
            if (string.CompareOrdinal(s, i, "Infinity", 0, 8) == 0) { i += 8; v = double.PositiveInfinity; return true; }
            if (string.CompareOrdinal(s, i, "-Infinity", 0, 9) == 0) { i += 9; v = double.NegativeInfinity; return true; }
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == a) return false;
            return double.TryParse(s.Substring(a, i - a), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}
