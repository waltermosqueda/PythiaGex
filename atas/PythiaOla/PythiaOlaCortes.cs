using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace PythiaLaOla
{
    /// <summary>
    /// LA OLA 1.0b (27-11-2026): la union con ATAS del detector de CORTES DE DATOS (OlaCortes.cs, sin ATAS, probado en el arnes con el log
    /// real del 27-11: re-pide en los 5 cortes del dia, 04:00, 12:47, 12:50, 13:06 y 14:07 AR, y en ningun otro momento).
    /// Cada 2 s lee lo NUEVO del log de ATAS (%APPDATA%\ATAS\Logs\app_AAAAMMDD.log, abierto compartido: ATAS lo tiene abierto) y cada latido
    /// le pasa al detector la hora de la ultima orden en vivo. Cuando el detector dice "re-pedir":
    ///  - con el ajuste "Tras un corte de datos: recargar el grafico solo" (de fabrica SI, pedido del operador el 27-11: "estaria bueno
    ///    agregarlo automaticamente cuando pase lo de las desconexiones") y el comando encontrado: aprieta POR DENTRO el "Reload all
    ///    historical data" de ESTE grafico (el ICommand ReloadDataCommand de su modelo de vista, en el hilo de la interfaz; sin mouse, sin
    ///    teclado, sin foco). ATAS recarga el grafico (en blanco 6-15 s, medido el 27-11 con el boton) y La Ola recarga todo (OnRecalculate).
    ///  - si no: La Ola tira sus cargas y pide de nuevo la jornada al historial (lo mismo que al cambiar la X fija) y el pie avisa que el
    ///    GRAFICO de ATAS tambien quedo sin esas ordenes.
    /// El comando se busca por reflexion la primera vez que hace falta (desde ChartInfo y, si no, en los modelos de vista de las ventanas
    /// de ATAS que apuntan al mismo grafico) y la ruta queda en el log. Para PROBARLO sin tocar la pantalla: crear el archivo
    /// %APPDATA%\ATAS\PythiaGex\pythiaola\recargar-&lt;inst&gt;.txt (lo toma y lo borra en 2 s; recarga una vez).
    /// </summary>
    public partial class PythiaOla
    {
        private readonly DetectorCortes _cortes = new DetectorCortes();
        private string _cortesRuta; private long _cortesPos = -1; private DateTime _cortesLeido = DateTime.MinValue;
        private int _cortesErrores;
        // el comando "Reload all historical data" de este grafico
        private System.Windows.Input.ICommand _recargaCmd; private string _recargaRuta = ""; private bool _recargaBuscada;
        // compartido POR INSTRUMENTO entre instancias: ATAS crea el indicador de nuevo al recargar el grafico, y el limite de "una recarga cada
        // 3 minutos" tiene que sobrevivir a eso (una conexion que se corta y vuelve seguido no puede dejar el grafico parpadeando)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Ult, DateTime Corte)> _recargasPorInst =
            new System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Ult, DateTime Corte)>();
        private DateTime RecargaUlt => _recargasPorInst.TryGetValue(Inst(), out var v) ? v.Ult : DateTime.MinValue;
        private DateTime RecargaCorte => _recargasPorInst.TryGetValue(Inst(), out var v) ? v.Corte : DateTime.MinValue;
        private const double RecargaMinEntreSeg = 180;

        /// <summary>Desde OlaLatido (cada 250 ms).</summary>
        private void OlaCortes()
        {
            var ahora = DateTime.UtcNow;
            if ((ahora - _cortesLeido).TotalSeconds >= 2) { _cortesLeido = ahora; CortesLeerLog(ahora); CortesPedidoArchivo(); }
            DateTime ult; bool hayVivo;
            lock (_llaveOla) { ult = _olaUltEventoPc; var v = OlaG().Viva; hayVivo = v != null && v.RecibeVivo; }
            if (ult > DateTime.MinValue) _cortes.Evento(ult);
            if (!_cortes.Revisar(ahora, CortesEnRueda(ahora), hayVivo)) return;
            string hora = HoraLocal(_cortes.UltimoCorte);
            string que = "corte de datos (" + _cortes.UltimoMotivo + ") desde " + _cortes.UltimoCorte.ToString("HH:mm:ss", Inv) + " UTC";
            AnotarLinea("ola: " + que + " (re-pedido " + _cortes.Repedidos + " de hoy; cortes vistos " + _cortes.Vistos + ")");
            if (_recargaTrasCorte && RecargarGrafico("corte de datos " + hora, _cortes.UltimoCorte)) { _cortes.FijarAviso("corte de datos " + hora + ": recargando el gráfico"); return; }
            _cortes.FijarAviso("corte de datos " + hora + ": en ATAS \"Reload all historical data\"");
            OlaReiniciarCargas(que);
        }

        /// <summary>Desde OnRecalculate: si la recarga la hizo La Ola hace menos de 90 s, queda una nota corta; si no, se apaga el aviso.</summary>
        private void CortesRecarga()
        {
            if (RecargaUlt > DateTime.MinValue && (DateTime.UtcNow - RecargaUlt).TotalSeconds < 90)
                _cortes.FijarNota("corte de datos " + HoraLocal(RecargaCorte) + ": gráfico recargado solo", DateTime.UtcNow, 10);
            else _cortes.Recarga();
        }

        private static string HoraLocal(DateTime utc) =>
            utc == DateTime.MinValue ? "?" : TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local).ToString("HH:mm", CultureInfo.InvariantCulture);

        /// <summary>Aprieta "Reload all historical data" de este grafico por dentro. false si no se pudo (sin comando o muy seguido).</summary>
        private bool RecargarGrafico(string motivo, DateTime corte)
        {
            try
            {
                if (RecargaUlt > DateTime.MinValue && (DateTime.UtcNow - RecargaUlt).TotalSeconds < RecargaMinEntreSeg)
                { AnotarLinea("ola: recarga del grafico NO (" + motivo + "): la anterior fue hace menos de " + RecargaMinEntreSeg + " s"); return false; }
                if (!BuscarRecarga()) { AnotarLinea("ola: recarga del grafico NO (" + motivo + "): no se encontro el comando de ATAS"); return false; }
                var disp = System.Windows.Application.Current != null ? System.Windows.Application.Current.Dispatcher : null;
                if (disp == null) { AnotarLinea("ola: recarga del grafico NO (" + motivo + "): sin el hilo de la interfaz"); return false; }
                var cmd = _recargaCmd; string ruta = _recargaRuta;
                _recargasPorInst[Inst()] = (DateTime.UtcNow, corte);
                disp.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!cmd.CanExecute(null)) { AnotarLinea("ola: recarga del grafico NO (" + motivo + "): ATAS dice que ahora no se puede (CanExecute false)"); return; }
                        cmd.Execute(null);
                        AnotarLinea("ola: recarga del grafico HECHA (" + motivo + ") con " + ruta);   // directo: la instancia la reemplaza ATAS al recargar
                    }
                    catch (Exception e) { AnotarLinea("ola: recarga del grafico fallo (" + motivo + "): " + e.GetType().Name + ": " + e.Message); }
                }));
                return true;
            }
            catch (Exception e) { Anotar(e); return false; }
        }

        /// <summary>El ICommand "ReloadDataCommand" del grafico: primero recorriendo los objetos de ATAS que cuelgan de ChartInfo (sin la
        /// interfaz); si no aparece, en el hilo de la interfaz, el DataContext de las ventanas de ATAS que tenga el comando y que apunte al
        /// MISMO ChartInfo. Se busca una sola vez; la ruta queda en el log.</summary>
        private bool BuscarRecarga()
        {
            if (_recargaBuscada) return _recargaCmd != null;
            _recargaBuscada = true;
            object raiz = null;
            try { raiz = ChartInfo; } catch { }
            if (raiz == null) { AnotarLinea("ola: comando de recarga: sin ChartInfo"); return false; }
            if (BuscarDesde(raiz, "ChartInfo", null, out var c1, out var r1, out int vistos1)) { _recargaCmd = c1; _recargaRuta = r1; AnotarLinea("ola: comando de recarga encontrado: " + r1 + " (" + vistos1 + " objetos)"); return true; }
            AnotarLinea("ola: comando de recarga: no cuelga de ChartInfo (" + vistos1 + " objetos); se busca en las ventanas");
            try
            {
                var disp = System.Windows.Application.Current != null ? System.Windows.Application.Current.Dispatcher : null;
                if (disp == null) return false;
                string inst = Inst();
                var lineas = new List<string>();
                disp.Invoke(new Action(() =>   // con tope de 8 s: el latido no se queda esperando a la interfaz
                {
                    // 1.0b (27-11, 2a prueba): ReloadDataCommand no cuelga de ChartInfo (638 objetos mirados). Se juntan TODOS los DataContext de
                    // las ventanas que lo tienen, con el titulo del panel que los contiene ("ESZ6 1m Chart", "MES 1m Chart": asi los ve UIA), y se
                    // elige el del panel cuyo titulo empieza con el instrumento de este grafico; si no, el que llega por campos a este ChartInfo.
                    var vistos = new HashSet<object>(ReferenceEqualityComparer.Instance);
                    var cands = new List<(object Dc, string Titulo, string Elem)>();
                    foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
                    {
                        var pila = new Stack<System.Windows.DependencyObject>(); pila.Push(w); int n = 0;
                        while (pila.Count > 0 && n < 60000)
                        {
                            var d = pila.Pop(); n++;
                            if (d is System.Windows.FrameworkElement fe && fe.DataContext != null && !vistos.Contains(fe.DataContext))
                            {
                                var dc = fe.DataContext;
                                var p = dc.GetType().GetProperty("ReloadDataCommand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (p != null && typeof(System.Windows.Input.ICommand).IsAssignableFrom(p.PropertyType))
                                { vistos.Add(dc); cands.Add((dc, TituloPanel(fe), fe.GetType().Name)); }
                            }
                            int k = 0; try { k = System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); } catch { }
                            for (int i = 0; i < k; i++) pila.Push(System.Windows.Media.VisualTreeHelper.GetChild(d, i));
                        }
                    }
                    foreach (var cd in cands) lineas.Add("candidato " + cd.Dc.GetType().FullName + " en " + cd.Elem + " | panel '" + cd.Titulo + "'");
                    var porTitulo = cands.FindAll(x => x.Titulo.StartsWith(inst + " ", StringComparison.OrdinalIgnoreCase));
                    (object Dc, string Titulo, string Elem) elegido = default;
                    if (porTitulo.Count == 1) elegido = porTitulo[0];
                    else foreach (var cd in cands) if (BuscarDesde(cd.Dc, cd.Dc.GetType().Name, raiz, out _, out _, out _)) { elegido = cd; break; }
                    if (elegido.Dc != null)
                    {
                        var p = elegido.Dc.GetType().GetProperty("ReloadDataCommand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        _recargaCmd = p.GetValue(elegido.Dc) as System.Windows.Input.ICommand;
                        _recargaRuta = "panel '" + elegido.Titulo + "' > " + elegido.Dc.GetType().FullName + ".ReloadDataCommand";
                    }
                }), System.Windows.Threading.DispatcherPriority.Background, System.Threading.CancellationToken.None, TimeSpan.FromSeconds(8));
                foreach (var l in lineas) AnotarLinea("ola: comando de recarga, " + l);
            }
            catch (Exception e) { Anotar(e); }
            AnotarLinea(_recargaCmd != null ? "ola: comando de recarga encontrado: " + _recargaRuta : "ola: comando de recarga NO encontrado: solo aviso en el pie");
            return _recargaCmd != null;
        }

        /// <summary>El titulo del panel que contiene al elemento (la propiedad Caption del panel de DevExpress, la que UIA muestra como
        /// "ESZ6 1m Chart"), subiendo por el arbol visual y el logico. Hilo de la interfaz.</summary>
        private static string TituloPanel(System.Windows.DependencyObject d)
        {
            for (int i = 0; i < 120 && d != null; i++)
            {
                try
                {
                    var pc = d.GetType().GetProperty("Caption", BindingFlags.Instance | BindingFlags.Public);
                    if (pc != null)
                    {
                        string s = null; try { s = pc.GetValue(d)?.ToString(); } catch { }
                        if (!string.IsNullOrWhiteSpace(s) && !s.StartsWith("System.", StringComparison.Ordinal)) return s.Trim();
                    }
                    string an = System.Windows.Automation.AutomationProperties.GetName(d);
                    if (!string.IsNullOrWhiteSpace(an) && an.IndexOf("Chart", StringComparison.OrdinalIgnoreCase) >= 0) return an.Trim();
                }
                catch { }
                System.Windows.DependencyObject padre = null;
                try { if (d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D) padre = System.Windows.Media.VisualTreeHelper.GetParent(d); } catch { }
                if (padre == null) { try { padre = System.Windows.LogicalTreeHelper.GetParent(d); } catch { } }
                d = padre;
            }
            return "";
        }

        /// <summary>Recorre (a lo ancho, profundidad 5, hasta 4000 objetos de ATAS) los campos de 'raiz'. Sin 'buscado': devuelve el primer
        /// ReloadDataCommand. Con 'buscado': true si llega a ESE objeto (para saber si un modelo de vista es el de este grafico).</summary>
        private static bool BuscarDesde(object raiz, string nombre, object buscado, out System.Windows.Input.ICommand cmd, out string ruta, out int vistos)
        {
            cmd = null; ruta = ""; vistos = 0;
            var ya = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var cola = new Queue<(object O, string R, int P)>(); cola.Enqueue((raiz, nombre, 0));
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            while (cola.Count > 0 && vistos < 4000)
            {
                var (o, r, p) = cola.Dequeue();
                if (o == null || !ya.Add(o)) continue;
                vistos++;
                if (buscado != null && ReferenceEquals(o, buscado)) { ruta = r; return true; }
                var tipo = o.GetType();
                if (buscado == null)
                {
                    PropertyInfo pi = null;
                    try { pi = tipo.GetProperty("ReloadDataCommand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); } catch { }
                    if (pi != null && typeof(System.Windows.Input.ICommand).IsAssignableFrom(pi.PropertyType))
                    {
                        System.Windows.Input.ICommand c = null;
                        try { c = pi.GetValue(o) as System.Windows.Input.ICommand; } catch { }
                        if (c != null) { cmd = c; ruta = r + ".ReloadDataCommand (" + tipo.FullName + ")"; return true; }
                    }
                }
                if (p >= 5) continue;
                for (var t = tipo; t != null && t != typeof(object); t = t.BaseType)
                {
                    FieldInfo[] fs; try { fs = t.GetFields(BF); } catch { continue; }
                    foreach (var f in fs)
                    {
                        if (f.FieldType.IsValueType || f.FieldType == typeof(string)) continue;
                        object v; try { v = f.GetValue(o); } catch { continue; }
                        if (v == null) continue;
                        string ns = v.GetType().Namespace ?? "";
                        if (!(ns.StartsWith("OFT", StringComparison.Ordinal) || ns.StartsWith("ATAS", StringComparison.Ordinal) || ns.StartsWith("Utils", StringComparison.Ordinal))) continue;
                        cola.Enqueue((v, r + "." + f.Name, p + 1));
                    }
                }
            }
            return false;
        }

        /// <summary>Para PROBAR la recarga sin tocar la pantalla: %APPDATA%\ATAS\PythiaGex\pythiaola\recargar-&lt;inst&gt;.txt (se borra al tomarlo).</summary>
        private void CortesPedidoArchivo()
        {
            try
            {
                string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex", "pythiaola", "recargar-" + Inst() + ".txt");
                if (!File.Exists(f)) return;
                File.Delete(f);
                AnotarLinea("ola: pedido de recarga por archivo (prueba)");
                _recargasPorInst.TryRemove(Inst(), out _);   // la prueba no espera los 3 minutos
                if (RecargarGrafico("prueba por archivo", DateTime.UtcNow)) _cortes.FijarAviso("prueba: recargando el gráfico");
            }
            catch (Exception e) { if (_cortesErrores++ < 5) Anotar(e); }
        }

        /// <summary>Lo nuevo del log de ATAS desde la ultima lectura. La primera vez arranca al FINAL (los cortes de antes de cargar el
        /// indicador ya los arreglo la carga); si cambia el archivo (dia nuevo), desde el principio.</summary>
        private void CortesLeerLog(DateTime ahora)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATAS", "Logs");
                string ruta = Path.Combine(dir, "app_" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
                if (!File.Exists(ruta)) return;
                using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long largo = fs.Length;
                    if (ruta != _cortesRuta) { bool primera = _cortesRuta == null; _cortesRuta = ruta; _cortesPos = primera ? largo : 0; if (primera) return; }
                    if (largo < _cortesPos) _cortesPos = 0;   // el archivo se achico: se lee de nuevo
                    if (largo == _cortesPos) return;
                    fs.Seek(_cortesPos, SeekOrigin.Begin);
                    int n = (int)Math.Min(largo - _cortesPos, 1 << 20);
                    var buf = new byte[n]; int leidos = 0;
                    while (leidos < n) { int r = fs.Read(buf, leidos, n - leidos); if (r <= 0) break; leidos += r; }
                    _cortesPos += leidos;
                    int vistos = _cortes.Log(Encoding.UTF8.GetString(buf, 0, leidos), ahora);
                    if (vistos > 0) AnotarLinea("ola: el log de ATAS anota " + vistos + " linea(s) de corte de conexion (" + DateTime.Now.ToString("HH:mm:ss", Inv) + " hora de la PC)");
                }
            }
            catch (Exception e) { if (_cortesErrores++ < 5) Anotar(e); }
        }

        /// <summary>9:30-16:00 NY de lunes a viernes: ahi un silencio de la cinta de 20 s no es mercado quieto.</summary>
        private static bool CortesEnRueda(DateTime ahoraUtc)
        {
            try
            {
                var ny = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, Ny());
                if (ny.DayOfWeek == DayOfWeek.Saturday || ny.DayOfWeek == DayOfWeek.Sunday) return false;
                var h = ny.TimeOfDay;
                return h >= new TimeSpan(9, 30, 0) && h < new TimeSpan(16, 0, 0);
            }
            catch { return false; }
        }
    }
}
