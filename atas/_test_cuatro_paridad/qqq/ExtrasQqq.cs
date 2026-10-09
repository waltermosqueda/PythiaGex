// ExtrasQqq.cs — pruebas del modulo QQQ que la referencia (un historico de sesion completa) no ejercita:
//   1. VIVO: las fotos de CBOE aparecen de a una (generado <= ahora) y la cinta llega solo hasta 'ahora' (t + 6 s, como la vista previa en
//      vivo que calcula al segundo 6): tiene que dar EXACTAMENTE lo mismo que el historico, minuto a minuto.
//   2. REINICIO: ATAS se reinicia y la cinta de los dias previos ya no esta (Precio NaN antes del inicio de la sesion). Con las muestras
//      persistidas (LineaMuestra -> LeerLineaMuestra -> SembrarPrecio) tiene que dar lo mismo; sin ellas se informa cuanto cambia.
//   3. HORA: el modo Corregida en horario de INVIERNO (C9 hasta 09:30 NY = 14:30 UTC, pausa de CME 22:00-23:00 UTC, franjas de oi_fresco
//      corridas una hora) y el contrato desde el codigo del grafico.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PythiaGexCuatro.Familia;
using PythiaGexCuatro.Familia.Qqq;

namespace ParidadQqq
{
    /// <summary>La fuente de CBOE vista "en vivo": solo las fotos con generado &lt;= Ahora; la version sube con cada foto visible.</summary>
    public sealed class CboeEnVivo : IFuenteCboe
    {
        private readonly IFuenteCboe _todo; private readonly string _libro;
        public DateTime Ahora;
        /// <summary>ms que se le suman a 'generado' (B2 guarda UtcNow con fraccion; el archivo y la referencia, segundos).</summary>
        public int FraccionMs;
        private readonly Dictionary<FotoCadena, FotoCadena> _copias = new Dictionary<FotoCadena, FotoCadena>();
        public CboeEnVivo(IFuenteCboe todo, string libro) { _todo = todo; _libro = libro; }
        public void Arrancar() { } public void Parar() { }
        private FotoCadena Fr(FotoCadena f)
        {
            if (FraccionMs == 0) return f;
            if (!_copias.TryGetValue(f, out var c))
                _copias[f] = c = new FotoCadena { Libro = f.Libro, GeneradoUtc = f.GeneradoUtc.AddMilliseconds(FraccionMs), TsUtc = f.TsUtc, DatoUtc = f.DatoUtc, Spot = f.Spot, Dias = f.Dias, Filas = f.Filas, OiTotal = f.OiTotal };
            return c;
        }
        public IReadOnlyList<FotoCadena> Fotos(string libro, DateTime desde, DateTime hasta) => _todo.Fotos(libro, desde, hasta).Select(Fr).Where(f => f.GeneradoUtc <= Ahora).ToList();
        public FotoCadena Ultima(string libro) => Fotos(libro, DateTime.MinValue, DateTime.MaxValue).LastOrDefault();
        public long Version => _todo.Fotos(_libro, DateTime.MinValue, DateTime.MaxValue).Select(Fr).Count(f => f.GeneradoUtc <= Ahora);   // coherente con Fotos (como B2)
        public string Estado => "en vivo hasta " + Ahora.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>La cinta vista "en vivo" (nada despues de Ahora) y/o sin historia antes de SinAntesDe (reinicio de ATAS).</summary>
    public sealed class CintaRecortada : ICinta
    {
        private readonly ICinta _c;
        public DateTime Ahora = DateTime.MaxValue, SinAntesDe = DateTime.MinValue;
        public CintaRecortada(ICinta c) { _c = c; }
        private bool Fuera(DateTime t) => t > Ahora || t < SinAntesDe;
        public double Precio(DateTime t) => Fuera(t) ? double.NaN : _c.Precio(t);
        public double PrecioSoloTick(DateTime t, int maxEdadS = 120) => Fuera(t) ? double.NaN : _c.PrecioSoloTick(t, maxEdadS);
        public double CierreConocido(DateTime t) => _c.CierreConocido(t);
        public IReadOnlyList<(long Ms, double O, double H, double L, double C)> VelasM2(DateTime d, DateTime h) => _c.VelasM2(d, h);
        public DateTime UltimoTickUtc => _c.UltimoTickUtc < Ahora ? _c.UltimoTickUtc : Ahora;
    }

    public static class ExtrasQqq
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Firma exacta (precios y montos con "R") de las series de un minuto, mas la razon: para comparar corridas C# entre si.</summary>
        public static string Firma(LibroMinuto b, Dictionary<string, List<NivelFam>> s)
        {
            if (b == null) return "";
            var sb = new StringBuilder();
            sb.Append(b.Conv.ToString("R", Inv)).Append('|').Append(b.ZeroVol.ToString("R", Inv)).Append('|').Append(b.OiOk ? 1 : 0).Append(b.Congelada ? 1 : 0);
            foreach (var id in SeriesQqq.IDS)
                if (s.TryGetValue(id, out var l))
                {
                    sb.Append('|').Append(id);
                    foreach (var x in l) sb.Append(';').Append(x.P.ToString("R", Inv)).Append(x.E).Append(x.GexM.ToString("R", Inv));
                }
            return sb.ToString();
        }

        public static bool Vivo(IFuenteCboe cboe, ICinta cinta, Func<OpcionesQqq> op, string dia, Dictionary<long, string> firma, int fraccionMs = 0)
        {
            var cv = new CboeEnVivo(cboe, "QQQ") { FraccionMs = fraccionMs }; var cr = new CintaRecortada(cinta);
            var min = new MinuteroQqq(cv, cr, op(), TiempoFam.IniSesion(dia).AddDays(-6));
            int dif = 0, n = 0; string ej = "";
            long k0 = TiempoFam.Clave(TiempoFam.IniSesion(dia)), k1 = TiempoFam.Clave(TiempoFam.FinSesion(dia)) - 1;
            for (long k = k0; k <= k1; k++)
            {
                var t = TiempoFam.DeClave(k);
                cv.Ahora = t.AddSeconds(6); cr.Ahora = t.AddSeconds(6);
                var b = min.Minuto(k, t, cinta.CierreConocido(t));
                var f = Firma(b, b == null ? new Dictionary<string, List<NivelFam>>() : SeriesQqq.Calcular(b));
                n++;
                if (f != firma[k]) { dif++; if (ej == "") ej = $" (primera {t:HH:mm}Z: vivo [{f}] historico [{firma[k]}])"; }
            }
            Console.WriteLine($"  {(dif == 0 ? "OK " : "DIF")} VIVO (fotos de a una, cinta hasta t+6 s{(fraccionMs != 0 ? ", generado con +" + fraccionMs + " ms como UtcNow" : "")}): {n - dif} de {n} minutos identicos al bit{ej}");
            return dif == 0;
        }

        public static bool Reinicio(IFuenteCboe cboe, ICinta cinta, Func<OpcionesQqq> op, string dia, Dictionary<long, string> firma, List<string> lineas)
        {
            var ini = TiempoFam.IniSesion(dia);
            bool ok = true;
            foreach (var sembrar in new[] { true, false })
            {
                var cr = new CintaRecortada(cinta) { SinAntesDe = ini };
                var min = new MinuteroQqq(cboe, cr, op(), ini.AddDays(-6));
                int leidas = 0;
                if (sembrar)
                    foreach (var l in lineas)
                        if (ConversionQqq.LeerLineaMuestra(l, out var ts, out var p, out var kc)) { min.Conversion.SembrarPrecio(ts, p, kc); leidas++; }
                int dif = 0, n = 0; string ej = "";
                long k0 = TiempoFam.Clave(ini), k1 = TiempoFam.Clave(TiempoFam.FinSesion(dia)) - 1;
                for (long k = k0; k <= k1; k++)
                {
                    var t = TiempoFam.DeClave(k);
                    var b = min.Minuto(k, t, cinta.CierreConocido(t));
                    var f = Firma(b, b == null ? new Dictionary<string, List<NivelFam>>() : SeriesQqq.Calcular(b));
                    n++;
                    if (f != firma[k]) { dif++; if (ej == "") ej = $" (primera {t:HH:mm}Z)"; }
                }
                if (sembrar)
                {
                    ok &= dif == 0;
                    Console.WriteLine($"  {(dif == 0 ? "OK " : "DIF")} REINICIO sin cinta previa + {leidas} muestras persistidas: {n - dif} de {n} minutos identicos{ej}");
                }
                else Console.WriteLine($"      (informativo) REINICIO sin cinta previa y SIN muestras: {dif} de {n} minutos distintos{ej} -> el motor tiene que persistir las muestras");
            }
            return ok;
        }

        public static bool Hora()
        {
            bool ok = true;
            void Chk(string que, bool c) { ok &= c; if (!c) Console.WriteLine("  DIF HORA: " + que); }
            DateTime U(int y, int mo, int d, int h, int mi) => new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc);
            var par = new ConversionQqq(new OpcionesQqq { Corregida = false });
            var cor = new ConversionQqq(new OpcionesQqq { Corregida = true });
            // C9 congelada: verano iguales; invierno 13:30 UTC (Python) contra 14:30 UTC (09:30 EST)
            Chk("C9 verano", par.Vigencia(U(2026, 10, 8, 21, 30), true) == U(2026, 10, 9, 13, 30) && cor.Vigencia(U(2026, 10, 8, 21, 30), true) == U(2026, 10, 9, 13, 30));
            Chk("C9 invierno paridad", par.Vigencia(U(2026, 11, 5, 22, 30), true) == U(2026, 11, 6, 13, 30));
            Chk("C9 invierno corregida", cor.Vigencia(U(2026, 11, 5, 22, 30), true) == U(2026, 11, 6, 14, 30));
            Chk("C9 madrugada corregida", cor.Vigencia(U(2026, 11, 6, 2, 0), true) == U(2026, 11, 6, 14, 30));
            Chk("C9 no congelada", cor.Vigencia(U(2026, 11, 6, 15, 0), false) == U(2026, 11, 6, 15, 25));
            // pausa de CME
            Chk("pausa verano", par.FueraDeSesion(U(2026, 10, 8, 21, 30)) && cor.FueraDeSesion(U(2026, 10, 8, 21, 30)) && !cor.FueraDeSesion(U(2026, 10, 8, 21, 0)) && !par.FueraDeSesion(U(2026, 10, 8, 22, 0)));
            Chk("pausa invierno", !par.FueraDeSesion(U(2026, 11, 5, 22, 30)) && cor.FueraDeSesion(U(2026, 11, 5, 22, 30)) && !cor.FueraDeSesion(U(2026, 11, 5, 21, 30)));
            // contrato del grafico
            Chk("contrato", OpcionesQqq.ContratoDeCodigo("MNQZ6") == "Z6" && OpcionesQqq.ContratoDeCodigo("NQH7") == "H7" && OpcionesQqq.ContratoDeCodigo("MNQ") == "" && OpcionesQqq.ContratoDeCodigo("MNQZ26") == "Z26");
            Chk("tabla Python", OpcionesQqq.ContratoTablaPython(U(2026, 9, 13, 23, 0)) == "U6" && OpcionesQqq.ContratoTablaPython(U(2026, 9, 14, 22, 0)) == "Z6" && OpcionesQqq.ContratoTablaPython(U(2026, 12, 17, 22, 0)) == "H7");
            // oi_fresco en invierno: una foto de las 22:30 UTC (17:30 EST, todavia 'dia' en NY) no marca OI viejo en Corregida; en Python si
            Chk("oi invierno", OiViejoTras(false) && !OiViejoTras(true));
            // roll: una muestra persistida de Z6 no entra si hoy el grafico es H7; la de H7 si
            {
                var g = new DateTime(2026, 12, 21, 15, 0, 0, DateTimeKind.Utc);
                FotoCadena F(DateTime x) => new FotoCadena { Libro = "QQQ", GeneradoUtc = x, TsUtc = x.AddSeconds(-40), DatoUtc = x.AddSeconds(-940), Spot = 700, Dias = new[] { 0.5 }, Filas = new[] { new FilaCadena(700, 0, 10, 10, 0.2, 0.2, 1, 1) }, OiTotal = 20 };
                var c = new ConversionQqq(new OpcionesQqq { Corregida = true, ContratoDe = OpcionesQqq.ContratoFijo("H7") });
                c.SembrarPrecio(g.AddSeconds(-40), 29000, "Z6");
                c.SembrarPrecio(g.AddSeconds(35), 29100, "H7");
                var a = c.Observar(F(g), double.NaN);
                var b = c.Observar(F(g.AddSeconds(75)), double.NaN);
                Chk("roll semilla", double.IsNaN(a.Precio) && b.Precio == 29100);
            }
            // 'generado' con fraccion: la vigencia sale del segundo truncado
            {
                var c = new ConversionQqq(new OpcionesQqq { Corregida = true, ContratoDe = OpcionesQqq.ContratoFijo("Z6") });
                var x = new DateTime(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc).AddMilliseconds(700);
                var r = c.Observar(new FotoCadena { Libro = "QQQ", GeneradoUtc = x, TsUtc = x.AddSeconds(-40), DatoUtc = x.AddSeconds(-940), Spot = 700, Dias = new[] { 0.5 }, Filas = new[] { new FilaCadena(700, 0, 10, 10, 0.2, 0.2, 1, 1) }, OiTotal = 20 }, double.NaN);
                Chk("generado truncado", r.VigenteHastaUtc == new DateTime(2026, 10, 8, 15, 25, 0, DateTimeKind.Utc) && r.GeneradoUtc.Millisecond == 0);
            }
            Console.WriteLine($"  {(ok ? "OK " : "DIF")} UNIDAD: C9 / pausa de CME / oi_fresco en verano iguales y en invierno corridos a NY; contrato del codigo del grafico; semilla de otro contrato descartada; generado truncado al segundo");
            return ok;
        }

        // dos fotos: 20:00 UTC (dia) y 22:30 UTC del 05-11 (invierno). ¿la segunda queda 'OI de 2 sesiones'?
        private static bool OiViejoTras(bool corregida)
        {
            var c = new ConversionQqq(new OpcionesQqq { Corregida = corregida, ContratoDe = OpcionesQqq.ContratoFijo("Z6") });
            FotoCadena F(DateTime g) => new FotoCadena { Libro = "QQQ", GeneradoUtc = g, TsUtc = g.AddSeconds(-40), DatoUtc = g.AddSeconds(-940), Spot = 700, Dias = new[] { 0.5 }, Filas = new[] { new FilaCadena(700, 0, 10, 10, 0.2, 0.2, 1, 1) }, OiTotal = 20 };
            c.Observar(F(new DateTime(2026, 11, 5, 20, 0, 0, DateTimeKind.Utc)), double.NaN);
            return c.Observar(F(new DateTime(2026, 11, 5, 22, 30, 0, DateTimeKind.Utc)), double.NaN).OiViejo;
        }
    }
}
