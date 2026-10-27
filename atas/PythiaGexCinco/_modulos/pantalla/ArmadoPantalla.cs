// ArmadoPantalla.cs — PythiaGex 5.0 (Gamma ES), clon de la 4.1 (modulo pantalla B5, 08-10-2026). SIN referencias a ATAS (el arnes lo prueba solo).
// Port del dibujo del visor 4.0.6 (atas/PythiaGexCinco/_visor_4_0_6/GammaCinco.cs, Pintar) sobre la FotoFamilia del contrato (sin json):
// decide QUE se dibuja y DONDE (lista de primitivas); PantallaFamilia.cs lo pinta en el RenderContext de ATAS.
// Lo que el operador aprobo y se conserva tal cual:
//   * rayitas por vela: la vela m2 que contiene la vela del grafico (hora por TICKS crudos de LastTime||Time, Kind Unspecified = UTC);
//   * etiquetas chicas pegadas al eje (hasta la 4.1.1 con "+N" si varias series caian a <= 1 pt; ver 4.1.2 abajo); arriba de la columna la edad del dato
//     ("DATO DE HACE ..." ANTES de los numeros si pasa de 30 min);
//   * pestaña desplegable arriba a la izquierda (margen 56), arranca cerrada; abierta: fuentes, edades, conversiones y el detalle;
//   * doble eje elegible Ninguno/SPX/SPY con la MISMA conversion que sus rayas (GetYByPrice por marca, paso automatico) (5.0: sin TQQQ);
//   * control de vencimiento (el grafico en otro contrato que el de la Familia: no dibuja); sin sombreado (la banda solo en el texto).
// Cambios contra la 4.0.6 (no tocan lo que se dibuja sobre las velas): las fuentes y la edad salen de la foto del motor (no de un json);
// "EL GENERADOR NO ESCRIBE" pasa a "EL MOTOR NO CALCULA" (CalculadoUtc); el aviso del motor va en la pestaña; el precio MES del
// detalle es el cierre de la ultima vela del grafico; el detalle muestra el strike de cada nivel (trazabilidad).
// Describe; no anticipa: nada de direccion.
// 4.1.2 (08-10-2026, pedido del operador: "los millones o billones en tiempo real ... podes sacar esos +1 +2"): la etiqueta pasa a
// LIBRO ROL MONTO [CAMBIO] PRECIO FUENTE ("SPX P −558M 31.500 OI", "ES C +45M ▲3,1M 31.100 V", "SPY 0G 31.054 V", "3.0 SPX D1 +143M 31.036").
// Sin "+N": en un grupo (<= 1 pt) va la etiqueta del item con mayor |GexM|. El monto es M USD de cobertura por 1 % (calls +, puts −; no esta
// medido quien esta largo); el cambio ▲/▼ (crece/se achica en magnitud) sale de los campos que anota CambiosFamilia en el host: en series por
// volumen, el volumen operado en la ventana valuado con la gamma de AHORA (no dice si abren o cierran); en series por OI, la publicacion
// vigente contra la anterior (nunca con OI viejo). Con Monto41Rotulos y Cambio41Rotulos apagados vuelve la etiqueta de la 4.1.1 sin "+N".
// Arreglos de la revision 4.1.2 (09-10-2026):
//   * ▲/▼ por MAGNITUD de verdad: el valor de antes es GexM − Δ (la foto de antes valuada con la gamma de ahora). La regla "signo de Δ ==
//     signo de GexM" del diseño solo equivale a eso si |Δ| <= 2|GexM|: MAJORS_ES_oi M+ 31.000 el 10-09 01:49Z (GexM +8,0, ΔOI +68,5; antes
//     −60,5) salia "▲68M" en verde cuando la magnitud BAJO de 60,5 a 8,0 y el neto dio vuelta. Ahora un neto que cambia de signo lleva ♦
//     ("dio vuelta", en blanco): ni crece ni se achica.
//   * el renglon de cambio del detalle (y el cambio de la etiqueta) solo para niveles con lado, la MISMA regla que CambiosFamilia.LadoDe:
//     CambiosFamilia anota arreglos NaN en TODOS los niveles y los zeros/cruces/CONF mostraban "vol 15 min: sin dato" (no aplica, no falta).
//   * los renglones de la pestaña se parten en el ancho VISIBLE (min(area, clip)), no en el del area, y el precio va primero en cada grupo:
//     en el grafico del operador (area 852 px, clip 781) la leyenda (127 letras) y los grupos largos se cortaban y se perdia el precio.
// 4.1.3 (09-10-2026): las dominantes como la 2.0 (R20_SPY_vol "2.0 SPY", R20_SPX_vol "2.0 SPX", DOMS_SPY_vol "SPY dom", DOMS_SPX_vol "SPX dom"),
//   rol D1/D2: etiqueta "2.0 SPY D1 +417M 31.083,50 V"; el detalle de la pestaña dice la conversion de cada una (razon/base de la 2.0 o la
//   sincronizada de la 4.1) y las fuentes "2.0 SPY" / "2.0 SPX" (su conversion y su edad) si su serie esta prendida. DOMS_* llevan el cambio
//   neto (como M+/M-); R20_* no (CambiosFamilia: NaN con nota).
// 4.1.4 (09-10-2026):
//   * ROTULOS DE LOS TRAMOS DE HISTORIA (pedido del operador: "la 31041 31040 etc dominantes, la doble o triple raya no tiene rotulo/etiqueta y
//     es importante"; "la linea roja no se que es"): la columna de la derecha solo nombra los niveles de AHORA; cada raya de la estela que ya no es
//     la vigente (tramo de >= 15 velas) lleva al final un rotulo chico con el nombre corto y el precio ("SPX muro V 31.040,74"; varias rayas de
//     la misma altura que terminan juntas, uno solo: "SPX muro V/OI · 2.0 SPX 31.041"). Ver RotularTramos. Ajuste Tramos41Rotulos.
//   * revision 4.1.3: en un grupo de etiquetas, las series "como la 2.0" que no son la cabeza van nombradas (" · SPY dom D1"); el detalle dice
//     el ORIGEN real de la conversion de cada replica (y en naranja si es un respaldo); la fuente "2.0 SPY" aclara que replica a la 2.0 en 1 min.
//   * revision 4.1.4: (1) un tramo terminado solo se "absorbe" en la MISMA serie al MISMO precio (muro C/P en el mismo strike); el de otra serie
//     lleva su rotulo en su propio final aunque otra raya siga a su lado (antes, dentro de una raya vigente quedaba sin nombre); (2) costo acotado:
//     juntar/agrupar con los TRAMO_CAND no vigentes mas largos; (3) un tramo rotulable dura ademas 8 velas m2 (~15 min: en graficos de segundos
//     los escalones de 2 min no se rotulan); (4) los rotulos no van en la franja de MargenSup4; (5) los acompañantes de la etiqueta van DESPUES
//     del precio y la fuente de la cabeza ("SPY P −2,19B 31.076 OI · SPY dom D1").
// 4.1.5 (09-10-2026): la replica de la clasica (R10_SPX_zero "Clasica SPX 0Γ", tipo ZEST, rol Z, sin monto): la etiqueta es "Clasica SPX 0Γ 31.068,21 V"
//   (el nombre corto ya dice que es el zero: sin repetir "0G"); su rotulo de tramo, "Clasica SPX 0Γ 31.068,21"; en un grupo va nombrada como las de la
//   2.0 si no es la cabeza; el detalle dice la base usada y de cuando es ("base clasica 243,06 de la rueda del 08-10, congelada") junto a la base de
//   ahora (la sincronizada de la 4.1) y la diferencia, y en naranja si la base es un respaldo (CRUDA o TEORICA); la fuente "Clasica SPX" (su base y su
//   origen) va en la pestaña si la serie esta prendida.
// 4.1.5d (09-10-2026, revision de la 4.1.5b/c): (1) debajo de la fuente "Clasica SPX" va la salvedad AVISO_CLASICA (si la clasica pierde Rithmic re-mide
//   su base en la rueda y la replica deja de coincidir; medido el 09-10: ~13 pts de 16:48 a 17:11 ART); (2) las D1-D3 de la clasica (R10_*) no llevan
//   cambio ▲▼ (TieneLado, igual que las R20_: su base y su S no son las del libro de la 4.1; antes D1/D2 lo tomaban del libro SPX de la 4.1 y D3 no);
//   (3) el detalle de R10_SPX_dom dice "(strike SPX 30.900, centroide ±12)" (antes "(en el indice SPX ...)", que es la leyenda del zero); (4) si la
//   fuente "Clasica SPX" dice "dominantes por OI", la etiqueta de R10_SPX_dom lleva OI en vez de V.
// 5.0 (27-10-2026): sin TQQQ. El eje doble queda Ninguno/SPX/SPY (default SPX: el valor TQQQ ya no existe); fuera el eje "TQQQ x3", la
//   etiqueta con el strike de TQQQ, la linea "TQQQ sin dato", el "por 1 % de TQQQ" del detalle y los roles/tipos de TQQQ (dom, dom (razon),
//   RAZON). El color de las marcas del eje (antes ColTqqq) se llama ColEje y es el MISMO. Las demas series se dibujan igual.
//   Carteles y pestaña: "PythiaGex 5.0" en vez de "la Familia" como nombre del producto; la base de la clasica se rotula "5.0 sincronizada".
// 5.0 (27-10-2026, P5): la pantalla en ES/MES.
//   * Cartel en un grafico que no es ES/MES: "PythiaGex 5.0 es para ES/MES: este gráfico es X, no dibujo", con la MISMA regla de raiz que el nucleo
//     (CadenaApi.RaizGrande: el codigo empieza con MES o ES, sin el '#' de la continua). La 4.1 buscaba "NQ" en cualquier lugar del codigo y el
//     clon lo habia dejado en "ES" en cualquier lugar (lo aceptaba, p. ej., en FESX).
//   * Doble eje: cotas de ES (MEDICIONES_ES.md §3.8): razon ES/SPY entre 9,95 y 10,25 (la 4.1 heredaba 30-60 de NQ/QQQ ~41 y con SPY ~10,08 el
//     eje no se dibujaba NUNCA) y base ES - SPX en (0, 150] (la 4.1: |base| < 2000); fuera de cota la pestaña dice el valor y la cota. Marcas
//     fuertes de SPX cada 25 pts del indice (las de NDX cada 100: ~0,33 % del indice en los dos); en SPY cada dolar entero (un strike).
//   * Distancias en PUNTOS que dependen de la escala del grafico (ES se mueve ~4 veces menos en puntos que NQ: precio NQ/ES 3,86, rangos 6,1-6,6,
//     MEDICIONES_ES.md §1): grupo de etiquetas 1 -> 0,25 pt (GRUPO_PTS), rotulos de tramos juntos 1,5 -> 0,25 pt (TRAMO_JUNTAR_PTS) y su precio al
//     tick (antes al punto). TRAMO_TOL queda 0,5 (no es una distancia de pantalla: absorbe la deriva de la conversion, y en ES un salto de strike
//     es >= 5 pts). Lo que esta en pixeles, velas, segundos o letras no cambia.
//   * Textos: la salvedad de la clasica (AVISO_CLASICA) sin la medicion de NDX presentada como de SPX; "2.0 SPY" dice que la 2.0 no dibuja SPY en
//     ES; "precio"/"cinta" con la raiz del grafico (MES o ES); el pie dice SIN MEDIR EN ES.
//   * Los ejemplos de los comentarios de arriba con precios 30.xxx-31.xxx son de la 4.1 (NQ/NDX/QQQ): el clon solo cambio los nombres de los
//     libros; no son mediciones de ES.
// 5.0.1 (27-10-2026): las replicas "2.0 SPX" y "Clasica SPX" con la conversion de la 5.0 (ajuste Replicas50Conversion, default): su texto empieza con
//   "conversion de la 5.0: base X" (TEXTO_CONV50). OrigenConv20 y OrigenClasica lo reconocen como NORMAL (no naranja: no es un respaldo); el detalle
//   de cada raya dice "conversion de la 5.0: base X sincronizada; eleccion con la base de la 2.0|clasica Y (origen): la raya va +-Z pts de donde la
//   ponia la 2.0|clasica" (DetalleConv50); la fuente dice "formula y seleccion de la 2.0|clasica en ES con la conversion de la 5.0" y la salvedad de
//   la clasica es AVISO_CLASICA_50. Con el ajuste apagado, todo como en la 5.0.0.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PythiaGexCinco
{
    /// <summary>Doble eje de la izquierda. MISMO nombre y namespace que en el visor 4.0.6: el .ws guarda Eje4Libro como texto.
    /// 5.0 (27-10-2026): sin TQQQ (era el ultimo valor: Ninguno, SPX y SPY conservan su numero).</summary>
    public enum EjeFamilia4 { Ninguno, SPX, SPY }

    /// <summary>4.1.2: ventana del cambio por volumen que muestran las etiquetas (Cambio41Ventana; el host calcula las tres: CambiosVentanas.Min).</summary>
    public enum VentanaCambio41
    {
        [Display(Name = "5 min")] M5,
        [Display(Name = "15 min")] M15,
        [Display(Name = "30 min")] M30
    }
}

namespace PythiaGexCinco.Familia
{
    /// <summary>Foto de los ajustes de la pantalla (las propiedades de GammaCinco) para un render.</summary>
    public sealed class AjustesPantalla
    {
        public readonly HashSet<string> Visibles = new HashSet<string>(StringComparer.Ordinal);
        public EjeFamilia4 Eje = EjeFamilia4.SPX;              // 5.0: era TQQQ (mismo default que Eje4Libro)
        public bool Estela = true, Rotulos = true;   // 5.0.2: sin Cabecera ni PanelAbierto (la pestaña se elimino)
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
        public string Instrumento = "";              // InstrumentInfo.Instrument (MESZ6)
        public double PrecioAlto = double.NaN, PrecioBajo = double.NaN;   // PriceChartContainer.High/Low
        public Func<double, int> Y;                  // GetYByPrice exacto; int.MinValue si no se puede
        public readonly List<(int X, long M2)> Velas = new List<(int X, long M2)>(1024);   // velas visibles: x y apertura m2 (ms UTC) de su hora
        public int AnchoVela = 6;
        public bool UltimaVisible = true;            // LastVisibleBarNumber >= CurrentBar - 1 (mirando velas viejas no van los niveles de AHORA)
        /// <summary>4.1.8: si no es null, la columna muestra ESE minuto del pasado en vez del vivo.</summary>
        public RebobinadoFam Rebobinado;
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
        /// <summary>X0/Ancho: la caja (las de fuera de pantalla no tienen caja: X0 = x del texto). EnGrupo: cuantos niveles caen a &lt;= GRUPO_PTS (5.0: 0,25 pt; la 4.1: 1 pt de NQ).</summary>
        public int X0, Ancho, Y, YNivel, Fuera, EnGrupo;
        /// <summary>4.1.4 (revision 4.1.3): las series "como la 2.0" del grupo que no son la cabeza y van nombradas en la etiqueta
        /// (" · SPY dom D1"); vacio si no hay.</summary>
        public string Acompanan = "";
    }

    /// <summary>4.1.4: diagnostico de un rotulo de tramo de historia (arnes y log). Un rotulo nombra una raya de la estela que ya no es la
    /// vigente (la columna de la derecha solo nombra las vigentes): una o varias series juntas, con el precio del final del tramo.</summary>
    public sealed class RotuloTramo
    {
        /// <summary>El texto entero ("SPX muro V/OI · SPX major V 31.040").</summary>
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
        public Rectangle Recuadro2E = Rectangle.Empty;   // 4.1.6: el titulo del recuadro desplegable (clic = abre/cierra)
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
            Prims.Clear(); Pestana = Rectangle.Empty; Recuadro2E = Rectangle.Empty; Cartel = ""; Titulo = ""; EjeTitulo = ""; EdadColumna = "";
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
        /// <summary>4.1.4: nombre corto de cada serie para los rotulos de los tramos de historia: base ("SPX muro") y fuente ("V", "OI" o "").</summary>
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
        public static readonly Color ColEje = Color.FromArgb(255, 213, 79);       // 5.0: antes ColTqqq (el mismo color, para el eje de cualquier libro)
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
        /// <summary>4.1.8: cuantas rayas de seguimiento punteadas se dibujan. Pedido del operador (12-10): "poneles las
        /// lineas seguimiento solo a las tres mas cercanas al precio". Son las tres mas cercanas al ultimo precio
        /// CONTANDO LOS TRES LIBROS juntos, no tres por libro: lo que importa es lo que el precio tiene al lado.</summary>
        public const int PUNTEADAS_N = 3;
        public const int RECUADRO_Y = 24;             // 4.1.6f: el recuadro, a esta distancia del borde de arriba (debajo del boton ▶| de ATAS)
        /// <summary>4.1.6: el recuadro desplegable arranca CERRADO ("que no moleste"); el clic en su titulo lo abre/cierra.</summary>
        public static volatile bool Recuadro2EAbierto = false;
        public const double MOTOR_PARADO_S = 150;     // el motor publica cada ~5 s: 150 s sin foto nueva = parado
        // 5.0 (27-10-2026, P5): tolerancias en PUNTOS de ES. ES se mueve ~4 veces menos en puntos que NQ (precio NQ/ES 3,86; rangos de sesion y de
        // 30 min 6,1-6,6: MEDICIONES_ES.md §1.1-1.2): con el zoom de siempre, 1 pt de NQ son ~2 px y 1 pt de ES ~10 px. Lo que es "la misma altura
        // en pantalla" se divide por 4 y se redondea al tick (0,25); lo que sale de la estructura de strikes (ES y SPX listan cada 5, como NQ) o del
        // ruido de la conversion no se escala por precio.
        /// <summary>5.0 (P5): el tick de ES/MES. El precio de un rotulo que junta series distintas va redondeado a esto (la 4.1: al punto, 4 ticks).</summary>
        public const double TICK_ES = 0.25;
        /// <summary>5.0 (P5): niveles a &lt;= esto van en UNA etiqueta (el grupo de la columna y del detalle). La 4.1: 1 pt de NQ (~2 px). En ES 1/4 = 0,25:
        /// un tick (~2-3 px). Con 1 pt en ES se esconderian niveles que se ven separados (~10 px). Un nivel de ES (multiplo de 5) y uno de SPX llevado a
        /// ES (corrido por la base) casi nunca caen a &lt;= 0,25: cada uno lleva su etiqueta; dos series del MISMO strike con la MISMA conversion si.</summary>
        public const double GRUPO_PTS = 0.25;
        /// <summary>5.0 (P5): cotas del doble eje (MEDICIONES_ES.md §3.8, sanidad). Razon ES/SPY: medido 10,081-10,127 del 15-09 al 26-10 (la 4.1: 30-60
        /// para NQ/QQQ ~41; con SPY el eje no se dibujaba nunca). Base ES - SPX: medido 35-72 en Z6, recien rolado ~65-75 (la 4.1: |base| &lt; 2000).</summary>
        public const double RAZON_SPY_MIN = 9.95, RAZON_SPY_MAX = 10.25, BASE_SPX_MAX = 150.0;
        /// <summary>5.0 (P5): marcas fuertes del eje SPX cada esto (pts del indice): ~0,33 % de SPX, como las de 100 de NDX en la 4.1. SPX lista cada 5.</summary>
        public const double SPX_FUERTE = 25.0;
        // 4.1.4 (pedido del operador 09-10: "la doble o triple raya no tiene rotulo/etiqueta y es importante"): rotulos de los tramos de historia
        // 5.0 (P5): TRAMO_TOL queda 0,5: no es una distancia de pantalla sino la tolerancia para seguir la MISMA raya mientras su conversion deriva (en
        // NDX 31.041,47 -> 31.040,42 en 7 h). En ES la deriva es menor (ruido de la conversion SPX 0,48 y SPY 0,45 pts contra NDX 1,79: MEDICIONES_ES.md
        // §1.4) y un salto de strike es >= 5 pts (SPY >= 10): con 0,5 nunca se juntan dos strikes y una raya que deriva no se parte en pedazos.
        public const double TRAMO_TOL = 0.5;          // mismo tramo: a <= 0,5 pt del ultimo precio del tramo
        public const int TRAMO_HUECO = 3;             // velas seguidas sin ese precio que se toleran adentro de un tramo
        public const int TRAMO_MIN = 15;              // velas del grafico (del principio al final del tramo) para rotularlo
        // revision 4.1.4: Y al menos 8 velas m2 de la historia (unos 15 min): la historia cambia de a 2 min, y en un grafico de segundos cada
        // escalon de 2 min ya pasaba las 15 velas (5 s: 24 velas) y llenaba el tope con escalones. En 1 min, 15 velas seguidas son siempre 8 m2.
        public const int TRAMO_MIN_M2 = 8;
        public const double TRAMO_JUNTAR_PTS = 0.25;  // tramos a <= 0,25 pt (5.0 P5: la 4.1 tenia 1,5 pt de NQ, ~3 px; en ES esos ~3 px son un tick) ...
        public const int TRAMO_JUNTAR_PX = 40;        // ... con el final a <= 40 px: un solo rotulo
        public const int TRAMO_TOPE = 14;             // rotulos de tramos por pantalla (los de los tramos mas largos)
        public const int TRAMO_PIEZAS = 4;            // nombres por rotulo como mucho (los de las series que mas velas dibujaron en esa raya)
        // revision 4.1.4: el juntar y el agrupar comparan tramos de a pares: antes de eso quedan los TRAMO_CAND no vigentes mas largos en pantalla
        // (alcanza de sobra para 14 rotulos; con niveles que saltan cada 16 velas habia 8.200-16.400 tramos y el render tardaba 220-570 ms)
        public const int TRAMO_CAND = 200;
        private static readonly Regex ReVencEs = new Regex(@"ES\s*([FGHJKMNQUVXZ])\s*(\d{1,4})", RegexOptions.Compiled);
        private static readonly Regex ReVencSolo = new Regex(@"^([FGHJKMNQUVXZ])(\d{1,4})$", RegexOptions.Compiled);

        /// <summary>Mes + ultimo digito del año del contrato: "MESZ6" -> "Z6", "ESZ26" -> "Z6", "Z6" -> "Z6" (lo que publica el motor). "" si no se entiende.</summary>
        public static string Venc(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var u = s.Trim().ToUpperInvariant();
            var m = ReVencEs.Match(u);
            if (!m.Success) m = ReVencSolo.Match(u);
            if (!m.Success) return "";
            var y = m.Groups[2].Value;
            return m.Groups[1].Value + y[y.Length - 1];
        }

        /// <summary>5.0 (P5): el grafico es de ES o de MES, con la MISMA regla que el nucleo (CadenaApi.RaizGrande: "MES..." o "ES..." despues de
        /// sacar el '#' de la continua). La 4.1 aceptaba cualquier codigo que tuviera "NQ" en cualquier lugar; el clon, "ES" en cualquier lugar.</summary>
        public static bool EsGraficoEs(string instr)
        {
            var s = (instr ?? "").ToUpperInvariant().TrimStart('#').Trim();
            return s.StartsWith("MES", StringComparison.Ordinal) || s.StartsWith("ES", StringComparison.Ordinal);
        }

        /// <summary>5.0 (P5): la raiz del grafico para los textos ("precio MES", "cinta ES"): MES si el codigo empieza con MES, ES si empieza con ES.</summary>
        public static string RaizGrafico(string instr)
        {
            var s = (instr ?? "").ToUpperInvariant().TrimStart('#').Trim();
            return s.StartsWith("ES", StringComparison.Ordinal) ? "ES" : "MES";
        }

        /// <summary>5.0 (P5): un precio redondeado al tick de ES (0,25).</summary>
        public static double AlTick(double p) => double.IsNaN(p) || double.IsInfinity(p) ? p : Math.Round(p / TICK_ES, MidpointRounding.AwayFromZero) * TICK_ES;

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
        /// magnitud pudo bajar (MAJORS_ES_oi M+ 31.000, 10-09 01:49Z: +8,0 con ΔOI +68,5, antes −60,5: la regla vieja decia ▲).</summary>
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
        /// MUROS/MAJORS/DOMS con rol muro C, muro P, M+ o M- (5.0: sin RAZON, dom y dom (razon), que eran de TQQQ). CambiosFamilia anota arreglos NaN en TODOS los niveles
        /// (tambien ZEST/ZTP/CONF/TRES): en esos el cambio no aplica (no es que falte), asi que no va ni en la etiqueta ni en el detalle.</summary>
        public static bool TieneLado(NivelActual a)
        {
            if (a == null) return false;
            if (a.Tipo != "MUROS" && a.Tipo != "MAJORS" && a.Tipo != "DOMS") return false;
            switch (a.Rol)
            {
                case "muro C": case "muro P": case "M+": case "M-": return true;
                // 4.1.3: la seleccion de la 2.0 sobre los libros de la 4.1 (DOMS_SPY_vol / DOMS_SPX_vol) lleva el cambio neto; las replicas de la
                // 2.0 (R20_*) no (CambiosFamilia las deja NaN con nota: su conversion no es la del libro de la 4.1)
                // 4.1.5d: las de la clasica (R10_*) tampoco (su base y su S no son las del libro SPX de la 4.1; antes D1/D2 tenian flecha y D3 no)
                case "D1": case "D2": return a.Tipo == "DOMS" && !EsReplica20(a.Serie) && !EsClasica(a.Serie);
                default: return false;
            }
        }

        /// <summary>4.1.3: las replicas de la 2.0 (R20_SPY_vol, R20_SPX_vol).</summary>
        public static bool EsReplica20(string serie) => serie != null && serie.StartsWith("R20_", StringComparison.Ordinal);

        /// <summary>4.1.3: de donde sale el precio de las dominantes como la 2.0, para el detalle de la pestaña: la conversion de la 2.0 (R20_*) o la
        /// sincronizada de la 4.1 (DOMS_*). "" para el resto de las series.
        /// 4.1.4 (revision 4.1.3): el texto de las replicas sale del ORIGEN real de su conversion (el texto de su fuente, que arma Replica20): antes
        /// decia siempre "MES del ultimo trade / SPY spot" y "cruda de forwards", tambien cuando la razon era la CRUDA de la guardia del 0,6 %, la
        /// ultima valida, la mediana de la rueda o la CRUDA sin alinear, o la base la TEORICA del carry (protocolo: cada numero con su fuente real).</summary>
        public static string ConvDe(string serie, IReadOnlyList<FuenteEstado> fuentes)
        {
            switch (serie)
            {
                case "R20_SPY_vol":
                {
                    var f = Fuente(fuentes, "2.0 SPY"); if (f == null || !Fin(f.ConvValor)) return " (razon 2.0 sin dato)";
                    return " (razon 2.0 " + f.ConvValor.ToString("0.0000", Es) + " = " + OrigenConv20(serie, f.Texto).Txt + ")";
                }
                case "R20_SPX_vol":
                {
                    var f = Fuente(fuentes, "2.0 SPX"); if (f == null || !Fin(f.ConvValor)) return " (base 2.0 sin dato)";
                    if (EsConv50(f.Texto)) return " (" + DetalleConv50(f, "2.0") + ")";          // 5.0.1: la conversion de la 5.0 (Replicas50Conversion)
                    return " (base 2.0 " + f.ConvValor.ToString("0.00", Es) + ", " + OrigenConv20(serie, f.Texto).Txt + ")";
                }
                case "DOMS_SPY_vol": { var f = Fuente(fuentes, "SPY"); return f == null || !Fin(f.ConvValor) ? "" : " (razon sincronizada " + f.ConvValor.ToString("0.0000", Es) + ")"; }
                case "DOMS_SPX_vol": { var f = Fuente(fuentes, "SPX"); return f == null || !Fin(f.ConvValor) ? "" : " (base sincronizada " + f.ConvValor.ToString("0.00", Es) + ")"; }
                case "R10_SPX_zero":
                case "R10_SPX_dom":                                  // 4.1.5b: la misma base que el 0Γ
                {   // 4.1.5: la base usada, de cuando es, y la de ahora (la sincronizada de la 4.1) con la diferencia: lo que corre a la raya
                    var f = Fuente(fuentes, "Clasica SPX"); if (f == null || !Fin(f.ConvValor)) return " (base clasica sin dato)";
                    if (EsConv50(f.Texto)) return " (" + DetalleConv50(f, "clasica") + ")";      // 5.0.1: la conversion de la 5.0 (Replicas50Conversion)
                    var (oTxt, _, ah, _) = OrigenClasica(f.Texto);
                    string dif = Fin(ah) ? "; base de ahora " + ah.ToString("0.00", Es) + " (sincronizada): " + (f.ConvValor - ah >= 0 ? "+" : MENOS) + Math.Abs(f.ConvValor - ah).ToString("0.00", Es) + " pts" : "";
                    return " (base clasica " + f.ConvValor.ToString("0.00", Es) + " " + oTxt + dif + ")";
                }
                default: return "";
            }
        }

        /// <summary>4.1.4 (revision 4.1.3): de donde salio la conversion de una replica de la 2.0, leido del texto de su fuente ("2.0 SPY": Replica20
        /// TextoRazon = "razon 2.0 X[ = MES ... / spot SPY Y] · ORIGEN[ · ...]"; "2.0 SPX": "base 2.0 X (ORIGEN ...; cota ...)"). Normal = la conversion
        /// de siempre de la 2.0 (la vela de 1 min del ultimo trade con sus ticks; la base cruda de forwards que paso la cota del carry); si no,
        /// es un respaldo y el detalle lo dice en naranja. Texto vacio o desconocido: no normal ("origen sin dato").
        /// 5.0 (P5): en ES lo normal de la base de la 2.0 es "medida", "medida hace N min" o "de la rueda"; la CRUDA pasa a respaldo.</summary>
        public static (string Txt, bool Normal) OrigenConv20(string serie, string texto)
        {
            texto = texto ?? "";
            if (serie == "R20_SPX_vol")
            {
                // 5.0.1: con Replicas50Conversion (default) la base es la de la 5.0 (Replica20, BaseSpx20.TEXTO_50): normal, no un respaldo
                if (EsConv50(texto)) return (ORIGEN_CONV50_TXT, true);
                // 5.0 (P5): la cascada de la 2.0 en ES (Replica20.BaseSpx20.Cascada, paquete P3; "base 2.0 X (ORIGEN...; cota ...)"): medida > medida hace
                // N min > de la rueda > CRUDA > TEORICA > CRUDA sin cota. Los tres primeros son lo que la 2.0 usa en ES (normal); CRUDA, TEORICA y sin
                // cota son su respaldo (en NDX la cadena nunca traia "medida" y la CRUDA era lo normal de la 4.1). Orden: "medida hace" antes que
                // "medida" y "(sin cota)" antes que CRUDA (lo pide P3).
                string o = OrigenBase20(texto);
                if (o.StartsWith("medida hace", StringComparison.Ordinal) || o.StartsWith("medida", StringComparison.Ordinal) || o.StartsWith("de la rueda", StringComparison.Ordinal))
                    return (Recortar(o, 90), true);
                if (texto.IndexOf("(TEORICA", StringComparison.Ordinal) >= 0) return ("TEORICA: el carry (ni medida ni de la rueda, y la cruda de forwards no paso la cota)", false);
                if (texto.IndexOf("(sin cota)", StringComparison.Ordinal) >= 0) return ("cruda de forwards SIN cota (no hay carry para comparar)", false);
                if (texto.IndexOf("(CRUDA", StringComparison.Ordinal) >= 0) return ("cruda de forwards (sin base medida ni de la rueda)", false);
                return ("origen sin dato", false);
            }
            if (serie != "R20_SPY_vol") return ("", true);
            var partes = texto.Split(new[] { " · " }, StringSplitOptions.None);
            string cab = partes[0], origen = partes.Length > 1 ? partes[1] : "";
            // " = MES <vela>" solo lo escribe TextoRazon con la razon valida que salio de la vela alineada (con el origen "vela alineada ...")
            if (cab.IndexOf(" = MES ", StringComparison.Ordinal) >= 0 && (origen == "" || origen.StartsWith("vela alineada", StringComparison.Ordinal)))
                return cab.IndexOf("vela m2 interpolada", StringComparison.Ordinal) >= 0
                    ? ("MES del ultimo trade (vela m2 interpolada: la cinta no tiene esos ticks) / SPY spot", false)
                    : ("MES del ultimo trade / SPY spot", true);
            if (origen.StartsWith("CRUDA: la vela alineada es de otro contrato", StringComparison.Ordinal))
                return ("CRUDA: MES de ahora / SPY spot (la vela del ultimo trade era de otro contrato: guardia del 0,6 %)", false);
            if (origen.StartsWith("ultima valida", StringComparison.Ordinal))
            {
                int pc = origen.IndexOf(';');
                return ("la ultima razon valida (" + (pc > 0 ? origen.Substring(0, pc) : origen).Replace("ultima valida ", "") + "), sin vela alineada", false);
            }
            if (origen.StartsWith("mediana de la rueda", StringComparison.Ordinal)) return ("la mediana de la rueda, sin vela alineada", false);
            if (origen.StartsWith("CRUDA sin alinear", StringComparison.Ordinal)) return ("CRUDA: MES de ahora / SPY spot, sin vela alineada ni razon previa", false);
            return (origen == "" ? "origen sin dato" : "origen: " + Recortar(origen, 70), false);
        }

        // ------------------------------------------------------------------ 5.0.1: replicas con la conversion de la 5.0
        /// <summary>5.0.1: como empieza el texto de la fuente "2.0 SPX" / "Clasica SPX" cuando la replica usa la conversion de la 5.0 (ajuste
        /// Replicas50Conversion): "conversion de la 5.0: base 36.12 (C7, ...) · seleccion y gamma con la base de la 2.0 34.57 (medida: ...)" (Replica20,
        /// BaseSpx20.TEXTO_50) y "conversion de la 5.0: base 36.12 (C7, ...) · zero y seleccion con la base de la clasica 34.57 (medida (...)) · zero en
        /// el indice ..." (ClasicaSpx, ResultadoClasica.TEXTO_50). Mismo literal en los tres archivos.</summary>
        public const string TEXTO_CONV50 = "conversion de la 5.0: base ";
        private const string ORIGEN_CONV50_TXT = "conversion de la 5.0 (la base SPX del minuto, sincronizada C7: la de los muros de SPX y el eje)";
        private static readonly Regex ReUsaria50 = new Regex(@" · (?:seleccion y gamma|zero y seleccion) con la base de la (?:2\.0|clasica) (-?\d+(?:\.\d+)?|ninguna) \(", RegexOptions.Compiled);

        /// <summary>5.0.1: el texto de una replica dice que uso la conversion de la 5.0.</summary>
        public static bool EsConv50(string texto) => (texto ?? "").StartsWith(TEXTO_CONV50, StringComparison.Ordinal);

        /// <summary>5.0.1: la base de la 5.0 que dice el texto ("conversion de la 5.0: base X"), NaN si no se entiende.</summary>
        private static double BaseConv50(string texto)
        {
            if (!EsConv50(texto)) return double.NaN;
            int i = TEXTO_CONV50.Length, j = i;
            while (j < texto.Length && (char.IsDigit(texto[j]) || texto[j] == '.' || texto[j] == '-')) j++;
            return double.TryParse(texto.Substring(i, j - i), NumberStyles.Float, Inv, out var b) ? b : double.NaN;
        }

        /// <summary>5.0.1: la base de la replica original (con la que se eligio y se calculo la gamma; la raya de la original iba ahi) y su origen corto
        /// ("medida", "de la rueda hace 5 min", "CRUDA 8 ticks"), leidos de " · seleccion y gamma|zero y seleccion con la base de la 2.0|clasica X
        /// (ORIGEN...". (NaN, "") si el texto no lo trae; (NaN, "sin base") si la original no tenia.</summary>
        private static (double Base, string Origen) OriginalConv50(string texto)
        {
            texto = texto ?? "";
            var m = ReUsaria50.Match(texto);
            if (!m.Success) return (double.NaN, "");
            int a = m.Index + m.Length, fin = texto.Length;
            foreach (var corte in new[] { ":", " (", ";", ")" }) { int k = texto.IndexOf(corte, a, StringComparison.Ordinal); if (k >= 0 && k < fin) fin = k; }
            string o = texto.Substring(a, fin - a).Trim();
            if (m.Groups[1].Value == "ninguna") return (double.NaN, "sin base");
            return (double.TryParse(m.Groups[1].Value, NumberStyles.Float, Inv, out var b) ? b : double.NaN, o);
        }

        /// <summary>5.0.1: el detalle de la conversion de una replica con la conversion de la 5.0: "conversion de la 5.0: base 36,12 sincronizada (la de
        /// los muros de SPX); eleccion con la base de la 2.0 34,57 (medida): la raya va +1,55 pts de donde la ponia la 2.0" (quien = "2.0" | "clasica").</summary>
        private static string DetalleConv50(FuenteEstado f, string quien)
        {
            string s = "conversion de la 5.0: base " + f.ConvValor.ToString("0.00", Es) + " sincronizada (la de los muros de SPX)";
            var (bo, oo) = OriginalConv50(f.Texto);
            if (Fin(bo))
                s += "; eleccion con la base de la " + quien + " " + bo.ToString("0.00", Es) + (oo != "" ? " (" + Recortar(oo, 40) + ")" : "") + ": la raya va "
                     + (f.ConvValor - bo >= 0 ? "+" : MENOS) + Math.Abs(f.ConvValor - bo).ToString("0.00", Es) + " pts de donde la ponia la " + quien;
            else if (oo != "") s += "; la " + quien + " no tiene base: eleccion con la de la 5.0";
            return s;
        }

        /// <summary>5.0.1: la salvedad de la replica de la clasica con la conversion de la 5.0 (debajo de su fuente en la pestaña).</summary>
        public const string AVISO_CLASICA_50 = "    Clasica SPX: el zero y las D1-D3 de la clasica en ES (libro SPX, radio 100, cadena 960 s atras; eligen con SU base, los mismos strikes) "
                                             + "puestos con la base SPX del minuto de la 5.0 (la de la clasica quedaba 1,17 pts abajo de la real, medido 27-10; apagado en "
                                             + "'Replicas con la conversion de la 5.0' vuelve la suya). Como nivel, SIN MEDIR EN ES";

        /// <summary>5.0 (P5): el origen de la base de la 2.0 en su texto: lo que va entre "base 2.0 X (" y su "; cota" o su ")" (con los parentesis de
        /// adentro balanceados: "medida hace 12 min (la ultima cadena ...)"). "" si no se entiende.</summary>
        public static string OrigenBase20(string texto)
        {
            texto = texto ?? "";
            int i = texto.IndexOf("base 2.0 ", StringComparison.Ordinal); if (i < 0) return "";
            int a = texto.IndexOf('(', i); if (a < 0) return "";
            int prof = 1, j = a + 1;
            for (; j < texto.Length; j++)
            {
                char c = texto[j];
                if (c == '(') prof++;
                else if (c == ')') { if (--prof == 0) break; }
                else if (prof == 1 && string.CompareOrdinal(texto, j, "; cota", 0, 6) == 0) break;
            }
            return texto.Substring(a + 1, Math.Min(j, texto.Length) - a - 1).Trim();
        }

        /// <summary>4.1.4: las series "como la 2.0" (R20_* y DOMS_*).</summary>
        public static bool EsSerie20(string serie) => serie != null && (serie.StartsWith("R20_", StringComparison.Ordinal) || serie.StartsWith("DOMS_", StringComparison.Ordinal));

        /// <summary>4.1.5: la replica de la clasica (R10_SPX_zero "Clasica SPX 0Γ"): su nombre corto ya dice el rol (el zero), no se repite.</summary>
        public static bool EsClasica(string serie) => serie != null && serie.StartsWith("R10_", StringComparison.Ordinal);

        /// <summary>4.1.5d (revision 4.1.5b/c): la salvedad de la replica de la clasica, debajo de su fuente en la pestaña. MEDIDO el 09-10 EN NQ (capa NDX
        /// de la clasica; el clon renombro NDX por SPX en este comentario): tras los reinicios de ATAS la clasica cayo a CBOE y re-midio su base en la rueda
        /// (229,88 -> 230,16) mientras la replica seguia con la del 08-10 (243,06): de 19:48 a 20:11 UTC el 0Γ y las D1-D3 quedaron ~13 pts corridos.
        /// 5.0 (P5, con el pedido de P2 del 27-10): la salvedad es la de ES. En ES la clasica no arma capas: su primaria es SPX (radio min(2 %, 100) =
        /// 100, cadena 960 s atras) con la cascada de base medida &gt; medida hace &lt;= 360 min &gt; de la rueda &gt; cruda &gt; teorica, y la replica sigue la
        /// misma (ClasicaSpx, paquete P2). MEDIDO por P2 (_diseno/P2_SPX.md §3 T7) contra los 131 AUDIT de la clasica en ES del 05-10: de dia (las dos
        /// en "medida") base con mediana 0,00 de diferencia; esa noche la clasica estaba en "de la rueda" 52,42 y la replica en "medida hace 185 min"
        /// 53,69 (+1,27). La medicion de NDX del 09-10 (~13 pts) era de la capa de NQ y no vale aca.</summary>
        public const string AVISO_CLASICA = "    Clasica SPX: replica a la clasica en ES (libro SPX, radio 100, cadena 960 s atras, su cascada de base). Contra la clasica en ES solo se "
                                          + "comparo el 05-10 (131 salidas): de dia la misma base; esa noche +1,27 pts (ella en la de la rueda, la replica en la medida de hace "
                                          + "185 min). Como nivel, SIN MEDIR EN ES";

        private static readonly Regex ReRuedaClasica = new Regex(@"= de la rueda (\d{4})-(\d{2})-(\d{2})", RegexOptions.Compiled);
        private static readonly Regex ReAhoraClasica = new Regex(@"5\.0 sincronizada (-?\d+(?:\.\d+)?)", RegexOptions.Compiled);   // 5.0: ClasicaSpx.Texto dice "5.0 sincronizada" (antes "4.1")
        private static readonly Regex ReHoyClasica = new Regex(@"rueda de hoy (-?\d+(?:\.\d+)?) \((\d+) muestras\)", RegexOptions.Compiled);
        /// <summary>5.0 (P5): los dos primeros escalones de la cascada de la clasica en ES (ClasicaSpx.Texto, paquete P2): "= medida (forwards de la cadena
        /// de las HH:MM:SSZ, X ticks de error, confiable)" y "= medida hace N min (forwards de la cadena de las HH:MM:SSZ, confiable)".</summary>
        private static readonly Regex ReMedidaClasica = new Regex(@"= (medida(?: hace \d+ min)?) \(forwards de la cadena de las (\d{2}:\d{2}:\d{2})Z(?:, (-?\d+(?:\.\d+)?) ticks de error)?", RegexOptions.Compiled);

        /// <summary>4.1.5: de donde salio la base de la replica de la clasica, leido del texto de su fuente ("Clasica SPX", ClasicaSpx.Texto:
        /// "base clasica X = ORIGEN (...) · ahora: 5.0 sincronizada Y; regla de la clasica con la rueda de hoy Z (n muestras) · zero en el indice W").
        /// Normal = la base de la rueda (la de la clasica); si no (CRUDA, TEORICA, sin cota) es un respaldo y el detalle lo dice en naranja.
        /// 5.0 (P5): tambien normal la "medida" y la "medida hace N min" (los dos primeros escalones de la clasica en ES).
        /// Ahora = la base sincronizada de la 4.1 del mismo minuto (NaN si no esta); Hoy = la regla de la clasica con la rueda de hoy (NaN si no hay).</summary>
        public static (string Txt, bool Normal, double Ahora, double Hoy) OrigenClasica(string texto)
        {
            texto = texto ?? "";
            double ahora = double.NaN, hoy = double.NaN;
            // 5.0.1: con Replicas50Conversion (default) la base ES la de la 5.0 (ClasicaSpx.Texto50, ResultadoClasica.TEXTO_50): normal; Ahora = esa
            // misma base. Va ANTES de los patrones de la cascada: el texto nuevo nombra la base con que eligio la clasica ("zero y seleccion con la base
            // de la clasica X (medida (...")
            if (EsConv50(texto)) return (ORIGEN_CONV50_TXT, true, BaseConv50(texto), double.NaN);
            var ma = ReAhoraClasica.Match(texto); if (ma.Success && !double.TryParse(ma.Groups[1].Value, NumberStyles.Float, Inv, out ahora)) ahora = double.NaN;
            var mh = ReHoyClasica.Match(texto); if (mh.Success && !double.TryParse(mh.Groups[1].Value, NumberStyles.Float, Inv, out hoy)) hoy = double.NaN;
            // 5.0 (P5): en ES la cascada de la clasica empieza con la base MEDIDA (forwards confiables de esta cadena) y la medida de hace <= 360 min: son
            // lo que la clasica usa en ES (normal), antes que la de la rueda. Se mira antes que la rueda (su texto trae "; la de la rueda ..." al final).
            var mm = ReMedidaClasica.Match(texto);
            if (mm.Success)
            {
                string err = mm.Groups[3].Success && double.TryParse(mm.Groups[3].Value, NumberStyles.Float, Inv, out var et) ? ", " + et.ToString("0.0", Es) + " ticks de error" : "";
                return (mm.Groups[1].Value + " (forwards de la cadena de las " + mm.Groups[2].Value + "Z" + err + ")", true, ahora, hoy);
            }
            int im = texto.IndexOf("= medida", StringComparison.Ordinal);
            if (im >= 0)
            {   // respaldo del regex: si P2 cambia el detalle del parentesis, sigue siendo la base medida (normal): se muestra lo que dice hasta el " · "
                int fin = texto.IndexOf(" · ", im, StringComparison.Ordinal);
                return (Recortar(texto.Substring(im + 2, (fin < 0 ? texto.Length : fin) - im - 2), 90), true, ahora, hoy);
            }
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

        /// <summary>4.1.4: nombre corto de una serie para los rotulos de tramos: base ("SPX muro", "SPX cruce", "2.0 SPX", "SPY dom", "3.0 SPX", "FAM muro")
        /// y fuente ("V"/"OI" solo si el catalogo tiene la misma serie por volumen y por OI; si no, ""). Ej.: MUROS_SPX_vol -> ("SPX muro","V").</summary>
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
                    case "ZTP": bas = lib + " cruce"; break;            // 5.0: sin el " 0G" de TQQQ
                    case "DOMS": bas = lib + " dom"; break;
                    default: bas = corto; break;                       // 5.0: sin "RAZON" (T_DOMS_raz)
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

        /// <summary>4.1.2: la etiqueta de un grupo (&lt;= GRUPO_PTS; 5.0: 0,25 pt) es la del item con mayor |GexM|; los sin monto al final; empate: el de precio mas
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

        /// <summary>4.1.4 (revision 4.1.3): las series "como la 2.0" de un grupo que no son su cabeza, nombradas en la etiqueta (" · SPY dom D1",
        /// " · 2.0 SPX D1"): antes la regla "la etiqueta del de mayor |monto|" las tapaba casi siempre (SPY dom cae en el mismo strike que el muro de
        /// SPY; 2.0 SPX D1 a 0,2-0,4 pt de otro nivel). Hasta dos, sin repetir, en el orden del grupo (de mayor a menor precio).</summary>
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
            // 5.0 (P5): la misma regla de raiz que el nucleo (EsGraficoEs); en otro grafico (p. ej. MNQZ6) no se dibuja nada: los niveles estan en precio de ES
            if (!EsGraficoEs(instr))
            { Cartel(d, tam, area, "PythiaGex 5.0 es para ES/MES: este gráfico es " + (instr.Trim() == "" ? "?" : instr.Trim()) + ", no dibujo", ColNaranja); return; }
            string raizG = RaizGrafico(instr);                // "MES" o "ES": para "precio MES" y "cinta MES" de la pestaña
            if (F == null)
            { Cartel(d, tam, area, "PythiaGex 5.0: esperando el primer calculo (todo adentro: libro ES por Rithmic, SPX/SPY bajados de CBOE)", ColNaranja); return; }
            // los niveles son del contrato de la Familia (el del grafico donde corre el motor): en el roll, otro vencimiento quedaria corrido por el spread
            string vj = Venc(F.Instrumento), vg = Venc(instr);
            if (vj != "" && vg != "" && vj != vg)
            { Cartel(d, tam, area, "PythiaGex 5.0: los niveles son de " + F.Instrumento + " y este grafico es " + instr + " (otro vencimiento): no dibujo", ColRojo); return; }

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
                Cartel(d, tam, area, "PythiaGex 5.0: " + (aviso != "" ? aviso : "sin niveles todavia en esta sesion"), ColNaranja);

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
            int yCab = area.Top + aj.MargenSup;   // 5.0.2: debajo de la leyenda de ATAS (la pestaña se elimino)
            string ejeCorto = "", ejeLargo = ""; Color ejeColor = Color.Empty;
            {
                Func<double, double> aLibro = null, aEs = null; string tituloEje = "", fmtEje = "0.00"; DateTime datoEje = DateTime.MinValue; double fuerte = 1;
                var fSpx = Fuente(fuentes, "SPX"); var fSpy = Fuente(fuentes, "SPY");
                // 5.0 (27-10-2026): aca iba el eje "TQQQ x3" (ES = s x TQQQ + c). 5.0 (P5): las cotas de SPY y SPX son las de ES (RAZON_SPY_*, BASE_SPX_MAX);
                // con una conversion fuera de cota el eje no se dibuja y el titulo dice el valor, la cota y su edad (protocolo: nada sin fuente ni edad)
                bool fueraCota = false;
                if (aj.Eje == EjeFamilia4.SPY && fSpy != null && Fin(fSpy.ConvValor))
                {
                    double r0 = fSpy.ConvValor; datoEje = fSpy.DatoUtc;
                    if (r0 >= RAZON_SPY_MIN && r0 <= RAZON_SPY_MAX)
                    {
                        aLibro = q => q / r0; aEs = x => x * r0;
                        tituloEje = "eje SPY: ES = SPY x " + r0.ToString("0.0000", Es) + " (razon sincronizada)";
                    }
                    else
                    {
                        fueraCota = true;
                        tituloEje = "eje SPY: razon ES/SPY " + r0.ToString("0.0000", Es) + " fuera de su cota (" + RAZON_SPY_MIN.ToString("0.00", Es) + "-"
                                  + RAZON_SPY_MAX.ToString("0.00", Es) + "): no dibujo el eje";
                    }
                }
                else if (aj.Eje == EjeFamilia4.SPX && fSpx != null && Fin(fSpx.ConvValor))
                {
                    double b0 = fSpx.ConvValor; datoEje = fSpx.DatoUtc;
                    if (b0 > 0 && b0 <= BASE_SPX_MAX)
                    {
                        aLibro = q => q - b0; aEs = x => x + b0; fmtEje = "#,##0"; fuerte = SPX_FUERTE;
                        tituloEje = "eje SPX: ES = SPX + " + b0.ToString("0.00", Es) + " (base sincronizada)";
                    }
                    else
                    {
                        fueraCota = true;
                        tituloEje = "eje SPX: base ES - SPX " + b0.ToString("0.00", Es) + " fuera de su cota (0-" + BASE_SPX_MAX.ToString("0", Es) + " pts): no dibujo el eje";
                    }
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
                        int y = Y(aEs(K)); if (!EnPantalla(y)) continue;     // GetYByPrice exacto por marca
                        bool f2 = Math.Abs(K / fuerte - Math.Round(K / fuerte)) < 1e-6;    // SPY: dolares enteros (strikes) / SPX: cada 25 pts (5.0 P5)
                        d.Linea(Color.FromArgb(f2 ? 170 : 90, ColEje), 1f, area.Left, y, area.Left + (f2 ? 8 : 5), y);
                        string tk = K.ToString(fmtEje, Es);
                        d.Texto(tk, tam, Color.FromArgb(f2 ? 235 : 150, ColEje), area.Left + 10, y - hf / 2);
                        d.Marcas.Add((K, y));
                        if (largos != null) ocupado.Add(new Rectangle(area.Left, y - hf / 2, 12 + medir(tk, tam).Width, hf));
                    }
                }
                if (tituloEje != "")
                {
                    double ee = datoEje == DateTime.MinValue ? double.NaN : (ahora - datoEje).TotalSeconds;
                    bool vj2 = !double.IsNaN(ee) && ee > VIEJO_S;
                    ejeLargo = (vj2 ? "DATO DE HACE " + Edad(ee) + " · " : "") + tituloEje + (!vj2 && !double.IsNaN(ee) ? " · dato " + Edad(ee) : "");
                    ejeCorto = aLibro != null ? "eje " + aj.Eje.ToString() : "eje " + aj.Eje + (fueraCota ? " FUERA DE COTA" : " SIN DATO");
                    ejeColor = vj2 ? ColNaranja : Color.FromArgb(220, ColEje);
                    d.EjeTitulo = ejeLargo;
                }
            }

            // ---- niveles actuales agrupados por cercania (a <= GRUPO_PTS; 5.0 P5: 0,25 pt, la 4.1 1 pt de NQ): los usan las etiquetas y el detalle de la pestaña
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
                // que rotula "SPX D1·OI"): la fuente "Clasica SPX" lo marca (ResultadoClasica.MARCA_DOM_OI) y la etiqueta dice OI en vez de V
                string fuA = a.Serie == "R10_SPX_dom" && (Fuente(fuentes, "Clasica SPX")?.Texto ?? "").IndexOf("dominantes por OI", StringComparison.Ordinal) >= 0 ? "oi" : a.Fuente;
                string fu = fuA == "3.0" ? "" : (fuA == "oi" ? "·OI" : "·vol");
                string corto0 = s?.Corto ?? "";
                bool esConf = s != null && s.Tipo == "CONF";
                // 4.1.8: las series de carga llevan el STRIKE NATIVO del libro al lado del nombre (como hacia TQQQ en la 4.1).
                // En ES el strike y el precio dibujado coinciden (conversion 0) y no se repite; en SPX y SPY no: la raya
                // del strike 30.600 de SPX se dibuja en 30.827,37, y el operador cruza el 30.600 contra CBOE, no el otro.
                bool esCarga = a.Serie != null && a.Serie.StartsWith("CARGA", StringComparison.Ordinal);
                string libro = s != null && s.Tipo == "TRES" ? (corto0 == "" ? "3.0" : corto0)          // 5.0: sin la rama "TQQQ <strike>"
                             : esConf ? "CONF"
                             : esCarga && !double.IsNaN(a.Strike) && Math.Abs(a.Strike - a.Precio) >= 0.005
                                 ? (corto0 == "" ? a.Libro : corto0) + " " + Strike(a.Strike)
                             : (corto0 == "" ? a.Libro : corto0);
                bool clasica = a.Serie == "R10_SPX_zero";          // 4.1.5: "Clasica SPX 0Γ" ya dice que es el zero: sin rol (4.1.5b: las D1-D3 de la clasica SI llevan rol)
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
                if (grupos.Count > 0 && grupos[grupos.Count - 1][0].Precio - x.Precio <= GRUPO_PTS + 1e-9) grupos[grupos.Count - 1].Add(x);
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
                // 4.1.8: las tres de la raya de seguimiento se eligen ANTES de armar los rotulos, por cercania al
                // ultimo precio. Antes era por distancia (+-110 pts) y dibujaba de mas en los dias de muchos strikes.
                var puntear = new HashSet<double>();
                if (!double.IsNaN(v.PrecioUltimo))
                {
                    var cand = new List<double>();
                    foreach (var l0 in grupos)
                    {
                        var c0 = nuevo ? Cabeza(l0) : l0[0];
                        if (c0 != null && c0.Partes != null && c0.Partes.Length > 0 && c0.Partes[0].Serie != null
                            && c0.Partes[0].Serie.StartsWith("CARGA", StringComparison.Ordinal)) cand.Add(c0.Precio);
                    }
                    cand.Sort((x, y) => Math.Abs(x - v.PrecioUltimo).CompareTo(Math.Abs(y - v.PrecioUltimo)));
                    for (int i = 0; i < cand.Count && i < PUNTEADAS_N; i++) puntear.Add(cand[i]);
                }
                var et = new List<Rot>();
                foreach (var l in grupos)
                {
                    var a0 = nuevo ? Cabeza(l) : l[0];
                    string precio = a0.Banda != "" ? P(Math.Round(a0.Precio)) : P(a0.Precio);
                    // 4.1.4 (revision 4.1.3): las series "como la 2.0" del grupo que no son la cabeza van nombradas (" · SPY dom D1").
                    // Revision 4.1.4: van AL FINAL, despues del precio y la fuente de la cabeza ("SPY P −2,19B 31.076 OI · SPY dom D1"): pegadas antes
                    // del precio, el precio y la fuente de la cabeza se leian como del acompañante ("· SPY dom D1 31.076 OI": el OI es del muro, SPY dom
                    // va por volumen; "· 2.0 SPX D1 31.042,06 V": el 2.0 SPX D1 esta en 31.041,83). El precio de cada acompañante esta en el detalle.
                    string acomp = Acompanan(l, a0);
                    var r = new Rot { Col = a0.Col, Cab = a0, N = l.Count, Acomp = acomp.StartsWith(" · ", StringComparison.Ordinal) ? acomp.Substring(3) : acomp };
                    if (!nuevo) r.T1 = a0.Corto + "  " + precio + acomp;
                    else
                    {
                        string roles = a0.RolTxt, acomp2 = acomp;
                        if (ESTILO_2E)
                        {   // 4.1.6: los acompañantes del MISMO libro se suman como rol ("SPY C/M+/D1"); los de otro libro quedan nombrados
                            var extra = new List<string>(); var otros = new List<string>();
                            foreach (var x in l)
                            {
                                if (ReferenceEquals(x, a0) || x.Partes == null || x.Partes.Length == 0 || !(EsSerie20(x.Partes[0].Serie) || EsClasica(x.Partes[0].Serie))) continue;
                                if (x.LibroTxt == a0.LibroTxt || x.LibroTxt.StartsWith(a0.LibroTxt + " ", StringComparison.Ordinal))
                                { if (x.RolTxt != "" && x.RolTxt != a0.RolTxt && !extra.Contains(x.RolTxt)) extra.Add(x.RolTxt); }
                                else { string t = Unir(x.LibroTxt, x.RolTxt); if (t != "" && !otros.Contains(t) && otros.Count < 2) otros.Add(t); }
                            }
                            if (extra.Count > 0) roles = roles + "/" + string.Join("/", extra);
                            acomp2 = otros.Count == 0 ? "" : " · " + string.Join(" · ", otros);
                        }
                        r.T1 = Unir(a0.LibroTxt, roles, aj.Montos ? (ESTILO_2E ? a0.MontoTxt.Replace("+", "") : a0.MontoTxt) : "");
                        if (aj.Cambios && a0.CambioTxt != "") { r.T2 = " " + a0.CambioTxt; r.ColCambio = ColorSentido(a0.CambioSentido); }
                        // 4.1.6 (estilo 2E elegido por el operador 09-10: "una sola columna a la derecha, sin el precio", con barrita LINEAL de peso):
                        // sin precio ni fuente (el precio se lee en el eje y en la raya; la fuente esta en el detalle); quedan los acompañantes.
                        // 4.1.8: la carga (strikes abiertos) SI lleva el precio aunque el estilo 2E lo saque del resto. Es la referencia
                        // dura: con el grafico alejado el eje no alcanza para leer a que strike apunta la raya, y el operador decide sobre ese
                        // numero. Las demas series siguen como las eligio el 09-10 ("una sola columna a la derecha, sin el precio").
                        bool esCarga = a0.Partes != null && a0.Partes.Length > 0 && a0.Partes[0].Serie != null
                                       && a0.Partes[0].Serie.StartsWith("CARGA", StringComparison.Ordinal);
                        r.T3 = ESTILO_2E ? (esCarga ? " " + precio + acomp2 : acomp2)
                                         : " " + precio + (a0.FuenteTxt != "" ? " " + a0.FuenteTxt : "") + acomp;
                    }
                    r.Txt = r.T1 + r.T2 + r.T3;
                    int y = Y(a0.Precio);
                    r.Y = y; r.Yl = y;
                    r.Fuera = y == int.MinValue ? 0 : (y < area.Top ? 1 : (y > area.Bottom ? -1 : 0));
                    // 4.1.8: raya PUNTEADA muy tenue de borde a borde en los strikes cargados que el precio puede
                    // llegar a tocar hoy. Es para no tener que adivinar que rotulo corresponde a que nivel; los
                    // lejanos no se marcan a proposito para no volver al arbol de navidad.
                    if (r.Fuera == 0 && y != int.MinValue && puntear.Contains(a0.Precio))
                    {
                        var cPunt = Color.FromArgb(55, a0.Col);
                        int xFin = Math.Max(area.Left + 2, xr - 8);   // los rotulos se dibujan DESPUES y con fondo: tapan lo que sobre
                        for (int xx = area.Left + 2; xx < xFin; xx += 9) d.Relleno(cPunt, new Rectangle(xx, y, 3, 1));
                    }
                    et.Add(r);
                }
                int topCol = yCab;
                // 4.1.8 REBOBINADO: el renglon que dice QUE MINUTO se esta mostrando. Va PRIMERO y en naranja: sin esto el
                // operador no puede distinguir el pasado del vivo, que es exactamente como se sacan conclusiones erroneas.
                if (v.Rebobinado != null)
                {
                    var rb = v.Rebobinado;
                    string tRb = "◀ " + rb.MinutoUtc.ToString("MM-dd HH:mm", Inv) + "Z"
                               + (double.IsNaN(rb.FutMnq) ? "" : " · fut " + P(rb.FutMnq))
                               + (rb.Libros != null && rb.Libros.Length > 0 ? " · " + string.Join("/", rb.Libros) : "")
                               + (string.IsNullOrEmpty(rb.Nota) ? "" : " · " + rb.Nota);
                    int wRb = medir(tRb, tamC).Width;
                    Sombra(d, tamC, tRb, ColNaranja, xr - wRb - 6, topCol);
                    ocupado.Add(new Rectangle(xr - wRb - 7, topCol, wRb + 8, hC)); colIzq = Math.Min(colIzq, xr - wRb - 7);
                    topCol += hC;
                }
                // la edad va ANTES de los numeros: una linea arriba de la columna
                if (!double.IsNaN(edadMax) && !(ESTILO_2E && nuevo))   // 4.1.6f: en 2E la edad va minima en el renglon del recuadro
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
                // 4.1.6d: el RECUADRO desplegable arriba a la DERECHA (pedido del operador 09-10: "el recuadro choca, ponelo arriba a la derecha donde
                // no moleste y se lea"): su titulo ocupa un renglon PROPIO de la columna (la columna arranca debajo); abierto, los 5 montos mas
                // grandes se dibujan debajo con fondo opaco (lo abre el operador con un clic: tapa a proposito).
                if (ESTILO_2E && nuevo)
                {
                    var libs = new List<(string L, Color C)>();
                    foreach (var r in et) { string lb = r.Cab?.Libro ?? ""; if (lb != "" && !libs.Any(z => z.L == lb)) libs.Add((lb, r.Col)); }
                    string tit = (Recuadro2EAbierto ? "▾ " : "▸ ");
                    int wt = (int)Math.Ceiling(cw * tit.Length) + libs.Sum(z => 14 + (int)Math.Ceiling(cw * z.L.Length)) + 8;
                    var top5 = et.Where(r => Fin(r.Cab?.Peso ?? double.NaN)).OrderByDescending(r => r.Cab.Peso).Take(5).ToList();
                    int wb = Math.Max(wt, top5.Select(r => AnchoTexto(r.T1 + r.T2, r.T2 != "", tamC, cw, medir) + 12).DefaultIfEmpty(0).Max());
                    int rx = xr - wb - 4, ry = area.Top + RECUADRO_Y;   // 4.1.6f: arriba de todo, apenas debajo del boton ▶| de ATAS
                    if (!double.IsNaN(edadMax))
                    {   // 4.1.6f (pedido del operador 09-10: "este mensaje nunca me sirvio, recortalo al minimo"): solo la edad, p. ej. "5,1 h";
                        // en naranja si pasa de 30 min (protocolo: la edad va ANTES de los numeros). 5.0.2: la pestaña de detalle se elimino.
                        bool viejo = edadMax > VIEJO_S;
                        // 5.0.2: la pestaña se elimino; los avisos que solo ella decia van aca, cortos: motor parado (rojo) y mercado cerrado
                        double egR = F.CalculadoUtc == DateTime.MinValue ? double.NaN : (ahora - F.CalculadoUtc).TotalSeconds;
                        bool paradoR = !double.IsNaN(egR) && egR > MOTOR_PARADO_S;
                        bool cerradoR = aviso.StartsWith("mercado cerrado", StringComparison.OrdinalIgnoreCase);
                        string tEd = (paradoR ? "MOTOR PARADO " + Edad(egR) + " · " : "") + Edad(edadMax) + (cerradoR ? " · mercado cerrado" : "");
                        int wEd = medir(tEd, tamC).Width;
                        Sombra(d, tamC, tEd, paradoR ? ColRojo : (viejo || cerradoR) ? ColNaranja : Color.FromArgb(170, ColTexto), rx - wEd - 8, ry + 1);
                        d.EdadColumna = tEd;
                        ocupado.Add(new Rectangle(rx - wEd - 9, ry, wEd + 8, hC)); colIzq = Math.Min(colIzq, rx - wEd - 9);
                    }
                    d.Relleno(Color.FromArgb(225, ColFondo), new Rectangle(rx, ry, wb, hC));
                    d.Borde(Color.FromArgb(120, ColTexto), 1f, new Rectangle(rx, ry, wb, hC));
                    d.Texto(tit, tamC, Color.FromArgb(200, ColTexto), rx + 4, ry + 1);
                    int lx = rx + 4 + (int)Math.Ceiling(cw * tit.Length);
                    foreach (var z in libs) { d.Relleno(z.C, new Rectangle(lx, ry + hC / 2 - 4, 8, 8)); d.Texto(z.L, tamC, z.C, lx + 11, ry + 1); lx += 14 + (int)Math.Ceiling(cw * z.L.Length); }
                    d.Recuadro2E = new Rectangle(rx, ry, wb, hC);
                    ocupado.Add(new Rectangle(rx, ry, wb, hC)); colIzq = Math.Min(colIzq, rx);
                    if (Recuadro2EAbierto && top5.Count > 0)
                    {
                        int yy = ry + hC + 1;
                        d.Relleno(Color.FromArgb(240, ColFondo), new Rectangle(rx, yy, wb, top5.Count * hC + 4));
                        d.Borde(Color.FromArgb(90, ColTexto), 1f, new Rectangle(rx, yy, wb, top5.Count * hC + 4));
                        foreach (var r in top5) { var q = new Rot { Txt = r.T1 + r.T2, T1 = r.T1, T2 = r.T2, T3 = "", ColCambio = r.ColCambio }; Tramos(d, q, "", tamC, cw, r.Col, rx + 5, yy + 2); yy += hC; }
                    }
                    // 4.1.6e (captura del operador 09-10: "choca, se superpone"): abierto, el recuadro RESERVA su alto: las etiquetas ↑ y la columna
                    // empiezan debajo, no encima
                    topCol = Math.Max(topCol, ry + hC + 4 + (Recuadro2EAbierto && top5.Count > 0 ? top5.Count * hC + 6 : 0));
                }
                var en = et.Where(r => r.Fuera == 0 && r.Y != int.MinValue).OrderBy(r => r.Y).ToList();
                // 4.1.2 (principal, 09-10): las etiquetas fuera de pantalla (↑ arriba, ↓ abajo) tienen renglones PROPIOS. Antes la primera etiqueta
                // visible caia en el mismo renglon que la primera ↑ y se tapaban (captura del operador 09-10 00:11: "SPX M+" encima de "SPX C").
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
                    // 4.1.6d (pedido del operador 09-10): sin los palitos que unian la etiqueta con su raya
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

            if (d.Cartel != "") ocupado.Add(new Rectangle(area.Left + 4, area.Top + 4, medir(d.Cartel, tam).Width + 6, hf + 4));

            // ---- 4.1.4: rotulos de los tramos de historia que ya no son el vigente (Tramos41Rotulos)
            //      revision 4.1.4: tampoco en la franja de arriba que MargenSup4 reserva para la leyenda y el cartel rojo de ATAS (la pestaña y la columna
            //      ya la respetaban; las rayitas se dibujan ahi como siempre, pero un texto encima pisaba el texto de ATAS)
            if (largos != null && largos.Count > 0)
                RotularTramos(d, largos, vis, cat, actuales, v, area, xr, colIzq, ocupado, wEst, tamC, medir, Yr, EnPantalla, area.Top + Math.Max(0, aj.MargenSup));

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
        ///  * rayas a &lt;= TRAMO_JUNTAR_PTS (5.0 P5: 0,25 pt; la 4.1: 1,5 pt de NQ) cuyo final queda a &lt;= 40 px van en un solo rotulo ("SPX muro V/OI ·
        ///    2.0 SPX 6.886,25", el precio al tick si junta series distintas; la 4.1 lo redondeaba al punto); cada nombre en el color de su serie;
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
            // 4. rayas a <= TRAMO_JUNTAR_PTS (5.0: 0,25 pt) con el final a <= 40 px: un solo rotulo (contra la raya mas larga del rotulo)
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
                segs.Add((" " + (ids.Count > 1 ? P(AlTick(g.Cab.P)) : P(g.Cab.P)), colCab));   // 5.0 (P5): series distintas -> al tick (la 4.1: al punto)
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
