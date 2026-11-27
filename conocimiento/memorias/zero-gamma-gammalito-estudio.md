---
name: zero-gamma-la referencia-estudio
description: "Estudio del zero/flip gris de la referencia (16-11): toques 53,3 % vs corridas 49,0 % y VWAP 52,8 %; cruces 51 %; 671 factores = azar; modelo de arboles verificado: NO se sostiene; C5 pre-registrada"
metadata:
  node_type: memory
  type: project
  originSessionId: b1346044-cbec-4f19-80c3-ffb1de06776c
  modified: 2026-11-16T14:05:33.782Z
---

Pedido 16-11 (opus 5.5, ultracode): "rebotan ahi y salen disparados / si lo atraviesan violento es tendencia", gatillo multifactor.
Todo en `PythiaGex/laboratorio/opciones_cero/` (INFORME.md tercera parte, PREREGISTRO_ZERO.md con Z1-Z11 y L1-L13 escritos ANTES).
Datos 39 ruedas 21-09..13-11, cinta ES por segundo, CBOE a hora de mercado.

**Medido:**
- La raya = cruce de signo de gamma x (volC - volP) 0DTE SPX MAS CERCANO al spot + base (c60/c62). Sigue al precio por construccion
  (precio a < 3 pts el 14,7 % de los segundos; corr 0,96 con la distancia al VWAP). Por eso "no aparece arriba y abajo como las dominantes".
- Toques (467): rebote +6/-6 53,3 % vs corridas +-7,5/12,5 49,0 % vs VWAP 52,8 %; t 1,67 todo, 0,70 confirmacion. Rayas de -15 a +15:
  el zero sale 9.o de 61. Con el atraso de CBOE (902 s) 46,4 %: un indicador PROPIO con CBOE no sirve; la referencia usa precios en vivo.
- Cruces (c66, 214): sigue 51,4 % vs 48,6 % / 52,2 %; violento NO sigue mas (48,5 vs 55,8 en confirmacion). Poder: solo detecta +-14 pp.
- 671 factores (5 familias auditadas + literatura): direccion 4 pasan vs 2 de azar (p 0,21); max-T/BH/jerarquico: 0. Tamaño si (p 0,04,
  0,002 volteando dias) pero es clima del dia (ATR, IV, grandes) e igual en placebos. Candidatos sin validar: op_domf_dO >= 15 (61 %
  conf, fragil: con 12,5 da 53,7 %); grandes en 15 min previos -> rebote rinde menos (max-T 0,044, no en diseño ni dentro del dia).
- Quien acierta (X-Ray, 67 ruedas): nadie anticipa 5-30 min; retail va contra el precio pasado (-0,68), pros lo copia (+0,53).

- Modelo de arboles con los 671 (ZM, c63 multi): AUC 0,566 validado / 0,611 diseño->confirmacion. Verificado por V1-V4 + juez
  (evidencia en `C_xray/paginas/ZERO/verificacion_ZM/`): NO SE SOSTIENE. Real pero chico (de a una rueda AUC 0,570), no paga (acierta
  54-61 %, hace falta 64 % para +1 neto; de a una rueda +0,09 neto y depende del 23-10), la raya corrida -7,5 ordena igual, y lo empuja
  la foto CBOE por t_mercado (llega 15 min tarde): con lo que hay en el toque, AUC 0,531 y neto -0,25. No se congelo.
- MI ERROR (corregido en INFORME y PREREGISTRO): informe t 2,15/2,84 que eran del BRUTO (`tdia(pn)` sin costo); neto -0,64/+1,47.
  Y el "placebos 0,483" mezclaba 4 rayas en un modelo: comparar SIEMPRE raya por raya con el mismo procedimiento.
- C5 pre-registrada para ruedas posteriores al 13-11: toque con op_domf_dO >= 15 (dominante del lado de origen lejos) -> rebote;
  vale la foto PUBLICADA (t0 - 902 s). Repite en las dos mitades pero tambien en el VWAP.

**Trampas encontradas:** base_med = mediana de toda la rueda (mira adelante 0,23 pt mediana; usar base causal de niveles_spx.csv
base_inst). c05 lee Hedge Pressure 1-2 px tarde (arrastra ~10 % de la vela siguiente): c12/c13/c20/c50/c52 lo usan; sus conclusiones eran
negativas, asi que no cambian. El t agrupado CR1 con 14-20 dias rechaza 7,6-7,9 % en vez de 5 % (critico real 2,25): azar ~2 pasadas, no 0,84.
Ver [[la referencia-5-dias-candidatas]], [[la referencia-es-formula]], [[busqueda-estrategia-sin-parar]].
