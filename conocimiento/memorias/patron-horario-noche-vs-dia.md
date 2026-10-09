---
name: patron-horario-noche-vs-dia
description: "Investigacion 18-09 (19 agentes + revisores): ningun activo tiene patron horario comprobado Y cobrable; lo unico estructural es la 'prima de la noche' (cierre->apertura), chica, sin ventaja sobre mantener y comida por costos; el 'caro a la mañana / barato al cierre' no existe."
metadata: 
  node_type: memory
  type: project
  originSessionId: b326083e-692e-4b3a-a94e-5ca9c5723558
  modified: 2026-09-19T00:42:10.381Z
---

Pregunta del operador (18-09-2026): ¿hay activos que esten caros a la mañana/mediodia y baratos al cierre, o que suban de noche, para comprar al cierre y vender en la apertura en ciclo? Sus horas son ART: 10:30 = apertura NY y 17:00 = cierre, con EE.UU. en horario de verano.

Resultado (medido con Yahoo 1993-2026, velas de 1 h 2023-2026, ESU6 de Databento, mas literatura con citas verificadas):
- **Patron literal dentro de la rueda: NO existe.** SPY cierra mas barato que a las 11:30 NY solo el 45,1 % de 721 dias (recalculado). 0 de 231 pruebas hora x activo pasan Holm.
- **Prima de la noche: existe pero no se cobra.** SPY 1993-2026 da ~+10 %/año de noche contra ~1-2 % de dia. Desde 2010 la ventaja de la noche sobre el dia ya no es significativa, y solo-noche NO le gana a mantener (2020-26: 9,2 contra 15,3 %/año). El equilibrio es de ~4 pb por vuelta: con 5 pb por lado da negativo y con CEDEAR (~1,2-1,3 % ida y vuelta) se come todo. NightShares NSPY/NIWM cerraron en 2023.
- **ES/MES noche 16:00->9:30 ET:** ~+3 pt por noche (t 1,6), achicandose por año. ESU6 jun-sep: +5,6 pt, desvio 39 pt, t 1,17 (recalculado). La peor racha fue -1.060 pt (abr-2025). Margen nocturno ~USD 2.863 por MES segun AMP (el de CME no se verifico) contra USD 40 intradia. Es una apuesta larga, no un patron.
- **IBIT: se dio vuelta.** 2024: noche +159 % / dia -27 %. 2026: noche -20 % / dia +16 % (recalculado). Es el ejemplo de un patron que muere al hacerse famoso.
- **ADR argentinos/ARGT:** el diferencial noche>dia mas robusto (fuera de muestra y con Holm), pero puede ser la impresion de la subasta y las puntas no se midieron. La cola: la noche de la PASO 2019 fue YPF -31 %, GGAL -49 %, ARGT -20 % (recalculado). En 2024 se dio vuelta en YPF.
- **Vencidos:** la deriva de 2-3 AM ET del ES (NY Fed: cero desde 2021), el pre-FOMC, el efecto de moneda local de Ranaldo y el momentum de la ultima media hora sin condicionar.

La unica hipotesis viva que conecta con su metodo es el momentum de la ultima media hora solo con gamma negativa de los dealers (Baltussen 2021, hasta 2020). Esta sin medir: hay que pre-registrarla en la bitacora con el GEX propio.

Archivos: `investigacion/patron_horario/` (bajar_datos.py, datos/, 107 scripts, resultados/juicio.txt y workflow_completo.json, graficos/*.png).

**Why:** el operador va a volver a preguntar variantes ("¿y si compro al cierre del MES?"). Ya esta medido y la respuesta tiene numeros.
**How to apply:** ante cualquier idea de "horario que paga", mostrar primero la ventaja sobre mantener, los costos y la vigencia. Relacionado: [[busqueda-gatillo-2026-09-17]], [[gatillo-cientifico-2026-09-10]], [[laboratorio-formulas]].
