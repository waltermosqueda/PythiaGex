using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PythiaGex
{
    /// <summary>
    /// El puente que le faltaba al proyecto.
    ///
    /// Hasta ahora el indicador calculaba el perfil de volumen, el VWAP, la
    /// absorcion y el order flow parado en cada nivel... y lo tiraba en cada
    /// cuadro. El centinela, que mide si lo que prometemos pasa de verdad,
    /// solo podia probar hipotesis sacadas de la cadena de opciones. O sea:
    /// las hipotesis con mas chance de ser ciertas eran justo las unicas que
    /// no se podian ni formular.
    ///
    /// Esta clase escribe una linea por foto con lo que ATAS vio en cada
    /// nivel. Con eso el centinela puede por fin preguntar cosas como
    /// "un muro que ademas cae sobre un nodo de alto volumen, aguanta mas?"
    /// y contestarlas con numeros en vez de con una opinion.
    ///
    /// Reglas que se respetan aca adentro:
    ///
    ///   - No se escribe NADA que no se haya medido. Un campo que no se pudo
    ///     calcular sale como null, nunca como cero.
    ///   - No bloquea el dibujo: se llama desde el temporizador, no desde
    ///     OnRender, y cualquier error se traga sin romper el indicador.
    ///   - No lleva ninguna ruta personal adentro. Por defecto escribe en la
    ///     carpeta de datos de ATAS del usuario que lo corre.
    /// </summary>
    public sealed class Bitacora
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public sealed class Anotacion
        {
            public string Nombre, Razones, Flujo0;
            public double? PrecioFut, PrecioIdx, GexM, Prob, Iv;
            public bool Es0dte;
            public int Puntaje;
            // lo que vio ATAS parado en el nivel
            public double? Volumen, Delta, PctSesion;
            public bool Absorcion;
            public string Nodo;              // alto | bajo | normal
            public int? DistVwapTk, DistPrecioTk;
            // gatillos de order flow
            public int? Apilados, LadoApilados, LadoDivergencia;
            public double? PrintVeces;
            public bool PrintGrande, Divergencia;
            // el libro: lo que espera, no lo que ya se opero
            public double? BarridoCompra, BarridoVenta, LibroBids, LibroAsks, DesbalanceDom;
            public int? Barridos;
            public string SuerteMuro;        // comido | retirado | crecio | igual
        }

        /// <summary>Carpeta donde se escribe. Vacio = la de ATAS del usuario.</summary>
        public string Carpeta = "";

        /// <summary>Cada cuanto se anota. Mas seguido no aporta: el perfil no
        /// cambia de manera util entre dos minutos.</summary>
        public int MinutosEntreAnotaciones = 5;

        private DateTime _ultima = DateTime.MinValue;
        public string Estado = "sin escribir";
        public int Escritas;

        public string CarpetaEfectiva()
        {
            if (!string.IsNullOrWhiteSpace(Carpeta)) return Carpeta.Trim();
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ATAS", "PythiaGex", "contexto");
        }

        public bool Toca()
            => (DateTime.UtcNow - _ultima).TotalMinutes >= Math.Max(1, MinutosEntreAnotaciones);

        /// <summary>
        /// Escribe una linea. Devuelve false y deja el motivo en Estado si no
        /// se pudo: nunca tira una excepcion hacia el indicador.
        /// </summary>
        /// <summary>Cuando se anoto por ultima vez, para que quien escribe
        /// sepa desde donde tiene que juntar el maximo y el minimo.</summary>
        public DateTime UltimaUtc => _ultima;

        public bool Anotar(string instrumento, double precio, double tickSize,
                           double maximo, double minimo,
                           Contexto sesion, Contexto previo, Contexto semana,
                           bool pocVirgen, List<Anotacion> niveles,
                           List<Disparo.Evento> disparos)
        {
            try
            {
                var dir = CarpetaEfectiva();
                Directory.CreateDirectory(dir);
                var ruta = Path.Combine(
                    dir, "contexto-" + DateTime.UtcNow.ToString("yyyy-MM-dd", Inv) + ".jsonl");

                var b = new StringBuilder(4096);
                b.Append('{');
                Txt(b, "t", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "Z"); b.Append(',');
                Txt(b, "instrumento", instrumento ?? ""); b.Append(',');
                Num(b, "precio", precio); b.Append(',');
                // El maximo y el minimo desde la anotacion anterior. Sin esto
                // el camino del precio quedaria hecho de puntos sueltos cada
                // cinco minutos, y una mecha que toca un nivel entre dos
                // puntos no se veria nunca. Con esto el tramo es completo.
                Num(b, "maximo", maximo); b.Append(',');
                Num(b, "minimo", minimo); b.Append(',');
                Num(b, "tick", tickSize); b.Append(',');

                b.Append("\"sesion\":"); Perfil(b, sesion, true); b.Append(',');
                b.Append("\"previo\":"); Perfil(b, previo, false);
                b.Append(",\"poc_previo_virgen\":");
                b.Append(previo != null && previo.Listo ? (pocVirgen ? "true" : "false") : "null");
                b.Append(',');
                b.Append("\"semana\":"); Perfil(b, semana, false); b.Append(',');

                b.Append("\"niveles\":[");
                for (int i = 0; i < niveles.Count; i++)
                {
                    if (i > 0) b.Append(',');
                    Nivel(b, niveles[i]);
                }
                b.Append(']');

                // Los disparos que salieron en este tramo. Sin esto, si el
                // gatillo sirve o es ruido queda en opinion: escritos, el
                // centinela cuenta cuantos acertaron y con eso se sube o se
                // baja el umbral. Es la unica forma honesta de calibrarlo.
                b.Append(",\"disparos\":[");
                if (disparos != null)
                {
                    for (int i = 0; i < disparos.Count; i++)
                    {
                        var e = disparos[i];
                        if (i > 0) b.Append(',');
                        b.Append('{');
                        Txt(b, "t", e.Hora.ToUniversalTime()
                                     .ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "Z"); b.Append(',');
                        Txt(b, "nivel", e.Nivel ?? ""); b.Append(',');
                        b.Append("\"es0dte\":").Append(e.Es0dte ? "true" : "false").Append(',');
                        Ent(b, "lado", e.Lado); b.Append(',');
                        Ent(b, "puntaje", e.Puntaje); b.Append(',');
                        Num(b, "precio", (double)e.Precio); b.Append(',');
                        Num(b, "precio_barra", (double)e.PrecioBarra); b.Append(',');
                        Txt(b, "razones", e.Razones ?? "");
                        b.Append('}');
                    }
                }
                b.Append("]}");
                b.Append('\n');

                // UTF8 a secas escribe un BOM al crear el archivo, y eso
                // rompe la primera linea para cualquiera que la lea como
                // JSON. Se descartaba en silencio la primera anotacion de
                // cada dia. UTF8Encoding(false) escribe sin BOM.
                File.AppendAllText(ruta, b.ToString(), new UTF8Encoding(false));
                _ultima = DateTime.UtcNow;
                Escritas++;
                Estado = "ok " + _ultima.ToString("HH:mm", Inv) + "  (" + Escritas + ")";
                return true;
            }
            catch (Exception e)
            {
                Estado = "error: " + e.Message;
                return false;
            }
        }

        private static void Perfil(StringBuilder b, Contexto c, bool completo)
        {
            if (c == null || !c.Listo) { b.Append("null"); return; }
            b.Append('{');
            Num(b, "poc", (double)c.Poc); b.Append(',');
            Num(b, "vah", (double)c.Vah); b.Append(',');
            Num(b, "val", (double)c.Val); b.Append(',');
            Num(b, "vwap", (double)c.Vwap); b.Append(',');
            Num(b, "sigma", (double)c.Sigma); b.Append(',');
            Num(b, "volumen", (double)c.VolumenTotal); b.Append(',');
            Ent(b, "barras", c.BarrasUsadas);
            if (completo)
            {
                b.Append(',');
                Num(b, "apertura", (double)c.Apertura); b.Append(',');
                Num(b, "maximo", (double)c.Maximo); b.Append(',');
                Num(b, "minimo", (double)c.Minimo); b.Append(',');
                Num(b, "ib_alto", (double)c.IbAlto); b.Append(',');
                Num(b, "ib_bajo", (double)c.IbBajo); b.Append(',');
                Num(b, "delta_acum", (double)c.DeltaAcumulado);
            }
            b.Append('}');
        }

        private static void Nivel(StringBuilder b, Anotacion a)
        {
            b.Append('{');
            Txt(b, "nombre", a.Nombre ?? ""); b.Append(',');
            b.Append("\"es0dte\":").Append(a.Es0dte ? "true" : "false").Append(',');
            NumN(b, "fut", a.PrecioFut); b.Append(',');
            NumN(b, "idx", a.PrecioIdx); b.Append(',');
            NumN(b, "gex_m", a.GexM); b.Append(',');
            NumN(b, "prob", a.Prob); b.Append(',');
            NumN(b, "iv", a.Iv); b.Append(',');
            Ent(b, "confluencia", a.Puntaje); b.Append(',');
            Txt(b, "razones", a.Razones ?? ""); b.Append(',');
            NumN(b, "vol", a.Volumen); b.Append(',');
            NumN(b, "delta", a.Delta); b.Append(',');
            NumN(b, "pct_sesion", a.PctSesion); b.Append(',');
            b.Append("\"absorcion\":").Append(a.Absorcion ? "true" : "false").Append(',');
            Txt(b, "nodo", a.Nodo ?? "normal"); b.Append(',');
            EntN(b, "dist_vwap_tk", a.DistVwapTk); b.Append(',');
            EntN(b, "dist_precio_tk", a.DistPrecioTk); b.Append(',');
            EntN(b, "apilados", a.Apilados); b.Append(',');
            EntN(b, "lado_apilados", a.LadoApilados); b.Append(',');
            NumN(b, "print_veces", a.PrintVeces); b.Append(',');
            b.Append("\"print_grande\":").Append(a.PrintGrande ? "true" : "false").Append(',');
            b.Append("\"divergencia\":").Append(a.Divergencia ? "true" : "false").Append(',');
            EntN(b, "lado_divergencia", a.LadoDivergencia); b.Append(',');
            EntN(b, "barridos", a.Barridos); b.Append(',');
            NumN(b, "barrido_compra", a.BarridoCompra); b.Append(',');
            NumN(b, "barrido_venta", a.BarridoVenta); b.Append(',');
            NumN(b, "libro_bids", a.LibroBids); b.Append(',');
            NumN(b, "libro_asks", a.LibroAsks); b.Append(',');
            NumN(b, "desbalance_dom", a.DesbalanceDom); b.Append(',');
            Txt(b, "suerte_muro", a.SuerteMuro ?? "");
            b.Append('}');
        }

        // --- serializacion a mano: son seis tipos, no hace falta una libreria
        private static void Txt(StringBuilder b, string k, string v)
        {
            b.Append('"').Append(k).Append("\":\"");
            foreach (var ch in v)
            {
                if (ch == '"' || ch == '\\') b.Append('\\').Append(ch);
                else if (ch == '\n' || ch == '\r' || ch == '\t') b.Append(' ');
                else if (ch < 32) { /* se descarta */ }
                else b.Append(ch);
            }
            b.Append('"');
        }

        private static void Num(StringBuilder b, string k, double v)
        {
            b.Append('"').Append(k).Append("\":");
            if (double.IsNaN(v) || double.IsInfinity(v)) b.Append("null");
            else b.Append(Math.Round(v, 4).ToString(Inv));
        }

        private static void NumN(StringBuilder b, string k, double? v)
        {
            if (!v.HasValue) { b.Append('"').Append(k).Append("\":null"); return; }
            Num(b, k, v.Value);
        }

        private static void Ent(StringBuilder b, string k, int v)
            => b.Append('"').Append(k).Append("\":").Append(v.ToString(Inv));

        private static void EntN(StringBuilder b, string k, int? v)
        {
            if (!v.HasValue) { b.Append('"').Append(k).Append("\":null"); return; }
            Ent(b, k, v.Value);
        }
    }
}
