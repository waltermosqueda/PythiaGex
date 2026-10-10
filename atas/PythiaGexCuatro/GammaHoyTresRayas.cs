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
        [Display(Name = "↳ NQ D1", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 43, Description = "Necesita la llave '3.0 NQ dom▸' prendida. La dominante D1 de NQ de la 3.0: estela por vela y rotulo. Tambien decide si D1 entra en la lista del '3.0 libro▸'.")] public bool Raya3NqD1 { get; set; } = true;
        [Display(Name = "↳ NQ D2", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 44, Description = "Necesita la llave '3.0 NQ dom▸' prendida. La dominante D2 de NQ de la 3.0: estela por vela y rotulo. Tambien decide si D2 entra en la lista del '3.0 libro▸'.")] public bool Raya3NqD2 { get; set; } = true;
        [Display(Name = "↳ NQ 0Γ", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 62, Description = "Necesita la llave '3.0 NQ 0Γ▸' prendida. El zero gamma de NQ por volumen de la 3.0: estela por vela (con '↳ ver 0Γ estela'), rotulo y, con '↳ ver 0Γ raya' y '↳ rayas largas', la raya punteada que cruza el grafico. Tambien decide si el 0Γ entra en la lista del '3.0 libro▸'.")] public bool Raya3NqZero { get; set; } = true;
        [Display(Name = "↳ NQ M+ vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 50, Description = "Necesita la llave '3.0 NQ dom▸' prendida. Tambien necesita los majors a la vista: '↳ ver majors' en Si (o '↳ perfil visual' en ConMajors o Todo); en NQ con Limpio, el default, no se ve. El major positivo por volumen de NQ de la 3.0: rotulo y, con '↳ rayas largas', la raya de lado a lado.")] public bool Raya3NqMas { get; set; } = true;
        [Display(Name = "↳ NQ M− vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 51, Description = "Necesita la llave '3.0 NQ dom▸' prendida. Tambien necesita los majors a la vista: '↳ ver majors' en Si (o '↳ perfil visual' en ConMajors o Todo); en NQ con Limpio, el default, no se ve. El major negativo por volumen de NQ de la 3.0: rotulo y, con '↳ rayas largas', la raya de lado a lado.")] public bool Raya3NqMenos { get; set; } = true;
        [Display(Name = "↳ NDX D1", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 68, Description = "Necesita la llave '3.0 capas▸' prendida. La dominante D1 de la capa NDX de la 3.0: estela por vela y rotulo.")] public bool Raya3NdxD1 { get; set; } = true;
        [Display(Name = "↳ NDX D2", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 69, Description = "Necesita la llave '3.0 capas▸' prendida. La D2 de la capa NDX: estela por vela y rotulo. Apagada por defecto (07-10: 0 toques de noche; de dia la mecha la paso 9 pts de mediana).")] public bool Raya3NdxD2 { get; set; } = false;
        [Display(Name = "↳ NDX D3", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 70, Description = "Necesita la llave '3.0 capas▸' prendida. La D3 de la capa NDX: rotulo (y la linea al eje con '↳ linea al eje' y '↳ rayas largas'); no tiene estela (la guardada es de D1 y D2). Con 'Capas: cuantas dominantes' en 2 (grupo 9.3) no hay D3.")] public bool Raya3NdxD3 { get; set; } = false;
        [Display(Name = "↳ NDX 0Γ", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 75, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' prendida. 3.6.7: nombre nuevo para volver a prenderla (el operador la habia apagado cuando la base de NDX la corria ~10 pts). Con la base de forwards (3.6.5) cae donde la 2.0: 31.375,43 / 31.350,73 el 08-10.")] public bool Raya3NdxZeroB { get; set; } = true;
        [Display(Name = "↳ NDX 0Γ enf.", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 76, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' y '↳ NDX 0Γ' prendidas. Enfasis del 0Γ de NDX: rombos mas grandes y opacos; ademas, con '↳ rayas largas' y '↳ NDX techo/piso' apagada, una raya de 2 px de la ultima vela al eje.")] public bool Raya3NdxZeroEnfasis { get; set; } = true;
        [Display(Name = "↳ NDX techo/piso", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 77, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' y '↳ NDX 0Γ' prendidas. 3.6.8. La 2.0 dibuja el zero de NDX como el cruce MAS CERCANO, y como salta entre el de arriba y el de abajo segun la mitad del rango, en su grafico quedan las dos filas (08-10: 31.375,43 y 31.350,73). Prendido: la 3.0 dibuja los dos cruces de signo por volumen que encierran al precio en cada vela (techo y piso).")]
        public bool Raya3NdxZeroCerco { get; set; } = true;
        [Display(Name = "↳ QQQ D1", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 71, Description = "Necesita la llave '3.0 capas▸' prendida. La dominante D1 de la capa QQQ de la 3.0: estela por vela y rotulo.")] public bool Raya3QqqD1 { get; set; } = true;
        [Display(Name = "↳ QQQ D2", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 72, Description = "Necesita la llave '3.0 capas▸' prendida. La D2 de la capa QQQ: estela por vela y rotulo. Apagada por defecto (07-10: 0 toques de noche; de dia la mecha la paso 6 pts de mediana).")] public bool Raya3QqqD2 { get; set; } = false;
        [Display(Name = "↳ QQQ D3", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 73, Description = "Necesita la llave '3.0 capas▸' prendida. La D3 de la capa QQQ: rotulo (y la linea al eje con '↳ linea al eje' y '↳ rayas largas'); no tiene estela (la guardada es de D1 y D2). Con 'Capas: cuantas dominantes' en 2 (grupo 9.3) no hay D3.")] public bool Raya3QqqD3 { get; set; } = false;
        [Display(Name = "↳ QQQ 0Γ", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 78, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' prendida. El zero gamma de la capa QQQ de la 3.0: rombos por vela y rotulo.")] public bool Raya3QqqZero { get; set; } = true;
        [Display(Name = "↳ QQQ 0Γ enf.", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 79, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' y '↳ QQQ 0Γ' prendidas. Enfasis del 0Γ de QQQ: rombos mas grandes y opacos y, con '↳ rayas largas', una raya de 2 px de la ultima vela al eje.")] public bool Raya3QqqZeroEnfasis { get; set; } = true;
        [Display(Name = "↳ sin cortes", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 60, Description = "Necesita la llave '3.0 NQ dom▸' prendida. Solo cambia algo con '↳ todos cruces' prendida (y lo que ella necesita). Los cruces de signo aparecen y desaparecen de una vela a otra (raya intermitente, p. ej. 31.380 el 07-10). Prendido: si un cruce estaba antes y vuelve dentro de 3 velas, se dibuja tambien en el hueco.")]
        public bool Cruces3SinCortes { get; set; } = true;

        [Display(Name = "Todas independientes: sin fusionar, sin esconder, sin promediar", GroupName = "9.7 3.0 · Rayas una por una", Order = 1040,
                 Description = "3.5.0 (pedido 08-10): ninguna dominante se fusiona ni se esconde detras de otra. Rotulos de capa aunque coincidan con D1/D2 de NQ (sin 'NQ·QQQ D1'), sin tope de rotulos, y D1/D2 en el STRIKE EXACTO (sin el centroide que promediaba strikes cercanos en uno, tambien en la histeresis y en el rebobinado). Apagado = como 3.4.1.")]
        public bool Rayas3Independientes { get; set; } = true;

        [Display(Name = "Tunel sombreado (franja entre D1 y D2)", GroupName = "9.7 3.0 · Rayas una por una", Order = 1050, Description = "3.5.5: apagado por defecto (pedido 08-10). 4.1.5d: en la 4.1 ya no cambia nada: el tunel lo prende solo '3.0 tunel D1-D2' del grupo de arriba (antes hacian falta las dos).")]
        public bool Tunel3Sombreado { get; set; } = false;

        [Display(Name = "↳ rayas largas", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 98, Description = "Necesita alguna de las llaves '3.0 NQ dom▸', '3.0 NQ 0Γ▸' o '3.0 capas▸' prendida. Vale para todas las que esten prendidas. 3.5.5: apagado por defecto (pedido 08-10: 'mas natural'). Prendido: los majors por volumen y el zero de NQ de lado a lado, y de la ultima vela al eje las capas (con '↳ linea al eje'), el 0Γ con enfasis de las capas, el apoyo y los cruces por OI. Apagado: quedan las estelas por vela y los rotulos.")]
        public bool Rayas3Largas { get; set; } = false;

        [Display(Name = "↳ rot. cortos", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 96, Description = "Necesita alguna de las llaves '3.0 NQ dom▸', '3.0 NQ 0Γ▸', '3.0 capas▸' o '3.0 F1-F8▸' prendida. Vale para todas las que esten prendidas. Tambien necesita '↳ ver rotulos' (Si por defecto) y solo cambia algo con 'Rotulos: estilo' en Columna (grupo 9.7; con Minimal, el default, no cambia nada). 3.5.5: sin flecha, sin distancia y sin la descripcion; la edad de las capas de CBOE solo si pasa de 30 min (p. ej. ·7h).")]
        public bool Rotulos3Cortos { get; set; } = true;

        [Display(Name = "Rotulos: tamaño (%)", GroupName = "9.7 3.0 · Rayas una por una", Order = 1053)]
        [Range(50, 120)]
        public int Rotulos3TamPct { get; set; } = 85;

        [Display(Name = "↳ rot. rayitas", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 97, Description = "Necesita alguna de las llaves '3.0 NQ dom▸', '3.0 NQ 0Γ▸', '3.0 capas▸' o '3.0 F1-F8▸' prendida. Vale para todas las que esten prendidas. Tambien necesita '↳ ver rotulos' (Si por defecto). 3.5.6: apagadas por defecto (pedido 08-10: 'quedaron sueltas').")]
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
