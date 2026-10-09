// Dobles.cs — dobles de prueba del arnes fam (B3d): cinta, minuteros, fuentes y TQQQ falsos que sirven los casos sinteticos de
// gen_casos_reglas.py como si fueran minutos de una sesion. No hay ATAS ni red.
using System;
using System.Collections.Generic;
using System.Text.Json;
using PythiaGexCuatro.Familia;

namespace FamTest
{
    /// <summary>Un caso de gen_casos_reglas.py: los libros (BM ya filtrado) y lo que devolvio Python.</summary>
    public sealed class Caso
    {
        public int I; public double Fut;
        public Dictionary<string, JsonElement> Libros = new Dictionary<string, JsonElement>();
        public JsonElement Reg, Crudo;

        public LibroMinuto Libro(string lb, long clave)
        {
            if (!Libros.TryGetValue(lb, out var b)) return null;
            double[] A(string k) { var e = b.GetProperty(k); var r = new double[e.GetArrayLength()]; int i = 0; foreach (var x in e.EnumerateArray()) r[i++] = x.GetDouble(); return r; }
            double N(string k) => b.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;
            var lm = new LibroMinuto
            {
                Libro = lb, Clave = clave, FutMnq = b.GetProperty("fut").GetDouble(), S = N("S"), Conv = N("conv"), PorRazon = lb == "QQQ",
                Fut = A("Fut"), GvC = A("gvC"), GvP = A("gvP"), GoC = A("goC"), GoP = A("goP"),
                ZeroVol = N("zv"), ZeroOi = N("zo"),
                OiOk = lb == "NQ" ? b.GetProperty("oi_ok").GetBoolean() : b.GetProperty("oi_fresco").GetBoolean(),
                Mult = lb == "NQ" ? 20 : 100,
                ConvTexto = lb == "NQ" ? "mismo contrato" : lb == "NDX" ? "base de prueba" : "razon de prueba",
            };
            int n = lm.Fut.Length; lm.Gv = new double[n]; lm.Go = new double[n];
            for (int i = 0; i < n; i++) { lm.Gv[i] = lm.GvC[i] + lm.GvP[i]; lm.Go[i] = lm.GoC[i] + lm.GoP[i]; }   // BF.BM: r["gv"] = gvC + gvP
            return lm;
        }
    }

    /// <summary>Sirve el caso (clave - ini) % n como el libro de un minuto. Cuenta llamadas y tandas.</summary>
    public sealed class MinuteroFalso : ILibroMinutero, ITandaMinutero
    {
        private readonly List<Caso> _c; private readonly long _ini;
        public string Libro { get; }
        public long Llamadas; public long UltimaClave = long.MinValue; public bool FueraDeOrden;
        public readonly List<(long Primera, long Ultima)> Tandas = new List<(long, long)>();
        public readonly HashSet<long> Pedidas = new HashSet<long>();
        public MinuteroFalso(string libro, List<Caso> casos, long iniClave) { Libro = libro; _c = casos; _ini = iniClave; }
        public LibroMinuto Minuto(long clave, DateTime tUtc, double futMnq)
        {
            Llamadas++; Pedidas.Add(clave);
            if (clave <= UltimaClave) FueraDeOrden = true;
            UltimaClave = clave;
            if (double.IsNaN(futMnq)) return null;
            var caso = _c[(int)((clave - _ini) % _c.Count)];
            var lm = caso.Libro(Libro, clave);
            if (lm != null) lm.DatoUtc = tUtc.AddSeconds(Libro == "NQ" ? -20 : -930);
            return lm;
        }
        public void NuevaTanda(DateTime ahoraUtc, long primeraClave, long ultimaClave) => Tandas.Add((primeraClave, ultimaClave));
    }

    public sealed class CintaFalsa : ICinta
    {
        private readonly List<Caso> _c; private readonly long _ini;
        public DateTime Ahora;
        public CintaFalsa(List<Caso> casos, long iniClave) { _c = casos; _ini = iniClave; }
        public double CierreConocido(DateTime tUtc)
        {
            long k = SesionFamilia.Ms(tUtc) / 60000L;
            if (k < _ini) return double.NaN;
            return _c[(int)((k - _ini) % _c.Count)].Fut;
        }
        public double Precio(DateTime tUtc) => double.NaN;
        public double PrecioSoloTick(DateTime tUtc, int maxEdadS = 120) => double.NaN;
        public IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime desdeUtc, DateTime hastaUtc) => Array.Empty<(long, double, double, double, double)>();
        public DateTime UltimoTickUtc => Ahora.AddSeconds(-1);
    }

    public sealed class FuenteFalsa : IFuenteCboe, ILibroNq
    {
        public long Version { get; set; } = 1;
        public string Estado => "QQQ hace 40 s · NDX hace 1 min · TQQQ hace 30 s (prueba)";
        public void Arrancar() { }
        public void Parar() { }
        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc) => Array.Empty<FotoCadena>();
        public FotoCadena Ultima(string libro) => null;
        public IReadOnlyList<FotoCadena> Fotos(DateTime desdeUtc, DateTime hastaUtc) => Array.Empty<FotoCadena>();
        public FotoCadena Ultima() => null;
    }

    /// <summary>TQQQ falso: niveles deterministas desde la rueda (para probar historia, actuales y el eje).</summary>
    public sealed class TqqqFalso : ICalculoTqqq
    {
        public long Llamadas; public DateTime Ahora; public DateTime RuedaIni;
        public TqqqVela Vela(long aperturaM2Ms, ICinta cinta, IFuenteCboe cboe)
        {
            Llamadas++;
            var t = SesionFamilia.DeMs(aperturaM2Ms);
            if (t < RuedaIni || t > Ahora) return null;
            double s = 124.21302718647851, c = 21012.058;
            double k1 = 80 + (aperturaM2Ms / 120000L) % 4, k2 = k1 + 1;
            var v = new TqqqVela { AperturaM2Ms = aperturaM2Ms, S = s, C = c, DatoUtc = t.AddSeconds(-900), Congelada = false };
            v.Series["T_DOMS_vol"] = new[] { new Nivel(NumPy.RoundPy(s * k1 + c, 2), "dom", 13.49), new Nivel(NumPy.RoundPy(s * k2 + c, 2), "dom", 6.43) };
            v.Series["T_MUROS_oi"] = new[] { new Nivel(NumPy.RoundPy(s * k2 + c, 2), "muro C", double.NaN), new Nivel(NumPy.RoundPy(s * k1 + c, 2), "muro P", double.NaN) };
            v.Strikes["T_DOMS_vol"] = new[] { k1, k2 }; v.Strikes["T_MUROS_oi"] = new[] { k2, k1 };
            return v;
        }
    }
}
