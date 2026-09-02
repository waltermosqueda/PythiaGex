---
name: retraso-cboe-902s
description: "El CDN de CBOE llega exactamente 902 segundos tarde — medido en 14 de 14 corridas, sin dispersión. Decide qué se puede y qué no se puede construir encima."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 5abdaa85-a198-45f5-bf5a-ffcdc763812f
  modified: 2026-09-02T21:25:49.158Z
---

Medido el 2026-09-02 sobre catorce cadenas de `_SPX` guardadas el 2026-09-01
durante la rueda. Para cada archivo se comparó su propio `timestamp` (UTC)
contra el `last_trade_time` más alto de todos los contratos (que viene en
hora de Nueva York, sin zona):

```
retraso = timestamp_archivo_UTC − (max(last_trade_time) + 4 h)
```

**902 segundos en las catorce. Desviación cero.** Quince minutos y dos
segundos. No es una estimación ni un promedio: es un número clavado.

El archivo en sí se regenera cada pocos segundos (`Last-Modified` avanza,
`s-maxage=5`), lo que engaña: parece fresco y el contenido no lo es. Un
`timestamp` que avanza no prueba nada, igual que pasó con los tableros
auditados. Ver [[paginas-gex-auditadas]].

## Lo que esto habilita y lo que prohíbe

**Habilita** todo lo estructural: el interés abierto es de ayer de todos
modos, así que quince minutos no le hacen nada al GEX, al gamma flip ni a los
muros. También habilita estudiar una sesión terminada, donde el retraso no
molesta en absoluto.

**Prohíbe** cualquier gatillo de entrada en vivo sacado de ahí. Los BigTrades
calculados sobre CBOE describen lo que pasó hace un cuarto de hora. Decirlo
después del número no alcanza: va antes.

## Para el gatillo en vivo

La cadena de opciones de ES que ATAS ya recibe por Rithmic. `Security` expone
`StrikePrice`, `OptionType`, `OpenInterest`, `LastTradePrice`,
`LastTradeVolume` y las dos puntas, e implementa `INotifyPropertyChanged`, o
sea que empuja cambios. El acceso sería
`IIndicatorDataProvider.GetService<ATAS.DataFeedsCore.IOptionsDataFeed>()`.
**Todavía sin confirmar que ese servicio esté registrado para indicadores** —
`NivelesGamma.ProbarOpcionesAtas()` lo prueba y escribe el resultado en su
tablero; hay que mirarlo con ATAS abierto. Ver [[atas-opciones-es]].

## Cómo volver a medirlo

```bash
python -c "
import gzip,json,glob,datetime as dt
for f in sorted(glob.glob('datos/cache/_SPX-*.json.gz'))[-5:]:
    d=json.loads(gzip.open(f).read())
    ts=dt.datetime.strptime(d['timestamp'],'%Y-%m-%d %H:%M:%S')
    m=max(o['last_trade_time'] for o in d['data']['options'] if o.get('last_trade_time'))
    print(f, (ts-(dt.datetime.fromisoformat(m)+dt.timedelta(hours=4))).total_seconds())
"
```

Ojo con el horario de verano: el `+4 h` es EDT. En invierno son 5.

**Why:** sin este número medido, todo lo que se construya sobre CBOE se
presenta como si fuera en vivo, y no lo es. Con él, cada cosa va al cajón que
le corresponde.

**How to apply:** la constante vive en `pythiagex/bigtrades.py` como
`RETRASO_CBOE_S` y viaja en cada archivo que produce `radar.py`. Ver
[[radar-dominantes-bigtrades]] y [[calcular-gex-propio]].
