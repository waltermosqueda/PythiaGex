---
name: atas-8-0-16-ai-connect
description: "ATAS se actualizo a 8.0.16.275-beta (15-11 noche) y trae 'Connect AI agent': servidor MCP local http://127.0.0.1:4823/mcp con token personal; para futuros NO da velas, valores de indicadores ni capturas (solo cripto); si deja agregar/configurar indicadores, cambiar vista, dibujar, alertas, compilar y leer posiciones/diario; el token lo carga el operador, no yo"
metadata:
  node_type: memory
  type: reference
  originSessionId: b1346044-cbec-4f19-80c3-ffb1de06776c
  modified: 2026-11-16T03:40:06.357Z
---

Visto en pantalla el 16-11-2026 00:38 ART (asistente "Start MCP server", 3 pasos) + documentacion oficial leida ese dia
(feedback.atas.net/changelog/beta-8016, learn.atas.net/ai/ai-connect, atas.net/developing/api-webhooks).
- Paso 1: "Enable MCP server" (corre mientras ATAS esta abierto, solo acepta conexiones de la propia PC). Paso 2: "Copy prompt"
  con la direccion (Streamable HTTP, puerto 4823) y un token Bearer; para Claude Code el propio texto trae el comando
  `claude mcp add --transport http atas http://127.0.0.1:4823/mcp --header "Authorization: Bearer <token>"`; Claude Desktop va por
  `OFT.Platform.McpProxy.exe` en claude_desktop_config.json.
- El token "funciona como contraseña": NO copiarlo, escribirlo ni usarlo yo; el operador corre el comando o pega el prompt.
- Que da (documentacion): lectura de workspace, graficos, ajustes de indicadores, cuentas, posiciones, ordenes y diario; datos de
  mercado, VALORES de indicadores y capturas SOLO con datos de exchanges cripto (en futuros/opciones: no). En futuros SI puede
  cambiar la vista, agregar y configurar indicadores, dibujar niveles, crear alertas y escribir/compilar indicadores.
- Trading: apagado por defecto; si se prende, cada orden pide confirmacion y 1 contrato por orden. Yo nunca envio ordenes.
- Solo plan Ultra; el agente se paga aparte. CLAUDE.md todavia dice 8.0.15.302.

**How to apply:** sirve para configurar graficos/indicadores sin mouse (lo que hoy hago con clics) y para leer el diario de
operaciones; NO reemplaza la lectura por pixeles de X-Ray en ES. Verificar la lista real de herramientas al conectar (no esta
documentada). PythiaRegla siguio cargando en 8.0.16. Ver [[opciones-desde-cero-1511]], [[atas-8-0-15-api-opciones]].
