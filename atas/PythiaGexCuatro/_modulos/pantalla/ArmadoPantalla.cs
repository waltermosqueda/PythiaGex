// ArmadoPantalla.cs — PythiaGex 4.1, modulo pantalla (B5), 08-10-2026. SIN referencias a ATAS (el arnes lo prueba solo).
// Port del dibujo del visor 4.0.6 (atas/PythiaGexCuatro/_visor_4_0_6/FamiliaCuatro.cs, Pintar) sobre la FotoFamilia del contrato (sin json):
// decide QUE se dibuja y DONDE (lista de primitivas); PantallaFamilia.cs lo pinta en el RenderContext de ATAS.
// Lo que el operador aprobo y se conserva tal cual:
//   * rayitas por vela: la vela m2 que contiene la vela del grafico (hora por TICKS crudos de LastTime||Time, Kind Unspecified = UTC);
//   * etiquetas chicas pegadas al eje (hasta la 4.1.1 con "+N" si varias series caian a <= 1 pt; ver 4.1.2 abajo); arriba de la columna la edad del dato
//     ("DATO DE HACE ..." ANTES de los numeros si pasa de 30 min);
//   * pestaña desplegable arriba a la izquierda (margen 56), arranca cerrada; abierta: fuentes, edades, conversiones y el detalle;
//   * doble eje elegible Ninguno/NDX/QQQ/TQQQ con la MISMA conversion que sus rayas (GetYByPrice por marca, paso automatico);
//   * control de vencimiento (el grafico en otro contrato que el de la Familia: no dibuja); sin sombreado (la banda solo en el texto).
// Cambios contra la 4.0.6 (no tocan lo que se dibuja sobre las velas): las fuentes y la edad salen de la foto del motor (no de un json);
// "EL GENERADOR NO ESCRIBE" pasa a "EL MOTOR NO CALCULA" (CalculadoUtc); el aviso del motor va en la pestaña; el precio MNQ del
// detalle es el cierre de la ultima vela del grafico; el detalle muestra el strike de cada nivel (trazabilidad).
// Describe; no anticipa: nada de direccion.
// 4.1.2 (08-10-2026, pedido del operador: "los millones o billones en tiempo real ... podes sacar esos +1 +2"): la etiqueta pasa a
// LIBRO ROL MONTO [CAMBIO] PRECIO FUENTE ("NDX P −558M 31.500 OI", "NQ C +45M ▲3,1M 31.100 V", "QQQ 0G 31.054 V", "3.0 NDX D1 +143M 31.036").
// Sin "+N": en un grupo (<= 1 pt) va la etiqueta del item con mayor |GexM|. El monto es M USD de cobertura por 1 % (calls +, puts −; no esta
// medido quien esta largo); el cambio ▲/▼ (crece/se achica en magnitud) sale de los campos que anota CambiosFamilia en el host: en series por
// volumen, el volumen operado en la ventana valuado con la gamma de AHORA (no dice si abren o cierran); en series por OI, la publicacion
// vigente contra la anterior (nunca con OI viejo). Con Monto41Rotulos y Cambio41Rotulos apagados vuelve la etiqueta de la 4.1.1 sin "+N".
// Arreglos de la revision 4.1.2 (09-10-2026):
//   * ▲/▼ por MAGNITUD de verdad: el valor de antes es GexM − Δ (la foto de antes valuada con la gamma de ahora). La regla "signo de Δ ==
//     signo de GexM" del diseño solo equivale a eso si |Δ| <= 2|GexM|: MAJORS_NQ_oi M+ 31.000 el 10-09 01:49Z (GexM +8,0, ΔOI +68,5; antes
//     −60,5) salia "▲68M" en verde cuando la magnitud BAJO de 60,5 a 8,0 y el neto dio vuelta. Ahora un neto que cambia de signo lleva ♦
//     ("dio vuelta", en blanco): ni crece ni se achica.
//   * el renglon de cambio del detalle (y el cambio de la etiqueta) solo para niveles con lado, la MISMA regla que CambiosFamilia.LadoDe:
//     CambiosFamilia anota arreglos NaN en TODOS los niveles y los zeros/cruces/CONF mostraban "vol 15 min: sin dato" (no aplica, no falta).
//   * los renglones de la pestaña se parten en el ancho VISIBLE (min(area, clip)), no en el del area, y el precio va primero en cada grupo:
//     en el grafico del operador (area 852 px, clip 781) la leyenda (127 letras) y los grupos largos se cortaban y se perdia el precio.
// 4.1.3 (09-10-2026): las dominantes como la 2.0 (R20_QQQ_vol "2.0 QQQ", R20_NDX_vol "2.0 NDX", DOMS_QQQ_vol "QQQ dom", DOMS_NDX_vol "NDX dom"),
//   rol D1/D2: etiqueta "2.0 QQQ D1 +417M 31.083,50 V"; el detalle de la pestaña dice la conversion de cada una (razon/base de la 2.0 o la
//   sincronizada de la 4.1) y las fuentes "2.0 QQQ" / "2.0 NDX" (su conversion y su edad) si su serie esta prendida. DOMS_* llevan el cambio
//   neto (como M+/M-); R20_* no (CambiosFamilia: NaN con nota).
// 4.1.4 (09-10-2026):
//   * ROTULOS DE LOS TRAMOS DE HISTORIA (pedido del operador: "la 31041 31040 etc dominantes, la doble o triple raya no tiene rotulo/etiqueta y
//     es importante"; "la linea roja no se que es"): la columna de la derecha solo nombra los niveles de AHORA; cada raya de la estela que ya no es
//     la vigente (tramo de >= 15 velas) lleva al final un rotulo chico con el nombre corto y el precio ("NDX muro V 31.040,74"; varias rayas de
//     la misma altura que terminan juntas, uno solo: "NDX muro V/OI · 2.0 NDX 31.041"). Ver RotularTramos. Ajuste Tramos41Rotulos.
//   * revision 4.1.3: en un grupo de etiquetas, las series "como la 2.0" que no son la cabeza van nombradas (" · QQQ dom D1"); el detalle dice
//     el ORIGEN real de la conversion de cada replica (y en naranja si es un respaldo); la fuente "2.0 QQQ" aclara que replica a la 2.0 en 1 min.
//   * revision 4.1.4: (1) un tramo terminado solo se "absorbe" en la MISMA serie al MISMO precio (muro C/P en el mismo strike); el de otra serie
//     lleva su rotulo en su propio final aunque otra raya siga a su lado (antes, dentro de una raya vigente quedaba sin nombre); (2) costo acotado:
//     juntar/agrupar con los TRAMO_CAND no vigentes mas largos; (3) un tramo rotulable dura ademas 8 velas m2 (~15 min: en graficos de segundos
//     los escalones de 2 min no se rotulan); (4) los rotulos no van en la franja de MargenSup4; (5) los acompañantes de la etiqueta van DESPUES
//     del precio y la fuente de la cabeza ("QQQ P −2,19B 31.076 OI · QQQ dom D1").
// 4.1.5 (09-10-2026): la replica de la clasica (R10_NDX_zero "Clasica NDX 0Γ", tipo ZEST, rol Z, sin monto): la etiqueta es "Clasica NDX 0Γ 31.068,21 V"
//   (el nombre corto ya dice que es el zero: sin repetir "0G"); su rotulo de tramo, "Clasica NDX 0Γ 31.068,21"; en un grupo va nombrada como las de la
//   2.0 si no es la cabeza; el detalle dice la base usada y de cuando es ("base clasica 243,06 de la rueda del 08-10, congelada") junto a la base de
//   ahora (la sincronizada de la 4.1) y la diferencia, y en naranja si la base es un respaldo (CRUDA o TEORICA); la fuente "Clasica NDX" (su base y su
//   origen) va en la pestaña si la serie esta prendida.
// 4.1.5d (09-10-2026, revision de la 4.1.5b/c): (1) debajo de la fuente "Clasica NDX" va la salvedad AVISO_CLASICA (si la clasica pierde Rithmic re-mide
//   su base en la rueda y la replica deja de coincidir; medido el 09-10: ~13 pts de 16:48 a 17:11 ART); (2) las D1-D3 de la clasica (R10_*) no llevan
//   cambio ▲▼ (TieneLado, igual que las R20_: su base y su S no son las del libro de la 4.1; antes D1/D2 lo tomaban del libro NDX de la 4.1 y D3 no);
//   (3) el detalle de R10_NDX_dom dice "(strike NDX 30.900, centroide ±12)" (antes "(en el indice NDX ...)", que es la leyenda del zero); (4) si la
//   fuente "Clasica NDX" dice "dominantes por OI", la etiqueta de R10_NDX_dom lleva OI en vez de V.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PythiaGexCuatro
{
    /// <summary>Doble eje de la izquierda. MISMO nombre y namespace que en el visor 4.0.6: el .ws guarda Eje4Libro como texto.</summary>
    public enum EjeFamilia4 { Ninguno, NDX, QQQ, TQQQ }

    /// <summary>4.1.2: ventana del cambio por volumen que muestran las etiquetas (Cambio41Ventana; el host calcula las tres: CambiosVentanas.Min).</summary>
    public enum VentanaCambio41
    {
        [Display(Name = "5 min")] M5,
        [Display(Name = "15 min")] M15,
        [Display(Name = "30 min")] M30
    }
}

namespace PythiaGexCuatro.Familia
{
    /// <summary>Foto de los ajustes de la pantalla (las propiedades de FamiliaCuatro) para un render.</summary>
    public sealed class AjustesPantalla
    {
        public readonly HashSet<string> Visibles = new HashSet<string>(StringComparer.Ordinal);
        public EjeFamilia4 Eje = EjeFamilia4.TQQQ;
        public bool Estela = true, Rotulos = true, Cabecera = true, PanelAbierto;
        public int Letra = 9, MargenSup = 56;
        /// <summary>4.1.2: Monto41Rotulos, Cambio41Rotulos y Cambio41Ventana.</summary>
        public bool Montos = true, Cambios = true;
        /// <summary>4.1.4: Tramos41Rotulos (rotulos chicos al final de cada tramo de historia que ya no es el vigente).</summary>
        public bool Tramos = true;
        public VentanaCambio41 Ventana = VentanaCambio41.M15;
        public int VentanaMin => Ventana == VentanaCambio41.M5 ? 5 : Ventana == VentanaCambio41.M30 ? 30 : 15;
        public bool Visible(string id) => id != null && Visibles.Contains(id);
        public void Limpiar() { Visibles.Clear(); }
    }

    /// <summary>Lo que el grafico de ATAS le da a la pantalla en un render (lo arma PantallaFamilia desde IGraficoPantalla).</summary>
    public sealed class VistaPantalla
    {
        public Rectangle Area;                       // ChartArea (mas alto que lo visible: nada anclado al fondo)
        public int XDerecha;                         // min(area.Right, clip.Right); 0 = area.Right
        public string Instrumento = "";              // InstrumentInfo.Instrument (MNQZ6)
        public double PrecioAlto = double.NaN, PrecioBajo = double.NaN;   // PriceChartContainer.High/Low
        public Func<double, int> Y;                  // GetYByPrice exacto; int.MinValue si no se puede
        public readonly List<(int X, long M2)> Velas = new List<(int X, long M2)>(1024);   // velas visibles: x y apertura m2 (ms UTC) de su hora
        public int AnchoVela = 6;
        public bool UltimaVisible = true;            // LastVisibleBarNumber >= CurrentBar - 1 (mirando velas viejas no van los niveles de AHORA)
        public double PrecioUltimo = double.NaN;     // cierre de la ultima vela del grafico (separador del detalle)
    }

    public enum TipoPrimPantalla : byte { Relleno, Borde, Linea, Texto }

    /// <summary>4.1.2: diagnostico de una etiqueta (arnes y log): de que nivel salio, que monto y que cambio lleva y donde quedo la caja.</summary>
    public sealed class RotuloPantalla
    {
        public string Txt = "", Serie = "", Rol = "", Monto = "", Cambio = "";
        /// <summary>CambioCrece: ▲ (la magnitud crecio). CambioVuelta: ♦ (el neto cambio de signo). Los dos en false con cambio: ▼.</summary>
        public bool CambioCrece, CambioVuelta;
        public Color Col, ColCambio;
        public double Precio = double.NaN;
        /// <summary>X0/Ancho: la caja (las de fuera de pantalla no tienen caja: X0 = x del texto). EnGrupo: cuantos niveles caen a &lt;= 1 pt.</summary>
        public int X0, Ancho, Y, YNivel, Fuera, EnGrupo;
        /// <summary>4.1.4 (revision 4.1.3): las series "como la 2.0" del grupo que no son la cabeza y van nombradas en la etiqueta
        /// (" · QQQ dom D1"); vacio si no hay.</summary>
        public string Acompanan = "";
    }

    /// <summary>4.1.4: diagnostico de un rotulo de tramo de historia (arnes y log). Un rotulo nombra una raya de la estela que ya no es la
    /// vigente (la columna de la derecha solo nombra las vigentes): una o varias series juntas, con el precio del final del tramo.</summary>
    public sealed class RotuloTramo
    {
        /// <summary>El texto entero ("NDX muro V/OI · NDX major V 31.040").</summary>
        public string Txt = "";
        /// <summary>Las series que nombra (ids, en el orden del catalogo).</summary>
        public readonly List<string> Series = new List<string>();
        /// <summary>El precio que dice el rotulo (el del final del tramo cabeza, sin redondear).</summary>
        public double Precio = double.NaN;
        /// <summary>Largo del tramo mas largo del grupo (velas del grafico) y cuantos tramos junta.</summary>
        public int Largo, Tramos;
        /// <summary>Indices (en VistaPantalla.Velas) del principio y del final del tramo cabeza, y la x del final de su rayita.</summary>
        public int VelaIni, VelaFin, XFin;
        /// <summary>Donde quedo el texto (la caja tenue).</summary>
        public Rectangle Caja;
        public Color Col;
    }

    /// <summary>Una primitiva de dibujo. Relleno/Borde: R. Linea: (R.X, R.Y) a (X2, Y2), A = grosor. Texto: T en (R.X, R.Y), A = tamaño de letra.</summary>
    public readonly struct PrimPantalla
    {
        public readonly TipoPrimPantalla Tipo; public readonly Color C; public readonly Rectangle R; public readonly int X2, Y2; public readonly float A; public readonly string T;
        public PrimPantalla(TipoPrimPantalla tipo, Color c, Rectangle r, int x2, int y2, float a, string t) { Tipo = tipo; C = c; R = r; X2 = x2; Y2 = y2; A = a; T = t; }
    }

    /// <summary>El resultado de un armado (se reusa entre renders: solo el hilo de dibujo lo toca).</summary>
    public sealed class DibujoPantalla
    {
        public readonly List<PrimPantalla> Prims = new List<PrimPantalla>(4096);
        public Rectangle Pestana = Rectangle.Empty;   // donde cae el clic que abre/cierra
        // diagnostico (arnes, log)
        public string Cartel = "", Titulo = "", EjeTitulo = "", EdadColumna = "";
        public Color ColorTitulo, ColorCartel;
        public readonly List<(string Txt, int Y, int YNivel)> Etiquetas = new List<(string, int, int)>();
        public readonly List<RotuloPantalla> Rotulos = new List<RotuloPantalla>();     // 4.1.2: una por etiqueta, mismo orden que Etiquetas
        public readonly List<string> Panel = new List<string>();
        public readonly List<(double V, int Y)> Marcas = new List<(double, int)>();
        public int Rayitas;
        /// <summary>4.1.4: los rotulos de los tramos de historia que se dibujaron (orden: del tramo mas largo al mas corto).</summary>
        public readonly List<RotuloTramo> RotulosTramos = new List<RotuloTramo>();
        /// <summary>4.1.4 (diagnostico): tramos detectados de al menos TRAMO_MIN velas, cuantos eran vigentes, cuantos grupos quedaron para rotular
        /// y cuantos se descartaron por choque o por el tope. TramosRecortados (revision 4.1.4): tramos no vigentes que quedaron afuera por ser mas
        /// de TRAMO_CAND (los mas cortos; 0 con datos reales).</summary>
        public int TramosLargos, TramosVigentes, TramosGrupos, TramosDescartados, TramosRecortados;
        /// <summary>Revision 4.1.4 (diagnostico, arnes): las rayas que no se rotularon (Txt = por que: "tope", "columna" o "sin lugar"; Series, Precio,
        /// VelaIni/VelaFin y XFin como en un rotulo puesto; Caja vacia).</summary>
        public readonly List<RotuloTramo> TramosNoPuestos = new List<RotuloTramo>();

        public void Limpiar()
        {
            Prims.Clear(); Pestana = Rectangle.Empty; Cartel = ""; Titulo = ""; EjeTitulo = ""; EdadColumna = "";
            ColorTitulo = Color.Empty; ColorCartel = Color.Empty; Etiquetas.Clear(); Rotulos.Clear(); Panel.Clear(); Marcas.Clear(); Rayitas = 0;
            RotulosTramos.Clear(); TramosNoPuestos.Clear(); TramosLargos = 0; TramosVigentes = 0; TramosGrupos = 0; TramosDescartados = 0; TramosRecortados = 0;
        }
        internal void Relleno(Color c, Rectangle r) => Prims.Add(new PrimPantalla(TipoPrimPantalla.Relleno, c, r, 0, 0, 0, null));
        internal void Borde(Color c, float grosor, Rectangle r) => Prims.Add(new PrimPantalla(TipoPrimPantalla.Borde, c, r, 0, 0, grosor, null));
        internal void Linea(Color c, float grosor, int x1, int y1, int x2, int y2) => Prims.Add(new PrimPantalla(TipoPrimPantalla.Linea, c, new Rectangle(x1, y1, 0, 0), x2, y2, grosor, null));
        internal void Texto(string t, float tam, Color c, int x, int y) => Prims.Add(new PrimPantalla(TipoPrimPantalla.Texto, c, new Rectangle(x, y, 0, 0), 0, 0, tam, t));
    }

    /// <summary>El catalogo que usa la pantalla: el del motor (manda) completado con el respaldo de SeriesPantalla; colores ya parseados.</summary>
    public sealed class CatalogoPantalla
    {
        public readonly IReadOnlyList<SerieInfo> Lista;
        public readonly Dictionary<string, SerieInfo> PorId = new Dictionary<string, SerieInfo>(StringComparer.Ordinal);
        public readonly Dictionary<string, Color> Colores = new Dictionary<string, Color>(StringComparer.Ordinal);
        /// <summary>4.1.4: nombre corto de cada serie para los rotulos de los tramos de historia: base ("NDX muro") y fuente ("V", "OI" o "").</summary>
        public readonly Dictionary<string, (string Base, string Suf)> NombreTramo = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        /// <summary>4.1.4: posicion de cada serie en Lista (el orden en que se nombran las series de un rotulo).</summary>
        public readonly Dictionary<string, int> Orden = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly bool DelMotor;
        public CatalogoPantalla(IReadOnlyList<SerieInfo> delMotor)
        {
            var l = new List<SerieInfo>();
            if (delMotor != null)
                foreach (var s in delMotor)
                    if (s != null && !string.IsNullOrEmpty(s.Id) && !PorId.ContainsKey(s.Id)) { l.Add(s); PorId[s.Id] = s; }
            DelMotor = l.Count > 0;
            foreach (var s in SeriesPantalla.Respaldo)
                if (!PorId.ContainsKey(s.Id)) { l.Add(s); PorId[s.Id] = s; }
            foreach (var s in l) Colores[s.Id] = SeriesPantalla.ColorDe(s.ColorHex);
            for (int i = 0; i < l.Count; i++) { Orden[l[i].Id] = i; NombreTramo[l[i].Id] = ArmadoPantalla.NombreDeTramo(l[i], l); }
            Lista = l.AsReadOnly();
        }
    }

    /// <summary>El armado: FotoFamilia + ajustes + vista del grafico -> primitivas. Puro (sin ATAS, sin E/S, sin llaves). Nunca deja la foto a medias.</summary>
    public static class ArmadoPantalla
    {
        public static readonly Color ColTexto = Color.FromArgb(220, 228, 236);
        public static readonly Color ColFondo = Color.FromArgb(8, 12, 18);
        public static readonly Color ColNaranja = Color.FromArgb(255, 150, 40);
        public static readonly Color ColRojo = Color.FromArgb(240, 60, 60);
        public static readonly Color ColTqqq = Color.FromArgb(255, 213, 79);
        public static readonly Color ColPrecio = Color.FromArgb(243, 211, 90);
        public static readonly Color ColSube = Color.FromArgb(0x08, 0x99, 0x81);   // 4.1.2: ▲ el muro crece en magnitud (ColPos de la 3.0)
        public static readonly Color ColBaja = Color.FromArgb(0xf2, 0x36, 0x45);   // 4.1.2: ▼ se achica (ColNeg de la 3.0; ojo: parecido a ColRojo)
        public static readonly Color ColVuelta = Color.FromArgb(255, 255, 255);    // 4.1.2 (revision): ♦ el neto dio vuelta (ni crece ni se achica)
        public const string VUELTA = "♦";              // 4.1.2 (revision): ▲ y ▼ juntos; Consolas lo tiene con el mismo avance que '0' (C60)
        public const double CAMBIO_MIN = 0.05;          // 4.1.2: |cambio| que Monto redondea a "0M": no va en la etiqueta
        private const string MENOS = "−";           // signo menos tipografico (Consolas lo tiene, mismo ancho que '0')
        public static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-AR");
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public const double VIEJO_S = 1800;           // protocolo: con mas de 30 min la edad va ANTES del numero
        /// <summary>4.1.6: estilo de etiqueta 2E (elegido por el operador 09-10 entre 21 maquetas: _previews_etiquetas): una columna a la derecha,
        /// sin precio, con una barrita de peso LINEAL antes del texto (la de mayor |monto| visible = 100 %) y el cambio pegado al monto.</summary>
        public const bool ESTILO_2E = true;
        public const int BARRA_2E = 44;               // ancho del carril de la barrita (px)
        public const double MOTOR_PARADO_S = 150;     // el motor publica cada ~5 s: 150 s sin foto nueva = parado
        // 4.1.4 (pedido del operador 09-10: "la doble o triple raya no tiene rotulo/etiqueta y es importante"): rotulos de los tramos de historia
        public const double TRAMO_TOL = 0.5;          // mismo tramo: a <= 0,5 pt del ultimo precio del tramo
        public const int TRAMO_HUECO = 3;             // velas seguidas sin ese precio que se toleran adentro de un tramo
        public const int TRAMO_MIN = 15;              // velas del grafico (del principio al final del tramo) para rotularlo
        // revision 4.1.4: Y al menos 8 velas m2 de la historia (unos 15 min): la historia cambia de a 2 min, y en un grafico de segundos cada
        // escalon de 2 min ya pasaba las 15 velas (5 s: 24 velas) y llenaba el tope con escalones. En 1 min, 15 velas seguidas son siempre 8 m2.
        public const int TRAMO_MIN_M2 = 8;
        public const double TRAMO_JUNTAR_PTS = 1.5;   // tramos a <= 1,5 pt ...
        public const int TRAMO_JUNTAR_PX = 40;        // ... con el final a <= 40 px: un solo rotulo
        public const int TRAMO_TOPE = 14;             // rotulos de tramos por pantalla (los de los tramos mas largos)
        public const int TRAMO_PIEZAS = 4;            // nombres por rotulo como mucho (los de las series que mas velas dibujaron en esa raya)
        // revision 4.1.4: el juntar y el agrupar comparan tramos de a pares: antes de eso quedan los TRAMO_CAND no vigentes mas largos en pantalla
        // (alcanza de sobra para 14 rotulos; con niveles que saltan cada 16 velas habia 8.200-16.400 tramos y el render tardaba 220-570 ms)
        public const int TRAMO_CAND = 200;
        private static readonly Regex ReVencNq = new Regex(@"NQ\s*([FGHJKMNQUVXZ])\s*(\d{1,4})", RegexOptions.Compiled);
        private static readonly Regex ReVencSolo = new Regex(@"^([FGHJKMNQUVXZ])(\d{1,4})$", RegexOptions.Compiled);

        /// <summary>Mes + ultimo digito del año del contrato: "MNQZ6" -> "Z6", "NQZ26" -> "Z6", "Z6" -> "Z6" (lo que publica el motor). "" si no se entiende.</summary>
        public static string Venc(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var u = s.Trim().ToUpperInvariant();
            var m = ReVencNq.Match(u);
            if (!m.Success) m = ReVencSolo.Match(u);
            if (!m.Success) return "";
            var y = m.Groups[2].Value;
            return m.Groups[1].Value + y[y.Length - 1];
        }

        /// <summary>Edad legible (la de la 4.0.6): 40 s, 12 min, 1,5 h, 14 h.</summary>
        public static string Edad(double s)
        {
            if (double.IsNaN(s)) return "?";
            if (s < 0) s = 0;
            if (s < 90) return Math.Round(s).ToString(Inv) + " s";
            if (s < 3600) return Math.Round(s / 60).ToString(Inv) + " min";
            return (s / 3600).ToString(s >= 36000 ? "0" : "0.0", Es) + " h";
        }

        /// <summary>Precio en formato del operador: 31.073 o 31.073,31.</summary>
        public static string P(double p) => double.IsNaN(p) ? "?" : p.ToString(Math.Abs(p - Math.Round(p)) < 1e-6 ? "#,##0" : "#,##0.00", Es);

        /// <summary>Strike de TQQQ en la etiqueta, como la 4.0.6: "82", "80,5", "80,88".</summary>
        private static string StrikeTqqq(double k) => double.IsNaN(k) ? "" : k.ToString(Math.Abs(k * 2 - Math.Round(k * 2)) < 1e-6 ? "0.#" : "0.00", Es);

        /// <summary>Strike en el detalle de la pestaña (unidad de su libro): "31.060", "750", "760,2".</summary>
        private static string Strike(double k) => double.IsNaN(k) ? "" : k.ToString("#,##0.##", Es);

        private static FuenteEstado Fuente(IReadOnlyList<FuenteEstado> fs, string libro)
        {
            if (fs == null) return null;
            foreach (var f in fs) if (f != null && f.Libro == libro) return f;
            return null;
        }

        private static string Recortar(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

        /// <summary>4.1.2 (revision): parte un renglon de la pestaña en pedazos de a lo sumo n letras (Consolas: monoespaciada), por el ultimo
        /// espacio que entre; la continuacion lleva la sangria del renglon + 2 espacios (para que se vea que sigue). Sin un espacio util: corte
        /// duro. No pierde ni cambia ninguna palabra (solo los espacios del corte).</summary>
        public static void Partir(string t, Color c, int n, List<(string T, Color C)> sal)
        {
            if (string.IsNullOrEmpty(t) || t.Length <= n || n < 8) { sal.Add((t ?? "", c)); return; }
            int sang = 0; while (sang < t.Length && t[sang] == ' ') sang++;
            string pre = new string(' ', Math.Min(sang + 2, n / 2));
            string resto = t; bool primero = true;
            for (int vueltas = 0; vueltas < 1000; vueltas++)
            {
                string cab = primero ? "" : pre;
                string cuerpo = primero ? resto : resto.TrimStart(' ');
                if (cuerpo.Trim().Length == 0) break;
                int cabe = n - cab.Length;
                if (cuerpo.Length <= cabe) { sal.Add((cab + cuerpo, c)); break; }
                int corte = cuerpo.LastIndexOf(' ', cabe);                     // el pedazo es cuerpo[0, corte): entra
                if (corte <= (primero ? sang : 0) + cabe / 3) corte = cabe;      // sin espacio util (o muy al principio): corte duro
                sal.Add((cab + cuerpo.Substring(0, corte).TrimEnd(' '), c));
                resto = cuerpo.Substring(corte);
                primero = false;
            }
        }

        private static bool Fin(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        /// <summary>4.1.6: el monto CON SIGNO de la parte de mayor |monto| de la cabeza del grupo (NaN si no tiene monto).</summary>
        private static double PesoFirmado(Item c)
        {
            if (c?.Partes == null) return double.NaN;
            double best = double.NaN;
            foreach (var p in c.Partes) if (p != null && Fin(p.GexM) && (double.IsNaN(best) || Math.Abs(p.GexM) > Math.Abs(best))) best = p.GexM;
            return best;
        }

        // ------------------------------------------------------------------ 4.1.2: montos y cambios
        /// <summary>4.1.2: el monto de la etiqueta (M USD de cobertura por 1 %, calls +, puts −). NaN/Inf -> ""; |m| &lt; 0,05 -> "0M";
        /// &lt; 9,95 -> 1 decimal ("+0,4M", "−2,1M"); &lt; 999,5 -> entero ("+102M"); en B (mil millones): &lt; 9,995 -> 2 decimales ("+2,30B"),
        /// &lt; 99,95 -> 1 decimal, si no entero. Signo "+" o "−" (U+2212). Coma decimal es-AR. Los cortes son los del redondeo: 9,96 da "+10M"
        /// (no "+10,0M"), 999,6 da "+1,00B" (no "+1000M").</summary>
        public static string Monto(double m)
        {
            if (!Fin(m)) return "";
            double a = Math.Abs(m);
            if (a < CAMBIO_MIN) return "0M";
            return (m > 0 ? "+" : MENOS) + MontoAbs(a);
        }

        /// <summary>El monto sin signo (para el cambio: "▲3,1M"). a >= 0 y finito.</summary>
        private static string MontoAbs(double a)
        {
            if (a < 9.95) return a.ToString("0.0", Es) + "M";
            if (a < 999.5) return a.ToString("0", Es) + "M";
            double b = a / 1000;
            if (b < 9.995) return b.ToString("0.00", Es) + "B";
            if (b < 99.95) return b.ToString("0.0", Es) + "B";
            return b.ToString("#,##0", Es) + "B";
        }

        /// <summary>4.1.2 (revision): hacia donde fue la MAGNITUD del nivel con el cambio d (mismas unidades que GexM g): +1 crece (▲), −1 se
        /// achica (▼), 0 dio vuelta (♦: el neto cambio de signo). El valor de antes es g − d: la foto de antes valuada con la gamma de ahora.
        /// Un lado con |x| &lt; 0,05 (lo que Monto escribe "0M") no tiene signo: de ~0 a algo es crecer, de algo a ~0 es achicarse (asi el redondeo
        /// de GexM a 1 decimal no inventa vueltas en un muro C). Sin monto (NaN, Inf): +1 si el cambio es positivo (regla del diseño).
        /// La regla del diseño "signo de Δ == signo de GexM" da lo mismo mientras |Δ| &lt;= 2|GexM|; con |Δ| &gt; 2|GexM| el neto dio vuelta y la
        /// magnitud pudo bajar (MAJORS_NQ_oi M+ 31.000, 10-09 01:49Z: +8,0 con ΔOI +68,5, antes −60,5: la regla vieja decia ▲).</summary>
        private static int Sentido(double d, double g)
        {
            if (!Fin(g)) return d > 0 ? 1 : -1;
            double antes = g - d;                       // d y g finitos: antes finito o ±Inf, nunca NaN
            if (Math.Abs(g) >= CAMBIO_MIN && Math.Abs(antes) >= CAMBIO_MIN && (g > 0) != (antes > 0)) return 0;
            return Math.Abs(g) > Math.Abs(antes) ? 1 : -1;
        }

        private static string Marca(int sentido) => sentido > 0 ? "▲" : sentido < 0 ? "▼" : VUELTA;
        private static Color ColorSentido(int sentido) => sentido > 0 ? ColSube : sentido < 0 ? ColBaja : ColVuelta;

        /// <summary>El cambio del detalle: "▲3,1M" / "▼20M" / "♦ dio vuelta +68M (antes −60M)"; "0M" si redondea a cero. d finito.</summary>
        private static string Flecha(double d, double g)
        {
            if (Math.Abs(d) < CAMBIO_MIN) return "0M";
            int s = Sentido(d, g);
            if (s != 0) return Marca(s) + MontoAbs(Math.Abs(d));
            string antes = Monto(g - d);
            return VUELTA + " dio vuelta " + Monto(d) + (antes != "" ? " (antes " + antes + ")" : "");
        }

        /// <summary>4.1.2 (revision): el nivel tiene lado y por lo tanto cambio. La MISMA regla que CambiosFamilia.LadoDe (posiciones/): Tipo
        /// MUROS/MAJORS/DOMS/RAZON con rol muro C, muro P, M+, M-, dom o dom (razon). CambiosFamilia anota arreglos NaN en TODOS los niveles
        /// (tambien ZEST/ZTP/CONF/TRES): en esos el cambio no aplica (no es que falte), asi que no va ni en la etiqueta ni en el detalle.</summary>
        public static bool TieneLado(NivelActual a)
        {
            if (a == null) return false;
            if (a.Tipo != "MUROS" && a.Tipo != "MAJORS" && a.Tipo != "DOMS" && a.Tipo != "RAZON") return false;
            switch (a.Rol)
            {
                case "muro C": case "muro P": case "M+": case "M-": case "dom": case "dom (razon)": return true;
                // 4.1.3: la seleccion de la 2.0 sobre los libros de la 4.1 (DOMS_QQQ_vol / DOMS_NDX_vol) lleva el cambio neto; las replicas de la
                // 2.0 (R20_*) no (CambiosFamilia las deja NaN con nota: su conversion no es la del libro de la 4.1)
                // 4.1.5d: las de la clasica (R10_*) tampoco (su base y su S no son las del libro NDX de la 4.1; antes D1/D2 tenian flecha y D3 no)
                case "D1": case "D2": return a.Tipo == "DOMS" && !EsReplica20(a.Serie) && !EsClasica(a.Serie);
                default: return false;
            }
        }

        /// <summary>4.1.3: las replicas de la 2.0 (R20_QQQ_vol, R20_NDX_vol).</summary>
        public static bool EsReplica20(string serie) => serie != null && serie.StartsWith("R20_", StringComparison.Ordinal);

        /// <summary>4.1.3: de donde sale el precio de las dominantes como la 2.0, para el detalle de la pestaña: la conversion de la 2.0 (R20_*) o la
        /// sincronizada de la 4.1 (DOMS_*). "" para el resto de las series.
        /// 4.1.4 (revision 4.1.3): el texto de las replicas sale del ORIGEN real de su conversion (el texto de su fuente, que arma Replica20): antes
        /// decia siempre "MNQ del ultimo trade / QQQ spot" y "cruda de forwards", tambien cuando la razon era la CRUDA de la guardia del 0,6 %, la
        /// ultima valida, la mediana de la rueda o la CRUDA sin alinear, o la base la TEORICA del carry (protocolo: cada numero con su fuente real).</summary>
        public static string ConvDe(string serie, IReadOnlyList<FuenteEstado> fuentes)
        {
            switch (serie)
            {
                case "R20_QQQ_vol":
                {
                    var f = Fuente(fuentes, "2.0 QQQ"); if (f == null || !Fin(f.ConvValor)) return " (razon 2.0 sin dato)";
                    return " (razon 2.0 " + f.ConvValor.ToString("0.0000", Es) + " = " + OrigenConv20(serie, f.Texto).Txt + ")";
                }
                case "R20_NDX_vol":
                {
                    var f = Fuente(fuentes, "2.0 NDX"); if (f == null || !Fin(f.ConvValor)) return " (base 2.0 sin dato)";
                    return " (base 2.0 " + f.ConvValor.ToString("0.00", Es) + ", " + OrigenConv20(serie, f.Texto).Txt + ")";
                }
                case "DOMS_QQQ_vol": { var f = Fuente(fuentes, "QQQ"); return f == null || !Fin(f.ConvValor) ? "" : " (razon sincronizada " + f.ConvValor.ToString("0.0000", Es) + ")"; }
                case "DOMS_NDX_vol": { var f = Fuente(fuentes, "NDX"); return f == null || !Fin(f.ConvValor) ? "" : " (base sincronizada " + f.ConvValor.ToString("0.00", Es) + ")"; }
                case "R10_NDX_zero":
                case "R10_NDX_dom":                                  // 4.1.5b: la misma base que el 0Γ
                {   // 4.1.5: la base usada, de cuando es, y la de ahora (la sincronizada de la 4.1) con la diferencia: lo que corre a la raya
                    var f = Fuente(fuentes, "Clasica NDX"); if (f == null || !Fin(f.ConvValor)) return " (base clasica sin dato)";
                    var (oTxt, _, ah, _) = OrigenClasica(f.Texto);
                    string dif = Fin(ah) ? "; base de ahora " + ah.ToString("0.00", Es) + " (sincronizada): " + (f.ConvValor - ah >= 0 ? "+" : MENOS) + Math.Abs(f.ConvValor - ah).ToString("0.00", Es) + " pts" : "";
                    return " (base clasica " + f.ConvValor.ToString("0.00", Es) + " " + oTxt + dif + ")";
                }
                default: return "";
            }
        }

        /// <summary>4.1.4 (revision 4.1.3): de donde salio la conversion de una replica de la 2.0, leido del texto de su fuente ("2.0 QQQ": Replica20
        /// TextoRazon = "razon 2.0 X[ = MNQ ... / spot QQQ Y] · ORIGEN[ · ...]"; "2.0 NDX": "base 2.0 X (ORIGEN ...; cota ...)"). Normal = la conversion
        /// de siempre de la 2.0 (la vela de 1 min del ultimo trade con sus ticks; la base cruda de forwards que paso la cota del carry); si no,
        /// es un respaldo y el detalle lo dice en naranja. Texto vacio o desconocido: no normal ("origen sin dato").</summary>
        public static (string Txt, bool Normal) OrigenConv20(string serie, string texto)
        {
            texto = texto ?? "";
            if (serie == "R20_NDX_vol")
            {
                if (texto.IndexOf("(TEORICA", StringComparison.Ordinal) >= 0) return ("TEORICA: el carry (la cruda de forwards no paso la cota)", false);
                if (texto.IndexOf("(sin cota)", StringComparison.Ordinal) >= 0) return ("cruda de forwards SIN cota (no hay carry para comparar)", false);
                if (texto.IndexOf("(CRUDA", StringComparison.Ordinal) >= 0) return ("cruda de forwards", true);
                return ("origen sin dato", false);
            }
            if (serie != "R20_QQQ_vol") return ("", true);
            var partes = texto.Split(new[] { " · " }, StringSplitOptions.None);
            string cab = partes[0], origen = partes.Length > 1 ? partes[1] : "";
            // " = MNQ <vela>" solo lo escribe TextoRazon con la razon valida que salio de la vela alineada (con el origen "vela alineada ...")
            if (cab.IndexOf(" = MNQ ", StringComparison.Ordinal) >= 0 && (origen == "" || origen.StartsWith("vela alineada", StringComparison.Ordinal)))
                return cab.IndexOf("vela m2 interpolada", StringComparison.Ordinal) >= 0
                    ? ("MNQ del ultimo trade (vela m2 interpolada: la cinta no tiene esos ticks) / QQQ spot", false)
                    : ("MNQ del ultimo trade / QQQ spot", true);
            if (origen.StartsWith("CRUDA: la vela alineada es de otro contrato", StringComparison.Ordinal))
                return ("CRUDA: MNQ de ahora / QQQ spot (la vela del ultimo trade era de otro contrato: guardia del 0,6 %)", false);
            if (origen.StartsWith("ultima valida", StringComparison.Ordinal))
            {
                int pc = origen.IndexOf(';');
                return ("la ultima razon valida (" + (pc > 0 ? origen.Substring(0, pc) : origen).Replace("ultima valida ", "") + "), sin vela alineada", false);
            }
            if (origen.StartsWith("mediana de la rueda", StringComparison.Ordinal)) return ("la mediana de la rueda, sin vela alineada", false);
            if (origen.StartsWith("CRUDA sin alinear", StringComparison.Ordinal)) return ("CRUDA: MNQ de ahora / QQQ spot, sin vela alineada ni razon previa", false);
            return (origen == "" ? "origen sin dato" : "origen: " + Recortar(origen, 70), false);
        }

        /// <summary>4.1.4: las series "como la 2.0" (R20_* y DOMS_*; no T_DOMS_vol).</summary>
        public static bool EsSerie20(string serie) => serie != null && (serie.StartsWith("R20_", StringComparison.Ordinal) || serie.StartsWith("DOMS_", StringComparison.Ordinal));

        /// <summary>4.1.5: la replica de la clasica (R10_NDX_zero "Clasica NDX 0Γ"): su nombre corto ya dice el rol (el zero), no se repite.</summary>
        public static bool EsClasica(string serie) => serie != null && serie.StartsWith("R10_", StringComparison.Ordinal);

        /// <summary>4.1.5d (revision 4.1.5b/c): la salvedad de la replica de la clasica, debajo de su fuente en la pestaña. MEDIDO el 09-10: tras los
        /// reinicios de ATAS la clasica cayo a CBOE y re-midio su base en la rueda (229,88 -> 230,16) mientras la replica seguia con la del 08-10
        /// (243,06): de 19:48 a 20:11 UTC (16:48-17:11 ART) el 0Γ y las D1-D3 quedaron ~13 pts corridos de los de la clasica.</summary>
        public const string AVISO_CLASICA = "    Clasica NDX: copia a la clasica cuando dibuja con la base de la rueda anterior; si la clasica pierde Rithmic (p. ej. al reiniciar ATAS) "
                                          + "re-mide su base en la rueda y esta replica deja de coincidir (09-10 16:48-17:11 ART: ~13 pts)";

        private static readonly Regex ReRuedaClasica = new Regex(@"= de la rueda (\d{4})-(\d{2})-(\d{2})", RegexOptions.Compiled);
        private static readonly Regex ReAhoraClasica = new Regex(@"4\.1 sincronizada (-?\d+(?:\.\d+)?)", RegexOptions.Compiled);
        private static readonly Regex ReHoyClasica = new Regex(@"rueda de hoy (-?\d+(?:\.\d+)?) \((\d+) muestras\)", RegexOptions.Compiled);

        /// <summary>4.1.5: de donde salio la base de la replica de la clasica, leido del texto de su fuente ("Clasica NDX", ClasicaNdx.Texto:
        /// "base clasica X = ORIGEN (...) · ahora: 4.1 sincronizada Y; regla de la clasica con la rueda de hoy Z (n muestras) · zero en el indice W").
        /// Normal = la base de la rueda (la de la clasica); si no (CRUDA, TEORICA, sin cota) es un respaldo y el detalle lo dice en naranja.
        /// Ahora = la base sincronizada de la 4.1 del mismo minuto (NaN si no esta); Hoy = la regla de la clasica con la rueda de hoy (NaN si no hay).</summary>
        public static (string Txt, bool Normal, double Ahora, double Hoy) OrigenClasica(string texto)
        {
            texto = texto ?? "";
            double ahora = double.NaN, hoy = double.NaN;
            var ma = ReAhoraClasica.Match(texto); if (ma.Success && !double.TryParse(ma.Groups[1].Value, NumberStyles.Float, Inv, out ahora)) ahora = double.NaN;
            var mh = ReHoyClasica.Match(texto); if (mh.Success && !double.TryParse(mh.Groups[1].Value, NumberStyles.Float, Inv, out hoy)) hoy = double.NaN;
            var mr = ReRuedaClasica.Match(texto);
            if (mr.Success)
                return ("de la rueda del " + mr.Groups[3].Value + "-" + mr.Groups[2].Value + (texto.IndexOf("congelada", StringComparison.Ordinal) >= 0 ? ", congelada" : ""), true, ahora, hoy);
            bool vencio = texto.IndexOf("vencio a las 24 h", StringComparison.Ordinal) >= 0, cota = texto.IndexOf("no paso la cota del carry", StringComparison.Ordinal) >= 0;
            string porque = vencio ? "la de la rueda vencio a las 24 h" : cota ? "la de la rueda no paso la cota del carry" : "sin base de la rueda";
            if (texto.IndexOf("= TEORICA", StringComparison.Ordinal) >= 0) return ("TEORICA: el carry (" + porque + " y la cruda no paso la cota)", false, ahora, hoy);
            if (texto.IndexOf("(sin cota)", StringComparison.Ordinal) >= 0) return ("cruda de forwards SIN cota (" + porque + ")", false, ahora, hoy);
            if (texto.IndexOf("= CRUDA", StringComparison.Ordinal) >= 0) return ("CRUDA de forwards de la cadena (" + porque + ")", false, ahora, hoy);
            return ("origen sin dato", false, ahora, hoy);
        }

        /// <summary>4.1.4: nombre corto de una serie para los rotulos de tramos: base ("NDX muro", "NDX cruce", "2.0 NDX", "QQQ dom", "3.0 NDX", "FAM muro",
        /// "TQQQ muro") y fuente ("V"/"OI" solo si el catalogo tiene la misma serie por volumen y por OI; si no, ""). Ej.: MUROS_NDX_vol -> ("NDX muro","V").</summary>
        public static (string Base, string Suf) NombreDeTramo(SerieInfo s, IReadOnlyList<SerieInfo> lista)
        {
            if (s == null) return ("", "");
            string lib = s.Libro == "familia" ? "FAM" : (s.Libro ?? "");
            string corto = string.IsNullOrEmpty(s.Corto) ? (s.Id ?? "") : s.Corto;
            string bas;
            if (EsSerie20(s.Id) || EsClasica(s.Id) || s.Tipo == "TRES" || s.Tipo == "CONF") bas = corto;     // 4.1.5: + la clasica
            else
                switch (s.Tipo)
                {
                    case "MUROS": bas = lib + " muro"; break;
                    case "MAJORS": bas = lib + " major"; break;
                    case "ZEST": bas = lib + " 0G"; break;
                    case "ZTP": bas = lib + (lib == "TQQQ" ? " 0G" : " cruce"); break;
                    case "DOMS": bas = lib + " dom"; break;
                    case "RAZON": bas = lib + " razon"; break;
                    default: bas = corto; break;
                }
            bool doble = false;
            if ((s.Fuente == "vol" || s.Fuente == "oi") && lista != null)
                foreach (var o in lista)
                    if (o != null && o.Id != s.Id && o.Libro == s.Libro && o.Tipo == s.Tipo && o.Fuente != s.Fuente && (o.Fuente == "vol" || o.Fuente == "oi")) { doble = true; break; }
            return (bas.Trim(), doble ? (s.Fuente == "oi" ? "OI" : "V") : "");
        }

        /// <summary>El cambio que puede ir en la etiqueta: series "vol" -> CambioVolM[ventana]; "oi" -> CambioOiDiaM, NUNCA con OI viejo; el resto
        /// (la 3.0 y los niveles sin lado: zeros, cruces, CONF), nada.</summary>
        private static double DeltaEtiqueta(NivelActual a, int iv)
        {
            if (a == null || !TieneLado(a)) return double.NaN;
            if (a.Fuente == "vol") return Idx(a.CambioVolM, iv);
            if (a.Fuente == "oi") return a.OiViejo ? double.NaN : a.CambioOiDiaM;
            return double.NaN;
        }

        private static double Idx(double[] x, int i) => x != null && i >= 0 && i < x.Length ? x[i] : double.NaN;
        private static DateTime IdxT(DateTime[] x, int i) => x != null && i >= 0 && i < x.Length ? x[i] : DateTime.MinValue;
        private static string FechaZ(DateTime t) => t.ToString("MM-dd HH:mm", Inv) + "Z";
        private static string Unir(params string[] s) => string.Join(" ", s.Where(x => !string.IsNullOrEmpty(x)));

        /// <summary>El renglon de cambios de un nivel en el detalle de la pestaña (vacio si no hay nada que decir) y su color. Solo niveles con
        /// lado (TieneLado): a un zero, un cruce o una CONF el cambio no se le aplica y "sin dato" daria a entender que falta un dato.</summary>
        private static (string T, Color C) DetalleCambio(NivelActual a, int iv, int vMin)
        {
            if (a == null || !TieneLado(a)) return ("", ColTexto);
            var ps = new List<string>(4); double dl = double.NaN; int ic = -1;
            if (a.Fuente == "vol" && a.CambioVolM != null)
            {
                dl = Idx(a.CambioVolM, iv);
                double seg = Idx(a.CambioVolSegReal, iv);
                DateTime de = IdxT(a.CambioVolDesdeUtc, iv), ha = IdxT(a.CambioVolHastaUtc, iv);
                ps.Add("vol " + (Fin(seg) && seg > 0 ? Edad(seg) : vMin + " min")
                       + (de != DateTime.MinValue && ha != DateTime.MinValue ? " " + de.ToString("HH:mm", Inv) + "→" + ha.ToString("HH:mm", Inv) + " UTC" : "")
                       + ": " + (Fin(dl) ? Flecha(dl, a.GexM) : "sin dato"));
                ic = iv;
            }
            else if (a.Fuente == "oi" && (Fin(a.CambioOiDiaM) || a.CambioOiDesdeUtc != DateTime.MinValue || a.CambioOiHastaUtc != DateTime.MinValue))
            {
                dl = a.CambioOiDiaM;
                ps.Add("OI" + (a.CambioOiHastaUtc != DateTime.MinValue ? " " + FechaZ(a.CambioOiHastaUtc) : "")
                       + (a.CambioOiDesdeUtc != DateTime.MinValue ? " vs " + FechaZ(a.CambioOiDesdeUtc) : "")
                       + ": " + (Fin(dl) ? Flecha(dl, a.GexM) : "sin dato") + (a.OiViejo && Fin(dl) ? " (OI de 2 sesiones: no va en la etiqueta)" : ""));
                ic = CambiosVentanas.IndiceOiDia;
            }
            if (ic >= 0)
            {
                double cob = Idx(a.CambioCobertura, ic);
                if (Fin(cob) && cob < 0.95) ps.Add("cobertura " + Math.Round(Math.Max(0, cob) * 100).ToString("0", Inv) + " %");
            }
            if (!string.IsNullOrWhiteSpace(a.CambioNota)) ps.Add(a.CambioNota.Trim());
            if (ps.Count == 0) return ("", ColTexto);
            var c = Fin(dl) && Math.Abs(dl) >= CAMBIO_MIN ? ColorSentido(Sentido(dl, a.GexM)) : Color.FromArgb(160, ColTexto);
            return (string.Join(" · ", ps), c);
        }

        private sealed class Item
        {
            public double Precio, E, Strike, Peso = double.NaN;    // Peso: el mayor |GexM| finito de sus partes (cabeza del grupo); NaN = sin monto
            public string Nom, Corto, Banda, Libro, LibroTxt = "", RolTxt = "", FuenteTxt = "", MontoTxt = "", CambioTxt = "";
            public int CambioSentido;         // +1 ▲, −1 ▼, 0 ♦ (solo con CambioTxt)
            public Color Col;
            public NivelActual[] Partes;      // el nivel; dos (o mas) si la misma serie trae varios roles al mismo precio ("C/P": el muro C primero)
        }

        /// <summary>4.1.2: la etiqueta de un grupo (&lt;= 1 pt) es la del item con mayor |GexM|; los sin monto al final; empate: el de precio mas
        /// alto (el grupo viene ordenado por precio de mayor a menor, como hoy).</summary>
        private static Item Cabeza(List<Item> l)
        {
            var c = l[0];
            for (int i = 1; i < l.Count; i++)
            {
                var x = l[i];
                if (!double.IsNaN(x.Peso) && (double.IsNaN(c.Peso) || x.Peso > c.Peso)) c = x;
            }
            return c;
        }

        /// <summary>Una etiqueta armada: Txt = T1 + T2 + T3; T2 (" ▲3,1M") es el tramo del cambio, con su color; vacio = una sola primitiva.</summary>
        private sealed class Rot
        {
            public int Y, Yl, Fuera, N; public string Txt, T1 = "", T2 = "", T3 = "", Acomp = ""; public Color Col, ColCambio; public Item Cab;
        }

        /// <summary>4.1.4 (revision 4.1.3): las series "como la 2.0" de un grupo que no son su cabeza, nombradas en la etiqueta (" · QQQ dom D1",
        /// " · 2.0 NDX D1"): antes la regla "la etiqueta del de mayor |monto|" las tapaba casi siempre (QQQ dom cae en el mismo strike que el muro de
        /// QQQ; 2.0 NDX D1 a 0,2-0,4 pt de otro nivel). Hasta dos, sin repetir, en el orden del grupo (de mayor a menor precio).</summary>
        private static string Acompanan(List<Item> grupo, Item cab)
        {
            if (grupo == null || grupo.Count < 2) return "";
            var vistos = new List<string>(2);
            foreach (var x in grupo)
            {
                if (ReferenceEquals(x, cab) || x.Partes == null || x.Partes.Length == 0 || !(EsSerie20(x.Partes[0].Serie) || EsClasica(x.Partes[0].Serie))) continue;   // 4.1.5: + la clasica
                string t = Unir(x.LibroTxt, x.RolTxt);
                if (t == "" || vistos.Contains(t)) continue;
                vistos.Add(t);
                if (vistos.Count == 2) break;
            }
            return vistos.Count == 0 ? "" : " · " + string.Join(" · ", vistos);
        }

        /// <summary>4.1.4: un tramo de la estela de una serie: velas seguidas (huecos de hasta TRAMO_HUECO) con el mismo precio (a <= TRAMO_TOL del
        /// ultimo). Indices en VistaPantalla.Velas.</summary>
        private sealed class TramoH
        {
            public int J, Ini, Fin, Hueco, XFin, Y;
            public double P;                  // el precio del final del tramo (el ultimo que se vio)
            public bool Vigente, EnCand;
            public TramoH Padre;              // absorbido por otro tramo de la misma raya que sigue despues de este
            /// <summary>Revision 4.1.4: al cerrarse, otro tramo ABIERTO de la misma serie en el mismo precio en ese momento (a &lt;= TRAMO_TOL; el muro
            /// C y el muro P en el mismo strike). Se mira al cerrar y no al final: el precio de una raya larga deriva con la base (31.041,47 a las
            /// 22:02Z y 31.040,42 a las 05:23Z) y su precio final ya no sirve para compararla con uno que termino horas antes.</summary>
            public TramoH Sigue;
            public int Largo => Fin - Ini + 1;
            public TramoH Raiz() { var t = this; for (int g = 0; t.Padre != null && g < 10000; g++) t = t.Padre; return t; }
        }

        /// <summary>4.1.4: el tramo es rotulable por su largo: al menos TRAMO_MIN velas del grafico Y (revision 4.1.4) al menos TRAMO_MIN_M2 velas m2 de la
        /// historia entre su primera y su ultima vela (unos 15 min): en un grafico de segundos un escalon de 2 min de la historia no es una raya.</summary>
        private static bool EsLargo(TramoH t, List<(int X, long M2)> velas)
            => t.Largo >= TRAMO_MIN && t.Ini >= 0 && t.Fin < velas.Count && (velas[t.Fin].M2 - velas[t.Ini].M2) >= (TRAMO_MIN_M2 - 1) * 120000L;

        /// <summary>4.1.4: sigue los tramos abiertos de una serie con los precios de una vela (cada tramo toma el precio libre mas cercano a <= TRAMO_TOL;
        /// los precios que sobran abren tramos nuevos; un tramo con mas de TRAMO_HUECO velas seguidas sin precio se cierra). Los cerrados rotulables
        /// (EsLargo) van a 'largos'. Barato: pocos precios y pocos tramos por serie.</summary>
        private static void SeguirTramos(ref List<TramoH> ab, double[] ps, int j, int i, int x, List<TramoH> largos, List<(int X, long M2)> velas)
        {
            int np = ps == null ? 0 : Math.Min(ps.Length, 64);
            ulong usados = 0;
            if (ab != null && ab.Count == np && np > 0)
            {   // lo comun: los mismos precios que en la vela anterior, en el mismo orden (nivel quieto): todos siguen, sin buscar
                bool iguales = true;
                for (int k = 0; k < np; k++) if (ab[k].P != ps[k] || ab[k].Hueco > 0) { iguales = false; break; }
                if (iguales) { for (int k = 0; k < np; k++) { var t = ab[k]; t.Fin = i; t.XFin = x; } return; }
            }
            if (ab != null)
            {
                for (int k = 0; k < ab.Count; k++)
                {
                    var t = ab[k]; int mejor = -1; double dm = double.MaxValue;
                    for (int a = 0; a < np; a++)
                    {
                        if ((usados & (1UL << a)) != 0) continue;
                        double p = ps[a]; if (!Fin(p)) continue;
                        double dd = Math.Abs(p - t.P);
                        if (dd <= TRAMO_TOL + 1e-9 && dd < dm) { dm = dd; mejor = a; }
                    }
                    if (mejor >= 0) { usados |= 1UL << mejor; t.P = ps[mejor]; t.Fin = i; t.XFin = x; t.Hueco = 0; }
                    else t.Hueco++;
                }
                for (int k = ab.Count - 1; k >= 0; k--)
                    if (ab[k].Hueco > TRAMO_HUECO)
                    {
                        var t = ab[k];
                        if (EsLargo(t, velas))
                        {   // revision 4.1.4: ¿la misma serie sigue en este precio? (otro tramo abierto, vivo, a <= TRAMO_TOL ahora)
                            foreach (var o in ab)
                                if (!ReferenceEquals(o, t) && o.Hueco <= TRAMO_HUECO && Math.Abs(o.P - t.P) <= TRAMO_TOL + 1e-9 && (t.Sigue == null || o.Ini < t.Sigue.Ini)) t.Sigue = o;
                            largos.Add(t);
                        }
                        ab.RemoveAt(k);
                    }
            }
            for (int a = 0; a < np; a++)
            {
                if ((usados & (1UL << a)) != 0 || !Fin(ps[a])) continue;
                (ab ?? (ab = new List<TramoH>(2))).Add(new TramoH { J = j, Ini = i, Fin = i, P = ps[a], XFin = x });
            }
        }

        /// <summary>Ancho del texto de una etiqueta: con tramos, el mayor entre lo medido y columnas x ancho de '0' (Consolas es monoespaciada).</summary>
        private static int AnchoTexto(string t, bool tramos, float tam, double cw, Func<string, float, Size> medir)
        {
            int w = medir(t, tam).Width;
            return tramos ? Math.Max(w, (int)Math.Ceiling(cw * t.Length)) : w;
        }

        /// <summary>Pinta la etiqueta: de una pieza si no hay cambio; si hay, en tres tramos por columna (x = columnas x ancho de '0', como la 3.0)
        /// con el cambio en verde (▲), rojo (▼) o blanco (♦ dio vuelta).</summary>
        private static void Tramos(DibujoPantalla d, Rot r, string pre, float tam, double cw, Color colBase, int x, int y)
        {
            if (r.T2 == "") { d.Texto(pre + r.Txt, tam, colBase, x, y); return; }
            string a = pre + r.T1;
            int x2 = x + (int)Math.Round(cw * a.Length), x3 = x + (int)Math.Round(cw * (a.Length + r.T2.Length));
            d.Texto(a, tam, colBase, x, y);
            d.Texto(r.T2, tam, r.ColCambio, x2, y);
            d.Texto(r.T3, tam, colBase, x3, y);
        }

        private static RotuloPantalla Diag(Rot r, string txt, int x0, int ancho, int y)
        {
            var c = r.Cab; var a = c?.Partes != null && c.Partes.Length > 0 ? c.Partes[0] : null;
            return new RotuloPantalla
            {
                Txt = txt, Serie = a?.Serie ?? "", Rol = c?.RolTxt ?? "", Monto = c?.MontoTxt ?? "", Cambio = r.T2.Trim(), Acompanan = r.Acomp ?? "",
                CambioCrece = r.T2 != "" && c != null && c.CambioSentido > 0, CambioVuelta = r.T2 != "" && c != null && c.CambioSentido == 0,
                Col = r.Col, ColCambio = r.ColCambio, Precio = c?.Precio ?? double.NaN, X0 = x0, Ancho = ancho, Y = y, YNivel = r.Y, Fuera = r.Fuera, EnGrupo = r.N
            };
        }

        /// <summary>Arma el dibujo. medir(texto, tamaño) = RenderContext.MeasureString con RenderFont("Consolas", tamaño).</summary>
        public static void Armar(DibujoPantalla d, FotoFamilia F, CatalogoPantalla cat, AjustesPantalla aj, VistaPantalla v, DateTime ahora, Func<string, float, Size> medir)
        {
            d.Limpiar();
            if (v == null || aj == null || medir == null) return;
            cat = cat ?? new CatalogoPantalla(null);
            var area = v.Area;
            int xr = v.XDerecha > 0 ? Math.Min(area.Right, v.XDerecha) : area.Right;
            float tam = Math.Max(6, Math.Min(14, aj.Letra));
            float tamC = Math.Max(6, tam - 1);
            int hf = medir("X", tam).Height + 1;

            string instr = v.Instrumento ?? "";
            if (instr.IndexOf("NQ", StringComparison.OrdinalIgnoreCase) < 0)
            { Cartel(d, tam, area, "PythiaGex 4.0: los niveles estan en precio de NQ/MNQ; este grafico es " + instr + ": no dibujo nada", ColNaranja); return; }
            if (F == null)
            { Cartel(d, tam, area, "PythiaGex 4.0: esperando el primer calculo de la Familia (todo adentro: libro NQ por Rithmic, NDX/QQQ/TQQQ bajados de CBOE)", ColNaranja); return; }
            // los niveles son del contrato de la Familia (el del grafico donde corre el motor): en el roll, otro vencimiento quedaria corrido por el spread
            string vj = Venc(F.Instrumento), vg = Venc(instr);
            if (vj != "" && vg != "" && vj != vg)
            { Cartel(d, tam, area, "PythiaGex 4.0: los niveles son de " + F.Instrumento + " y este grafico es " + instr + " (otro vencimiento): no dibujo", ColRojo); return; }

            int Y(double p)
            {
                if (double.IsNaN(p) || double.IsInfinity(p) || v.Y == null) return int.MinValue;
                try { return v.Y(p); } catch { return int.MinValue; }
            }
            double pA = v.PrecioAlto, pB = v.PrecioBajo; int yA0 = Y(pA), yB0 = Y(pB);
            double mY = (!double.IsNaN(pA) && !double.IsNaN(pB) && pA != pB && yA0 != int.MinValue && yB0 != int.MinValue) ? (yB0 - yA0) / (pB - pA) : double.NaN;
            int Yr(double p)
            {
                if (double.IsNaN(p) || double.IsInfinity(p)) return int.MinValue;
                if (double.IsNaN(mY)) return Y(p);
                double yy = yA0 + (p - pA) * mY;
                return yy > -1e6 && yy < 1e6 ? (int)Math.Round(yy) : int.MinValue;
            }
            bool EnPantalla(int y) => y != int.MinValue && y >= area.Top && y <= area.Bottom;

            var vis = new List<SerieInfo>();
            foreach (var s in cat.Lista) if (aj.Visible(s.Id)) vis.Add(s);
            var hist = F.HistoriaM2;
            IReadOnlyList<NivelActual> actuales = F.Actuales ?? (IReadOnlyList<NivelActual>)Array.Empty<NivelActual>();
            IReadOnlyList<FuenteEstado> fuentes = F.Fuentes ?? (IReadOnlyList<FuenteEstado>)Array.Empty<FuenteEstado>();
            string aviso = (F.Aviso ?? "").Trim();
            bool sinNiveles = (hist == null || hist.Count == 0) && actuales.Count == 0;
            if (sinNiveles)   // y sigue: la pestaña dice de donde sale cada cosa y por que no hay niveles
                Cartel(d, tam, area, "PythiaGex 4.0: " + (aviso != "" ? aviso : "sin niveles todavia en esta sesion"), ColNaranja);

            // ---- estela: una rayita por vela en el nivel vigente de cada serie (la vela m2 que contiene la hora de la vela del grafico)
            //      4.1.4: en la misma pasada se siguen los TRAMOS de cada serie (mismo precio a <= 0,5 pt, huecos de hasta 3 velas) para rotular
            //      los que ya no son el vigente (Tramos41Rotulos): la columna de la derecha solo nombra los niveles de AHORA.
            List<TramoH> largos = null; int wEst = 0;
            if (aj.Estela && vis.Count > 0 && hist != null && hist.Count > 0 && v.Velas.Count > 0)
            {
                int w = Math.Max(2, (int)Math.Round(v.AnchoVela * 0.9)); wEst = w;
                var colEst = new Color[vis.Count]; var tres = new bool[vis.Count];
                for (int j = 0; j < vis.Count; j++)
                {
                    tres[j] = vis[j].Tipo == "TRES";
                    colEst[j] = Color.FromArgb(tres[j] ? 190 : 240, cat.Colores.TryGetValue(vis[j].Id, out var cc) ? cc : ColTexto);
                }
                bool conTramos = aj.Tramos;
                var abiertos = conTramos ? new List<TramoH>[vis.Count] : null;
                if (conTramos) largos = new List<TramoH>();
                for (int iv0 = 0; iv0 < v.Velas.Count; iv0++)
                {
                    var (x, m2) = v.Velas[iv0];
                    if (!hist.TryGetValue(m2, out var porSerie) || porSerie == null)
                    {   // vela sin historia: cuenta como hueco para los tramos abiertos
                        if (conTramos) for (int j = 0; j < vis.Count; j++) if (abiertos[j] != null) SeguirTramos(ref abiertos[j], null, j, iv0, x, largos, v.Velas);
                        continue;
                    }
                    for (int j = 0; j < vis.Count; j++)
                    {
                        if (!porSerie.TryGetValue(vis[j].Id, out var ps)) ps = null;
                        if (ps != null)
                            foreach (var p in ps)
                            {
                                int y = Yr(p); if (!EnPantalla(y)) continue;
                                d.Relleno(colEst[j], new Rectangle(x - w / 2, y - 1, w, tres[j] ? 1 : 2));
                                d.Rayitas++;
                            }
                        if (conTramos && (ps != null && ps.Length > 0 || abiertos[j] != null)) SeguirTramos(ref abiertos[j], ps, j, iv0, x, largos, v.Velas);
                    }
                }
                if (conTramos)
                    for (int j = 0; j < vis.Count; j++)
                        if (abiertos[j] != null) foreach (var t in abiertos[j]) if (EsLargo(t, v.Velas)) largos.Add(t);
            }

            // 4.1.4: lo que ya ocupa lugar en pantalla (marcas del eje, columna de etiquetas, edad, pestaña): los rotulos de tramos no lo tapan
            var ocupado = new List<Rectangle>(64);
            int colIzq = int.MaxValue;                                       // borde izquierdo de la columna de etiquetas de la derecha
            // ---- doble eje elegible a la izquierda: valor del libro elegido en cada marca, con la conversion que usan sus rayas
            int yCab = area.Top + aj.MargenSup + (aj.Cabecera ? hf + 10 : 0);   // debajo de la leyenda de ATAS y de la pestaña
            string ejeCorto = "", ejeLargo = ""; Color ejeColor = Color.Empty;
            {
                Func<double, double> aLibro = null, aNq = null; string tituloEje = "", fmtEje = "0.00"; DateTime datoEje = DateTime.MinValue; double fuerte = 1;
                var fNdx = Fuente(fuentes, "NDX"); var fQqq = Fuente(fuentes, "QQQ");
                if (aj.Eje == EjeFamilia4.TQQQ && !double.IsNaN(F.TqS) && !double.IsNaN(F.TqC) && F.TqS >= 20 && F.TqS <= 1000)
                {
                    double s0 = F.TqS, c0 = F.TqC; aLibro = q => (q - c0) / s0; aNq = x => s0 * x + c0; datoEje = F.TqDatoUtc;
                    tituloEje = "eje TQQQ x3: NQ = " + s0.ToString("0.00", Es) + " x TQQQ + " + c0.ToString("#,##0.0", Es) + " (no regla de tres)";
                }
                else if (aj.Eje == EjeFamilia4.QQQ && fQqq != null && !double.IsNaN(fQqq.ConvValor) && fQqq.ConvValor > 30 && fQqq.ConvValor < 60)
                {
                    double r0 = fQqq.ConvValor; aLibro = q => q / r0; aNq = x => x * r0; datoEje = fQqq.DatoUtc;
                    tituloEje = "eje QQQ: NQ = QQQ x " + r0.ToString("0.0000", Es) + " (razon sincronizada)";
                }
                else if (aj.Eje == EjeFamilia4.NDX && fNdx != null && !double.IsNaN(fNdx.ConvValor) && Math.Abs(fNdx.ConvValor) < 2000)
                {
                    double b0 = fNdx.ConvValor; aLibro = q => q - b0; aNq = x => x + b0; datoEje = fNdx.DatoUtc; fmtEje = "#,##0"; fuerte = 100;
                    tituloEje = "eje NDX: NQ = NDX + " + b0.ToString("0.00", Es) + " (base sincronizada)";
                }
                else if (aj.Eje != EjeFamilia4.Ninguno) tituloEje = "eje " + aj.Eje + ": sin conversion vigente (no dibujo el eje)";
                if (aLibro != null && !double.IsNaN(pA) && !double.IsNaN(pB))
                {
                    double vHi = aLibro(pA), vLo = aLibro(pB);
                    double porPx = Math.Abs(vHi - vLo) / Math.Max(1, area.Height);
                    double paso = 250;
                    foreach (var cand in new[] { 0.01, 0.02, 0.05, 0.1, 0.25, 0.5, 1, 2, 2.5, 5, 10, 25, 50, 100, 250 }) if (cand / Math.Max(1e-12, porPx) >= 34) { paso = cand; break; }
                    if (paso < 1 && fmtEje == "#,##0") fmtEje = "#,##0.00";
                    if (paso < 0.1) fmtEje = "0.000";
                    int nEje = 0;
                    for (double kk = Math.Ceiling(Math.Min(vLo, vHi) / paso) * paso; kk <= Math.Max(vLo, vHi) + 1e-9 && nEje++ < 300; kk += paso)
                    {
                        double K = Math.Round(kk / paso) * paso;
                        int y = Y(aNq(K)); if (!EnPantalla(y)) continue;     // GetYByPrice exacto por marca
                        bool f2 = Math.Abs(K / fuerte - Math.Round(K / fuerte)) < 1e-6;    // dolares enteros (strikes) / NDX cada 100
                        d.Linea(Color.FromArgb(f2 ? 170 : 90, ColTqqq), 1f, area.Left, y, area.Left + (f2 ? 8 : 5), y);
                        string tk = K.ToString(fmtEje, Es);
                        d.Texto(tk, tam, Color.FromArgb(f2 ? 235 : 150, ColTqqq), area.Left + 10, y - hf / 2);
                        d.Marcas.Add((K, y));
                        if (largos != null) ocupado.Add(new Rectangle(area.Left, y - hf / 2, 12 + medir(tk, tam).Width, hf));
                    }
                }
                if (tituloEje != "")
                {
                    double ee = datoEje == DateTime.MinValue ? double.NaN : (ahora - datoEje).TotalSeconds;
                    bool vj2 = !double.IsNaN(ee) && ee > VIEJO_S;
                    ejeLargo = (vj2 ? "DATO DE HACE " + Edad(ee) + " · " : "") + tituloEje + (!vj2 && !double.IsNaN(ee) ? " · dato " + Edad(ee) : "");
                    ejeCorto = aLibro != null ? "eje " + (aj.Eje == EjeFamilia4.TQQQ ? "TQQQ x3" : aj.Eje.ToString()) : "eje " + aj.Eje + " SIN DATO";
                    ejeColor = vj2 ? ColNaranja : Color.FromArgb(220, ColTqqq);
                    d.EjeTitulo = ejeLargo;
                }
            }

            // ---- niveles actuales agrupados por cercania (a 1 pt): los usan las etiquetas y el detalle de la pestaña
            var act = new List<(NivelActual A, string Rol, List<NivelActual> Partes)>();
            {
                var claves = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var a in actuales)
                {
                    if (a == null || double.IsNaN(a.Precio) || !aj.Visible(a.Serie)) continue;
                    string k = a.Serie + "|" + a.Precio.ToString("0.00", Inv);
                    if (claves.TryGetValue(k, out int i))
                    {   // misma serie al mismo precio (muro C y muro P en el mismo strike): un solo item con los roles juntos (y sus montos)
                        var rolA = act[i].Rol; var r = a.Rol ?? "";
                        if (!rolA.Split('/').Contains(r)) { act[i] = (act[i].A, rolA + "/" + r, act[i].Partes); act[i].Partes.Add(a); }
                        continue;
                    }
                    claves[k] = act.Count; act.Add((a, a.Rol ?? "", new List<NivelActual>(2) { a }));
                }
            }
            int iv = CambiosVentanas.Indice(aj.VentanaMin);
            var items = new List<Item>();
            foreach (var (a, rol, ps) in act)
            {
                cat.PorId.TryGetValue(a.Serie, out var s);
                var col = s != null && cat.Colores.TryGetValue(s.Id, out var cc) ? cc : ColTexto;
                double e = a.DatoUtc == DateTime.MinValue ? double.NaN : (ahora - a.DatoUtc).TotalSeconds;
                // 4.1.5d (revision 4.1.5b): las D1-D3 de la clasica salen del interes abierto si ningun strike del radio tiene volumen (como la clasica,
                // que rotula "NDX D1·OI"): la fuente "Clasica NDX" lo marca (ResultadoClasica.MARCA_DOM_OI) y la etiqueta dice OI en vez de V
                string fuA = a.Serie == "R10_NDX_dom" && (Fuente(fuentes, "Clasica NDX")?.Texto ?? "").IndexOf("dominantes por OI", StringComparison.Ordinal) >= 0 ? "oi" : a.Fuente;
                string fu = fuA == "3.0" ? "" : (fuA == "oi" ? "·OI" : "·vol");
                string corto0 = s?.Corto ?? "";
                bool esConf = s != null && s.Tipo == "CONF";
                string libro = a.Libro == "TQQQ" ? (double.IsNaN(a.Strike) ? "TQQQ" : "TQQQ " + StrikeTqqq(a.Strike))
                             : s != null && s.Tipo == "TRES" ? (corto0 == "" ? "3.0" : corto0)
                             : esConf ? "CONF"
                             : (corto0 == "" ? a.Libro : corto0);
                bool clasica = a.Serie == "R10_NDX_zero";          // 4.1.5: "Clasica NDX 0Γ" ya dice que es el zero: sin rol (4.1.5b: las D1-D3 de la clasica SI llevan rol)
                string nom = esConf ? "CONF" + fu : clasica ? libro + fu : libro + " " + rol + fu;
                string rolCorto = clasica ? "" : rol.Replace("muro C/muro P", "C/P").Replace("muro P/muro C", "C/P").Replace("muro C", "C").Replace("muro P", "P").Replace(" est.", "");
                string corto = esConf ? "CONF" : libro + (clasica ? "" : " " + rolCorto) + (fuA == "3.0" ? "" : (fuA == "oi" ? " OI" : " v"));
                if (a.OiViejo) { nom += " (OI 2s)"; corto += "*"; }
                double banda = a.Banda > 0 ? a.Banda : 0;
                // 4.1.2: las piezas de la etiqueta nueva (LIBRO ROL MONTO [CAMBIO] PRECIO FUENTE)
                var partes = ps.ToArray();
                if (partes.Length == 2 && partes[0].Rol == "muro P" && partes[1].Rol == "muro C") { var t0 = partes[0]; partes[0] = partes[1]; partes[1] = t0; }
                double peso = double.NaN;
                foreach (var pp in partes) { double g = Math.Abs(pp.GexM); if (Fin(g) && (double.IsNaN(peso) || g > peso)) peso = g; }
                string monto;
                if (partes.Length == 1) monto = Monto(a.GexM);
                else
                {   // "C/P": un monto por lado ("+336M/−499M"); "?" si a un lado le falta
                    var ms = partes.Select(pp => Monto(pp.GexM)).ToArray();
                    monto = ms.All(x => x == "") ? "" : string.Join("/", ms.Select(x => x == "" ? "?" : x));
                }
                string cambio = ""; int sentido = 0;
                if (partes.Length == 1)
                {   // con dos roles en el mismo precio no va cambio (cada lado tiene el suyo: esta en el detalle)
                    double dl = DeltaEtiqueta(a, iv);
                    if (Fin(dl) && Math.Abs(dl) >= CAMBIO_MIN) { sentido = Sentido(dl, a.GexM); cambio = Marca(sentido) + MontoAbs(Math.Abs(dl)); }
                }
                items.Add(new Item { Precio = a.Precio, Nom = nom, Corto = corto, E = e, Col = col, Banda = banda > 0 ? "±" + banda.ToString("0.#", Es) : "",
                                     Strike = a.Strike, Libro = a.Libro ?? "", Peso = peso, Partes = partes,
                                     LibroTxt = libro ?? "", RolTxt = esConf ? "" : rolCorto.Replace("cruce arriba", "cruce↑").Replace("cruce abajo", "cruce↓"),
                                     FuenteTxt = (fuA == "3.0" ? "" : (fuA == "oi" ? "OI" : "V")) + (a.OiViejo ? "*" : ""),
                                     MontoTxt = monto, CambioTxt = cambio, CambioSentido = sentido });
            }
            var grupos = new List<List<Item>>();
            foreach (var x in items.OrderByDescending(x => x.Precio))
            {
                if (grupos.Count > 0 && grupos[grupos.Count - 1][0].Precio - x.Precio <= 1.0) grupos[grupos.Count - 1].Add(x);
                else grupos.Add(new List<Item> { x });
            }
            double edadMax = items.Where(x => !double.IsNaN(x.E)).Select(x => x.E).DefaultIfEmpty(double.NaN).Max();

            // ---- etiquetas chicas pegadas al eje: caja oscura con borde del color de la serie. 4.1.2: LIBRO ROL MONTO [CAMBIO] PRECIO FUENTE,
            //      sin "+N" (en un grupo, la del item con mayor |GexM|); el cambio en su color. Con montos y cambios apagados: la de la 4.1.1 sin "+N".
            //      Sin rayas sobre las velas: solo un tic de 6 px en el precio exacto. Arriba de la columna, la edad del dato (protocolo).
            bool nuevo = aj.Montos || aj.Cambios;
            if (aj.Rotulos && v.UltimaVisible && grupos.Count > 0)
            {
                int hC = medir("X", tamC).Height + 3;
                double cw = medir(new string('0', 20), tamC).Width / 20.0;     // Consolas: todas las letras (▲ ▼ − incluidas) avanzan lo mismo
                var et = new List<Rot>();
                foreach (var l in grupos)
                {
                    var a0 = nuevo ? Cabeza(l) : l[0];
                    string precio = a0.Banda != "" ? P(Math.Round(a0.Precio)) : P(a0.Precio);
                    // 4.1.4 (revision 4.1.3): las series "como la 2.0" del grupo que no son la cabeza van nombradas (" · QQQ dom D1").
                    // Revision 4.1.4: van AL FINAL, despues del precio y la fuente de la cabeza ("QQQ P −2,19B 31.076 OI · QQQ dom D1"): pegadas antes
                    // del precio, el precio y la fuente de la cabeza se leian como del acompañante ("· QQQ dom D1 31.076 OI": el OI es del muro, QQQ dom
                    // va por volumen; "· 2.0 NDX D1 31.042,06 V": el 2.0 NDX D1 esta en 31.041,83). El precio de cada acompañante esta en el detalle.
                    string acomp = Acompanan(l, a0);
                    var r = new Rot { Col = a0.Col, Cab = a0, N = l.Count, Acomp = acomp.StartsWith(" · ", StringComparison.Ordinal) ? acomp.Substring(3) : acomp };
                    if (!nuevo) r.T1 = a0.Corto + "  " + precio + acomp;
                    else
                    {
                        r.T1 = Unir(a0.LibroTxt, a0.RolTxt, aj.Montos ? a0.MontoTxt : "");
                        if (aj.Cambios && a0.CambioTxt != "") { r.T2 = " " + a0.CambioTxt; r.ColCambio = ColorSentido(a0.CambioSentido); }
                        // 4.1.6 (estilo 2E elegido por el operador 09-10: "una sola columna a la derecha, sin el precio", con barrita LINEAL de peso):
                        // sin precio ni fuente (el precio se lee en el eje y en la raya; la fuente esta en el detalle); quedan los acompañantes.
                        r.T3 = ESTILO_2E ? acomp : " " + precio + (a0.FuenteTxt != "" ? " " + a0.FuenteTxt : "") + acomp;
                    }
                    r.Txt = r.T1 + r.T2 + r.T3;
                    int y = Y(a0.Precio);
                    r.Y = y; r.Yl = y;
                    r.Fuera = y == int.MinValue ? 0 : (y < area.Top ? 1 : (y > area.Bottom ? -1 : 0));
                    et.Add(r);
                }
                int topCol = yCab;
                // la edad va ANTES de los numeros: una linea arriba de la columna
                if (!double.IsNaN(edadMax))
                {
                    bool viejo = edadMax > VIEJO_S;
                    bool congelada = fuentes.Any(x => x != null && x.Congelada);
                    string tEd = (viejo ? "DATO DE HACE " : "dato de hace ") + Edad(edadMax) + (congelada ? " · cadena congelada" : "");
                    int wEd = medir(tEd, tamC).Width;
                    Sombra(d, tamC, tEd, viejo ? ColNaranja : Color.FromArgb(170, ColTexto), xr - wEd - 6, topCol);
                    d.EdadColumna = tEd;
                    ocupado.Add(new Rectangle(xr - wEd - 7, topCol, wEd + 8, hC)); colIzq = Math.Min(colIzq, xr - wEd - 7);
                    topCol += hC;
                }
                var en = et.Where(r => r.Fuera == 0 && r.Y != int.MinValue).OrderBy(r => r.Y).ToList();
                // 4.1.2 (principal, 09-10): las etiquetas fuera de pantalla (↑ arriba, ↓ abajo) tienen renglones PROPIOS. Antes la primera etiqueta
                // visible caia en el mismo renglon que la primera ↑ y se tapaban (captura del operador 09-10 00:11: "NDX M+" encima de "NDX C").
                int nArr = Math.Min(2, et.Count(r => r.Fuera > 0)), nAba = Math.Min(2, et.Count(r => r.Fuera < 0));
                int topEn = topCol + nArr * hC;
                int ultimo = nArr > 0 ? topEn - hC + hC / 2 : topCol - hC;          // con ↑ arriba, la caja de la primera visible empieza debajo de ellas
                for (int i2 = 0; i2 < en.Count; i2++) { var r = en[i2]; r.Yl = Math.Max(r.Y, ultimo + hC); ultimo = r.Yl; }
                int lim = nAba > 0 ? area.Bottom - 2 - nAba * hC - (hC - hC / 2) : area.Bottom - hC;   // y con ↓ abajo, la caja termina arriba de ellas (hC impar: la caja va de Yl-hC/2 a Yl-hC/2+hC)
                for (int i2 = en.Count - 1; i2 >= 0; i2--) { var r = en[i2]; if (r.Yl > lim) r.Yl = lim; lim = r.Yl - hC; }
                // 4.1.6 estilo 2E: barrita de peso LINEAL (|monto| / el mayor |monto| de las etiquetas visibles = 100 %, minimo 2 px), verde si el
                // monto es positivo y roja si es negativo; sin caja; el texto en el color de la serie con sombra; el cambio pegado al monto.
                double maxPeso = ESTILO_2E && nuevo ? en.Select(r => r.Cab?.Peso ?? double.NaN).Where(Fin).DefaultIfEmpty(double.NaN).Max() : double.NaN;
                foreach (var r in (ESTILO_2E && nuevo) ? en : new List<Rot>())
                {
                    int bw = BARRA_2E, tw = AnchoTexto(r.Txt, r.T2 != "", tamC, cw, medir), w = bw + 6 + tw, x0 = xr - w - 4, yb = r.Yl - hC / 2;
                    int bh = Math.Max(4, hC - 8), by = r.Yl - bh / 2;
                    d.Relleno(Color.FromArgb(150, 40, 46, 58), new Rectangle(x0, by, bw, bh));                                   // el carril (100 %)
                    double pf = PesoFirmado(r.Cab);
                    if (Fin(pf) && Fin(maxPeso) && maxPeso > 0)
                    {
                        int fw = Math.Max(2, Math.Min(bw, (int)Math.Round(bw * Math.Abs(pf) / maxPeso)));                         // LINEAL: proporcion exacta
                        d.Relleno(pf < 0 ? Color.FromArgb(230, 239, 83, 80) : Color.FromArgb(230, 38, 166, 154), new Rectangle(x0 + bw - fw, by, fw, bh));
                    }
                    int xt = x0 + bw + 6;
                    var sombra = new Rot { Txt = r.Txt, T1 = r.T1, T2 = r.T2, T3 = r.T3, ColCambio = Color.FromArgb(200, ColFondo) };
                    Tramos(d, sombra, "", tamC, cw, Color.FromArgb(200, ColFondo), xt + 1, yb + 2);                               // sombra
                    Tramos(d, r, "", tamC, cw, r.Col, xt, yb + 1);
                    if (r.Y != int.MinValue && Math.Abs(r.Yl - r.Y) > 1) d.Linea(Color.FromArgb(160, r.Col), 1f, x0 - 8, r.Y, x0 - 2, r.Yl);   // si la etiqueta se corrio, una linea a su raya
                    else if (r.Y != int.MinValue) d.Linea(Color.FromArgb(220, r.Col), 2f, x0 - 6, r.Y, x0 - 1, r.Y);
                    d.Etiquetas.Add((r.Txt, r.Yl, r.Y));
                    d.Rotulos.Add(Diag(r, r.Txt, x0, w, r.Yl));
                    ocupado.Add(new Rectangle(x0 - 9, yb, w + 9, hC)); colIzq = Math.Min(colIzq, x0 - 9);
                }
                foreach (var r in (ESTILO_2E && nuevo) ? new List<Rot>() : en)
                {
                    int w = AnchoTexto(r.Txt, r.T2 != "", tamC, cw, medir) + 10, x0 = xr - w - 2, yb = r.Yl - hC / 2;
                    d.Relleno(Color.FromArgb(225, ColFondo), new Rectangle(x0, yb, w, hC));
                    d.Borde(Color.FromArgb(200, r.Col), 1f, new Rectangle(x0, yb, w, hC));
                    d.Relleno(r.Col, new Rectangle(x0, yb, 3, hC));
                    Tramos(d, r, "", tamC, cw, Color.FromArgb(240, ColTexto), x0 + 6, yb + 1);
                    if (r.Y != int.MinValue) d.Linea(Color.FromArgb(220, r.Col), 2f, x0 - 6, r.Y, x0 - 1, r.Y);
                    d.Etiquetas.Add((r.Txt, r.Yl, r.Y));
                    d.Rotulos.Add(Diag(r, r.Txt, x0, w, r.Yl));
                    ocupado.Add(new Rectangle(x0 - 7, yb, w + 7, hC)); colIzq = Math.Min(colIzq, x0 - 7);
                }
                // fuera de pantalla: sin caja, con sombra, en el color de la serie (el monto y el cambio tambien van)
                int yA = topCol, yB = area.Bottom - hC - 2;
                foreach (var r in et.Where(r => r.Fuera > 0).OrderByDescending(r => r.Y).Take(2))
                {
                    var t = "↑ " + r.Txt; int w = AnchoTexto(t, r.T2 != "", tamC, cw, medir), x = xr - w - 6;
                    d.Texto(t, tamC, Color.FromArgb(200, ColFondo), x + 1, yA + 1);
                    Tramos(d, r, "↑ ", tamC, cw, r.Col, x, yA);
                    d.Etiquetas.Add((t, yA, r.Y)); d.Rotulos.Add(Diag(r, t, x, w, yA));
                    ocupado.Add(new Rectangle(x - 1, yA, w + 2, hC)); colIzq = Math.Min(colIzq, x - 1); yA += hC;
                }
                foreach (var r in et.Where(r => r.Fuera < 0).OrderBy(r => r.Y).Take(2))
                {
                    var t = "↓ " + r.Txt; int w = AnchoTexto(t, r.T2 != "", tamC, cw, medir), x = xr - w - 6;
                    d.Texto(t, tamC, Color.FromArgb(200, ColFondo), x + 1, yB + 1);
                    Tramos(d, r, "↓ ", tamC, cw, r.Col, x, yB);
                    d.Etiquetas.Add((t, yB, r.Y)); d.Rotulos.Add(Diag(r, t, x, w, yB));
                    ocupado.Add(new Rectangle(x - 1, yB, w + 2, hC)); colIzq = Math.Min(colIzq, x - 1); yB -= hC;
                }
            }

            // ---- la pestaña (su lugar: los rotulos de tramos no la tapan; se dibuja mas abajo, encima de todo)
            int pX0 = area.Left + 3, pY0 = area.Top + aj.MargenSup;
            double eg = F.CalculadoUtc == DateTime.MinValue ? double.NaN : (ahora - F.CalculadoUtc).TotalSeconds;
            bool sinCalculo = double.IsNaN(eg);
            bool motorParado = !sinCalculo && eg > MOTOR_PARADO_S;
            bool viejoTodo = !double.IsNaN(edadMax) && edadMax > VIEJO_S;
            string tPest = ""; Color cPest = Color.Empty; Size mp = Size.Empty;
            if (aj.Cabecera)
            {
                tPest = "PythiaGex 4.0 " + (aj.PanelAbierto ? "▾" : "▸")
                      + (motorParado ? " · EL MOTOR NO CALCULA HACE " + Edad(eg) : sinCalculo ? " · sin calcular todavia" : "")
                      + (!double.IsNaN(edadMax) ? (viejoTodo ? " · DATO DE HACE " : " · dato de hace ") + Edad(edadMax) : "")
                      + (aviso != "" ? " · " + Recortar(aviso, 90) : "")
                      + (ejeCorto != "" ? " · " + ejeCorto : "");
                cPest = motorParado ? ColRojo : (viejoTodo || aviso != "" || sinCalculo) ? ColNaranja : Color.FromArgb(220, ColTexto);
                mp = medir(tPest, tam);
                d.Pestana = new Rectangle(pX0, pY0, mp.Width + 12, mp.Height + 6);
                ocupado.Add(Rectangle.Inflate(d.Pestana, 2, 2));
            }
            if (d.Cartel != "") ocupado.Add(new Rectangle(area.Left + 4, area.Top + 4, medir(d.Cartel, tam).Width + 6, hf + 4));

            // ---- 4.1.4: rotulos de los tramos de historia que ya no son el vigente (Tramos41Rotulos)
            //      revision 4.1.4: tampoco en la franja de arriba que MargenSup4 reserva para la leyenda y el cartel rojo de ATAS (la pestaña y la columna
            //      ya la respetaban; las rayitas se dibujan ahi como siempre, pero un texto encima pisaba el texto de ATAS)
            if (largos != null && largos.Count > 0)
                RotularTramos(d, largos, vis, cat, actuales, v, area, xr, colIzq, ocupado, wEst, tamC, medir, Yr, EnPantalla, area.Top + Math.Max(0, aj.MargenSup));

            // ---- pestaña desplegable (arranca cerrada): "PythiaGex 4.0 ▸ · dato de hace X". Abierta: fuentes, edades y el detalle de cada nivel.
            if (aj.Cabecera)
            {
                int x0 = pX0, y0 = pY0;                       // 4.1.4: el texto, el color y el lugar se calcularon arriba (antes de los rotulos de tramos)
                d.Titulo = tPest; d.ColorTitulo = cPest;
                d.Relleno(Color.FromArgb(225, ColFondo), d.Pestana);
                d.Borde(Color.FromArgb(150, cPest), 1f, d.Pestana);
                d.Texto(tPest, tam, cPest, x0 + 6, y0 + 3);
                if (aj.PanelAbierto)
                {
                    var lineas = new List<(string T, Color C)>();
                    lineas.Add(("calculado adentro: libro NQ por Rithmic + NDX/QQQ/TQQQ bajados de CBOE (15 min de retraso de CBOE) · calculo de hace "
                                + (sinCalculo ? "?" : Edad(eg)) + (string.IsNullOrEmpty(F.Sesion) ? "" : " · sesion " + F.Sesion), Color.FromArgb(200, ColTexto)));
                    if (motorParado) lineas.Add(("el motor de la Familia no publica hace " + Edad(eg) + ": lo que se ve puede estar viejo", ColRojo));
                    if (aviso != "") lineas.Add(("aviso: " + aviso, ColNaranja));
                    if (ejeLargo != "") lineas.Add((ejeLargo, ejeColor));
                    var libros = new HashSet<string>(vis.Select(s => s.Libro ?? ""), StringComparer.Ordinal);
                    bool usaFam = libros.Contains("familia");
                    bool pideTq = aj.Eje == EjeFamilia4.TQQQ || libros.Contains("TQQQ");
                    bool usaCboe = usaFam || pideTq || libros.Contains("NDX") || libros.Contains("QQQ") || aj.Eje == EjeFamilia4.NDX || aj.Eje == EjeFamilia4.QQQ;
                    foreach (var fu in fuentes)
                    {
                        if (fu == null || string.IsNullOrEmpty(fu.Libro)) continue;
                        double e = fu.DatoUtc == DateTime.MinValue ? double.NaN : (ahora - fu.DatoUtc).TotalSeconds;
                        bool vv = !double.IsNaN(e) && e > VIEJO_S;
                        switch (fu.Libro)
                        {
                            case "NQ": case "NDX": case "QQQ": case "TQQQ":
                            {
                                bool usa = fu.Libro == "TQQQ" ? pideTq : (libros.Contains(fu.Libro) || usaFam || aj.Eje.ToString() == fu.Libro);
                                if (!usa) continue;
                                string t = (vv ? "DATO DE HACE " + Edad(e) + " · " : "") + fu.Libro + (fu.Libro == "NQ" ? " Rithmic" : " CBOE")
                                         + (vv || double.IsNaN(e) ? "" : " (" + Edad(e) + ")") + (fu.Congelada ? " · cadena congelada" : "")
                                         + (!fu.OiOk ? " · OI de 2 sesiones" : "") + (string.IsNullOrEmpty(fu.Texto) ? "" : " · " + fu.Texto);
                                lineas.Add((t, vv ? ColNaranja : Color.FromArgb(180, ColTexto)));
                                break;
                            }
                            case "CBOE":
                                if (usaCboe && !string.IsNullOrEmpty(fu.Texto)) lineas.Add(("descarga CBOE: " + fu.Texto, Color.FromArgb(160, ColTexto)));
                                break;
                            case "2.0 QQQ": case "2.0 NDX":
                            {   // 4.1.3: la conversion de cada replica de la 2.0, solo si su serie esta prendida (edad ANTES del numero si es vieja)
                                if (!vis.Any(s => s.Corto == fu.Libro)) continue;
                                string t = (vv ? "DATO DE HACE " + Edad(e) + " · " : "") + fu.Libro + " (replica, CBOE)" + (vv || double.IsNaN(e) ? "" : " (" + Edad(e) + ")")
                                         + (fu.Congelada ? " · cadena congelada" : "") + (string.IsNullOrEmpty(fu.Texto) ? "" : " · " + fu.Texto)
                                         // 4.1.4 (revision 4.1.3): la razon de la 2.0 depende del marco del grafico donde corre la 2.0 (su vela del ultimo trade)
                                         + (fu.Libro == "2.0 QQQ" ? " · replica a la 2.0 en un grafico de 1 min (en otro marco su vela, y su razon, cambian)" : "");
                                lineas.Add((t, vv ? ColNaranja : Color.FromArgb(180, ColTexto)));
                                break;
                            }
                            case "Clasica NDX":
                            {   // 4.1.5: la base de la replica de la clasica (de cuando es y la de ahora), solo si su serie esta prendida
                                if (!vis.Any(s => EsClasica(s.Id))) continue;
                                string t = (vv ? "DATO DE HACE " + Edad(e) + " · " : "") + fu.Libro + " (replica, CBOE)" + (vv || double.IsNaN(e) ? "" : " (" + Edad(e) + ")")
                                         + (fu.Congelada ? " · cadena congelada" : "") + (string.IsNullOrEmpty(fu.Texto) ? "" : " · " + fu.Texto);
                                lineas.Add((t, vv ? ColNaranja : Color.FromArgb(180, ColTexto)));
                                // 4.1.5d (revision 4.1.5b/c, MEDIDO el 09-10): la replica supone a la clasica dibujando con la base de la rueda ANTERIOR (su primaria
                                // en Rithmic); si la clasica la pierde (p. ej. al reiniciar ATAS) vuelve a medir su base en la rueda y la replica deja de coincidir
                                lineas.Add((AVISO_CLASICA, Color.FromArgb(160, ColTexto)));
                                break;
                            }
                            case "cinta":
                                lineas.Add(("cinta MNQ: ultimo tick hace " + Edad(e), !double.IsNaN(e) && e > 120 ? ColNaranja : Color.FromArgb(160, ColTexto)));
                                break;
                            default:
                                lineas.Add(((vv ? "DATO DE HACE " + Edad(e) + " · " : "") + fu.Libro + (string.IsNullOrEmpty(fu.Texto) ? "" : ": " + fu.Texto), Color.FromArgb(160, ColTexto)));
                                break;
                        }
                    }
                    if (pideTq && double.IsNaN(F.TqS))
                        lineas.Add(("TQQQ sin dato en la sesion " + F.Sesion + " (arranca con la rueda de NY)", ColNaranja));
                    // 4.1.2: el estado del calculo de cambios por libro (CambiosFamilia), al final de las fuentes
                    //      (sin repetir "cambios" si la linea ya empieza asi: "cambios: sin minuto con libros todavia")
                    if (F.CambiosEstado != null)
                        foreach (var ce in F.CambiosEstado)
                        {
                            if (string.IsNullOrWhiteSpace(ce)) continue;
                            string ct = ce.Trim();
                            lineas.Add(((ct.StartsWith("cambios", StringComparison.OrdinalIgnoreCase) ? "" : "cambios ") + ct, Color.FromArgb(160, ColTexto)));
                        }
                    lineas.Add(("", ColTexto));
                    if (grupos.Count > 0)
                    {   // 4.1.2: leyenda de los montos y los cambios, en tres renglones cortos (<= 103 letras: el grafico del operador muestra ~115)
                        lineas.Add(("monto = M USD de cobertura por 1 % (M millones, B mil millones) · gamma de ahora: el precio no lo mueve", Color.FromArgb(170, ColTexto)));
                        lineas.Add(("▲ crece · ▼ se achica · " + VUELTA + " dio vuelta (cambió de signo)", Color.FromArgb(170, ColTexto)));
                        lineas.Add(("V: volumen nuevo en " + aj.VentanaMin + " min (no dice si abren o cierran) · OI: posiciones vs la publicación anterior",
                                    Color.FromArgb(170, ColTexto)));
                    }
                    double px = v.PrecioUltimo; bool puesto = double.IsNaN(px);
                    foreach (var l in grupos)
                    {
                        var a0 = l[0];
                        if (!puesto && a0.Precio < px) { lineas.Add(("— precio MNQ " + P(px) + " —", ColPrecio)); puesto = true; }
                        double e = l.Where(x => !double.IsNaN(x.E)).Select(x => x.E).DefaultIfEmpty(double.NaN).Max();
                        bool viejo = !double.IsNaN(e) && e > VIEJO_S;
                        // 4.1.2: el monto de cada nivel va pegado a su nombre (TQQQ: por 1 % de TQQQ, no de NQ)
                        // 4.1.3: las dominantes como la 2.0 dicen con que conversion se pusieron (la de la 2.0 o la sincronizada de la 4.1)
                        string noms = string.Join(" / ", l.Select(x => x.Nom + (x.MontoTxt != "" ? " " + x.MontoTxt + (x.Libro == "TQQQ" ? " por 1 % de TQQQ" : "") : "")
                                                                   + ConvDe(x.Partes != null && x.Partes.Length > 0 ? x.Partes[0].Serie : null, fuentes)
                                                                   + (l.Count > 1 && x.Banda != "" ? " " + x.Banda : "")).Distinct());
                        string precios = string.Join("/", l.Select(x => x.Banda != "" ? P(Math.Round(x.Precio)) : P(x.Precio)).Distinct());
                        string k = "";
                        if (l.Count == 1 && a0.Libro != "TQQQ" && a0.Libro != "familia" && !double.IsNaN(a0.Strike))
                        {
                            string sx0 = a0.Partes != null && a0.Partes.Length > 0 ? a0.Partes[0].Serie : null;
                            k = sx0 == "R10_NDX_zero" ? " (en el indice " + a0.Libro + " " + Strike(a0.Strike) + ")"           // 4.1.5: un zero no es un strike
                              : sx0 == "R10_NDX_dom" ? " (strike " + a0.Libro + " " + Strike(a0.Strike) + ", centroide ±12)"   // 4.1.5d: el STRIKE ganador; la raya va en el centroide (DominantesClasica.CENTROIDE)
                              : " (strike " + a0.Libro + " " + Strike(a0.Strike) + ")";
                        }
                        // revision 4.1.2: el PRECIO va primero (despues de la edad si es vieja): con los montos el renglon de un grupo pasa de
                        // 130 letras y al final el precio quedaba fuera de la vista
                        lineas.Add(((viejo ? "[" + Edad(e) + "] " : "") + precios + (l.Count == 1 && a0.Banda != "" ? " " + a0.Banda : "") + "  " + noms + k
                                    + (!viejo && !double.IsNaN(e) ? " · " + Edad(e) : ""), a0.Col));
                        // 4.1.4 (revision 4.1.3): una replica de la 2.0 puesta con una conversion de RESPALDO (no la vela del ultimo trade / no la base
                        // cruda que paso la cota) lo dice en naranja: la raya puede quedar corrida
                        foreach (var sx in l.Select(x => x.Partes != null && x.Partes.Length > 0 ? x.Partes[0].Serie : null).Where(s => s == "R20_QQQ_vol" || s == "R20_NDX_vol").Distinct())
                        {
                            string lb20 = sx == "R20_QQQ_vol" ? "2.0 QQQ" : "2.0 NDX";
                            var f20 = Fuente(fuentes, lb20);
                            if (f20 == null || !Fin(f20.ConvValor)) continue;
                            var (oTxt, normal) = OrigenConv20(sx, f20.Texto);
                            if (!normal) lineas.Add(("    " + lb20 + ": conversion de RESPALDO de la 2.0 (" + oTxt + "): la raya puede quedar corrida", ColNaranja));
                        }
                        // 4.1.5: la replica de la clasica con una base de RESPALDO (la de la rueda vencio: CRUDA de cada cadena o el carry) lo dice en naranja
                        if (l.Any(x => x.Partes != null && x.Partes.Length > 0 && EsClasica(x.Partes[0].Serie)))
                        {
                            var fc = Fuente(fuentes, "Clasica NDX");
                            if (fc != null && Fin(fc.ConvValor))
                            {
                                var (oTxtC, normalC, _, _) = OrigenClasica(fc.Texto);
                                if (!normalC) lineas.Add(("    Clasica NDX: base de RESPALDO de la clasica (" + oTxtC + "): la raya salta con cada cadena", ColNaranja));
                            }
                        }
                        // 4.1.2: debajo, el cambio de cada nivel con su ventana real o las fechas de las dos publicaciones de OI, la cobertura y la nota
                        foreach (var x in l)
                            for (int j = 0; j < x.Partes.Length; j++)
                            {
                                var (sub, colSub) = DetalleCambio(x.Partes[j], iv, aj.VentanaMin);
                                if (sub == "") continue;
                                string quien = (l.Count > 1 ? x.Nom : "") + (x.Partes.Length > 1 ? (l.Count > 1 ? " " : "") + (x.Partes[j].Rol ?? "") : "");
                                lineas.Add(("    " + (quien != "" ? quien + ": " : "") + sub, colSub));
                            }
                    }
                    if (!puesto) lineas.Add(("— precio MNQ " + P(px) + " —", ColPrecio));
                    lineas.Add(("sin validar: describe, no anticipa · clic en la pestaña para cerrar", Color.FromArgb(140, ColTexto)));
                    // revision 4.1.2: cada renglon se parte en el ancho VISIBLE (xr = min(area.Right, clip.Right)), no en el del area: en el grafico
                    // del operador (area 852 px, clip 781) entran ~115 letras y la leyenda, los grupos y las notas de OI de la FAM se cortaban.
                    // Consolas es monoespaciada: letras que entran = ancho visible / ancho de '0' medido con medir().
                    int xT = x0 + 7;
                    double cwP = medir(new string('0', 20), tam).Width / 20.0;
                    int maxCh = cwP > 0 ? Math.Max(20, (int)Math.Floor((xr - xT - 8) / cwP)) : int.MaxValue;
                    var vista = new List<(string T, Color C)>(lineas.Count + 16);
                    foreach (var (t, c) in lineas) Partir(t, c, maxCh, vista);
                    int wMax = 0; foreach (var (t, _) in vista) if (t != "") wMax = Math.Max(wMax, medir(t, tam).Width);
                    int wP = Math.Max(40, Math.Min(xr - x0 - 2, wMax + 14));
                    int yP0 = y0 + mp.Height + 8, hP = vista.Count * hf + 8;
                    d.Relleno(Color.FromArgb(235, ColFondo), new Rectangle(x0, yP0, wP, hP));
                    d.Borde(Color.FromArgb(120, ColTexto), 1f, new Rectangle(x0, yP0, wP, hP));
                    int yy = yP0 + 4;
                    foreach (var (t, c) in vista) { if (t != "") d.Texto(t, tam, c, xT, yy); d.Panel.Add(t); yy += hf; }
                }
            }
        }

        /// <summary>4.1.4: un rotulo de tramos (una o varias rayas de la misma altura que terminan juntas).</summary>
        private sealed class GrupoTramos
        {
            public TramoH Cab;                                     // la raiz del tramo mas largo (su precio va en el rotulo)
            public readonly List<TramoH> Raices = new List<TramoH>(), Miembros = new List<TramoH>();
            public int Largo;
        }

        /// <summary>4.1.4 (pedido del operador 09-10: "la doble o triple raya no tiene rotulo/etiqueta y es importante"; "la linea roja no se que es"):
        /// rotula los tramos de la estela que ya no son el vigente. Reglas:
        ///  * tramo = velas seguidas de una serie con el mismo precio (a &lt;= 0,5 pt del ultimo), huecos de hasta 3 velas; se rotula si tiene al menos
        ///    15 velas del grafico y 8 velas m2 de la historia (unos 15 min; revision 4.1.4), su raya cae en pantalla y NO es el vigente (llega a la
        ///    ultima vela visible, con el grafico en la ultima vela, y la serie tiene un nivel actual en ese precio: ese ya lo nombra la columna);
        ///  * revision 4.1.4: un tramo que termina mientras la MISMA serie sigue en el MISMO precio (a &lt;= 0,5 pt en ese momento; el muro C y el muro
        ///    P en el mismo strike) no lleva rotulo propio: su nombre y su precio son los de esa raya, que sigue (la nombra la columna si es la vigente, o su rotulo
        ///    al final). Un tramo de OTRA serie (o de la misma a mas de 0,5 pt) lleva su rotulo en SU final aunque otra raya siga a su lado (antes
        ///    se absorbia en cualquier raya a &lt;= 1,5 pt: si esa raya era la vigente quedaba sin nombre en toda la pantalla, y si no, nombrado al
        ///    final de la otra raya, horas y cientos de px despues);
        ///  * rayas a &lt;= 1,5 pt cuyo final queda a &lt;= 40 px van en un solo rotulo ("NDX muro V/OI · 2.0 NDX 31.041", el precio redondeado si junta
        ///    series distintas); cada nombre en el color de su serie;
        ///  * el rotulo va al final de la raya, arriba (si choca, abajo; despues un renglon mas arriba o mas abajo), letra tamC - 1, sombra y caja
        ///    tenue; nunca sobre la columna de etiquetas de la derecha ni sobre la pestaña ni sobre otro rotulo ni en la franja de arriba (yMin =
        ///    area.Top + MargenSup4: la leyenda y el cartel rojo de ATAS); tope TRAMO_TOPE (los mas largos primero; el que no entra se descarta);
        ///  * revision 4.1.4 (costo): juntar y agrupar comparan de a pares; entran los vigentes y los TRAMO_CAND no vigentes mas largos.</summary>
        private static void RotularTramos(DibujoPantalla d, List<TramoH> largos, List<SerieInfo> vis, CatalogoPantalla cat, IReadOnlyList<NivelActual> actuales,
                                          VistaPantalla v, Rectangle area, int xr, int colIzq, List<Rectangle> ocupado, int wEst, float tamC,
                                          Func<string, float, Size> medir, Func<double, int> yr, Func<int, bool> enPantalla, int yMin)
        {
            int nV = v.Velas.Count;
            float tamR = Math.Max(6, tamC - 1);
            int hR = medir("X", tamR).Height;
            double cwR = medir(new string('0', 20), tamR).Width / 20.0;
            // 1. en pantalla y vigente
            var cand = new List<TramoH>(Math.Min(largos.Count, 4096));
            var noVig = new List<TramoH>(Math.Min(largos.Count, 4096));
            foreach (var t in largos)
            {
                t.Y = yr(t.P); t.Padre = null; t.EnCand = false;
                if (!enPantalla(t.Y)) continue;
                string id = vis[t.J].Id;
                bool hayActual = false;
                if (v.UltimaVisible && t.Fin >= nV - 1 - TRAMO_HUECO)
                    foreach (var a in actuales)
                        if (a != null && a.Serie == id && Fin(a.Precio) && Math.Abs(a.Precio - t.P) <= TRAMO_TOL + 1e-9) { hayActual = true; break; }
                t.Vigente = hayActual;
                if (hayActual) { cand.Add(t); d.TramosVigentes++; } else noVig.Add(t);
            }
            d.TramosLargos = cand.Count + noVig.Count;
            // revision 4.1.4 (costo): los pasos 2-4 comparan de a pares: entran los vigentes (pocos: los niveles de ahora) y los TRAMO_CAND no vigentes
            // mas largos (orden total: largo, final, precio, serie, principio)
            if (noVig.Count > TRAMO_CAND)
            {
                noVig.Sort((a, b) =>
                {
                    int c = b.Largo.CompareTo(a.Largo); if (c != 0) return c;
                    c = b.Fin.CompareTo(a.Fin); if (c != 0) return c;
                    c = a.P.CompareTo(b.P); if (c != 0) return c;
                    c = a.J.CompareTo(b.J); return c != 0 ? c : a.Ini.CompareTo(b.Ini);
                });
                d.TramosRecortados = noVig.Count - TRAMO_CAND;
                noVig.RemoveRange(TRAMO_CAND, noVig.Count - TRAMO_CAND);
            }
            cand.AddRange(noVig);
            foreach (var t in cand) t.EnCand = true;
            // 2. absorbidos (revision 4.1.4): solo por la MISMA serie en el MISMO precio (a <= TRAMO_TOL cuando este termino: Sigue, anotado al
            //    cerrarlo) que sigue despues (el muro C y el muro P en el mismo strike: el C se va y el P sigue). Un tramo de otra serie no se absorbe.
            foreach (var a in cand)
            {
                var b = a.Sigue;
                if (b != null && b.EnCand && b.Fin > a.Fin + TRAMO_HUECO) a.Padre = b;     // b.Fin > a.Fin: la cadena siempre termina
            }
            // 3. una raya por raiz (las vigentes no se rotulan: las nombra la columna de la derecha)
            var porRaiz = new Dictionary<TramoH, List<TramoH>>();
            foreach (var t in cand)
            {
                var r = t.Raiz();
                if (!porRaiz.TryGetValue(r, out var l)) porRaiz[r] = l = new List<TramoH>(2);
                l.Add(t);
            }
            var rayas = porRaiz.Where(kv => !kv.Key.Vigente).Select(kv => (R: kv.Key, M: kv.Value, Largo: kv.Value.Max(x => x.Largo)))
                               .OrderByDescending(x => x.Largo).ThenByDescending(x => x.R.Fin).ThenBy(x => x.R.P).ThenBy(x => x.R.J).ThenBy(x => x.R.Ini).ToList();
            // 4. rayas a <= 1,5 pt con el final a <= 40 px: un solo rotulo (contra la raya mas larga del rotulo)
            var grupos = new List<GrupoTramos>();
            foreach (var (r, m, largo) in rayas)
            {
                GrupoTramos g = null;
                foreach (var x in grupos)
                    if (Math.Abs(x.Cab.P - r.P) <= TRAMO_JUNTAR_PTS + 1e-9 && Math.Abs(x.Cab.XFin - r.XFin) <= TRAMO_JUNTAR_PX) { g = x; break; }
                if (g == null) { g = new GrupoTramos { Cab = r, Largo = largo }; grupos.Add(g); }
                g.Raices.Add(r); g.Miembros.AddRange(m);
            }
            d.TramosGrupos = grupos.Count;
            // 5. texto y lugar (los mas largos primero; tope)
            var puestos = new List<Rectangle>(TRAMO_TOPE);
            var sep = Color.FromArgb(200, ColTexto);
            void Descartar(GrupoTramos g, string por)
            {
                d.TramosDescartados++;
                var nr = new RotuloTramo { Txt = por, Precio = g.Cab.P, Largo = g.Largo, Tramos = g.Miembros.Count, VelaIni = g.Cab.Ini, VelaFin = g.Cab.Fin,
                                           XFin = g.Raices.Max(t => t.XFin) + wEst / 2 + 1 };
                nr.Series.AddRange(g.Miembros.Select(t => vis[t.J].Id).Distinct());
                d.TramosNoPuestos.Add(nr);
            }
            foreach (var g in grupos)
            {
                if (puestos.Count >= TRAMO_TOPE) { Descartar(g, "tope"); continue; }
                var ids = g.Miembros.Select(t => vis[t.J].Id).Distinct().OrderBy(id => cat.Orden.TryGetValue(id, out var o) ? o : int.MaxValue).ToList();
                var piezas = new List<(string Base, List<string> Suf, string Id)>();
                var pesos = new List<int>();
                foreach (var id in ids)
                {
                    var (bas, suf) = cat.NombreTramo.TryGetValue(id, out var nt) ? nt : (id, "");
                    int peso = g.Miembros.Where(t => vis[t.J].Id == id).Sum(t => t.Largo);
                    int ip = piezas.FindIndex(p => p.Base == bas);
                    if (ip < 0) { piezas.Add((bas, suf == "" ? new List<string>() : new List<string> { suf }, id)); pesos.Add(peso); }
                    else { if (suf != "" && !piezas[ip].Suf.Contains(suf)) piezas[ip].Suf.Add(suf); pesos[ip] += peso; }
                }
                if (piezas.Count > TRAMO_PIEZAS)
                {   // como mucho TRAMO_PIEZAS nombres: los de las series que mas velas dibujaron en esa raya (en el orden del catalogo; sin "+N")
                    var quedan = Enumerable.Range(0, piezas.Count).OrderByDescending(q => pesos[q]).ThenBy(q => q).Take(TRAMO_PIEZAS).OrderBy(q => q).ToList();
                    piezas = quedan.Select(q => piezas[q]).ToList();
                    var bases = new HashSet<string>(piezas.Select(p => p.Base));
                    ids = ids.Where(id => bases.Contains(cat.NombreTramo.TryGetValue(id, out var nt) ? nt.Base : id)).ToList();
                }
                var segs = new List<(string T, Color C)>(piezas.Count * 2 + 1);
                for (int q = 0; q < piezas.Count; q++)
                {
                    if (q > 0) segs.Add((" · ", sep));
                    segs.Add((piezas[q].Base + (piezas[q].Suf.Count > 0 ? " " + string.Join("/", piezas[q].Suf) : ""),
                              cat.Colores.TryGetValue(piezas[q].Id, out var cc) ? cc : ColTexto));
                }
                var colCab = cat.Colores.TryGetValue(vis[g.Cab.J].Id, out var c0) ? c0 : ColTexto;
                segs.Add((" " + (ids.Count > 1 ? P(Math.Round(g.Cab.P)) : P(g.Cab.P)), colCab));
                string txt = string.Concat(segs.Select(s => s.T));
                int wT = Math.Max(medir(txt, tamR).Width, (int)Math.Ceiling(cwR * txt.Length));
                int xFin = g.Raices.Max(t => t.XFin) + wEst / 2 + 1;
                int yArr = g.Raices.Min(t => t.Y), yAba = g.Raices.Max(t => t.Y);
                int xDer = Math.Min(xFin, xr - 2);
                if (colIzq != int.MaxValue) xDer = Math.Min(xDer, colIzq - 4);     // nunca sobre la columna de etiquetas de la derecha
                int x0 = Math.Max(area.Left + 2, xDer - wT);
                if (x0 + wT > xr - 2 || (colIzq != int.MaxValue && x0 + wT > colIzq - 4)) { Descartar(g, "columna"); continue; }
                Rectangle lugar = Rectangle.Empty;
                foreach (int y0 in new[] { yArr - 2 - hR, yAba + 3, yArr - 3 - 2 * hR, yAba + 4 + hR })
                {
                    var rc = new Rectangle(x0 - 2, y0, wT + 4, hR);
                    if (rc.Top < Math.Max(area.Top, yMin) || rc.Bottom > area.Bottom) continue;      // revision 4.1.4: no en la franja de MargenSup4
                    bool choca = false;
                    foreach (var o in ocupado) if (o.IntersectsWith(rc)) { choca = true; break; }
                    if (!choca) foreach (var o in puestos) if (o.IntersectsWith(Rectangle.Inflate(rc, 1, 1))) { choca = true; break; }
                    if (!choca) { lugar = rc; break; }
                }
                if (lugar.IsEmpty) { Descartar(g, "sin lugar"); continue; }
                puestos.Add(lugar);
                d.Relleno(Color.FromArgb(120, ColFondo), lugar);                       // caja tenue
                d.Texto(txt, tamR, Color.FromArgb(200, ColFondo), x0 + 1, lugar.Y + 1); // sombra
                int col0 = 0;
                foreach (var (st, sc) in segs) { d.Texto(st, tamR, sc, x0 + (int)Math.Round(cwR * col0), lugar.Y); col0 += st.Length; }
                var rt = new RotuloTramo { Txt = txt, Precio = g.Cab.P, Largo = g.Largo, Tramos = g.Miembros.Count, VelaIni = g.Cab.Ini, VelaFin = g.Cab.Fin, XFin = xFin, Caja = lugar, Col = colCab };
                rt.Series.AddRange(ids);
                d.RotulosTramos.Add(rt);
            }
        }

        private static void Sombra(DibujoPantalla d, float tam, string txt, Color col, int x, int y)
        {
            d.Texto(txt, tam, Color.FromArgb(200, ColFondo), x + 1, y + 1);
            d.Texto(txt, tam, col, x, y);
        }

        private static void Cartel(DibujoPantalla d, float tam, Rectangle area, string txt, Color col)
        {
            d.Texto(txt, tam, Color.FromArgb(200, ColFondo), area.Left + 7, area.Top + 7);
            d.Texto(txt, tam, col, area.Left + 6, area.Top + 6);
            d.Cartel = txt; d.ColorCartel = col;
        }
    }
}
