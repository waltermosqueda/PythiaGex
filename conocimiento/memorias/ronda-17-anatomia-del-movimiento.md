---
name: ronda-17-anatomia-del-movimiento
description: "Ronda 17 (26-11): anatomia del movimiento con cinta en 12 ruedas (9 ES + 3 MES) + 263 sesiones MES 5 min. Las piernas van con su delta 86-99 %; la firma del giro aparece DESPUES del extremo; la chance de giro es la geometria del paseo al azar; propuesta 'la ola' (previews A/B/C) para el Superindicador"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T02:49:12.347Z
---

Medido el 26-11-2026 (agente de medicion + recalculo mio por fuera). Carpeta `PythiaGex/laboratorio/ronda17_orderflow/`
(RESULTADOS.md, PREREGISTRO.md con 4 enmiendas, PRUEBA_EN_VIVO.md, `narraciones/narracion-<inst>-<dia>.md` = una por rueda,
giro por giro con sus factores). Datos intermedios en `PythiaGex/datos/ronda17` (39 MB, fuera del repo). Baul #36-41.

**Metodo:** zigzag X = 0,08 x mediana del rango de las 10 ruedas previas (MES 9,75 por la semana enorme del 16-20-11; ES 4,75-6,75),
tambien X/2 y 4X. "Pausa" = extremo que volvio entre X/2 y X y despues fue superado. Tanda 0: ES 06, 09, 11-11 (elegidas por regla)
+ MES 23, 24, 25-11; tanda 1: ES 02, 03, 04, 10, 12, 13-11 con codigo congelado; amplia: 263 sesiones MES 5 min.

**Lo mecanico (se repite en las 12 ruedas):**
- Mueve el precio quien VACIA la punta: piernas de X con delta a favor 86-99 % (OFI de la punta 97-100 %); grandes 4X 90 % en un ano
  de MES. La minoria contra el delta es el libro pasivo que se retira ("baja sin vendedores": MES 25-11 10:36-11:17 -56 pts con delta +145).
- La absorcion en contra NO frena la pierna: siguio X/2 a favor 78-87 % contra 82-88 % de un segundo cualquiera.
- Giro contra pausa: agotamiento 41 / 60 % contra 13 / 23 %, subasta terminada 63 / 60 contra 42 / 43 %, la otra punta agrede en
  60 s RR 3,05 / 2,28. Todo usa segundos POSTERIORES al extremo: leido a 15-60 s no suma (RR 0,95-1,12). Describe, no anticipa.
- Lugares (ayer, noche, IB, VWAP, semana, mes, redondos): giran igual que pausan (0,92 / 1,06; 263 sesiones 0,88-1,23). Pista chica:
  "ayer" en 2X 1,13 (IC 1,00-1,29). Donde se forman extremos: absorcion real 1,5-1,6x y empuje 3,3-3,5x mas que al azar.
- Geometria: P(giro | ya volvio d) ~ d/X (ruina del jugador). Recalculado por mi en MES 23-25-11 a 15 s: 25,9 % (0,25-0,375 X),
  48,4 (0,375-0,5), 59,8 (0,5-0,625), 63,2 (0,625-0,75), 88,2 (0,75-1). Es aritmetica: sirve para leer cuanto falta, no es borde.

**Verificacion propia** (zigzag independiente, `verif_r17.py` del scratchpad de la sesion 233bf8d4): MES 23/24/25-11 piernas de X con
delta a favor 96,8 / 98,9 / 98,6 % (357/364); 25-11 pivotes 4X identicos al segundo y al cuarto de punto; deltas a +-1 %
(-3.867 vs -3.847, +10.724 vs +10.830; la pasiva +186 vs +145). El agente rehizo 09-11 ES (86,05 % y 47,67 %).

**Auditoria de sesgos (RESULTADOS.md seccion 2):** potencia baja, escala de swing para un scalper, todo lo micro en MNQ, look-ahead
en `rebote_niveles.py` y en los placebos "misma media hora, mismo lado": el "nada" viejo es "inconcluso", no "el flujo no informa".

**Why:** el operador pidio entender POR QUE se mueve el precio, factor por factor y por zona, para seguir la ola y no chocarla
(ver [[explicar-el-movimiento-no-azar]], [[de-menor-a-mayor-muestra]], [[teoria-dura-y-confluencia]]).

**How to apply:**
- Explicarle el movimiento con esta anatomia (quien vacia la punta, firma del giro despues, lugares donde se arman extremos).
- Propuesta para el Superindicador, previews mostradas el 26-11: A renglon de la ola bajo la fecha; B ola dibujada (lleno = con
  agresion, punteado = pasivo, etiqueta puntos + delta); C ola (X) + marea (4X). Ninguna flecha LARGO/CORTO; implementar solo con su OK.
- Proximo paso de investigacion: la pieza de MOMENTO que le falta a la "zona de extremo" (absorcion + empuje), y medir en vivo
  la lectura segun PRUEBA_EN_VIVO.md. Ver [[superindicador-velas-delta-perfil]], [[baul-de-pistas-no-descartar]].
