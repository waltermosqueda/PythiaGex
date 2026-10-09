using System; using System.Linq; using System.Globalization;
namespace PythiaGexCuatro {
class Program { static void Main(string[] a) {
  var f = Foto.Leer(a[0]);
  Console.WriteLine("error=[" + f.Error + "] sesion=" + f.Sesion + " gen=" + f.GeneradoUtc.ToString("o") + " precio=" + f.Precio + " tq s=" + f.TqS + " c=" + f.TqC + " tqdato=" + f.TqDatoUtc.ToString("o"));
  foreach (var s in f.Series.Values.OrderBy(x=>x.Id)) { var last = s.Hist.Keys.DefaultIfEmpty(0).Max(); Console.WriteLine(s.Id + " n=" + s.Hist.Count + " color=" + s.Color.R + "," + s.Color.G + "," + s.Color.B + " ult=" + last + " -> " + (last>0? string.Join("/", s.Hist[last].Select(p=>p.ToString("0.00", CultureInfo.InvariantCulture))) : "")); }
  foreach (var fu in f.Fuentes) Console.WriteLine("fuente " + fu.Lb + " dato=" + fu.DatoUtc.ToString("HH:mm:ss") + " congelada=" + fu.Congelada + " conv=[" + fu.Conv + "]"); Console.WriteLine("instrumento=[" + f.Instrumento + "] tqsesion=" + f.TqSesion + " oiviejos=" + f.Actuales.Count(x=>x.OiViejo));
  Console.WriteLine("actuales=" + f.Actuales.Count + " ej: " + string.Join(" | ", f.Actuales.Take(4).Select(x=>x.Serie+" "+x.Rol+" "+x.Precio.ToString(CultureInfo.InvariantCulture)+" "+x.DatoUtc.ToString("HH:mm:ss"))));
}}}
