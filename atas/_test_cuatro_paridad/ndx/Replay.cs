// Replay.cs — arnes NDX (B3b). Prueba E del diseño: el MISMO modulo alimentado minuto a minuto con un reloj simulado (las fotos aparecen a su
// 'generado', la cinta solo hasta 'ahora', las de mas de 30 h sin filas como en B2), con dos reinicios a mitad de la sesion (03:00 y 15:00 UTC) que vuelven a arrancar con la cinta
// de la sesion solamente y las muestras persistidas. Tiene que dar lo mismo que la corrida historica.
using System;
using System.Collections.Generic;
using System.Linq;
using PythiaGexCuatro.Familia;

namespace ArnesNdx
{
    public sealed class ReplayCboe : IFuenteCboe
    {
        readonly List<FotoCadena> _todas;
        public DateTime Ahora = DateTime.MinValue;
        public ReplayCboe(IFuenteCboe fuente) { _todas = fuente.Fotos("NDX", DateTime.MinValue, DateTime.MaxValue).ToList(); }
        int Visibles() { int lo = 0, hi = _todas.Count; while (lo < hi) { int m = (lo + hi) >> 1; if (_todas[m].GeneradoUtc <= Ahora) lo = m + 1; else hi = m; } return lo; }
        public void Arrancar() { }
        public void Parar() { }
        public long Version => Visibles();
        public string Estado => "replay";
        public FotoCadena Ultima(string libro) { int n = Visibles(); return libro == "NDX" && n > 0 ? _todas[n - 1] : null; }
        /// <summary>Como BajadorCboe (B2): las fotos de mas de HorasConFilas (30 h) quedan "livianas" (sin filas), salvo la ultima anterior al corte.</summary>
        public double HorasConFilas = 30;
        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desdeUtc, DateTime hastaUtc)
        {
            if (libro != "NDX") return new List<FotoCadena>();
            int n = Visibles();
            var corte = Ahora.AddHours(-HorasConFilas);
            int j = -1; for (int i = 0; i < n; i++) if (_todas[i].GeneradoUtc < corte) j = i;     // ultima anterior al corte: conserva filas
            var r = new List<FotoCadena>();
            for (int i = 0; i < n; i++)
            {
                var f = _todas[i];
                if (f.GeneradoUtc < desdeUtc || f.GeneradoUtc >= hastaUtc) continue;
                if (HorasConFilas > 0 && i < j) f = Liviana(f);
                r.Add(f);
            }
            return r;
        }

        static FotoCadena Liviana(FotoCadena f) => new FotoCadena
        {
            Libro = f.Libro, GeneradoUtc = f.GeneradoUtc, TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot, Dias = f.Dias,
            Filas = Array.Empty<FilaCadena>(), EsFuturo = f.EsFuturo, OiTotal = f.OiTotal, BaseCruda = f.BaseCruda, BaseErrorTicks = f.BaseErrorTicks,
            CierreAnterior = f.CierreAnterior, FuturoFoto = f.FuturoFoto,
        };
    }

    public sealed class ReplayCinta : ICinta
    {
        readonly ICinta _c;
        public DateTime Ahora = DateTime.MinValue;
        public ReplayCinta(ICinta c) { _c = c; }
        public double Precio(DateTime t) => t > Ahora ? double.NaN : _c.Precio(t);
        public double PrecioSoloTick(DateTime t, int maxEdadS = 120) => t > Ahora ? double.NaN : _c.PrecioSoloTick(t, maxEdadS);
        public double CierreConocido(DateTime t) => t > Ahora ? double.NaN : _c.CierreConocido(t);
        public IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime d, DateTime h) => _c.VelasM2(d, h < Ahora ? h : Ahora);
        public DateTime UltimoTickUtc => _c.UltimoTickUtc < Ahora ? _c.UltimoTickUtc : Ahora;
    }
}
