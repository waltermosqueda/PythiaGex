---
name: estrategia-ronda-2
description: "21-09: ronda 2 (7 reglas de minutos con stop detras de estructura): 0 de 7 pasan t>=2,5 en confirmar, pero H8 (falla del rango de la 1a hora de la noche -> medio) y I1 (elastico del VWAP en rueda) siguieron positivas; se validan congeladas en reserva + ruedas nuevas en la ronda 3."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b882b35-31bc-4131-8cd4-4b099f670c25
  modified: 2026-09-22T03:23:40.925Z
---

Workflow estrategia-ronda-2, codigo en `PythiaGex\laboratorio\dom\ronda2`. Confirmar = 08-09..17-09, costo 0,96.

- H8 trampa de la primera hora de la noche (rango 18:00-18:59 NY, ruptura que falla, objetivo el medio, stop tras la
  mecha ~15 pts): explorar +16,6/op n 12; CONFIRMAR +9,52 n 8 (5 de 8, t 1,34, z 1,04 placebo hora); espejo -13,1;
  juntas n 20 +13,8 t 2,78. Paso su criterio laxo, no el t>=2,5. Casi subconjunto de G6 (9 de 12).
- I1 elastico del VWAP (rueda, estiron que se detiene, objetivo VWAP, stop tras extremo 30 min): explorar +18,6 n 38;
  CONFIRMAR +7,2 n 23 t 0,86; juntas n 61 +14,3 t 2,67. Bandera: el placebo de la misma media hora no la separa.
- No: F-A ruptura fallida nocturna en nivel (+0,87 t 0,18), F-B gamma negativa sigue de largo (-6,3), G6 falsa ruptura
  del extremo de la hora (noche -3,67), H3 amague de Londres (-12,9 n 5), J3 resorte de la 1a hora (-1,47).

**Ronda 3 (22-09):** H8 congelada en reserva+nuevas: +4,88 n 14 (dudosa); total 36 noches +10,1 t 2,34. En 243 noches
de MES 5 min NUNCA miradas (08-2025..07-2026) PIERDE -0,88 pts, 34 % (n 143, t -2,31): era suerte de la muestra chica.
I1 en reserva+nuevas -3,95 (n 56): muerta. Leccion: 36 sesiones no alcanzan para validar nada; usar la historia larga.

**Why:** el unico hilo repetido en rondas 1-2 es la REVERSION AL VALOR JUSTO con stop ancho (H8, I1, C4 54-59 %); las
falsas rupturas cortas y todo lo de segundos no aguanta. El limite es la muestra (8 y 23 casos en confirmar).
**How to apply:** ronda 3 (estrategia-ronda-3, `laboratorio\dom\ronda3`): H8 e I1 CONGELADAS en reserva (30-07..19-08) y
noches/ruedas nuevas (18-09, 21-09); familia de reversion; intento de leer velas largas de Cache_v2. La cinta de ATAS
alcanza ~7 semanas hacia atras (07-28/29 dieron archivo vacio). Databento bloqueado por el limite del operador.
Ver [[estrategia-dom-ronda-1]], [[busqueda-estrategia-sin-parar]].
