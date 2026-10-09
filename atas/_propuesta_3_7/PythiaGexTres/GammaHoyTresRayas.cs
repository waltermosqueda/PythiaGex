using System;
using System.ComponentModel.DataAnnotations;

namespace PythiaGexTres
{
    /// <summary>
    /// 3.5.0 (pedido del operador 08-10 00:00 ART): "poder desactivar una por una, aunque sea de la misma familia, hasta ver el patron que nos
    /// sirva; no a lo bruto toda la familia". Cada raya que dibuja la 3.0 tiene su casilla. Medido el 07-10 con su criterio visual
    /// (laboratorio/tres/rayas_hoy.py, resultados/rayas_hoy_2026-10-07.md): de noche la mejor fue QQQ D1 (18 de 23, mecha a 0,7 pt), NDX D2 y
    /// QQQ D2 no tuvieron toques y de dia la mecha las paso 6-9 pts: por eso arrancan apagadas. Nombres nuevos: el .ws no los pisa.
    /// </summary>
    public partial class GammaHoyTres
    {
        [Display(Name = "NQ D1", GroupName = "7. Rayas una por una", Order = 1)] public bool Raya3NqD1 { get; set; } = true;
        [Display(Name = "NQ D2", GroupName = "7. Rayas una por una", Order = 2)] public bool Raya3NqD2 { get; set; } = true;
        [Display(Name = "NQ cruce mas cercano (rombos del medio, antes '0Γ')", GroupName = "7. Rayas una por una", Order = 3,
                 Description = "3.7.0 (D4, C8): APAGADO por defecto y nombre nuevo (Raya3NqZeroB) para pisar el 'true' guardado en el .ws. Es el cruce de signo del perfil por strike MAS CERCANO al precio (sin islas desde la 3.7.0), no el zero gamma estandar: se pega al precio y salta al cruce vecino (CLAUDE.md, 07-10). Se rotula 'NQ cruce'.")]
        public bool Raya3NqZeroB { get; set; } = false;
        [Display(Name = "NQ M+ vol (major positivo por volumen)", GroupName = "7. Rayas una por una", Order = 4,
                 Description = "3.7.1: APAGADO por defecto y nombre nuevo (Raya3NqMasVolB) para pisar el 'true' guardado en el .ws. Era SOLO un rotulo (no tiene estela por vela): un numero sin raya en el grafico. Prendido: rotulo 'NQ M+vol' (con 'Rayas largas', la linea).")]
        public bool Raya3NqMasVolB { get; set; } = false;
        [Display(Name = "NQ M− vol (major negativo por volumen)", GroupName = "7. Rayas una por una", Order = 5, Description = "3.7.1: idem (Raya3NqMenosVolB), apagado por defecto.")]
        public bool Raya3NqMenosVolB { get; set; } = false;
        [Display(Name = "NDX D1", GroupName = "7. Rayas una por una", Order = 10)] public bool Raya3NdxD1 { get; set; } = true;
        [Display(Name = "NDX D2", GroupName = "7. Rayas una por una", Order = 11, Description = "3.7.0 (D3): PRENDIDA por defecto y nombre nuevo (Raya3NdxD2b). Las capas dibujan una por lado (D1/D2 = la mas cercana / la otra pared): con D2 apagada el tunel de la capa no se veia (cod_14: D1 y D2 a lados opuestos en 63-79 % de las lineas). Ya no hay D3.")] public bool Raya3NdxD2b { get; set; } = true;
        [Display(Name = "NDX cruce (la fila de rombos de la 2.0)", GroupName = "7. Rayas una por una", Order = 13, Description = "3.7.3: APAGADO por defecto (nombre nuevo Raya3NdxZeroE; la 3.7.2 lo prendia como techo y piso). Pedido del orquestador (vuelta 3): cerco y cruces de NDX apagados, quedan como opcion. Medido en la 3.7.2 (v372.md): era lo que mas ruido metia (11,1 rayas en el cuerpo cada 100 velas) y, contra un placebo que sigue al precio, no le ganaba (p = 0,19, revisor escéptico): es la 'sombra del precio' de CLAUDE.md. Prendido: el cruce mas cercano (o techo y piso, con la casilla de abajo).")] public bool Raya3NdxZeroE { get; set; } = false;   // 3.7.3: APAGADO y renombrado (3.7.2: Raya3NdxZeroD = true; 3.7.1: Raya3NdxZeroC = false; 3.6.9: Raya3NdxZeroB)
        [Display(Name = "NDX zero gamma con enfasis (rombos grandes)", GroupName = "7. Rayas una por una", Order = 14, Description = "3.7.3: apagado por defecto (nombre nuevo Raya3NdxZeroEnfasisB; el .ws del operador ya lo tenia en false). Prendido, los rombos de NDX van hasta 6 px con alfa 255, mas gruesos que la D1 de NQ.")] public bool Raya3NdxZeroEnfasisB { get; set; } = false;   // 3.7.3: renombrado (antes Raya3NdxZeroEnfasis = true por defecto)
        [Display(Name = "NDX cruce: techo y piso (los dos cruces que encierran al precio, como la 2.0)", GroupName = "7. Rayas una por una", Order = 15,
                 Description = "3.6.8. La 2.0 dibuja el zero de NDX como el cruce MAS CERCANO, y como salta entre el de arriba y el de abajo segun la mitad del rango, en su grafico quedan las dos filas (08-10: 31.375,43 y 31.350,73). Prendido: la 3.0 dibuja los dos cruces de signo por volumen que encierran al precio en cada vela (techo y piso). 3.7.1: apagado por defecto (Raya3NdxZeroCercoB). 3.7.2: prendido (Raya3NdxZeroCercoC). 3.7.3: APAGADO por defecto (Raya3NdxZeroCercoD), pedido del orquestador; solo cuenta con 'NDX cruce' prendido.")]
        public bool Raya3NdxZeroCercoD { get; set; } = false;   // 3.7.3: APAGADO y renombrado (3.7.2: Raya3NdxZeroCercoC = true; el .ws tiene Raya3NdxZeroCerco = true)
        [Display(Name = "QQQ D1", GroupName = "7. Rayas una por una", Order = 20, Description = "3.7.3: PRENDIDA por defecto y nombre nuevo (Raya3QqqD1c). Pedido del orquestador (vuelta 3): QQQ D1/D2 y M± OI quedan. La 3.7.1/3.7.2 apagaba la capa QQQ entera para bajar ruido (ablacion_371.md), y el escéptico de la vuelta 2 midio que 'elegida + QQQ D1' era la UNICA configuracion con el rebote de dominantes entero arriba de la 2.0 (IC +0,6; 8,3). D1/D2 = la regla de la 2.0 (las dos de mayor |GEX vol|).")] public bool Raya3QqqD1c { get; set; } = true;   // 3.7.3: PRENDIDA y renombrada (3.7.1/3.7.2: Raya3QqqD1b = false)
        [Display(Name = "QQQ D2", GroupName = "7. Rayas una por una", Order = 21, Description = "3.7.3: PRENDIDA por defecto y nombre nuevo (Raya3QqqD2d), como QQQ D1.")] public bool Raya3QqqD2d { get; set; } = true;   // 3.7.3: PRENDIDA y renombrada (3.7.1/3.7.2: Raya3QqqD2c = false)
        [Display(Name = "QQQ cruce mas cercano (antes 'QQQ 0Γ')", GroupName = "7. Rayas una por una", Order = 23, Description = "El cruce de signo de QQQ por volumen mas cercano al precio (sin islas desde la 3.7.0). 3.7.0 C8: se rotula 'QQQ cruce'. 3.7.1: APAGADO por defecto y nombre nuevo (Raya3QqqZeroB) para pisar el 'true' del .ws (ruido medido en cara_a_cara). Queda como opcion.")] public bool Raya3QqqZeroB { get; set; } = false;
        [Display(Name = "QQQ zero: con enfasis (rombo grande + linea al eje)", GroupName = "7. Rayas una por una", Order = 24)] public bool Raya3QqqZeroEnfasis { get; set; } = true;
        [Display(Name = "Cruces del zero sin cortes (completa huecos de hasta 3 velas)", GroupName = "7. Rayas una por una", Order = 30,
                 Description = "Los cruces de signo aparecen y desaparecen de una vela a otra (raya intermitente, p. ej. 31.380 el 07-10). Prendido: si un cruce estaba antes y vuelve dentro de 3 velas, se dibuja tambien en el hueco.")]
        public bool Cruces3SinCortes { get; set; } = true;

        [Display(Name = "Todas independientes: sin fusionar, sin esconder, sin promediar", GroupName = "7. Rayas una por una", Order = 40,
                 Description = "3.5.0 (pedido 08-10): ninguna dominante se fusiona ni se esconde detras de otra. Rotulos de capa aunque coincidan con D1/D2 de NQ (sin 'NQ·QQQ D1') y D1/D2 en el STRIKE EXACTO (sin el centroide que promediaba strikes cercanos en uno, tambien en la histeresis y en el rebobinado). Apagado = como 3.4.1. 3.7.1 (A5): el 'Tope de rotulos' se respeta SIEMPRE, despues de agrupar por (libro, strike) y por jerarquia.")]
        public bool Rayas3Independientes { get; set; } = true;

        [Display(Name = "Tunel sombreado (franja entre D1 y D2)", GroupName = "7. Rayas una por una", Order = 50, Description = "3.5.5: apagado por defecto (pedido 08-10).")]
        public bool Tunel3Sombreado { get; set; } = false;

        [Display(Name = "Rayas largas (hasta el eje o de lado a lado)", GroupName = "7. Rayas una por una", Order = 51,
                 Description = "3.5.5: apagado por defecto (pedido 08-10: 'mas natural'). Prendido: majors y zero de lado a lado, y de la ultima vela al eje las capas, el APOYO, los cruces por OI y el zero de QQQ. Apagado: quedan las estelas por vela y los rotulos.")]
        public bool Rayas3Largas { get; set; } = false;

        [Display(Name = "Rotulos cortos (libro, tipo y precio)", GroupName = "7. Rayas una por una", Order = 52,
                 Description = "3.5.5: sin flecha, sin distancia y sin la descripcion; la edad de las capas de CBOE solo si pasa de 30 min (p. ej. ·7h).")]
        public bool Rotulos3Cortos { get; set; } = true;

        [Display(Name = "Rotulos: tamaño (%)", GroupName = "7. Rayas una por una", Order = 53, Description = "3.7.0 (D5): 100 por defecto, como lo dejo el operador en el .ws.")]
        [Range(50, 120)]
        public int Rotulos3TamPct { get; set; } = 100;

        [Display(Name = "Rotulos: rayitas que los unen a su precio", GroupName = "7. Rayas una por una", Order = 54, Description = "3.5.6: apagadas por defecto (pedido 08-10: 'quedaron sueltas').")]
        public bool Rotulos3Conectores { get; set; } = false;

        /// <summary>3.5.5: el rotulo corto. 'NQ D2 ▼' -> 'NQ D2'; edad ' ·422m' -> ' ·7h' solo si > 30 min; ' ·21 ctos' -> ' ·21'; descripciones fuera.</summary>
        private string RotuloCorto(string nombre, string precio, string sufijo) => Dibujo37.RotuloCorto(nombre, precio, sufijo);   // 3.7.0: sin ATAS (Simulador37)

        /// <summary>Capa CBOE (NDX/QQQ): indice 0..1 = D1..D2 (3.7.0: sin D3); zero aparte.</summary>
        private bool RayaCapa(string nombre, int i)
        {
            bool ndx = string.Equals(nombre, "NDX", StringComparison.OrdinalIgnoreCase);
            bool qqq = string.Equals(nombre, "QQQ", StringComparison.OrdinalIgnoreCase);
            if (!ndx && !qqq) return true;
            if (i == 0) return ndx ? Raya3NdxD1 : Raya3QqqD1c;   // 3.7.3: QQQ prendida por defecto
            if (i == 1) return ndx ? Raya3NdxD2b : Raya3QqqD2d;
            return false;
        }
        private bool RayaCapaZero(string nombre)
        {
            if (string.Equals(nombre, "NDX", StringComparison.OrdinalIgnoreCase)) return Raya3NdxZeroE;   // 3.7.3: apagado por defecto
            if (string.Equals(nombre, "QQQ", StringComparison.OrdinalIgnoreCase)) return Raya3QqqZeroB;   // 3.7.1: apagado por defecto
            return true;
        }
        private bool EnfasisCapaZero(string nombre) => (Raya3QqqZeroEnfasis && string.Equals(nombre, "QQQ", StringComparison.OrdinalIgnoreCase)) || (Raya3NdxZeroEnfasisB && string.Equals(nombre, "NDX", StringComparison.OrdinalIgnoreCase));   // 3.6.7: NDX tambien (3.7.3: renombrada, apagada)
    }
}
