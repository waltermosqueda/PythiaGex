---
name: tqqq-referencia
description: la referencia sobre TQQQ es la referencia visual que el operador quiere replicar; solo mirar, nunca tocar su backend
metadata:
  type: reference
---

El operador abre <sitio de la referencia> en el navegador integrado (pestaña propia) con el indicador **la referencia** (modo full) sobre **TQQQ 1m**, mas VWAP, EMA 9, Unusual Flow, <estudio de flujo de la referencia> (presion de cobertura) y una tabla de flujo por activo (SPX, SPY, QQQ, NDX, TQQQ, VIX, GLD con estrellas).

Regla del operador (08-10): "ojo con los bots, solo para analizar y replicarlo en el nuestro; nada de urgar su backend ni nada que nos bloquee o detecte". → Solo get_page_text / screenshot de lo que ya muestra la pestaña; nunca sus APIs, nunca tabs_select, nada automatizado contra ese sitio.

Lo que dibuja (visto 08-10 14:19 ART): barras por strike entero de TQQQ (izquierda roja/verde agua; derecha verde agua/violeta, probablemente OI y volumen, SUPUESTO), rayas amarillas = dominantes por dia (07-10: 84 y 83; 08-10: 82 y 81,5), gris punteada = zero (~82,6 y ~80,6). Ese dia TQQQ se apoyo en 82 de 12:40 a 14:00 y cayo a 79,9 a las 14:10, justo en 80 (la zona mas cargada).

Hipotesis del operador: TQQQ (x3 diario del NDX) marca rumbo/rechazo/cambio de tendencia en sus strikes fuertes porque los apalancados no aguantan perdidas. Workflow capa-tqqq-la referencia (laboratorio/tres/tqqq/) baja la cadena de TQQQ de CBOE una vez y la pasa a precio de NQ. Ver [[pythiagex-3-0-estado]].

**Medido 08-10 (t02/t04/t06 en laboratorio/tres/tqqq/):**
- Conversion correcta TQQQ -> NQ: NQ = s x K + c, s = cierreNDX / (3 x cierreTQQQ) (124,2 pts por dolar, fija en el dia), c sincronizado a sello CBOE - 900 s (sd 2 pts en 177 muestras). NO usar F x (1+(K/S-1)/3) de la clasica/2.0 (erro 10-22 pts) ni razon simple.
- la referencia sobre NQ indexa TQQQ con RAZON SIMPLE (como QQQ): sus 15 rayitas amarillas = un solo strike 82,05 +- 0,15; pendiente nivel vs precio -2,33. Todo queda 3x mas lejos del precio y se abre en abanico al reves del precio (14:37: el 82 a ~31.825 en vez de 31.195; el 80 a 31.048 en vez de 30.946). En el grafico propio de TQQQ esta bien. El operador lo vio primero.
- TQQQ no tiene 0DTE martes a jueves (vence lunes y viernes). Bien convertido, hoy no le gano al azar (3/8 contra 48 %) y el 82 era el mismo lugar que QQQ/NQ/NDX.
- Idea del operador (08-10): un 3.x/4.0 "integrado" con selector de derivado (NQ/NDX/QQQ/TQQQ), cada uno con su conversion correcta. La 3.0 no tiene capa TQQQ. Antes de tocar atas/: preview y su OK.
- Revision 08-10 15:2x (t07/t08/t09): 56 de 56 rayitas de su capa TQQQ sobre NQ caen en strike entero por razon (9 de 56 por x3); RMS 5,5 pts contra 165+ de cualquier alternativa. En su grafico propio de TQQQ la amarilla esta en 82,00; las mechas giraron en 81,71-81,81 (10:55, 12:56, 13:04). OJO: "el 82 vivo en 31.825" y "la amarilla viva = su zero" son calculado/supuesto, no visto. Preview: laboratorio/tres/tqqq/png/tqqq_integrado_vs_referencia_0810.png.
- Doble eje TQQQ de la referencia sobre NQ (captura 15:43 del 08-10): razon fija anclada al ultimo precio (386,7 pts/USD vs 124,2): muestra 1/3 del movimiento. Sus amarillas en TQQQ = 2 strikes de mayor |GEX vol| de la SEMANA; calzan con la hora del DATO de CBOE (vivo o atrasado: no se sabe).
- Construido 08-10 16:20 ART: laboratorio/tres/tqqq/tqqq_vivo.py (pythonw, cada minuto, BELOW_NORMAL; --parar/--estado) escribe profundidad/pagina/preview_datos/tqqq_vivo.json; preview.js lo suma (grupo "TQQQ x3", vistas "TQQQ x3" y "TQQQ vs la referencia", boton "eje TQQQ x3"). Respaldo de preview.js/html previos en el scratchpad de la sesion. Falta: noche (reanclar s y c al cierre) y portarlo a ATAS (copia, apagado, con OK).
