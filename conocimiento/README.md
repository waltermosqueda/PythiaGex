# Conocimiento

Todo lo aprendido en el proyecto, respaldado acá porque **vivía solo en el
disco de una máquina**. Si se rompe esa máquina, o se limpia la carpeta de
Claude, se perdía.

Se sincroniza corriendo, desde la raíz del repositorio:

```bash
python respaldar.py           # copia lo que cambió
python respaldar.py --listar  # solo dice qué falta
```

El script tacha los identificadores de cuenta antes de copiar: son de
simulación, pero este repositorio es público porque GitHub Pages lo necesita
para servir el panel gratis.

## Qué hay

| Carpeta | Qué es | Vive originalmente en |
|---|---|---|
| `memorias/` | 26 fichas de lo aprendido, una por tema | `~/.claude/projects/<proyecto>/memory/` |
| `bitacora/` | Las sesiones medidas, una por día | `../bitacora/` |
| `CLAUDE.md` | Las instrucciones del proyecto | `../CLAUDE.md` |

`memorias/MEMORY.md` es el índice: una línea por ficha.

## Las que más pesan

Si hubiera que reconstruir el proyecto desde cero, éstas son las que ahorran
más tiempo:

- **`conversion-spx-a-es.md`** — la base se mide restando dos forwards de la
  misma cadena, nunca contra el índice. El método obvio da −21,6 donde la
  respuesta es +10,8: treinta y dos puntos, ciento veintiocho ticks.
- **`auditoria-punta-a-punta.md`** — el auditor que recalcula todo desde la
  cadena cruda, y los tres errores que encontró. Incluye el peor de todos: los
  commits del bot nunca disparaban el deploy de Pages, así que el dato se
  calculaba y no llegaba nunca.
- **`datos-ocultos-de-atas.md`** — cada vela de ATAS trae el footprint
  completo por precio. Con eso se ve quién está ganando en cada nivel de
  gamma, que es lo que ningún tablero de GEX puede dar.
- **`compilar-indicadores-atas.md`** — SDK, referencias sin copiar, la API por
  reflexión, y el botón "Add to chart" que no es doble clic.
- **`calcular-gex-propio.md`** — el método de cálculo, mejor que los cinco
  tableros públicos auditados.

## La bitácora

Seis sesiones medidas, con la plantilla en `bitacora/PLANTILLA.md`. El acuerdo
son **quince sesiones** antes de evaluar si se justifica pagar algo. Hasta
ahora: niveles 4 de 4 correctos, régimen 1 de 4.

No hay ninguna estrategia validada acá. Hay un método de lectura y un
protocolo de verificación.
