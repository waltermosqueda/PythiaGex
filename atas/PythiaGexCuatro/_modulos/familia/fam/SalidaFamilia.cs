// SalidaFamilia.cs — PythiaGex 4.1, modulo fam (B3d), 08-10-2026.
// Lo que va a la pantalla, port de preview_niveles.Motor.salida / _actual / _meta_libro y de tqqq_vivo.ciclo (historia y actuales):
//   * historia por vela m2 (apertura T en ms UTC): niv[T/60000] o, si falta o esta vacio, niv[T/60000 - 1]; TRES solo de la clave T/60000
//     (como la vista previa); TQQQ de su vela (ICalculoTqqq.Vela).
//   * actuales: el ultimo minuto con libros (fuente y edad de cada libro; la familia con el dato MAS VIEJO de los libros que entran),
//     TRES buscado hasta 29 minutos atras (edad: NQ = la linea de su estela; NDX/QQQ = el t_dato de la cadena de CBOE de ese minuto;
//     4.1.2: GexM = el "gm" de la misma linea, NaN en las estelas viejas),
//     TQQQ de la ultima vela con niveles.
//   * 4.1.3: las series extra (RegistroMinuto.Extra: R20_*, DOMS_*) en la historia y en los actuales, despues de las 21 de la cuenta; su edad es
//     la de su replica de la 2.0 (MetaExtra) o la de su libro de la 4.1 (Meta); la conversion de cada replica va como una fuente mas
//     ("2.0 QQQ", "2.0 NDX"). Sin extras (motor sin OpcionesMotorFamilia.Extras) la salida queda igual que en la 4.1.2.
// Sin referencias a ATAS.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaGexCuatro.Familia
{
    public static class SalidaFamilia
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Precios por serie de la vela m2 que abre en aperturaMs. null si no hay nada.</summary>
        public static Dictionary<string, double[]> Entrada(long aperturaMs, Func<long, RegistroMinuto> niv, AlmacenTres tres, TqqqVela tq)
        {
            long key = aperturaMs / 60000L;
            Dictionary<string, double[]> d = null;
            var r = niv(key);
            if (r == null || r.Vacio) r = niv(key - 1);
            if (r != null && !r.Vacio)
            {
                var calc = CatalogoFamilia.Calc;
                for (int s = 0; s < calc.Length; s++)
                {
                    var lv = r.Series[s]; if (lv == null || lv.Length == 0) continue;
                    var ps = new double[lv.Length]; for (int k = 0; k < lv.Length; k++) ps[k] = lv[k].Precio;
                    (d ?? (d = new Dictionary<string, double[]>(StringComparer.Ordinal)))[calc[s]] = ps;
                }
            }
            // 4.1.3: las series extra con la misma regla (el minuto de la apertura o, si no las tiene, el anterior)
            var rx = niv(key);
            if (rx == null || !rx.TieneExtra) rx = niv(key - 1);
            if (rx != null && rx.TieneExtra)
                for (int s = 0; s < CatalogoFamilia.Extra.Length && s < rx.Extra.Length; s++)
                {
                    var lv = rx.Extra[s]; if (lv == null || lv.Length == 0) continue;
                    var ps = new double[lv.Length]; for (int k = 0; k < lv.Length; k++) ps[k] = lv[k].Precio;
                    (d ?? (d = new Dictionary<string, double[]>(StringComparer.Ordinal)))[CatalogoFamilia.Extra[s]] = ps;
                }
            if (tres != null)
                foreach (var capa in CatalogoFamilia.CapasTres)
                {
                    var lv = tres.Niveles(capa, key, out _); if (lv == null) continue;
                    var ps = new double[lv.Length]; for (int k = 0; k < lv.Length; k++) ps[k] = lv[k].Precio;
                    (d ?? (d = new Dictionary<string, double[]>(StringComparer.Ordinal)))["TRES_" + capa] = ps;
                }
            if (tq?.Series != null)
                foreach (var kv in tq.Series)
                {
                    if (kv.Value == null || kv.Value.Length == 0 || !CatalogoFamilia.PorId.ContainsKey(kv.Key)) continue;
                    var ps = new double[kv.Value.Length]; for (int k = 0; k < ps.Length; k++) ps[k] = kv.Value[k].Precio;
                    (d ?? (d = new Dictionary<string, double[]>(StringComparer.Ordinal)))[kv.Key] = ps;
                }
            return d;
        }

        /// <summary>Los niveles vigentes "ahora" (preview_niveles.salida 'actuales' + tqqq_vivo 'actuales').</summary>
        public static List<NivelActual> Actuales(RegistroMinuto ultimo, AlmacenTres tres, TqqqVela tqUltima)
        {
            var act = new List<NivelActual>();
            var calc = CatalogoFamilia.Calc;
            if (ultimo != null && ultimo.Series != null)
            {
                for (int s = 0; s < calc.Length; s++)
                {
                    var lv = ultimo.Series[s]; if (lv == null) continue;
                    var si = CatalogoFamilia.PorId[calc[s]];
                    for (int k = 0; k < lv.Length; k++)
                        act.Add(Actual(si, lv[k], ultimo, ultimo.Strikes?[s] != null && k < ultimo.Strikes[s].Length ? ultimo.Strikes[s][k] : double.NaN));
                }
            }
            // 4.1.3: las series extra del mismo minuto (edad: la de su replica de la 2.0 o la de su libro de la 4.1)
            if (ultimo != null && ultimo.TieneExtra)
                for (int s = 0; s < CatalogoFamilia.Extra.Length && s < ultimo.Extra.Length; s++)
                {
                    var lv = ultimo.Extra[s]; if (lv == null) continue;
                    var si = CatalogoFamilia.PorId[CatalogoFamilia.Extra[s]];
                    var mx = CatalogoFamilia.MetaExtraDe(si.Id);
                    var m = mx != null ? ultimo.MetaExtraDe(mx) : ultimo.MetaDe(si.Libro);
                    var ks = ultimo.ExtraStrikes != null && s < ultimo.ExtraStrikes.Length ? ultimo.ExtraStrikes[s] : null;
                    for (int k = 0; k < lv.Length; k++)
                        act.Add(new NivelActual { Serie = si.Id, Libro = si.Libro, Fuente = si.Fuente, Tipo = si.Tipo, Rol = CatalogoFamilia.Rol(si.Tipo, lv[k].Etq),
                                                  Precio = lv[k].Precio, Strike = ks != null && k < ks.Length ? ks[k] : double.NaN, GexM = lv[k].GexM, Banda = si.Banda,
                                                  DatoUtc = m?.DatoUtc ?? default, OiViejo = false });
                }
            if (ultimo != null && tres != null)
                foreach (var capa in CatalogoFamilia.CapasTres)
                {
                    Nivel[] x = null; DateTime linea = default;
                    for (long kk = ultimo.Clave; kk > ultimo.Clave - 30; kk--)
                    { x = tres.Niveles(capa, kk, out linea); if (x != null) break; }
                    if (x == null) continue;
                    var si = CatalogoFamilia.PorId["TRES_" + capa];
                    var m = capa == "NQ" ? null : ultimo.MetaDe(capa);
                    var dato = m != null && m.DatoUtc != default ? m.DatoUtc : linea;
                    // 4.1.2: GexM = el monto de la raya ("gm" de la estela, en unidades de la familia; NaN si la linea no lo trae o la dominante
                    // no es un strike con monto, p. ej. el tunel por cruces de noche). El strike sigue NaN: la linea no trae K.
                    foreach (var n in x)
                        act.Add(new NivelActual { Serie = si.Id, Libro = si.Libro, Fuente = si.Fuente, Tipo = si.Tipo, Rol = CatalogoFamilia.Rol(si.Tipo, n.Etq),
                                                  Precio = n.Precio, Strike = double.NaN, GexM = n.GexM, Banda = si.Banda, DatoUtc = dato, OiViejo = false });
                }
            if (tqUltima?.Series != null)
                foreach (var id in CatalogoFamilia.SeriesTqqq)
                {
                    if (!tqUltima.Series.TryGetValue(id, out var lv) || lv == null) continue;
                    var si = CatalogoFamilia.PorId[id];
                    tqUltima.Strikes.TryGetValue(id, out var ks);
                    for (int k = 0; k < lv.Length; k++)
                    {
                        string rol = lv[k].Etq;
                        if (id == "T_DOMS_raz" && (rol == "dom" || string.IsNullOrEmpty(rol))) rol = "dom (razon)";   // tqqq_vivo: rol="dom (razon)"
                        act.Add(new NivelActual { Serie = id, Libro = "TQQQ", Fuente = si.Fuente, Tipo = si.Tipo, Rol = rol, Precio = lv[k].Precio,
                                                  Strike = ks != null && k < ks.Length ? ks[k] : double.NaN, GexM = lv[k].GexM, Banda = si.Banda,
                                                  DatoUtc = tqUltima.DatoUtc, OiViejo = false });
                    }
                }
            return act;
        }

        /// <summary>preview_niveles.Motor._actual para una serie calculada (libro o familia).</summary>
        private static NivelActual Actual(SerieInfo si, Nivel n, RegistroMinuto r, double strike)
        {
            DateTime dato = default; bool oiOk = true;
            if (si.Libro == "familia")
            {   // el dato mas viejo de los libros del minuto; OI ok solo si todos lo estan (series por OI)
                foreach (var m in r.Meta) if (m.DatoUtc != default && (dato == default || m.DatoUtc < dato)) dato = m.DatoUtc;
                if (si.Fuente == "oi") foreach (var m in r.Meta) oiOk &= m.OiOk;
                strike = double.NaN;
            }
            else
            {
                var m = r.MetaDe(si.Libro);
                if (m != null) { dato = m.DatoUtc; if (si.Fuente == "oi") oiOk = m.OiOk; }
            }
            return new NivelActual
            {
                Serie = si.Id, Libro = si.Libro, Fuente = si.Fuente, Tipo = si.Tipo, Rol = CatalogoFamilia.Rol(si.Tipo, n.Etq),
                Precio = n.Precio, Strike = strike, GexM = n.GexM, Banda = si.Banda, DatoUtc = dato, OiViejo = !oiOk
            };
        }

        /// <summary>Una por libro del ultimo minuto (conversion, edad, OI) + TQQQ + el estado del descargador y de la cinta.</summary>
        public static List<FuenteEstado> Fuentes(RegistroMinuto ultimo, TqqqVela tq, string estadoCboe, DateTime ultimoTickUtc, DateTime ahoraUtc)
        {
            var l = new List<FuenteEstado>();
            if (ultimo != null)
                foreach (var m in ultimo.Meta)
                    l.Add(new FuenteEstado { Libro = m.Libro, Texto = TextoLibro(m), ConvValor = m.Conv, DatoUtc = m.DatoUtc, Congelada = m.Congelada, OiOk = m.OiOk });
            // 4.1.3: la conversion de cada replica de la 2.0 ("2.0 QQQ": su razon; "2.0 NDX": su base), aparte de la de los libros de la 4.1
            if (ultimo?.MetaExtra != null)
                foreach (var m in ultimo.MetaExtra)
                    l.Add(new FuenteEstado { Libro = m.Libro, Texto = m.ConvTexto ?? "", ConvValor = m.Conv, DatoUtc = m.DatoUtc, Congelada = m.Congelada, OiOk = m.OiOk });
            if (tq != null && !double.IsNaN(tq.S) && !double.IsNaN(tq.C))
                l.Add(new FuenteEstado { Libro = "TQQQ", Texto = string.Format(Inv, "x3: NQ = {0:0.00} x TQQQ + {1:0.0} (+-4)", tq.S, tq.C), DatoUtc = tq.DatoUtc, Congelada = tq.Congelada });
            if (!string.IsNullOrEmpty(estadoCboe))
                l.Add(new FuenteEstado { Libro = "CBOE", Texto = estadoCboe });
            if (ultimoTickUtc != default)
                l.Add(new FuenteEstado { Libro = "cinta", Texto = "ultimo tick hace " + Edad((ahoraUtc - ultimoTickUtc).TotalSeconds), DatoUtc = ultimoTickUtc });
            return l;
        }

        private static string TextoLibro(MetaLibro m)
        {
            if (!string.IsNullOrEmpty(m.ConvTexto)) return m.ConvTexto;
            if (m.Libro == "NQ") return Math.Abs(m.Conv) < 1e-9 || double.IsNaN(m.Conv) ? "mismo contrato" : "corrimiento " + m.Conv.ToString("0.00", Inv);
            if (m.Libro == "NDX") return "base " + m.Conv.ToString("0.00", Inv);
            return "razon " + m.Conv.ToString("0.0000", Inv);
        }

        public static string Edad(double s)
        {
            if (double.IsNaN(s)) return "?";
            if (s < 90) return Math.Max(0, (int)Math.Round(s)).ToString(Inv) + " s";
            if (s < 5400) return ((int)Math.Round(s / 60)).ToString(Inv) + " min";
            return (s / 3600).ToString("0.0", Inv) + " h";
        }
    }
}
