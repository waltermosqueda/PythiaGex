// ContratosPantalla.cs — PythiaGex 4.1, modulo pantalla (B5), 08-10-2026. PROPUESTA de B5 (no estaba en _contratos/Contratos.cs).
// IPantallaFamilia: lo que el indicador llama desde OnRender y ProcessMouseClick (ganchos de B1: GammaHoyTres.cs "GANCHO-4.1 pantalla" y
// GammaHoyTresRecuadro.cs). IGraficoPantalla: lo que la pantalla necesita del grafico de ATAS; lo implementa la propia FamiliaCuatro
// (FamiliaCuatroPantalla.cs, implementacion explicita) porque ChartArea, GetCandle, FirstVisibleBarNumber, InstrumentInfo y RedrawChart
// son PROTEGIDOS en Indicator (medido por reflexion sobre ATAS.Indicators.dll 8.0.15).
using System.Collections.Generic;
using System.Drawing;
using OFT.Rendering.Context;

namespace PythiaGexCuatro.Familia
{
    public interface IPantallaFamilia
    {
        /// <summary>El catalogo de las 29 series (IMotorFamilia.Catalogo). null = el respaldo de SeriesPantalla (mismos valores).</summary>
        IReadOnlyList<SerieInfo> Catalogo { get; set; }
        /// <summary>Dibuja la 4.0 sobre la foto publicada por el motor. Hilo de dibujo: sin E/S, sin llaves del motor. Nunca tira (log propio 1/min).</summary>
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
        void Redibujar();
    }
}
