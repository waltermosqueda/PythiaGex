using System;
using System.Globalization;

namespace PythiaLaOla
{
    /// <summary>
    /// LA OLA 1.0b (27-11-2026): CORTES DE DATOS. Medido ese dia contra la cinta de la sonda: tras el corte de Rithmic de las 12:47:17 AR
    /// (las dos conexiones a la vez, otra vez 12:50:50 y 13:06:17), ATAS NO repuso las ordenes del hueco: ni en OnCumulativeTrade (La Ola
    /// perdio 533 ordenes en el primero y 965 en los tres) ni en su propio grafico (la vela de las 12:46 quedo con 2.483 de 2.873 contratos y
    /// maximo 7.292,00 en vez de 7.293,50) hasta que el grafico se recargo (13:37). La ayuda de ATAS dice lo mismo: despues de un corte,
    /// "Reload Data" en cada grafico.
    /// Este detector (SIN ATAS: lo prueba el arnes atas/_test_pythiaola, modo "cortes") decide CUANDO volver a pedir la jornada al historial:
    ///  - corte: una linea "Market Data Connection Broken" o "Connection lost" en el log de ATAS, o (solo en la rueda 9:30-16:00 NY, con la
    ///    carga viva recibiendo) un silencio de la cinta en vivo de SilencioSeg o mas que despues se corta;
    ///  - se re-pide cuando la cinta en vivo VOLVIO (una orden posterior al corte + 3 s, con ReanudoSeg de margen para que el historial de
    ///    ATAS ya tenga el hueco), pasaron EsperaSeg desde el corte y MinEntreSeg desde el re-pedido anterior;
    ///  - el aviso del pie del panel queda hasta la proxima recarga del grafico (Recarga, desde OnRecalculate) o AvisoMin minutos.
    /// Lo que hace el indicador al re-pedir es lo mismo que al cambiar la X fija (GestorOla.ReiniciarCargas): tira las cargas y las pide de
    /// nuevo; el motor, la costura y el bufer no cambian.
    /// </summary>
    public sealed class DetectorCortes
    {
        public double SilencioSeg = 20, EsperaSeg = 10, ReanudoSeg = 5, MinEntreSeg = 120, AvisoMin = 30;

        public DateTime Pendiente { get; private set; } = DateTime.MinValue;    // UTC del corte que todavia no se repuso
        public string MotivoPendiente { get; private set; } = "";
        public DateTime UltimoCorte { get; private set; } = DateTime.MinValue;  // el que disparo el ultimo re-pedido
        public string UltimoMotivo { get; private set; } = "";
        public DateTime UltimoRepedido { get; private set; } = DateTime.MinValue;
        public DateTime UltimoEvento { get; private set; } = DateTime.MinValue;
        public int Vistos { get; private set; }
        public int Repedidos { get; private set; }
        public string Aviso { get; private set; } = "";
        public DateTime AvisoDesde { get; private set; } = DateTime.MinValue;

        private string _resto = "";
        private bool _enSilencio; private DateTime _silencioDesde = DateTime.MinValue;
        private DateTime _reanudo = DateTime.MinValue;   // primera orden en vivo despues de Pendiente + 3 s

        public static bool EsCorte(string linea) =>
            linea != null && (linea.IndexOf("Market Data Connection Broken", StringComparison.Ordinal) >= 0
                           || linea.IndexOf("Connection lost", StringComparison.Ordinal) >= 0);

        /// <summary>Texto NUEVO del log de ATAS. Puede traer media linea al final: se guarda para la proxima. Devuelve cuantos cortes vio.</summary>
        public int Log(string texto, DateTime ahoraUtc)
        {
            if (string.IsNullOrEmpty(texto)) return 0;
            string t = _resto + texto;
            int ult = t.LastIndexOf('\n');
            if (ult < 0) { _resto = t.Length > 4096 ? t.Substring(t.Length - 4096) : t; return 0; }
            _resto = t.Substring(ult + 1);
            if (_resto.Length > 4096) _resto = _resto.Substring(_resto.Length - 4096);
            int n = 0;
            foreach (var l in t.Substring(0, ult).Split('\n'))
                if (EsCorte(l)) { Marcar(ahoraUtc, "log de ATAS"); n++; }
            return n;
        }

        /// <summary>Llego (o crecio) una orden en vivo; ahoraUtc = hora de la PC al recibirla.</summary>
        public void Evento(DateTime ahoraUtc)
        {
            if (ahoraUtc <= UltimoEvento) return;
            UltimoEvento = ahoraUtc;
            if (Pendiente > DateTime.MinValue && _reanudo == DateTime.MinValue && ahoraUtc > Pendiente.AddSeconds(3)) _reanudo = ahoraUtc;
        }

        /// <summary>OnRecalculate: el grafico se recargo (con "Reload Data" ATAS repone el hueco): se apaga el aviso.</summary>
        public void Recarga() { Aviso = ""; AvisoDesde = DateTime.MinValue; }

        /// <summary>Cada latido. enRueda: 9:30-16:00 NY de un dia habil (el silencio solo cuenta ahi); hayVivo: hay carga viva recibiendo la
        /// cinta. true = re-pedir la jornada AHORA (UltimoCorte / UltimoMotivo dicen por que).</summary>
        public bool Revisar(DateTime ahoraUtc, bool enRueda, bool hayVivo)
        {
            // silencio de la cinta en vivo (respaldo por si el log no esta o cambia de formato)
            if (!enRueda || !hayVivo) _enSilencio = false;
            else if (!_enSilencio)
            {
                if (UltimoEvento > DateTime.MinValue && (ahoraUtc - UltimoEvento).TotalSeconds >= SilencioSeg) { _enSilencio = true; _silencioDesde = UltimoEvento; }
            }
            else if (UltimoEvento > _silencioDesde)
            {
                _enSilencio = false;
                Marcar(_silencioDesde, "silencio de " + (UltimoEvento - _silencioDesde).TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s");
                if (_reanudo == DateTime.MinValue && UltimoEvento > Pendiente.AddSeconds(3)) _reanudo = UltimoEvento;
            }
            // el aviso vence
            if (Aviso.Length > 0 && (ahoraUtc - AvisoDesde).TotalMinutes >= AvisoMin) Recarga();
            // re-pedir
            if (Pendiente == DateTime.MinValue || _reanudo == DateTime.MinValue) return false;
            if ((ahoraUtc - _reanudo).TotalSeconds < ReanudoSeg || (ahoraUtc - Pendiente).TotalSeconds < EsperaSeg) return false;
            if (UltimoRepedido > DateTime.MinValue && (ahoraUtc - UltimoRepedido).TotalSeconds < MinEntreSeg) return false;
            UltimoCorte = Pendiente; UltimoMotivo = MotivoPendiente;
            UltimoRepedido = ahoraUtc; Repedidos++;
            AvisoDesde = ahoraUtc;
            Pendiente = DateTime.MinValue; MotivoPendiente = ""; _reanudo = DateTime.MinValue;
            return true;
        }

        /// <summary>El texto del aviso con la hora del corte en la zona local de la PC (la del panel).</summary>
        public void FijarAviso(string texto) { Aviso = texto ?? ""; }

        /// <summary>Una nota corta en el pie (por ejemplo "grafico recargado solo") que vence en 'minutos' (lo controla Revisar).</summary>
        public void FijarNota(string texto, DateTime ahoraUtc, double minutos)
        {
            Aviso = texto ?? ""; AvisoDesde = ahoraUtc.AddMinutes(minutos - AvisoMin);   // vence a los 'minutos' con la misma cuenta de AvisoMin
        }

        private void Marcar(DateTime t, string motivo)
        {
            Vistos++;
            if (Pendiente == DateTime.MinValue || t < Pendiente)
            {
                // un corte mas viejo que el pendiente lo reemplaza (la reanudacion vista despues del nuevo sigue valiendo para el viejo)
                Pendiente = t; MotivoPendiente = motivo;
                if (_reanudo != DateTime.MinValue && _reanudo <= t.AddSeconds(3)) _reanudo = DateTime.MinValue;
            }
        }
    }
}
