---
name: respaldo-del-conocimiento
description: "Las memorias y la bitácora viven fuera del repo: hay que correr respaldar.py para que queden en la nube."
metadata: 
  node_type: memory
  type: project
  modified: 2026-08-31T15:46:23.306Z
  originSessionId: a021c566-b4fe-42c5-acd1-0a01a165a079
---

Las memorias viven en `~/.claude/projects/<proyecto>/memory/`, la bitácora y las guías en la carpeta del escritorio. **Nada de eso está en la nube por sí solo.** Si se rompe la máquina o se limpia la carpeta de Claude, se pierde.

```bash
cd "PythiaGex" && python respaldar.py
```

Copia las memorias, la bitácora, las guías, las herramientas y el `CLAUDE.md` adentro de `PythiaGex/conocimiento/` y `PythiaGex/guias/`, y **tacha los identificadores de cuenta** antes de copiar, porque el repositorio es público (GitHub Pages lo necesita para servir el panel gratis).

**Correrlo al final de cualquier sesión en la que se hayan escrito o cambiado memorias, y commitear.** Es el único momento en que el conocimiento llega a la nube.

## La estructura del repositorio

El proyecto empezó como panel web y se bifurcó. Hoy son tres mitades, todas adentro de PythiaGex:

- `pythiagex/` + `panel/` — el motor y el panel. El **mapa**.
- `atas/` — el indicador de ATAS. El **reloj**. Tiene su propio README con cómo compilar, instalar, de dónde sale la probabilidad y por qué cada cosa está o no en la vista por defecto.
- `conocimiento/` — las memorias, la bitácora y el CLAUDE.md.

## Una trampa que ya mordió

**Git subió los PDF rotos.** Los tomó como texto y les convirtió los saltos de línea: la guía de CME pesaba 45.743 bytes en disco y 45.346 en el repositorio. Casi cuatrocientos bytes perdidos y el archivo inservible al descargarlo. El DLL zafó porque git lo detectó solo como binario.

Ya está el `.gitattributes` marcando `*.pdf`, `*.dll`, `*.png`, `*.jpg`, `*.gz`, `*.zip`, `*.ico` como binarios. **Si se agrega otro formato binario, marcarlo ahí antes de commitear**, y verificar después que el tamaño del repositorio coincida con el del disco:

```bash
git cat-file -s $(git rev-parse HEAD:ruta/archivo)
```

**Why:** son semanas de trabajo y de errores ya cometidos y documentados. Reconstruirlo desde cero costaría más que todo lo que llevamos.

**How to apply:** correr `respaldar.py` y commitear cuando cambien memorias. Ver [[auditoria-punta-a-punta]] y [[compilar-indicadores-atas]].

## Desde el 09-10 (auditoría del respaldo)

- `python -I respaldar.py` necesita `PythiaGex-privado/tachar_publico.json` (o la ruta en `PYTHIAGEX_TACHAR`): ahí
  están los patrones de terceros (la referencia, la directriz) y personales (otra conexión de fondeo, el agente de
  soporte). Sin ese archivo no copia nada. Antes estaban en base64 dentro del propio respaldar.py público: eso era
  ofuscación, no remoción, y se sacó (el commit se rehízo antes del push).
- El login de Rithmic (`LT-...`) se tacha siempre como `<login>`; una auditoría lo encontró publicado en dos memorias.
- `EXCLUIR_PUBLICO` deja afuera `estrategia-ronda-8-edge-personal.md` y `estrategia-ronda-9-patron.md` (operaciones
  reales, USD netos, cuentas, liquidaciones) y saca sus renglones del MEMORY.md público. Van solo al privado.
- Al final escanea conocimiento/, herramientas/ y guias/ y sale con error si queda algo. También avisa "REVISAR A
  MANO" si una memoria nueva trae montos en USD de 4+ cifras: si son del operador, sumarla a `EXCLUIR_PUBLICO`.
