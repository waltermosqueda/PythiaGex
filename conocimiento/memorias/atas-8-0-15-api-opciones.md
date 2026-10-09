---
name: atas-8-0-15-api-opciones
description: "ATAS 8.0.15.302 (29-09-2026) trae la API publica de opciones (IOptionsDataProvider) y RECHAZA las suscripciones por fuera de ella: 200 contratos maximo 'outside the options API' y lookups a 1 cada 30 s. La clasica y la 2.0 (PuenteRithmic por reflexion) pegan contra ese tope desde el 02-10: libro vivo de NQ con 84-86 strikes y 'refused' en el log. La 3.0 va por la API."
metadata:
  node_type: memory
  type: project
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-06T18:08:22.585Z
---

**Medido el 06-10-2026** (`%APPDATA%\ATAS\Logs\app_2026100*.log`): desde el 02-10 02:27 aparece `Subscription of option ... refused:
200 options are already held outside the options API. Use IOptionsDataProvider` y `Option chain request ... refused: the rate of option
lookups outside the options API is exceeded` (15/18/18/9 por dia). Los avisos de latencia pasaron de 0-3 por dia a 49 (05-10) y 86 (06-10).
`OFT.Platform.exe` = 8.0.15.302 (instalado 29-09 08:47). CLAUDE.md decia 8.0.14.398: desactualizado.

**La API** (descompilada con ilspycmd, solo lectura, en el scratchpad de la sesion `sdk/`):
- `ATAS.Indicators.Indicator.OptionsDataProvider` (protegida) -> `IOptionsDataProvider { IsAvailable; GetOptionSeriesAsync(ct);
  GetOptionsAsync(series, ct); SubscribeToOption(Security) -> IOptionQuoteSubscription { Option; Summary; event Changed } }`.
  `SecuritySummary` trae BestBid/Ask (precio y volumen), LastTrade, SettlementPrice, OpenInterest, CurrentDayTotalVolume, PrevDayTotalVolume.
- Resuelve el subyacente como la Security DEL GRAFICO: en un grafico de MNQ da opciones de MNQ (iliquidas). Para NQ desde MNQ hay que usar
  `OFT.Platform.Core.Providers.Options.OptionsSubscriptionService` (clase PUBLICA en OFT.Platform.Core.dll): `GetOptionSeriesAsync(connector,
  underlying)`, `GetOptionsAsync(connector, series)`, `Subscribe(connector, option, owner)`, `ReleaseOwned(owner)`. La instancia es un campo
  privado (ofuscado) de `IndicatorOptionsDataProvider`: buscarlo POR TIPO.
- Limites: 512 suscripciones vivas por instancia de indicador; 3000 en total; rafaga 2000 con recarga 5/s; lookups 20 de rafaga, 1/s, cache
  10 min, timeout 30 s; al soltar hay 'hold' de 5 s / 30 s / 5 min / 15 min (anti-flapping). Lo que va por la API entra en
  `OptionRequestGuard.EnterBudgetedScope` y NO cuenta en el cupo de 200 'outside'. `OptionRequestGuard`: MaxDirectOptions 200, refill 0,5/s,
  lookups 10 de rafaga y 1 cada 30 s.
- La suite nueva del marketplace: `ATAS.OptionsSuite` (GUID 78113cd5..., 'Options Chain: GEX Profile / Key Levels / OI Profile / Expected
  Move', usa esta API) y `AdvancedOptionsSuite` (GUIDs 73879a2e.../cdefe191..., 'Options X-Ray', datos de un servidor propio
  `options-data.orderflowtrading.net` con cohortes retail/pros/firms/brokers/dealers; su cache `%APPDATA%\ATAS\OptionsDataCache` (1,3 GB)
  esta cifrada con DPAPI: NO intentar leerla, es licencia de ellos). El operador tiene las dos vitalicias ('My addons').

**Why:** sin esto, cualquier libro vivo propio queda ahogado por el cupo de 200 y se culpa a Rithmic o al dato.
**How to apply:** toda cadena viva nueva va por la API (3.0, `atas/PythiaGexTres/CadenaApi.cs`); ante 'libro flaco' o 'latencia' desde
el 02-10, grep `refused` en el log de ATAS antes de tocar nada. Ver [[gamma-hoy-2-0-clon]], [[cadena-es-en-vivo-rithmic]],
[[atas-lento-cortes-rithmic]], [[cache-velas-atas-podada]].
