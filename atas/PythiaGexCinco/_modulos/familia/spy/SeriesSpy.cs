// SeriesSpy.cs — PythiaGex 4.1, modulo Familia SPY (B3c). 08-10-2026.
// Las 5 series de SPY de la vista previa sobre el LibroMinuto de MinuteroSpy (backtest_familia.conjuntos_minuto + preview_niveles.calcular):
//   MUROS_SPY_vol  D1 "muro C" = Fut[argmax GvC > 0], D2 "muro P" = Fut[argmin GvP < 0] en R = min(2 % fut, 100); monto GvC / GvP
//   MUROS_SPY_oi   igual con GoC / GoP (siempre: oi_vale(SPY) = True; si el OI es de 2 sesiones solo se MARCA, LibroMinuto.OiOk)
//   MAJORS_SPY_vol D1 "M+" = argmax Gv > 0, D2 "M-" = argmin Gv < 0 en R; monto Gv
//   MAJORS_SPY_oi  igual con Go
//   ZEST_SPY_vol   "Z" = zero estandar por volumen (C5) si |z - fut| <= 300; sin monto
// Montos: preview_niveles._monto, round(v * MULT/100 / 1e6, 1) en M USD por 1 % (SPY: MULT 100 -> x1).
// Ademas: el aporte de SPY a la CONFLUENCIA (pools de MUROS + MAJORS + ZTP por fuente, incluido el ZTP de SPY que no es serie visible)
// y la ficha de catalogo de las 5 series (color, banda +-20, textos) copiada literal de preview_niveles.SERIES.
using System;
using System.Collections.Generic;

namespace PythiaGexCinco.Familia.Spy
{
    public static class SeriesSpy
    {
        public static readonly string[] IDS = { "MUROS_SPY_vol", "MUROS_SPY_oi", "MAJORS_SPY_vol", "MAJORS_SPY_oi", "ZEST_SPY_vol" };

        /// <summary>Las 5 series de SPY en el minuto (solo las que tienen niveles), con precio crudo (sin redondear) y monto.</summary>
        public static Dictionary<string, List<NivelFam>> Calcular(LibroMinuto b)
        {
            var o = new Dictionary<string, List<NivelFam>>(5);
            if (b == null) return o;
            double mult = b.Mult / 100.0;
            void Pon(string id, List<NivelFam> lv) { if (lv != null && lv.Count > 0) o[id] = lv; }

            var mv = SeleccionFam.Muros(b, false);
            foreach (var x in mv) x.GexM = SeleccionFam.Monto(b.Fut, x.E == "D1" ? b.GvC : b.GvP, x.P, mult);
            Pon("MUROS_SPY_vol", mv);

            var mo = SeleccionFam.Muros(b, true);
            foreach (var x in mo) x.GexM = SeleccionFam.Monto(b.Fut, x.E == "D1" ? b.GoC : b.GoP, x.P, mult);
            Pon("MUROS_SPY_oi", mo);

            var jv = SeleccionFam.Majors(b, false);
            foreach (var x in jv) x.GexM = SeleccionFam.Monto(b.Fut, b.Gv, x.P, mult);
            Pon("MAJORS_SPY_vol", jv);

            var jo = SeleccionFam.Majors(b, true);
            foreach (var x in jo) x.GexM = SeleccionFam.Monto(b.Fut, b.Go, x.P, mult);
            Pon("MAJORS_SPY_oi", jo);

            Pon("ZEST_SPY_vol", SeleccionFam.Zest(b, false));
            return o;
        }

        /// <summary>Los Nivel del contrato (precio round 2 de Python, etiqueta = rol: "muro C", "M+", "0G est.") de lo que dio Calcular.</summary>
        public static Dictionary<string, Nivel[]> ANiveles(Dictionary<string, List<NivelFam>> s)
        {
            var o = new Dictionary<string, Nivel[]>(s.Count);
            foreach (var kv in s)
            {
                string tipo = kv.Key.StartsWith("MUROS", StringComparison.Ordinal) ? "MUROS" : kv.Key.StartsWith("MAJORS", StringComparison.Ordinal) ? "MAJORS" : "ZEST";
                var a = new Nivel[kv.Value.Count];
                for (int i = 0; i < a.Length; i++) a[i] = kv.Value[i].ANivel(tipo);
                o[kv.Key] = a;
            }
            return o;
        }

        /// <summary>Lo que SPY aporta a la confluencia (backtest_familia.conjuntos_minuto: pools[fuente]["SPY"] = precios de MUROS, MAJORS y ZTP
        /// de esa fuente, en ese orden, D1 antes que D2). Con oi = true es el pool de CONF_oi (SPY: oi_vale siempre).</summary>
        public static List<double> PoolConf(LibroMinuto b, bool oi)
        {
            var o = new List<double>(6);
            if (b == null) return o;
            foreach (var x in SeleccionFam.Muros(b, oi)) o.Add(x.P);
            foreach (var x in SeleccionFam.Majors(b, oi)) o.Add(x.P);
            foreach (var x in SeleccionFam.Ztp(b, oi)) o.Add(x.P);
            return o;
        }

        /// <summary>Las fichas de catalogo de las 5 series (preview_niveles.SERIES, literal).</summary>
        public static IReadOnlyList<SerieInfo> Catalogo() => new List<SerieInfo>
        {
            new SerieInfo { Id = "MAJORS_SPY_oi", Corto = "SPY", Libro = "SPY", Fuente = "oi", Tipo = "MAJORS", Grupo = "Recomendadas", ColorHex = "#e2b872", Banda = 20,
                Tecnico = "Major positivo / negativo de SPY por interes abierto (banda +-20)",
                Criollo = "el strike de SPY con mas gamma neta acumulada (posiciones de ayer); un strike de SPY son 41 pts de ES, por eso es una banda de +-20" },
            new SerieInfo { Id = "MUROS_SPY_vol", Corto = "SPY", Libro = "SPY", Fuente = "vol", Tipo = "MUROS", Grupo = "Familia", ColorHex = "#dabe6f", Banda = 20,
                Tecnico = "Call wall / put wall de SPY por volumen (banda +-20)", Criollo = "lo mismo en el ETF SPY (strikes cada 41 pts de ES)" },
            new SerieInfo { Id = "MUROS_SPY_oi", Corto = "SPY", Libro = "SPY", Fuente = "oi", Tipo = "MUROS", Grupo = "Familia", ColorHex = "#ff9f43", Banda = 20,
                Tecnico = "Call wall / put wall de SPY por interes abierto (banda +-20)", Criollo = "posiciones abiertas en SPY" },
            new SerieInfo { Id = "MAJORS_SPY_vol", Corto = "SPY", Libro = "SPY", Fuente = "vol", Tipo = "MAJORS", Grupo = "Familia", ColorHex = "#f6c177", Banda = 20,
                Tecnico = "Major +/- de SPY por volumen (banda +-20)", Criollo = "gamma neta de hoy en SPY" },
            new SerieInfo { Id = "ZEST_SPY_vol", Corto = "SPY", Libro = "SPY", Fuente = "vol", Tipo = "ZEST", Grupo = "Zero", ColorHex = "#fcd34d", Banda = 0,
                Tecnico = "Zero gamma estandar de SPY por volumen", Criollo = "zero del ETF" },
        };

        /// <summary>Estado (texto de validacion) de cada serie, literal de preview_niveles.SERIES[...]["estado"].</summary>
        public static string EstadoSerie(string id)
        {
            switch (id)
            {
                case "MAJORS_SPY_oi": return "opcional de noche, SIN VALIDAR: rebote 68,2 % contra 63,9 % del azar y 62,9 % de la raya corrida, Holm 0,84";
                case "MUROS_SPY_oi": return "sin ventaja medida (rebote 68,0 % contra 63,9 % del azar, Holm 1,0)";
                case "MUROS_SPY_vol":
                case "MAJORS_SPY_vol":
                case "ZEST_SPY_vol": return "sin ventaja medida";
                default: return "";
            }
        }
    }
}
