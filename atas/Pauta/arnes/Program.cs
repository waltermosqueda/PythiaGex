using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Pauta;

namespace PautaArnes
{
    /// <summary>
    /// EL ARNES: corre el MISMO PautaNucleo.cs que va adentro de ATAS sobre las velas exportadas por
    /// laboratorio/pauta/p_paridad_pauta.py (las mismas filas que uso la medicion) y escribe niveles, ordenes y
    /// operaciones de P1 y P2 para compararlas contra P1_niveles_*.csv, P1_ops_*.csv y P2_ops_*.csv.
    ///
    ///   PautaArnes &lt;velas.csv&gt; &lt;prefijo_salida&gt; &lt;D&gt; &lt;stop&gt; &lt;objetivo&gt; &lt;rollSpread&gt; &lt;rollMedido 0/1&gt; &lt;topeP2&gt;
    ///
    /// velas.csv: t,o,h,l,c,bid,ask,contrato,paso  (t = segundos UTC desde 1970; bid/ask vacios = sin punta;
    /// paso = duracion de la barra en segundos: 1 o 60). No toca ATAS ni %APPDATA%, no usa red.
    /// </summary>
    public static class Program
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static int Main(string[] a)
        {
            if (a.Length < 8)
            {
                Console.Error.WriteLine("uso: PautaArnes velas.csv prefijo D stop objetivo rollSpread rollMedido(0/1) topeP2");
                return 2;
            }
            var p = new Parametros
            {
                D = double.Parse(a[2], Inv), Stop = double.Parse(a[3], Inv), Objetivo = double.Parse(a[4], Inv),
                RollSpread = double.Parse(a[5], Inv), RollMedido = a[6] == "1", TopeP2 = double.Parse(a[7], Inv),
            };
            var n = new PautaNucleo(p);
            long filas = 0;
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            using (var r = new StreamReader(a[0]))
            {
                r.ReadLine();
                string s;
                while ((s = r.ReadLine()) != null)
                {
                    if (s.Length == 0) continue;
                    var f = s.Split(',');
                    long t = long.Parse(f[0], Inv);
                    double o = double.Parse(f[1], Inv), h = double.Parse(f[2], Inv), l = double.Parse(f[3], Inv), c = double.Parse(f[4], Inv);
                    double bid = f[5].Length == 0 ? double.NaN : double.Parse(f[5], Inv);
                    double ask = f[6].Length == 0 ? double.NaN : double.Parse(f[6], Inv);
                    int paso = int.Parse(f[8], Inv);
                    n.Agregar(Barra.Crear(t, o, h, l, c, bid, ask, string.Intern(f[7]), paso));
                    filas++;
                }
            }
            Escribir(n, a[1]);
            int ops1 = 0, ops2 = 0, ords = 0;
            foreach (var se in n.Sesiones) { ops1 += se.OpsP1.Count; ops2 += se.OpsP2.Count; ords += se.Ordenes.Count; }
            Console.WriteLine(string.Format(Inv, "filas {0}, rellenos {1}, sesiones {2}, ordenes P1 {3}, operaciones P1 {4}, operaciones P2 {5}, {6:0.0} s",
                filas, n.Rellenos, n.Sesiones.Count, ords, ops1, ops2, reloj.Elapsed.TotalSeconds));
            return 0;
        }

        private static string R(double x) => double.IsNaN(x) ? "" : x.ToString("R", Inv);
        private static string Res(int paso) => paso >= 60 ? "1m" : "1s";

        private static void Escribir(PautaNucleo n, string pre)
        {
            var b = new StringBuilder(1 << 20);
            b.Append("sesion,res,contrato,P0,precio,lado,familia,tipo,dist,notas,n_ordenes\n");
            foreach (var s in n.Sesiones)
            {
                if (!s.Congelada) continue;
                if (s.Ordenes.Count == 0)
                {
                    b.Append(s.SesionIso).Append(',').Append(Res(s.Paso)).Append(',').Append(s.Contrato).Append(',').Append(R(s.P0))
                     .Append(",,0,(ninguna dentro de D),,,").Append(s.Notas).Append(",0\n");
                    continue;
                }
                foreach (var od in s.Ordenes)
                    b.Append(s.SesionIso).Append(',').Append(Res(s.Paso)).Append(',').Append(s.Contrato).Append(',').Append(R(s.P0)).Append(',')
                     .Append(R(od.PrecioInicial)).Append(',').Append(od.Lado).Append(',').Append(od.Familia).Append(',').Append(od.Tipo).Append(',')
                     .Append(R(od.PrecioInicial - s.P0)).Append(',').Append(s.Notas).Append(',').Append(s.Ordenes.Count).Append('\n');
            }
            File.WriteAllText(pre + "_niveles.csv", b.ToString());

            b.Clear().Append("sesion,precio,familia,tipo,lado,dist,orden\n");
            foreach (var s in n.Sesiones)
                foreach (var c in s.Candidatos)
                    b.Append(s.SesionIso).Append(',').Append(R(c.Precio)).Append(',').Append(c.Familia).Append(',').Append(c.Tipo).Append(',')
                     .Append(c.Lado).Append(',').Append(R(c.Dist)).Append(',').Append(c.Orden ? 1 : 0).Append('\n');
            File.WriteAllText(pre + "_candidatos.csv", b.ToString());

            b.Clear().Append("sesion,res,contrato,n_op,t_ent,t_sal,lado,familia,tipo,nivel,entrada,salida,motivo,entrada_por,be_armado,bruto,dur_s,P0,notas,relleno\n");
            foreach (var s in n.Sesiones)
                foreach (var op in s.OpsP1)
                    b.Append(s.SesionIso).Append(',').Append(Res(s.Paso)).Append(',').Append(s.Contrato).Append(',').Append(op.NOp).Append(',')
                     .Append(RelojNy.Hms(op.SodEnt)).Append(',').Append(op.SodSal < 0 ? "" : RelojNy.Hms(op.SodSal)).Append(',')
                     .Append(op.Lado).Append(',').Append(op.Familia).Append(',').Append(op.Tipo).Append(',').Append(R(op.Nivel)).Append(',')
                     .Append(R(op.Entrada)).Append(',').Append(R(op.Salida)).Append(',').Append(op.Motivo).Append(',').Append(op.EntradaPor).Append(',')
                     .Append(op.BeArmado ? "True" : "False").Append(',').Append(R(op.Bruto)).Append(',').Append(op.DurS).Append(',')
                     .Append(R(op.P0)).Append(',').Append(s.Notas).Append(',').Append(op.EntradaEnRelleno ? 1 : 0).Append('\n');
            File.WriteAllText(pre + "_ops_P1.csv", b.ToString());

            b.Clear().Append("sesion,contrato,marco_s,lado,sod_ent,entrada,sod_sal,salida,motivo,be_activado,bruto,dur_s,n4_hi,n4_lo,reingreso,nivel,relleno\n");
            foreach (var s in n.Sesiones)
                foreach (var op in s.OpsP2)
                    b.Append(s.SesionIso).Append(',').Append(s.Contrato).Append(',').Append(s.Paso).Append(',').Append(op.Lado).Append(',')
                     .Append(op.SodEnt).Append(',').Append(R(op.Entrada)).Append(',').Append(op.SodSal).Append(',').Append(R(op.Salida)).Append(',')
                     .Append(op.Motivo).Append(',').Append(op.BeArmado ? "True" : "False").Append(',').Append(R(op.Bruto)).Append(',').Append(op.DurS).Append(',')
                     .Append(R(s.N4Hi)).Append(',').Append(R(s.N4Lo)).Append(',').Append(op.NOp - 1).Append(',').Append(op.NivelP2).Append(',')
                     .Append(op.EntradaEnRelleno ? 1 : 0).Append('\n');
            File.WriteAllText(pre + "_ops_P2.csv", b.ToString());

            b.Clear().Append("sesion,contrato,paso,t0_utc,utc_congela,P0,n4_hi,n4_lo,n_min_ema,notas,n_ordenes,n_ops_p1,n_ops_p2,ln_venta,ln_compra\n");
            foreach (var s in n.Sesiones)
                b.Append(s.SesionIso).Append(',').Append(s.Contrato).Append(',').Append(s.Paso).Append(',').Append(s.T0Utc).Append(',').Append(s.UtcCongela).Append(',')
                 .Append(R(s.P0)).Append(',').Append(R(s.N4Hi)).Append(',').Append(R(s.N4Lo)).Append(',').Append(s.NMinEma).Append(',').Append(s.Notas).Append(',')
                 .Append(s.Ordenes.Count).Append(',').Append(s.OpsP1.Count).Append(',').Append(s.OpsP2.Count).Append(',')
                 .Append(s.LnVenta == null ? "" : R(s.LnVenta.X1) + "|" + R(s.LnVenta.Y1) + "|" + R(s.LnVenta.X2) + "|" + R(s.LnVenta.Y2)).Append(',')
                 .Append(s.LnCompra == null ? "" : R(s.LnCompra.X1) + "|" + R(s.LnCompra.Y1) + "|" + R(s.LnCompra.X2) + "|" + R(s.LnCompra.Y2)).Append('\n');
            File.WriteAllText(pre + "_sesiones.csv", b.ToString());
        }
    }
}
