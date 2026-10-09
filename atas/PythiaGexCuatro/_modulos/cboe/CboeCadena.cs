// CboeCadena.cs — PythiaGex 4.1, modulo cboe (B2). Sin referencias a ATAS.
// Port EXACTO de:
//   pythiagex/cadena_atas.py  construir(crudo, ahora)            -> CboeCadena.Construir
//   pythiagex/base.py         medir(crudo)  (base por forwards)   -> CboeCadena.Medir
//   archivar_cadena.py        bajar_y_archivar + linea_flaca      -> CboeCadena.LineaFlaca (texto igual al de json.dumps)
//   preview_niveles.py        foto_cboe_de_json / libros.fotos_cboe -> CboeCadena.DeLinea (el MISMO lector para el vivo y el archivo)
// Diferencias declaradas:
//   * base / base_confiable: la confianza necesita la curva del Tesoro (descarga externa): se escriben null / false (DISENO_4_1 C.2).
//     base_cruda y base_error_ticks (lo que usan la 3.0 y la Familia) salen igual que en Python.
//   * Si medir no da (falta el trimestral, < 3 forwards), Python revienta bajar_y_archivar y NO archiva la cadena; aca se archiva
//     con base_cruda = null.
//   * Un simbolo OCC con fecha imposible se descarta (Python revienta construir entero).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using PythiaGexCuatro.Familia;

namespace PythiaGexCuatro.Cboe
{
    /// <summary>Una fila de construir con el tipo que tendria en Python (int o float) para escribirla igual.</summary>
    public struct FilaCboe
    {
        public double K; public int V;
        public double OiC, OiP, IvC, IvP, VolC, VolP;
        public byte Ent;   // bits: 1 OiC int, 2 OiP int, 4 IvC int, 8 IvP int, 16 VolC int, 32 VolP int
    }

    /// <summary>La salida de construir (la cadena comprimida).</summary>
    public sealed class CadenaCboe
    {
        public NumCboe SpotIdx;
        public string Ts;
        public string UltimoTrade = "";
        public int HorizonteDias = 14;
        public string[] VencF = Array.Empty<string>();
        public double[] VencDias = Array.Empty<double>();
        public FilaCboe[] Filas = Array.Empty<FilaCboe>();   // todas las de construir (<= 14 dias)

        /// <summary>archivar_cadena.linea_flaca: solo filas con 0 &lt;= dias[v] &lt;= diasMax, sin reindexar los vencimientos.</summary>
        public FilaCboe[] FilasFlacas(double diasMax)
        {
            var l = new List<FilaCboe>(Filas.Length);
            foreach (var f in Filas)
            {
                if (f.V < 0 || f.V >= VencDias.Length) continue;
                double d = VencDias[f.V];
                if (0 <= d && d <= diasMax) l.Add(f);
            }
            return l.ToArray();
        }
    }

    /// <summary>La salida de medir (solo lo que se usa: base_cruda y el residuo; mas la curva para diagnostico).</summary>
    public sealed class BaseCboe
    {
        public double Base = double.NaN;            // base_cruda = round(fwd_trimestral - contado, 2)
        public double Contado = double.NaN;         // round(a, 2)
        public double Forward = double.NaN;         // forward del trimestral
        public double Residuo = double.NaN;         // max |residuo| (puntos)
        public double ResiduoTicks = double.NaN;    // round(residuo / tick, 1)
        public double Pendiente = double.NaN, R2 = double.NaN;
        public string Venc = "", Cercano = "";
        public int Puntos, MuestrasFut;
        public double DispersionFut = double.NaN;
        public List<(string F, int Dias, double Fwd, int Muestras, double Disp)> Curva = new List<(string, int, double, int, double)>();
    }

    /// <summary>Lo que trae data.* (el subyacente) en la linea propia.</summary>
    public sealed class SubCboe
    {
        public double CurrentPrice = double.NaN, Close = double.NaN, PrevDayClose = double.NaN, PriceChange = double.NaN,
                      Bid = double.NaN, Ask = double.NaN, Open = double.NaN;
        public string LastTradeTime;   // NY sin zona
    }

    /// <summary>Datos de la bajada que se anotan en la linea (bloque "bajada").</summary>
    public sealed class InfoBajada
    {
        public string Url = ""; public int Http; public long BytesGz, Bytes, Ms;
    }

    /// <summary>Una linea flaca leida (cboe-local o la propia).</summary>
    public sealed class LineaCboe
    {
        public DateTime GeneradoUtc;
        public string CadenaTs = "";
        public DateTime TsUtc;
        public string UltimoTrade;
        public DateTime DatoUtc;
        public double Spot = double.NaN;          // float(spot_idx or spot or 0) (0 -> NaN)
        public double BaseCruda = double.NaN, BaseErr = double.NaN;
        public double[] Dias = Array.Empty<double>();
        public FilaCadena[] Filas;                // null si se leyo sin filas
        public int NFilas;
        public double OiTotal;
        public SubCboe Sub;                       // null en las lineas de cboe-local (no traen "sub")

        public FotoCadena AFoto(string libro)
        {
            return new FotoCadena
            {
                Libro = libro, GeneradoUtc = GeneradoUtc, TsUtc = TsUtc, DatoUtc = DatoUtc, Spot = Spot, Dias = Dias,
                Filas = Filas ?? Array.Empty<FilaCadena>(), EsFuturo = false, OiTotal = OiTotal, BaseCruda = BaseCruda, BaseErrorTicks = BaseErr,
                CierreAnterior = Sub != null ? Sub.PrevDayClose : double.NaN, FuturoFoto = double.NaN,
            };
        }
    }

    public static class CboeCadena
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public const string Campos = "strike,venc,oi_call,oi_put,iv_call,iv_put,vol_call,vol_put";
        public const double Ancho = 0.05;        // cadena_atas.ANCHO
        public const int DiasMax = 14;           // cadena_atas.DIAS_MAX
        public const double DiasMaxArchivo = 8.0; // archivar_cadena.DIAS_MAX
        public const int RetrasoS = 902;         // libros.RETRASO_S: t_dato de respaldo = ts - 902 s

        // ================================================================== construir

        private sealed class Acum
        {
            public double K; public int Fecha;
            public double Oc, Op, Vc, Vp, Ic, Ip;
            public bool OcE = true, OpE = true, VcE = true, VpE = true, IcE, IpE;   // int 0 al empezar; ic/ip = 0.0 float
        }

        /// <summary>cadena_atas.construir(crudo, ahora). ahoraUtc conviene truncado al microsegundo (como datetime de Python).</summary>
        public static CadenaCboe Construir(CrudoCboe c, DateTime ahoraUtc, double ancho = Ancho, int diasMax = DiasMax)
        {
            double S = c.CurrentPrice.V;
            var cad = new CadenaCboe { Ts = c.Timestamp, UltimoTrade = c.UltimoTrade ?? "", HorizonteDias = diasMax };
            cad.SpotIdx = new NumCboe { Hay = c.CurrentPrice.Hay, Entero = c.CurrentPrice.Entero, V = c.CurrentPrice.Entero ? S : PyCboe.Round(S, 2) };
            if (!(S > 0) || double.IsNaN(S)) return cad;
            long ahora = ahoraUtc.Ticks;
            var mapa = new Dictionary<(double, int), Acum>();
            var vencDias = new Dictionary<int, double>();
            var vencOrden = new List<int>();
            var diasPorFecha = new Dictionary<int, double>();
            for (int i = 0; i < c.N; i++)
            {
                int fe = c.Fecha[i];
                if (fe == 0) continue;
                if (!diasPorFecha.TryGetValue(fe, out double dias))
                {
                    dias = (CrudoCboe.VencUtc(fe).Ticks - ahora) / 1e7 / 86400.0;   // total_seconds()/86400.0
                    diasPorFecha[fe] = dias;
                }
                if (dias < 0 || dias > diasMax) continue;
                double K = c.K[i];
                if (Math.Abs(K - S) / S > ancho) continue;
                double oi = c.Oi[i], vol = c.Vol[i];
                if (oi == 0 && vol == 0) continue;               // "if not oi and not vol"
                double iv = c.Iv[i];
                if (iv <= 0) continue;
                if (!vencDias.ContainsKey(fe)) { vencDias[fe] = PyCboe.Round(dias, 4); vencOrden.Add(fe); }
                if (!mapa.TryGetValue((K, fe), out var e)) { e = new Acum { K = K, Fecha = fe }; mapa[(K, fe)] = e; }
                double ivr = c.IvEntero[i] ? iv : PyCboe.Round(iv, 4);   // round(int, 4) devuelve el int
                if (c.EsCall[i])
                {
                    e.Oc += oi; e.OcE &= c.OiEntero[i] || oi == 0;   // "or 0" -> int 0 no cambia el tipo
                    e.Vc += vol; e.VcE &= c.VolEntero[i] || vol == 0;
                    e.Ic = ivr; e.IcE = c.IvEntero[i];
                }
                else
                {
                    e.Op += oi; e.OpE &= c.OiEntero[i] || oi == 0;
                    e.Vp += vol; e.VpE &= c.VolEntero[i] || vol == 0;
                    e.Ip = ivr; e.IpE = c.IvEntero[i];
                }
            }
            // orden = sorted(vencs.values(), key=dias)  (estable: empates por orden de aparicion)
            var orden = new List<int>(vencOrden);
            var pos = new Dictionary<int, int>();
            for (int j = 0; j < vencOrden.Count; j++) pos[vencOrden[j]] = j;
            orden.Sort((a, b) => { int r = vencDias[a].CompareTo(vencDias[b]); return r != 0 ? r : pos[a].CompareTo(pos[b]); });
            var idx = new Dictionary<int, int>();
            cad.VencF = new string[orden.Count]; cad.VencDias = new double[orden.Count];
            for (int j = 0; j < orden.Count; j++) { idx[orden[j]] = j; cad.VencF[j] = CrudoCboe.FechaIso(orden[j]); cad.VencDias[j] = vencDias[orden[j]]; }
            // sorted(filas.items()): por (K, fecha iso) — la fecha iso ordena igual que yyyymmdd
            var lista = new List<Acum>(mapa.Values);
            lista.Sort((a, b) => { int r = a.K.CompareTo(b.K); return r != 0 ? r : a.Fecha.CompareTo(b.Fecha); });
            var filas = new List<FilaCboe>(lista.Count);
            foreach (var e in lista)
            {
                if (e.Oc == 0 && e.Op == 0 && e.Vc == 0 && e.Vp == 0) continue;
                byte ent = 0;
                if (e.OcE) ent |= 1; if (e.OpE) ent |= 2; if (e.IcE) ent |= 4; if (e.IpE) ent |= 8; if (e.VcE) ent |= 16; if (e.VpE) ent |= 32;
                filas.Add(new FilaCboe { K = e.K, V = idx[e.Fecha], OiC = e.Oc, OiP = e.Op, IvC = e.Ic, IvP = e.Ip, VolC = e.Vc, VolP = e.Vp, Ent = ent });
            }
            cad.Filas = filas.ToArray();
            return cad;
        }

        // ================================================================== medir (base por forwards)

        public static DateTime TercerViernes(int anio, int mes)
        {
            var d = new DateTime(anio, mes, 1);
            int hasta = ((int)DayOfWeek.Friday - (int)d.DayOfWeek + 7) % 7;
            return d.AddDays(hasta + 14);
        }

        /// <summary>base.contrato_vigente: el trimestral cuyo roll (tercer viernes - 8 dias) todavia no llego.</summary>
        public static DateTime ContratoVigente(DateTime hoy)
        {
            hoy = hoy.Date;
            foreach (int anio in new[] { hoy.Year, hoy.Year + 1 })
                foreach (int mes in new[] { 3, 6, 9, 12 })
                {
                    var tv = TercerViernes(anio, mes);
                    if (hoy < tv.AddDays(-8)) return tv;
                }
            return DateTime.MinValue;
        }

        private sealed class Lado { public double MidC = double.NaN, LastC = double.NaN, MidP = double.NaN, LastP = double.NaN; }

        /// <summary>base.medir(crudo): recta por minimos cuadrados sobre los forwards de los vencimientos hasta el trimestral.
        /// hoyLocal = la fecha LOCAL de la PC (Python usa dt.date.today()). null si no se puede medir.</summary>
        public static BaseCboe Medir(CrudoCboe c, DateTime hoyLocal, int n = 12, int maxVencimientos = 10, double tick = 0.25)
        {
            double S = c.CurrentPrice.V;
            if (double.IsNaN(S)) return null;
            var vt = ContratoVigente(hoyLocal);
            if (vt == DateTime.MinValue) return null;
            int venc = vt.Year * 10000 + vt.Month * 100 + vt.Day;
            var set = new SortedSet<int>();
            for (int i = 0; i < c.N; i++) if (c.Fecha[i] != 0) set.Add(c.Fecha[i]);
            if (!set.Contains(venc)) return null;
            var vencs = new List<int>(set);
            int cercano = vencs[0];
            var hasta = new List<int>();
            foreach (var v in vencs) { if (v <= venc) { if (hasta.Count < maxVencimientos) hasta.Add(v); } }
            if (!hasta.Contains(venc)) hasta.Add(venc);
            // _porK de cada vencimiento: dict K -> {C,P} -> {mid, last}; el orden de K es el de primera aparicion; un (K, lado) repetido pisa
            var porFecha = new Dictionary<int, (List<double> Orden, Dictionary<double, Lado> Mapa)>();
            foreach (var v in hasta) porFecha[v] = (new List<double>(), new Dictionary<double, Lado>());
            for (int i = 0; i < c.N; i++)
            {
                int fe = c.Fecha[i];
                if (fe == 0 || !porFecha.TryGetValue(fe, out var pk)) continue;
                double K = c.K[i];
                if (!pk.Mapa.TryGetValue(K, out var lado)) { lado = new Lado(); pk.Mapa[K] = lado; pk.Orden.Add(K); }
                double b = c.BidO[i], a = c.AskO[i];
                double mid = (double.IsNaN(b) || double.IsNaN(a) || a == 0) ? double.NaN : (b + a) / 2.0;   // "if b is None or not a"
                double last = c.LastPx[i];
                if (last == 0) last = double.NaN;                                                         // "v if v else None"
                if (c.EsCall[i]) { lado.MidC = mid; lado.LastC = last; } else { lado.MidP = mid; lado.LastP = last; }
            }
            var res = new BaseCboe { Venc = CrudoCboe.FechaIso(venc), Cercano = CrudoCboe.FechaIso(cercano) };
            var puntos = new List<(int D, double F)>();
            foreach (var v in hasta)
            {
                var pk = porFecha[v];
                if (pk.Orden.Count == 0) continue;
                var f = Forward(pk.Orden, pk.Mapa, S, true, n) ?? Forward(pk.Orden, pk.Mapa, S, false, n);
                if (f == null) continue;
                int dias = (int)(CrudoCboe.FechaDe(v) - CrudoCboe.FechaDe(cercano)).TotalDays;
                puntos.Add((dias, f.Value.Fwd));
                res.Curva.Add((CrudoCboe.FechaIso(v), dias, f.Value.Fwd, f.Value.M, f.Value.Disp));
            }
            if (puntos.Count < 3) return null;
            var aj = Ajuste(puntos);
            int iFut = res.Curva.FindIndex(x => x.F == res.Venc);
            if (iFut < 0) return null;
            var fFut = res.Curva[iFut];
            var fCer = res.Curva[0];
            double contado = aj != null ? PyCboe.Round(aj.Value.A, 2) : fCer.Fwd;
            res.Contado = contado;
            res.Forward = fFut.Fwd;
            res.Base = PyCboe.Round(fFut.Fwd - contado, 2);
            res.Residuo = aj != null ? aj.Value.Res : 9e9;
            res.ResiduoTicks = PyCboe.Round(res.Residuo / tick, 1);
            res.Pendiente = aj != null ? aj.Value.B : double.NaN;
            res.R2 = aj != null ? aj.Value.R2 : 0.0;
            res.Puntos = puntos.Count; res.MuestrasFut = fFut.Muestras; res.DispersionFut = fFut.Disp;
            return res;
        }

        private static (double Fwd, int M, double Disp)? Forward(List<double> orden, Dictionary<double, Lado> mapa, double S, bool mid, int n)
        {
            // sorted(porK.keys(), key=lambda k: abs(k - S))[:n]  (estable)
            var ix = new int[orden.Count];
            for (int i = 0; i < ix.Length; i++) ix[i] = i;
            Array.Sort(ix, (a, b) => { int r = Math.Abs(orden[a] - S).CompareTo(Math.Abs(orden[b] - S)); return r != 0 ? r : a.CompareTo(b); });
            var fwd = new List<double>(n);
            for (int j = 0; j < Math.Min(n, ix.Length); j++)
            {
                double k = orden[ix[j]];
                var e = mapa[k];
                double cc = mid ? e.MidC : e.LastC, pp = mid ? e.MidP : e.LastP;
                if (double.IsNaN(cc) || double.IsNaN(pp)) continue;
                fwd.Add(k + cc - pp);
            }
            if (fwd.Count < 3) return null;
            double s = 0, mx = double.NegativeInfinity, mn = double.PositiveInfinity;
            foreach (var x in fwd) { s += x; if (x > mx) mx = x; if (x < mn) mn = x; }
            return (PyCboe.Round(s / fwd.Count, 2), fwd.Count, PyCboe.Round(mx - mn, 2));
        }

        private static (double A, double B, double R2, double Res)? Ajuste(List<(int D, double F)> p)
        {
            int n = p.Count;
            if (n < 3) return null;
            long sx = 0, sxx = 0; double sy = 0, sxy = 0;
            foreach (var q in p) sx += q.D;
            foreach (var q in p) sy += q.F;
            foreach (var q in p) sxx += (long)q.D * q.D;
            foreach (var q in p) sxy += q.D * q.F;
            long den = n * sxx - sx * sx;                       // entero exacto, como en Python
            if (Math.Abs((double)den) < 1e-9) return null;
            double b = (n * sxy - sx * sy) / den;
            double a = (sy - b * sx) / n;
            double ym = sy / n;
            double sst = 0, sse = 0;
            foreach (var q in p) sst += (q.F - ym) * (q.F - ym);
            foreach (var q in p) { double r = q.F - (a + b * q.D); sse += r * r; }
            double r2 = sst > 1e-12 ? 1.0 - (sse / sst) : 0.0;
            double res = double.NegativeInfinity;
            foreach (var q in p) { double r = Math.Abs(q.F - (a + b * q.D)); if (r > res) res = r; }
            return (a, b, r2, res);
        }

        // ================================================================== la linea flaca (texto igual al de json.dumps)

        /// <summary>isoformat(timespec="seconds") de un datetime aware UTC.</summary>
        public static string IsoGenerado(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss", Inv) + "+00:00";

        public static bool ParsearTs(string ts, out DateTime utc)
        {
            bool ok = DateTime.TryParseExact(ts ?? "", "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);
            if (ok) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return ok;
        }

        /// <summary>La linea de archivar_cadena.bajar_y_archivar pasada por linea_flaca, con dos bloques nuevos: "sub" (data.* del
        /// subyacente) y "bajada" (url, http, bytes). Las claves de Python salen en el mismo orden y con el mismo texto.</summary>
        public static string LineaFlaca(CadenaCboe cad, CrudoCboe cr, BaseCboe b, DateTime generadoUtc, InfoBajada baj, double diasMax = DiasMaxArchivo)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\"generado\":"); PyCboe.Str(sb, IsoGenerado(generadoUtc));
            sb.Append(",\"cadena_ts\":"); PyCboe.Str(sb, cr.Timestamp);
            sb.Append(",\"edad_min\":");
            if (ParsearTs(cr.Timestamp, out var tsu)) sb.Append(PyCboe.Repr(PyCboe.Round((generadoUtc.Ticks - tsu.Ticks) / 1e7 / 60.0, 1)));
            else sb.Append("null");
            sb.Append(",\"spot\":").Append(cr.CurrentPrice.ToString());
            sb.Append(",\"base\":null,\"base_confiable\":false");
            sb.Append(",\"base_cruda\":").Append(b != null && !double.IsNaN(b.Base) ? PyCboe.Repr(b.Base) : "null");
            sb.Append(",\"base_error_ticks\":").Append(b != null && !double.IsNaN(b.ResiduoTicks) ? PyCboe.Repr(b.ResiduoTicks) : "null");
            sb.Append(",\"sub\":{\"current_price\":").Append(cr.CurrentPrice.ToString());
            sb.Append(",\"close\":").Append(cr.Close.ToString());
            sb.Append(",\"prev_day_close\":").Append(cr.PrevDayClose.ToString());
            sb.Append(",\"price_change\":").Append(cr.PriceChange.ToString());
            sb.Append(",\"last_trade_time\":"); PyCboe.Str(sb, cr.LastTradeTime);
            sb.Append(",\"bid\":").Append(cr.Bid.ToString());
            sb.Append(",\"ask\":").Append(cr.Ask.ToString());
            sb.Append(",\"open\":").Append(cr.Open.ToString());
            sb.Append('}');
            if (baj != null)
            {
                sb.Append(",\"bajada\":{\"url\":"); PyCboe.Str(sb, baj.Url);
                sb.Append(",\"http\":").Append(baj.Http.ToString(Inv));
                sb.Append(",\"bytes_gz\":").Append(baj.BytesGz.ToString(Inv));
                sb.Append(",\"bytes\":").Append(baj.Bytes.ToString(Inv));
                sb.Append(",\"ms\":").Append(baj.Ms.ToString(Inv));
                sb.Append('}');
            }
            sb.Append(",\"cadena\":");
            EscribirCadena(sb, cad, diasMax);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>El objeto "cadena" de linea_flaca: ts, spot_idx, ultimo_trade, horizonte_dias, vencimientos, campos, filas, dias_max_archivo.</summary>
        public static void EscribirCadena(StringBuilder sb, CadenaCboe cad, double diasMax)
        {
            sb.Append("{\"ts\":"); PyCboe.Str(sb, cad.Ts);
            sb.Append(",\"spot_idx\":").Append(cad.SpotIdx.ToString());
            sb.Append(",\"ultimo_trade\":"); PyCboe.Str(sb, cad.UltimoTrade ?? "");
            sb.Append(",\"horizonte_dias\":").Append(cad.HorizonteDias.ToString(Inv));
            sb.Append(",\"vencimientos\":[");
            for (int j = 0; j < cad.VencF.Length; j++)
            {
                if (j > 0) sb.Append(',');
                sb.Append("{\"f\":"); PyCboe.Str(sb, cad.VencF[j]); sb.Append(",\"dias\":").Append(PyCboe.Repr(cad.VencDias[j])).Append('}');
            }
            sb.Append("],\"campos\":"); PyCboe.Str(sb, Campos);
            sb.Append(",\"filas\":[");
            bool primera = true;
            foreach (var f in cad.FilasFlacas(diasMax))
            {
                if (!primera) sb.Append(',');
                primera = false;
                sb.Append('[').Append(PyCboe.Repr(f.K)).Append(',').Append(f.V.ToString(Inv))
                  .Append(',').Append(PyCboe.Num(f.OiC, (f.Ent & 1) != 0)).Append(',').Append(PyCboe.Num(f.OiP, (f.Ent & 2) != 0))
                  .Append(',').Append(PyCboe.Num(f.IvC, (f.Ent & 4) != 0)).Append(',').Append(PyCboe.Num(f.IvP, (f.Ent & 8) != 0))
                  .Append(',').Append(PyCboe.Num(f.VolC, (f.Ent & 16) != 0)).Append(',').Append(PyCboe.Num(f.VolP, (f.Ent & 32) != 0)).Append(']');
            }
            sb.Append("],\"dias_max_archivo\":").Append(PyCboe.Repr(diasMax)).Append('}');
        }

        // ================================================================== lectura de una linea (port de foto_cboe_de_json)

        /// <summary>Lee una linea flaca. conFilas=false: no arma las filas (solo las cuenta y suma el OI): para la historia vieja.
        /// null si no se puede leer o no trae filas (como foto_cboe_de_json).</summary>
        public static LineaCboe DeLinea(ReadOnlySpan<byte> utf8, Func<DateTime, bool> conFilas = null)
        {
            try { return DeLineaAdentro(utf8, conFilas); }
            catch (JsonException) { return null; }
            catch (FormatException) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        public static LineaCboe DeLinea(string texto, Func<DateTime, bool> conFilas = null) => DeLinea(Encoding.UTF8.GetBytes(texto ?? ""), conFilas);

        private static LineaCboe DeLineaAdentro(ReadOnlySpan<byte> utf8, Func<DateTime, bool> conFilas)
        {
            var rd = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = 64 });
            if (!rd.Read() || rd.TokenType != JsonTokenType.StartObject) return null;
            var L = new LineaCboe();
            string gen = null, ts = null, ut = null;
            bool hayGen = false, hayTs = false;
            double spotRaiz = double.NaN, spotIdx = double.NaN, retrasoS = double.NaN;
            var dias = new List<double>();
            List<FilaCadena> filas = null;
            int nFilas = 0; double oiC = 0, oiP = 0;
            bool? quiereFilas = null;
            var v = new double[8];
            while (rd.Read())
            {
                if (rd.TokenType == JsonTokenType.EndObject) break;
                if (rd.TokenType != JsonTokenType.PropertyName) continue;
                if (rd.ValueTextEquals("generado")) { rd.Read(); if (rd.TokenType == JsonTokenType.String) { gen = rd.GetString(); hayGen = true; } }
                else if (rd.ValueTextEquals("cadena_ts")) { rd.Read(); if (rd.TokenType == JsonTokenType.String) { ts = rd.GetString(); hayTs = true; } else if (rd.TokenType != JsonTokenType.Null) { ts = TextoDe(ref rd); hayTs = ts != null; } }
                else if (rd.ValueTextEquals("spot")) { rd.Read(); spotRaiz = NumODe(ref rd); }
                else if (rd.ValueTextEquals("retraso_s")) { rd.Read(); retrasoS = NumODe(ref rd); }
                else if (rd.ValueTextEquals("base_cruda")) { rd.Read(); L.BaseCruda = NumODe(ref rd); }
                else if (rd.ValueTextEquals("base_error_ticks")) { rd.Read(); L.BaseErr = NumODe(ref rd); }
                else if (rd.ValueTextEquals("sub")) { rd.Read(); if (rd.TokenType == JsonTokenType.StartObject) L.Sub = LeerSub(ref rd); else rd.Skip(); }
                else if (rd.ValueTextEquals("cadena"))
                {
                    rd.Read();
                    if (rd.TokenType != JsonTokenType.StartObject) { rd.Skip(); continue; }
                    while (rd.Read())
                    {
                        if (rd.TokenType == JsonTokenType.EndObject) break;
                        if (rd.TokenType != JsonTokenType.PropertyName) continue;
                        if (rd.ValueTextEquals("spot_idx")) { rd.Read(); spotIdx = NumODe(ref rd); }
                        else if (rd.ValueTextEquals("ultimo_trade")) { rd.Read(); ut = rd.TokenType == JsonTokenType.String ? rd.GetString() : null; }
                        else if (rd.ValueTextEquals("vencimientos"))
                        {
                            rd.Read();
                            if (rd.TokenType != JsonTokenType.StartArray) { rd.Skip(); continue; }
                            while (rd.Read() && rd.TokenType != JsonTokenType.EndArray)
                            {
                                if (rd.TokenType != JsonTokenType.StartObject) { rd.Skip(); continue; }
                                double d = 0;
                                while (rd.Read() && rd.TokenType != JsonTokenType.EndObject)
                                {
                                    if (rd.TokenType != JsonTokenType.PropertyName) continue;
                                    if (rd.ValueTextEquals("dias")) { rd.Read(); double x = NumODe(ref rd); d = double.IsNaN(x) ? 0 : x; }   // float(v.get("dias") or 0)
                                    else { rd.Read(); rd.Skip(); }
                                }
                                dias.Add(d);
                            }
                        }
                        else if (rd.ValueTextEquals("filas"))
                        {
                            rd.Read();
                            if (rd.TokenType != JsonTokenType.StartArray) { rd.Skip(); continue; }
                            if (quiereFilas == null)
                            {
                                DateTime g0 = default;
                                bool gOk = hayGen && ParsearGenerado(gen, out g0);
                                quiereFilas = conFilas == null || !gOk || conFilas(g0);
                            }
                            if (quiereFilas.Value) filas = new List<FilaCadena>(1024);
                            while (rd.Read() && rd.TokenType != JsonTokenType.EndArray)
                            {
                                if (rd.TokenType != JsonTokenType.StartArray) { rd.Skip(); continue; }
                                int k = 0;
                                while (rd.Read() && rd.TokenType != JsonTokenType.EndArray)
                                {
                                    double x;
                                    if (rd.TokenType == JsonTokenType.Number) x = rd.GetDouble();
                                    else { if (rd.TokenType == JsonTokenType.StartArray || rd.TokenType == JsonTokenType.StartObject) rd.Skip(); x = double.NaN; }
                                    if (k < 8) v[k] = x;
                                    k++;
                                }
                                if (k < 8) continue;                                        // x[:8] for x in filas if len(x) >= 8
                                nFilas++; oiC += v[2]; oiP += v[3];
                                if (filas != null) filas.Add(new FilaCadena(v[0], double.IsNaN(v[1]) ? -1 : (int)v[1], v[2], v[3], v[4], v[5], v[6], v[7]));
                            }
                        }
                        else { rd.Read(); rd.Skip(); }
                    }
                }
                else { rd.Read(); rd.Skip(); }
            }
            if (!hayGen || !hayTs) return null;
            if (!ParsearGenerado(gen, out var genUtc)) return null;
            if (nFilas == 0) return null;                                                       // "if not filas: return None"
            L.GeneradoUtc = genUtc;
            L.CadenaTs = ts;
            bool tsOk = ParsearTs(ts, out var tsUtc);
            L.TsUtc = tsOk ? tsUtc : genUtc;
            L.UltimoTrade = ut;
            // t_dato = _ny_a_utc(ut) if ut else strptime(ts) - (retraso_s or 902); ante un error, strptime(ts) - 902
            DateTime dato;
            if (!string.IsNullOrEmpty(ut))
            {
                if (!HoraNyCboe.TextoNyAUtc(ut, out dato)) dato = L.TsUtc.AddSeconds(-RetrasoS);
            }
            else
            {
                int r = (!double.IsNaN(retrasoS) && retrasoS != 0) ? (int)retrasoS : RetrasoS;
                dato = L.TsUtc.AddSeconds(-r);
            }
            L.DatoUtc = DateTime.SpecifyKind(dato, DateTimeKind.Utc);
            double sp = (!double.IsNaN(spotIdx) && spotIdx != 0) ? spotIdx : ((!double.IsNaN(spotRaiz) && spotRaiz != 0) ? spotRaiz : 0);
            L.Spot = sp == 0 ? double.NaN : sp;
            L.Dias = dias.ToArray();
            L.Filas = filas?.ToArray();
            L.NFilas = nFilas;
            L.OiTotal = oiC + oiP;
            return L;
        }

        /// <summary>libros._utc: ISO con zona -> UTC; sin zona se toma como UTC.</summary>
        public static bool ParsearGenerado(string s, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Replace("\\u002B", "+");
            if (!DateTime.TryParse(s, Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)) return false;
            utc = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            return true;
        }

        private static SubCboe LeerSub(ref Utf8JsonReader rd)
        {
            var s = new SubCboe();
            while (rd.Read())
            {
                if (rd.TokenType == JsonTokenType.EndObject) break;
                if (rd.TokenType != JsonTokenType.PropertyName) continue;
                if (rd.ValueTextEquals("current_price")) { rd.Read(); s.CurrentPrice = NumODe(ref rd); }
                else if (rd.ValueTextEquals("close")) { rd.Read(); s.Close = NumODe(ref rd); }
                else if (rd.ValueTextEquals("prev_day_close")) { rd.Read(); s.PrevDayClose = NumODe(ref rd); }
                else if (rd.ValueTextEquals("price_change")) { rd.Read(); s.PriceChange = NumODe(ref rd); }
                else if (rd.ValueTextEquals("bid")) { rd.Read(); s.Bid = NumODe(ref rd); }
                else if (rd.ValueTextEquals("ask")) { rd.Read(); s.Ask = NumODe(ref rd); }
                else if (rd.ValueTextEquals("open")) { rd.Read(); s.Open = NumODe(ref rd); }
                else if (rd.ValueTextEquals("last_trade_time")) { rd.Read(); s.LastTradeTime = rd.TokenType == JsonTokenType.String ? rd.GetString() : null; }
                else { rd.Read(); rd.Skip(); }
            }
            return s;
        }

        private static double NumODe(ref Utf8JsonReader rd)
        {
            if (rd.TokenType == JsonTokenType.Number) return rd.GetDouble();
            if (rd.TokenType == JsonTokenType.StartArray || rd.TokenType == JsonTokenType.StartObject) rd.Skip();
            return double.NaN;
        }

        private static string TextoDe(ref Utf8JsonReader rd)
        {
            if (rd.TokenType == JsonTokenType.Number) return Encoding.UTF8.GetString(rd.ValueSpan);
            if (rd.TokenType == JsonTokenType.StartArray || rd.TokenType == JsonTokenType.StartObject) rd.Skip();
            return null;
        }
    }
}
