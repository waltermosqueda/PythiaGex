using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PythiaLaOla
{
    /// <summary>
    /// LA OLA 1.0: EL REGISTRO DE AUDITORIA. Una linea JSON por minuto (auditoria-&lt;inst&gt;-&lt;tf&gt;-&lt;aaaammdd&gt;.jsonl, desde el dibujo, asi
    /// queda lo que se VIO): la seccion "pantalla" con lo que muestran el panel, las rayas, la linea y el fondo en ese instante (clima y su
    /// numero, marea, ola, rayas con su precio y su pixel, X, mediana usada, ruedas usadas y de donde sale el dato), la seccion "ola" con
    /// el estado completo de las cargas (OlaNucleo.AuditoriaJson) y el rendimiento. El csv de pivotes, fotos y clima por minuto lo escribe
    /// el latido (PythiaOlaCinta.OlaCsv). Los dos los rehace herramientas/auditar_pythiaola.py desde la cinta de la sonda.
    /// </summary>
    public partial class PythiaOla
    {
        /// <summary>Lo que se dibujo en el ultimo cuadro (para la auditoria).</summary>
        private sealed class Pantalla
        {
            public int Momento, Ultima, PrimVis, UltVis;
            public string FondoModo = ""; public bool FondoRecorte; public sbyte FondoMomento = -1;
            public int Tramos, Rombos; public string TramoEnCurso = "";
            public decimal RayaOla, RayaMarea; public int RayaOlaY = int.MinValue, RayaMareaY = int.MinValue; public string RayaOlaTexto = "", RayaMareaTexto = "";
            public Rectangle Panel = Rectangle.Empty; public bool Live; public string Hora = "", Dia = "", Pie = "";
            public bool Minimizado, PieVisible; public string Estilo = "", Pastilla = "";   // 1.0c: lo que se VIO del cuadro
            public string[] Textos = Array.Empty<string>();
        }

        private static string J(string s) => (s ?? "").Replace("\\", "/").Replace("\"", "'").Replace('\n', ' ').Replace('\r', ' ');

        private static void FotoPantalla(StringBuilder sb, string nombre, bool hay, in FotoOla f)
        {
            sb.Append(",\"").Append(nombre).Append("\":{\"hay\":").Append(hay && f.HayPierna ? "true" : "false");
            if (hay && f.HayPierna)
                sb.Append(",\"dir\":").Append(f.Dir).Append(",\"con\":").Append(f.ConAgresores ? "true" : "false").Append(",\"delta\":").Append(F(f.DeltaPierna))
                  .Append(",\"faltan\":").Append(F(f.Faltan)).Append(",\"raya\":").Append(F(f.Raya)).Append(",\"extremo\":").Append(F(f.Extremo.Precio))
                  .Append(",\"origen\":").Append(F(f.Origen.Precio)).Append(",\"x\":").Append(F(f.X)).Append(",\"n\":").Append(f.N)
                  .Append(",\"t\":\"").Append(Hu(f.T)).Append('"');
            sb.Append('}');
        }

        private void Auditar(Instante s, Pantalla p, Rectangle reg)
        {
            try
            {
                var sb = new StringBuilder(16 * 1024);
                sb.Append("{\"utc\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv)).Append("\",\"v\":\"").Append(Version)
                  .Append("\",\"inst\":\"").Append(Inst()).Append("\",\"tf\":\"").Append(Marco()).Append('"');
                decimal tick = 0m; try { tick = InstrumentInfo?.TickSize ?? 0m; } catch { }
                sb.Append(",\"tick\":").Append(F(tick)).Append(",\"reg\":[").Append(reg.X).Append(',').Append(reg.Y).Append(',').Append(reg.Width).Append(',').Append(reg.Height).Append(']');
                // ---- lo que muestra la pantalla
                sb.Append(",\"pantalla\":{\"momento\":").Append(p.Momento).Append(",\"ultima\":").Append(p.Ultima).Append(",\"visibles\":[").Append(p.PrimVis).Append(',').Append(p.UltVis).Append(']')
                  .Append(",\"clave\":").Append(s.Clave).Append(",\"live\":").Append(p.Live ? "true" : "false").Append(",\"hora\":\"").Append(p.Hora).Append("\",\"dia\":\"").Append(p.Dia)
                  .Append("\",\"x\":").Append(F(s.X)).Append(",\"x_fuente\":\"").Append(J(s.FuenteX)).Append("\",\"mediana\":").Append(F(s.Mediana))
                  .Append(",\"med_clima\":").Append(ClimaRueda.D(s.MedClima)).Append(",\"ruedas\":").Append(s.Usadas)
                  .Append(",\"fuente\":\"cinta de ATAS (RequestForCumulativeTrades + OnCumulativeTrade)\",\"carga\":\"").Append(J(s.EstadoCarga))
                  .Append("\",\"estado\":\"").Append(J(s.Estado)).Append("\",\"atrasada\":").Append(s.Atrasada ? "true" : "false");
                sb.Append(",\"clima\":");
                GestorOla.ClimaJson(sb, s.HayClima, s.Clima, s.MotivoClima);
                FotoPantalla(sb, "marea", s.Hay, s.Marea);
                FotoPantalla(sb, "ola", s.Hay, s.Ola);
                sb.Append(",\"rayas\":{\"ola\":").Append(p.RayaOlaY == int.MinValue ? "null" : "{\"precio\":" + F(p.RayaOla) + ",\"y\":" + p.RayaOlaY + ",\"texto\":\"" + J(p.RayaOlaTexto) + "\"}")
                  .Append(",\"marea\":").Append(p.RayaMareaY == int.MinValue ? "null" : "{\"precio\":" + F(p.RayaMarea) + ",\"y\":" + p.RayaMareaY + ",\"texto\":\"" + J(p.RayaMareaTexto) + "\"}")
                  .Append('}');
                sb.Append(",\"capas\":{\"fondo\":").Append(_fondo ? "true" : "false").Append(",\"fondo_capa\":\"").Append(_capa).Append("\",\"modo_grafico\":\"").Append(J(p.FondoModo))
                  .Append("\",\"recorte\":").Append(p.FondoRecorte ? "true" : "false").Append(",\"fondo_momento\":").Append(p.FondoMomento)
                  .Append(",\"linea\":").Append(_linea ? "true" : "false").Append(",\"tramos\":").Append(p.Tramos).Append(",\"tramo_en_curso\":\"").Append(J(p.TramoEnCurso))
                  .Append("\",\"rayas\":").Append(_rayas ? "true" : "false").Append(",\"rombo\":").Append(_rombo ? "true" : "false").Append(",\"rombos\":").Append(p.Rombos)
                  .Append(",\"velas\":").Append(_velas ? "true" : "false").Append(",\"panel\":").Append(_panel ? "true" : "false").Append(",\"senales\":").Append(_senales ? "true" : "false").Append('}');
                sb.Append(",\"panel\":{\"rect\":[").Append(p.Panel.X).Append(',').Append(p.Panel.Y).Append(',').Append(p.Panel.Width).Append(',').Append(p.Panel.Height)
                  .Append("],\"pie\":\"").Append(J(p.Pie)).Append("\",\"pie_visible\":").Append(p.PieVisible ? "true" : "false")
                  .Append(",\"minimizado\":").Append(p.Minimizado ? "true" : "false").Append(",\"estilo\":\"").Append(J(p.Estilo))
                  .Append("\",\"pastilla\":\"").Append(J(p.Pastilla)).Append("\",\"textos\":[");
                for (int i = 0; i < p.Textos.Length; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(J(p.Textos[i])).Append('"'); }
                sb.Append("]}}");
                // ---- el estado completo de las cargas
                AuditarOla(sb);
                sb.Append(",\"rendimiento\":{\"dibujos\":").Append(_nRender).Append(",\"ms_dibujo_prom\":").Append((_nRender > 0 ? _msRender / _nRender : 0).ToString("0.000", Inv))
                  .Append(",\"ms_dibujo_max\":").Append(_msRenderMax.ToString("0.000", Inv)).Append("}}");
                string ruta = Path.Combine(Carpeta(), "auditoria-" + Inst() + "-" + Marco() + "-" + DateTime.UtcNow.ToString("yyyyMMdd", Inv) + ".jsonl");
                string linea = sb.ToString();
                Task.Run(() => { try { lock (_llaveArchivo) File.AppendAllText(ruta, linea + "\n"); } catch { } });
            }
            catch (Exception e) { Anotar(e); }
        }
    }
}
