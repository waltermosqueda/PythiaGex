---
name: auditoria-en-vivo-2026-09-09
description: "Auditoria en vivo de Gamma Hoy (noche del 09 al 10-09): la logica reproduce a mano en strike; dos fallas reales, verificadas varias veces y arregladas: la base rota por el roll a diciembre y el Max Change indexado por precio del futuro."
metadata:
  type: project
---

Metodo: laboratorio/auditar_vivo.py rehace todo en STRIKE con la cadena
archivada y lo compara con la linea AUDIT del indicador; CBOE bajado aparte;
cuenta manual del carry; centinelas del indicador contra recomputo.

- **Bien**: neto, zero gamma (los dos libros), majors, dominante con
  centroide, pico, OI, dias al vencimiento, gamma: todo coincide en strike.
- **Mal 1, la base** (verificado 5 veces): a las 21:08 UTC del 09-09 la
  cruda de la nube paso de 28,7 a 322,2 (NQ) y de 7,1 a 72,7 (ES) porque
  pythiagex.base.contrato_vigente() rola 8 dias antes del vencimiento
  (DIAS_ROLL=8) y desde entonces mide el forward de DICIEMBRE; el grafico
  sigue en septiembre. Todos los niveles de MNQ quedaron ~294 pts arriba.
  Arreglo 1.5e: el nucleo acota la base con el carry teorico del contrato del
  grafico (Security.Expiration, o codigo, o trimestral supuesto); orden
  medida > medida reciente > de la rueda > cruda > TEORICA. Ver
  [[conversion-spx-a-es]].
- **Mal 2, el Max Change** (verificado 3 veces): fotos por minuto indexadas
  por strike + base; al cambiar la base (148 de 310 minutos en NQ) nada
  coincide y el "cambio" es la barra entera: mc30 = major/dominante en 237
  de 237 minutos. Arreglo 1.5f: indexar por strike; medido antes/despues en
  la misma rueda: mc30 = barra grande 99 % -> 70 % en MNQ 2 min.
- **Ojo**: la cadena de NDX en la nube cambia cada ~4 min; entre cadenas el
  Max Change es solo repreciado por el spot, no operaciones nuevas.

**Why:** el operador pidio "auditar con maximo poder, no corregir sin
verificar varias veces". Las dos fallas no se veian en pantalla: los niveles
parecian razonables, solo que 294 pts mas arriba.

**How to apply:** ante cualquier duda, `python laboratorio/auditar_vivo.py NQ
--audit "<linea AUDIT>" --base <base de la rueda> --fut <precio>`; mirar en
el log "base:" y "vencimiento del contrato:". Reiniciar ATAS con
herramientas/reiniciar_atas.ps1 (reintenta Connect solo). Ver
[[vencimientos-0dte-auditados]], [[gex-formula-auditada]].

**Rueda del 10-09 (11:27-11:45 local):** con la MISMA cadena que usaba el indicador, todo
coincide a mano (neto, zero, majors, dominantes con centroide identicas, pico). Comparar
contra otra cadena de la nube da diferencias que no son errores: el feed y el archivo
llegan cada ~15 min (el cron de GitHub Actions no corre cada minuto). Tercera falla real
encontrada y arreglada (1.6c): BarraDe() sumaba 3 h a la hora de la vela (ToUniversalTime
sobre "Unspecified"); la base de la rueda salia 271/55 y las burbujas de Big Trades iban
3 h corridas. Yahoo por minuto confirma la base real NQU26-NDX = 12-28 pts. Max Change
en vivo: mc30 = barra grande 38 % (ayer 100 %). Gatillo ES 11:22 LARGO 7603: +17 / -1,75.
