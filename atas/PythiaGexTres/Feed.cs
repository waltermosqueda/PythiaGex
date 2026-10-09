using System;
using System.Collections.Generic;

namespace PythiaGexTres
{
    /// <summary>
    /// LAS FILAS DE STRIKE QUE ENTIENDE EL NUCLEO: lo MINIMO de atas/PythiaGexDos/Feed.cs.
    ///
    /// Copiado (06-10-2026) solo lo que GammaHoyNucleo necesita: Feed.Fila y Feed.Cadena
    /// con los campos que usa la cuenta (K, K0, V, OI, IV, volumen y flujo por lado;
    /// Dias, GeneradoUtc, EsFuturo, PorRazon, Escala, Apalancamiento, AlFuturo/AlLibro,
    /// Fuente, Base*, EdadMin). NO se copia el bajador de la nube, ni el archivador,
    /// ni el token de GitHub ni la CBOE local: la 3.0 arma la cadena desde
    /// CadenaApi.Filas() (la API publica de ATAS) y nada mas, por ahora.
    ///
    /// Los nombres y los tipos son los mismos que en 2.0 a proposito: el nucleo es una
    /// copia textual y tiene que compilar sin tocarlo.
    /// </summary>
    public static class Feed
    {
        public sealed class Fila
        {
            public double K;        // strike en puntos del futuro (corrido si es del trimestre que vence)
            public double K0;       // strike CRUDO del contrato cuando K lleva el spread del roll; 0 = igual a K
            public int V;           // indice del vencimiento en Cadena.Dias
            public double OiC, OiP; // interes abierto (de AYER, siempre)
            public double IvC, IvP; // volatilidad implicita (Black-76 del punto medio)
            public double VolC, VolP; // contratos operados HOY (CurrentDayTotalVolume de la API)
            public double FluC, FluP; // compras - ventas por lado agresor; la API no trae cinta: 0
            public double BidVolC, AskVolC, BidVolP, AskVolP; // contratos apoyados en las puntas (3.2.3, libro profundo)
        }

        public sealed class Cadena
        {
            public string Ts = "";
            public double SpotIdx;
            public double[] Dias = Array.Empty<double>();
            public List<Fila> Filas = new();
            public double Base, BaseCruda, BaseErrorTicks, BaseUltimaBuena, BaseUltimaBuenaEdad;
            public bool BaseConfiable;
            public double EdadMin;
            public string UltimoTrade = "";
            public double HorizonteCadena = double.NaN;
            public DateTime RecibidoUtc;
            /// <summary>Cuando se armo esta cadena (UTC): el nucleo envejece los dias desde aca.</summary>
            public DateTime GeneradoUtc;
            /// <summary>Opciones SOBRE EL FUTURO (NQ/ES por la API): strikes en precio del futuro, sin base, gamma Black-76.</summary>
            public bool EsFuturo;
            public string Fuente = "";
            // libro de un ETF dibujado por razon (no se usa en 3.0 todavia; el nucleo lo consulta)
            public bool PorRazon;
            public double Escala = 1.0;
            public string EscalaOrigen = "";
            public double Apalancamiento = 1.0;
            public double AlFuturo(double k)
            {
                if (!PorRazon) return k;
                if (Apalancamiento == 1.0 || SpotIdx <= 0 || Apalancamiento <= 0) return k * Escala;
                return Escala * (SpotIdx + (k - SpotIdx) / Apalancamiento);
            }
            public double AlLibro(double fut)
            {
                if (!PorRazon || Escala <= 0) return fut;
                if (Apalancamiento == 1.0 || SpotIdx <= 0 || Apalancamiento <= 0) return fut / Escala;
                return SpotIdx + Apalancamiento * (fut / Escala - SpotIdx);
            }
        }
    }
}
