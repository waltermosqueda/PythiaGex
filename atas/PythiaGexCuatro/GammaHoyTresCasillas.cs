// GammaHoyTresCasillas.cs — PythiaGex 4.1.5c (09-10-2026; 4.1.5e: el log dice si una casilla prendida no dibuja por su llave). Pedido del operador: "activa/desactiva algunas dominantes para ver si se desactivan
// o no realmente en pantalla en tiempo real y cuanto tardan en responder ... con que funcione la activacion/desactivacion en pocos instantes me sirve".
// Medido (09-10 17:04 ART, mercado abierto): destildar en el dialogo de ajustes se ve en < 1 s porque cada tick redibuja. Sin ticks el Tick de 5 s
// (GammaHoyTres.Tick, el mismo SubscribeToTimer) igual termina en RedrawChart, tambien con un grafico que no es NQ/ES/RTY: antes el peor caso era
// <= 5 s, ahora <= 250 ms (4.1.5d: corregido; aca decia que sin ticks 'nadie pedia redibujar' hasta el proximo precio, y eso NO era cierto ni
// estaba medido de noche).
// 4.1.5d (revision 4.1.5c): el reloj mira tambien los ENUM del grupo de arriba (perfil y 'ver' de la 3.0, doble eje), no solo los bool; la
// comparacion es por valor (object.Equals). 'primer render con el cambio' mide el OnRender, no prueba que algo se vea: la prueba es la captura.
// Aca: un reloj de 250 ms mira las casillas del grupo de arriba ("0. PRENDER / APAGAR", ver herramientas/grupo_arriba_415e.py: los mismos bool y
// enum que el operador ve en el dialogo, por Display.Order) con una foto barata; si alguna cambio, pide RedrawChart y anota en pythiagex4-pantalla.log:
//   "casilla <Propiedad> (<nombre en el dialogo>): False->True detectada HH:mm:ss.fff"  y en el primer OnRender despues:
//   "primer render con el cambio HH:mm:ss.fff (N ms desde que se detecto)".
// 4.1.5e (pedido del operador: "pierdo mucho tiempo ... adivinando para ver si aparecen/desaparecen las dominantes especificas que quiero"): la
// jerarquia del grupo (cada llave '...▸' antes de sus '↳ ...') la arma grupo_arriba_415e.py y la primera frase de cada descripcion dice que llave
// necesita ("Necesita la llave '3.0 capas▸' prendida." / "Necesita alguna de las llaves ... prendida."). El reloj la lee de ahi (una vez) y el log
// lo dice: al arrancar, "prendidas SIN dibujo por su llave apagada: N (...)" (lo que midio el verificador a mano el 09-10: 8 de 28), y en cada
// cambio, si esa casilla queda prendida con su llave apagada, " · su llave '...' esta APAGADA: no se dibuja". Solo texto del log: no cambia el dibujo.
// No recalcula nada (ni la Familia ni la 3.0): solo redibuja. Nunca tira.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ATAS.Indicators;

namespace PythiaGexCuatro
{
    public partial class FamiliaCuatro
    {
        /// <summary>El grupo de arriba del dialogo (4.1.5c; 4.1.5e: con la jerarquia de llaves de herramientas/grupo_arriba_415e.py): todas las casillas de dibujo.</summary>
        public const string GRUPO_CASILLAS = "0. PRENDER / APAGAR (todo lo que se dibuja)";
        private const string LOG_CASILLAS = "pantalla";

        private static PropertyInfo[] _casillasDibujo;
        private static string[] _casillasNombre;
        private static PropertyInfo[][] _casillasLlaves;   // 4.1.5e: la(s) llave(s) que pide la primera frase de la descripcion (null = ninguna)
        private static bool[] _casillasAlguna;             // 4.1.5e: true = "alguna de las llaves" (lo comun a varias); false = "la llave"
        private object[] _casillasFoto;
        private readonly TimeSpan _casillasPeriodo = TimeSpan.FromMilliseconds(250);
        private Action _casillasReloj;
        private int _casillasCorriendo;
        private long _casillasCambioTicks;            // DateTime.UtcNow.Ticks del ultimo cambio detectado sin render todavia (0 = nada pendiente)
        private string _casillasCambioTxt;

        /// <summary>Las propiedades bool o enum del grupo de arriba, en el orden del dialogo (Display.Order).</summary>
        private static void CasillasArmar()
        {
            if (_casillasDibujo != null) return;
            var ps = typeof(FamiliaCuatro).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => (p.PropertyType == typeof(bool) || p.PropertyType.IsEnum) && p.CanRead && p.GetCustomAttribute<DisplayAttribute>()?.GroupName == GRUPO_CASILLAS)
                .OrderBy(p => p.GetCustomAttribute<DisplayAttribute>().GetOrder() ?? 0).ToArray();
            _casillasNombre = ps.Select(p => p.GetCustomAttribute<DisplayAttribute>().Name ?? p.Name).ToArray();
            // 4.1.5e: la llave de cada casilla, de la primera frase de su descripcion (la escribe herramientas/grupo_arriba_415e.py); solo llaves bool
            var llaves = new PropertyInfo[ps.Length][]; var alguna = new bool[ps.Length];
            try
            {
                var porNombre = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
                for (int i = 0; i < ps.Length; i++) if (ps[i].PropertyType == typeof(bool) && !porNombre.ContainsKey(_casillasNombre[i])) porNombre[_casillasNombre[i]] = ps[i];
                for (int i = 0; i < ps.Length; i++)
                {
                    var m = Regex.Match(ps[i].GetCustomAttribute<DisplayAttribute>().Description ?? "", @"^Necesita (la llave|alguna de las llaves) (.+?) prendida\.");
                    if (!m.Success) continue;
                    var ks = Regex.Matches(m.Groups[2].Value, "'([^']+)'").Cast<Match>()
                                  .Select(x => porNombre.TryGetValue(x.Groups[1].Value, out var k) ? k : null).Where(k => k != null).ToArray();
                    if (ks.Length == 0) continue;
                    llaves[i] = ks; alguna[i] = m.Groups[1].Value != "la llave";
                }
            }
            catch { }
            _casillasLlaves = llaves; _casillasAlguna = alguna;
            _casillasDibujo = ps;
        }

        /// <summary>4.1.5e: " · su llave '3.0 capas▸' esta APAGADA: no se dibuja" si la casilla i necesita una llave que esta apagada; "" si no
        /// necesita ninguna o la tiene prendida. Lee las llaves en vivo (reflexion); nunca tira.</summary>
        private string CasillasLlaveTexto(int i)
        {
            try
            {
                var ks = _casillasLlaves?[i]; if (ks == null) return "";
                var on = ks.Select(k => k.GetValue(this) is bool b && b).ToArray();
                if (_casillasAlguna[i] ? on.Any(x => x) : on.All(x => x)) return "";
                var nombres = string.Join(", ", ks.Select(k => "'" + (k.GetCustomAttribute<DisplayAttribute>()?.Name ?? k.Name) + "'"));
                return _casillasAlguna[i] ? " · ninguna de sus llaves (" + nombres + ") esta prendida: no se dibuja" : " · su llave " + nombres + " esta APAGADA: no se dibuja";
            }
            catch { return ""; }
        }

        /// <summary>4.1.5e: las casillas bool PRENDIDAS que no dibujan nada porque su llave esta apagada: "N (Prop1,Prop2,...)".</summary>
        private string CasillasSinDibujo()
        {
            try
            {
                var l = new List<string>();
                for (int i = 0; i < _casillasDibujo.Length; i++)
                {
                    var p = _casillasDibujo[i];
                    if (p.PropertyType != typeof(bool) || _casillasLlaves?[i] == null || !(p.GetValue(this) is bool b && b)) continue;
                    if (CasillasLlaveTexto(i) != "") l.Add(p.Name);
                }
                return l.Count.ToString(CultureInfo.InvariantCulture) + (l.Count == 0 ? "" : " (" + string.Join(",", l) + ")");
            }
            catch { return "?"; }
        }

        /// <summary>Desde OnInitialize (idempotente).</summary>
        private void CasillasArrancar()
        {
            try
            {
                if (_casillasReloj != null) return;
                CasillasArmar();
                _casillasFoto = CasillasLeer();
                _casillasReloj = CasillasMirar;
                SubscribeToTimer(_casillasPeriodo, _casillasReloj);
                Registro.Linea(LOG_CASILLAS, "casillas 4.1.5e: " + _casillasDibujo.Length + " en el grupo '" + GRUPO_CASILLAS + "' ("
                    + (_casillasLlaves?.Count(x => x != null) ?? 0) + " con llave); prendidas: "
                    + string.Join(",", _casillasDibujo.Where((p, i) => _casillasFoto != null && _casillasFoto[i] is bool b && b).Select(p => p.Name))
                    + "; listas: " + string.Join(",", _casillasDibujo.Select((p, i) => (p, i)).Where(x => x.p.PropertyType.IsEnum && _casillasFoto != null)
                                                                    .Select(x => x.p.Name + "=" + Convert.ToString(_casillasFoto[x.i], CultureInfo.InvariantCulture)))
                    + "; prendidas SIN dibujo por su llave apagada: " + CasillasSinDibujo());
            }
            catch (Exception e) { Registro.Excepcion(LOG_CASILLAS, "CasillasArrancar", e); }
        }

        /// <summary>Desde OnDispose.</summary>
        private void CasillasParar()
        {
            try { if (_casillasReloj != null) UnsubscribeFromTimer(_casillasPeriodo, _casillasReloj); } catch { }
            _casillasReloj = null;
        }

        private object[] CasillasLeer()
        {
            var ps = _casillasDibujo; if (ps == null) return null;
            var v = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++) { try { v[i] = ps[i].GetValue(this); } catch { } }
            return v;
        }

        /// <summary>El reloj de 250 ms: si cambio alguna casilla de dibujo, redibuja YA (aunque no lleguen ticks) y anota la hora.</summary>
        private void CasillasMirar()
        {
            if (System.Threading.Interlocked.Exchange(ref _casillasCorriendo, 1) == 1) return;
            try
            {
                var ahora = CasillasLeer(); var antes = _casillasFoto;
                if (ahora == null) return;
                if (antes == null || antes.Length != ahora.Length) { _casillasFoto = ahora; return; }
                string txt = null, llave = "";
                for (int i = 0; i < ahora.Length; i++)
                {
                    if (Equals(ahora[i], antes[i])) continue;
                    txt = (txt == null ? "" : txt + "; ") + _casillasDibujo[i].Name + " (" + _casillasNombre[i] + "): " + antes[i] + "->" + ahora[i];
                    if (!(ahora[i] is bool on) || on) { string lt = CasillasLlaveTexto(i); if (lt != "") llave += lt.Replace(" · ", " · " + _casillasDibujo[i].Name + ": "); }   // 4.1.5e
                }
                if (txt == null) return;
                _casillasFoto = ahora;
                var t = DateTime.UtcNow;
                _casillasCambioTxt = txt;
                System.Threading.Interlocked.Exchange(ref _casillasCambioTicks, t.Ticks);
                Registro.Linea(LOG_CASILLAS, "casilla " + txt + " detectada " + t.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                    + llave + " · prendidas SIN dibujo por su llave apagada: " + CasillasSinDibujo());   // 4.1.5e
                try { RedrawChart(new RedrawArg(ChartArea)); } catch { }
            }
            catch (Exception e) { Registro.Excepcion(LOG_CASILLAS, "CasillasMirar", e); }
            finally { System.Threading.Interlocked.Exchange(ref _casillasCorriendo, 0); }
        }

        /// <summary>Desde OnRender, al final: el primer render despues de un cambio anota cuanto tardo.</summary>
        private void CasillasRender()
        {
            long t0 = System.Threading.Interlocked.Exchange(ref _casillasCambioTicks, 0);
            if (t0 == 0) return;
            try
            {
                var t = DateTime.UtcNow;
                Registro.Linea(LOG_CASILLAS, "primer render con el cambio " + t.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " ("
                    + ((t.Ticks - t0) / TimeSpan.TicksPerMillisecond).ToString(CultureInfo.InvariantCulture) + " ms desde que se detecto: " + _casillasCambioTxt + ")");
            }
            catch { }
        }
    }
}
