# -*- coding: utf-8 -*-
"""grupo_arriba_415e.py — PythiaGex 4.1.5e (09-10-2026). FUENTE DE VERDAD del grupo de arriba del dialogo de ajustes de la 4.1
("0. PRENDER / APAGAR (todo lo que se dibuja)"): el ORDEN, el NOMBRE que se ve y la PRIMERA FRASE de la descripcion de cada casilla.

Pedido del operador (textual): "pierdo mucho tiempo y tengo que estar buscando que es cada cosa ... adivinando para ver si aparecen/desaparecen
las dominantes especificas que quiero". Medido por el verificador (09-10 ~19:30): 8 de sus 28 casillas prendidas no dibujaban nada porque
dependian de una llave Tres41* apagada, y ni el nombre ni la descripcion lo decian.

JERARQUIA VISIBLE (la columna del dialogo muestra unos 79 DIP en Segoe UI 12, 12-13 letras: medido con la captura
capturas/2026-10-09_1729_ajustes_grupo_arriba_4_1_5c.jpg, entran 'NDX majors OI' 78,9 y 'NDX 0Γ est vol' 77,6, se cortan 'NDX muros vol' 80,2 y
'NDX majors vol' 82,4; --anchos lo vuelve a medir):
  1. arriba, lo que NO necesita llave, en el orden de siempre: las series de la Familia (NQ, NDX, QQQ, TQQQ, FAM/CONF, clasica, 2.0, 3.0 rayas);
     despues la pantalla de la 4.1 (doble eje, 'Estela▸' con '↳ rot. tramos', 'Etiquetas▸' con '↳ montos' y '↳ cambios ▲▼', la pestaña).
  2. la 3.0: cada LLAVE (nombre que termina en ▸) va inmediatamente ANTES de sus dependientes (nombre que empieza con '↳ '). Dos sub-llaves
     ('↳ capas 0Γ▸', '↳ F1-F8 ver▸') son dependientes que a su vez prenden lo que sigue.
  3. al final, lo comun a varias llaves de la 3.0 ('↳ ver estela', '↳ ver rotulos', '↳ rayas largas', ...): su descripcion dice cuales.
PRIMERA FRASE de la descripcion: "Necesita la llave '<llave>' prendida." (dependientes), "Necesita alguna de las llaves ... prendida." (comunes),
"Llave: ..." / "Sub-llave: ..." (llaves) o "No necesita ninguna llave..." (lo de arriba). Si ademas hace falta otra casilla, la frase siguiente
empieza con "Tambien necesita" o "Solo cambia algo con". Esas frases las arma y las reemplaza este script; el resto de la descripcion es la de
siempre (o la de BASE). Cada condicion sale del codigo (grep de cada propiedad en Pintar, PintarCapas, PintarApoyo, PintarMajorOi, PintarFormulas,
Rotulos, Candidatos, Cabecera y PintarRecuadro de la 3.0, y en ArmadoPantalla de la 4.1): ver la tabla del CHANGELOG 4.1.5e.

SOLO cambia [Display(Name/GroupName/Order/Description)]: NINGUNA propiedad se renombra ni cambia de tipo o default (el .ws guarda por nombre).
Los demas grupos: Order + 1000 si era menor (como la 4.1.5c), asi el grupo de arriba queda primero. Idempotente: correrlo dos veces no cambia
nada. El reloj de 250 ms (GammaHoyTresCasillas.cs) mira todo el grupo por reflexion (bool y enum del grupo, por Order): no hay que tocarlo.
Uso:  python -I herramientas/grupo_arriba_415e.py              aplica (escribe solo los .cs que cambian)
      python -I herramientas/grupo_arriba_415e.py --verificar  no escribe; sale con 1 si algun .cs cambiaria (lo corre el arnes ws_compat)
      python -I herramientas/grupo_arriba_415e.py --anchos     ademas mide el ancho de cada nombre (fontTools, Segoe UI 12)
"""
import os, re, sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

AQUI = os.path.dirname(os.path.abspath(__file__))
P4 = os.path.normpath(os.path.join(AQUI, "..", "atas", "PythiaGexCuatro"))
CATALOGO = os.path.join(P4, "_modulos", "familia", "fam", "CatalogoFamilia.cs")
GRUPO = "0. PRENDER / APAGAR (todo lo que se dibuja)"
CORRER = 1000

# (propiedad, nombre en el dialogo) en el orden del grupo: el indice es el Order (0..100, sin huecos). Lo distintivo va al principio.
# Formato fijo ("Prop", "Nombre"): lo leen los arneses (pantalla A21-A23 y ws_compat W6-W8) con una expresion regular.
LISTA = [
    # ---- 1. lo que NO necesita llave: las series de la Familia, en el orden de siempre
    ("S_MUROS_NQ_vol", "NQ muros vol"), ("S_MUROS_NQ_oi", "NQ muros OI"), ("S_MAJORS_NQ_vol", "NQ majors vol"), ("S_MAJORS_NQ_oi", "NQ majors OI"),
    ("S_ZEST_NQ_vol", "NQ 0Γ est vol"), ("S_ZEST_NQ_oi", "NQ 0Γ est OI"), ("S_ZTP_NQ_vol", "NQ cruces vol"),
    ("S_MUROS_NDX_vol", "NDX muros vol"), ("S_MUROS_NDX_oi", "NDX muros OI"), ("S_MAJORS_NDX_vol", "NDX majors vol"), ("S_MAJORS_NDX_oi", "NDX majors OI"),
    ("S_ZEST_NDX_vol", "NDX 0Γ est vol"), ("S_ZTP_NDX_vol", "NDX cruces vol"),
    ("S_MUROS_QQQ_vol", "QQQ muros vol"), ("S_MUROS_QQQ_oi", "QQQ muros OI"), ("S_MAJORS_QQQ_vol", "QQQ majors vol"), ("S_MAJORS_QQQ_oi", "QQQ majors OI"),
    ("S_ZEST_QQQ_vol", "QQQ 0Γ est vol"),
    ("S_T_DOMS_vol", "TQQQ dom vol"), ("S_T_MUROS_vol", "TQQQ muros vol"), ("S_T_MUROS_oi", "TQQQ muros OI"), ("S_T_ZERO_oi", "TQQQ 0Γ OI"),
    ("S_T_DOMS_raz", "TQQQ razon (comparar)"),
    ("S_FAM_MUROS_vol", "FAM muros vol"), ("S_FAM_MUROS_oi", "FAM muros OI"), ("S_CONF_vol", "CONF vol"),
    ("S_R10_NDX_dom", "Clas NDX D1-D3"), ("S_R10_NDX_zero", "Clas NDX 0Γ"),
    ("S_R20_QQQ_vol", "2.0 QQQ D1-D2"), ("S_R20_NDX_vol", "2.0 NDX D1-D2"), ("S_DOMS_QQQ_vol", "QQQ dom sel2.0"), ("S_DOMS_NDX_vol", "NDX dom sel2.0"),
    ("S_TRES_NQ", "3.0 NQ rayas"), ("S_TRES_NDX", "3.0 NDX rayas"), ("S_TRES_QQQ", "3.0 QQQ rayas"),
    # ---- la pantalla de la 4.1 (sus dos llaves: las rayitas y las etiquetas de TODAS las series de arriba)
    ("Eje4Libro", "Doble eje izq."),
    ("Estela4", "Estela▸"), ("Tramos41Rotulos", "↳ rot. tramos"),
    ("Rotulos4", "Etiquetas▸"), ("Monto41Rotulos", "↳ montos"), ("Cambio41Rotulos", "↳ cambios ▲▼"),
    ("Cabecera4", "Pestaña de detalle"),
    # ---- 2. la 3.0: cada llave inmediatamente antes de sus dependientes
    ("Tres41Dominantes", "3.0 NQ dom▸"),
    ("Raya3NqD1", "↳ NQ D1"), ("Raya3NqD2", "↳ NQ D2"), ("Ver3Toques", "↳ toques"),
    ("Raya3NqMasOi", "↳ NQ M+ OI"), ("Raya3NqMenosOi", "↳ NQ M− OI"),
    ("Perfil3Visual", "↳ perfil visual"), ("Ver3Majors", "↳ ver majors"), ("Raya3NqMas", "↳ NQ M+ vol"), ("Raya3NqMenos", "↳ NQ M− vol"),
    ("Barras3Lado", "↳ barras perfil"),
    ("Raya3NdxMasOi", "↳ NDX M+ OI"), ("Raya3NdxMenosOi", "↳ NDX M− OI"), ("Raya3QqqMasOi", "↳ QQQ M+ OI"), ("Raya3QqqMenosOi", "↳ QQQ M− OI"),
    ("Ver3Apoyo", "↳ apoyo"), ("Ver3EstelaZeroOi", "↳ 0Γ OI estela"), ("Zero3TodosLosCruces", "↳ todos cruces"), ("Cruces3SinCortes", "↳ sin cortes"),
    ("Tres41Zero", "3.0 NQ 0Γ▸"),
    ("Raya3NqZero", "↳ NQ 0Γ"), ("Ver3EstelaZero", "↳ ver 0Γ estela"), ("Ver3Zero", "↳ ver 0Γ raya"),
    ("Tres41Tunel", "3.0 tunel▸"), ("Ver3Tunel", "↳ ver tunel"),
    ("Tres41Capas", "3.0 capas▸"),
    ("Raya3NdxD1", "↳ NDX D1"), ("Raya3NdxD2", "↳ NDX D2"), ("Raya3NdxD3", "↳ NDX D3"),
    ("Raya3QqqD1", "↳ QQQ D1"), ("Raya3QqqD2", "↳ QQQ D2"), ("Raya3QqqD3", "↳ QQQ D3"),
    ("Capa3ZeroRombos", "↳ capas 0Γ▸"),
    ("Raya3NdxZeroB", "↳ NDX 0Γ"), ("Raya3NdxZeroEnfasis", "↳ NDX 0Γ enf."), ("Raya3NdxZeroCerco", "↳ NDX techo/piso"),
    ("Raya3QqqZero", "↳ QQQ 0Γ"), ("Raya3QqqZeroEnfasis", "↳ QQQ 0Γ enf."),
    ("Capa3LineaActual", "↳ linea al eje"),
    ("Tres41Formulas", "3.0 F1-F8▸"),
    ("Formulas3Ver", "↳ F1-F8 ver▸"),
    ("Formula3F1", "↳ F1 cruce"), ("Formula3F2b", "↳ F2 lado vol"), ("Formula3F3b", "↳ F3 lado OI"), ("Formula3F4b", "↳ F4 lado v+OI"),
    ("Formula3F5", "↳ F5 muros vol"), ("Formula3F6b", "↳ F6 muros OI"), ("Formula3F8b", "↳ F8 como 2.0"),
    ("Tres41Recuadro", "3.0 libro▸"), ("RecuadroLibro3Ver", "↳ ver libro"),
    ("Tres41Cabecera", "3.0 cabecera▸"), ("Ver3Cabecera", "↳ ver cabecera"),
    # ---- 3. lo comun a varias llaves de la 3.0 (la descripcion dice cuales)
    ("Ver3Estela", "↳ ver estela"), ("Ver3Rotulos", "↳ ver rotulos"), ("Rotulos3Cortos", "↳ rot. cortos"), ("Rotulos3Conectores", "↳ rot. rayitas"),
    ("Rayas3Largas", "↳ rayas largas"), ("Estela3SesionAnterior", "↳ sesion ant."), ("Atravesadas3AtenuarB", "↳ atravesadas"),
]

# Llaves: sin ellas no se dibuja ninguno de los renglones '↳' que las siguen (hasta la proxima llave).
LLAVES = ["Estela4", "Rotulos4", "Tres41Dominantes", "Tres41Zero", "Tres41Tunel", "Tres41Capas", "Tres41Formulas", "Tres41Recuadro", "Tres41Cabecera"]
# Sub-llaves: dependientes ('↳ ... ▸') que ademas prenden algunos de los renglones que siguen (los que lo dicen en su 'Tambien necesita').
SUBLLAVES = {
    "Capa3ZeroRombos": "sin ella no se dibuja ningun 0Γ de las capas ('↳ NDX 0Γ', '↳ NDX 0Γ enf.', '↳ NDX techo/piso', '↳ QQQ 0Γ' y '↳ QQQ 0Γ enf.', abajo)",
    "Formulas3Ver": "sin ella no se dibuja ninguna de las F1-F8 de abajo",
}
# Lo comun a varias llaves de la 3.0 (medido en el codigo, ver CHANGELOG 4.1.5e): necesita ALGUNA de estas prendida.
COMUNES = {
    "Ver3Estela": ["Tres41Dominantes", "Tres41Zero", "Tres41Capas"],
    "Ver3Rotulos": ["Tres41Dominantes", "Tres41Zero", "Tres41Capas", "Tres41Formulas"],
    "Rotulos3Cortos": ["Tres41Dominantes", "Tres41Zero", "Tres41Capas", "Tres41Formulas"],
    "Rotulos3Conectores": ["Tres41Dominantes", "Tres41Zero", "Tres41Capas", "Tres41Formulas"],
    "Rayas3Largas": ["Tres41Dominantes", "Tres41Zero", "Tres41Capas"],
    "Estela3SesionAnterior": ["Tres41Dominantes", "Tres41Zero", "Tres41Capas", "Tres41Formulas"],
    "Atravesadas3AtenuarB": ["Tres41Dominantes", "Tres41Zero", "Tres41Capas", "Tres41Formulas"],
}
# Sin llave (arriba): las series de la Familia (se dibujan solas) y dos de la pantalla de la 4.1.
SIN_LLAVE_OTROS = ["Eje4Libro", "Cabecera4"]

# Frases que siguen a la primera (otra casilla que tambien hace falta). Nombres tal como se ven en el dialogo.
TAMBIEN = {
    "Raya3NqMas": "Tambien necesita los majors a la vista: '↳ ver majors' en Si (o '↳ perfil visual' en ConMajors o Todo); en NQ con Limpio, el default, no se ve.",
    "Raya3NqMenos": "Tambien necesita los majors a la vista: '↳ ver majors' en Si (o '↳ perfil visual' en ConMajors o Todo); en NQ con Limpio, el default, no se ve.",
    "Zero3TodosLosCruces": "Tambien necesita '↳ apoyo' o '↳ 0Γ OI estela' prendida: sin ninguna de las dos no se dibuja (asi esta en el codigo).",
    "Cruces3SinCortes": "Solo cambia algo con '↳ todos cruces' prendida (y lo que ella necesita).",
    "Ver3Zero": "Solo cambia algo con '↳ rayas largas' prendida (por defecto apagada).",
    "Ver3EstelaZero": "Tambien necesita '↳ NQ 0Γ' prendida y '↳ ver estela' en Si (el default).",
    "Raya3NdxZeroB": "Tambien necesita '↳ capas 0Γ▸' prendida.",
    "Raya3NdxZeroEnfasis": "Tambien necesita '↳ capas 0Γ▸' y '↳ NDX 0Γ' prendidas.",
    "Raya3NdxZeroCerco": "Tambien necesita '↳ capas 0Γ▸' y '↳ NDX 0Γ' prendidas.",
    "Raya3QqqZero": "Tambien necesita '↳ capas 0Γ▸' prendida.",
    "Raya3QqqZeroEnfasis": "Tambien necesita '↳ capas 0Γ▸' y '↳ QQQ 0Γ' prendidas.",
    "Capa3LineaActual": "Solo cambia algo con '↳ rayas largas' prendida (por defecto apagada).",
    "Formula3F1": "Tambien necesita '↳ F1-F8 ver▸' prendida.", "Formula3F2b": "Tambien necesita '↳ F1-F8 ver▸' prendida.",
    "Formula3F3b": "Tambien necesita '↳ F1-F8 ver▸' prendida.", "Formula3F4b": "Tambien necesita '↳ F1-F8 ver▸' prendida.",
    "Formula3F5": "Tambien necesita '↳ F1-F8 ver▸' prendida.", "Formula3F6b": "Tambien necesita '↳ F1-F8 ver▸' prendida.",
    "Formula3F8b": "Tambien necesita '↳ F1-F8 ver▸' prendida.",
    "Rotulos3Cortos": "Tambien necesita '↳ ver rotulos' (Si por defecto) y solo cambia algo con 'Rotulos: estilo' en Columna (grupo 9.7; con Minimal, el default, no cambia nada).",
    "Rotulos3Conectores": "Tambien necesita '↳ ver rotulos' (Si por defecto).",
}

# Descripciones que se REEMPLAZAN (las llaves, lo que no tenia descripcion y lo que nombraba casillas con el nombre viejo).
# Lo que no esta aca conserva su descripcion de siempre (sin la primera frase vieja de este script).
BASE = {
    # pantalla de la 4.1
    "Estela4": "Las rayitas por vela de todas las series de la Familia de arriba (la historia de cada nivel). Apagada: sin rayitas ni rotulos de tramos; las etiquetas siguen.",
    "Rotulos4": "Las etiquetas chicas pegadas al eje con el nombre, el precio y la edad del dato de cada nivel de ahora de las series de la Familia de arriba. Apagada: sin etiquetas, sin montos y sin cambios (las rayitas siguen).",
    "Cabecera4": "La pestaña 'PythiaGex 4.0 ▸' arriba a la izquierda: la edad del dato, los avisos (p. ej. 'mercado cerrado: se muestra la sesion del ...') y, con un clic, las fuentes, las conversiones y el detalle de cada nivel.",
    # llaves de la 3.0
    "Tres41Dominantes": "Lo de NQ que dibuja la 3.0: la estela de D1/D2, las marcas de toque, los majors por volumen y por OI, las barras del perfil y sus rotulos; ADEMAS los majors por OI de NDX y QQQ, el apoyo y los cruces del zero (asi esta en el codigo). Apagada: no se dibuja nada de eso (se calcula igual). Lo comun a varias llaves (ver estela, ver rotulos, rayas largas, sesion anterior, atravesadas) esta al final del grupo.",
    "Tres41Zero": "El zero gamma de NQ de la 3.0: su estela por vela, su raya larga y su rotulo. Apagada: no se dibuja (se calcula igual). Lo comun a varias llaves esta al final del grupo.",
    "Tres41Tunel": "La franja entre D1 y D2 de NQ de la 3.0, desde la ultima vela hasta el eje, si las dos estan dentro del radio de dibujo. 4.1.5d: se prende SOLA con esta llave (antes ademas hacia falta 'Tunel sombreado' de 9.7, que estaba apagado: no dibujaba nada).",
    "Tres41Capas": "Las capas NDX y QQQ de la 3.0 (CBOE, 15 min tarde): D1-D3, el 0Γ con su techo/piso, la linea al eje y sus rotulos (con 'Capa NDX' y 'Capa QQQ' en Propia, grupo 9.3: el default). Se CALCULAN igual: TRES_NDX y TRES_QQQ de la Familia salen de ahi. Los majors por OI de NDX y QQQ NO van con esta llave: van con '3.0 NQ dom▸'. Lo comun a varias llaves esta al final del grupo.",
    "Tres41Formulas": "Las formulas de dominantes de la 3.0 dibujadas a la vez para juzgarlas (F1..F8, cada una en su color, con su estela y un rotulo chico). Apagada: no se dibuja ninguna (se calculan igual). F7 son los majors por OI: '↳ NQ M+ OI' y siguientes, con '3.0 NQ dom▸'. Lo comun a varias llaves esta al final del grupo.",
    "Tres41Recuadro": "La pestañita 'LIBRO NQ ▸' de la 3.0 (el recuadro del libro: niveles cercanos con GEX por volumen y por OI, muros, majors y neto). Apagada tampoco toma el clic (la pestaña de la 4.0 va en la misma esquina).",
    "Tres41Cabecera": "La cabecera de la 3.0: un renglon arriba a la izquierda con el estado del libro NQ y sus niveles, y el cartel de estado (LIBRO VIVO CAIDO, SIN CADENA, MERCADO CERRADO) cuando hace falta. La 4.0 tiene su propia pestaña con fuentes y edades.",
    # dependientes de '3.0 NQ dom▸'
    "Raya3NqD1": "La dominante D1 de NQ de la 3.0: estela por vela y rotulo. Tambien decide si D1 entra en la lista del '3.0 libro▸'.",
    "Raya3NqD2": "La dominante D2 de NQ de la 3.0: estela por vela y rotulo. Tambien decide si D2 entra en la lista del '3.0 libro▸'.",
    "Ver3Toques": "La marca de toque (un bloque de una vela de ancho) en la vela que llega a D1 o D2. Segun el perfil = Si.",
    "Raya3NqMasOi": "3.6.1. Raya de dominante en el strike con mas GEX positivo por OI (p. ej. 31.350 el 08-10 a la noche): estela por vela y rotulo 'NQ M+ OI'. Tambien decide si entra (con su punto) en el '3.0 libro▸'.",
    "Raya3NqMenosOi": "3.6.1. Idem del lado negativo (puts): el strike con mas GEX negativo por OI. Tambien decide si entra (con su punto) en el '3.0 libro▸'.",
    "Ver3EstelaZeroOi": "3.2.3. Ademas del zero por volumen, el cruce por cero de la gamma por INTERES ABIERTO por vela: rombos turquesa (con 'Zero gamma: estilo de la estela' en RomboVerde, grupo 9.3: el default) o puntos grises finos. La 2.0 lo dibuja como rombos; aca es apagable y se mide.",
    "Perfil3Visual": "Solo decide dos cosas cuando estan en 'Segun el perfil': '↳ ver majors' (NQ: Limpio = no; ConMajors y Todo = si; ES: siempre si) y '↳ barras perfil' (solo con Todo). Las demas 'ver' en 'Segun el perfil' quedan en Si con cualquier perfil.",
    "Ver3Majors": "Si los majors por volumen de NQ ('↳ NQ M+ vol' y '↳ NQ M− vol') se ven. Segun el perfil: no en NQ con Limpio (el default), si con ConMajors o Todo, y en ES siempre. El rotulo siempre; la raya ademas con '↳ rayas largas' y dentro del radio de dibujo. Tambien la usa el '3.0 libro▸'.",
    "Raya3NqMas": "El major positivo por volumen de NQ de la 3.0: rotulo y, con '↳ rayas largas', la raya de lado a lado.",
    "Raya3NqMenos": "El major negativo por volumen de NQ de la 3.0: rotulo y, con '↳ rayas largas', la raya de lado a lado.",
    "Barras3Lado": "Las barras del perfil de GEX a la izquierda. Ninguna por defecto; Izquierda: ancho maximo 12 % del lienzo, alpha 50 %, sin pelotitas ni montos. Segun el perfil = solo con '↳ perfil visual' en Todo.",
    "Raya3NdxMasOi": "3.6.2. Major positivo por OI del libro de NDX (CBOE), estela en el color de la capa con filo verde; la edad del dato va en el rotulo. Va con la llave '3.0 NQ dom▸' y NO con '3.0 capas▸' (asi esta en el codigo); con 'Capa NDX' en No (grupo 9.3) no hay dato.",
    "Raya3NdxMenosOi": "3.6.2. Major negativo por OI del libro de NDX (CBOE), filo rojo. Va con la llave '3.0 NQ dom▸' y NO con '3.0 capas▸' (asi esta en el codigo); con 'Capa NDX' en No (grupo 9.3) no hay dato.",
    "Raya3QqqMasOi": "3.6.2. Major positivo por OI del libro de QQQ (CBOE, por razon), estela en el color de la capa con filo verde; la edad del dato va en el rotulo. Va con la llave '3.0 NQ dom▸' y NO con '3.0 capas▸' (asi esta en el codigo); con 'Capa QQQ' en No (grupo 9.3) no hay dato.",
    "Raya3QqqMenosOi": "3.6.2. Major negativo por OI del libro de QQQ (CBOE, por razon), filo rojo. Va con la llave '3.0 NQ dom▸' y NO con '3.0 capas▸' (asi esta en el codigo); con 'Capa QQQ' en No (grupo 9.3) no hay dato.",
    # dependientes de '3.0 NQ 0Γ▸'
    "Raya3NqZero": "El zero gamma de NQ por volumen de la 3.0: estela por vela (con '↳ ver 0Γ estela'), rotulo y, con '↳ ver 0Γ raya' y '↳ rayas largas', la raya punteada que cruza el grafico. Tambien decide si el 0Γ entra en la lista del '3.0 libro▸'.",
    "Ver3Zero": "La raya punteada del zero de NQ que cruza el grafico (Segun el perfil = Si).",
    "Ver3EstelaZero": "La estela del zero de NQ por vela cerrada: rombos verdes o guiones grises segun 'Zero gamma: estilo de la estela' (grupo 9.3). Segun el perfil = Si. 3.1.2 (pedido del operador): para ver donde estuvo el flip y si el precio reacciono ahi. Lo medido en el laboratorio: como nivel de rebote el zero rebota igual que su placebo y cambia 26-31 veces por hora; la caja negra lo graba como fam ZERO para medirlo con muestra nueva.",
    # dependientes de '3.0 tunel▸'
    "Ver3Tunel": "Segun el perfil = Si. Con No la llave no dibuja nada.",
    # dependientes de '3.0 capas▸'
    "Raya3NdxD1": "La dominante D1 de la capa NDX de la 3.0: estela por vela y rotulo.",
    "Raya3NdxD2": "La D2 de la capa NDX: estela por vela y rotulo. Apagada por defecto (07-10: 0 toques de noche; de dia la mecha la paso 9 pts de mediana).",
    "Raya3NdxD3": "La D3 de la capa NDX: rotulo (y la linea al eje con '↳ linea al eje' y '↳ rayas largas'); no tiene estela (la guardada es de D1 y D2). Con 'Capas: cuantas dominantes' en 2 (grupo 9.3) no hay D3.",
    "Raya3QqqD1": "La dominante D1 de la capa QQQ de la 3.0: estela por vela y rotulo.",
    "Raya3QqqD2": "La D2 de la capa QQQ: estela por vela y rotulo. Apagada por defecto (07-10: 0 toques de noche; de dia la mecha la paso 6 pts de mediana).",
    "Raya3QqqD3": "La D3 de la capa QQQ: rotulo (y la linea al eje con '↳ linea al eje' y '↳ rayas largas'); no tiene estela (la guardada es de D1 y D2). Con 'Capas: cuantas dominantes' en 2 (grupo 9.3) no hay D3.",
    "Raya3NdxZeroEnfasis": "Enfasis del 0Γ de NDX: rombos mas grandes y opacos; ademas, con '↳ rayas largas' y '↳ NDX techo/piso' apagada, una raya de 2 px de la ultima vela al eje.",
    "Raya3QqqZero": "El zero gamma de la capa QQQ de la 3.0: rombos por vela y rotulo.",
    "Raya3QqqZeroEnfasis": "Enfasis del 0Γ de QQQ: rombos mas grandes y opacos y, con '↳ rayas largas', una raya de 2 px de la ultima vela al eje.",
    # dependientes de '3.0 F1-F8▸' (los textos de cada formula son los de FormulaDoms3; color = el de su estela)
    "Formulas3Ver": "3.6.1. Cada formula de dominantes dibujada a la vez, con su color, su estela por vela y un rotulo chico. F7 son los majors por OI ('↳ NQ M+ OI' y siguientes, con la llave '3.0 NQ dom▸').",
    "Formula3F1": "F1 Cruces de signo (3.3.3): el cruce mas cercano por lado. Se pega al precio. Color gris claro.",
    "Formula3F2b": "F2 Una por lado por volumen: el strike con mas |GEX vol| arriba y abajo del precio en el radio (como la clasica). Color naranja.",
    "Formula3F3b": "F3 Una por lado por OI: el strike con mas |GEX OI| arriba y abajo del precio en el radio. Color violeta.",
    "Formula3F4b": "F4 Una por lado, volumen + OI: el strike con mas |GEX vol| + |GEX OI| arriba y abajo. Color lila.",
    "Formula3F5": "F5 Muros por volumen: el mayor GEX de calls y el mayor de puts a +-100 pts (como la pagina). Color verde lima.",
    "Formula3F6b": "F6 Muros por OI: el mayor GEX de calls y el mayor de puts por interes abierto a +-100 pts. Color rosa.",
    "Formula3F8b": "F8 Las dos mas grandes por volumen (como la 2.0), sin importar el lado. Color celeste.",
    # dependientes de '3.0 cabecera▸'
    "Ver3Cabecera": "El renglon de la cabecera (Segun el perfil = Si). Con No queda solo el cartel de estado cuando hace falta.",
    # comunes
    "Ver3Estela": "La estela por vela (guiones o rombos) de D1/D2 de NQ, del 0Γ de NQ y de las capas. Segun el perfil = Si. No toca las F1-F8, los majors por OI ni el apoyo (tienen su estela propia).",
    "Ver3Rotulos": "Los rotulos de la 3.0 (la columna de nombres al lado de las velas). Segun el perfil = Si.",
    "Rayas3Largas": "3.5.5: apagado por defecto (pedido 08-10: 'mas natural'). Prendido: los majors por volumen y el zero de NQ de lado a lado, y de la ultima vela al eje las capas (con '↳ linea al eje'), el 0Γ con enfasis de las capas, el apoyo y los cruces por OI. Apagado: quedan las estelas por vela y los rotulos.",
    "Estela3SesionAnterior": "3.0.5: lo de antes del inicio de sesion (18:00 NY) se dibuja al 45 % en vez de esconderse: estelas, toques, rombos, majors por OI y F1-F8.",
}
# Series de la Familia cuya descripcion sale del catalogo (CatalogoFamilia.cs: tecnico, criollo y estado); las R10/R20/DOMS tienen la suya.
DEL_CATALOGO_EXTRA = {
    "S_TRES_NQ": "No es la llave '3.0 NQ dom▸': es la serie de la Familia que sale de la estela que la 3.0 calcula siempre.",
    "S_TRES_NDX": "No es la llave '3.0 capas▸': es la serie de la Familia que sale de la capa NDX que la 3.0 calcula siempre.",
    "S_TRES_QQQ": "No es la llave '3.0 capas▸': es la serie de la Familia que sale de la capa QQQ que la 3.0 calcula siempre.",
}

# Las frases que maneja este script empiezan asi (se reconocen y se reemplazan; lo demas no se toca).
APERTURAS = ("Llave:", "Sub-llave:", "Necesita la llave ", "Necesita alguna de las llaves ", "Tambien necesita ", "Solo cambia algo con ",
             "No necesita ninguna llave", "Se ve como ", "Vale para ")

NOMBRE = dict(LISTA)
INDICE = {p: i for i, (p, _) in enumerate(LISTA)}
ES_SERIE = lambda p: p.startswith("S_")


def q(p):
    return "'" + NOMBRE[p] + "'"


def lista_y(nombres):
    """'a', 'b' o 'c'"""
    if len(nombres) == 1:
        return nombres[0]
    return ", ".join(nombres[:-1]) + " o " + nombres[-1]


def llave_de(p):
    """La llave mas cercana ARRIBA en la LISTA (las sub-llaves no cuentan: son dependientes)."""
    i = INDICE[p]
    for j in range(i - 1, -1, -1):
        pj = LISTA[j][0]
        if pj in LLAVES:
            return pj
        if not NOMBRE[pj].startswith("↳ "):
            return None          # un renglon sin flecha corta el bloque
    return None


def frases(p):
    """Las frases que arma este script (la primera dice que necesita)."""
    if p in LLAVES:
        f = ["Llave: sin ella no se dibuja ninguno de los renglones con ↳ que la siguen."]
    elif p in COMUNES:
        f = ["Necesita alguna de las llaves " + lista_y([q(k) for k in COMUNES[p]]) + " prendida.", "Vale para todas las que esten prendidas."]
    elif ES_SERIE(p):
        f = ["No necesita ninguna llave: es una serie de la Familia y se dibuja sola.",
             "Se ve como rayitas con " + q("Estela4") + " y como etiqueta con " + q("Rotulos4") + " (las dos prendidas por defecto)."]
    elif p in SIN_LLAVE_OTROS:
        f = ["No necesita ninguna llave."]
    else:
        k = llave_de(p)
        if k is None:
            raise SystemExit("ERROR: " + p + " (" + NOMBRE[p] + ") no tiene llave arriba en la LISTA")
        f = ["Necesita la llave " + q(k) + " prendida."]
        if p in SUBLLAVES:
            f.append("Sub-llave: " + SUBLLAVES[p] + ".")
    if p in TAMBIEN:
        f.append(TAMBIEN[p])
    return f


def fin_de_frase(s, i):
    """Indice justo despues del punto que cierra la frase que empieza en i (afuera de las comillas simples; '9.7' no cierra)."""
    dentro = False
    n = len(s)
    while i < n:
        c = s[i]
        if c == "'":
            dentro = not dentro
        elif c == "." and not dentro and (i + 1 == n or s[i + 1] == " "):
            return i + 1
        i += 1
    return n


def sacar_frases(d):
    """Saca del principio las frases de este script (las que empiezan con APERTURAS). Devuelve el resto."""
    d = d or ""
    while d.startswith(APERTURAS):
        j = fin_de_frase(d, 0)
        d = d[j:].lstrip(" ")
    return d


def armar(p, base):
    f = " ".join(frases(p))
    return f + (" " + base if base else "")


# ---------------------------------------------------------------------------------------------------- lectura del C#
def catalogo_textos():
    s = open(CATALOGO, encoding="utf-8").read()
    t = {}
    for m in re.finditer(r'S\("(\w+)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)",\s*([\d.]+),\s*'
                         r'"((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)"', s, re.S):
        t[m.group(1)] = (m.group(9), m.group(10), m.group(11))
    return t


def base_catalogo(p, cat):
    sid = p[2:]
    if sid not in cat:
        raise SystemExit("ERROR: la serie " + sid + " no esta en " + CATALOGO)
    tec, cri, est = (x.strip().rstrip(".") for x in cat[sid])
    b = tec + ": " + cri + ". Estado: " + est + "."
    if p in DEL_CATALOGO_EXTRA:
        b += " " + DEL_CATALOGO_EXTRA[p]
    return b


def archivos():
    for raiz, _, fs in os.walk(P4):
        if any(x in raiz for x in (os.sep + "obj", os.sep + "bin", "_visor", os.sep + "prueba", os.sep + "capturas")):
            continue
        for f in fs:
            if f.endswith(".cs"):
                yield os.path.join(raiz, f)


def cerrar_parentesis(s, i):
    """s[i] == '(' -> indice del ')' que lo cierra, salteando literales de C# ("..." con escapes)."""
    nivel = 0
    n = len(s)
    while i < n:
        c = s[i]
        if c == '"':
            i += 1
            while i < n and s[i] != '"':
                i += 2 if s[i] == "\\" else 1
        elif c == "(":
            nivel += 1
        elif c == ")":
            nivel -= 1
            if nivel == 0:
                return i
        i += 1
    return -1


def args_display(contenido):
    """Name = "x", GroupName = "y", Order = 3, Description = "z" -> dict (strings tal cual, escapados)."""
    d = {}
    i = 0
    n = len(contenido)
    while i < n:
        m = re.compile(r"\s*,?\s*(\w+)\s*=\s*").match(contenido, i)
        if not m:
            if contenido[i:].strip(" ,\r\n\t") == "":
                break
            raise ValueError("Display ilegible: " + contenido[i:i + 80])
        clave = m.group(1)
        i = m.end()
        if i < n and contenido[i] == '"':
            j = i + 1
            while contenido[j] != '"':
                j += 2 if contenido[j] == "\\" else 1
            d[clave] = ("s", contenido[i + 1:j])
            i = j + 1
        else:
            m2 = re.compile(r"-?\d+").match(contenido, i)
            if not m2:
                raise ValueError("valor no reconocido en Display: " + contenido[i:i + 40])
            d[clave] = ("n", int(m2.group(0)))
            i = m2.end()
    return d


def texto_display(d):
    partes = []
    for k in ("Name", "GroupName", "Order", "Description"):
        if k in d:
            t, v = d[k]
            partes.append(k + " = " + ('"' + v + '"' if t == "s" else str(v)))
    otras = [k for k in d if k not in ("Name", "GroupName", "Order", "Description")]
    if otras:
        raise ValueError("Display con argumentos que este script no conoce: " + ", ".join(otras))
    return "[Display(" + ", ".join(partes) + ")]"


def displays(s):
    """Cada [Display(...)] del texto: (ini, fin, contenido, propiedad que sigue o None)."""
    for m in re.finditer(r"\[Display\(", s):
        a = m.end() - 1
        b = cerrar_parentesis(s, a)
        if b < 0 or s[b + 1:b + 2] != "]":
            raise ValueError("Display sin cerrar cerca de: " + s[m.start():m.start() + 80])
        fin = b + 2
        mp = re.compile(r"(?:\s*\[[^\]]*\])*\s*public\s+[\w.<>]+\s+(\w+)\s*(?:\{|=>)").match(s, fin)
        yield m.start(), fin, s[a + 1:b], (mp.group(1) if mp else None)


def transformar(textos, cat):
    nuevos = dict(textos)
    hechos, faltan = {}, []
    for p_arch, s in textos.items():
        if "partial class FamiliaCuatro" not in s:
            continue
        piezas, ult = [], 0
        for ini, fin, cont, prop in displays(s):
            d = args_display(cont)
            if prop in NOMBRE:
                if prop in hechos:
                    faltan.append(prop + " (dos veces)")
                actual = d.get("Description", ("s", ""))[1]
                if prop in BASE:
                    base = BASE[prop]
                elif ES_SERIE(prop) and prop not in ("S_R10_NDX_dom", "S_R10_NDX_zero", "S_R20_QQQ_vol", "S_R20_NDX_vol", "S_DOMS_QQQ_vol", "S_DOMS_NDX_vol"):
                    base = base_catalogo(prop, cat)
                else:
                    base = sacar_frases(actual)
                d["Name"] = ("s", NOMBRE[prop])
                d["GroupName"] = ("s", GRUPO)
                d["Order"] = ("n", INDICE[prop])
                d["Description"] = ("s", armar(prop, base))
                hechos[prop] = (p_arch, base)
            else:
                if "GroupName" in d and d["GroupName"][1] == GRUPO:
                    faltan.append((prop or "?") + " esta en el grupo de arriba y no en la LISTA")
                if "Order" in d and d["Order"][1] < CORRER:
                    d["Order"] = ("n", d["Order"][1] + CORRER)
                elif "Order" not in d:
                    faltan.append((prop or "?") + " sin Order fuera del grupo")
            piezas.append(s[ult:ini])
            if prop in NOMBRE:
                piezas.append(texto_display(d))          # el de la LISTA: en un renglon, siempre igual
            else:                                        # los demas: solo el numero del Order (el formato no se toca)
                attr = s[ini:fin]
                if "Order" in d:
                    attr = re.sub(r"Order\s*=\s*-?\d+", "Order = %d" % d["Order"][1], attr, count=1)
                piezas.append(attr)
            ult = fin
        piezas.append(s[ult:])
        nuevos[p_arch] = "".join(piezas)
    for p, _ in LISTA:
        if p not in hechos:
            faltan.append(p + " (no encontrada)")
    return nuevos, hechos, faltan


# ---------------------------------------------------------------------------------------------------- controles
def controlar(hechos):
    errores = []
    nombres = [n for _, n in LISTA]
    if len(set(nombres)) != len(nombres):
        errores.append("nombres repetidos en la LISTA")
    if len(set(p for p, _ in LISTA)) != len(LISTA):
        errores.append("propiedades repetidas en la LISTA")
    for k in LLAVES:
        i = INDICE[k]
        if not NOMBRE[k].endswith("▸") or NOMBRE[k].startswith("↳"):
            errores.append(k + ": una llave termina en ▸ y no empieza con ↳")
        if i + 1 >= len(LISTA) or not LISTA[i + 1][1].startswith("↳ "):
            errores.append(k + ": la llave no tiene un dependiente inmediatamente despues")
    for p, n in LISTA:
        if n.startswith("↳ ") and p not in COMUNES and llave_de(p) is None:
            errores.append(p + ": dependiente sin llave arriba")
        if not n.startswith("↳ ") and p not in LLAVES and not (ES_SERIE(p) or p in SIN_LLAVE_OTROS):
            errores.append(p + ": sin ↳ y sin ser llave ni de la Familia")
        if (ES_SERIE(p) or p in SIN_LLAVE_OTROS) and INDICE[p] > INDICE["Tres41Dominantes"]:
            errores.append(p + ": lo que no necesita llave va arriba de la 3.0")
        if n.endswith("▸") and n.startswith("↳ ") and p not in SUBLLAVES:
            errores.append(p + ": '↳ ... ▸' solo para sub-llaves")
    for p, (arch, base) in hechos.items():
        if base.startswith(APERTURAS):
            errores.append(p + ": la descripcion de siempre empieza como una frase de este script")
        d = armar(p, base)
        if sacar_frases(d) != base:
            errores.append(p + ": la primera frase no se puede volver a sacar (idempotencia)")
        if armar(p, sacar_frases(d)) != d:
            errores.append(p + ": armar(sacar(armar)) distinto")
        if '"' in "".join(frases(p)) or "\\" in "".join(frases(p)):
            errores.append(p + ": comillas dobles o barras en una frase del script")
    for p, b in BASE.items():
        if p not in NOMBRE:
            errores.append(p + ": en BASE y no en la LISTA")
        if '"' in b or "\\" in b:
            errores.append(p + ": comillas dobles o barras en BASE (romperia el literal de C#)")
    for p in list(TAMBIEN) + list(COMUNES) + list(SUBLLAVES) + LLAVES + SIN_LLAVE_OTROS + list(DEL_CATALOGO_EXTRA):
        if p not in NOMBRE:
            errores.append(p + ": nombrada en las tablas y no en la LISTA")
    # cada casilla nombrada entre comillas en las frases o en BASE ('↳ ...' o '...▸') tiene que existir con ese nombre (si se renombra una, avisa)
    nombres = set(NOMBRE.values())
    rotulos_en_pantalla = {"LIBRO NQ ▸", "PythiaGex 4.0 ▸"}        # textos que se ven en el grafico, no casillas
    textos = [(p, " ".join(frases(p))) for p, _ in LISTA] + [(p, b) for p, b in BASE.items()] + [(p, t) for p, t in DEL_CATALOGO_EXTRA.items()]
    for p, t in textos:
        for m in re.finditer(r"'([^']+)'", t):
            q_ = m.group(1)
            if (q_.startswith("↳ ") or q_.endswith("▸")) and q_ not in nombres and q_ not in rotulos_en_pantalla:
                errores.append(p + ": nombra '" + q_ + "', que no es ninguna casilla del grupo")
    return errores


def anchos():
    try:
        from fontTools.ttLib import TTFont
    except Exception:
        print("  (anchos: sin fontTools)")
        return
    fuentes = [TTFont(os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", n)) for n in ("segoeui.ttf", "seguisym.ttf")]

    def ancho(s):
        w = 0.0
        for ch in s:
            for f in fuentes:
                g = f.getBestCmap().get(ord(ch))
                if g is not None:
                    w += f["hmtx"][g][0] / f["head"].unitsPerEm
                    break
            else:
                w += 0.6
        return w * 12.0
    entra = max(ancho(x) for x in ("NQ majors vol", "NDX majors OI", "NDX 0Γ est vol", "NDX muros OI"))
    corta = min(ancho(x) for x in ("NDX muros vol", "NDX majors vol"))
    print("  anchos (Segoe UI 12; calibrado con la captura 17:29: entran hasta %.1f, se cortan desde %.1f):" % (entra, corta))
    for i, (p, n) in enumerate(LISTA):
        w = ancho(n)
        print("   %3d %-24s %5.1f %s" % (i, n, w, "entra" if w <= entra else ("SE CORTA" if w >= corta else "dudoso")))


def main():
    verificar = "--verificar" in sys.argv
    textos = {}
    for p in archivos():                                  # newline="": los .cs son CRLF y se escriben tal cual
        with open(p, encoding="utf-8", newline="") as f:
            textos[p] = f.read()
    cat = catalogo_textos()
    nuevos, hechos, faltan = transformar(textos, cat)
    errores = controlar(hechos)
    # idempotencia: una segunda pasada sobre lo nuevo no cambia nada
    nuevos2, _, _ = transformar(nuevos, cat)
    if any(nuevos2[p] != nuevos[p] for p in nuevos):
        errores.append("la segunda pasada cambia algo (no es idempotente)")
    cambian = [p for p in nuevos if nuevos[p] != textos[p]]
    print("grupo_arriba_415e: %d casillas en '%s' (Order 0..%d), %d llaves, %d sub-llaves, %d comunes" % (len(LISTA), GRUPO, len(LISTA) - 1, len(LLAVES), len(SUBLLAVES), len(COMUNES)))
    if "--anchos" in sys.argv:
        anchos()
    if faltan or errores:
        for x in faltan:
            print("FALTA: " + x)
        for x in errores:
            print("ERROR: " + x)
        return 3
    if verificar:
        if cambian:
            print("DISTINTO: cambiarian " + ", ".join(os.path.relpath(p, P4) for p in cambian))
            return 1
        print("sin cambios: el codigo ya coincide con la LISTA (idempotente)")
        return 0
    for p in cambian:
        with open(p, "w", encoding="utf-8", newline="") as f:
            f.write(nuevos[p])
    print("escritos %d archivos: %s" % (len(cambian), ", ".join(os.path.relpath(p, P4) for p in cambian) if cambian else "ninguno (ya estaba)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
