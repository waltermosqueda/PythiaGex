---
name: la referencia-5-dias-candidatas
description: "16-11: formula de la referencia reconstruida con CBOE (92-98 % de las amarillas reales); las 3 reglas de los 5 dias NO pasan en 34 ruedas nuevas; ficha de 492 toques x 80 factores (cinta, IV, charm, absorcion): rebote 48,4 % vs ruptura 48,8 %, nada pasa; giros grandes no nacen mas cerca de las dominantes que cualquier vela; queda UNA candidata pre-registrada (C4)"
metadata:
  node_type: memory
  type: project
  originSessionId: b1346044-cbec-4f19-80c3-ffb1de06776c
  modified: 2026-11-16T05:10:42.769Z
---

Metodo que pidio el operador (16-11, textual resumido): "con pocos dias encontrar la relacion y despues sumar dias"; cruzar TODOS
los factores (delta, big trades, absorcion, order blocks, DOM, IV, charm, theta); "prohibido decir que no hay relacion".
El cartel "Pon tu usuario y contraseña" de la referencia es solo un mensaje de cache: los niveles ESTAN dibujados (mirar primero).

**Lector pasivo** (`laboratorio/opciones_cero/C_xray/`): `c30_gl.py` / `c32_gl_dias.py` capturan su grafico MES 5m con PrintWindow y
calzan las mechas contra las velas conocidas (~1 px); `c31_gl_tabla.py` saca amarillos (dominantes) y grises (zero). Su grafico
carga 5 dias. Solo con "Trading is locked". Devolver la vista.

**Formula validada** (`c40_dom_cboe.py`): dominantes = strikes 0DTE con mayor |gamma x (vol calls - vol puts)| de la cadena SPX de
CBOE a hora de mercado, + base medida del dia. Contra 749 amarillas reales: top-2 92 %, top-3 97 %, top-4 98 % a <= 1,5 pts.
Sirve de sustituto de la referencia en las ruedas con cadena archivada (21-09 en adelante).

**Resultados medidos (datos hasta 13-11):**
- Las 3 reglas de los 5 dias (iman, veredicto de la vela, llegada lenta) en 34 ruedas NO miradas: iman 38,6 % vs espejo 37,0 %;
  veredicto +0,47 bruto / -0,23 neto y signo opuesto entre mitades; llegada +1,25 vs rayas corridas +1,01. NO pasan.
  El "veredicto" era filo de navaja: 5 velas que cerraron a < 1,5 pts del nivel explicaban 226 de 270 pts; 1 punto de corrimiento lo invierte.
- Ficha por toque (`c50_ficha.py`, 492 llegadas, 39 ruedas, ~80 factores con cinta/IV/charm/theta/absorcion/reposicion/X-Ray):
  +6 antes que -6 = 48,4 % (ruptura 48,8 %; rayas corridas 46,5 %); 0 factores pasan diseño+confirmacion; modelos AUC ~0,50.
  Rebotes violentos y rupturas limpias SI difieren en el primer minuto (penetracion 1,0 vs 2,6; delta +45 vs -248), pero entrar
  despues de ver esa firma da 43,8 %: describe, no anticipa.
- Giros grandes (`c52_giros.py`, 115 tramos): nacen a <= 2,5 pts de una dominante top-4 en 40,9 %, pero CUALQUIER vela tiene su
  extremo a <= 2,5 pts de una en 52,4 %: las dominantes estan donde esta el precio (por eso "todo parece respetarlas").
  Detector de giros (1.309 candidatos, 73 ruedas, 15/7,5): en contra 26,5 %, a favor 26,6 % (empata 36,4 %).
- UNICA sobreviviente (C4, candidata, no estrategia): 2da visita o mas a una dominante DENTRO del rango del dia, entrar 60 s
  despues, 30 min: +1,69 bruto (+0,99 neto), 57 %, t 2,2, ambas mitades; rayas corridas +0,02. Contras: con corte +-6 da +0,06
  (excursion en contra mediana 6,6), sin gradiente por puesto, hallada tras ~20 reglas. Pre-registrada en PREREGISTRO.md
  (20 ruedas posteriores al 13-11, sin tocar nada).

**How to apply:** no volver a presentar iman/veredicto/llegada como pistas: ya se probaron fuera de muestra. Si pide "seguir
buscando", lo que falta es muestra NUEVA (C4 hacia adelante), no mas vueltas sobre las mismas 39 ruedas (cada vuelta suma
falsos positivos). Antes de afirmar que un nivel "se respeta", comparar contra el extremo de cualquier vela, no solo contra
rayas corridas. Ver [[opciones-desde-cero-1511]], [[la referencia-es-formula]], [[foco-atas-atajos-trading]].
