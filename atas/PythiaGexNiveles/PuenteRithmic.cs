using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using ATAS.DataFeedsCore;

namespace PythiaGex
{
    /// <summary>
    /// PUENTE a las opciones de Rithmic en ATAS 8.0.14.399.
    ///
    /// Medido el 2026-09-11 descompilando OFT.Rithmic.dll de la .399: los dos metodos publicos
    /// del conector, GetOptionSeriesAsync y GetOptionsAsync, quedaron reducidos a un aviso
    /// ("Options are not available in the current version, the option series request ... is
    /// ignored", esta en %APPDATA%\ATAS\Logs\app_*.log) y una lista vacia. Pero TODA la
    /// maquinaria privada que los hacia funcionar sigue adentro del ensamblado:
    ///
    ///   - el comando que llama a REngine.getInstrumentByUnderlying(subyacente, bolsa,
    ///     vencimiento, contexto) en el hilo del motor de Rithmic;
    ///   - los dos contextos (struct) que viajan en el pedido: (Security, long,
    ///     TaskCompletionSource de IEnumerable de OptionSeries) para las SERIES y
    ///     (OptionSeries, long, TaskCompletionSource de IEnumerable de Security) para los CONTRATOS;
    ///   - el manejador de la respuesta, que arma las OptionSeries (o registra los Security de
    ///     cada contrato con ProcessSecurity) y completa el TaskCompletionSource.
    ///
    /// Este puente rehace exactamente lo que hacian los dos metodos publicos: arma el
    /// contexto, encola el pedido y espera la respuesta. No toca nada del conector.
    ///
    /// TODO SE BUSCA POR FORMA, NUNCA POR NOMBRE: los nombres estan ofuscados y cambian con
    /// cada version de ATAS. El comando se reconoce porque su codigo IL llama a
    /// getInstrumentByUnderlying; los contextos, por la firma de su constructor. Si alguna
    /// pieza no aparece, se dice en el log y se devuelve vacio: nunca se inventa nada.
    /// </summary>
    internal static class PuenteRithmic
    {
        internal sealed class Mapa
        {
            public FieldInfo CampoWrapper;      // campo del conector con el envoltorio del REngine
            public MethodInfo Pedido;           // wrapper.X(string subyacente, string bolsa, string vencimiento, object contexto)
            public FieldInfo CampoEngine;       // alternativa: el REngine directo dentro del wrapper
            public MethodInfo PedidoDirecto;    // REngine.getInstrumentByUnderlying(...)
            public ConstructorInfo CtxSeries;   // (Security, long, TCS<IEnumerable<OptionSeries>>)
            public ConstructorInfo CtxOpciones; // (OptionSeries, long, TCS<IEnumerable<Security>>)
            public string Resumen = "";
            public bool Sirve => CtxSeries != null && CtxOpciones != null && (Pedido != null || PedidoDirecto != null);
        }

        const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Dictionary<Type, Mapa> _mapas = new();
        static readonly object _candado = new();
        static long _id = (DateTime.UtcNow.Ticks / 10000) % 900000000L;
        public const int EsperaMs = 25000;

        /// <summary>Reconoce las piezas privadas del conector. Se cachea por tipo.</summary>
        public static Mapa Mapear(object conn, Action<string> log)
        {
            if (conn == null) return null;
            var t = conn.GetType();
            lock (_candado) { if (_mapas.TryGetValue(t, out var listo)) return listo; }
            var m = new Mapa();
            var notas = new List<string>();
            try
            {
                // 1) los contextos, por la firma del constructor
                foreach (var tipo in TiposDe(t.Assembly))
                {
                    if (!tipo.IsValueType) continue;
                    foreach (var c in tipo.GetConstructors(TODO))
                    {
                        var ps = c.GetParameters();
                        if (ps.Length != 3 || ps[1].ParameterType != typeof(long)) continue;
                        if (ps[0].ParameterType == typeof(Security) && ps[2].ParameterType == typeof(TaskCompletionSource<IEnumerable<OptionSeries>>)) m.CtxSeries = c;
                        if (ps[0].ParameterType == typeof(OptionSeries) && ps[2].ParameterType == typeof(TaskCompletionSource<IEnumerable<Security>>)) m.CtxOpciones = c;
                    }
                }
                notas.Add("ctxSeries=" + (m.CtxSeries != null ? m.CtxSeries.DeclaringType.Name : "NO") + " ctxOpciones=" + (m.CtxOpciones != null ? m.CtxOpciones.DeclaringType.Name : "NO"));

                // 2) el envoltorio del motor y su metodo de pedido: un campo del conector cuyo tipo tiene un
                //    metodo (string,string,string,object) que construye un comando cuyo IL llama a
                //    getInstrumentByUnderlying
                foreach (var f in CamposDe(t))
                {
                    var ft = f.FieldType;
                    if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft.Assembly != t.Assembly) continue;
                    foreach (var met in ft.GetMethods(TODO))
                    {
                        var ps = met.GetParameters();
                        if (ps.Length != 4 || ps[0].ParameterType != typeof(string) || ps[1].ParameterType != typeof(string)
                            || ps[2].ParameterType != typeof(string) || ps[3].ParameterType != typeof(object)) continue;
                        foreach (var llamada in Llamadas(met).OfType<ConstructorInfo>())
                        {
                            var tc = llamada.DeclaringType;
                            if (tc == null || tc.Assembly != t.Assembly) continue;
                            bool usa = false;
                            foreach (var mm in tc.GetMethods(TODO))
                                if (Llamadas(mm).Any(x => x.Name == "getInstrumentByUnderlying")) { usa = true; break; }
                            if (usa) { m.CampoWrapper = f; m.Pedido = met; break; }
                        }
                        if (m.Pedido != null) break;
                    }
                    if (m.Pedido != null) break;
                }
                notas.Add("pedido=" + (m.Pedido != null ? m.CampoWrapper.FieldType.Name + "." + m.Pedido.Name : "NO"));

                // 3) alternativa: el REngine directo (si el metodo del envoltorio no aparece)
                if (m.Pedido == null)
                {
                    foreach (var f in CamposDe(t))
                    {
                        if (f.FieldType.Assembly == t.Assembly)
                            foreach (var g in CamposDe(f.FieldType))
                                if (g.FieldType.Name == "REngine") { m.CampoWrapper = f; m.CampoEngine = g; break; }
                        if (m.CampoEngine != null) break;
                    }
                    if (m.CampoEngine != null)
                        m.PedidoDirecto = m.CampoEngine.FieldType.GetMethod("getInstrumentByUnderlying", new[] { typeof(string), typeof(string), typeof(string), typeof(object) });
                    notas.Add("directo=" + (m.PedidoDirecto != null ? "REngine.getInstrumentByUnderlying" : "NO"));
                }
            }
            catch (Exception e) { notas.Add("mapeo fallo: " + e.GetType().Name + " " + e.Message); }
            m.Resumen = string.Join(" · ", notas);
            log?.Invoke("[puente] " + (m.Sirve ? "piezas encontradas: " : "FALTAN PIEZAS: ") + m.Resumen);
            lock (_candado) _mapas[t] = m;
            return m;
        }

        /// <summary>Las series (vencimientos) de un futuro, como las daba GetOptionSeriesAsync.</summary>
        public static async Task<List<OptionSeries>> SeriesAsync(object conn, Security fut, Action<string> log)
        {
            var m = Mapear(conn, log);
            if (m == null || !m.Sirve || fut == null) return new List<OptionSeries>();
            var (codigo, bolsa) = Partes(fut);
            foreach (var sub in new[] { codigo, Raiz(codigo) }.Where(s => !string.IsNullOrEmpty(s)).Distinct())
            {
                var tcs = new TaskCompletionSource<IEnumerable<OptionSeries>>(TaskCreationOptions.RunContinuationsAsynchronously);
                object ctx;
                try { ctx = m.CtxSeries.Invoke(new object[] { fut, Interlocked.Increment(ref _id), tcs }); }
                catch (Exception e) { log?.Invoke("[puente] no pude armar el contexto de series: " + e.Message); return new List<OptionSeries>(); }
                if (!Pedir(conn, m, sub, bolsa, "", ctx, log)) return new List<OptionSeries>();
                try
                {
                    var listo = await Task.WhenAny(tcs.Task, Task.Delay(EsperaMs)).ConfigureAwait(false);
                    if (listo != tcs.Task) { log?.Invoke("[puente] Rithmic no contesto en " + (EsperaMs / 1000) + " s las series de " + sub + "@" + bolsa); continue; }
                    var lista = (await tcs.Task.ConfigureAwait(false) ?? Enumerable.Empty<OptionSeries>()).ToList();
                    log?.Invoke("[puente] " + lista.Count + " series para " + sub + "@" + bolsa
                        + (lista.Count > 0 ? " (" + string.Join(", ", lista.Take(6).Select(s => s.Expiration.ToString("MM-dd") + " " + s.Type)) + (lista.Count > 6 ? ", ..." : "") + ")" : ""));
                    if (lista.Count > 0) return lista;
                }
                catch (Exception e) { log?.Invoke("[puente] Rithmic rechazo las series de " + sub + "@" + bolsa + ": " + e.Message); }
            }
            return new List<OptionSeries>();
        }

        /// <summary>Los contratos de una serie, como los daba GetOptionsAsync (quedan registrados
        /// en el catalogo del conector, asi que despues se pueden suscribir igual que antes).</summary>
        public static async Task<List<Security>> OpcionesAsync(object conn, OptionSeries serie, Action<string> log)
        {
            var m = Mapear(conn, log);
            if (m == null || !m.Sirve) return new List<Security>();
            string sub = serie.UnderlyingCode ?? "", bolsa = serie.Exchange ?? "CME";
            // 2026-09-11: la serie del 0DTE de NQ se perdio a las 10:16 ET porque el primer pedido
            // (fecha exacta) no contesto en 25 s y el segundo (solo mes) vino "no data"; la cadena
            // siguio sin el vencimiento mas importante del dia. Se pide la fecha exacta DOS veces
            // antes de caer al mes.
            string vencDia = serie.Expiration.ToString("yyyyMMdd", CultureInfo.InvariantCulture), vencMes = serie.Expiration.ToString("yyyyMM", CultureInfo.InvariantCulture);
            int intento = 0;
            foreach (var venc in new[] { vencDia, vencDia, vencMes })
            {
                intento++;
                var tcs = new TaskCompletionSource<IEnumerable<Security>>(TaskCreationOptions.RunContinuationsAsynchronously);
                object ctx;
                try { ctx = m.CtxOpciones.Invoke(new object[] { serie, Interlocked.Increment(ref _id), tcs }); }
                catch (Exception e) { log?.Invoke("[puente] no pude armar el contexto de contratos: " + e.Message); return new List<Security>(); }
                if (!Pedir(conn, m, sub, bolsa, venc, ctx, log)) return new List<Security>();
                try
                {
                    var listo = await Task.WhenAny(tcs.Task, Task.Delay(EsperaMs)).ConfigureAwait(false);
                    if (listo != tcs.Task) { log?.Invoke("[puente] Rithmic no contesto en " + (EsperaMs / 1000) + " s los contratos de " + serie.Code + " (" + venc + ", intento " + intento + ")"); continue; }
                    var lista = (await tcs.Task.ConfigureAwait(false) ?? Enumerable.Empty<Security>()).ToList();
                    if (lista.Count > 0) return lista;
                    log?.Invoke("[puente] 0 contratos para " + serie.Code + " con vencimiento " + venc);
                }
                catch (Exception e) { log?.Invoke("[puente] Rithmic rechazo los contratos de " + serie.Code + " (" + venc + "): " + e.Message); }
            }
            return new List<Security>();
        }

        static bool Pedir(object conn, Mapa m, string sub, string bolsa, string venc, object ctx, Action<string> log)
        {
            try
            {
                var wrapper = m.CampoWrapper?.GetValue(conn);
                if (wrapper == null) { log?.Invoke("[puente] el envoltorio del motor de Rithmic esta vacio (desconectado?)"); return false; }
                if (m.Pedido != null) { m.Pedido.Invoke(wrapper, new object[] { sub, bolsa, venc, ctx }); return true; }
                var engine = m.CampoEngine?.GetValue(wrapper);
                if (engine == null) { log?.Invoke("[puente] el REngine esta vacio"); return false; }
                m.PedidoDirecto.Invoke(engine, new object[] { sub, bolsa, venc, ctx });
                return true;
            }
            catch (Exception e) { log?.Invoke("[puente] el pedido a Rithmic fallo: " + (e.InnerException ?? e).Message); return false; }
        }

        static (string, string) Partes(Security s)
        {
            var id = s.SecurityId ?? "";
            var i = id.IndexOf('@');
            if (i > 0 && i < id.Length - 1) return (id.Substring(0, i), id.Substring(i + 1));
            return (s.Code ?? "", "CME");
        }

        static string Raiz(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return codigo;
            int n = codigo.Length;
            while (n > 0 && char.IsDigit(codigo[n - 1])) n--;
            if (n > 1 && n < codigo.Length && char.IsLetter(codigo[n - 1])) n--;
            return codigo.Substring(0, Math.Max(1, n));
        }

        static IEnumerable<Type> TiposDe(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null); }
        }

        static IEnumerable<FieldInfo> CamposDe(Type t)
        {
            for (var b = t; b != null && b != typeof(object); b = b.BaseType)
                foreach (var f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    yield return f;
        }

        /// <summary>Los metodos y constructores que un metodo llama, leidos del IL (call, callvirt,
        /// newobj). Lectura tolerante: un token que no resuelve se ignora.</summary>
        static IEnumerable<MethodBase> Llamadas(MethodBase met)
        {
            byte[] il = null;
            try { il = met.GetMethodBody()?.GetILAsByteArray(); } catch { }
            if (il == null) yield break;
            var mod = met.Module;
            Type[] gt = null, gm = null;
            try { gt = met.DeclaringType != null && met.DeclaringType.IsGenericType ? met.DeclaringType.GetGenericArguments() : null; } catch { }
            try { gm = met.IsGenericMethod ? met.GetGenericArguments() : null; } catch { }
            for (int i = 0; i < il.Length - 4; i++)
            {
                byte op = il[i];
                if (op != 0x28 && op != 0x6F && op != 0x73) continue;
                int tok = BitConverter.ToInt32(il, i + 1);
                int tabla = (tok >> 24) & 0xFF;
                if (tabla != 0x06 && tabla != 0x0A && tabla != 0x2B) continue; // MethodDef, MemberRef, MethodSpec
                MethodBase r = null;
                try { r = mod.ResolveMethod(tok, gt, gm); } catch { }
                if (r != null) { yield return r; i += 4; }
            }
        }
    }
}
