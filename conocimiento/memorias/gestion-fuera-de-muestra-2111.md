---
name: gestion-fuera-de-muestra-2111
description: "21-11: sus operaciones 23-09..20-11 (43 sesiones, 1.978 ciclos, neto -27.945): fuera de muestra se sostienen TOPE 3-4 contratos y NO PROMEDIAR; el stop 200 y el reloj 5 min NO (corregido de frente). Sigue persiguiendo; entrada = azar. ATAS Risk Manager no tiene tope de contratos."
metadata:
  node_type: memory
  type: project
  originSessionId: f223fe47-37a3-4f31-9f95-4d26b0bbe4d6
  modified: 2026-11-21T03:30:16.930Z
---

Workflow wg7vgzmja (reconstruccion + 3 mediciones + verificador independiente), carpeta
`PythiaGex\laboratorio\dom\ronda8\adelante_2011\` (INFORME_OPERADOR.md, PREREGISTRO_GESTION_FUERA.md, verificador\).
Recalculado por mi desde ciclos.parquet el 21-11: neto -27.945,00; t por sesion -3,41; 19/43 positivas;
<=3 contratos (micro-eq) +3.223,5 / 4+ -31.168,5; comision 11.246,5 (40 %, 162 ops/dia vs 46,5 en sept);
acierto por ciclo 70,3 %, ganancia media 31,66, perdida media 119,94 (3,8x; empate 79 %).

- **Se sostienen (criterio pre-registrado t>=2 y las dos mitades):** tope 4 +17.390 (t 2,78), tope 3 +18.740 (t 2,80),
  no promediar +16.390..16.720 (t 2,8). Juntas ~+20.900; aun asi el neto queda negativo (~-7 mil).
  Reparos: 36 % de la perdida viene de 5 cuentas con fills imputados (E2T, <cuenta>, LFE-015); en las 29 verificables
  t 2,01-2,34 (no pasa Bonferroni 2,49). Buena parte de la mejora sale de los 29 cierres de cuenta (-16.108,5).
- **NO se sostienen:** stop 200/ciclo (efecto -1.000..+8.200, t <=1,13; con cinta 670 ciclos -2.216) y reloj 5 min.
  En septiembre yo lo habia presentado como +7.290: error corregido en el mensaje del 21-11.
- **Gatillo:** sigue persiguiendo (+32 pp vendiendo, +29 pp comprando vs azar, 14/14 sesiones MES); paso a limitadas
  (77 %); d60 = azar; a 60 s despues de costos -2,02 USD por contrato MES. Ahora la mitad de los agregados en MES son
  piramide a favor (+4.054, t 2,6) y la otra mitad promediar en contra (-30.380, t -3,9).
- **ATAS 8.0.15 Risk Manager (leido en OFT.Localization.dll):** perdida diaria (realizada y flotante), objetivo diario,
  perdidas seguidas, operaciones maximas por dia, incluir comisiones, modo aviso / solo reducir / bloqueo, frase para
  cambiar limites. NO tiene tope de posicion: el tope lo hace el guardian de PythiaBot ([[pythiabot-1-0]]) o el.
- Lucid microscalping: solo 0,8 % de la ganancia bruta en ciclos <= 5 s: sin riesgo (no hace falta decirlo).

**How to apply:** las reglas a proponer son tope + no promediar (+ menos operaciones por la comision, sin numero
medido); el stop de 200 queda como seguro opcional, nunca como "mejora". Nada de configurar ATAS ni encender el
guardian en una cuenta real sin su OK. Ver [[estrategia-ronda-8-edge-personal]], [[estrategia-ronda-9-patron]].
