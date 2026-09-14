---
name: nube-roll-yahoo-2026-09-14
description: "La web de contingencia estuvo mal toda la rueda del 14-09: la base 'medida' de diciembre (+71) se colaba con el precio de septiembre de Yahoo (S 65-70 pts abajo del indice), y al rodar ES=F/NQ=F a diciembre a las 15:50 UTC el NQ quedo 300 pts arriba; arreglado infiriendo el contrato por la base por precio. Ademas el subidor murio a las 00:38 con 'gh auth login' aunque gh funcionaba: relanzarlo lo arregla."
metadata: 
  node_type: memory
  type: project
  originSessionId: cfe2e2e1-5319-4bee-a9ea-7507c655c3e4
  modified: 2026-09-14T16:11:47.858Z
---

Encontrado el 2026-09-14 (13:00 local) auditando `estado-ES.json` / `estado-NQ.json`
y `serie-*-2026-09-14.jsonl` de la rama `cadenas`.

## El error (verificado en la serie de la nube y reproducido con el codigo viejo)

- Toda la noche y la mañana (03:26 a 14:47 UTC) la nube tomo `futuro` = Yahoo
  ES=F (todavia septiembre, 7600-7625) y base = "medida" 71 (forward de
  DICIEMBRE menos forward del 0DTE, correcta para dic). S = 7600 - 71 = 7530
  con el indice en 7600: **todo el perfil repreciado 65-70 pts abajo**, zero
  publicado 7710 contra 7627-7642 del indicador. NQ estaba bien (base TEORICA
  10 porque la cruda de dic, 315, no cabia en el carry de sep).
- A las 15:50 UTC Yahoo rodo ES=F y NQ=F a diciembre (7697 / 29462). ES quedo
  bien de casualidad (7697 - 71 = 7626); NQ paso a S = 29466 con NDX en 29155:
  **300 pts arriba**, zero 29035 y muros sin sentido.
- Causa: `elegir_base` acepta la base "medida" si cabe en el carry del
  trimestral cercano O del siguiente (`carry_alt`), y `correr()` no sabe que
  contrato es el futuro de Yahoo.

## El arreglo (commits a2132479 y 67841250, sin push: lo corre el operador)

En `correr()`, con la base por precio fresca (mediana de fut - indice de Yahoo
en el mismo minuto, <= 60 min) se elige el trimestral cuyo carry teorico queda
mas cerca y se anula el escape al siguiente. Probado con las cadenas reales
de las 16:09 UTC: ES S 7631,6 (contrato 2026-12-18, carry 51, base 70,8), NQ
S 29156,8 (base por precio 314,7). El estado publica `fut_contrato` y el
`fut_origen` dice "contrato Dec26". Primer intento fallo al escribir
(`exp_futuro_alt.isoformat()` con None): corregido.

**Queda una salvedad hasta que el operador ruede su grafico:** la nube dibuja
en el marco del contrato de Yahoo (diciembre: ES 7702, NQ 29471) y su ATAS
sigue en septiembre (7630 / 29170). Los niveles de la nube estan ~71 pts (ES)
y ~315 pts (NQ) por encima de su grafico hasta el roll. La tarjeta "Niveles
de tu ATAS" del vivo si esta en septiembre.

## El subidor

`pythonw subir_vivo.py --bucle` (arrancado 13-09 20:01) fallo desde las 00:38
del 14-09 en cada vuelta con "To get started with GitHub CLI, please run: gh
auth login", mientras `gh auth status` y `gh api user` desde un proceso nuevo
funcionaban (keyring, cuenta waltermosqueda). No se encontro la causa; matar
el proceso y relanzarlo (`Start-Process pythonw herramientas\subir_vivo.py
--bucle`, cwd PythiaGex) subio al instante (13:05 local, 130 KB cada 20 s).
Ver [[web-gamma-hoy-nube]]. Pendiente: que el subidor se relance solo tras N
fallos seguidos.

## Cadencia real del archivo

`cadenas.yml` no corre cada minuto: hoy entre 13:00 y 16:00 UTC hubo 13-14
cadenas distintas por raiz (cada 10-20 min). SPY no se archiva (solo ES, NQ,
QQQ) y la referencia usa SPY en la mitad de sus paneles de ES: agregar SPY al
`--bajar` de cadenas.yml.

**Why:** la web es el respaldo si la PC se cae; hoy habria dado un mapa 70 pts
corrido sin avisar.

**How to apply:** ante un salto de los niveles de la nube, mirar
`fut_origen`/`fut_contrato` en estado-*.json y la serie del dia. Ver
[[conversion-spx-a-es]], [[auditoria-2026-09-14-referencia-es]].
