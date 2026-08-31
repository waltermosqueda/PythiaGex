using System.ComponentModel;

namespace PythiaGex
{
    /// <summary>Opciones de estilo, para que todo lo visual se pueda cambiar
    /// desde la ventana de ajustes sin tocar el codigo.</summary>

    public enum TipoLinea
    {
        [Description("Continua")] Continua,
        [Description("Cortada")] Cortada,
        [Description("Punteada")] Punteada,
        [Description("Raya punto")] RayaPunto,
    }

    public enum LadoEtiqueta
    {
        [Description("Izquierda")] Izquierda,
        [Description("Derecha")] Derecha,
        [Description("Sigue al precio")] SigueAlPrecio,
        [Description("Sin etiqueta")] Ninguna,
    }

    public enum Esquina
    {
        [Description("Arriba a la derecha")] ArribaDerecha,
        [Description("Arriba a la izquierda")] ArribaIzquierda,
        [Description("Abajo a la derecha")] AbajoDerecha,
        [Description("Abajo a la izquierda")] AbajoIzquierda,
    }

    public enum LadoPerfil
    {
        [Description("Apagado")] Apagado,
        [Description("Izquierda")] Izquierda,
        [Description("Derecha")] Derecha,
    }

    public enum AlcancePerfil
    {
        [Description("Sesion actual")] Sesion,
        [Description("Barras visibles")] Visibles,
        [Description("Cantidad fija de barras")] Fijo,
    }
}
