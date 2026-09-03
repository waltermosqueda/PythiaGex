using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PythiaGex
{
    /// <summary>
    /// LO ACUMULADO SOBREVIVE AL REINICIO.
    ///
    /// POR QUE EXISTE ESTE ARCHIVO
    ///
    /// El indicador acumula cosas que solo se pueden medir con el tiempo: donde
    /// estuvo la dominante en cada vela, el rastro de cada strike, las pelotitas
    /// del Max Change. Cada reinicio -- y en etapa de construccion son muchos --
    /// borraba todo eso y no se podia evaluar nada. El operador lo pidio, con la
    /// condicion explicita de no romper calculos ni "predicciones".
    ///
    /// LA CONDICION SE CUMPLE POR CONSTRUCCION. Aca NO se guarda ni un solo
    /// nivel calculado: el zero gamma, los muros y el perfil se rehacen desde la
    /// cadena en cada tick y no leen nada de este archivo. Lo unico que persiste
    /// es el REGISTRO de lo que ya se habia medido, que es historia y no
    /// prediccion.
    ///
    /// LA TRAMPA, Y COMO SE ESQUIVA
    ///
    /// Lo acumulado esta indexado por NUMERO DE VELA, y ese numero no es estable
    /// entre reinicios: si el grafico carga distinto historico, la vela 500 de
    /// hoy es otra vela manana. Guardarlo asi dejaria cada marca corrida a una
    /// vela que no le corresponde, sin dar error: se veria prolijo y estaria
    /// mal, que es la peor de las fallas.
    ///
    /// Por eso se guarda todo por HORA y al restaurar se busca a que vela le
    /// toca. Y se descarta:
    ///
    ///   - lo de otro instrumento o de otra temporalidad (archivo separado)
    ///   - lo mas viejo que el limite de horas
    ///   - lo que caiga fuera del rango de tiempo que el grafico tiene cargado
    ///   - lo de la vela en formacion, que se mide en vivo y no se restaura
    /// </summary>
    internal static class Historia
    {
        public sealed class MarcaDto
        {
            public long T;                 // unix segundos, UTC
            public double Mp, Mn, Z;
            public double[] D, I;
        }

        public sealed class PelotitaDto
        {
            public long T;
            public double K, Fut, Delta, Fuerza;
        }

        public sealed class EstelaDto
        {
            public double K;
            public long[] T;
            public double[] V;
        }

        public sealed class Paquete
        {
            public int V = 1;
            public string Guardado = "";
            public string Instrumento = "";
            public List<MarcaDto> Marcas = new();
            public List<PelotitaDto> Pelotitas = new();
            public List<EstelaDto> Estela = new();
        }

        private static readonly DateTime Epoca = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long AUnix(DateTime t)
        {
            var u = t.Kind == DateTimeKind.Utc ? t : DateTime.SpecifyKind(t, DateTimeKind.Utc);
            return (long)(u - Epoca).TotalSeconds;
        }

        public static DateTime DeUnix(long s) => Epoca.AddSeconds(s);

        public static string Ruta(string instrumento, string periodo)
        {
            var limpio = new string((instrumento + "-" + periodo)
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS");
            return Path.Combine(dir, "pythiagex-historia-" + limpio + ".json");
        }

        /// <summary>Escribe atomico: primero a .tmp y despues se reemplaza. Si
        /// ATAS se cae en el medio de un guardado, el archivo bueno sigue.</summary>
        /// <summary>
        /// OJO: IncludeFields. Los DTO de aca usan CAMPOS y no propiedades, y
        /// System.Text.Json ignora los campos por defecto: sin esto guardaba
        /// "{}" -- cinco bytes -- y parecia que funcionaba porque el archivo se
        /// creaba igual. Se vio pesando el archivo, no leyendolo.
        /// </summary>
        private static readonly JsonSerializerOptions Opciones =
            new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };

        public static void Guardar(string ruta, Paquete p)
        {
            var opt = Opciones;
            var tmp = ruta + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(p, opt), Encoding.UTF8);
            if (File.Exists(ruta)) File.Delete(ruta);
            File.Move(tmp, ruta);
        }

        public static Paquete Leer(string ruta, string instrumento, int horasMax, out string motivo)
        {
            motivo = "";
            try
            {
                if (!File.Exists(ruta)) { motivo = "no hay historia guardada"; return null; }
                var p = JsonSerializer.Deserialize<Paquete>(File.ReadAllText(ruta, Encoding.UTF8), Opciones);
                if (p == null) { motivo = "el archivo no se pudo leer"; return null; }

                // NO MEZCLAR INSTRUMENTOS. El archivo ya va separado por nombre,
                // pero se verifica igual: restaurar marcas de ES sobre un grafico
                // de NQ seria dibujar niveles que nunca existieron ahi.
                if (!string.IsNullOrEmpty(p.Instrumento) &&
                    !string.Equals(p.Instrumento, instrumento, StringComparison.OrdinalIgnoreCase))
                { motivo = "la historia es de " + p.Instrumento; return null; }

                var corte = AUnix(DateTime.UtcNow.AddHours(-Math.Max(1, horasMax)));
                p.Marcas = p.Marcas.Where(m => m.T >= corte).ToList();
                p.Pelotitas = p.Pelotitas.Where(x => x.T >= corte).ToList();
                foreach (var e in p.Estela)
                {
                    if (e.T == null || e.V == null) continue;
                    var t = new List<long>(); var v = new List<double>();
                    for (int i = 0; i < e.T.Length && i < e.V.Length; i++)
                        if (e.T[i] >= corte) { t.Add(e.T[i]); v.Add(e.V[i]); }
                    e.T = t.ToArray(); e.V = v.ToArray();
                }
                p.Estela = p.Estela.Where(e => e.T != null && e.T.Length > 0).ToList();
                return p;
            }
            catch (Exception e) { motivo = "error leyendo: " + e.Message; return null; }
        }
    }
}
