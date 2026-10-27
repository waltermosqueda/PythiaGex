// CierresAnticipados.cs — PythiaGex 5.0.1 (27-10-2026). Sin referencias a ATAS: lo usan el modulo cboe (CboeUtil, CboeCrudo, CboeCadena,
// BajadorCboe), RelojNy (el libro ES de Rithmic, CadenaApi) y las capas 3.0 (GammaHoyTresCapas).
//
// LOS DIAS DE CIERRE ANTICIPADO. Hay dias en que la bolsa de Nueva York cierra a las 13:00 (el viernes despues de Thanksgiving, el 24-12
// cuando es habil, algunos 03-07) y las opciones que VENCEN ese dia dejan de operar y liquidan a esa hora, no a las 16:00. Hasta la 5.0.0
// el cierre estaba clavado en 16:00 en los tres lugares que fijan el vencimiento (HoraNyCboe.CierreUtc, RelojNy.Cierre y
// GammaHoyTresCapas.VencimientoYaVencido): esos dias el 0DTE seguia en el horizonte Hoy de 13:00 a 16:00 ya liquidado (la trampa de
// CLAUDE.md "siguen contando el 0DTE ya vencido": tres horas con T en el piso de 1 minuto, la gamma del strike al dinero se dispara) y a
// la mañana su T salia 3 h mas larga (a las 12:00 NY, 4 h en vez de 1 h: la gamma al dinero ~2 veces menor, raiz de 4/1).
//
// LA TABLA ES EDITABLE (abajo, Tabla): una fila por fecha de NY con la hora de NY a la que vencen, ese dia, las opciones PM de cada
// producto. Una fecha que no esta = 16:00 (como siempre). Solo ADELANTA el cierre: una hora que no este entre 09:30 y 16:00 (excluidas) o
// que no se entienda se ignora y lo dice Resumen() (va al log al arrancar). Para agregar un año: copiar una fila, cambiar fecha y horas y
// decir en la nota de donde sale; lo que no se pudo confirmar en una fuente oficial va con verificada = false y "SIN VERIFICAR" en la nota,
// con el valor mas conservador: el cierre MAS TEMPRANO (un 0DTE ya liquidado adentro del libro es peor que uno vivo afuera). Se recompila.
//
// FUENTES OFICIALES (consultadas el 27-10-2026; el detalle de cada hora va en la nota de su fila):
//   NYSE  https://www.nyse.com/markets/hours-calendars (notas de la tabla de feriados): cada mercado cierra temprano a la 1:00 p.m. (1:15 p.m.
//         las "eligible options") el viernes 27-11-2026 y el jueves 24-12-2026; tambien publica 26-11-2027, 03-07-2028 y 24-11-2028. De ese
//         cierre salen el valor de liquidacion de las SPXW (precios de cierre del dia) y el cierre de SPY.
//   Cboe  https://www.cboe.com/about/hours/us-options/ : tabla 2026, "Thanksgiving Early Close" (27-11) y "Christmas Early Close" (24-12),
//         las dos con Regular Trading Hours 09:30:00 - 13:00:00. Regla 5.1(b)(2)(C) (Exhibit 5 de SR-CBOE-2026-005, en sec.gov): el ultimo dia
//         de negociacion las P.M.-settled operan de 9:30 a 4:00 p.m. (el cierre de las acciones, no el 4:15 de las demas); la 4.13 .13 define
//         P.M.-Settled como liquidacion con los precios de cierre del dia de vencimiento.
//   CME   regla de las opciones europeas semanales sobre el S&P, leida en la presentacion CME 26-264 a la CFTC (11-06-2026, capitulo 353B de
//         las Micro, que remite a la 358A del E-mini): https://www.cftc.gov/filings/ptc/ptc0611265993.pdf. Terminan a las 3:00 p.m. "or at noon
//         in the case of an early scheduled close" de la bolsa principal, en hora de Chicago (mediodia CT = 13:00 NY), y el Fixing Price es el
//         VWAP del futuro de 11:59:30 a 12:00 en ese caso; igual de lunes a jueves (fin de mes: el mismo texto en la presentacion CME 21-344 del
//         S&P estandar, cftc.gov/sites/default/files/filings/orgrules/21/08/rule081021cmedcm002.pdf, leida). La 358A del E-mini
//         (cmegroup.com/content/dam/cmegroup/rulebook/CME/IV/350/358A/358A.pdf) y los horarios de liquidacion 2026 (equity 12:00 CT el 27-11 y
//         el 24-12: cmegroup.com/tools-information/holiday-calendar/files/2026/thanksgiving-holiday-settlement-times-2026.pdf y
//         christmas-holiday-settlement-times-2026.pdf) se vieron SOLO por el indice del buscador: cmegroup.com no deja leer (WebFetch: timeout;
//         desde la PC: 403 "IP blocked ... scraping").
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PythiaGexCinco
{
    /// <summary>5.0.1: el producto de una opcion PM que vence (cada uno puede tener su hora en un cierre anticipado).</summary>
    public enum ProductoVenc
    {
        /// <summary>SPXW de CBOE (y toda SPX que liquida con el cierre): el valor de liquidacion sale de los precios de cierre del dia.</summary>
        SpxPm,
        /// <summary>Opciones de SPY (ETF; ejercicio con el cierre de SPY).</summary>
        Spy,
        /// <summary>Opciones PM sobre el futuro ES de CME (semanales de lunes a viernes, fin de mes, mensuales): terminan 15:00 CT o al
        /// mediodia CT con cierre anticipado. La trimestral (AM, 9:30 NY) no pasa por aca.</summary>
        EsPm,
    }

    /// <summary>5.0.1 (27-10-2026): la tabla editable de cierres anticipados y la hora de vencimiento PM que sale de ella (ver cabecera).</summary>
    public static class CierresAnticipados
    {
        /// <summary>La hora de vencimiento PM de un dia normal: 16:00 NY.</summary>
        public static readonly TimeSpan Normal = new TimeSpan(16, 0, 0);
        private static readonly TimeSpan Apertura = new TimeSpan(9, 30, 0);

        /// <summary>Una fila de la tabla. Las horas son de Nueva York; null = ese producto vence a las 16:00 ese dia (o la hora no se entendio).</summary>
        public sealed class Fila
        {
            public DateTime Fecha;                         // fecha de NY, sin hora
            public TimeSpan? SpxPm, Spy, EsPm;
            public bool SpxPmVerificada, SpyVerificada, EsPmVerificada;
            public string Nota = "";
            public string Error = "";                      // lo que no se entendio al armar la fila ("" = nada)

            public TimeSpan? Hora(ProductoVenc p) => p == ProductoVenc.SpxPm ? SpxPm : p == ProductoVenc.Spy ? Spy : EsPm;
            public bool Verificada(ProductoVenc p) => p == ProductoVenc.SpxPm ? SpxPmVerificada : p == ProductoVenc.Spy ? SpyVerificada : EsPmVerificada;
        }

        private static Fila F(int anio, int mes, int dia, string spxPm, bool spxPmVerif, string spy, bool spyVerif, string esPm, bool esPmVerif, string nota)
        {
            var f = new Fila { Nota = nota ?? "", SpxPmVerificada = spxPmVerif, SpyVerificada = spyVerif, EsPmVerificada = esPmVerif };
            var err = new StringBuilder();
            try { f.Fecha = new DateTime(anio, mes, dia); }
            catch { err.Append("fecha invalida " + anio + "-" + mes + "-" + dia + "; "); f.Fecha = DateTime.MinValue; }
            TimeSpan? H(string s, string quien)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                if ((TimeSpan.TryParseExact(s.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var h) || TimeSpan.TryParseExact(s.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out h))
                    && h > Apertura && h < Normal) return h;
                err.Append(quien + " '" + s + "' no es una hora entre 09:30 y 16:00 (excluidas): se ignora; ");
                return null;
            }
            f.SpxPm = H(spxPm, "SPXW"); f.Spy = H(spy, "SPY"); f.EsPm = H(esPm, "ES");
            f.Error = err.ToString().Trim();
            return f;
        }

        // =====================================================================================================================================
        // LA TABLA (editar aca). Horas de NY ("13:00"); "" = 16:00 ese dia para ese producto. verificada = confirmada en fuente oficial.
        // =====================================================================================================================================
        public static readonly Fila[] Tabla =
        {
            //   fecha NY      SPXW PM          SPY               ES PM (CME)
            F(2026, 11, 27,    "13:00", true,   "13:00", false,   "13:00", true,
              "viernes despues de Thanksgiving. SPXW 13:00: NYSE cierra a la 1:00 p.m. y las PM liquidan con los precios de cierre (Cboe 4.13 .13); " +
              "Cboe 2026: RTH 09:30-13:00. ES 13:00: regla CME de las semanales (mediodia CT con cierre anticipado de la bolsa principal; fixing " +
              "11:59:30-12:00 CT); liquidacion de equity de CME 12:00 CT (visto por el indice del buscador). SPY 13:00 SIN VERIFICAR: el cierre " +
              "de SPY es a las 13:00 (NYSE), pero no encontre en fuente oficial si las SPY que vencen operan hasta 13:15 como las 'eligible options'; " +
              "13:00 es el valor conservador."),
            F(2026, 12, 24,    "13:00", true,   "13:00", false,   "13:00", true,
              "jueves de Nochebuena. Igual que el 27-11: NYSE 1:00 p.m., Cboe 2026 RTH 09:30-13:00, regla CME (las semanales del jueves tambien " +
              "terminan al mediodia CT); liquidacion de equity de CME 12:00 CT (visto por el indice del buscador). SPY 13:00 SIN VERIFICAR (idem)."),
            F(2027, 11, 26,    "13:00", false,  "13:00", false,   "13:00", false,
              "SIN VERIFICAR: la fecha la publica NYSE (cierre 1:00 p.m. el dia despues de Thanksgiving 2027); Cboe y CME todavia no publicaron " +
              "2027. Horas por la misma regla que 2026."),
            F(2028,  7,  3,    "13:00", false,  "13:00", false,   "13:00", false,
              "SIN VERIFICAR: la fecha la publica NYSE (lunes 03-07-2028, cierre 1:00 p.m.); Cboe y CME sin publicar 2028. Misma regla que 2026."),
            F(2028, 11, 24,    "13:00", false,  "13:00", false,   "13:00", false,
              "SIN VERIFICAR: la fecha la publica NYSE (cierre 1:00 p.m. el dia despues de Thanksgiving 2028); Cboe y CME sin publicar 2028."),
        };

        /// <summary>La fila de esa fecha de NY (la hora no importa); null si ese dia no hay cierre anticipado.</summary>
        public static Fila Buscar(DateTime fechaNy)
        {
            var d = fechaNy.Date;
            foreach (var f in Tabla) if (f.Fecha == d) return f;
            return null;
        }

        /// <summary>La hora de NY a la que vencen ESE dia las opciones PM del producto: la de la tabla o 16:00. producto null (una cadena de
        /// CBOE cuyo producto no se sabe) = la mas temprana de SPXW y SPY.</summary>
        public static TimeSpan HoraPm(DateTime fechaNy, ProductoVenc? producto)
        {
            var f = Buscar(fechaNy);
            if (f == null) return Normal;
            TimeSpan? h;
            if (producto.HasValue) h = f.Hora(producto.Value);
            else if (f.SpxPm.HasValue && f.Spy.HasValue) h = f.SpxPm.Value < f.Spy.Value ? f.SpxPm : f.Spy;
            else h = f.SpxPm ?? f.Spy;
            return h ?? Normal;
        }

        /// <summary>true si ese dia el producto vence antes de las 16:00 (con la hora).</summary>
        public static bool Anticipado(DateTime fechaNy, ProductoVenc? producto, out TimeSpan hora)
        {
            hora = HoraPm(fechaNy, producto);
            return hora < Normal;
        }

        /// <summary>Para los logs: "13:00 NY (cierre anticipado 2026-11-27, verificada)" / "... SIN VERIFICAR"; "" en un dia normal.</summary>
        public static string Describir(DateTime fechaNy, ProductoVenc? producto)
        {
            if (!Anticipado(fechaNy, producto, out var h)) return "";
            var f = Buscar(fechaNy);
            bool verif = producto.HasValue ? f.Verificada(producto.Value) : f.SpxPmVerificada && f.SpyVerificada;
            return h.ToString(@"h\:mm", CultureInfo.InvariantCulture) + " NY (cierre anticipado " + fechaNy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                   + (verif ? ", verificada" : ", SIN VERIFICAR") + ")";
        }

        /// <summary>Una linea para el log de arranque: la tabla entera (fecha SPXW/SPY/ES, * = sin verificar) y lo que no se entendio.</summary>
        public static string Resumen()
        {
            var inv = CultureInfo.InvariantCulture;
            var partes = new List<string>(); var errores = new List<string>();
            string T(TimeSpan? h, bool v) => (h.HasValue ? h.Value.ToString(@"h\:mm", inv) : "16:00") + (h.HasValue && !v ? "*" : "");
            foreach (var f in Tabla)
            {
                partes.Add((f.Fecha == DateTime.MinValue ? "?" : f.Fecha.ToString("yyyy-MM-dd", inv)) + " " + T(f.SpxPm, f.SpxPmVerificada) + "/" + T(f.Spy, f.SpyVerificada) + "/" + T(f.EsPm, f.EsPmVerificada));
                if (!string.IsNullOrEmpty(f.Error)) errores.Add(f.Error);
            }
            return "cierres anticipados (hora NY de vencimiento SPXW/SPY/ES, * = SIN VERIFICAR): " + string.Join(", ", partes)
                   + (errores.Count > 0 ? " | FILAS CON ERROR: " + string.Join(" ", errores) : "");
        }
    }
}
