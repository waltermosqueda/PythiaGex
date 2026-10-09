---
name: cache-velas-atas-podada
description: "ATAS poda su Cache_v2 de velas (06-10-2026: MNQ m2/m5 solo desde el 08-09; MNQ m1 y NQ/ES ya no estan). La exportacion de laboratorio/dom/ronda3/velas (julio..21-09, MES m5 276 sesiones) es IRREMPLAZABLE: nunca volver a exportar encima. Exportaciones nuevas van a laboratorio/tres/datos/velas_cache_<fecha>/ y se UNEN por t."
metadata:
  node_type: memory
  type: project
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-06T18:08:22.614Z
---

**Medido 06-10-2026** con `laboratorio/dom/ronda3/atas_cache_velas.py inventario`: MNQ@CME_Ind m2/m5/m15 19 carpetas (2026_09_08..2026_10_04),
MNQZ6 m2/m5/m10 igual, MES m1 20 carpetas (09-07..10-04), m1440 desde 2024. Lo que el 21-09 tenia 33-36 carpetas y MNQ m1 de 34 sesiones ya no
esta en la cache: ATAS la recorta (~20 dias en marcos chicos). Un `exportar` sobre ronda3/velas habria PISADO los CSV largos con los cortos
(por eso la corrida en segundo plano de hoy "fallo" sin salida: suerte).

**Regla:** `laboratorio/tres/exportar_velas_nuevas.py` exporta a `laboratorio/tres/datos/velas_cache_<hoy>/` (hoy: MNQ m2 13.110 velas,
MNQZ6 m2 13.041, MES m1 27.598; MESZ6/NQ/ES vacios). Los cargadores unen viejo + nuevo por `t` (el nuevo pisa en fechas repetidas).
Exportar cada 2 semanas para no perder dias. La carpeta `dia_carpeta` es la fecha UTC en que ABRE la sesion (22:00 UTC).

**Why:** 276 sesiones de MES m5 con delta es la muestra mas grande del proyecto y no se puede volver a bajar.
**How to apply:** antes de cualquier `exportar`, mirar `inventario` y comparar con los CSV existentes. Ver [[fuentes-datos-historicos]].
