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

    public enum ModoTablero
    {
        [Description("Colapsado: una sola linea")] Colapsado,
        [Description("Chip: lo minimo que sirve para decidir")] Chip,
        [Description("Compacto: lo esencial")] Compacto,
        [Description("Completo: todo")] Completo,
    }

    /// <summary>Como se rotula cada nivel sobre el grafico.</summary>
    public enum FormatoEtiqueta
    {
        [Description("Chip: dos renglones cortos")] Chip,
        [Description("Una linea larga")] Linea,
        [Description("Minima: nombre y precio")] Minima,
        [Description("Solo el precio")] SoloPrecio,
    }

    /// <summary>Cuanto nombre se escribe.</summary>
    public enum LargoNombre
    {
        [Description("Corto: TECHO, PISO, IMAN")] Corto,
        [Description("Completo: Call Wall, Put Wall")] Completo,
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
