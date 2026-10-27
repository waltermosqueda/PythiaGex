// GammalitoFam.cs — PythiaGex 5.0.3 (27-10-2026), modulo gammalito. Pedido del operador (textual): "agrega a la 5.0 las barras por strike al estilo
// Gammalito (izquierda gamma x volumen neto de SPX y SPY, amarillos y zero), bien convertidas a ES".
// La formula sale de la ingenieria inversa de 10 capturas (_referencia/2026-10-27-gammalito-ES/INFORME.md; MEDIDO contra la captura de las 12:33 NY:
// R2 0,998 de los largos y 14/14 colores en SPX, 0,996 y 7/7 en SPY):
//   * valor de un strike = gamma Black-Scholes x (vol calls - vol puts), cada lado con la IV de su contrato = Gv del LibroMinuto de la 5.0
//     (Perfil.cs: GvC + GvP, calls +, puts -), en M USD por 1 % (x ReglasFam.Escala / 1e6). Dos libros: SPX 0DTE (el horizonte Hoy de la 5.0) en
//     K + base C7 del minuto y SPY en K x razon C8 del minuto: los mismos precios que MUROS_SPX_vol / MUROS_SPY_vol (LibroMinuto.Fut).
//   * BARRAS (Perfil): una por strike del libro (los del LibroMinuto: a <= 3 % del fut), con el largo relativo a la mas larga de SU libro. Las arma
//     el motor en su hilo (MotorFamilia.CalcularMinuto) y van en la foto (FotoFamilia.PerfilGl): la pantalla solo pone coordenadas.
//   * AMARILLOS (GL_SPX_dom, GL_SPY_dom): las 2 barras mas largas (|valor|) de cada libro, sin importar el lado; D1 la mas larga. Informe §3: en 192
//     minutos 09:30-12:42 NY, 92 % de las marcas predichas visibles encontradas (sin ventana; con +-20/25/30 pts peor).
//   * ZERO (GL_SPX_zero): el cambio de signo del perfil SPX por strike, interpolado lineal entre los dos strikes, el cruce MAS CERCANO al precio del
//     minuto (fut del MES), llevado a ES con la base del minuto. Un strike con valor 0 no es cruce (np.sign: como scripts/comun.zero_por_signo).
//     Informe §5: con la base de cada minuto de Gammalito 73 % <= 0,5 pt.
// Diferencias conocidas con Gammalito (no se pueden cerrar con estos datos): nuestro CBOE llega ~15 min tarde (Gammalito tiene el dato en vivo: su perfil
// de las 12:33 NY calza con nuestra foto de dato 12:32:29, que la 5.0 usa a las ~12:48); la base y la razon son las suavizadas de la 5.0 (C7/C8), no la
// muestra cruda de cada minuto (su ruido de +-0,4 pt no correlaciona con nada del ES: informe §4); la estela de la 5.0 dibuja por vela de 2 min
// (HistoriaM2: el minuto de apertura de cada vela m2), Gammalito un punto por minuto. SIN MEDIR EN ES como nivel.
// MEDIDO con la DLL (arnes P8, datos reales del 27-10, contra las capturas de Gammalito, a la MISMA hora de reloj): barras a las 16:30-16:35Z contra la
// captura de las 12:33 NY R2 0,987-0,991 y 14/14 colores en SPX, 0,977-0,983 y 7/7 en SPY (la gamma al precio y la hora del minuto pesa mas que los
// 15 min de volumen: la foto del MISMO dato que Gammalito, evaluada a las 16:49Z cuando la 5.0 la tiene, da 0,876); amarillos (192 minutos) 80 % de los
// visibles a <= 0,9 pt, brillo D1/D2 59 %; zero mediana 1,55 pts (8 % a <= 0,5 pt): el cruce se mueve con el volumen al dinero y llega 15 min tarde.
// Series EXTRA (CatalogoFamilia.Extra, AL FINAL): no entran en Calc ni en CONF/FAM; se guardan en el niv con sus ids. Va DESPUES de la Replica20 y de
// la clasica en ExtrasCompuestos y AGREGA sus series sin tocar las demas (como ClasicaSpx.Guardar). Un minuto del archivo sin estas series no se
// completa: el perfil necesita los libros del minuto, que ya no estan (Completar = false). Sin referencias a ATAS.
// 5.0.4 (27-10-2026), pedido del operador (textual): "agregá el perfil estilo Gammalito también con el libro de ES de Rithmic (barras, amarillos y
// zero), en vivo las 24 horas". La MISMA cuenta sobre el LibroMinuto ES de la 5.0 (opciones sobre el futuro ES de CME por Rithmic, una foto por
// minuto, sin el atraso de CBOE): valor = Gv del libro ES (gamma Black-76 sin descuento x (vol calls - vol puts), el horizonte Hoy de ESE libro) x
// ReglasFam.Escala (ES 0,5: 50 USD por punto) / 1e6; precio = Fut = K + corrimiento de contrato de la 5.0 (0 con el libro del mismo trimestre que el
// grafico). BARRAS en PerfilGl.BarrasEs (la pantalla las ancla a la DERECHA, contra la columna de etiquetas); series GL_ES_dom (top-2 |valor|, D1 y
// D2) y GL_ES_zero (el cruce mas cercano a S = fut - corrimiento, + corrimiento) AL FINAL de CatalogoFamilia.Extra. Lo de SPX/SPY no cambia (mismas
// series, mismas barras, mismo orden); un minuto con SOLO el libro ES ahora tambien publica perfil (Barras vacio, BarrasEs con las de ES). De noche,
// despues del vencimiento de las 16:00 NY, el horizonte Hoy es el vencimiento siguiente, con el volumen que lleva la sesion. SIN MEDIR EN ES.
using System;
using System.Collections.Generic;

namespace PythiaGexCinco.Familia
{
    public sealed class GammalitoFam : IExtrasMinuto
    {
        public const string SERIE_SPX_DOM = "GL_SPX_dom", SERIE_SPY_DOM = "GL_SPY_dom", SERIE_SPX_ZERO = "GL_SPX_zero";
        /// <summary>5.0.4: las del libro ES de Rithmic.</summary>
        public const string SERIE_ES_DOM = "GL_ES_dom", SERIE_ES_ZERO = "GL_ES_zero";

        /// <summary>El minuto recien calculado: los amarillos de SPX y de SPY y el zero de SPX, con los libros del minuto (bk). No toca r.Series ni bk.
        /// 5.0.4: + los amarillos y el zero del libro ES (las mismas funciones: el libro ES ya esta en precio del futuro, Conv = corrimiento).</summary>
        public void Calcular(RegistroMinuto r, DateTime tUtc, double fut, IReadOnlyList<LibroMinuto> bk)
        {
            if (r == null || bk == null) return;
            Nivel[] dSpx = null, dSpy = null, z = null, dEs = null, zEs = null; double[] kSpx = null, kSpy = null, kz = null, kEs = null, kzEs = null;
            foreach (var b in bk)
            {
                if (b == null) continue;
                if (b.Libro == "SPX") { (dSpx, kSpx) = Dominantes(b); (z, kz) = ZeroNivel(b); }
                else if (b.Libro == "SPY") (dSpy, kSpy) = Dominantes(b);
                else if (b.Libro == "ES") { (dEs, kEs) = Dominantes(b); (zEs, kzEs) = ZeroNivel(b); }   // 5.0.4
            }
            if (dSpx == null && dSpy == null && z == null && dEs == null && zEs == null) return;
            Guardar(r, (SERIE_SPX_DOM, dSpx, kSpx), (SERIE_SPY_DOM, dSpy, kSpy), (SERIE_SPX_ZERO, z, kz), (SERIE_ES_DOM, dEs, kEs), (SERIE_ES_ZERO, zEs, kzEs));
        }

        /// <summary>Un minuto del archivo: sin los libros del minuto no hay perfil (no se inventa). false.</summary>
        public bool Completar(RegistroMinuto r, DateTime tUtc) => false;

        // ------------------------------------------------------------------ la cuenta (publica: la usan el motor y el arnes)
        /// <summary>El valor de cada strike del libro en M USD por 1 %: Gv x ReglasFam.Escala / 1e6 (gamma BS x (vol calls - vol puts)).</summary>
        public static double Valor(LibroMinuto b, int i) => b.Gv[i] * ReglasFam.Escala(b) / 1e6;

        /// <summary>Indices de las 2 barras de mayor |valor| (D1 primero; empate: el strike mas bajo). Sin valores distintos de 0: vacio.</summary>
        public static int[] TopDos(LibroMinuto b)
        {
            if (b?.Gv == null || b.Fut == null) return Array.Empty<int>();
            int i1 = -1, i2 = -1; double a1 = 0, a2 = 0;
            for (int i = 0; i < b.Gv.Length && i < b.Fut.Length; i++)
            {
                double a = Math.Abs(b.Gv[i]);
                if (!(a > 0) || double.IsInfinity(a)) continue;
                if (i1 < 0 || a > a1) { i2 = i1; a2 = a1; i1 = i; a1 = a; }
                else if (i2 < 0 || a > a2) { i2 = i; a2 = a; }
            }
            return i1 < 0 ? Array.Empty<int>() : i2 < 0 ? new[] { i1 } : new[] { i1, i2 };
        }

        /// <summary>El cruce de signo del perfil (Gv por strike, K ascendente) mas cercano a S, interpolado lineal; en el eje del libro (K). NaN si no hay.
        /// Empate de distancia: el de abajo. Un 0 exacto no cruza (como np.sign).</summary>
        public static double Zero(LibroMinuto b)
        {
            if (b?.Gv == null || b.K == null || double.IsNaN(b.S)) return double.NaN;
            int n = Math.Min(b.Gv.Length, b.K.Length);
            double mejor = double.NaN, dm = double.PositiveInfinity;
            for (int i = 0; i + 1 < n; i++)
            {
                double v0 = b.Gv[i], v1 = b.Gv[i + 1];
                if (double.IsNaN(v0) || double.IsNaN(v1) || !(Math.Sign(v0) * Math.Sign(v1) < 0)) continue;   // Math.Sign(NaN) tira
                double z = b.K[i] + (b.K[i + 1] - b.K[i]) * (-v0) / (v1 - v0);
                double d = Math.Abs(z - b.S);
                if (d < dm) { dm = d; mejor = z; }
            }
            return mejor;
        }

        /// <summary>D1/D2: precio round(Fut, 2), monto round(valor, 1) con signo, strike round(K, 2).</summary>
        public static (Nivel[] Lv, double[] Ks) Dominantes(LibroMinuto b)
        {
            var ix = TopDos(b);
            if (ix.Length == 0) return (null, null);
            var lv = new Nivel[ix.Length]; var ks = new double[ix.Length];
            for (int j = 0; j < ix.Length; j++)
            {
                lv[j] = new Nivel(NumPy.RoundPy(b.Fut[ix[j]], 2), "D" + (j + 1), NumPy.RoundPy(Valor(b, ix[j]), 1));
                ks[j] = NumPy.RoundPy(b.K[ix[j]], 2);
            }
            return (lv, ks);
        }

        /// <summary>El zero como nivel "Z": precio round(zero en ES, 2) (SPX: zero + base; SPY: zero x razon; 5.0.4, ES: zero + corrimiento), sin monto;
        /// strike = el zero en el eje del libro, round 2.</summary>
        public static (Nivel[] Lv, double[] Ks) ZeroNivel(LibroMinuto b)
        {
            double z = Zero(b);
            if (double.IsNaN(z) || double.IsNaN(b.Conv)) return (null, null);
            double p = b.PorRazon ? z * b.Conv : z + b.Conv;
            return (new[] { new Nivel(NumPy.RoundPy(p, 2), "Z", double.NaN) }, new[] { NumPy.RoundPy(z, 2) });
        }

        /// <summary>El perfil de las barras del minuto (SPY primero, despues SPX: el orden de dibujo). null si no hay ni SPX ni SPY con valores.
        /// 5.0.4: + las del libro ES en BarrasEs (aparte: la pantalla las ancla a la derecha); null solo si no hay barras de NINGUN libro.</summary>
        public static PerfilGl Perfil(long clave, IReadOnlyList<LibroMinuto> bk)
        {
            if (bk == null) return null;
            LibroMinuto spx = null, spy = null, es = null;
            foreach (var b in bk) { if (b == null) continue; if (b.Libro == "SPX") spx = b; else if (b.Libro == "SPY") spy = b; else if (b.Libro == "ES") es = b; }
            if (spx == null && spy == null && es == null) return null;
            var l = new List<BarraGl>((spx?.Fut?.Length ?? 0) + (spy?.Fut?.Length ?? 0));
            double mSpy = Agregar(l, spy, true), mSpx = Agregar(l, spx, false);
            var le = new List<BarraGl>(es?.Fut?.Length ?? 0);                                         // 5.0.4: el libro ES de Rithmic
            double mEs = Agregar(le, es, false);
            if (l.Count == 0 && le.Count == 0) return null;
            return new PerfilGl
            {
                Clave = clave, Barras = l.ToArray(), MaxSpx = mSpx, MaxSpy = mSpy,
                ConvSpx = spx?.Conv ?? double.NaN, ConvSpy = spy?.Conv ?? double.NaN,
                DatoSpx = spx?.DatoUtc ?? default, DatoSpy = spy?.DatoUtc ?? default,
                BarrasEs = le.ToArray(), MaxEs = mEs, ConvEs = es?.Conv ?? double.NaN, DatoEs = es?.DatoUtc ?? default,   // 5.0.4
            };
        }

        /// <summary>Las barras de un libro (valor distinto de 0) con su largo relativo a la mas larga del libro. Devuelve ese maximo (NaN si no hay).</summary>
        private static double Agregar(List<BarraGl> l, LibroMinuto b, bool spy)
        {
            if (b?.Gv == null || b.Fut == null || b.K == null) return double.NaN;
            int n = Math.Min(b.Gv.Length, Math.Min(b.Fut.Length, b.K.Length));
            double max = 0;
            for (int i = 0; i < n; i++) { double a = Math.Abs(Valor(b, i)); if (a > max && !double.IsInfinity(a)) max = a; }
            if (!(max > 0)) return double.NaN;
            for (int i = 0; i < n; i++)
            {
                double v = Valor(b, i);
                if (double.IsNaN(v) || v == 0 || double.IsInfinity(v) || double.IsNaN(b.Fut[i])) continue;
                l.Add(new BarraGl(b.Fut[i], v, v / max, b.K[i], spy));
            }
            return max;
        }

        /// <summary>Pone las series en su lugar SIN tocar las demas extras (las de la Replica20 y la clasica, o las del archivo); MetaExtra no cambia
        /// (la edad y la conversion de estas series son las de su libro de la 5.0: RegistroMinuto.Meta).</summary>
        private static void Guardar(RegistroMinuto r, params (string Id, Nivel[] Lv, double[] Ks)[] s)
        {
            int n = CatalogoFamilia.Extra.Length;
            var ex = new Nivel[n][]; var kk = new double[n][];
            if (r.Extra != null) Array.Copy(r.Extra, ex, Math.Min(n, r.Extra.Length));
            if (r.ExtraStrikes != null) Array.Copy(r.ExtraStrikes, kk, Math.Min(n, r.ExtraStrikes.Length));
            foreach (var (id, lv, ks) in s)
            {
                int i = CatalogoFamilia.IndiceExtra(id);
                if (i < 0) continue;
                ex[i] = lv; kk[i] = ks;
            }
            r.Extra = ex; r.ExtraStrikes = kk;
        }
    }
}
