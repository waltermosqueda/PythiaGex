using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace PythiaGexDos
{
    /// <summary>La razon futuro/libro de un libro por razon (vela alineada + mediana de la rueda). Una por libro:
    /// la del indicador (Libro = CBOE_ETF) y una por cada capa extra (15-09).</summary>
    public sealed class RazonEtf
    {
        public readonly List<double> Obs = new();
        public double Rueda = double.NaN;
        // F2 (2.0.1): la ULTIMA razon valida (vela alineada con cadena fresca), en memoria y en un archivo chico
        // (%APPDATA%\ATAS\PythiaGex2\razon-<raiz del futuro>-<ticker>.txt, p. ej. razon-NQ-SPY.txt; 2.0.2: antes era
        // razon-<ticker>.txt y la capa SPY de un grafico de MNQ (NQ/SPY ~ 45) y la de uno de MES (ES/SPY ~ 10) se pisaban)
        // para que sobreviva al reinicio. Con la cadena vieja (CBOE
        // congelada de noche) el 18-09 04:02-05:22 una instancia uso NQ_ahora / spot_viejo = 41,80 contra 41,55: +0,8 %,
        // ~240 pts de NQ en todos los niveles de la capa QQQ durante una hora y pico. Nunca mas esa division.
        public double Ultima = double.NaN;
        public DateTime UltimaUtc = DateTime.MinValue;
        /// <summary>Clave del archivo: raiz del futuro + ticker ("NQ-QQQ", "ES-SPY"...). null = no persiste (los RazonEtf temporales del rebobinado).</summary>
        public string Ticker;
        public DateTime UltimoAvisoUtc = DateTime.MinValue;   // para no loguear la guardia mas de una vez cada 5 min
        private bool _cargada; private DateTime _ultimoGuardadoUtc = DateTime.MinValue;

        private static string Ruta(string ticker) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex2", "razon-" + ticker + ".txt");

        /// <summary>Lee la ultima razon guardada de este ticker (una vez). Solo vale si tiene menos de 3 dias.</summary>
        public void CargarSiHaceFalta()
        {
            if (_cargada || string.IsNullOrEmpty(Ticker)) return;
            _cargada = true;
            try
            {
                var p = Ruta(Ticker);
                if (!File.Exists(p)) return;
                var partes = File.ReadAllText(p).Trim().Split('|');
                if (partes.Length < 2) return;
                if (!double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v <= 0) return;
                if (!DateTime.TryParse(partes[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)) return;
                if ((DateTime.UtcNow - t).TotalDays > 3) return;
                lock (Obs) { if (double.IsNaN(Ultima) || t > UltimaUtc) { Ultima = v; UltimaUtc = t; } }
            }
            catch { }
        }

        /// <summary>Anota una razon valida y la guarda en el archivo (a lo sumo una escritura por minuto, fuera de locks).</summary>
        public void AnotarValida(double razon, DateTime utc)
        {
            bool escribir;
            lock (Obs)
            {
                if (utc < UltimaUtc) return;   // nunca hacia atras
                Ultima = razon; UltimaUtc = utc;
                escribir = !string.IsNullOrEmpty(Ticker) && (utc - _ultimoGuardadoUtc).TotalSeconds >= 60;
                if (escribir) _ultimoGuardadoUtc = utc;
            }
            if (!escribir) return;
            try
            {
                var p = Ruta(Ticker);
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, razon.ToString("R", CultureInfo.InvariantCulture) + "|" + utc.ToString("o", CultureInfo.InvariantCulture));
            }
            catch { }
        }
    }

    /// <summary>La pizarra compartida del DLL (15-09): cada grafico con Gamma Hoy anota su ultimo precio por minuto
    /// bajo su raiz (ES, NQ, RTY). Con eso el grafico de NQ mide la beta NQ/ES con las velas de los DOS mercados a la
    /// misma hora, sin depender del spot del libro (que llega cada 5 min por el cache de la nube y salio con r2 0).
    /// Vive mientras ATAS esta abierto; con un solo grafico no hay beta y se dice.</summary>
    public static class VelasCompartidas
    {
        private static readonly object _llave = new();
        private static readonly Dictionary<string, SortedDictionary<long, double>> _series = new();

        public static void Anotar(string raiz, DateTime utc, double precio)
        {
            if (string.IsNullOrEmpty(raiz) || precio <= 0) return;
            long min = utc.Ticks / TimeSpan.TicksPerMinute;
            lock (_llave)
            {
                if (!_series.TryGetValue(raiz, out var s)) { s = new SortedDictionary<long, double>(); _series[raiz] = s; }
                s[min] = precio;
                if (s.Count > 720) s.Remove(s.Keys.First());
            }
        }

        public static SortedDictionary<long, double> Serie(string raiz)
        {
            lock (_llave) return _series.TryGetValue(raiz, out var s) ? new SortedDictionary<long, double>(s) : new SortedDictionary<long, double>();
        }
    }

    /// <summary>Una capa extra (15-09): un libro mas, calculado con el MISMO nucleo y dibujado con su color
    /// encima del grafico de NQ/MNQ. Solo baja su cadena, corre Calcular() y se dibuja; nada de la lectura
    /// primaria (centinela, gatillos, AUDIT, archivo, pelotitas) la lee. Ver conocimiento/traspasos/2026-09-15-capas-nq.md.
    ///
    /// Capas del mismo subyacente (QQQ, TQQQ, NDX, Rithmic NQ): el strike va al futuro por razon (y por
    /// apalancamiento en TQQQ). Capas de OTRO subyacente (SPX, SPY, ES): un muro de SPX no es un precio de NQ;
    /// se lleva por la distancia porcentual al spot, multiplicada por la beta NQ/S&P MEDIDA con las velas de NQ
    /// y de ES: Fut = F x (1 + beta x (K/S - 1)), que es el mismo mapeo del apalancamiento con
    /// apalancamiento = 1/beta. Sin muestra, beta = 1 y se dice SUPUESTA.</summary>
    public sealed class CapaLibro
    {
        public enum TipoCapa { EtfPorRazon, IndiceConBase, RithmicViva, VivaLocal }

        public readonly string Nombre;        // "QQQ", "TQQQ", "NDX", "NQ", "SPX", "SPY", "ES": el producto cuyas opciones son el libro
        public readonly string Descripcion;   // que es tecnicamente ese libro (para la leyenda): "ETF · opciones CBOE", "futuro · opciones CME en vivo"...
        public readonly string Ticker;        // lo que se baja: ultima-<Ticker>.json / "NQ" (radar -> NDX) / "ES" (radar -> SPX, o viva local)
        public readonly TipoCapa Tipo;
        public readonly bool PorBeta;         // otro subyacente: apalancamiento = 1 / beta medida
        public double Apalancamiento;         // 1 (QQQ), 3 (TQQQ), 1/beta (SPX, SPY, ES): solo cambia a que precio del futuro va cada strike
        public Color Color;                   // se sincroniza con los ajustes de "6. Estilo" en cada dibujo

        public Feed.Cadena C;                                   // la ultima cadena bajada
        public readonly GammaHoyNucleo Nucleo = new();          // su propio nucleo (fotos del Max Change propias, sin uso)
        public GammaHoyNucleo.Lectura L;                        // la ultima lectura (bajo el candado del indicador)
        public readonly RazonEtf Razon = new();
        public string Error = "";
        public DateTime UltimaBajada = DateTime.MinValue, UltimoCalculo = DateTime.MinValue, UltimoAudit = DateTime.MinValue;
        public Feed.Cadena CCalculada;                          // con que cadena se calculo L (referencia)

        // la beta con la que se dibuja (la mide el indicador con las velas compartidas; manual manda)
        public double Beta = 1.0, BetaR2 = double.NaN;
        public int BetaN;
        public string BetaOrigen = "SUPUESTA (sin velas)";

        // la estela: un guion por cada dominante y por cada actualizacion en la que se movio mas de un cuarto de punto,
        // por vela (misma regla que la primaria); el pasado se rebobina desde el archivo por minuto de la nube
        public readonly Dictionary<int, List<(double Fut, int Rango, DateTime Hora)>> Guiones = new();
        public bool EstelaCargada, EstelaCargando;
        public string UltimoTsEstela = "";                       // sello de la ultima cadena que dejo marca FUERTE en la estela (16-09)

        // toques de hoy en las dominantes de esta capa (contados en el indicador, SIN placebo: el laboratorio juzga)
        public int Toques, Rebotes;
        public readonly List<Toque> Pendientes = new();
        public sealed class Toque { public double Nivel, Ent; public int Lado, Barra; }

        public CapaLibro(string nombre, string ticker, TipoCapa tipo, double apalancamiento, Color color, bool porBeta = false, string descripcion = "")
        { Nombre = nombre; Ticker = ticker; Tipo = tipo; Apalancamiento = apalancamiento; Color = color; PorBeta = porBeta; Descripcion = descripcion; }

        /// <summary>Edad del dato, dicha ANTES del numero (regla 4 del protocolo): "vivo", "N min" (ya con los 902 s de CBOE),
        /// "hace N min" para el viva grabado por otro grafico, o "sin dato".</summary>
        public string Edad(CultureInfo es)
        {
            var c = C;
            if (c == null) return "sin dato";
            if (Tipo == TipoCapa.VivaLocal) return "hace " + Math.Max(0, (DateTime.UtcNow - c.GeneradoUtc).TotalMinutes).ToString("0", es) + " min";
            if (c.EsFuturo) return "vivo";
            // la edad REAL del dato: de la foto de CBOE (902 s tarde) al momento en que la nube la genero (EdadMin)
            // y de ahi hasta ahora. Antes solo se decia EdadMin + 15, y de noche (nube cada 5 min, CBOE congelada)
            // la leyenda decia "25 min" con una cadena de 74 min (auditoria 15-09).
            double desdeGen = c.GeneradoUtc != default(DateTime) ? Math.Max(0, (DateTime.UtcNow - c.GeneradoUtc).TotalMinutes) : 0;
            return (c.EdadMin + desdeGen + 902.0 / 60.0).ToString("0", es) + " min";
        }
    }

    public partial class GammaHoy
    {
        // ==================================================================
        // Capas extra (15-09): QQQ + TQQQ + NDX + Rithmic + SPX + SPY + ES a la vez, en NQ/MNQ
        // ==================================================================
        [Display(Name = "Capa QQQ (celeste)", GroupName = "5. Capas extra (NQ)", Order = 1,
                 Description = "Libro de QQQ 0DTE por volumen (CBOE, 902 s tarde) llevado a NQ por razon, como la referencia. Solo en graficos de NQ/MNQ. Apagada no cambia nada.")]
        public bool CapaQqq { get; set; } = false;

        [Display(Name = "Capa TQQQ (magenta)", GroupName = "5. Capas extra (NQ)", Order = 2,
                 Description = "Libro de TQQQ (3x) 0DTE por volumen. Cada strike va a NQ con el apalancamiento (un strike a +3 % de TQQQ es NQ a +1 %). Las MAGNITUDES no son comparables con QQQ ni NDX: cada capa se normaliza a su propio maximo.")]
        public bool CapaTqqq { get; set; } = false;

        [Display(Name = "Capa NDX (gris)", GroupName = "5. Capas extra (NQ)", Order = 3,
                 Description = "Libro de NDX (CBOE, el mismo que 'Libro en vivo = CBOE_SPX' en NQ) con la base de la rueda de la lectura primaria; si la primaria es un ETF, cae a la base medida o teorica y lo dice.")]
        public bool CapaNdx { get; set; } = false;

        [Display(Name = "Capa NQ: opciones del futuro, CME en vivo (lima)", GroupName = "5. Capas extra (NQ)", Order = 4,
                 Description = "Las opciones de NQ desde tu ATAS (la cadena viva). Necesita 'Cadena viva de Rithmic' prendida; no abre una segunda suscripcion. OJO: su volumen arranca en cero con cada reinicio de ATAS.")]
        public bool CapaRithmic { get; set; } = false;

        [Display(Name = "Capa SPX (violeta): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 5,
                 Description = "Libro de SPX 0DTE por volumen (CBOE, 902 s tarde) llevado a NQ por la distancia porcentual al spot x beta NQ/ES medida con las velas de los dos graficos. Un muro de SPX NO es un precio de NQ: es 'donde estaria NQ si el S&P llega a su muro y NQ lo sigue con su beta'. Se dice la beta y si es medida o supuesta.")]
        public bool CapaSpx { get; set; } = false;

        [Display(Name = "Capa SPY (turquesa): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 6,
                 Description = "Libro de SPY 0DTE por volumen (CBOE), el que dibuja la referencia para ES, llevado a NQ por beta como SPX.")]
        public bool CapaSpy { get; set; } = false;

        [Display(Name = "Capa ES Rithmic (salmon): otro subyacente, por beta", GroupName = "5. Capas extra (NQ)", Order = 7,
                 Description = "Las opciones de ES por Rithmic que graba el grafico de MES cada minuto (viva-ES-<dia>.jsonl, 'Guardar la cadena viva'); sin segunda suscripcion. Strikes del futuro ES, Black-76, llevados a NQ por beta. Si el grafico de MES no esta abierto, dice hace cuanto es el dato.")]
        public bool CapaEs { get; set; } = false;

        [Display(Name = "Beta NQ vs S&P (0 = medir con las velas de NQ y MES)", GroupName = "5. Capas extra (NQ)", Order = 8,
                 Description = "Cuanto se mueve NQ por cada 1 % del S&P. 0 = se mide con los retornos por minuto de las velas de este grafico y del grafico de MES (los dos tienen que estar abiertos), ultimos 90 minutos, 20+ pares y r2 >= 0,2; si no, 1 y se rotula SUPUESTA. Un valor fijo manda y se rotula 'manual'.")]
        [Range(0, 3)]
        public decimal BetaManual { get; set; } = 0m;

        [Display(Name = "Capas: dibujar barras (izquierda: gamma x volumen)", GroupName = "5. Capas extra (NQ)", Order = 10)]
        public bool CapasBarras { get; set; } = true;

        // ==================================================================
        // 6. Puntos por capa (16-09): D1, D2 y zero gamma de cada capa, un punto por vela, configurables
        // ==================================================================
        public enum FormaPunto { Auto, Guion, Punto, Cuadrado, Rombo, Triangulo }
        public enum ColorPunto { DeLaCapa, Fijo }

        [Display(Name = "D1 (dominante mayor): dibujar", GroupName = "6. Puntos por capa", Order = 1,
                 Description = "Un punto por vela en el precio de la dominante mayor de cada capa (la estela). Fuerte cuando esa vela tuvo dato nuevo, tenue cuando el nivel solo se mantuvo.")]
        public bool PuntoD1Ver { get; set; } = true;
        [Display(Name = "D1: forma", GroupName = "6. Puntos por capa", Order = 2, Description = "Auto = guion para indices y opciones del futuro, punto para ETF.")]
        public FormaPunto PuntoD1Forma { get; set; } = FormaPunto.Auto;
        [Display(Name = "D1: tamaño (px)", GroupName = "6. Puntos por capa", Order = 3)]
        [Range(1, 12)]
        public int PuntoD1Tam { get; set; } = 4;
        [Display(Name = "D1: color", GroupName = "6. Puntos por capa", Order = 4, Description = "DeLaCapa = el color de la fuente (QQQ celeste, SPX violeta...). Fijo = el color de abajo para todas.")]
        public ColorPunto PuntoD1Color { get; set; } = ColorPunto.DeLaCapa;
        [Display(Name = "D1: color fijo", GroupName = "6. Puntos por capa", Order = 5)]
        public System.Windows.Media.Color PuntoD1ColorFijo { get; set; } = System.Windows.Media.Color.FromRgb(255, 210, 60);

        [Display(Name = "D2 (dominante menor): dibujar", GroupName = "6. Puntos por capa", Order = 6)]
        public bool PuntoD2Ver { get; set; } = true;
        [Display(Name = "D2: forma", GroupName = "6. Puntos por capa", Order = 7)]
        public FormaPunto PuntoD2Forma { get; set; } = FormaPunto.Auto;
        [Display(Name = "D2: tamaño (px)", GroupName = "6. Puntos por capa", Order = 8)]
        [Range(1, 12)]
        public int PuntoD2Tam { get; set; } = 3;
        [Display(Name = "D2: color", GroupName = "6. Puntos por capa", Order = 9)]
        public ColorPunto PuntoD2Color { get; set; } = ColorPunto.DeLaCapa;
        [Display(Name = "D2: color fijo", GroupName = "6. Puntos por capa", Order = 10)]
        public System.Windows.Media.Color PuntoD2ColorFijo { get; set; } = System.Windows.Media.Color.FromRgb(255, 170, 40);

        [Display(Name = "Zero gamma: dibujar", GroupName = "6. Puntos por capa", Order = 11,
                 Description = "Un punto por vela en el zero gamma (cruce del GEX por volumen) de cada capa. Antes solo salia con 'Capas: dibujar el zero gamma' y era un puntito de 3 px al 50 %.")]
        public bool PuntoZeroVer { get; set; } = true;
        [Display(Name = "Zero gamma: forma", GroupName = "6. Puntos por capa", Order = 12)]
        public FormaPunto PuntoZeroForma { get; set; } = FormaPunto.Rombo;
        [Display(Name = "Zero gamma: tamaño (px)", GroupName = "6. Puntos por capa", Order = 13)]
        [Range(1, 12)]
        public int PuntoZeroTam { get; set; } = 4;
        [Display(Name = "Zero gamma: color", GroupName = "6. Puntos por capa", Order = 14)]
        public ColorPunto PuntoZeroColor { get; set; } = ColorPunto.DeLaCapa;
        [Display(Name = "Zero gamma: color fijo", GroupName = "6. Puntos por capa", Order = 15)]
        public System.Windows.Media.Color PuntoZeroColorFijo { get; set; } = System.Windows.Media.Color.FromRgb(235, 235, 235);

        [Display(Name = "Fuerza: alfa de los puntos fuertes (dato nuevo, %)", GroupName = "6. Puntos por capa", Order = 16)]
        [Range(10, 100)]
        public int PuntoAlfaFuerte { get; set; } = 80;
        [Display(Name = "Fuerza: alfa de los puntos tenues (nivel sostenido, %)", GroupName = "6. Puntos por capa", Order = 17)]
        [Range(0, 100)]
        public int PuntoAlfaTenue { get; set; } = 45;

        [Display(Name = "Fuente QQQ: sus puntos", GroupName = "6. Puntos por capa", Order = 20)]
        public bool PuntosQqq { get; set; } = true;
        [Display(Name = "Fuente TQQQ: sus puntos", GroupName = "6. Puntos por capa", Order = 21)]
        public bool PuntosTqqq { get; set; } = true;
        [Display(Name = "Fuente NDX: sus puntos", GroupName = "6. Puntos por capa", Order = 22)]
        public bool PuntosNdx { get; set; } = true;
        [Display(Name = "Fuente NQ (CME en vivo): sus puntos", GroupName = "6. Puntos por capa", Order = 23)]
        public bool PuntosNq { get; set; } = true;
        [Display(Name = "Fuente SPX: sus puntos", GroupName = "6. Puntos por capa", Order = 24)]
        public bool PuntosSpx { get; set; } = true;
        [Display(Name = "Fuente SPY: sus puntos", GroupName = "6. Puntos por capa", Order = 25)]
        public bool PuntosSpy { get; set; } = true;
        [Display(Name = "Fuente ES (Rithmic grabado): sus puntos", GroupName = "6. Puntos por capa", Order = 26)]
        public bool PuntosEs { get; set; } = true;

        private bool PuntosDeCapa(CapaLibro k)
        {
            switch (k.Nombre)
            {
                case "QQQ": return PuntosQqq;
                case "TQQQ": return PuntosTqqq;
                case "NDX": return PuntosNdx;
                case "NQ": return PuntosNq;
                case "SPX": return PuntosSpx;
                case "SPY": return PuntosSpy;
                case "ES": return PuntosEs;
            }
            return true;
        }

        /// <summary>Dibuja un punto de la estela de una capa (D1 = rango 0, D2 = 1, zero = 2) con la forma, el tamaño,
        /// el color y la fuerza elegidos en "6. Puntos por capa". bw = ancho de una vela en px.</summary>
        private void PuntoCapa(RenderContext g, CapaLibro k, int rango, bool tenue, int x, int y, int bw)
        {
            bool ver; FormaPunto fp; int tam; ColorPunto cp; System.Windows.Media.Color fijo;
            if (rango == 2) { ver = PuntoZeroVer; fp = PuntoZeroForma; tam = PuntoZeroTam; cp = PuntoZeroColor; fijo = PuntoZeroColorFijo; }
            else if (rango == 0) { ver = PuntoD1Ver; fp = PuntoD1Forma; tam = PuntoD1Tam; cp = PuntoD1Color; fijo = PuntoD1ColorFijo; }
            else { ver = PuntoD2Ver; fp = PuntoD2Forma; tam = PuntoD2Tam; cp = PuntoD2Color; fijo = PuntoD2ColorFijo; }
            if (!ver) return;
            var col = cp == ColorPunto.Fijo ? De(fijo) : k.Color;
            int alfa = Math.Max(0, Math.Min(255, (tenue ? PuntoAlfaTenue : PuntoAlfaFuerte) * 255 / 100));
            if (alfa <= 0) return;
            var forma = fp == FormaPunto.Auto
                ? (k.Tipo != CapaLibro.TipoCapa.EtfPorRazon && rango != 2 ? FormaMarca.Guion : FormaMarca.Punto)
                : (FormaMarca)((int)fp - 1);
            tam = Math.Max(1, tam);
            if (forma == FormaMarca.Guion) g.FillRectangle(Color.FromArgb(alfa, col), new Rectangle(x - bw / 2, y - tam / 2, Math.Max(3, bw), Math.Max(1, tam)));
            else Marca(g, forma, Color.FromArgb(alfa, col), x, y, Math.Max(3, bw), Math.Max(1, tam - 2));
        }

        [Display(Name = "Capas: barras solo si pesan mas del % del maximo de su fuente", GroupName = "5. Capas extra (NQ)", Order = 10,
                 Description = "Las barras chicas no se dibujan (las dominantes siempre). 25 = un perfil ralo, solo lo que pesa. 0 = todas.")]
        [Range(0, 90)]
        public int CapasUmbralPct { get; set; } = 25;

        [Display(Name = "Capas: perfil derecho (convexidad), apagado por defecto", GroupName = "5. Capas extra (NQ)", Order = 11,
                 Description = "En 0DTE la convexidad de cada strike es casi menos su gamma: el perfil derecho es un espejo del izquierdo y solo ensucia. Prenderlo cuando haya vencimientos mas largos en el horizonte.")]
        public bool CapasConvexidadVisible { get; set; } = false;

        [Display(Name = "Capas: rayas horizontales de los niveles", GroupName = "5. Capas extra (NQ)", Order = 10,
                 Description = "Apagado (pedido 16-09: 'las dominantes son puntos tipo estela, no una raya horizontal'): las dominantes y el zero de cada libro se ven como puntos por vela (la estela), como barra a la izquierda y como renglon en la escalera del eje. Prendido: ademas una raya horizontal por nivel a lo ancho del grafico.")]
        public bool CapasRayas { get; set; } = false;

        [Display(Name = "Capas: fusionar niveles que coinciden en una sola raya", GroupName = "5. Capas extra (NQ)", Order = 11,
                 Description = "Apagado (pedido 16-09: 'no veo los niveles de QQQ'): cada libro dibuja su propia raya y su propio renglon aunque coincida con otro. Prendido: dos libros con el mismo tipo de nivel a menos de la tolerancia se dibujan como una raya de dos colores con rotulo 'QQQ·ES D1'.")]
        public bool CapasFusionar { get; set; } = false;

        [Display(Name = "Capas: fusionar niveles que coinciden (tolerancia, % del precio)", GroupName = "5. Capas extra (NQ)", Order = 12,
                 Description = "Si dos fuentes tienen un nivel a menos de esta distancia (0,03 % = unos 9 pts de NQ), se dibuja UNA raya gruesa con los colores de las dos alternados y un solo rotulo (SPX·SPY D1). 0 = no fusionar.")]
        [Range(0, 0.2)]
        public decimal CapasFusionPct { get; set; } = 0.03m;

        [Display(Name = "Capas: dibujar dominantes", GroupName = "5. Capas extra (NQ)", Order = 12)]
        public bool CapasDominantes { get; set; } = true;

        [Display(Name = "Capas: majors (+Γ / −Γ), apagados por defecto", GroupName = "5. Capas extra (NQ)", Order = 13,
                 Description = "La barra positiva mas grande y la negativa mas grande de cada capa, punteadas y tenues, en su color, solo dentro del radio de abajo y solo si no son ya una dominante. Suman rayas: prender solo si hacen falta.")]
        public bool CapasMajorsVisibles { get; set; } = false;

        [Display(Name = "Capas: majors solo a menos de (% del precio)", GroupName = "5. Capas extra (NQ)", Order = 14)]
        [Range(0.1, 5)]
        public decimal CapasMajorsRadioPct { get; set; } = 1.0m;

        [Display(Name = "Capas: dibujar el zero gamma de cada una", GroupName = "5. Capas extra (NQ)", Order = 15)]
        public bool CapasZero { get; set; } = false;

        [Display(Name = "Capas: etiquetas EN LA ESCALERA del eje (pedido 15-09)", GroupName = "5. Capas extra (NQ)", Order = 15,
                 Description = "Prendido: cada nivel de cada capa va como caja de color en la escalera pegada al eje ('SPX D1 ▲ 28.981 +36'), ordenadas por precio y sin pisarse; niveles que coinciden comparten la fila. En el medio del grafico quedan solo las rayas. Apagado: las etiquetas van en su carril, al final de las rayas.")]
        public bool CapasEtiquetasEnEscalera { get; set; } = true;

        [Display(Name = "Capas: rayas y bandas de la primaria al (%)", GroupName = "5. Capas extra (NQ)", Order = 16,
                 Description = "Con alguna capa prendida, las rayas, la banda de dominancia y la estela del libro primario (ambar) se dibujan a este porcentaje de su intensidad, para que las capas se lean. 100 = como siempre.")]
        [Range(0, 100)]
        public int CapasAtenuarPrimariaPct { get; set; } = 75;

        [Display(Name = "Capas: leyenda (abajo a la izquierda)", GroupName = "5. Capas extra (NQ)", Order = 18,
                 Description = "La lista de texto con cada libro, su edad, beta, dominantes y toques, y que es la primaria. Apagada por defecto (pedido del operador 15-09: 'esos textos molestan'); la escalera del eje y las siglas de las barras siguen.")]
        public bool CapasLeyenda { get; set; } = false;

        [Display(Name = "Capas: ocultar la primaria si una capa dibuja el mismo libro", GroupName = "5. Capas extra (NQ)", Order = 17,
                 Description = "Apagado (pedido del operador 15-09): la primaria (ambar) y su estela se ven siempre, aunque una capa dibuje el mismo libro. Prendido: si la capa NDX esta activa y la primaria es NDX, la primaria no se dibuja (misma cuenta dos veces).")]
        public bool CapasOcultarPrimariaDuplicada { get; set; } = false;

        [Display(Name = "Capas: ancho de sus columnas (% del ancho de barras)", GroupName = "5. Capas extra (NQ)", Order = 17)]
        [Range(15, 100)]
        public int CapasAnchoPct { get; set; } = 35;

        [Display(Name = "Capas: sigla de la fuente en la punta de cada barra", GroupName = "5. Capas extra (NQ)", Order = 16,
                 Description = "En cada barra dibujada (izquierda y derecha) va la sigla de su fuente con letra minima ('SPX', y 'SPX D1' si es una dominante), sin pisarse: si dos barras estan pegadas, la de abajo no lleva sigla.")]
        public bool CapasSiglas { get; set; } = true;

        [Display(Name = "Capas: estela de las dominantes por vela (guiones)", GroupName = "5. Capas extra (NQ)", Order = 17,
                 Description = "Un guion por vela, en el color de la fuente, donde estaba cada dominante en ese momento (D1 grueso, D2 fino). El pasado se rebobina desde el archivo por minuto de la nube al arrancar; el presente se va agregando en vivo. Asi se ve como se comporto cada nivel contra el precio.")]
        public bool CapasEstela { get; set; } = true;

        [Display(Name = "Capas: estela, cuantas horas hacia atras", GroupName = "5. Capas extra (NQ)", Order = 17,
                 Description = "Cuanto archivo se rebobina por capa al arrancar (cada cadena por minuto es una cuenta entera: 30 h son unas 1.500 por capa, en un hilo aparte).")]
        [Range(1, 120)]
        public int CapasEstelaHoras { get; set; } = 30;

        [Display(Name = "Capas: contar toques y rebotes de hoy (sin placebo)", GroupName = "5. Capas extra (NQ)", Order = 18,
                 Description = "Por capa: cuantas veces el precio llego a una dominante desde lejos y cuantas reboto (misma regla que el laboratorio: banda, llegada de lejos, R a favor antes que R en contra en 20 min). Es un CONTEO del dia, sin placebo: el laboratorio (capas_respeto.py) es el que juzga.")]
        public bool CapasToques { get; set; } = true;

        private readonly CapaLibro[] _capas =
        {
            // el NOMBRE es el producto cuyas opciones forman el libro (pedido 15-09: nada de "Rithmic", que es el proveedor)
            new CapaLibro("QQQ", "QQQ", CapaLibro.TipoCapa.EtfPorRazon, 1, Color.FromArgb(80, 180, 255), descripcion: "ETF Nasdaq-100, opciones CBOE"),
            new CapaLibro("TQQQ", "TQQQ", CapaLibro.TipoCapa.EtfPorRazon, 3, Color.FromArgb(255, 90, 200), descripcion: "ETF Nasdaq-100 3x, opciones CBOE"),
            new CapaLibro("NDX", "NQ", CapaLibro.TipoCapa.IndiceConBase, 1, Color.FromArgb(232, 232, 245), descripcion: "índice Nasdaq-100, opciones CBOE"),
            new CapaLibro("NQ", "", CapaLibro.TipoCapa.RithmicViva, 1, Color.FromArgb(170, 255, 90), descripcion: "futuro E-mini Nasdaq, opciones CME en vivo"),
            new CapaLibro("SPX", "ES", CapaLibro.TipoCapa.EtfPorRazon, 1, Color.FromArgb(180, 120, 255), porBeta: true, descripcion: "índice S&P 500, opciones CBOE"),
            new CapaLibro("SPY", "SPY", CapaLibro.TipoCapa.EtfPorRazon, 1, Color.FromArgb(0, 210, 190), porBeta: true, descripcion: "ETF S&P 500, opciones CBOE"),
            new CapaLibro("ES", "ES", CapaLibro.TipoCapa.VivaLocal, 1, Color.FromArgb(255, 150, 120), porBeta: true, descripcion: "futuro E-mini S&P, opciones CME grabadas del gráfico de MES"),
        };
        private DateTime _diaToques = DateTime.MinValue;
        private bool _pintandoCapas;

        // la beta NQ/ES medida con las velas compartidas (una para todas las capas del S&P)
        private double _betaSp = 1.0, _betaR2 = double.NaN;
        private int _betaN;
        private string _betaOrigen = "SUPUESTA (sin velas)";
        private DateTime _ultimaBeta = DateTime.MinValue;

        /// <summary>Solo en NQ/MNQ (no inventar capas de ES) y solo si su llave esta prendida.</summary>
        private static readonly Dictionary<PaletaCapas, Dictionary<string, Color>> PALETAS = new()
        {
            [PaletaCapas.VerdeVioleta] = new() { ["NQ"] = Color.FromArgb(140, 255, 50), ["NDX"] = Color.FromArgb(61, 220, 151), ["QQQ"] = Color.FromArgb(79, 195, 247), ["ES"] = Color.FromArgb(180, 120, 255), ["SPX"] = Color.FromArgb(224, 112, 240), ["SPY"] = Color.FromArgb(207, 168, 255) },
            [PaletaCapas.VerdeNaranja] = new() { ["NQ"] = Color.FromArgb(140, 255, 50), ["NDX"] = Color.FromArgb(32, 197, 181), ["QQQ"] = Color.FromArgb(90, 210, 255), ["ES"] = Color.FromArgb(255, 150, 64), ["SPX"] = Color.FromArgb(255, 122, 112), ["SPY"] = Color.FromArgb(255, 196, 92) },
            [PaletaCapas.VerdeRosa] = new() { ["NQ"] = Color.FromArgb(140, 255, 50), ["NDX"] = Color.FromArgb(108, 230, 166), ["QQQ"] = Color.FromArgb(96, 200, 255), ["ES"] = Color.FromArgb(255, 90, 160), ["SPX"] = Color.FromArgb(255, 127, 208), ["SPY"] = Color.FromArgb(255, 179, 224) },
            [PaletaCapas.FrioCalido] = new() { ["NQ"] = Color.FromArgb(141, 255, 58), ["NDX"] = Color.FromArgb(43, 226, 255), ["QQQ"] = Color.FromArgb(90, 168, 255), ["ES"] = Color.FromArgb(255, 107, 92), ["SPX"] = Color.FromArgb(255, 160, 60), ["SPY"] = Color.FromArgb(255, 225, 77) },
        };

        private Color ColorDeCapa(CapaLibro k)
        {
            if (Paleta != PaletaCapas.Personalizada && PALETAS.TryGetValue(Paleta, out var pal) && pal.TryGetValue(k.Nombre, out var cp)) return cp;
            switch (k.Nombre)
            {
                case "QQQ": return De(ColorCapaQqq);
                case "TQQQ": return De(ColorCapaTqqq);
                case "NDX": return De(ColorCapaNdx);
                case "NQ": return De(ColorCapaNq);
                case "SPX": return De(ColorCapaSpx);
                case "SPY": return De(ColorCapaSpy);
                case "ES": return De(ColorCapaEs);
            }
            return k.Color;
        }
        private void SincronizarColoresCapas() { foreach (var k in _capas) k.Color = ColorDeCapa(k); }

        private bool CapaActiva(CapaLibro k)
        {
            if (Fuente == FuenteDatos.Archivo) return false;
            string raiz; try { raiz = Raiz(); } catch { return false; }
            if (raiz != "NQ") return false;
            switch (k.Nombre)
            {
                case "QQQ": return CapaQqq;
                case "TQQQ": return CapaTqqq;
                case "NDX": return CapaNdx;
                case "NQ": return CapaRithmic;
                case "SPX": return CapaSpx;
                case "SPY": return CapaSpy;
                case "ES": return CapaEs;
            }
            return false;
        }

        /// <summary>El nombre del libro de la primaria, con los mismos nombres que las capas: QQQ/SPY (CBOE_ETF), NDX/SPX (CBOE_SPX),
        /// RITHMIC (Rithmic_ES). Asi se sabe si la primaria es el MISMO libro que una capa prendida.</summary>
        private string NombrePrimaria()
        {
            try
            {
                if (Libro == LibroEnVivo.CBOE_ETF) return RaizLibro();
                if (Libro == LibroEnVivo.Rithmic_ES) return Raiz() == "NQ" ? "NQ" : "ES";   // opciones del futuro (CME, en vivo)
                return Raiz() == "NQ" ? "NDX" : "SPX";
            }
            catch { return "?"; }
        }

        /// <summary>La primaria es un duplicado cuando su libro es el de una capa prendida (misma cadena, misma cuenta, mismos
        /// numeros): entonces no se dibuja nada de ella (barras, rayas, banda, estela) y la leyenda lo dice.</summary>
        private bool PrimariaDuplicada()
        {
            var n = NombrePrimaria();
            foreach (var k in _capas) if (k.Nombre == n && CapaActiva(k)) return true;
            return false;
        }

        /// <summary>Con capas activas y atenuacion < 100, la primaria es un fantasma: sus barras no llevan rotulo.</summary>
        private bool PrimariaSilenciada()
        {
            if (CapasAtenuarPrimariaPct >= 100) return false;
            foreach (var k in _capas) if (CapaActiva(k)) return true;
            return false;
        }

        /// <summary>Con capas activas, las rayas y bandas de la primaria bajan al porcentaje elegido; las de las capas no.</summary>
        private int AtenuarPrimaria(int alfa)
        {
            if (_pintandoCapas) return alfa;
            if (CapasOcultarPrimariaDuplicada && PrimariaDuplicada()) return 0;   // solo si el operador lo pide: misma cuenta dos veces
            if (CapasAtenuarPrimariaPct >= 100) return alfa;
            bool hay = false; foreach (var k in _capas) if (CapaActiva(k)) { hay = true; break; }
            return hay ? Math.Max(0, alfa * CapasAtenuarPrimariaPct / 100) : alfa;
        }

        /// <summary>beta = pendiente de los retornos por minuto de NQ (este grafico) sobre los de ES (el grafico de MES),
        /// minutos comunes de los ultimos 90, sin intercepto; 20+ pares y r2 >= 0,2, acotada a [0,4; 3]. Manual manda.
        /// Se recalcula una vez por minuto.</summary>
        private void MedirBetaVelas(DateTime ahoraUtc)
        {
            if ((ahoraUtc - _ultimaBeta).TotalSeconds < 60) return;
            _ultimaBeta = ahoraUtc;
            double manual = (double)BetaManual;
            if (manual > 0) { _betaSp = manual; _betaN = 0; _betaR2 = double.NaN; _betaOrigen = "manual"; return; }
            var nq = VelasCompartidas.Serie(Raiz());
            var es = VelasCompartidas.Serie("ES");
            long desde = ahoraUtc.Ticks / TimeSpan.TicksPerMinute - 90;
            var comunes = nq.Keys.Where(m => m >= desde && es.ContainsKey(m)).OrderBy(m => m).ToList();
            double sxy = 0, sxx = 0, syy = 0; int n = 0;
            for (int i = 1; i < comunes.Count; i++)
            {
                if (comunes[i] - comunes[i - 1] > 3) continue;
                double dx = Math.Log(es[comunes[i]] / es[comunes[i - 1]]), dy = Math.Log(nq[comunes[i]] / nq[comunes[i - 1]]);
                if (dx == 0 && dy == 0) continue;
                sxy += dx * dy; sxx += dx * dx; syy += dy * dy; n++;
            }
            _betaN = n;
            if (n >= 20 && sxx > 0 && syy > 0)
            {
                double r2 = (sxy * sxy) / (sxx * syy), b = sxy / sxx;
                _betaR2 = r2;
                if (r2 >= 0.2) { _betaSp = Math.Max(0.4, Math.Min(3.0, b)); _betaOrigen = "velas" + (b != _betaSp ? " ACOTADA" : ""); }
                else { _betaSp = 1.0; _betaOrigen = "SUPUESTA (r2 " + r2.ToString("0.00", CultureInfo.InvariantCulture) + " bajo)"; }
            }
            else { _betaSp = 1.0; _betaR2 = double.NaN; _betaOrigen = es.Count == 0 ? "SUPUESTA (sin velas de MES: abrir su grafico)" : "SUPUESTA (n " + n + " < 20)"; }
        }

        /// <summary>Los puntos de la estela de una lectura, en posicion fija: [0] D1, [1] D2, [2] zero gamma (16-09: el zero
        /// tambien va a la estela, un puntito por vela en el color del libro). 0 donde no hay.</summary>
        /// <summary>Las tres pelotitas del Max Change de cada barra de cada capa: la punta de esa barra hace 15
        /// (grande), 5 (mediana) y 1 min (chica), con las fotos por minuto del nucleo de la capa. En el perfil
        /// izquierdo (GEX por volumen, desde xBorde hacia la derecha) o en la escalera de convexidad (desde xBorde
        /// hacia la izquierda). Un cambio de signo cuenta como "estaba en cero". Sin fotos viejas (recien
        /// arrancado) no dibuja nada: no inventa. Solo sobre las barras que se dibujaron (anchoDeBarra).</summary>
        private void PelotitasCapas(RenderContext g, IChartContainer cont, List<CapaLibro> activas, Dictionary<CapaLibro, GammaHoyNucleo.Lectura> lecturas,
                                    Dictionary<(CapaLibro, double), int> anchoDeBarra, int xBorde, int ancho, int alto, bool convexidad)
        {
            if (anchoDeBarra == null || anchoDeBarra.Count == 0) return;
            long ahoraMin = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
            int[] vent = { 15, 5, 1 };
            int[] rad = { Math.Max(3, alto / 2 + 1), Math.Max(2, alto / 2), Math.Max(2, alto / 2 - 1) };
            foreach (var k in activas)
            {
                var L = lecturas[k]; if (L == null || L.SinBase || L.Perfil.Count == 0) continue;
                double maxK = convexidad ? L.MaxAbsConv : L.MaxAbsVol; if (maxK <= 0) continue;
                List<GammaHoyNucleo.Snap> fotos; try { fotos = k.Nucleo.FotosCopia(); } catch { continue; }
                if (fotos.Count == 0) continue;
                var antes = new Dictionary<double, double>[3];
                for (int i = 0; i < 3; i++) { var f = fotos.Where(z => z.Minuto <= ahoraMin - vent[i]).LastOrDefault(); antes[i] = f == null ? null : (convexidad ? f.Conv : f.GexVol); }
                if (antes.All(a => a == null)) continue;
                foreach (var s in L.Perfil)
                {
                    if (!anchoDeBarra.ContainsKey((k, s.Fut))) continue;
                    int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                    double ahora = convexidad ? s.Conv : s.GexVol;
                    int cx0 = int.MinValue;
                    for (int i = 0; i < 3; i++)
                    {
                        if (antes[i] == null || !antes[i].TryGetValue(s.Clave, out var vAntes)) continue;
                        if (Math.Sign(vAntes) != Math.Sign(ahora) && vAntes != 0) vAntes = 0;
                        int wa = Math.Max(0, (int)(Math.Sqrt(Math.Abs(vAntes) / maxK) * ancho));
                        int rr = rad[i];
                        int cx = convexidad ? xBorde - wa : xBorde + wa;
                        if (i == 0) cx0 = cx; else if (Math.Abs(cx - cx0) <= 1) continue;   // quieto: las tres en el mismo pixel, se dibuja la grande sola
                        // relleno del color de la capa aclarado, borde del fondo: recorta sobre la barra y sobre el vacio
                        var claro = Color.FromArgb(235, (k.Color.R * 2 + 255 * 3) / 5, (k.Color.G * 2 + 255 * 3) / 5, (k.Color.B * 2 + 255 * 3) / 5);
                        g.FillEllipse(claro, new Rectangle(cx - rr, y - rr, 2 * rr, 2 * rr));
                        g.DrawEllipse(new RenderPen(Color.FromArgb(230, ColFondo), 1f), new Rectangle(cx - rr, y - rr, 2 * rr, 2 * rr));
                    }
                }
            }
        }

        private static List<(double Fut, double Gex)> PuntosEstela(GammaHoyNucleo.Lectura L)
        {
            var p = new List<(double Fut, double Gex)>();
            for (int i = 0; i < 2; i++) p.Add(L != null && L.Doms.Count > i ? L.Doms[i] : (0.0, 0.0));
            p.Add((L != null && !double.IsNaN(L.ZeroVol) && L.ZeroVol > 0 ? L.ZeroVol : 0.0, 0.0));
            return p;
        }

        /// <summary>Un guion por dominante cuando se movio mas de un cuarto de punto (misma regla que AgregarGuiones).</summary>
        // MARCA FUERTE O TENUE (16-09, pedido: "los competidores dibujan estelas dispersas"): la marca es fuerte cuando
        // esa vela tuvo un DATO NUEVO (otra cadena de CBOE, o el libro vivo que cambia a cada rato) y tenue cuando el nivel
        // solo se mantuvo (relleno entre cadenas, o la misma cadena de CBOE vela tras vela). Se codifica en Rango: +10 = tenue.
        private static void AgregarGuionesCapa(CapaLibro k, int bar, List<(double Fut, double Gex)> doms, DateTime hora, bool relleno = false)
        {
            if (doms == null || bar < 0) return;
            if (!k.Guiones.TryGetValue(bar, out var lg)) { lg = new List<(double, int, DateTime)>(); k.Guiones[bar] = lg; }
            for (int i = 0; i < doms.Count; i++)
            {
                double p = doms[i].Fut;
                if (double.IsNaN(p) || p <= 0) continue;
                bool hay = false;
                for (int j = lg.Count - 1; j >= 0; j--) if (lg[j].Rango % 10 == i) { hay = Math.Abs(lg[j].Fut - p) < 0.25; break; }
                if (!hay && lg.Count < 24) lg.Add((p, i + (relleno ? 10 : 0), hora));
            }
        }

        // LA ESTELA SOBREVIVE AL REINICIO (15-09 23:50, pedido: "los guiones de NQ desaparecen"). Los guiones de cada capa
        // vivian solo en memoria: cada reinicio o cambio de grafico los borraba, y el libro NQ en vivo no tiene archivo en
        // la nube para rebobinar. Ahora cada cambio de dominantes se anota en %APPDATA%\ATAS\PythiaGex\estela\
        // estela-<capa>-<dia>.jsonl y al arrancar se vuelve a poner en su vela (por hora UTC), antes del rebobinado de la nube.
        private static readonly string CarpetaEstela = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex2", "estela");
        // UN SOLO ESCRITOR POR CAPA (17-09): dos graficos con la capa NQ escribian la misma estela con 0,75 pts de diferencia (cada uno mide su spread del roll)
        // y a veces con otro strike: el detector de Flujo Claro perdia la raya cada 26 s. Gana el primero; si calla 180 s, lo releva otro.
        private static readonly Dictionary<string, string> _estelaUltimaLinea = new();
        private static readonly Dictionary<string, DateTime> _estelaUltimaHora = new();
        private static readonly Dictionary<string, (object Dueno, DateTime Hora)> _estelaEscritor = new();

        private void GuardarGuionCapa(CapaLibro k, DateTime horaUtc, List<(double Fut, double Gex)> doms)
        {
            try
            {
                if (doms == null || doms.Count == 0) return;
                var inv = CultureInfo.InvariantCulture;
                string d = string.Join(",", doms.Select(x => double.IsNaN(x.Fut) || x.Fut <= 0 ? "0" : x.Fut.ToString("0.00", inv)));
                if (d.Length == 0 || d.Replace(",", "").Replace("0", "").Length == 0) return;
                lock (_estelaUltimaLinea)
                {
                    if (_estelaEscritor.TryGetValue(k.Nombre, out var esc) && !ReferenceEquals(esc.Dueno, this) && (horaUtc - esc.Hora).TotalSeconds < 180) return;   // escribe otro grafico
                    _estelaEscritor[k.Nombre] = (this, horaUtc);
                    // cuando cambia, o cada 60 s: el detector de Flujo Claro necesita saber que la raya sigue viva y con que fuerza (17-09)
                    bool igual = _estelaUltimaLinea.TryGetValue(k.Nombre, out var u) && u == d;
                    if (igual && _estelaUltimaHora.TryGetValue(k.Nombre, out var h) && (horaUtc - h).TotalSeconds < 60) return;
                    _estelaUltimaLinea[k.Nombre] = d; _estelaUltimaHora[k.Nombre] = horaUtc;
                }
                string fz = string.Join(",", doms.Take(2).Select(x => (Math.Abs(x.Gex) / 1e6).ToString("0", inv)));
                string extra = ",\"g\":[" + fz + "],\"n\":" + ((k.L?.NetVol ?? 0) / 1e6).ToString("0", inv) + ",\"b\":\"" + (k.L?.LibroDom ?? "vol") + "\",\"f\":" + (k.L?.Futuro ?? 0).ToString("0.00", inv);
                Directory.CreateDirectory(CarpetaEstela);
                File.AppendAllText(Path.Combine(CarpetaEstela, "estela-" + k.Nombre + "-" + horaUtc.ToString("yyyy-MM-dd", inv) + ".jsonl"),
                                   "{\"t\":\"" + horaUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv) + "\",\"d\":[" + d + "]" + extra + "}\n");
            }
            catch { }
        }

        /// <summary>Vuelve a poner en su vela los guiones guardados de los ultimos dias (por hora UTC). Devuelve cuantos entraron.</summary>
        private int CargarEstelaGuardada(CapaLibro k, DateTime desde, DateTime hasta)
        {
            int puestos = 0, lineas = 0;
            var cambios = new List<(DateTime T, List<(double Fut, double Gex)> Doms)>();
            try
            {
                var inv = CultureInfo.InvariantCulture;
                for (var dia = desde.Date; dia <= hasta.Date; dia = dia.AddDays(1))
                {
                    var ruta = Path.Combine(CarpetaEstela, "estela-" + k.Nombre + "-" + dia.ToString("yyyy-MM-dd", inv) + ".jsonl");
                    if (!File.Exists(ruta)) continue;
                    foreach (var l in File.ReadAllLines(ruta))
                    {
                        if (l.Length < 20) continue;
                        lineas++;
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(l);
                            var r = doc.RootElement;
                            if (!DateTime.TryParseExact(r.GetProperty("t").GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'", inv,
                                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var t)) continue;
                            if (t < desde || t > hasta) continue;
                            var doms = new List<(double Fut, double Gex)>();
                            foreach (var x in r.GetProperty("d").EnumerateArray()) doms.Add((x.GetDouble(), 0.0));
                            cambios.Add((t, doms));
                        }
                        catch { }
                    }
                }
                // UN PUNTO POR VELA (16-09): cada cambio vale hasta el cambio siguiente, como la estela de la primaria
                cambios.Sort((a, b) => a.T.CompareTo(b.T));
                for (int i = 0; i < cambios.Count; i++)
                {
                    int bar = BarraDeCapa(cambios[i].T);
                    if (bar < 0) continue;
                    // con el tope en puntos (16-09) lo guardado con el radio viejo puede estar lejos: se filtra con el cierre de esa vela
                    double radioMax = _nucleo.A.RadioDominantesMaxPts > 0 ? _nucleo.A.RadioDominantesMaxPts : double.MaxValue;
                    try { double cierre = (double)GetCandle(bar).Close; for (int j = 0; j < 2 && j < cambios[i].Doms.Count; j++) if (Math.Abs(cambios[i].Doms[j].Fut - cierre) > radioMax) cambios[i].Doms[j] = (0.0, 0.0); } catch { }
                    int hastaBar = i + 1 < cambios.Count ? BarraDeCapa(cambios[i + 1].T) : CurrentBar - 1;
                    if (hastaBar < bar) hastaBar = bar;
                    hastaBar = Math.Min(hastaBar, bar + 2000);
                    lock (_candado) for (int b = bar; b <= hastaBar; b++) AgregarGuionesCapa(k, b, cambios[i].Doms, cambios[i].T, b != bar);
                    puestos++;
                }
                if (lineas > 0) Log("estela guardada " + k.Nombre + ": " + puestos + " de " + lineas + " cambios vueltos a su vela, rellenados hasta el siguiente");
            }
            catch (Exception e) { Registrar(e); }
            return puestos;
        }

        /// <summary>La vela que contenia esa hora UTC, buscando hasta 4.000 velas atras (el archivo de 30 h en M2 son 900).</summary>
        private int BarraDeCapa(DateTime horaUtc)
        {
            try
            {
                for (int b = CurrentBar - 1; b >= Math.Max(0, CurrentBar - 4000); b--)
                {
                    var c = GetCandle(b);
                    if (c == null) continue;
                    if (Utc(c.Time) <= horaUtc) return b;
                }
            }
            catch { }
            return -1;
        }

        /// <summary>Rebobina la estela de una capa desde el archivo por minuto de la nube (cadena-<ticker>-<dia>.jsonl.gz),
        /// en un hilo aparte y con un nucleo propio: para cada cadena, la vela que la contenia, el cierre de esa vela como
        /// futuro, la misma preparacion que en vivo (razon por vela alineada, apalancamiento, beta de ahora) y las
        /// dominantes que salen van como guiones a esa vela. Solo capas de la nube (QQQ, TQQQ, SPY, SPX, NDX).</summary>
        private void CargarEstelaCapa(CapaLibro k)
        {
            if (k.EstelaCargada || k.EstelaCargando) return;
            k.EstelaCargando = true;
            string raiz = k.Ticker;                       // QQQ, TQQQ, SPY, ES (=SPX), NQ (=NDX)
            var hasta = DateTime.UtcNow; var desde = hasta.AddHours(-Math.Max(1, CapasEstelaHoras));
            if (k.Tipo == CapaLibro.TipoCapa.RithmicViva || k.Tipo == CapaLibro.TipoCapa.VivaLocal)
            {
                // libros vivos: no hay archivo en la nube; la estela vuelve de lo guardado en esta maquina
                _ = Task.Run(() => { try { CargarEstelaGuardada(k, desde, hasta); k.EstelaCargada = true; } catch (Exception e) { Registrar(e); } finally { k.EstelaCargando = false; } });
                return;
            }
            _ = Task.Run(() =>
            {
                try
                {
                    CargarEstelaGuardada(k, desde, hasta);   // lo visto en vivo en esta maquina, antes que el rebobinado de la nube
                    if (BajarArchivo)
                        for (var d = desde.Date; d <= hasta.Date; d = d.AddDays(1))
                            Feed.Archivo.BajarDia(string.IsNullOrWhiteSpace(UrlArchivo) ? Url : UrlArchivo, raiz, d, Log).GetAwaiter().GetResult();
                    var ls = Feed.Archivo.Cargar(raiz, desde, hasta, null);
                    var nuc = new GammaHoyNucleo();
                    var t = typeof(GammaHoyNucleo.Ajustes);
                    foreach (var fi in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) fi.SetValue(nuc.A, fi.GetValue(_nucleo.A));
                    var razon = new RazonEtf();
                    int con = 0, sinVela = 0, sinBase = 0;
                    var lecturasRep = new List<(int Bar, List<(double Fut, double Gex)> Doms, DateTime T)>();
                    foreach (var c in ls)
                    {
                        if (c == null || c.GeneradoUtc == default(DateTime)) continue;
                        int bar = BarraDeCapa(c.GeneradoUtc);
                        if (bar < 0) { sinVela++; continue; }
                        double futuro; try { futuro = (double)GetCandle(bar).Close; } catch { continue; }
                        if (futuro <= 0) continue;
                        if (k.Tipo == CapaLibro.TipoCapa.EtfPorRazon)
                        {
                            EscalarCon(c, k.Nombre, razon, Math.Max(0, RetrasoCboeSeg), c.GeneradoUtc, futuro);   // F2: la edad y el precio de ESE minuto
                            c.Fuente = "CBOE " + k.Nombre;
                            c.Apalancamiento = k.PorBeta ? 1.0 / Math.Max(0.1, _betaSp) : k.Apalancamiento;
                        }
                        GammaHoyNucleo.Lectura L;
                        try { L = nuc.Calcular(c, futuro, c.GeneradoUtc); } catch { continue; }
                        if (L == null || L.SinBase) { sinBase++; continue; }
                        lecturasRep.Add((bar, PuntosEstela(L), c.GeneradoUtc));
                        con++;
                    }
                    // las fotos por minuto del rebobinado siembran el Max Change de la capa: pelotitas desde el primer minuto (16-09)
                    try { k.Nucleo.SembrarFotos(nuc.FotosCopia().Select(z => (z.Minuto, z.GexVol, z.Conv))); } catch (Exception e) { Registrar(e); }
                    // UN PUNTO POR VELA (16-09): cada cadena vale hasta la cadena siguiente (la nube llega cada 8-25 min)
                    lecturasRep.Sort((a, b) => a.Bar.CompareTo(b.Bar));
                    for (int i = 0; i < lecturasRep.Count; i++)
                    {
                        int b0 = lecturasRep[i].Bar, b1 = i + 1 < lecturasRep.Count ? lecturasRep[i + 1].Bar - 1 : Math.Min(CurrentBar - 1, b0 + 60);
                        if (b1 < b0) b1 = b0;
                        lock (_candado) for (int b = b0; b <= Math.Min(b1, b0 + 2000); b++) AgregarGuionesCapa(k, b, lecturasRep[i].Doms, lecturasRep[i].T, b != b0);
                    }
                    Log("estela " + k.Nombre + ": " + ls.Count + " cadenas del archivo, " + con + " con guion, " + sinVela + " sin vela, " + sinBase + " sin base");
                    k.EstelaCargada = true;
                }
                catch (Exception e) { Registrar(e); }
                finally { k.EstelaCargando = false; }
            });
        }

        /// <summary>La capa Rithmic se refresca desde el temporizador (cada SegundosLibroRithmic), reusando la viva.
        /// Si la primaria ya es Rithmic, comparte su cadena.</summary>
        private void RefrescarCapaRithmic(DateTime ahora)
        {
            var k = _capas.First(z => z.Tipo == CapaLibro.TipoCapa.RithmicViva);
            if (!CapaActiva(k)) return;
            if (Libro == LibroEnVivo.Rithmic_ES) { var c0 = _c; if (c0 != null && c0.EsFuturo) { k.C = c0; k.Error = ""; k.UltimaBajada = ahora; } return; }
            if (!_viva.Activa) { k.Error = UsarCadenaViva ? "viva: " + _viva.Estado : "prender 'Cadena viva de Rithmic'"; return; }
            if ((ahora - k.UltimaBajada).TotalSeconds < Math.Max(5, SegundosLibroRithmic)) return;
            k.UltimaBajada = ahora;
            try { var cv = DesdeViva(); if (cv != null) { k.C = cv; k.Error = ""; } }
            catch (Exception e) { k.Error = e.Message; Registrar(e); }
        }

        /// <summary>Un libro de otro subyacente (SPX, SPY, ES) o de un ETF: razon por vela alineada y el apalancamiento
        /// (3 en TQQQ, 1/beta en el S&P) en la cadena.</summary>
        private void PrepararCapa(CapaLibro k, Feed.Cadena c, int retrasoSeg, string fuente, bool esFuturo)
        {
            EscalarCon(c, k.Nombre, k.Razon, retrasoSeg);
            c.EsFuturo = esFuturo;
            c.Fuente = fuente;
            if (k.PorBeta) AplicarBeta(k);
            c.Apalancamiento = k.Apalancamiento;
        }

        private void AplicarBeta(CapaLibro k)
        {
            k.Beta = _betaSp; k.BetaN = _betaN; k.BetaR2 = _betaR2; k.BetaOrigen = _betaOrigen;
            k.Apalancamiento = 1.0 / Math.Max(0.1, _betaSp);
        }

        /// <summary>Baja las cadenas de las capas de CBOE y del viva local, en serie y cada una en su try: una que falle
        /// no tumba a las otras ni a la primaria. Se llama al final de BajarFeed (misma cadencia que el feed).</summary>
        private async Task BajarCapas()
        {
            foreach (var k in _capas)
            {
                if (!CapaActiva(k) || k.Tipo == CapaLibro.TipoCapa.RithmicViva) continue;
                try
                {
                    Feed.Cadena c = null;
                    Action<string> err = m => k.Error = (m ?? "").Contains("404") ? "sin archivo en la nube todavia (404)" : m;
                    if (k.Tipo == CapaLibro.TipoCapa.EtfPorRazon)
                    {
                        if (k.Nombre == "SPX")
                        {
                            // la cadena de SPX: el feed de la nube (radar, cada 5 min) y la de la rama cadenas (cada minuto), la mas nueva
                            c = await Feed.Bajar(Url, "ES", err).ConfigureAwait(false);
                            if (FeedMinuto)
                            {
                                var u = await Feed.BajarUltima(UrlArchivo, "ES", null).ConfigureAwait(false);
                                if (u != null && (c == null || u.GeneradoUtc > c.GeneradoUtc)) c = u;
                            }
                        }
                        else c = await Feed.BajarUltima(UrlArchivo, k.Ticker, err).ConfigureAwait(false);
                        if (c != null)
                            PrepararCapa(k, c, Math.Max(0, RetrasoCboeSeg), "CBOE " + k.Nombre + (k.Apalancamiento != 1 && !k.PorBeta ? " x" + k.Apalancamiento.ToString("0", CultureInfo.InvariantCulture) : ""), false);
                    }
                    else if (k.Tipo == CapaLibro.TipoCapa.VivaLocal)
                    {
                        c = Feed.Archivo.UltimaViva(k.Ticker);
                        if (c == null) { k.Error = "sin viva-" + k.Ticker + " local: el grafico de MES la graba ('Guardar la cadena viva')"; }
                        else PrepararCapa(k, c, 0, "Rithmic " + k.Ticker + " (grabado)", true);
                    }
                    else // NDX: si la primaria ya es la cadena de NDX (CBOE_SPX en NQ), es la misma
                    {
                        var c0 = _c;
                        if (Libro == LibroEnVivo.CBOE_SPX && c0 != null && !c0.EsFuturo && !c0.PorRazon) c = c0;
                        else
                        {
                            c = await Feed.Bajar(Url, k.Ticker, err).ConfigureAwait(false);
                            if (FeedMinuto)
                            {
                                var u = await Feed.BajarUltima(UrlArchivo, k.Ticker, null).ConfigureAwait(false);
                                if (u != null && (c == null || u.GeneradoUtc > c.GeneradoUtc)) c = u;
                            }
                        }
                    }
                    if (c != null) { k.C = c; k.Error = ""; k.UltimaBajada = DateTime.UtcNow; }
                }
                catch (Exception e) { k.Error = e.Message; Registrar(e); }
            }
        }

        /// <summary>Corre Calcular() de cada capa con los MISMOS ajustes que la primaria (copiados por reflexion,
        /// incluida la base de la rueda y el vencimiento del futuro). Solo cuando cambio su cadena o cada 5 s:
        /// cada Calcular son ~211 strikes x 61 pasos y ya se midio (10-09) que repreciar por tick funde un nucleo.
        /// Se llama al final de RepreciarCon. AUDIT por capa cada 60 s, con reloj propio.</summary>
        private void RepreciarCapas(double futuro, DateTime ahoraUtc)
        {
            try { MedirBetaVelas(ahoraUtc); } catch (Exception e) { Registrar(e); }
            foreach (var k in _capas)
            {
                if (!CapaActiva(k)) { if (k.L != null) lock (_candado) k.L = null; continue; }
                var c = k.C;
                if (c == null) continue;
                bool betaCambio = k.PorBeta && k.Beta != _betaSp;
                if (betaCambio) { AplicarBeta(k); c.Apalancamiento = k.Apalancamiento; }
                else if (k.PorBeta) { k.BetaN = _betaN; k.BetaR2 = _betaR2; k.BetaOrigen = _betaOrigen; }   // la leyenda dice por que sigue SUPUESTA (n, r2, sin velas)
                if (!betaCambio && ReferenceEquals(c, k.CCalculada) && (ahoraUtc - k.UltimoCalculo).TotalSeconds < 5) continue;
                var a = k.Nucleo.A; var de = _nucleo.A;
                var t = typeof(GammaHoyNucleo.Ajustes);
                foreach (var fi in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) fi.SetValue(a, fi.GetValue(de));
                foreach (var pi in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) if (pi.CanRead && pi.CanWrite) pi.SetValue(a, pi.GetValue(de));
                GammaHoyNucleo.Lectura L = null;
                try { L = k.Nucleo.Calcular(c, futuro, ahoraUtc); }
                catch (Exception e) { k.Error = e.Message; Registrar(e); }
                lock (_candado)
                {
                    k.L = L;
                    if (CapasEstela && L != null && !L.SinBase)
                    {
                        var pe = PuntosEstela(L);
                        bool nuevo = c.EsFuturo || !string.Equals(c.Ts ?? "", k.UltimoTsEstela);   // vivo: siempre; CBOE: solo con otra cadena
                        k.UltimoTsEstela = c.Ts ?? "";
                        AgregarGuionesCapa(k, Math.Max(0, CurrentBar - 1), pe, ahoraUtc, !nuevo);
                        GuardarGuionCapa(k, ahoraUtc, pe);
                    }
                    if (k.Guiones.Count > 6000) foreach (var kb in k.Guiones.Keys.Where(b => b < CurrentBar - 5000).ToList()) k.Guiones.Remove(kb);
                }
                if (CapasEstela && Fuente != FuenteDatos.Archivo && CurrentBar > 10) CargarEstelaCapa(k);
                k.UltimoCalculo = ahoraUtc; k.CCalculada = c;
                if (L != null && !L.SinBase && (ahoraUtc - k.UltimoAudit).TotalSeconds >= 60)
                {
                    k.UltimoAudit = ahoraUtc;
                    var inv = CultureInfo.InvariantCulture;
                    Log("AUDIT capa=" + k.Nombre + " " + GammaHoyNucleo.Audit(L, c, k.Tipo == CapaLibro.TipoCapa.RithmicViva).Substring(6)
                        + (k.PorBeta ? " beta=" + k.Beta.ToString("0.###", inv) + " betaN=" + k.BetaN + " betaR2=" + (double.IsNaN(k.BetaR2) ? "NaN" : k.BetaR2.ToString("0.00", inv)) + " betaOrigen=" + k.BetaOrigen.Replace(' ', '_') + " velasNQ=" + VelasCompartidas.Serie(Raiz()).Count + " velasES=" + VelasCompartidas.Serie("ES").Count : "")
                        + " toques=" + k.Toques + " rebotes=" + k.Rebotes
                        + (k.Tipo == CapaLibro.TipoCapa.RithmicViva ? " apoyo=" + string.Join("/", _viva.ApoyoPorStrikeLados().OrderByDescending(z => z.Value.cb + z.Value.ca + z.Value.pb + z.Value.pa).Take(3).Select(z => z.Key.ToString("0", inv) + ":C" + z.Value.cb.ToString("0", inv) + "|" + z.Value.ca.ToString("0", inv) + "P" + z.Value.pb.ToString("0", inv) + "|" + z.Value.pa.ToString("0", inv))) + " evProf=" + _viva.EventosProfundidad : "")
                        + " cadenaTs=" + (c.Ts ?? "").Replace(' ', '_') + " gen=" + (c.GeneradoUtc == default(DateTime) ? "?" : c.GeneradoUtc.ToString("HH:mm:ss", inv))
                        + " horizonte=" + k.Nucleo.A.Horizonte + " fuente=" + (c.Fuente ?? "").Replace(' ', '_') + (k.Tipo == CapaLibro.TipoCapa.RithmicViva ? " sin0dte=" + (_viva.SinCeroDte ? "SI" : "no") + " ventana=" + _viva.CentroVentana.ToString("0", CultureInfo.InvariantCulture) + "±" + _viva.RadioDenso.ToString("0", CultureInfo.InvariantCulture) : ""));
                }
            }
        }

        /// <summary>Los niveles de cada capa activa, para el centinela: <capa>_dom0, <capa>_dom1, <capa>_zero_vol,
        /// <capa>_mp_vol, <capa>_mn_vol (en minuscula). Se llama bajo _candado desde Anotar, una vez por vela cerrada.
        /// Con esto el laboratorio (capas_respeto.py) juzga cada fuente contra su placebo con la misma regla de siempre.</summary>
        private void NivelesCapas(List<KeyValuePair<string, double>> niv)
        {
            foreach (var k in _capas)
            {
                if (!CapaActiva(k)) continue;
                var L = k.L;
                if (L == null || L.SinBase) continue;
                string pre = k.Nombre.ToLowerInvariant() + "_";
                foreach (var kv in GammaHoyNucleo.Niveles(L))
                    if (kv.Key.StartsWith("dom") || kv.Key == "zero_vol" || kv.Key == "mp_vol" || kv.Key == "mn_vol")
                        niv.Add(new KeyValuePair<string, double>(pre + kv.Key, kv.Value));
            }
        }

        /// <summary>El conteo de hoy, por capa, con la regla del laboratorio (rebote_niveles.py): toque = la vela cerrada
        /// entra en la banda (+-TOL) de una dominante y la anterior no, viniendo de lejos (el cierre de hace ATRAS velas a
        /// mas de LEJOS); rebote = desde el cierre del toque, R a favor de la llegada antes que R en contra, dentro del
        /// horizonte. Ni placebo ni veredicto: es el conteo crudo, para ver EN VIVO que fuente esta trabajando.</summary>
        private void ContarToquesCapas(int cerrada, IndicatorCandle vela)
        {
            if (!CapasToques || vela == null || cerrada < 4) return;
            bool nq = Raiz() == "NQ";
            double tol = nq ? 8 : 2, lejos = nq ? 30 : 7, reb = nq ? 20 : 5;
            int atras = 3;
            double minPorVela = 1;
            try { var d = (vela.Time - GetCandle(cerrada - 1).Time).TotalMinutes; if (d > 0 && d < 240) minPorVela = d; } catch { }
            int horiz = Math.Max(1, (int)Math.Round(20.0 / minPorVela));
            var dia = vela.Time.Date;
            if (dia != _diaToques) { _diaToques = dia; foreach (var k in _capas) { k.Toques = 0; k.Rebotes = 0; k.Pendientes.Clear(); } }
            double h = (double)vela.High, l = (double)vela.Low, cl = (double)vela.Close;
            double hp, lp, c0;
            try { var p = GetCandle(cerrada - 1); hp = (double)p.High; lp = (double)p.Low; c0 = (double)GetCandle(cerrada - atras).Close; } catch { return; }
            foreach (var k in _capas)
            {
                if (!CapaActiva(k)) continue;
                // primero se resuelven los toques pendientes con esta vela
                for (int i = k.Pendientes.Count - 1; i >= 0; i--)
                {
                    var t = k.Pendientes[i];
                    if (cerrada <= t.Barra) continue;
                    bool gano = t.Lado > 0 ? h >= t.Ent + reb : l <= t.Ent - reb;
                    bool perdio = t.Lado > 0 ? l <= t.Ent - reb : h >= t.Ent + reb;
                    if (gano && perdio) { k.Pendientes.RemoveAt(i); continue; }          // ambiguo: no cuenta
                    if (gano) { k.Toques++; k.Rebotes++; k.Pendientes.RemoveAt(i); continue; }
                    if (perdio) { k.Toques++; k.Pendientes.RemoveAt(i); continue; }
                    if (cerrada - t.Barra >= horiz) k.Pendientes.RemoveAt(i);            // se vencio sin definirse
                }
                GammaHoyNucleo.Lectura L; lock (_candado) L = k.L;
                if (L == null || L.SinBase) continue;
                foreach (var d in L.Doms)
                {
                    double N = d.Fut;
                    bool toca = l <= N + tol && h >= N - tol;
                    bool tocaba = lp <= N + tol && hp >= N - tol;
                    if (!toca || tocaba) continue;
                    if (Math.Abs(c0 - N) < lejos) continue;                                 // no venia de lejos
                    if (k.Pendientes.Any(t => Math.Abs(t.Nivel - N) <= tol)) continue;       // ya hay un toque abierto en ese nivel
                    k.Pendientes.Add(new CapaLibro.Toque { Nivel = N, Ent = cl, Lado = c0 > N ? 1 : -1, Barra = cerrada });
                }
            }
        }

        /// <summary>Los niveles de las capas con su texto corto y su color: D1/D2 (y zero si se pide) de cada capa, y los
        /// majors si estan cerca del precio y no son ya una dominante. Con `raya` dibuja las rayas; con null solo lista.</summary>
        private List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)> EtiquetasCapas(
            List<CapaLibro> activas, Dictionary<CapaLibro, GammaHoyNucleo.Lectura> lecturas, double futuro,
            Action<double, Color, float, System.Drawing.Drawing2D.DashStyle, int> raya)
        {
            var etiquetas = new List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)>();
            double radioMajors = double.IsNaN(futuro) ? double.MaxValue : futuro * (double)CapasMajorsRadioPct / 100.0;
            foreach (var k in activas)
            {
                var L = lecturas[k]; if (L == null || L.SinBase || L.Perfil.Count == 0) continue;
                var col = k.Color;
                if (CapasDominantes)
                {
                    for (int d = 0; d < L.Doms.Count; d++)
                    {
                        double p = L.Doms[d].Fut;
                        raya?.Invoke(p, col, d == 0 ? (float)GrosorCapaDominante : (float)Math.Max(0.6, (double)GrosorCapaDominante - 0.5), Trazo(LineaCapaDominante), d == 0 ? 210 : 150);
                        etiquetas.Add((p, k.Nombre + " " + TextoDominante + (d + 1) + (L.LibroDom == "OI" ? "·OI" : ""), col, d == 0 ? 3 : 2, k));
                    }
                    if (CapasZero && !double.IsNaN(L.ZeroVol))
                    {
                        raya?.Invoke(L.ZeroVol, col, (float)GrosorZero + 0.4f, Trazo(LineaCapaZero), 210);
                        etiquetas.Add((L.ZeroVol, k.Nombre + " " + TextoZero, col, 1, k));
                    }
                }
                if (CapasMajorsVisibles)
                {
                    bool porOi = L.MaxAbsVol <= 0;
                    double mp = porOi ? L.MpOi : L.MpVol, mn = porOi ? L.MnOi : L.MnVol;
                    bool mpEsDom = L.Doms.Any(d => Math.Abs(d.Fut - mp) < 1.0), mnEsDom = L.Doms.Any(d => Math.Abs(d.Fut - mn) < 1.0);   // con centroide la dominante se corre unos centavos del strike: tolerancia de 1 pt
                    if (!double.IsNaN(mp) && !mpEsDom && Math.Abs(mp - futuro) <= radioMajors)
                    { raya?.Invoke(mp, col, 1f, Trazo(LineaCapaMajors), 110); etiquetas.Add((mp, k.Nombre + " " + TextoMajorPos, col, 1, k)); }
                    if (!double.IsNaN(mn) && !mnEsDom && Math.Abs(mn - futuro) <= radioMajors)
                    { raya?.Invoke(mn, col, 1f, Trazo(LineaCapaMajors), 110); etiquetas.Add((mn, k.Nombre + " " + TextoMajorNeg, col, 1, k)); }
                }
            }
            return etiquetas;
        }

        /// <summary>Agrupa niveles del mismo tipo (dominante con dominante, zero con zero...) de fuentes distintas que estan a
        /// menos de CapasFusionPct del precio: comparten raya y etiqueta.</summary>
        private List<List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)>> AgruparEtiquetas(
            List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)> etiquetas, double futuro)
        {
            var grupos = new List<List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)>>();
            double tolFusion = (!CapasFusionar || double.IsNaN(futuro)) ? 0 : futuro * (double)CapasFusionPct / 100.0;
            string TipoDe(string texto) { var t = texto.Substring(texto.IndexOf(' ') + 1); return t.StartsWith(TextoDominante) ? TextoDominante : t; }
            foreach (var e in etiquetas.OrderByDescending(r => r.Precio))
            {
                var ult = grupos.Count > 0 ? grupos[grupos.Count - 1] : null;
                if (ult != null && tolFusion > 0 && ult[0].Precio - e.Precio <= tolFusion && !ult.Any(z => z.K == e.K) && TipoDe(ult[0].Texto) == TipoDe(e.Texto)) ult.Add(e);
                else grupos.Add(new List<(double, string, Color, int, CapaLibro)> { e });
            }
            return grupos;
        }

        /// <summary>Los renglones de las capas para la escalera primaria (nombre corto, precio, color). Vacio sin capas.</summary>
        private List<(string N, double P, Color C, int Peso)> FilasCapas()
        {
            SincronizarColoresCapas();
            var salida = new List<(string N, double P, Color C, int Peso)>();
            if (!CapasEtiquetasEnEscalera || !VerEscalera || Rayas == EstiloRayas.Ninguna) return salida;
            var activas = _capas.Where(CapaActiva).ToList();
            if (activas.Count == 0) return salida;
            var lecturas = new Dictionary<CapaLibro, GammaHoyNucleo.Lectura>();
            double futuro;
            lock (_candado) { futuro = _futuro; foreach (var k in activas) lecturas[k] = k.L; }
            foreach (var gr in AgruparEtiquetas(EtiquetasCapas(activas, lecturas, futuro, null), futuro))
            {
                double pm = gr.Count == 1 ? gr[0].Precio : gr.Average(z => z.Precio);
                string tipo = gr[0].Texto.Substring(gr[0].Texto.IndexOf(' ') + 1);
                string sentido = tipo.StartsWith(TextoDominante) ? (pm > futuro ? " ▲" : " ▼") : tipo == TextoZero ? " ↕" : "";
                salida.Add((string.Join("·", gr.Select(z => z.K.Nombre)) + " " + tipo + sentido, pm, gr.Count == 1 ? gr[0].Col : ColTexto, gr.Max(z => z.Peso)));
            }
            return salida;
        }

        /// <summary>La sigla de la fuente en la punta de cada barra, letra minima, en su color; de arriba abajo y sin pisarse
        /// (si dos barras estan a menos de una letra de distancia, la segunda no lleva sigla). A la derecha de la punta en las
        /// barras de la izquierda; a la izquierda de la punta (alineada a la derecha) en las de la derecha.</summary>
        private void Siglas(RenderContext g, List<(int Y, int X, Color Col, string Texto, bool Der)> items, Rectangle area, int piso, RenderFont fMin, int altoMin)
        {
            int ultimoFondo = int.MinValue;
            foreach (var it in items.OrderBy(i => i.Y).ThenByDescending(i => i.Texto.Length))
            {
                int top = it.Y - altoMin / 2;
                if (top < area.Top || top + altoMin > piso) continue;
                if (top < ultimoFondo) continue;                 // pegada a la anterior: sin sigla
                var m = g.MeasureString(it.Texto, fMin);
                int x = it.Der ? it.X - m.Width : it.X;
                g.FillRectangle(Color.FromArgb(150, ColFondo), new Rectangle(x - 1, top, m.Width + 2, altoMin));
                g.DrawString(it.Texto, fMin, Color.FromArgb(235, it.Col), x, top);
                ultimoFondo = top + altoMin;
            }
        }

        /// <summary>Dibujo de las capas, disposicion C "superpuestas" afinada (15-09, 17:00): solo las barras que pesan
        /// (umbral por fuente), todas desde el borde izquierdo, transparentes y la mas larga atras; la primaria de
        /// fantasma. Sin perfil derecho ni columna de precios por defecto: la guia es el COLOR de la fuente, la barra y
        /// la abreviatura ("SPX D1") en la punta de la barra. Si dos fuentes tienen un nivel en el mismo lugar, se
        /// FUSIONAN: una sola raya gruesa con los colores alternados y un solo rotulo ("SPX·SPY D1", con un cuadrado
        /// por fuente). Leyenda abajo a la izquierda.</summary>
        private void PintarCapas(RenderContext g, IChartContainer cont, Rectangle area, int piso, int x0, int ancho, int alto,
                                 int xl0, int xl1, int xConv, int altoRot, RenderFont fRot, CultureInfo es,
                                 Action<double, Color, float, System.Drawing.Drawing2D.DashStyle, int> raya)
        {
            SincronizarColoresCapas();
            var activas = _capas.Where(CapaActiva).ToList();
            if (activas.Count == 0) return;
            _pintandoCapas = true;
            try
            {
                int xLey = Math.Max(x0 + ancho + 8, x0 + 235);   // a la derecha del cuadro Account de ATAS
                double futuro; lock (_candado) futuro = _futuro;
                var fMin = new RenderFont("Consolas", (float)Math.Max(6m, Math.Min(12m, TamLetra - 3m)));
                int altoMin = g.MeasureString("0", fMin).Height;
                var lecturas = new Dictionary<CapaLibro, GammaHoyNucleo.Lectura>();
                foreach (var k in activas) { GammaHoyNucleo.Lectura L; lock (_candado) L = k.L; lecturas[k] = L; }

                if (CapasLeyenda)   // apagada por defecto (15-09): los textos molestan; queda la escalera y las siglas
                {
                // 0) que es la primaria (las rayas ambar): su libro, y si esta oculta por duplicada
                {
                    string np = NombrePrimaria();
                    string descP = ""; foreach (var kk in _capas) if (kk.Nombre == np) { descP = kk.Descripcion; break; }
                    if (descP.Length > 0) descP = " (" + descP.Replace(" grabadas del gráfico de MES", "") + ")";
                    string leyP = PrimariaDuplicada()
                        ? "■ primaria (ámbar) = " + np + descP + " · misma cuenta que la capa " + np + (CapasOcultarPrimariaDuplicada ? ": oculta para no dibujarla dos veces" : " (las ámbar son ese mismo libro, con su estela)")
                        : "■ primaria (ámbar) = " + np + descP + " · sus niveles van en la escalera como " + np;
                    int ylp = piso - 4 - altoRot * (activas.Count + 1);
                    var mlp = g.MeasureString(leyP, fRot);
                    g.FillRectangle(Color.FromArgb(170, ColFondo), new Rectangle(xLey - 2, ylp, mlp.Width + 4, altoRot));
                    g.DrawString(leyP, fRot, Color.FromArgb(235, ColDom), xLey, ylp);
                }
                // 1) la leyenda, una linea por capa
                for (int i = 0; i < activas.Count; i++)
                {
                    var k = activas[i]; var L = lecturas[k]; var col = k.Color;
                    string doms = L == null || L.Doms.Count == 0 ? "" : " · " + string.Join(" ", L.Doms.Select((d, j) => TextoDominante + (j + 1) + " " + d.Fut.ToString("N0", es)));
                    string estado = L == null ? (k.C == null ? "sin dato" : "calculando") : "dato de hace " + k.Edad(es).Replace("hace ", "") + (L.SinBase ? " SIN BASE" : "");
                    if (L != null && k.C != null && k.C.EsFuturo && k.Tipo != CapaLibro.TipoCapa.VivaLocal) estado = "en vivo";
                    string beta = !k.PorBeta ? "" : " · β " + k.Beta.ToString("0.00", es) + " " + (k.BetaOrigen.StartsWith("velas") ? "medida (n " + k.BetaN + (double.IsNaN(k.BetaR2) ? "" : ", r² " + k.BetaR2.ToString("0.00", es)) + ")" : k.BetaOrigen.Replace("SUPUESTA", "supuesta"));
                    string toques = !CapasToques ? "" : " · rebotó " + k.Rebotes + " de " + k.Toques + " toques" + (k.Pendientes.Count > 0 ? " (+" + k.Pendientes.Count + " abierto)" : "");
                    string ley = "■ " + k.Nombre + (string.IsNullOrEmpty(k.Descripcion) ? "" : " (" + k.Descripcion + ")") + " · " + estado + beta + doms + toques + (string.IsNullOrEmpty(k.Error) ? "" : " · " + k.Error);
                    int yl = piso - 4 - altoRot * (activas.Count - i);
                    var ml = g.MeasureString(ley, fRot);
                    g.FillRectangle(Color.FromArgb(170, ColFondo), new Rectangle(xLey - 2, yl, ml.Width + 4, altoRot));
                    g.DrawString(ley, fRot, Color.FromArgb(235, col), xLey, yl);
                }
                }

                // 2) barras que pesan, superpuestas desde el borde izquierdo, de la mas larga a la mas corta
                var anchoBarra = new Dictionary<(CapaLibro, double), int>();
                if (CapasBarras)
                {
                    var barras = new List<(int W, int Y, Color Col, bool Neg, string Sigla)>();
                    foreach (var k in activas)
                    {
                        var L = lecturas[k]; if (L == null || L.SinBase || L.Perfil.Count == 0) continue;
                        bool porOi = L.MaxAbsVol <= 0; double maxK = porOi ? L.MaxAbsOi : L.MaxAbsVol; if (maxK <= 0) continue;
                        double umbral = Math.Max(UmbralBarraPct, CapasUmbralPct) / 100.0;
                        foreach (var s in L.Perfil)
                        {
                            double v = porOi ? s.GexOi : s.GexVol; if (v == 0) continue;
                            int idxDom = L.Doms.FindIndex(d => Math.Abs(d.Fut - s.Fut) < 1.0);
                            bool fijo = idxDom >= 0;
                            if (Math.Abs(v) < maxK * umbral && !fijo) continue;
                            int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                            if (y < area.Top || y > piso) continue;
                            int w = Math.Max(3, (int)(Math.Sqrt(Math.Abs(v) / maxK) * ancho));
                            anchoBarra[(k, s.Fut)] = w;
                            barras.Add((w, y, k.Color, v < 0, k.Nombre + (fijo ? " " + TextoDominante + (idxDom + 1) : "")));
                        }
                    }
                    foreach (var b in barras.OrderByDescending(b => b.W))
                    {
                        g.FillRectangle(Color.FromArgb(120, b.Col), new Rectangle(x0, b.Y - alto / 2, b.W, alto));
                        g.DrawLine(new RenderPen(Color.FromArgb(b.Neg ? 220 : 170, b.Neg ? ColNeg : b.Col), 1f), x0, b.Y + alto / 2, x0 + b.W, b.Y + alto / 2);
                    }
                    // PELOTITAS DEL MAX CHANGE POR CAPA (16-09): donde estaba la punta de ESA barra hace 15 (grande),
                    // 5 (mediana) y 1 min (chica), con las fotos por minuto del nucleo de la capa. Adentro de la
                    // barra = ese strike crece; afuera = decrece; las tres en la punta = quieto. Solo sobre las
                    // barras que se dibujaron, y solo con el libro por volumen (las fotos son de GEX por volumen).
                    if (PelotitasMaxChange) PelotitasCapas(g, cont, activas, lecturas, anchoBarra, x0, ancho, alto, false);
                    if (CapasSiglas) Siglas(g, barras.Select(b => (b.Y, x0 + b.W + 3, b.Col, b.Sigla, false)).ToList(), area, piso, fMin, altoMin);
                    int xt = x0 + 2;
                    foreach (var k in activas) { g.DrawString(k.Nombre, fRot, Color.FromArgb(220, k.Color), xt, area.Top + 8 + altoRot + 2); xt += g.MeasureString(k.Nombre + " ", fRot).Width; }
                }

                // 2b) la estela: un guion por vela y por dominante, en el color de la capa (D1 grueso, D2 fino)
                if (CapasEstela && (PuntoD1Ver || PuntoD2Ver || PuntoZeroVer))
                {
                    int desdeB = Math.Max(0, FirstVisibleBarNumber), hastaB = Math.Min(CurrentBar - 1, LastVisibleBarNumber);
                    int bw = 5;
                    try { if (hastaB > desdeB) bw = Math.Max(3, (cont.GetXByBar(hastaB, false) - cont.GetXByBar(desdeB, false)) / Math.Max(1, hastaB - desdeB)); } catch { }
                    if (AnchoGuion > 0) bw = AnchoGuion;
                    int grueso = Math.Max(1, GrosorGuion), fino = Math.Max(1, GrosorGuion - 1);
                    foreach (var k in activas)
                    {
                        Dictionary<int, List<(double Fut, int Rango, DateTime Hora)>> gui;
                        lock (_candado) gui = k.Guiones.Where(kv => kv.Key >= desdeB && kv.Key <= hastaB).ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
                        foreach (var kv in gui)
                        {
                            int x; try { x = cont.GetXByBar(kv.Key, false); } catch { continue; }
                            foreach (var gu in kv.Value)
                            {
                                int y; try { y = cont.GetYByPrice((decimal)gu.Fut, false); } catch { continue; }
                                if (y < area.Top || y > piso) continue;
                                // ESTILO C, "PUNTOS FINOS" (elegido por el operador 16-09 02:45 entre cuatro variantes): puntitos chicos e
                                // iguales para D1 y D2, nuevos o sostenidos, sin bandas gruesas ni brillo; el zero, mas chico y mas tenue.
                                // PUNTOS POR CAPA (16-09): D1, D2 y zero gamma con forma, tamaño, color y fuerza del grupo 6
                                if (!PuntosDeCapa(k)) continue;
                                PuntoCapa(g, k, gu.Rango % 10, gu.Rango >= 10, x, y, bw);
                            }
                        }
                    }
                }

                // 3) perfil derecho solo si se pide (en 0DTE es un espejo)
                if (VerConvexidad && CapasConvexidadVisible)
                {
                    int anchoDer = Math.Max(20, (int)(ancho * 0.7));
                    var barras = new List<(int W, int Y, Color Col, bool Neg, string Sigla)>();
                    var anchoConv = new Dictionary<(CapaLibro, double), int>();
                    foreach (var k in activas)
                    {
                        var L = lecturas[k]; if (L == null || L.SinBase || L.Perfil.Count == 0 || L.MaxAbsConv <= 0) continue;
                        double umbral = Math.Max(UmbralBarraPct, CapasUmbralPct) / 100.0;
                        foreach (var s in L.Perfil)
                        {
                            if (s.Conv == 0) continue;
                            double fr2 = Math.Abs(s.Conv) / L.MaxAbsConv;
                            int idxDom = L.Doms.FindIndex(d => Math.Abs(d.Fut - s.Fut) < 1.0);
                            if (fr2 < umbral && idxDom < 0) continue;
                            int y; try { y = cont.GetYByPrice((decimal)s.Fut, false); } catch { continue; }
                            if (y < area.Top || y > piso) continue;
                            barras.Add((Math.Max(3, (int)(Math.Sqrt(fr2) * anchoDer)), y, k.Color, s.Conv < 0, k.Nombre + (idxDom >= 0 ? " " + TextoDominante + (idxDom + 1) : "")));
                            anchoConv[(k, s.Fut)] = Math.Max(3, (int)(Math.Sqrt(fr2) * anchoDer));
                        }
                    }
                    foreach (var b in barras.OrderByDescending(b => b.W))
                    {
                        g.FillRectangle(Color.FromArgb(120, b.Col), new Rectangle(xConv - b.W, b.Y - alto / 2, b.W, alto));
                        g.DrawLine(new RenderPen(Color.FromArgb(b.Neg ? 220 : 170, b.Neg ? ColNeg : b.Col), 1f), xConv - b.W, b.Y + alto / 2, xConv, b.Y + alto / 2);
                    }
                    // las mismas tres pelotitas sobre la convexidad de cada capa (la referencia las lleva en los dos perfiles)
                    if (PelotitasMaxChange) PelotitasCapas(g, cont, activas, lecturas, anchoConv, xConv, anchoDer, alto, true);
                    if (CapasSiglas) Siglas(g, barras.Select(b => (b.Y, xConv - b.W - 3, b.Col, b.Sigla, true)).ToList(), area, piso, fMin, altoMin);
                }

                // 4) niveles: se listan sin dibujar, se agrupan los que coinciden, y recien ahi se dibujan las rayas
                var etiquetas = EtiquetasCapas(activas, lecturas, futuro, null);
                if (Rayas == EstiloRayas.Ninguna || etiquetas.Count == 0) return;
                double tolFusion = double.IsNaN(futuro) ? 0 : futuro * (double)CapasFusionPct / 100.0;
                var grupos = new List<List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)>>();
                foreach (var e in etiquetas.OrderByDescending(r => r.Precio))
                {
                    var ult = grupos.Count > 0 ? grupos[grupos.Count - 1] : null;
                    // se fusionan solo niveles del MISMO tipo (dominante con dominante, zero con zero...): el 15-09 a las
                    // 17:20 un "SPX D2" se fusiono con un "SPY 0Γ" y el rotulo mentia
                    string TipoDe(string texto) { var t = texto.Substring(texto.IndexOf(' ') + 1); return t.StartsWith("D") ? "D" : t; }
                    if (ult != null && tolFusion > 0 && ult[0].Precio - e.Precio <= tolFusion && !ult.Any(z => z.K == e.K) && TipoDe(ult[0].Texto) == TipoDe(e.Texto)) ult.Add(e);
                    else grupos.Add(new List<(double, string, Color, int, CapaLibro)> { e });
                }
                int xRaya0 = xl0, xRaya1 = xl1;
                var siglasZero = new List<(int Y, int X, Color Col, string Texto, bool Der)>();
                if (Rayas == EstiloRayas.Tenues) { /* las capas no se atenuan: son lo que se quiere ver */ }
                // RAYAS SEPARADAS (16-09, pedido: "no quiero ver mas lineas fusionadas"): dos niveles de libros distintos a
                // 3-5 pts se dibujaban uno encima del otro y, a guiones, los guiones se intercalaban y parecian UNA raya
                // rayada de dos colores. Ahora, si una raya cae a menos de 4 px de otra ya dibujada, se corre 4 px (de
                // arriba hacia abajo, en orden de precio): cada libro queda con su raya entera y su color. El renglon de
                // la escalera sigue diciendo el precio exacto.
                var ysCapas = new List<int>();
                const int sepPx = 4;
                foreach (var gr in CapasRayas ? grupos : new List<List<(double Precio, string Texto, Color Col, int Peso, CapaLibro K)>>())
                {
                    if (gr.Count == 1)
                    {
                        var e = gr[0];
                        if (Rayas == EstiloRayas.Ninguna) continue;
                        bool dom = e.Texto.Contains(" " + TextoDominante), zero = e.Texto.EndsWith(TextoZero);
                        int y1; try { y1 = cont.GetYByPrice((decimal)e.Precio, false); } catch { continue; }
                        if (y1 < area.Top || y1 > piso) continue;
                        while (ysCapas.Any(yu => Math.Abs(yu - y1) < sepPx)) y1 += sepPx;
                        ysCapas.Add(y1);
                        float w1 = zero ? (float)GrosorZero + 0.4f : dom ? (e.Peso >= 3 ? (float)GrosorCapaDominante + 0.1f : (float)Math.Max(0.6, (double)GrosorCapaDominante - 0.4)) : 1f;
                        var ds1 = zero ? Trazo(LineaCapaZero) : dom ? Trazo(LineaCapaDominante) : Trazo(LineaCapaMajors);
                        int a1 = zero ? 210 : dom ? (e.Peso >= 3 ? 220 : 160) : 120;
                        g.DrawLine(new RenderPen(Color.FromArgb(a1, e.Col), w1, ds1), xRaya0, y1, xRaya1, y1);
                        if (zero) siglasZero.Add((y1, xl0 + 2, e.Col, TextoZero + " " + e.K.Nombre, false));
                        continue;
                    }
                    // fusion: una raya gruesa, colores alternados por tramo, al precio medio del grupo
                    double pm = gr.Average(z => z.Precio);
                    int y; try { y = cont.GetYByPrice((decimal)pm, false); } catch { continue; }
                    if (y < area.Top || y > piso) continue;
                    int tramo = 9, n = gr.Count, j = 0;
                    for (int x = xRaya0; x < xRaya1; x += tramo, j++)
                        g.DrawLine(new RenderPen(Color.FromArgb(235, gr[j % n].Col), 2.6f), x, y, Math.Min(xRaya1, x + tramo - 2), y);
                }

                if (CapasSiglas && siglasZero.Count > 0) Siglas(g, siglasZero, area, piso, fMin, altoMin);
                // PROFUNDIDAD (16-09, punto 3): los 3 strikes con mas contratos apoyados en las opciones del futuro, como rombo y numero
                try
                {
                    var kNq = activas.FirstOrDefault(z => z.Tipo == CapaLibro.TipoCapa.RithmicViva);
                    if (kNq != null && ProfundidadOpciones)
                    {
                        var ap = _viva.ApoyoPorStrikeLados();
                        foreach (var z in ap.OrderByDescending(q => q.Value.cb + q.Value.ca + q.Value.pb + q.Value.pa).Take(3))
                        {
                            int ya; try { ya = cont.GetYByPrice((decimal)z.Key, false); } catch { continue; }
                            if (ya < area.Top || ya > piso) continue;
                            var ptsR = new[] { new Point(xl0 + 6, ya - 4), new Point(xl0 + 10, ya), new Point(xl0 + 6, ya + 4), new Point(xl0 + 2, ya) };
                            g.FillPolygon(Color.FromArgb(200, kNq.Color), ptsR);
                            // "apoyo 234 · C 120|60 · P 30|24": total; calls compra|venta; puts compra|venta (contratos apoyados)
                            double tot = z.Value.cb + z.Value.ca + z.Value.pb + z.Value.pa;
                            string txt = "apoyo " + tot.ToString("N0", es) + " · C " + z.Value.cb.ToString("N0", es) + "|" + z.Value.ca.ToString("N0", es) + " · P " + z.Value.pb.ToString("N0", es) + "|" + z.Value.pa.ToString("N0", es);
                            g.DrawString(txt, fMin, Color.FromArgb(200, kNq.Color), xl0 + 13, ya - altoMin / 2);
                        }
                    }
                }
                catch { }
                if (CapasEtiquetasEnEscalera && VerEscalera) return;   // las etiquetas viven en la escalera del eje
                // 5) las etiquetas, en SU CARRIL: entre el final de las rayas y las barras de la derecha (convexidad) o la
                //    escalera si la convexidad esta apagada. Alineadas a la derecha, ordenadas por precio, sin pisarse; si
                //    dos niveles coinciden comparten la etiqueta (un cuadrado por fuente). Con ▲/▼ si la dominante esta
                //    arriba/abajo del precio y ↕ para el zero. Nunca tocan las barras ni la escalera aunque el grafico avance.
                {
                    int xGut1 = xl1 + 2;                                   // borde derecho del carril (las rayas terminan en xl1)
                    string Sentido(string tipo, double precio) => tipo.StartsWith(TextoDominante) ? (precio > futuro ? " ▲" : " ▼") : tipo == TextoZero ? " ↕" : "";
                    int ultimoFondo = area.Top + altoRot * 2;            // debajo del titulo de las columnas
                    foreach (var gr in grupos)
                    {
                        double pm = gr.Count == 1 ? gr[0].Precio : gr.Average(z => z.Precio);
                        int y; try { y = cont.GetYByPrice((decimal)pm, false); } catch { continue; }
                        if (y < area.Top - altoRot || y > piso + altoRot) continue;
                        int top = y - altoRot / 2;
                        if (top < ultimoFondo + 1) top = ultimoFondo + 1;
                        if (top + altoRot > piso - altoRot * (activas.Count + 1)) break;   // no pisar la leyenda de abajo
                        string tipo = gr[0].Texto.Substring(gr[0].Texto.IndexOf(' ') + 1);
                        string texto = string.Join("·", gr.Select(z => z.K.Nombre)) + " " + tipo + Sentido(tipo, pm);
                        var m = g.MeasureString(texto, fRot);
                        int cuad = altoRot - 5, wCaja = gr.Count * (cuad + 3) + m.Width + 8;
                        int xc = xGut1 - wCaja;
                        g.FillRectangle(Color.FromArgb(225, ColFondo), new Rectangle(xc, top, wCaja, altoRot));
                        int xq = xc + 3;
                        foreach (var z in gr) { g.FillRectangle(Color.FromArgb(240, z.Col), new Rectangle(xq, top + 3, cuad, cuad)); xq += cuad + 3; }
                        g.DrawString(texto, fRot, Color.FromArgb(245, gr.Count == 1 ? gr[0].Col : ColTexto), xq + 1, top);
                        if (Math.Abs(top + altoRot / 2 - y) > 2 && y >= area.Top && y <= piso)
                            g.DrawLine(new RenderPen(Color.FromArgb(200, gr[0].Col), 1f), xc - 10, y, xc, y);   // marquita al precio exacto
                        ultimoFondo = top + altoRot;
                    }
                }
            }
            finally { _pintandoCapas = false; }
        }
    }
}
