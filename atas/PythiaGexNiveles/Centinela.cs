using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PythiaGex
{
    /// <summary>
    /// EL CENTINELA DE ACTIVIDAD.
    ///
    /// POR QUE EXISTE
    /// GAMMAlito explica sus lineas en video y lo que dice, textualmente, es que
    /// las barras marcan "los nodos donde esta mayormente expuesto el market
    /// maker", que ahi "va a cubrir su cartera" y que lo que va a pasar es que
    /// "va a aumentar la velocidad del tape".
    ///
    /// NO dicen que el precio rebote. Dicen que ahi se ACELERA LA ACTIVIDAD.
    ///
    /// Y el laboratorio venia juzgando 19 formulas con la pregunta "se da vuelta
    /// el precio?", que no es esa afirmacion. Todas perdieron o empataron contra
    /// el placebo. Es posible que las formulas estuvieran bien y la vara mal.
    ///
    /// QUE REGISTRA, Y QUE NO
    /// Registra MATERIA PRIMA, una fila por vela: la hora, el rango, el volumen,
    /// las OPERACIONES (Ticks, que es literalmente la velocidad del tape), el
    /// delta, y los niveles vigentes en ese momento.
    ///
    /// NO calcula si el nivel "funciono". Eso se hace afuera, con placebo, en el
    /// laboratorio. Un indicador que decide si tiene razon es un indicador que
    /// siempre va a tener razon.
    ///
    /// COSTO
    /// Una linea de texto por vela. Con velas de un minuto son 390 lineas por
    /// rueda; el archivo de un mes entra en un megabyte.
    /// </summary>
    public sealed class Centinela
    {
        private readonly string _ruta;
        private readonly object _llave = new();
        private int _ultimaBarra = -1;
        private readonly StringBuilder _buf = new();
        private DateTime _ultimoVolcado = DateTime.MinValue;

        public Centinela(string instrumento, string marco)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            var limpio = Limpiar(instrumento) + "-" + Limpiar(marco);
            _ruta = Path.Combine(dir, "pythiagex-centinela-" + limpio + ".jsonl");
        }

        private static string Limpiar(string s)
        {
            if (string.IsNullOrEmpty(s)) return "x";
            var sb = new StringBuilder();
            foreach (var c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return sb.ToString();
        }

        /// <summary>
        /// Anota una vela. Se llama una vez por vela cerrada; si se llama de
        /// nuevo con la misma barra no hace nada, asi el archivo no se llena de
        /// repeticiones a cada tick.
        /// </summary>
        public void Anotar(int barra, DateTime hora, double o, double h, double l, double c,
                           double volumen, double ticks, double delta,
                           double spot, IEnumerable<KeyValuePair<string, double>> niveles)
        {
            Anotar(barra, hora, o, h, l, c, volumen, ticks, delta, spot, niveles, null);
        }

        /// <summary>Igual, con campos extra de order flow ya en JSON (sin la coma
        /// inicial): p. ej. "dmax":12,"dmin":-30,"big_n":2,"big_max":85. Van en "of".</summary>
        public void Anotar(int barra, DateTime hora, double o, double h, double l, double c,
                           double volumen, double ticks, double delta,
                           double spot, IEnumerable<KeyValuePair<string, double>> niveles, string extra)
        {
            if (barra <= _ultimaBarra) return;
            lock (_llave)
            {
                if (barra <= _ultimaBarra) return;
                _ultimaBarra = barra;
                var iv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(320);
                sb.Append("{\"t\":\"").Append(hora.ToString("yyyy-MM-ddTHH:mm:ss", iv))
                  .Append("\",\"o\":").Append(o.ToString("0.####", iv))
                  .Append(",\"h\":").Append(h.ToString("0.####", iv))
                  .Append(",\"l\":").Append(l.ToString("0.####", iv))
                  .Append(",\"c\":").Append(c.ToString("0.####", iv))
                  .Append(",\"vol\":").Append(volumen.ToString("0.#", iv))
                  // Ticks = cuantas operaciones se imprimieron en la vela. Es LA
                  // velocidad del tape, no un sustituto.
                  .Append(",\"ops\":").Append(ticks.ToString("0.#", iv))
                  .Append(",\"delta\":").Append(delta.ToString("0.#", iv))
                  // NaN NO ES JSON VALIDO y cualquier parser rechaza el archivo
                  // entero. Al arrancar, la cadena todavia no cargo y el spot
                  // viene NaN: ahi va null, que si es JSON.
                  .Append(",\"spot\":")
                  .Append(double.IsNaN(spot) || double.IsInfinity(spot)
                          ? "null" : spot.ToString("0.####", iv))
                  .Append(",\"niv\":{");
                bool primero = true;
                foreach (var kv in niveles)
                {
                    if (kv.Value <= 0 || double.IsNaN(kv.Value) || double.IsInfinity(kv.Value)) continue;
                    if (!primero) sb.Append(',');
                    primero = false;
                    sb.Append('"').Append(kv.Key).Append("\":")
                      .Append(kv.Value.ToString("0.####", iv));
                }
                sb.Append('}');
                if (!string.IsNullOrEmpty(extra)) sb.Append(",\"of\":{").Append(extra).Append('}');
                sb.Append('}').Append('\n');
                _buf.Append(sb);
            }
            Volcar(false);
        }

        /// <summary>Escribe lo acumulado. Se agrupa para no tocar el disco en
        /// cada vela con el mercado corriendo.</summary>
        public void Volcar(bool forzar)
        {
            string texto = null;
            lock (_llave)
            {
                if (_buf.Length == 0) return;
                if (!forzar && (DateTime.UtcNow - _ultimoVolcado).TotalSeconds < 20) return;
                texto = _buf.ToString();
                _buf.Clear();
                _ultimoVolcado = DateTime.UtcNow;
            }
            // SIN BOM. Encoding.UTF8 escribe la marca de orden de bytes en la
            // primera escritura y eso rompe la primera linea del JSONL.
            try { File.AppendAllText(_ruta, texto, new UTF8Encoding(false)); }
            catch { }
        }

        public string Ruta { get { return _ruta; } }
    }
}
