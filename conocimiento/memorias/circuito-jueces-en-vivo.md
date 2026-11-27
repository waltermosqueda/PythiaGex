---
name: circuito-jueces-en-vivo
description: "Desde el 30-11-2026, cada dia habil despues de las 17:00 NY: herramientas/cinta_y_jueces.py baja la cinta de ES por la sonda, la enlaza como cinta-ES-... (la R21 no ve 'ESZ6') y corre los jueces congelados R21 (COMB-T7+V4) y R23 (A3); trampas del roll de diciembre"
metadata:
  node_type: memory
  type: project
  originSessionId: 233bf8d4-5113-4e95-a0d9-68bff726690d
  modified: 2026-11-27T12:40:30.582Z
---

**Que hace** `PythiaGex/herramientas/cinta_y_jueces.py` (27-11-2026):
1. Escribe `%APPDATA%\ATAS\PythiaGex\flujo\sonda-ESZ6.txt` = `cinta;<18:00 NY de la vispera en UTC>;<17:00 NY en UTC>;1;1380`.
   El Superindicador 1.9 lo toma en ~2 s. Si no lo toma en 20 s, ATAS no esta con el Superindicador en ese grafico: borra el pedido y corta.
2. Espera el `.listo` (hasta 40 min).
3. Hace un ENLACE DURO `cinta-ES-<desde>-<hasta>.csv` (+ `.listo`), sin disco extra. **Trampa:** `r19_comun.cintas_crudas('ES')`, que usa el juez de la R21,
   solo acepta 'ES' exacto. El grafico de pruebas nombra sus cintas `cinta-ESZ6-...`, asi que sin el enlace la R21 no veria ninguna jornada.
   La R23 acepta `cinta-ES*` y elige `cinta-ES-` en el empate.
4. Corre `ronda21_regimen/r21_11_vivo.py` y `ronda23_diseno/r23_vivo.py`. Log en `laboratorio/bitacora_vivo/<dia>.txt`.

**Uso:**
- Normal: `python -I herramientas/cinta_y_jueces.py` (hoy) o con `AAAA-MM-DD` para un dia pasado.
- `--verificar` corre los jueces con `--verificar <dia>` y no escriben. Probado 27-11 con el 24-11 `--sin-pedir --verificar`: los dos COINCIDEN.
- `--hasta-ahora` baja la cinta PARCIAL de hoy, sin enlazar ni juzgar (controles en la rueda, La Ola).

**Roll de diciembre:**
- Desde el 07-12 la R21 exige el contrato de cada cinta en `ronda21_regimen/vivo_contratos.json`; si no, excluye la jornada. El log anota el instrumento.
- Z6 vence el 18-12: el grafico de pruebas tiene que pasar a ESH7 y se usa `--instrumento ESH7`.
- La R23 no depende del contrato.
- 27-11 y 24-12 son ruedas cortas.

**Pendiente:** tarea programada lun-vie 19:15 AR. Es configuracion persistente, asi que va con el OK del operador. Ver [[busqueda-salto-de-calidad-rondas-19-21]], [[sonda-cinta-historica-atas]], [[arranque-automatico-atas]].
