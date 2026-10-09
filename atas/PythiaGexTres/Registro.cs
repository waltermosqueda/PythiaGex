using System;
using System.IO;
using System.Text;

namespace PythiaGexTres
{
    /// <summary>
    /// EL LOG DE 3.0: %APPDATA%\ATAS\pythiagex3-&lt;nombre&gt;.log, una linea por paso.
    ///
    /// ATAS se traga las excepciones de los indicadores sin decir nada: si algo
    /// no queda escrito aca, no paso. Por eso todo lo que hace la cadena y la
    /// sonda pasa por esta clase, y por eso escribir NUNCA puede tirar (si el
    /// disco falla, se pierde la linea y listo).
    ///
    /// Los datos van a %APPDATA%\ATAS\PythiaGex3\ (carpeta propia, separada de
    /// PythiaGex (prod) y PythiaGex2 (2.0)).
    /// </summary>
    internal static class Registro
    {
        private static readonly object _llave = new object();

        public static string CarpetaAtas =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");

        /// <summary>%APPDATA%\ATAS\PythiaGex3</summary>
        public static string CarpetaDatos => Path.Combine(CarpetaAtas, "PythiaGex3");

        public static string RutaLog(string nombre) => Path.Combine(CarpetaAtas, "pythiagex3-" + nombre + ".log");

        public static void Linea(string nombre, string msg)
        {
            try
            {
                var txt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + msg + "\n";
                lock (_llave) File.AppendAllText(RutaLog(nombre), txt, new UTF8Encoding(false));
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
