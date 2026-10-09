// CambiosFamilia.cs — PythiaGex 4.1.2 (08-10-2026), modulo posiciones (B-pos). Pedido del operador: ver en la etiqueta si en un strike o muro
// "estan aumentando o sacando posiciones en vivo". Lo que se puede medir y lo que no (DISENO_4_1_2_MONTOS.md, NO negociable):
//   * Series por VOLUMEN: el cambio es el volumen operado en la ventana (5/15/30 min) en el MISMO (strike, vencimiento, lado), valuado con la
//     gamma de AHORA (el efecto del precio, el tiempo y la IV queda afuera). No dice si abren o cierran: cerrar tambien suma volumen.
//     Con la cadena congelada (CBOE de noche) el cambio es 0 exacto.
//   * Series por OI: OI(publicacion vigente, R0) - OI(publicacion anterior, R1) en el mismo (strike, vencimiento, lado), valuado con la gamma de
//     ahora. Es cambio de POSICIONES entre dos noches (la OCC consolida una vez por noche), no actividad de hoy.
//   * Nada de restar montos entre minutos: el perfil es lineal en VolC/VolP/OiC/OiP, asi que el cambio = PerfilLado(fotoΔ), con fotoΔ = la foto
//     de ahora con esas columnas reemplazadas por las diferencias (mismo S, misma t, misma IV). TQQQ: la misma cuenta de TqqqPerfil.Calcular.
// Unidades = las de NivelActual.GexM del mismo nivel (NQ x0,2; NDX/QQQ x1; familia = suma escalada; TQQQ = las de TqqqPerfil), SIN redondear.
// 4.1.4 (09-10-2026): la fotoΔ se valua con "aporta si CUALQUIER lado es distinto de 0" (PerfilLado.Armar porLado). Con el neto (la regla de las
//   series, que no cambia) un cambio igual de calls y puts con la misma IV se anulaba y el cambio del muro C y del muro P salia 0. Visto en el arnes
//   posiciones (P3 linealidad) con los datos de esta noche: NQ OI 10-09 05:59:55Z, K 31.160 (+9 calls, +9 puts, IV 0,206258 en los dos lados).
// 09-10-2026 (revision adversarial, arreglos):
//   * La ventana del volumen (CambioVolDesdeUtc/HastaUtc/SegReal) es la HORA DEL DATO de las dos fotos (FotoCadena.DatoUtc), no el sello:
//     en CBOE el sello va ~900 s DESPUES del dato (medido 900-912 s en NDX/QQQ del 10-08) y la pestaña rotulaba "14:43->14:58" un volumen
//     operado de 14:28 a 14:43. En NQ (Rithmic) dato = sello. El sello queda solo para buscar la foto de antes y para la tolerancia.
//   * Volumen de OTRO dia de negociacion = NaN ("volumen de otro dia de negociacion"): CBOE/TQQQ por la fecha de NY del dato, NQ por la
//     sesion de CME del sello. Antes pasaba si bajaban menos de la mitad de las claves (10-01 14:17Z NDX 30 min: 49,2 %, error 20 %).
//   * OI: salto de libro flaco y cambio sin confirmar (RegimenesOi); en CBOE/TQQQ, desde las 09:30 NY de un dia habil la publicacion
//     vigente tiene que ser de ese dia (si no: NaN "sin la publicacion de OI del ..."): un salto perdido no se publica como cambio de un dia.
//   * FAM: OI de la familia solo si las publicaciones de los tres libros son de la misma noche (sesion de CME); volumen de la familia sin
//     ventana agregada cuando las ventanas de los libros no coinciden (NQ en vivo contra CBOE de ~15 min antes): la nota dice cada una.
// 4.1.3 (09-10-2026): DOMS_QQQ_vol / DOMS_NDX_vol (rol D1/D2, la seleccion de la 2.0 sobre el libro de la 4.1) llevan el cambio NETO del strike
// (como M+/M-); las replicas de la 2.0 (R20_*) quedan NaN con nota (su S y su conversion son otras).
// La llama HostFamilia en SU hilo despues de MotorFamilia.Avanzar: devuelve foto.Copia() con Actuales = copias anotadas de los niveles y
// CambiosEstado. Nunca modifica lo que publico el motor. Recalcula solo con minuto nuevo o foto nueva (cache). Log una linea por minuto en
// %APPDATA%\ATAS\pythiagex4-cambios.log. No es seguro entre hilos (un solo hilo: el del host).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using PythiaGexCuatro.Familia.Qqq;

namespace PythiaGexCuatro.Familia
{
    public sealed class OpcionesCambios
    {
        /// <summary>Mismo modo que los minuteros (pausa de CME y vigencia de CBOE en hora de NY). false = paridad UTC fija.</summary>
        public bool Corregida = true;
        /// <summary>Tqqq41AnclaNoche (la sesion de TQQQ es "continua").</summary>
        public bool TqqqNoche = false;
        public string RutaLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "pythiagex4-cambios.log");
        /// <summary>Tope por llamada para leer la historia de OI (arranque: ~30 h de fotos de NQ y la rueda previa de CBOE). 0 = sin tope.</summary>
        public int PresupuestoHistoriaMs = 25;
        public int MinClavesSalto = 20;
        public double FraccionSalto = 0.20;
        /// <summary>Libro flaco (TQQQ: 28 claves comunes el 10-08 y bajando): salto con >= 8 claves comunes si cambio >= 80 % (RegimenesOi).</summary>
        public int MinClavesSaltoFlaco = 8;
        public double FraccionSaltoFlaco = 0.80;
        /// <summary>Cambio de OI sin confirmar (pocas claves): >= 50 % de las comunes cambiadas. Mientras sea el regimen vigente, sin ΔOI.</summary>
        public double FraccionDudoso = 0.50;
        /// <summary>CBOE/TQQQ: hora de NY desde la que la publicacion de OI del dia ya tendria que haberse visto (medido: 02:26-08:40 NY).</summary>
        public TimeSpan PublicacionCboeHastaNy = new TimeSpan(9, 30, 0);
        /// <summary>NivelActual.Strike (round 2) contra K del libro.</summary>
        public double TolStrike = 0.006;
        /// <summary>Opcional: true cuando las fuentes ya cargaron su historico (si no, el OI del dia queda "cargando la historia").</summary>
        public Func<bool> HistoriaLista;
    }

    /// <summary>Perfil por K sin escalar (M USD * 1e6 por 1 % con multiplicador 100): volumen y OI por lado. K ascendente.</summary>
    public sealed class PerfilK
    {
        public double[] K, VC, VP, OC, OP;
        public static readonly PerfilK Vacio = new PerfilK { K = Array.Empty<double>(), VC = Array.Empty<double>(), VP = Array.Empty<double>(), OC = Array.Empty<double>(), OP = Array.Empty<double>() };
        public static PerfilK De(PerfilLado p) => p == null ? Vacio : new PerfilK { K = p.K, VC = p.GvC, VP = p.GvP, OC = p.GoC, OP = p.GoP };
        public static PerfilK De(TqqqPerfil.Perfil p) => p == null ? Vacio : new PerfilK { K = p.K, VC = p.Gvc, VP = p.Gvp, OC = p.Goc, OP = p.Gop };

        /// <summary>Valor en K exacto (0 si K no aporta). lado: 'C' calls, 'P' puts, 'N' neto.</summary>
        public double Valor(double k, char lado, bool oi)
        {
            int i = ClavesCambios.Buscar(K, k, 1e-9);
            if (i < 0) return 0.0;
            if (lado == 'C') return oi ? OC[i] : VC[i];
            if (lado == 'P') return oi ? OP[i] : VP[i];
            return oi ? OC[i] + OP[i] : VC[i] + VP[i];
        }
    }

    /// <summary>Una comparacion de un libro: una ventana de volumen (Minutos 5/15/30) o el OI del dia (Minutos 0).</summary>
    public sealed class CambioVentana
    {
        public int Minutos;
        public bool Valida;
        public string Nota = "";
        public FotoCadena Antes;
        /// <summary>Volumen: hora del DATO de la foto de antes y de la de ahora (CBOE: ultimo_trade, ~900 s antes del sello; NQ: el sello).
        /// OI: inicio de R1 y de R0 (sello de la foto en que se vio cada publicacion).</summary>
        public DateTime DesdeUtc, HastaUtc;
        /// <summary>Volumen: segundos entre las horas del dato (0 con la cadena congelada). OI: NaN.</summary>
        public double SegReal = double.NaN;
        /// <summary>Volumen: segundos entre los sellos (TsUtc) de las dos fotos: solo para la tolerancia de la ventana y el log.</summary>
        public double SegSello = double.NaN;
        public int Presentes, Comparadas, Negativas, Distintas;
        public bool Congelada;
        public PerfilK Delta, Cubierto;          // cambio y monto actual de las claves comparadas (sin escalar)
        public RegimenOi R0, R1;                 // solo OI
    }

    /// <summary>Lo calculado para un libro en un minuto (NQ, NDX, QQQ o TQQQ).</summary>
    public sealed class CambiosLibro
    {
        public string Libro; public bool EsTqqq;
        public string Nota = "";                 // por que no hay nada
        public FotoCadena Ahora, AhoraOriginal;  // Ahora = la que usa el minutero (NDX/QQQ: generado al segundo); Original = la de la fuente
        public DateTime TUtc;
        public double S = double.NaN, Conv = double.NaN, Escala = double.NaN;
        public bool PorRazon;
        public PerfilK Base;                     // perfil de ahora
        public IndiceFilas Indice;
        public readonly CambioVentana[] Vol = new CambioVentana[3];
        public CambioVentana Oi;
    }

    public sealed class CambiosFamilia
    {
        public const string VERSION = "cambios 4.1.4 (09-10-2026)";
        public static readonly string[] Libros = { "NQ", "NDX", "QQQ" };
        /// <summary>Hasta cuanto antes de (ahora - W) se busca la foto de "antes" para reconocer una cadena congelada.</summary>
        public const double MIRAR_ATRAS_CONGELADA_S = 7200;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly ILibroNq _nq;
        private readonly IFuenteCboe _cboe;
        private readonly OpcionesCambios _op;
        private readonly ConversionQqq _convQqq;
        private readonly Dictionary<string, RegimenesOi> _reg = new Dictionary<string, RegimenesOi>(StringComparer.Ordinal);
        private readonly Dictionary<string, CambiosLibro> _res = new Dictionary<string, CambiosLibro>(StringComparer.Ordinal);
        private readonly Dictionary<FotoCadena, Dictionary<ClaveFila, double>> _mapasVol = new Dictionary<FotoCadena, Dictionary<ClaveFila, double>>();
        private (long, long, long, long, long, bool) _clave;
        private bool _hayClave, _pendiente, _listoVisto;
        private IReadOnlyList<string> _estado = Array.Empty<string>();
        private FotoFamilia _fotoMotor, _anotada;
        private long _claveLog = long.MinValue;

        // diagnostico (pestaña, log y arnes)
        public double MsUltimo { get; private set; }
        public double MsMax { get; private set; }
        public double MsUltimoRecalculo { get; private set; }
        /// <summary>Lo que tardo la escritura de la linea del log en el ultimo recalculo (E/S del disco; el resto es la cuenta).</summary>
        public double MsUltimoLog { get; private set; }
        public int Llamadas { get; private set; }
        public int Recalculos { get; private set; }
        public RegistroMinuto Ultimo { get; private set; }
        public IReadOnlyList<string> Estado => _estado;
        /// <summary>true si la historia de OI todavia no se termino de leer (arranque, con el presupuesto por llamada).</summary>
        public bool HistoriaPendiente => _pendiente;
        public CambiosLibro Resultado(string libro) => _res.TryGetValue(libro ?? "", out var r) ? r : null;
        public RegimenesOi Regimenes(string libro) => _reg.TryGetValue(libro ?? "", out var r) ? r : null;
        public OpcionesCambios Opciones => _op;

        public CambiosFamilia(ILibroNq nq, IFuenteCboe cboe, OpcionesCambios opciones = null)
        {
            _nq = nq; _cboe = cboe; _op = opciones ?? new OpcionesCambios();
            _convQqq = new ConversionQqq(new OpcionesQqq { Corregida = _op.Corregida });
            foreach (var lb in new[] { "NQ", "NDX", "QQQ", "TQQQ" })
                _reg[lb] = new RegimenesOi(lb, _op.MinClavesSalto, _op.FraccionSalto, _op.MinClavesSaltoFlaco, _op.FraccionSaltoFlaco, _op.FraccionDudoso);
        }

        // ================================================================== entrada
        /// <summary>La foto del motor anotada (copia). ultimo = MotorFamilia.Almacen.Ultimo; tq = la ultima vela de TQQQ con niveles (o null).
        /// Si algo falla devuelve la foto del motor tal cual (y lo anota en el log).</summary>
        public FotoFamilia Anotar(FotoFamilia foto, RegistroMinuto ultimo, TqqqVela tq)
        {
            if (foto == null) return null;
            var sw = Stopwatch.StartNew();
            Llamadas++;
            try
            {
                bool listo = Listo();
                if (listo && !_listoVisto) { _listoVisto = true; foreach (var d in _reg.Values) d.Reiniciar(); }
                long vNq = 0, vCb = 0;
                try { vNq = _nq?.Version ?? 0; } catch { }
                try { vCb = _cboe?.Version ?? 0; } catch { }
                var clave = (ultimo?.Clave ?? long.MinValue, vNq, vCb, tq?.AperturaM2Ms ?? long.MinValue, tq?.DatoUtc.Ticks ?? 0L, listo);
                bool recalc = !_hayClave || !clave.Equals(_clave) || _pendiente || !ReferenceEquals(ultimo, Ultimo);
                if (recalc)
                {
                    Recalcular(ultimo, tq, listo, sw);
                    _clave = clave; _hayClave = true; Ultimo = ultimo; Recalculos++;
                    MsUltimoRecalculo = sw.Elapsed.TotalMilliseconds;
                }
                if (!recalc && ReferenceEquals(foto, _fotoMotor) && _anotada != null) return _anotada;
                var f = foto.Copia();
                var act = foto.Actuales ?? (IReadOnlyList<NivelActual>)Array.Empty<NivelActual>();
                var l = new List<NivelActual>(act.Count);
                foreach (var n in act) l.Add(n == null ? null : AnotarNivel(n, ultimo));
                f.Actuales = l.AsReadOnly();
                f.CambiosEstado = _estado;
                _fotoMotor = foto; _anotada = f;
                return f;
            }
            catch (Exception e) { BitacoraCambios.Error(_op.RutaLog, "Anotar", e); return foto; }
            finally { MsUltimo = sw.Elapsed.TotalMilliseconds; if (MsUltimo > MsMax) MsMax = MsUltimo; }
        }

        private bool Listo()
        {
            if (_op.HistoriaLista == null) return true;
            try { return _op.HistoriaLista(); } catch { return true; }
        }

        // ================================================================== el calculo de un minuto
        private void Recalcular(RegistroMinuto u, TqqqVela tq, bool listo, Stopwatch sw)
        {
            _res.Clear();
            _pendiente = false;
            MsUltimoLog = 0;
            var est = new List<string>();
            if (u == null || !u.TieneLibros) est.Add("cambios: sin minuto con libros todavia");
            var t = u != null ? TiempoFam.DeClave(u.Clave) : default;
            if (u != null && u.TieneLibros)
                foreach (var lb in Libros)
                {
                    var m = u.MetaDe(lb);
                    if (m == null) continue;
                    CambiosLibro r;
                    try { r = CalcularLibro(lb, m, t, listo, sw); }
                    catch (Exception e) { r = new CambiosLibro { Libro = lb, Nota = "error: " + e.Message }; BitacoraCambios.Error(_op.RutaLog, "libro " + lb, e); }
                    _res[lb] = r;
                }
            if (tq != null)
            {
                try { _res["TQQQ"] = CalcularTqqq(tq, listo, sw); }
                catch (Exception e) { _res["TQQQ"] = new CambiosLibro { Libro = "TQQQ", EsTqqq = true, Nota = "error: " + e.Message }; BitacoraCambios.Error(_op.RutaLog, "TQQQ", e); }
            }
            foreach (var lb in new[] { "NQ", "NDX", "QQQ", "TQQQ" }) if (_res.TryGetValue(lb, out var r)) est.Add(EstadoLibro(r));
            if (_pendiente) est.Add("cambios de OI: leyendo la historia de las fuentes (sigue en el proximo paso)");
            _estado = est.AsReadOnly();
            if (u != null && u.Clave != _claveLog)
            {
                _claveLog = u.Clave;
                var linea = LineaLog(u, sw.Elapsed.TotalMilliseconds);
                double t0 = sw.Elapsed.TotalMilliseconds;
                BitacoraCambios.Linea(_op.RutaLog, linea);
                MsUltimoLog = sw.Elapsed.TotalMilliseconds - t0;
            }
        }

        private CambiosLibro CalcularLibro(string lb, MetaLibro m, DateTime t, bool listo, Stopwatch sw)
        {
            var r = new CambiosLibro { Libro = lb, TUtc = t, S = m.S, Conv = m.Conv, PorRazon = lb == "QQQ", Escala = ReglasFam.Escala(new LibroMinuto { Libro = lb }) };
            string motivo;
            if (lb == "NQ") { r.Ahora = r.AhoraOriginal = FotosCambios.AhoraNq(_nq, t, out motivo); }
            else { var (o, us) = FotosCambios.AhoraCboe(_cboe, lb, t, _op.Corregida, _convQqq, out motivo); r.AhoraOriginal = o; r.Ahora = us; }
            if (r.Ahora == null) { r.Nota = motivo; return r; }
            // control: la foto elegida es la del minuto (NQ: el sello; CBOE: la hora del dato)
            bool misma = lb == "NQ" ? r.Ahora.TsUtc == m.DatoUtc : r.Ahora.DatoUtc == m.DatoUtc;
            if (!misma)
            {
                r.Nota = "la foto de " + lb + " no coincide con la del minuto (" + Hms(lb == "NQ" ? r.Ahora.TsUtc : r.Ahora.DatoUtc) + " contra " + Hms(m.DatoUtc) + ")";
                r.Ahora = null; return r;
            }
            if (!(r.S > 0)) { r.Nota = "sin S del minuto"; r.Ahora = null; return r; }
            r.Indice = ClavesCambios.Indexar(r.Ahora, t);
            r.Base = Valuar(r, r.Ahora);
            for (int w = 0; w < CambiosVentanas.Min.Length; w++) r.Vol[w] = Ventana(r, CambiosVentanas.Min[w]);
            r.Oi = OiDia(r, listo, sw);
            return r;
        }

        private CambiosLibro CalcularTqqq(TqqqVela tq, bool listo, Stopwatch sw)
        {
            var r = new CambiosLibro { Libro = "TQQQ", EsTqqq = true, Escala = 1.0 };
            var f = FotosCambios.AhoraTqqq(_cboe, tq, _op.Corregida, _op.Corregida && _op.TqqqNoche, out var motivo);
            if (f == null) { r.Nota = motivo; return r; }
            r.Ahora = r.AhoraOriginal = f; r.TUtc = TiempoFam.DeMs(tq.AperturaM2Ms + 120_000L); r.S = f.Spot;
            r.Indice = ClavesCambios.Indexar(f, null);       // TqqqPerfil elige solo el vencimiento mas cercano (desde la hora del dato)
            r.Base = Valuar(r, f);
            for (int w = 0; w < CambiosVentanas.Min.Length; w++) r.Vol[w] = Ventana(r, CambiosVentanas.Min[w]);
            r.Oi = OiDia(r, listo, sw);
            return r;
        }

        /// <summary>El perfil de una foto del libro con el S y la t del minuto (TQQQ: TqqqPerfil, spot y T de la foto).
        /// porLado (4.1.4): para la fotoΔ (el cambio de cada lado) una fila aporta si cualquier lado es distinto de 0 (PerfilLado.Armar): con el neto, un
        /// cambio igual de calls y puts con la misma IV se anulaba y el cambio del muro C y del muro P de ese strike salia 0.</summary>
        public static PerfilK Valuar(CambiosLibro r, FotoCadena f, bool porLado = false)
        {
            if (r.EsTqqq) return PerfilK.De(TqqqPerfil.Calcular(f));
            return PerfilK.De(PerfilLado.Armar(FilasHoy.Armar(f, r.TUtc), r.S, r.Libro == "NQ", porLado));
        }

        // ---------------------------------------------------------------- volumen
        /// <summary>Una ventana de volumen de W min para el libro r (publica para el arnes). La foto de antes se busca por SELLO (orden de la
        /// fuente) y la tolerancia se mide en sellos; la ventana que se publica (DesdeUtc/HastaUtc/SegReal) es la HORA DEL DATO de las dos fotos.</summary>
        public CambioVentana Ventana(CambiosLibro r, int minutos)
        {
            var v = new CambioVentana { Minutos = minutos, HastaUtc = r.Ahora.DatoUtc };
            double W = minutos * 60.0, tol = W + Math.Max(120.0, W / 2.0);
            var lim = r.Ahora.TsUtc.AddSeconds(-W);
            // se mira hasta 2 h atras: si la cadena estuvo CONGELADA todo ese tiempo (mismo dato, mismo volumen), el cambio de cualquier
            // ventana adentro es 0 exacto aunque no haya foto justo de hace W min (CBOE de noche baja cada 300 s y a veces saltea una)
            var antes = FotosCambios.Antes(_nq, _cboe, r.Libro, lim, r.AhoraOriginal.GeneradoUtc, Math.Max(tol - W, MIRAR_ATRAS_CONGELADA_S));
            if (antes == null) { v.Nota = "sin foto de hace " + minutos + " min"; return v; }
            double seg = (r.Ahora.TsUtc - antes.TsUtc).TotalSeconds;           // sellos: tolerancia
            v.Antes = antes; v.DesdeUtc = antes.DatoUtc; v.SegReal = (r.Ahora.DatoUtc - antes.DatoUtc).TotalSeconds; v.SegSello = seg;
            Dictionary<ClaveFila, double> mapa = null;
            if (!ReferenceEquals(antes.Filas, r.Ahora.Filas) && !_mapasVol.TryGetValue(antes, out mapa))
            {
                if (_mapasVol.Count > 16) _mapasVol.Clear();
                mapa = ClavesCambios.Mapa(antes, false);
                _mapasVol[antes] = mapa;
            }
            CompararVolumen(r, v, antes, mapa);
            if (seg > tol && !(v.Valida && v.Congelada))
            {   // la foto mas cercana esta demasiado lejos y la cadena se movio: el cambio no se puede atribuir a la ventana
                v.Valida = false; v.Congelada = false; v.Delta = null; v.Cubierto = null;
                v.Nota = "sin foto de hace " + minutos + " min (la anterior es de hace " + Math.Round(seg / 60.0).ToString(Inv) + " min)";
            }
            return v;
        }

        /// <summary>Dia de negociacion de una foto (el volumen de opciones vuelve a cero en cada uno): NQ (Rithmic) la sesion de CME del sello
        /// (TiempoFam.Sesion, 22:00 UTC); CBOE y TQQQ la fecha de Nueva York de la hora del DATO (el sello de la noche sigue con el dato de la tarde).</summary>
        public static string DiaNegociacion(string libro, FotoCadena f)
            => libro == "NQ" ? TiempoFam.Sesion(f.TsUtc) : TiempoFam.ANy(f.DatoUtc).ToString("yyyy-MM-dd", Inv);

        /// <summary>Volumen de ahora contra el de 'antes' por clave (mapa = clave -> volumen de antes; null = mismas filas). Llena v.
        /// Foto de antes de OTRO dia de negociacion: NaN "volumen de otro dia de negociacion" (el volumen de hoy menos el total de ayer no es
        /// volumen operado en la ventana). Clave ausente en antes: fuera (baja la cobertura). Δvol &lt; 0: fuera; si bajan mas de la mitad:
        /// NaN "volumen reiniciado" (red de seguridad).</summary>
        public static void CompararVolumen(CambiosLibro r, CambioVentana v, FotoCadena antes, Dictionary<ClaveFila, double> mapa)
        {
            string diaAntes = DiaNegociacion(r.Libro, antes), diaAhora = DiaNegociacion(r.Libro, r.Ahora);
            if (diaAntes != diaAhora)
            {
                v.Nota = "volumen de otro dia de negociacion (la foto de antes es del " + diaAntes.Substring(5) + (r.Libro == "NQ" ? ", sesion de CME" : ", dato " + Dia(antes.DatoUtc))
                         + "; la de ahora del " + diaAhora.Substring(5) + ")";
                return;
            }
            var F = r.Ahora.Filas; var ix = r.Indice; int n = F.Length;
            var dC = new double[n]; var dP = new double[n]; var cC = new double[n]; var cP = new double[n]; var z = new double[n];
            bool mismas = ReferenceEquals(antes.Filas, F) || mapa == null;
            for (int i = 0; i < n; i++)
            {
                if (ix.HayC[i]) Uno(F[i].VolC, ix.C[i], ref dC[i], ref cC[i]);
                if (ix.HayP[i]) Uno(F[i].VolP, ix.P[i], ref dP[i], ref cP[i]);
            }
            // 4.1.2 (principal, 09-10): en CBOE/TQQQ, misma HORA DEL DATO en las dos fotos = no hubo negociacion entre ellas. Despues del cierre
            // CBOE alterna dos versiones de la misma cadena (medido: QQQ +-39 contratos en 10 claves, mismo ultimo_trade 16:14:59 NY, 20:55Z/
            // 21:00Z/03:50:54Z): eso NO es volumen operado. Cambio 0 exacto, con la nota que lo dice.
            if (r.Libro != "NQ" && v.Comparadas > 0 && antes.DatoUtc == r.Ahora.DatoUtc)
            {
                int raras = v.Distintas + v.Negativas;
                v.Valida = true; v.Congelada = true; v.Delta = PerfilK.Vacio;
                v.Nota = raras == 0 ? "cadena congelada" : "cadena congelada (misma hora del dato; CBOE sirvio otra version con " + raras + " claves distintas: no es volumen operado)";
                v.Cubierto = Valuar(r, ClavesCambios.ConColumnas(r.Ahora, z, z, cC, cP));
                return;
            }
            if (v.Comparadas == 0) { v.Nota = "ninguna clave de ahora estaba en la foto de hace " + v.Minutos + " min"; return; }
            if (v.Negativas * 2 > v.Comparadas)
            {
                v.Nota = "volumen reiniciado (" + v.Negativas + " de " + v.Comparadas + " claves bajaron: nuevo dia de negociacion)";
                return;
            }
            v.Valida = true;
            // congelada: nada cambio (ni bajo) y es la misma cadena (mismas filas, o en CBOE la misma hora del dato). NQ (Rithmic) nunca.
            if (v.Distintas == 0 && v.Negativas == 0 && (mismas || (r.Libro != "NQ" && antes.DatoUtc == r.Ahora.DatoUtc)))
            { v.Congelada = true; v.Nota = "cadena congelada"; }
            v.Delta = v.Distintas == 0 ? PerfilK.Vacio : Valuar(r, ClavesCambios.ConColumnas(r.Ahora, z, z, dC, dP), porLado: true);
            v.Cubierto = Valuar(r, ClavesCambios.ConColumnas(r.Ahora, z, z, cC, cP));

            void Uno(double ahora, ClaveFila k, ref double d, ref double c)
            {
                v.Presentes++;
                if (double.IsNaN(ahora)) return;
                double a;
                if (mismas) a = ahora;
                else if (!mapa.TryGetValue(k, out a)) return;
                v.Comparadas++;
                double dd = ahora - a;
                if (dd < 0) { v.Negativas++; return; }
                if (dd != 0) v.Distintas++;
                d = dd; c = ahora;
            }
        }

        // ---------------------------------------------------------------- OI del dia
        private CambioVentana OiDia(CambiosLibro r, bool listo, Stopwatch sw)
        {
            var v = new CambioVentana { Minutos = 0 };
            if (!listo) { v.Nota = "cargando la historia de las fuentes"; return v; }
            var det = _reg[r.Libro];
            if (!AvanzarDetector(det, r, sw))
            {
                _pendiente = true;
                v.Nota = "leyendo la historia de OI (" + det.ConFilas + " fotos hasta " + Dia(det.Cursor) + ")";
                return v;
            }
            CompararOi(r, v, det, _op.PublicacionCboeHastaNy);
            return v;
        }

        /// <summary>CBOE/TQQQ: fecha de NY de la publicacion de OI que ya tendria que haberse visto en t. La OCC publica el OI de cada rueda antes
        /// de la apertura del dia habil siguiente (medido 10-01..10-08: NDX 02:26-02:36, QQQ 03:31-05:12, TQQQ 05:02-08:40, hora de NY). Desde
        /// 'hasta' (09:30 NY) de un dia habil es la de ese dia; antes, la del dia habil anterior. Sabados, domingos y feriados de la bolsa no publican.</summary>
        public static DateTime PublicacionCboeEsperada(DateTime tUtc, TimeSpan hasta)
        {
            var ny = TiempoFam.ANy(tUtc);
            var d = ny.TimeOfDay >= hasta ? ny.Date : ny.Date.AddDays(-1);
            for (int i = 0; i < 15 && (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || TiempoFam.EsFeriadoBolsa(d)); i++) d = d.AddDays(-1);
            return d;
        }

        /// <summary>OI de ahora contra el de la publicacion anterior (R1). Llena v. CBOE/TQQQ (r.Libro != "NQ" y r.TUtc puesto): si la publicacion
        /// vigente (R0) no es la que ya tendria que haberse visto (PublicacionCboeEsperada), NaN: un salto perdido no se publica como cambio de un dia.
        /// R0 que empezo con un cambio SIN CONFIRMAR (RegimenOi.Dudoso): NaN hasta la proxima publicacion confirmada.</summary>
        public static void CompararOi(CambiosLibro r, CambioVentana v, RegimenesOi det) => CompararOi(r, v, det, new TimeSpan(9, 30, 0));

        public static void CompararOi(CambiosLibro r, CambioVentana v, RegimenesOi det, TimeSpan publicacionCboeHastaNy)
        {
            if (det.R0 == null) { v.Nota = "sin historia de OI"; return; }
            if (det.R1 == null) { v.Nota = "OI sin la sesion anterior en memoria (un solo OI visto, desde " + Dia(det.R0.InicioUtc) + ")"; return; }
            if (det.R0.Dudoso)
            {
                v.Nota = "el OI cambio el " + Dia(det.R0.InicioUtc) + " en " + det.R0.DistintasInicio + " de " + det.R0.ComunesInicio
                         + " claves: pocas para confirmar una publicacion nueva; sin cambio de OI hasta la proxima";
                return;
            }
            if (r.Libro != "NQ" && r.TUtc != default)
            {
                var esp = PublicacionCboeEsperada(r.TUtc, publicacionCboeHastaNy);
                if (TiempoFam.ANy(det.R0.InicioUtc).Date < esp)
                {
                    v.Nota = "sin la publicacion de OI del " + esp.ToString("MM-dd", Inv) + " (la vigente se vio el " + Dia(det.R0.InicioUtc) + "): sin cambio de OI";
                    return;
                }
            }
            v.R0 = det.R0; v.R1 = det.R1; v.DesdeUtc = det.R1.InicioUtc; v.HastaUtc = det.R0.InicioUtc;
            var F = r.Ahora.Filas; var ix = r.Indice; int n = F.Length;
            var dC = new double[n]; var dP = new double[n]; var cC = new double[n]; var cP = new double[n]; var z = new double[n];
            var r1 = det.R1.Oi;
            for (int i = 0; i < n; i++)
            {
                if (ix.HayC[i]) Uno(F[i].OiC, ix.C[i], ref dC[i], ref cC[i]);
                if (ix.HayP[i]) Uno(F[i].OiP, ix.P[i], ref dP[i], ref cP[i]);
            }
            if (v.Comparadas == 0) { v.Nota = "ninguna clave de ahora estaba en la publicacion anterior"; return; }
            v.Valida = true;
            if (!det.R1.InicioEsSalto) v.Nota = "OI anterior visto desde " + Dia(det.R1.InicioUtc) + " (su salto quedo fuera de memoria)";
            else if (det.R1.Dudoso) v.Nota = "OI anterior visto desde " + Dia(det.R1.InicioUtc) + " (empezo con un cambio sin confirmar)";
            v.Delta = v.Distintas == 0 ? PerfilK.Vacio : Valuar(r, ClavesCambios.ConColumnas(r.Ahora, dC, dP, z, z), porLado: true);
            v.Cubierto = Valuar(r, ClavesCambios.ConColumnas(r.Ahora, cC, cP, z, z));

            void Uno(double ahora, ClaveFila k, ref double d, ref double c)
            {
                v.Presentes++;
                if (double.IsNaN(ahora) || !r1.TryGetValue(k, out double a)) return;
                v.Comparadas++;
                double dd = ahora - a;
                if (dd != 0) v.Distintas++;
                d = dd; c = ahora;
            }
        }

        /// <summary>Procesa las fotos del libro hasta la de ahora (incluida). false si se corto por el presupuesto (sigue en el proximo paso).</summary>
        private bool AvanzarDetector(RegimenesOi det, CambiosLibro r, Stopwatch sw)
        {
            bool nq = r.Libro == "NQ";
            DateTime hasta = nq ? r.Ahora.TsUtc : r.AhoraOriginal.GeneradoUtc;
            if (det.Cursor >= hasta) return true;
            // OJO: LibroNqFamilia.Fotos compara en MILISEGUNDOS (un tick de mas no alcanza para incluir la foto de ahora); CBOE compara en ticks
            var paso = nq ? TimeSpan.FromMilliseconds(1) : TimeSpan.FromTicks(1);
            var desde = det.Cursor == DateTime.MinValue ? DateTime.MinValue : det.Cursor + paso;
            var l = nq ? _nq.Fotos(desde, hasta + paso) : _cboe.Fotos(r.Libro, desde, hasta + paso);
            int tope = _op.PresupuestoHistoriaMs;
            for (int i = 0; i < (l?.Count ?? 0); i++)
            {
                if (tope > 0 && i > 0 && (i & 7) == 0 && sw.ElapsedMilliseconds >= tope) return false;
                var f = l[i];
                if (f == null) continue;
                det.Procesar(f, nq ? f.TsUtc : f.GeneradoUtc);
            }
            det.Avanzado(hasta);                 // se leyo todo lo que hay hasta la foto de ahora
            return true;
        }

        // ================================================================== anotar un nivel
        private static char LadoDe(NivelActual n)
        {
            if (n.Tipo != "MUROS" && n.Tipo != "MAJORS" && n.Tipo != "DOMS" && n.Tipo != "RAZON") return '?';
            switch (n.Rol)
            {
                case "muro C": return 'C';
                case "muro P": return 'P';
                case "M+": case "M-": case "dom": case "dom (razon)": return 'N';
                // 4.1.3: la seleccion de la 2.0 sobre los libros de la 4.1 (DOMS_QQQ_vol / DOMS_NDX_vol): GEX NETO del strike, como M+/M-
                case "D1": case "D2": return n.Tipo == "DOMS" && !EsReplica20(n) ? 'N' : '?';
                default: return '?';
            }
        }

        /// <summary>4.1.3: las replicas de la 2.0 (R20_*): su S y su conversion no son las del libro de la 4.1, asi que no se anotan cambios.</summary>
        public static bool EsReplica20(NivelActual n) => n?.Serie != null && n.Serie.StartsWith("R20_", StringComparison.Ordinal);
        public const string NOTA_REPLICA20 = "replica de la 2.0: sin cambio por nivel (su conversion y su S no son las del libro de la 4.1)";

        private NivelActual AnotarNivel(NivelActual n0, RegistroMinuto u)
        {
            var n = n0.Copia();
            int nv = CambiosVentanas.Min.Length;
            n.CambioVolM = Llenar(nv, double.NaN); n.CambioVolSegReal = Llenar(nv, double.NaN);
            n.CambioVolDesdeUtc = new DateTime[nv]; n.CambioVolHastaUtc = new DateTime[nv];
            for (int w = 0; w < nv; w++) { n.CambioVolDesdeUtc[w] = DateTime.MinValue; n.CambioVolHastaUtc[w] = DateTime.MinValue; }
            n.CambioCobertura = Llenar(nv + 1, double.NaN);
            n.CambioOiDiaM = double.NaN; n.CambioOiDesdeUtc = default; n.CambioOiHastaUtc = default; n.CambioNota = "";
            if (EsReplica20(n)) { n.CambioNota = NOTA_REPLICA20; return n; }      // 4.1.3: NaN con nota
            char lado = LadoDe(n);
            if (lado == '?') return n;
            bool oi = n.Fuente == "oi";
            if (!oi && n.Fuente != "vol") return n;
            try
            {
                if (n.Libro == "familia") PonerFam(n, lado, oi, u);
                else PonerLibro(n, _res.TryGetValue(n.Libro ?? "", out var r) ? r : null, lado, oi);
            }
            catch (Exception e) { n.CambioNota = "error: " + e.Message; BitacoraCambios.Error(_op.RutaLog, "AnotarNivel", e); }
            return n;
        }

        private void PonerLibro(NivelActual n, CambiosLibro r, char lado, bool oi)
        {
            if (r == null) { n.CambioNota = "sin libro " + n.Libro + " en el minuto"; return; }
            if (r.Ahora == null || r.Base == null) { n.CambioNota = r.Nota; return; }
            int ib = ClavesCambios.Buscar(r.Base.K, n.Strike, _op.TolStrike);
            if (ib < 0) { n.CambioNota = "el strike " + NumFam.F(n.Strike) + " no esta en el perfil de ahora de " + r.Libro; return; }
            double k = r.Base.K[ib], e = r.Escala / 1e6;
            if (!oi)
            {
                var notas = new string[n.CambioVolM.Length];
                for (int w = 0; w < notas.Length; w++)
                {
                    var v = r.Vol[w];
                    notas[w] = v?.Nota ?? "sin calcular";
                    if (v == null || !v.Valida) continue;
                    n.CambioVolM[w] = v.Delta.Valor(k, lado, false) * e;
                    n.CambioVolSegReal[w] = v.SegReal; n.CambioVolDesdeUtc[w] = v.DesdeUtc; n.CambioVolHastaUtc[w] = v.HastaUtc;
                    n.CambioCobertura[w] = Cobertura(v.Cubierto, r.Base, ib, lado, false);
                }
                n.CambioNota = UnirNotas(notas);
            }
            else
            {
                var v = r.Oi;
                n.CambioNota = v?.Nota ?? "sin calcular";
                if (v == null || !v.Valida) return;
                n.CambioOiDiaM = v.Delta.Valor(k, lado, true) * e;
                n.CambioOiDesdeUtc = v.DesdeUtc; n.CambioOiHastaUtc = v.HastaUtc;
                n.CambioCobertura[CambiosVentanas.IndiceOiDia] = Cobertura(v.Cubierto, r.Base, ib, lado, true);
            }
        }

        /// <summary>FAM_MUROS: el nivel es un balde de 5 pts en precio de NQ (ReglasFam.Fusion). Cambio = suma de los cambios por libro de los
        /// strikes que HOY caen en ese balde (Fut = K + conv o K x razon, dentro del +-3 % de fut), cada uno escalado; NaN si falta algun libro.
        /// OI: NaN si las publicaciones de los libros no son de la misma noche (MismaNoche): sumar el cambio de NQ de una noche con el de NDX/QQQ
        /// de la anterior no es el cambio de posiciones de ninguna noche. Volumen: si las ventanas de los libros no coinciden (NQ en vivo, CBOE
        /// ~15 min atras) no se publica una ventana agregada (Desde/Hasta = MinValue) y la nota dice la de cada libro.</summary>
        private void PonerFam(NivelActual n, char lado, bool oi, RegistroMinuto u)
        {
            if (u == null) { n.CambioNota = "sin minuto"; return; }
            double balde = n.Precio, fut = u.FutMnq;
            int nv = oi ? 1 : n.CambioVolM.Length;
            var notas = new string[nv];
            for (int w = 0; w < nv; w++)
            {
                double d = 0, num = 0, den = 0, seg = double.NaN; string falta = null;
                DateTime desde = DateTime.MaxValue, hasta = DateTime.MinValue, desdeMax = DateTime.MinValue, hastaMin = DateTime.MaxValue;
                var pubs = new StringBuilder();
                var porLibro = new List<(string Libro, CambioVentana V)>(Libros.Length);
                foreach (var lb in Libros)
                {
                    if (!_res.TryGetValue(lb, out var r) || r?.Ahora == null || r.Base == null) { falta = lb + " (" + (r?.Nota ?? "sin libro en el minuto") + ")"; break; }
                    var v = oi ? r.Oi : r.Vol[w];
                    if (v == null || !v.Valida) { falta = lb + " (" + (v?.Nota ?? "sin calcular") + ")"; break; }
                    porLibro.Add((lb, v));
                    if (oi) pubs.Append(pubs.Length > 0 ? "; " : "").Append(lb).Append(' ').Append(Dia(v.HastaUtc)).Append(" vs ").Append(Dia(v.DesdeUtc))
                                .Append(v.R1 != null && !v.R1.InicioEsSalto ? " o antes" : "");
                    else pubs.Append(pubs.Length > 0 ? ", " : "").Append(lb).Append(' ')       // igual en las 3 ventanas: UnirNotas la deja una vez
                             .Append(v.Congelada ? "congelada (dato " + Dia(v.HastaUtc) + ")" : Dia(v.HastaUtc));
                    var B = r.Base;
                    for (int i = 0; i < B.K.Length; i++)
                    {
                        double f = r.PorRazon ? B.K[i] * r.Conv : B.K[i] + r.Conv;
                        if (!(Math.Abs(f - fut) <= fut * SeleccionFam.FILTRO)) continue;
                        if (Math.Abs(Math.Round(f / ReglasFam.FAM_GRILLA, MidpointRounding.ToEven) * ReglasFam.FAM_GRILLA - balde) > 1e-6) continue;
                        d += v.Delta.Valor(B.K[i], lado, oi) * r.Escala;
                        double bc = oi ? B.OC[i] : B.VC[i], bp = oi ? B.OP[i] : B.VP[i];
                        double cc = v.Cubierto.Valor(B.K[i], 'C', oi), cp = v.Cubierto.Valor(B.K[i], 'P', oi);
                        if (lado == 'C') { num += Math.Abs(cc) * r.Escala; den += Math.Abs(bc) * r.Escala; }
                        else if (lado == 'P') { num += Math.Abs(cp) * r.Escala; den += Math.Abs(bp) * r.Escala; }
                        else { num += (Math.Abs(cc) + Math.Abs(cp)) * r.Escala; den += (Math.Abs(bc) + Math.Abs(bp)) * r.Escala; }
                    }
                    if (v.DesdeUtc < desde) desde = v.DesdeUtc;
                    if (v.DesdeUtc > desdeMax) desdeMax = v.DesdeUtc;
                    if (v.HastaUtc > hasta) hasta = v.HastaUtc;
                    if (v.HastaUtc < hastaMin) hastaMin = v.HastaUtc;
                    if (double.IsNaN(seg) || v.SegReal > seg) seg = v.SegReal;
                }
                if (falta != null) { notas[w] = "falta " + falta; continue; }
                double cob = den > 0 ? Math.Min(1.0, num / den) : double.NaN;
                if (oi)
                {
                    if (!MismaNoche(porLibro))
                    {   // NaN: las fechas del renglon no corresponderian a ninguna comparacion de libro
                        notas[w] = "publicaciones de distinta noche por libro (" + pubs + "): sin cambio de OI de la familia";
                        continue;
                    }
                    notas[w] = "OI por libro: " + pubs;      // las fechas del nivel son la publicacion mas nueva y la anterior mas vieja, de las mismas noches
                    n.CambioOiDiaM = d / 1e6; n.CambioOiDesdeUtc = desde; n.CambioOiHastaUtc = hasta;
                    n.CambioCobertura[CambiosVentanas.IndiceOiDia] = cob;
                }
                else
                {
                    n.CambioVolM[w] = d / 1e6; n.CambioVolSegReal[w] = seg; n.CambioCobertura[w] = cob;
                    bool alineadas = (desdeMax - desde).TotalSeconds <= VENTANAS_ALINEADAS_S && (hasta - hastaMin).TotalSeconds <= VENTANAS_ALINEADAS_S;
                    if (alineadas) { n.CambioVolDesdeUtc[w] = desde; n.CambioVolHastaUtc[w] = hasta; notas[w] = ""; }
                    else notas[w] = "ventana de cada libro hasta la hora de su dato: " + pubs;     // sin ventana agregada (quedan MinValue)
                }
            }
            n.CambioNota = UnirNotas(notas);
        }

        /// <summary>Las ventanas de volumen de los libros de la familia "coinciden" si sus extremos difieren a lo sumo esto (s).</summary>
        public const double VENTANAS_ALINEADAS_S = 90;

        /// <summary>FAM OI: las publicaciones vigentes (R0) de los libros son de la misma noche (sesion de CME: NQ publica ~01:30 UTC y CBOE
        /// 06:30-12:40 UTC del mismo dia de sesion, las dos sobre la rueda anterior), y las anteriores (R1) que empezaron con un salto visto
        /// tambien (una R1 "o antes" empezo antes de lo que hay en memoria: su noche no se sabe y no se compara).</summary>
        public static bool MismaNoche(IReadOnlyList<(string Libro, CambioVentana V)> l)
        {
            string s0 = null, s1 = null;
            foreach (var (_, v) in l)
            {
                if (v?.R0 == null || v.R1 == null) return false;
                var a = TiempoFam.Sesion(v.R0.InicioUtc);
                if (s0 == null) s0 = a; else if (a != s0) return false;
                if (v.R1.InicioEsSalto && !v.R1.Dudoso)
                {
                    var b = TiempoFam.Sesion(v.R1.InicioUtc);
                    if (s1 == null) s1 = b; else if (b != s1) return false;
                }
            }
            return true;
        }

        private static double Cobertura(PerfilK cub, PerfilK bas, int ib, char lado, bool oi)
        {
            double k = bas.K[ib];
            double bc = oi ? bas.OC[ib] : bas.VC[ib], bp = oi ? bas.OP[ib] : bas.VP[ib];
            double cc = cub.Valor(k, 'C', oi), cp = cub.Valor(k, 'P', oi);
            double num, den;
            if (lado == 'C') { num = Math.Abs(cc); den = Math.Abs(bc); }
            else if (lado == 'P') { num = Math.Abs(cp); den = Math.Abs(bp); }
            else { num = Math.Abs(cc) + Math.Abs(cp); den = Math.Abs(bc) + Math.Abs(bp); }
            return den > 0 ? Math.Min(1.0, num / den) : double.NaN;
        }

        /// <summary>Una nota por ventana -> una sola: si todas dicen lo mismo, esa; si no, "5 min: ...; 30 min: ...".</summary>
        private static string UnirNotas(string[] notas)
        {
            if (notas.Length == 0) return "";
            bool iguales = true;
            for (int i = 1; i < notas.Length; i++) if (notas[i] != notas[0]) { iguales = false; break; }
            if (iguales) return notas[0] ?? "";
            var sb = new StringBuilder();
            for (int i = 0; i < notas.Length; i++)
            {
                if (string.IsNullOrEmpty(notas[i])) continue;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(CambiosVentanas.Min[i]).Append(" min: ").Append(notas[i]);
            }
            return sb.ToString();
        }

        private static double[] Llenar(int n, double v) { var a = new double[n]; for (int i = 0; i < n; i++) a[i] = v; return a; }

        // ================================================================== textos
        private static string Hms(DateTime t) => t.ToString("HH:mm:ss", Inv) + "Z";
        private static string Hm(DateTime t) => t.ToString("HH:mm", Inv);
        private static string Dia(DateTime t) => t == DateTime.MinValue ? "-" : t.ToString("MM-dd HH:mm", Inv) + "Z";

        /// <summary>Una linea por libro para la pestaña (FotoFamilia.CambiosEstado).</summary>
        private static string EstadoLibro(CambiosLibro r)
        {
            if (r.Ahora == null) return r.Libro + ": sin cambios (" + r.Nota + ")";
            var sb = new StringBuilder(160);
            sb.Append(r.Libro).Append(": ");
            bool congelada = true, alguna = false;
            foreach (var v in r.Vol) { if (v == null || !v.Valida) { congelada = false; continue; } alguna = true; congelada &= v.Congelada; }
            if (alguna && congelada) sb.Append("cadena congelada (dato ").Append(Dia(r.Ahora.DatoUtc)).Append("): sin volumen nuevo");
            else
            {
                sb.Append(r.Libro == "NQ" ? "vol" : "vol (hora del dato)");
                for (int w = 0; w < r.Vol.Length; w++)
                {
                    var v = r.Vol[w];
                    sb.Append(w == 0 ? " " : " · ").Append(CambiosVentanas.Min[w]).Append(" min ");
                    if (v != null && v.Valida) sb.Append(Hm(v.DesdeUtc)).Append('-').Append(Hm(v.HastaUtc)).Append('Z');
                    else sb.Append('(').Append(v?.Nota ?? "sin calcular").Append(')');
                }
            }
            sb.Append("; OI ");
            var o = r.Oi;
            if (o != null && o.Valida)
                sb.Append(Dia(o.R0.InicioUtc)).Append(" vs ").Append(Dia(o.R1.InicioUtc)).Append(o.R1.InicioEsSalto ? (o.R1.Dudoso ? " (sin confirmar)" : "") : " o antes")
                  .Append(" (").Append(o.Comparadas).Append(" de ").Append(o.Presentes).Append(" claves)");
            else sb.Append(o?.Nota ?? "sin calcular");
            return sb.ToString();
        }

        private string LineaLog(RegistroMinuto u, double ms)
        {
            var sb = new StringBuilder(600);
            sb.Append("k ").Append(u.Clave).Append(' ').Append(Hm(TiempoFam.DeClave(u.Clave))).Append("Z");
            foreach (var lb in new[] { "NQ", "NDX", "QQQ", "TQQQ" })
            {
                if (!_res.TryGetValue(lb, out var r)) continue;
                sb.Append(" || ").Append(lb);
                if (r.Ahora == null) { sb.Append(" sin foto: ").Append(r.Nota); continue; }
                sb.Append(" ahora ").Append(Hms(r.Ahora.TsUtc)).Append(" dato ").Append(Hms(r.Ahora.DatoUtc)).Append(" S ").Append(NumFam.F(r.S, "0.####"))
                  .Append(" filas ").Append(r.Ahora.Filas.Length);
                foreach (var v in r.Vol)
                {
                    if (v == null) continue;
                    sb.Append(" | ").Append(v.Minutos).Append("m ");
                    if (v.Antes != null) sb.Append("antes ").Append(Hms(v.Antes.TsUtc)).Append(" dato ").Append(Hms(v.Antes.DatoUtc)).Append(' ')
                                           .Append(NumFam.F(v.SegSello, "0")).Append("s sello/").Append(NumFam.F(v.SegReal, "0")).Append("s dato ");
                    sb.Append("cmp ").Append(v.Comparadas).Append('/').Append(v.Presentes).Append(" neg ").Append(v.Negativas).Append(" dist ").Append(v.Distintas);
                    if (v.Congelada) sb.Append(" congelada");
                    if (!v.Valida) sb.Append(" NaN: ").Append(v.Nota);
                }
                var o = r.Oi;
                if (o != null)
                {
                    sb.Append(" | OI ");
                    var dt = _reg.TryGetValue(lb, out var rg) ? rg : null;
                    var r0 = o.R0 ?? dt?.R0; var r1 = o.R1 ?? dt?.R1;
                    if (r0 != null) sb.Append("R0 ").Append(Dia(r0.InicioUtc)).Append(r0.Dudoso ? "(sin confirmar)" : r0.InicioEsSalto ? "(salto)" : "(primero)");
                    if (r1 != null) sb.Append(" R1 ").Append(Dia(r1.InicioUtc)).Append(r1.Dudoso ? "(sin confirmar)" : r1.InicioEsSalto ? "(salto)" : "(primero)");
                    if (dt != null && dt.Dudosos > 0) sb.Append(" dudosos ").Append(dt.Dudosos);
                    sb.Append(" cmp ").Append(o.Comparadas).Append('/').Append(o.Presentes).Append(" dist ").Append(o.Distintas);
                    if (!o.Valida) sb.Append(" NaN: ").Append(o.Nota);
                }
            }
            sb.Append(" || ").Append(NumFam.F(ms, "0.0")).Append(" ms");
            if (_pendiente) sb.Append(" (historia de OI pendiente)");
            return sb.ToString();
        }
    }

    /// <summary>Log propio (una linea por minuto; errores con freno de 60 s por lugar). Rota a los 4 MB.</summary>
    internal static class BitacoraCambios
    {
        private static readonly object _l = new object();
        private static readonly Dictionary<string, DateTime> _ult = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        public static void Linea(string ruta, string msg)
        {
            try
            {
                if (string.IsNullOrEmpty(ruta)) return;
                lock (_l)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                    var fi = new FileInfo(ruta);
                    if (fi.Exists && fi.Length > 4_000_000) { try { File.Copy(ruta, ruta + ".1", true); File.Delete(ruta); } catch { } }
                    File.AppendAllText(ruta, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC " + msg + "\n", new UTF8Encoding(false));
                }
            }
            catch { }
        }

        public static void Error(string ruta, string lugar, Exception e)
        {
            lock (_l)
            {
                if (_ult.TryGetValue(lugar, out var t) && (DateTime.UtcNow - t).TotalSeconds < 60) return;
                _ult[lugar] = DateTime.UtcNow;
            }
            Linea(ruta, "[" + lugar + "] ERROR " + e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Replace("\r", "").Replace("\n", " | "));
        }
    }
}
