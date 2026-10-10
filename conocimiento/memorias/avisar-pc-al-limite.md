---
name: avisar-pc-al-limite
description: "Regla del operador (09-10): avisarle cuando la PC está al límite (CPU/RAM) y decirle qué programas suyos cerrar; mis tareas tienen prioridad 1 sobre todo lo demás"
metadata:
  node_type: memory
  type: feedback
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-09T19:18:37.943Z
---

Textual (09-10-2026 ~16:20 ART): "lo de la PC es culpa mía porque tenía programas más abiertos y Edge también; tenés que avisarme
cuando están al límite y no podés usar más agentes, y los cierro, porque lo otro no es importante; tus tareas siempre tendrán
prioridad 1 para mí".

**Why:** la madrugada del 09-10 la PC estuvo al 100 % de CPU con 1,5 GB libres de 16 y frenó a los agentes; él prefiere cerrar lo suyo.

**How to apply:** antes de lanzar trabajos pesados (workflows con varios agentes que corren Python/compilan) y si algo tarda de más,
medir CPU y RAM (Win32_Processor LoadPercentage, Win32_OperatingSystem FreePhysicalMemory) y listar los procesos que más consumen.
Si CPU > ~85 % sostenido o RAM libre < ~2 GB: avisarle en una línea con los programas SUYOS que más pesan (Edge, WhatsApp, etc.) para
que los cierre. Nunca cerrarlos yo. ATAS y sus procesos de infraestructura (cboe_local, motor.py, etc.) no se tocan sin su OK.
Ver [[cambios-atas-de-a-uno]] (nada pesado con mercado abierto que trabe ATAS).
