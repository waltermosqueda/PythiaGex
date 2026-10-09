using System;
using System.IO;
using System.Text;

namespace PythiaGexCuatro
{
    /// <summary>
    /// EL LOG DE LA 4.0 (motor 3.0 adentro): %APPDATA%\ATAS\pythiagex4-&lt;nombre&gt;.log, una linea por paso.
    ///
    /// ATAS se traga las excepciones de los indicadores sin decir nada: si algo
    /// no queda escrito aca, no paso. Por eso todo lo que hace la cadena y la
    /// sonda pasa por esta clase, y por eso escribir NUNCA puede tirar (si el
    /// disco falla, se pierde la linea y listo).
    ///
    /// Los datos van a %APPDATA%\ATAS\PythiaGex4\ (carpeta propia, separada de
    /// PythiaGex (prod) y PythiaGex2 (2.0)).
    /// </summary>
    internal static class Registro
    {
        private static readonly object _llave = new object();

        public static string CarpetaAtas =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");

        /// <summary>%APPDATA%\ATAS\PythiaGex4 (4.1: carpeta propia; la 3.0 usa PythiaGex3)</summary>
        public static string CarpetaDatos => Path.Combine(CarpetaAtas, "PythiaGex4");

        public static string RutaLog(string nombre) => Path.Combine(CarpetaAtas, "pythiagex4-" + nombre + ".log");

        /// <summary>4.1.0: tope de cada log (el de la 3.0 crecio 5,2 MB en 2 dias; el de produccion llego a 64 MB). Al pasarlo se aparta a .1
        /// (pisando el .1 anterior) y se empieza de nuevo: como mucho 2 x 20 MB por log.</summary>
        public const long TOPE_LOG_BYTES = 20L << 20;
        private static int _escrituras;

        public static void Linea(string nombre, string msg)
        {
            try
            {
                var txt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + msg + "\n";
                var ruta = RutaLog(nombre);
                lock (_llave)
                {
                    if ((++_escrituras & 255) == 1) Rotar(ruta, TOPE_LOG_BYTES);   // el tamaño se mira cada 256 lineas (y en la primera)
                    File.AppendAllText(ruta, txt, new UTF8Encoding(false));
                }
            }
            catch { }
        }

        /// <summary>Si el archivo pasa el tope, lo mueve a ruta.1 (nunca tira).</summary>
        public static void Rotar(string ruta, long topeBytes)
        {
            try
            {
                var fi = new FileInfo(ruta);
                if (fi.Exists && fi.Length > topeBytes) File.Move(ruta, ruta + ".1", true);
            }
            catch { }
        }

        public static void Excepcion(string nombre, string donde, Exception e)
        {
            if (e == null) return;
            var i = e;
            while (i.InnerException != null && (i is System.Reflection.TargetInvocationException || i is AggregateException)) i = i.InnerException;
            var pila = (i.StackTrace ?? "").Replace("\r", " ").Replace("\n", " ");
            if (pila.Length > 400) pila = pila.Substring(0, 400);
            Linea(nombre, "EXCEPCION en " + donde + ": " + i.GetType().Name + ": " + i.Message + " | " + pila);
        }

        /// <summary>Escribe un renglon de texto a un archivo de datos (jsonl), creando la carpeta.</summary>
        public static void Anexar(string rutaCompleta, string renglon)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(rutaCompleta) ?? CarpetaDatos);
                lock (_llave) File.AppendAllText(rutaCompleta, renglon + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
