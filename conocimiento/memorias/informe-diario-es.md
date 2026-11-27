---
name: informe-diario-es
description: "17-11: informe diario ES 1.3 (PythiaGex/laboratorio/informe_diario/informe_diario.py) con 'En 30 segundos' + escalera arriba y lo demas plegado; verificador independiente 169 chequeos; R1-R4 medidos: ningun nivel le gana al placebo, el tamaño si (straddle x1,86 y factor por hora). Uso privado (X-Ray)."
metadata:
  node_type: memory
  type: project
  originSessionId: b1346044-cbec-4f19-80c3-ffb1de06776c
  modified: 2026-11-17T03:11:26.293Z
---

Pedido del operador 16-11: "un informe claro cada dia de los niveles clave, que pasa al llegar, regimen, objetivos honestos, como
vamos, X-Ray y sus dos teorias". Construido por el workflow wf_8d713e6e-042 (20 agentes, cada investigacion con 2 verificadores,
auditor + critico) y terminado a mano el 17-11 (version 1.3).

- Correr: `python informe_diario.py` en `PythiaGex/laboratorio/informe_diario/` (IDLE y 2 hilos adentro; ~1,3 s, ~115 MB).
  Sale `informes/informe-AAAA-MM-DD-HHMM.html/.md/-numeros.json` y agrega UNA fila a `informes/sellos_pronosticos.csv`
  (pronostico sellado; solo se agrega, nunca se reescribe). `--hora HH:MM --fecha AAAA-MM-DD` simula sin sellar.
  Comprobar: `python verificacion_informe/verificar_informe.py informes/<...>-numeros.json` (otra implementacion desde el crudo;
  rechaza informes de otra version del script por sha). Horarios propuestos NY: 08:45, 09:50, 14:30, 16:15 (sin programar: falta su OK).
- Lo medido (48 ruedas 08-09..13-11, un dia un voto): ningun nivel (muros, flip, max pain, redondos) se alcanza, rebota ni atrae
  el cierre mas que una raya corrida 7,5-12,5 pts; rebote pierde 0,6-1,2 pts/op con costo 0,70 (R1, 602 pruebas, 1 pasa y por
  azar 4,2). Regimen (lado del flip, signo gamma) no cambia tamaño relativo ni tipo de dia (R2); tendencia ~1 de cada 5 (10 de 45).
  Tamaño: rango ~1,86 straddles de apertura; dentro del dia, lo hecho x factor de la hora (ver [[straddle-no-es-el-rango]]).
  X-Ray: relata, no anticipa (ver [[xray-historia-medida]]). R4: el 16-11 SPX 7400 coincidio con el minimo (1 evento).
- 1.3 (17-11): factor por hora (critico A1), resumen de una pantalla, avisos y laboratorio en desplegables, frases corregidas
  (OI de los muros, tendencia por tramo, noche = cota inferior, 'no se detecto', ES vs SPX en puntos, 'sigue de largo' es direccion),
  market maker/firms/brokers en X-Ray, nombres de archivo que no pisan un sello. Capturas: el Edge sin ventana arma a 492 px minimo;
  para probar celular, meter el informe en un iframe de 390/320 px.
- Pendiente del critico (no hecho): max pain de la mensual 20-11 aparte, analogos de R1 para rayas sin estadistica, sesgo de la
  Basis de noche (-1,12/rueda; con acarreo -0,16), cantidad de cruces del zero 0DTE en el texto, jerga del cuerpo plegado (ATR,
  RTH, pp, Spearman, ATM).

**Why:** el operador queria anticipar el regimen y objetivos honestos; lo honesto medido es el TAMAÑO, no el lugar ni el lado.
**How to apply:** entregar el HTML local (nunca publicarlo: tiene X-Ray); arriba lo de 30 segundos; nunca 1x straddle como techo.
Ver [[comprobacion-fehaciente-en-pantalla]], [[como-ensenarle-trading]].
