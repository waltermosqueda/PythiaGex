using System;
using System.ComponentModel.DataAnnotations;

namespace PythiaGexCinco
{
    /// <summary>
    /// 3.5.0 (pedido del operador 08-10 00:00 ART): "poder desactivar una por una, aunque sea de la misma familia, hasta ver el patron que nos
    /// sirva; no a lo bruto toda la familia". Cada raya que dibuja la 3.0 tiene su casilla. Medido el 07-10 con su criterio visual
    /// (laboratorio/tres/rayas_hoy.py, resultados/rayas_hoy_2026-10-07.md): de noche la mejor fue QQQ D1 (18 de 23, mecha a 0,7 pt), NDX D2 y
    /// QQQ D2 no tuvieron toques y de dia la mecha las paso 6-9 pts: por eso arrancan apagadas. Nombres nuevos: el .ws no los pisa.
    /// 5.0 (27-10-2026, P4): las casillas de las capas pasan a SPX/SPY (Raya3Ndx* -> Raya3Spx*, Raya3Qqq* -> Raya3Spy*: DLL nueva, nada guardado);
    /// las Raya3Nq* quedan con su nombre de propiedad porque gobiernan el libro PRIMARIO (en la 5.0, ES por Rithmic) y solo cambia lo que se lee
    /// ("↳ ES D1"). Nombres de <=12 caracteres (la columna del dialogo muestra ~12) y los mismos Order. Los defaults son los de la 4.1 (lo que el
    /// operador eligio mirando NQ): en ES estan SIN MEDIR.
    /// </summary>
    public partial class GammaCinco
    {
        [Display(Name = "↳ ES D1", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 38, Description = "Necesita la llave '3.0 ES dom▸' prendida. La dominante D1 del libro ES de la 3.0 (opciones del futuro por Rithmic): estela por vela y rotulo. Tambien decide si D1 entra en la lista del '3.0 libro▸'.")] public bool Raya3NqD1 { get; set; } = true;
        [Display(Name = "↳ ES D2", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 39, Description = "Necesita la llave '3.0 ES dom▸' prendida. La dominante D2 del libro ES de la 3.0: estela por vela y rotulo. Tambien decide si D2 entra en la lista del '3.0 libro▸'.")] public bool Raya3NqD2 { get; set; } = true;
        [Display(Name = "↳ ES 0Γ", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 57, Description = "Necesita la llave '3.0 ES 0Γ▸' prendida. El zero gamma del libro ES por volumen de la 3.0: estela por vela (con '↳ ver 0Γ estela'), rotulo y, con '↳ ver 0Γ raya' y '↳ rayas largas', la raya punteada que cruza el grafico. Tambien decide si el 0Γ entra en la lista del '3.0 libro▸'.")] public bool Raya3NqZero { get; set; } = true;
        [Display(Name = "↳ ES M+ vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 45, Description = "Necesita la llave '3.0 ES dom▸' prendida. En ES los majors por volumen se ven con cualquier '↳ perfil visual' (con '↳ ver majors' en Segun el perfil, el default). El major positivo por volumen del libro ES de la 3.0: rotulo y, con '↳ rayas largas', la raya de lado a lado.")] public bool Raya3NqMas { get; set; } = true;
        [Display(Name = "↳ ES M− vol", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 46, Description = "Necesita la llave '3.0 ES dom▸' prendida. En ES los majors por volumen se ven con cualquier '↳ perfil visual' (con '↳ ver majors' en Segun el perfil, el default). El major negativo por volumen del libro ES de la 3.0: rotulo y, con '↳ rayas largas', la raya de lado a lado.")] public bool Raya3NqMenos { get; set; } = true;
        [Display(Name = "↳ SPX D1", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 63, Description = "Necesita la llave '3.0 capas▸' prendida. La dominante D1 de la capa SPX de la 3.0 (CBOE, 15 min tarde, llevada a ES con la base del grupo 9.3): estela por vela y rotulo con la edad del dato.")] public bool Raya3SpxD1 { get; set; } = true;
        [Display(Name = "↳ SPX D2", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 64, Description = "Necesita la llave '3.0 capas▸' prendida. La D2 de la capa SPX: estela por vela y rotulo. Apagada por defecto como su par de la 4.1 (NDX D2: 0 toques de noche el 07-10 en NQ). SIN MEDIR EN ES.")] public bool Raya3SpxD2 { get; set; } = false;
        [Display(Name = "↳ SPX D3", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 65, Description = "Necesita la llave '3.0 capas▸' prendida. La D3 de la capa SPX: rotulo (y la linea al eje con '↳ linea al eje' y '↳ rayas largas'); no tiene estela (la guardada es de D1 y D2). Con 'Capas: cuantas dominantes' en 2 (grupo 9.3) no hay D3.")] public bool Raya3SpxD3 { get; set; } = false;
        [Display(Name = "↳ SPX 0Γ", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 70, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' prendida. El zero gamma por volumen de la capa SPX (cruce de signo del perfil por strike, como la 2.0), en precio de ES con la base de la capa: rombos por vela y rotulo.")] public bool Raya3SpxZero { get; set; } = true;
        [Display(Name = "↳ SPX 0Γ enf", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 71, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' y '↳ SPX 0Γ' prendidas. Enfasis del 0Γ de SPX: rombos mas grandes y opacos; ademas, con '↳ rayas largas' y '↳ SPX 0Γ t/p' apagada, una raya de 2 px de la ultima vela al eje.")] public bool Raya3SpxZeroEnfasis { get; set; } = true;
        [Display(Name = "↳ SPX 0Γ t/p", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 72, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' y '↳ SPX 0Γ' prendidas. Techo/piso del 0Γ de SPX (3.6.8): en vez del cruce MAS CERCANO (que salta entre el de arriba y el de abajo y en la 2.0 deja dos filas), los dos cruces de signo por volumen que encierran al precio en cada vela. Rotulos 'SPX 0Γ techo' y 'SPX 0Γ piso'.")]
        public bool Raya3SpxZeroCerco { get; set; } = true;
        [Display(Name = "↳ SPY D1", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 66, Description = "Necesita la llave '3.0 capas▸' prendida. La dominante D1 de la capa SPY de la 3.0 (CBOE, 15 min tarde, llevada a ES por la razon ES/SPY del grupo 9.3): estela por vela y rotulo con la edad del dato.")] public bool Raya3SpyD1 { get; set; } = true;
        [Display(Name = "↳ SPY D2", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 67, Description = "Necesita la llave '3.0 capas▸' prendida. La D2 de la capa SPY: estela por vela y rotulo. Apagada por defecto como su par de la 4.1 (QQQ D2: 0 toques de noche el 07-10 en NQ). SIN MEDIR EN ES.")] public bool Raya3SpyD2 { get; set; } = false;
        [Display(Name = "↳ SPY D3", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 68, Description = "Necesita la llave '3.0 capas▸' prendida. La D3 de la capa SPY: rotulo (y la linea al eje con '↳ linea al eje' y '↳ rayas largas'); no tiene estela (la guardada es de D1 y D2). Con 'Capas: cuantas dominantes' en 2 (grupo 9.3) no hay D3.")] public bool Raya3SpyD3 { get; set; } = false;
        [Display(Name = "↳ SPY 0Γ", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 73, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' prendida. El zero gamma por volumen de la capa SPY, en precio de ES por la razon: rombos por vela y rotulo.")] public bool Raya3SpyZero { get; set; } = true;
        [Display(Name = "↳ SPY 0Γ enf", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 74, Description = "Necesita la llave '3.0 capas▸' prendida. Tambien necesita '↳ capas 0Γ▸' y '↳ SPY 0Γ' prendidas. Enfasis del 0Γ de SPY: rombos mas grandes y opacos y, con '↳ rayas largas', una raya de 2 px de la ultima vela al eje.")] public bool Raya3SpyZeroEnfasis { get; set; } = true;
        [Display(Name = "↳ sin cortes", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 55, Description = "Necesita la llave '3.0 ES dom▸' prendida. Solo cambia algo con '↳ todos cruces' prendida (y lo que ella necesita). Los cruces de signo aparecen y desaparecen de una vela a otra (raya intermitente). Prendido: si un cruce estaba antes y vuelve dentro de 3 velas, se dibuja tambien en el hueco.")]
        public bool Cruces3SinCortes { get; set; } = true;

        [Display(Name = "Todas independientes: sin fusionar, sin esconder, sin promediar", GroupName = "9.7 3.0 · Rayas una por una", Order = 1040,
                 Description = "3.5.0 (pedido 08-10): ninguna dominante se fusiona ni se esconde detras de otra. Rotulos de capa aunque coincidan con D1/D2 de ES (sin 'ES·SPX D1'), sin tope de rotulos, y D1/D2 en el STRIKE EXACTO (sin el centroide que promediaba strikes cercanos en uno, tambien en la histeresis y en el rebobinado). Apagado = como 3.4.1.")]
        public bool Rayas3Independientes { get; set; } = true;

        [Display(Name = "Tunel sombreado (franja entre D1 y D2)", GroupName = "9.7 3.0 · Rayas una por una", Order = 1050, Description = "3.5.5: apagado por defecto (pedido 08-10). 4.1.5d: en la 5.0 ya no cambia nada: el tunel lo prende solo '3.0 tunel D1-D2' del grupo de arriba (antes hacian falta las dos).")]
        public bool Tunel3Sombreado { get; set; } = false;

        [Display(Name = "↳ rayas largas", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 93, Description = "Necesita alguna de las llaves '3.0 ES dom▸', '3.0 ES 0Γ▸' o '3.0 capas▸' prendida. Vale para todas las que esten prendidas. 3.5.5: apagado por defecto (pedido 08-10: 'mas natural'). Prendido: los majors por volumen y el zero del libro ES de lado a lado, y de la ultima vela al eje las capas SPX/SPY (con '↳ linea al eje'), el 0Γ con enfasis de las capas, el apoyo y los cruces por OI. Apagado: quedan las estelas por vela y los rotulos.")]
        public bool Rayas3Largas { get; set; } = false;

        [Display(Name = "↳ rot. cortos", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 91, Description = "Necesita alguna de las llaves '3.0 ES dom▸', '3.0 ES 0Γ▸', '3.0 capas▸' o '3.0 F1-F8▸' prendida. Vale para todas las que esten prendidas. Tambien necesita '↳ ver rotulos' (Si por defecto) y solo cambia algo con 'Rotulos: estilo' en Columna (grupo 9.7; con Minimal, el default, no cambia nada). 3.5.5: sin flecha, sin distancia y sin la descripcion; la edad de las capas de CBOE solo si pasa de 30 min (p. ej. ·7h).")]
        public bool Rotulos3Cortos { get; set; } = true;

        [Display(Name = "Rotulos: tamaño (%)", GroupName = "9.7 3.0 · Rayas una por una", Order = 1053)]
        [Range(50, 120)]
        public int Rotulos3TamPct { get; set; } = 85;

        [Display(Name = "↳ rot. rayitas", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 92, Description = "Necesita alguna de las llaves '3.0 ES dom▸', '3.0 ES 0Γ▸', '3.0 capas▸' o '3.0 F1-F8▸' prendida. Vale para todas las que esten prendidas. Tambien necesita '↳ ver rotulos' (Si por defecto). 3.5.6: apagadas por defecto (pedido 08-10: 'quedaron sueltas').")]
        public bool Rotulos3Conectores { get; set; } = false;

        [Display(Name = "↳ rot. rayita solo si esta corrida", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 93, Description = "4.1.7 (12-10-2026): dibuja la rayita que une el rotulo con su precio SOLO cuando la escalera anti-solape lo corrio mas de N pixeles. Resuelve el problema que se ve al alejar el zoom: con el grafico ampliado los rotulos caen casi en su precio y se leen bien, pero al alejar entran mas niveles en menos pixeles, la escalera los corre mucho y el rotulo pasa a estar lejos de su raya. A diferencia de 'rot. rayitas' (que las dibuja para TODAS y por eso se apagaron el 08-10 porque 'quedaron sueltas'), esta solo aparece donde hace falta.")]
        public bool Rotulos3ConectaCorridos { get; set; } = true;

        // 4.1.8: los [Display]/[Range] van SOBRE LA PROPIEDAD PUBLICA. Hasta la 4.1.7 estaban sobre el campo privado,
        // asi que ATAS no les veia ni nombre ni descripcion ni rango. Mover el atributo no cambia el nombre de la
        // propiedad, asi que lo guardado en el .ws se respeta.
        private int _cargaMinOi = 50;
        [Display(Name = "↳ carga: contratos minimos para marcar", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 95, Description = "4.1.8 (12-10-2026): cuantos contratos de interes abierto TOTAL tiene que tener un strike para que las series 'carga' lo marquen. Pedido del operador: 'si hay mas de 50 tiene minimo que estar marcado, pase lo que pase'. OJO: lo que se suma NO son todos los vencimientos de CME. El libro ES suscribe solo los 2 mas cercanos (CadenaApi.Vencimientos) y CBOE recorta a 14 dias en vivo y 8 desde el archivo. 0 = marcar todos los que haya (hasta el tope). 5.0: el 50 es el de NQ; medido el 27-10 04:00 UTC, en ES TODOS los strikes del radio de 25 tienen 528 contratos o mas (mediana 1.008): con 50 la carga de ES marca el radio entero. Queda en 50 hasta decidirlo con el operador.")]
        [Range(0, 5000)]
        public int CargaMinimaOi { get { return _cargaMinOi; } set { _cargaMinOi = value; PythiaGexCinco.Familia.ReglasFam.CargaMinima = value; } }

        private int _cargaTope = 12;
        [Display(Name = "↳ carga: tope de rayas", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 96, Description = "Tope de seguridad para que un dia raro no llene la pantalla. Se quedan los de mas carga. OJO: este tope descarta EN SILENCIO (medido en NQ el 12-10: con el futuro en 31.350, 18 strikes cumplian los contratos minimos y se dibujaban 12). En ES, con el umbral de 50, el radio entero cumple.")]
        [Range(1, 40)]
        public int CargaTopeRayas { get { return _cargaTope; } set { _cargaTope = value; PythiaGexCinco.Familia.ReglasFam.CargaTope = value; } }

        private int _cargaMinRayas = 10;
        [Display(Name = "↳ carga: rayas minimas (aunque no lleguen a los contratos)", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 98, Description = "4.1.8: cuantos strikes cargados se marcan SIEMPRE por libro, aunque no alcancen los contratos minimos. Hace falta porque cuando vence el 0DTE TODOS los strikes pierden sus contratos del dia a la vez y un umbral fijo los borra de golpe (medido en NQ el 12-10: un strike de NDX con 51 contratos desaparecio al quedar en 32, aunque 25 de esos 32 eran una posicion abierta a 4 dias). 0 = solo los que pasan el umbral. SIN MEDIR EN ES.")]
        [Range(0, 20)]
        public int CargaMinimaRayas { get { return _cargaMinRayas; } set { _cargaMinRayas = value; PythiaGexCinco.Familia.ReglasFam.CargaMinimaRayas = value; } }

        private int _cargaRadio = 0;
        [Display(Name = "↳ carga: radio de busqueda (pts, 0 = como las dominantes)", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 97, Description = "4.1.8: a cuantos puntos del futuro se buscan los strikes cargados. 0 = el radio de cada libro de la familia (5.0: ES 25, SPX y SPY 20; el termino del 2 % son ~154 pts y nunca manda). Por eso una raya de carga puede aparecer y desaparecer sola cuando el precio se corre (medido en NQ el 12-10). 5.0: techo 75 (era 300 en NQ): el 1 % de ES en 7.700; mas lejos la cadena de ES de Rithmic llega con un strike de cada dos y el numero dejaria de ser trazable (ReglasFam.CARGA_RADIO_TECHO). SIN MEDIR EN ES.")]
        [Range(0, 75)]
        public int CargaRadioPts { get { return _cargaRadio; } set { _cargaRadio = value; PythiaGexCinco.Familia.ReglasFam.CargaRadioPts = value; } }

        [Display(Name = "↳ rot. pixeles para considerarla corrida", GroupName = "0. PRENDER / APAGAR (todo lo que se dibuja)", Order = 94, Description = "Cuantos pixeles tiene que haberse corrido el rotulo respecto de su precio real para que se dibuje la rayita. Mas chico = mas rayitas.")]
        [Range(2, 60)]
        public int Rotulos3CorridoPx { get; set; } = 7;

        /// <summary>3.5.5: el rotulo corto. 'ES D2 ▼' -> 'ES D2'; edad ' ·422m' -> ' ·7h' solo si > 30 min; ' ·21 ctos' -> ' ·21'; descripciones fuera.</summary>
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

        /// <summary>Capa CBOE (5.0: SPX/SPY; la 4.1: NDX/QQQ): indice 0..2 = D1..D3; zero aparte. Un nombre que no es de capa = sin casilla (true).</summary>
        private bool RayaCapa(string nombre, int i)
        {
            bool spx = string.Equals(nombre, "SPX", StringComparison.OrdinalIgnoreCase);
            bool spy = string.Equals(nombre, "SPY", StringComparison.OrdinalIgnoreCase);
            if (!spx && !spy) return true;
            if (i == 0) return spx ? Raya3SpxD1 : Raya3SpyD1;
            if (i == 1) return spx ? Raya3SpxD2 : Raya3SpyD2;
            return spx ? Raya3SpxD3 : Raya3SpyD3;
        }
        private bool RayaCapaZero(string nombre)
        {
            if (string.Equals(nombre, "SPX", StringComparison.OrdinalIgnoreCase)) return Raya3SpxZero;
            if (string.Equals(nombre, "SPY", StringComparison.OrdinalIgnoreCase)) return Raya3SpyZero;
            return true;
        }
        private bool EnfasisCapaZero(string nombre) => (Raya3SpyZeroEnfasis && string.Equals(nombre, "SPY", StringComparison.OrdinalIgnoreCase)) || (Raya3SpxZeroEnfasis && string.Equals(nombre, "SPX", StringComparison.OrdinalIgnoreCase));   // 3.6.7: la aditiva tambien
    }
}
