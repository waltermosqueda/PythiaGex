using System;
using System.Collections.Generic;
using System.Globalization;

namespace PythiaVwap
{
    /// <summary>Un precio de la vela con su volumen y su delta (ask - bid).</summary>
    public struct NivelPV
    {
        public decimal Precio;
        public decimal Volumen;
        public decimal Delta;
    }

    /// <summary>Todo lo que se miro para decidir, para poder auditarlo.</summary>
    public sealed class ResultadoAbsorcion
    {
        public bool Piso;               // true = se evaluo el piso; false = el techo
        public bool Dispara;
        public string Motivo = "";      // por que si o por que no, en una palabra
        public decimal Rango;           // alto - bajo, en puntos
        public int RangoTicks;
        public decimal VolVela;
        public decimal VolExt, VolResto;
        public int NivExt, NivResto;
        public decimal TipicoExt, TipicoResto;
        public double Concentracion;    // TipicoExt / TipicoResto
        public decimal DeltaExt;
        public double Cierre;           // 0 = cerro en el bajo, 1 = en el alto
        public decimal MedianaVelas;    // volumen mediano de las velas recientes

        public string Linea(CultureInfo c)
        {
            return string.Format(c,
                "{0} rango={1}tk volVela={2} medVelas={3} ext={4}/{5}niv tip={6:0.#} resto={7}/{8}niv tip={9:0.#} conc={10:0.00} deltaExt={11} cierre={12:0.00} -> {13} {14}",
                Piso ? "PISO " : "TECHO", RangoTicks, VolVela, MedianaVelas,
                VolExt, NivExt, TipicoExt, VolResto, NivResto, TipicoResto,
                Concentracion, DeltaExt, Cierre, Dispara ? "DISPARA" : "no", Motivo);
        }
    }

    /// <summary>
    /// LA REGLA DE ABSORCION, SIN ATAS ADENTRO, PARA PODER PROBARLA.
    ///
    /// EL ERROR QUE REEMPLAZA
    /// La version anterior sumaba el volumen de los niveles del extremo (con
    /// TicksExtremo = 2 son tres niveles) y lo dividia por el volumen promedio
    /// POR NIVEL de la misma vela. Una vela con el volumen repartido parejo da
    /// exactamente 3 -- tres niveles, cada uno con el promedio -- que era el
    /// umbral. O sea: cualquier pivote parejo disparaba. Medido el 2026-09-06
    /// en MES de 1 minuto: 13 flechas en 84 velas dentro de un rango de 5
    /// puntos, en la sesion mas muerta de la semana. Eso no es absorcion.
    ///
    /// LA REGLA NUEVA, EN TRES CONDICIONES
    ///  1. CONCENTRACION. El volumen promedio POR NIVEL en el extremo tiene que
    ///     ser N veces el promedio por nivel del RESTO de la vela. Una vela
    ///     pareja da 1,0 y no dispara, tenga tres o treinta niveles.
    ///  2. VELA CON CUERPO. El rango tiene que tener al menos R ticks (si no,
    ///     el "extremo" es la vela entera y no hay resto contra que comparar) y
    ///     el volumen de la vela no puede estar por debajo de la mediana de las
    ///     velas recientes: una vela flaca no absorbe nada.
    ///  3. AGRESION CONTRA EL EXTREMO Y CIERRE DEL OTRO LADO. Igual que antes:
    ///     en el piso el delta del extremo es negativo (pegaron contra la
    ///     oferta) y la vela cierra arriba; en el techo, el espejo.
    ///
    /// Cada evaluacion devuelve TODOS los numeros que miro, y el indicador los
    /// escribe en un archivo. Una flecha sin sus numeros no se puede discutir.
    /// </summary>
    public static class AbsorcionRegla
    {
        public static ResultadoAbsorcion Evaluar(
            IList<NivelPV> niveles, decimal low, decimal high, decimal close,
            bool piso, decimal tick, int ticksExtremo, int rangoMinTicks,
            double fuerza, double cierreMinimo, decimal medianaVelasRecientes)
        {
            var r = new ResultadoAbsorcion { Piso = piso, MedianaVelas = medianaVelasRecientes };
            if (tick <= 0) tick = 0.25m;
            r.Rango = high - low;
            r.RangoTicks = (int)Math.Round(r.Rango / tick);
            if (r.Rango <= 0) { r.Motivo = "sin rango"; return r; }

            decimal tol = tick * Math.Max(0, ticksExtremo);
            decimal refPrecio = piso ? low : high;
            foreach (var l in niveles)
            {
                if (l.Volumen <= 0) continue;
                r.VolVela += l.Volumen;
                if (Math.Abs(l.Precio - refPrecio) <= tol)
                {
                    r.VolExt += l.Volumen; r.NivExt++; r.DeltaExt += l.Delta;
                }
                else { r.VolResto += l.Volumen; r.NivResto++; }
            }

            if (r.NivExt == 0 || r.VolExt <= 0) { r.Motivo = "sin volumen en el extremo"; return r; }
            r.Cierre = (double)((close - low) / r.Rango);

            // 2. vela con cuerpo
            if (r.RangoTicks < rangoMinTicks) { r.Motivo = "rango chico"; return r; }
            if (r.NivResto < 2) { r.Motivo = "sin resto contra que comparar"; return r; }
            if (medianaVelasRecientes > 0 && r.VolVela < medianaVelasRecientes)
            { r.Motivo = "vela flaca"; return r; }

            // 1. concentracion, por nivel contra por nivel
            r.TipicoExt = r.VolExt / r.NivExt;
            r.TipicoResto = r.VolResto / r.NivResto;
            if (r.TipicoResto <= 0) { r.Motivo = "resto vacio"; return r; }
            r.Concentracion = (double)(r.TipicoExt / r.TipicoResto);
            if (r.Concentracion < fuerza) { r.Motivo = "sin concentracion"; return r; }

            // 3. agresion contra el extremo, cierre del otro lado
            if (piso)
            {
                if (r.DeltaExt >= 0) { r.Motivo = "no pegaron contra el piso"; return r; }
                if (r.Cierre < cierreMinimo) { r.Motivo = "no cerro arriba"; return r; }
            }
            else
            {
                if (r.DeltaExt <= 0) { r.Motivo = "no pegaron contra el techo"; return r; }
                if (1.0 - r.Cierre < cierreMinimo) { r.Motivo = "no cerro abajo"; return r; }
            }
            r.Dispara = true;
            r.Motivo = "absorcion";
            return r;
        }

        /// <summary>La regla VIEJA, solo para que la prueba demuestre el defecto.</summary>
        public static bool ReglaVieja(IList<NivelPV> niveles, decimal low, decimal high, decimal close,
                                      bool piso, decimal tick, int ticksExtremo, double fuerza, double cierreMinimo)
        {
            decimal rango = high - low; if (rango <= 0) return false;
            decimal tol = tick * ticksExtremo, refP = piso ? low : high;
            decimal volExt = 0, deltaExt = 0, volVela = 0; int n = 0;
            foreach (var l in niveles)
            {
                if (l.Volumen <= 0) continue;
                volVela += l.Volumen; n++;
                if (Math.Abs(l.Precio - refP) > tol) continue;
                volExt += l.Volumen; deltaExt += l.Delta;
            }
            if (n == 0 || volExt <= 0) return false;
            decimal tipico = volVela / n;
            if ((double)(volExt / tipico) < fuerza) return false;
            double cierre = (double)((close - low) / rango);
            if (piso) return deltaExt < 0 && cierre >= cierreMinimo;
            return deltaExt > 0 && (1.0 - cierre) >= cierreMinimo;
        }
    }
}
