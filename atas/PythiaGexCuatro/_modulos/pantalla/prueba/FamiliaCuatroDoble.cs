// FamiliaCuatroDoble.cs — SOLO para el arnes de la pantalla (B5). Hace de la parte de B1 (GammaHoyTres.cs) de la clase del indicador:
// la base Indicator, el constructor y los overrides con los ganchos EXACTOS que el integrador tiene que poner. NO va al csproj del indicador.
using System.ComponentModel;
using System.Threading;
using ATAS.Indicators;
using OFT.Rendering.Context;
using PythiaGexCuatro.Familia;

namespace PythiaGexCuatro
{
    [DisplayName("PythiaGex 4.0 - Gamma Familia (doble de prueba B5)")]
    [Category("PythiaGex 4.0")]
    public partial class FamiliaCuatro : Indicator
    {
        internal FotoFamilia FotoDoble;
        internal string LogArranque = "";

        public FamiliaCuatro() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        protected override void OnInitialize() { LogArranque = AjustesPantalla4Texto(); }            // GANCHO: log de arranque
        protected override void OnCalculate(int bar, decimal value) { }

        protected override void OnRender(RenderContext g, DrawingLayouts layout)
        {
            // (lo de la 3.0 iria antes) GANCHO-4.1 pantalla: siempre y al final
            _pantalla.Pintar(g, Volatile.Read(ref FotoDoble));
        }

        public override bool ProcessMouseClick(OFT.Rendering.Control.RenderControlMouseEventArgs e)
        {
            if (_pantalla.Clic(e.X, e.Y)) return true;                                                  // GANCHO-4.1 pantalla: lo primero
            return base.ProcessMouseClick(e);
        }
    }
}
