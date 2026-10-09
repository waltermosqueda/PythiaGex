using System;
using System.ComponentModel.DataAnnotations;

namespace PythiaGexCuatro
{
    /// <summary>
    /// 3.5.0 (pedido del operador 08-10 00:00 ART): "poder desactivar una por una, aunque sea de la misma familia, hasta ver el patron que nos
    /// sirva; no a lo bruto toda la familia". Cada raya que dibuja la 3.0 tiene su casilla. Medido el 07-10 con su criterio visual
    /// (laboratorio/tres/rayas_hoy.py, resultados/rayas_hoy_2026-10-07.md): de noche la mejor fue QQQ D1 (18 de 23, mecha a 0,7 pt), NDX D2 y
    /// QQQ D2 no tuvieron toques y de dia la mecha las paso 6-9 pts: por eso arrancan apagadas. Nombres nuevos: el .ws no los pisa.
    /// </summary>
    public partial class FamiliaCuatro
    {
        [Display(Name = "NQ D1", GroupName = "9.7 3.0 · Rayas una por una", Order = 1)] public bool Raya3NqD1 { get; set; } = true;
        [Display(Name = "NQ D2", GroupName = "9.7 3.0 · Rayas una por una", Order = 2)] public bool Raya3NqD2 { get; set; } = true;
        [Display(Name = "NQ zero gamma (0Γ)", GroupName = "9.7 3.0 · Rayas una por una", Order = 3)] public bool Raya3NqZero { get; set; } = true;
        [Display(Name = "NQ M+ (major positivo)", GroupName = "9.7 3.0 · Rayas una por una", Order = 4)] public bool Raya3NqMas { get; set; } = true;
        [Display(Name = "NQ M− (major negativo)", GroupName = "9.7 3.0 · Rayas una por una", Order = 5)] public bool Raya3NqMenos { get; set; } = true;
        [Display(Name = "NDX D1", GroupName = "9.7 3.0 · Rayas una por una", Order = 10)] public bool Raya3NdxD1 { get; set; } = true;
        [Display(Name = "NDX D2", GroupName = "9.7 3.0 · Rayas una por una", Order = 11, Description = "Apagada por defecto (07-10: 0 toques de noche; de dia la mecha la paso 9 pts de mediana).")] public bool Raya3NdxD2 { get; set; } = false;
        [Display(Name = "NDX D3", GroupName = "9.7 3.0 · Rayas una por una", Order = 12)] public bool Raya3NdxD3 { get; set; } = false;
        [Display(Name = "NDX zero gamma (la fila de rombos de la 2.0)", GroupName = "9.7 3.0 · Rayas una por una", Order = 13, Description = "3.6.7: nombre nuevo para volver a prenderla (el operador la habia apagado cuando la base de NDX la corria ~10 pts). Con la base de forwards (3.6.5) cae donde la 2.0: 31.375,43 / 31.350,73 el 08-10.")] public bool Raya3NdxZeroB { get; set; } = true;
        [Display(Name = "NDX zero gamma con enfasis (rombos grandes)", GroupName = "9.7 3.0 · Rayas una por una", Order = 14)] public bool Raya3NdxZeroEnfasis { get; set; } = true;
        [Display(Name = "NDX zero: techo y piso (los dos cruces que encierran al precio, como la 2.0)", GroupName = "9.7 3.0 · Rayas una por una", Order = 15,
                 Description = "3.6.8. La 2.0 dibuja el zero de NDX como el cruce MAS CERCANO, y como salta entre el de arriba y el de abajo segun la mitad del rango, en su grafico quedan las dos filas (08-10: 31.375,43 y 31.350,73). Prendido: la 3.0 dibuja los dos cruces de signo por volumen que encierran al precio en cada vela (techo y piso).")]
        public bool Raya3NdxZeroCerco { get; set; } = true;
        [Display(Name = "QQQ D1", GroupName = "9.7 3.0 · Rayas una por una", Order = 20)] public bool Raya3QqqD1 { get; set; } = true;
        [Display(Name = "QQQ D2", GroupName = "9.7 3.0 · Rayas una por una", Order = 21, Description = "Apagada por defecto (07-10: 0 toques de noche; de dia la mecha la paso 6 pts de mediana).")] public bool Raya3QqqD2 { get; set; } = false;
        [Display(Name = "QQQ D3", GroupName = "9.7 3.0 · Rayas una por una", Order = 22)] public bool Raya3QqqD3 { get; set; } = false;
        [Display(Name = "QQQ zero gamma", GroupName = "9.7 3.0 · Rayas una por una", Order = 23)] public bool Raya3QqqZero { get; set; } = true;
        [Display(Name = "QQQ zero: con enfasis (rombo grande + linea al eje)", GroupName = "9.7 3.0 · Rayas una por una", Order = 24)] public bool Raya3QqqZeroEnfasis { get; set; } = true;
        [Display(Name = "Cruces del zero sin cortes (completa huecos de hasta 3 velas)", GroupName = "9.7 3.0 · Rayas una por una", Order = 30,
                 Description = "Los cruces de signo aparecen y desaparecen de una vela a otra (raya intermitente, p. ej. 31.380 el 07-10). Prendido: si un cruce estaba antes y vuelve dentro de 3 velas, se dibuja tambien en el hueco.")]
        public bool Cruces3SinCortes { get; set; } = true;

        [Display(Name = "Todas independientes: sin fusionar, sin esconder, sin promediar", GroupName = "9.7 3.0 · Rayas una por una", Order = 40,
                 Description = "3.5.0 (pedido 08-10): ninguna dominante se fusiona ni se esconde detras de otra. Rotulos de capa aunque coincidan con D1/D2 de NQ (sin 'NQ·QQQ D1'), sin tope de rotulos, y D1/D2 en el STRIKE EXACTO (sin el centroide que promediaba strikes cercanos en uno, tambien en la histeresis y en el rebobinado). Apagado = como 3.4.1.")]
        public bool Rayas3Independientes { get; set; } = true;

        [Display(Name = "Tunel sombreado (franja entre D1 y D2)", GroupName = "9.7 3.0 · Rayas una por una", Order = 50, Description = "3.5.5: apagado por defecto (pedido 08-10).")]
        public bool Tunel3Sombreado { get; set; } = false;

        [Display(Name = "Rayas largas (hasta el eje o de lado a lado)", GroupName = "9.7 3.0 · Rayas una por una", Order = 51,
                 Description = "3.5.5: apagado por defecto (pedido 08-10: 'mas natural'). Prendido: majors y zero de lado a lado, y de la ultima vela al eje las capas, el APOYO, los cruces por OI y el zero de QQQ. Apagado: quedan las estelas por vela y los rotulos.")]
        public bool Rayas3Largas { get; set; } = false;

        [Display(Name = "Rotulos cortos (libro, tipo y precio)", GroupName = "9.7 3.0 · Rayas una por una", Order = 52,
                 Description = "3.5.5: sin flecha, sin distancia y sin la descripcion; la edad de las capas de CBOE solo si pasa de 30 min (p. ej. ·7h).")]
        public bool Rotulos3Cortos { get; set; } = true;

        [Display(Name = "Rotulos: tamaño (%)", GroupName = "9.7 3.0 · Rayas una por una", Order = 53)]
        [Range(50, 120)]
        public int Rotulos3TamPct { get; set; } = 85;

        [Display(Name = "Rotulos: rayitas que los unen a su precio", GroupName = "9.7 3.0 · Rayas una por una", Order = 54, Description = "3.5.6: apagadas por defecto (pedido 08-10: 'quedaron sueltas').")]
        public bool Rotulos3Conectores { get; set; } = false;

        /// <summary>3.5.5: el rotulo corto. 'NQ D2 ▼' -> 'NQ D2'; edad ' ·422m' -> ' ·7h' solo si > 30 min; ' ·21 ctos' -> ' ·21'; descripciones fuera.</summary>
        private string RotuloCorto(string nombre, string precio, string sufijo)
        {
            string n = (nombre ?? "").Replace("▲", "").Replace("▼", "").Trim();
            while (n.Contains("  ")) n = n.Replace("  ", " ");
            string s = "";
            var m = System.Text.RegularExpressions.Regex.Match(sufijo ?? "", @"·\s*(\d+)\s*m\b");
            if (m.Success) { int min = int.Parse(m.Groups[1].Value); if (min > 30) s = " ·" + (min >= 90 ? (min / 60.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "h" : min + "m"); }
            var c = System.Text.RegularExpressions.Regex.Match(sufijo ?? "", @"·\s*([\d.,]+)\s*ctos");
            if (c.Success) s = " ·" + c.Groups[1].Value;
            return n + " " + precio + s;
        }

        /// <summary>Capa CBOE (NDX/QQQ): indice 0..2 = D1..D3; zero aparte.</summary>
        private bool RayaCapa(string nombre, int i)
        {
            bool ndx = string.Equals(nombre, "NDX", StringComparison.OrdinalIgnoreCase);
            bool qqq = string.Equals(nombre, "QQQ", StringComparison.OrdinalIgnoreCase);
            if (!ndx && !qqq) return true;
            if (i == 0) return ndx ? Raya3NdxD1 : Raya3QqqD1;
            if (i == 1) return ndx ? Raya3NdxD2 : Raya3QqqD2;
            return ndx ? Raya3NdxD3 : Raya3QqqD3;
        }
        private bool RayaCapaZero(string nombre)
        {
            if (string.Equals(nombre, "NDX", StringComparison.OrdinalIgnoreCase)) return Raya3NdxZeroB;
            if (string.Equals(nombre, "QQQ", StringComparison.OrdinalIgnoreCase)) return Raya3QqqZero;
            return true;
        }
        private bool EnfasisCapaZero(string nombre) => (Raya3QqqZeroEnfasis && string.Equals(nombre, "QQQ", StringComparison.OrdinalIgnoreCase)) || (Raya3NdxZeroEnfasis && string.Equals(nombre, "NDX", StringComparison.OrdinalIgnoreCase));   // 3.6.7: NDX tambien
    }
}
