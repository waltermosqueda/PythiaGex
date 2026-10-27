// ContratosPantalla.cs — PythiaGex 5.0 (Gamma ES), clon de la 4.1 (modulo pantalla B5, 08-10-2026). PROPUESTA de B5 (no estaba en _contratos/Contratos.cs).
// 5.0 (27-10-2026, P5): sin cambios de contrato (solo comentarios: 35 series, "la 5.0").
// IPantallaFamilia: lo que el indicador llama desde OnRender y ProcessMouseClick (ganchos de B1: GammaHoyTres.cs "GANCHO-4.1 pantalla" y
// GammaHoyTresRecuadro.cs). IGraficoPantalla: lo que la pantalla necesita del grafico de ATAS; lo implementa la propia GammaCinco
// (GammaCincoPantalla.cs, implementacion explicita) porque ChartArea, GetCandle, FirstVisibleBarNumber, InstrumentInfo y RedrawChart
// son PROTEGIDOS en Indicator (medido por reflexion sobre ATAS.Indicators.dll 8.0.15).
using System.Collections.Generic;
using System.Drawing;
using OFT.Rendering.Context;

namespace PythiaGexCinco.Familia
{
    public interface IPantallaFamilia
    {
        /// <summary>El catalogo de las 35 series (IMotorFamilia.Catalogo). null = el respaldo de SeriesPantalla (mismos valores).</summary>
        IReadOnlyList<SerieInfo> Catalogo { get; set; }
        /// <summary>Dibuja la 5.0 sobre la foto publicada por el motor. Hilo de dibujo: sin E/S, sin llaves del motor. Nunca tira (log propio 1/min).</summary>
        void Pintar(RenderContext g, FotoFamilia foto);
        /// <summary>true = el clic cayo en la pestaña (la abrio o la cerro y pidio redibujar): el clic no sigue al grafico.</summary>
        bool Clic(int x, int y);
        /// <summary>El ultimo armado (diagnostico: log de arranque, arnes).</summary>
        DibujoPantalla UltimoDibujo { get; }
    }

    /// <summary>El grafico de ATAS visto por la pantalla. Todos los metodos nunca tiran (devuelven int.MinValue / false / NaN).</summary>
    public interface IGraficoPantalla
    {
        /// <summary>Llena las casillas y ajustes de pantalla actuales (las propiedades S_*, Eje4Libro, Estela4, ...).</summary>
        void Ajustes(AjustesPantalla destino);
        /// <summary>Recuadro4Abierto (la pestaña arranca cerrada).</summary>
        bool PanelAbierto { get; set; }
        /// <summary>false si todavia no hay ChartInfo/PriceChartContainer.</summary>
        bool Contenedor(out Rectangle area);
        string Instrumento { get; }
        int PrimeraVisible { get; }
        int UltimaVisible { get; }
        /// <summary>CurrentBar (cantidad de velas: la ultima es CurrentBar - 1).</summary>
        int BarraActual { get; }
        double PrecioAlto { get; }
        double PrecioBajo { get; }
        int YDePrecio(double precio);
        int XDeBarra(int barra);
        /// <summary>ms UTC de la hora de la vela por TICKS crudos de (LastTime si no es default, si no Time) — Kind Unspecified = UTC, nunca
        /// ToUniversalTime — y su cierre.</summary>
        bool Vela(int barra, out long msUtc, out double cierre);
        /// <summary>4.1.8 REBOBINADO: la vela que tiene el mouse encima, o -1 si ninguna. Unico camino posible: el SDK
        /// de ATAS no expone inversa pixel->barra (no existe GetBarByX), solo IMouseLocationInfo.BarBelowMouse.</summary>
        int BarraBajoMouse { get; }
        void Redibujar();
    }
}
