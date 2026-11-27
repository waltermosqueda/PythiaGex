# Ronda 24 — A3 a 30 minutos en MES 2024-25 — PRE-REGISTRO

Escrito el **27-11-2026 a las 09:31 AR (UTC−3, reloj del sistema), ANTES de correr la prueba.** Ninguna operación de 2024-25 fue mirada
con esta regla. Correcciones: siempre como nota nueva al final, con hora. Nada de esto promete rentabilidad.

## 0. Qué se prueba
**A3-30:** la regla A3 congelada en la R23 (`ronda23_diseno/cand_A3_tarde_giro_amplia.py`) llevada a velas de 30 minutos **sin ningún
parámetro nuevo**: X, VWAP, IB, las tres condiciones, stop, objetivo, cierre a las 16:00, una a la vez, costo 1,00 (detalle en el
encabezado de `r24_a3_30.py`). Lo único que cambia es la resolución: se mira en 6 cierres (13:00 a 15:30 cada media hora) en vez de 31, y
las salidas se resuelven con velas de 30 minutos (misma vela con stop y objetivo = stop).

**Es una HERMANA más débil de A3, no A3.** La calibración en el año ya visto (`calibracion.txt`, `diagnostico.txt`, sin tocar nada de 2024-25):
- A3-30 en sep-2025..feb-2026: 71 % de acierto, +2,2 por operación (A3 en 5 min: 74,3 %, +2,49);
- A3-30 en mar..ago-2026: 48-52 %, −3,5 a −4,4 (A3 en 5 min: 67,2 %, +0,59);
- la mayor parte de esa diferencia viene de mirar menos cierres: A3 en 5 min mirando solo los cierres de en punto y de y media da en
  mar..ago-2026 57,7 % y −1,65. La resolución de la salida pesa menos (la regla de camino O-H-L-C da lo mismo que la conservadora).

## 1. La muestra
- **MES, velas de 30 minutos nativas** de la caché de ATAS (`laboratorio/dom/ronda3/velas/velas-MES-m30.csv`, solo lectura).
- **337 ruedas completas (13 velas de 9:30 a 16:00 NY) del 29-04-2024 al 08-09-2025.** Las 10 ruedas previas (15 al 26-04-2024) solo arman
  la mediana de la primera X. Termina el día antes del diseño de la R23 (09-09-2025).
- Nadie eligió A3 ni A3-30 con estas ruedas. La R22 las usó para otra pregunta (el semáforo D1 y el "elástico", que la R23 mostró que era la vara).

## 2. La vara (decide; UNA sola corrida)
**PASA** si se cumplen las tres:
1. acierto (operaciones con puntos > 0) **≥ 60 %**;
2. neto por operación **> 0** con costo 1,00;
3. ventaja sobre el azar con la misma ubicación (puntos reales − puntos del azar por operación, 200 caminos de velas reflejadas, error
   agrupado por rueda) con **t ≥ 1,96**.

Si falla cualquiera: **NO PASA**. Se informan sin decidir: cada mitad (2024 y 2025), los motivos de salida, el neto del azar y la fórmula P.

## 3. Cómo se lee
- **PASA:** suma evidencia a favor de la idea (la vuelta al VWAP de la tarde después del giro) en otro año y otro clima. No prueba A3 en
  5 minutos; los jueces de A3 siguen siendo el vivo (desde el 30-11) y ES 2023 si se compra.
- **NO PASA:** dice que la idea depende del período, como mar..ago-2026. No tumba a A3 por sí solo, porque la hermana es más débil, pero
  **baja la expectativa** del juez en vivo y hay que decírselo así al operador.

## 4. Huellas y candado
- `r24_a3_30.py` SHA-256 `288f3c9f123fbdc6bed9cb38207f1fe5b8a3c3255d9ec659b555395f33724be9`; semilla del azar 2024.
- `python -I r24_a3_30.py prueba` verifica las huellas de `huellas_r24.json`, escribe `PRUEBA_CORRIDA.lock` ANTES de calcular y se niega a correr
  otra vez. Deja `ops_prueba_2024_25.csv` y `resultado_prueba.json`.
