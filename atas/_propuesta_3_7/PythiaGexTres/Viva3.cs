using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PythiaGexTres
{
    /// <summary>
    /// EL ARCHIVO viva3: la foto de la cadena por la API, una linea por minuto.
    ///
    /// Factorizado de SondaApi (06-10-2026) para que la sonda y Gamma Hoy 3.0 escriban
    /// EXACTAMENTE la misma linea en el mismo archivo:
    ///   %APPDATA%\ATAS\PythiaGex3\viva\viva3-&lt;NQ|ES&gt;-&lt;yyyy-MM-dd UTC&gt;.jsonl
    ///   {"ts":"yyyy-MM-dd HH:mm:ss","futuro":F,"grandes":0,
    ///    "campos":"strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_cinta,vol_compra,vol_venta,strike0",
    ///    "filas":[[K,dias,1|0,oi,iv,bid,ask,vol_hoy,0,0,0,strike0],...]}
    /// con los formatos numericos de GammaHoy.VivaJson (2.0): K 0.##, dias 0.#####, oi 0.#,
    /// iv 0.######, bid/ask 0.####, vol 0.#. Solo filas con puntas e IV valida.
    ///
    /// UN SOLO ESCRITOR POR RAIZ: si la sonda y Gamma Hoy 3.0 (o dos graficos) comparten
    /// ATAS, el primero que escribio en los ultimos 180 s es el dueño del archivo y los
    /// demas no escriben (misma regla que la estela de GammaHoyCapas). Si el dueño se va,
    /// a los 180 s lo toma otro.
    /// </summary>
    internal static class Viva3
    {
        private static readonly object _llave = new object();
        private static readonly Dictionary<string, (object Dueno, DateTime Hora)> _escritor = new Dictionary<string, (object, DateTime)>(StringComparer.OrdinalIgnoreCase);

        public static string Carpeta => Path.Combine(Registro.CarpetaDatos, "viva");

        /// <summary>Puede escribir este dueño la raiz dada ahora? (y lo anota como dueño si si)</summary>
        public static bool TomarTurno(string raiz, object dueno, DateTime ahoraUtc)
        {
            lock (_llave)
            {
                if (_escritor.TryGetValue(raiz, out var e) && !ReferenceEquals(e.Dueno, dueno) && (ahoraUtc - e.Hora).TotalSeconds < 180) return false;
                _escritor[raiz] = (dueno, ahoraUtc);
                return true;
            }
        }

        /// <summary>Escribe la foto de ahora si este dueño tiene el turno. Devuelve cuantas filas fueron (0 = no se escribio).</summary>
        public static int Guardar(CadenaApi cadena, string raiz, object dueno, string log)
        {
            try
            {
                if (cadena == null || string.IsNullOrEmpty(raiz)) return 0;
                var ahora = DateTime.UtcNow;
                if (!TomarTurno(raiz, dueno, ahora)) return 0;
                var (json, n) = Json(cadena);
                if (json == null) return 0;
                var ruta = Path.Combine(Carpeta, "viva3-" + raiz + "-" + ahora.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
                Registro.Anexar(ruta, json);
                return n;
            }
            catch (Exception e) { Registro.Excepcion(log ?? "viva3", "Viva3.Guardar", e); return 0; }
        }

        /// <summary>La linea, o null si no hay futuro ni filas con IV.</summary>
        public static (string json, int filas) Json(CadenaApi cadena)
        {
            List<CadenaApi.Fila> fs = cadena.Filas();
            fs = fs.Where(f => f.ConPuntas && !double.IsNaN(f.IV) && f.IV > 0).ToList();
            if (fs.Count == 0 || cadena.Futuro <= 0) return (null, 0);
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1 << 15);
            sb.Append("{\"ts\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", inv))
              .Append("\",\"futuro\":").Append(cadena.Futuro.ToString("0.####", inv))
              .Append(",\"grandes\":0")
              .Append(",\"campos\":\"strike,dias,es_call,oi,iv,bid,ask,vol_hoy,vol_cinta,vol_compra,vol_venta,strike0\",\"filas\":[");
            bool primero = true;
            foreach (var f in fs)
            {
                if (!primero) sb.Append(','); primero = false;
                sb.Append('[').Append(f.K.ToString("0.##", inv)).Append(',').Append(f.Dias.ToString("0.#####", inv))
                  .Append(',').Append(f.EsCall ? 1 : 0).Append(',').Append(f.OI.ToString("0.#", inv))
                  .Append(',').Append(f.IV.ToString("0.######", inv)).Append(',').Append(f.Bid.ToString("0.####", inv))
                  .Append(',').Append(f.Ask.ToString("0.####", inv)).Append(',').Append(f.VolHoy.ToString("0.#", inv))
                  .Append(",0,0,0,").Append(f.K0.ToString("0.##", inv)).Append(']');
            }
            sb.Append("]}");
            return (sb.ToString(), fs.Count);
        }
    }
}
