# PythiaGex 4.0 - Familia

Indicador de ATAS (08-10-2026) que dibuja en el gráfico de MNQ **exactamente lo mismo que la vista previa** (`profundidad/pagina/preview.html`):
la familia del Nasdaq (NQ por Rithmic, NDX y QQQ de CBOE, la familia sumada, zero estándar, lo que dibujó la 3.0) y TQQQ ×3 bien convertido.
**No calcula**: lee cada 5 s los json que escriben los generadores del laboratorio. No toca la 3.0, que sigue alimentando el libro de NQ (viva3).

## De qué depende (si no corren, la cabecera lo dice en rojo)
- `laboratorio/tres/auditoria_0810/preview_niveles.py` → `profundidad/pagina/preview_datos/preview_niveles.json` (cada minuto).
- `laboratorio/tres/tqqq/tqqq_vivo.py` → `profundidad/pagina/preview_datos/tqqq_vivo.json` (cada minuto; `--parar`, `--estado`).
- La 3.0 corriendo en ATAS (escribe viva3 y la cinta que usa el generador).

## Instalar
`herramientas/instalar_4_0.ps1` (copia solo `PythiaGexCuatro.dll`, cierra ATAS guardando, espera 30 s para que Lucid/Rithmic suelte la sesión,
relanza y conecta). Compilar antes: `dotnet build -c Release -o bin/Release` en esta carpeta.

## Qué dibuja
- **Rayitas por vela** de cada serie prendida (la vela m2 de la vista previa que contiene la vela del gráfico; hora por ticks UTC).
- **Etiquetas chicas a la derecha**, pegadas al eje: caja con borde del color de la serie, nombre corto + precio, "+N" si varias series caen a
  ≤1 pt. Arriba de la columna, la edad del dato (si tiene más de 30 min: "DATO DE HACE …", antes de los números).
- **Pestaña desplegable** arriba a la izquierda ("PythiaGex 4.0 ▸ · dato de hace … · eje …"): arranca cerrada; clic (o el ajuste "Recuadro abierto")
  muestra fuentes, edades, conversiones y el detalle de cada nivel.
- **Doble eje elegible** a la izquierda: Ninguno / NDX (NQ − base sincronizada) / QQQ (NQ ÷ razón sincronizada) / TQQQ ((NQ − c) ÷ s, ×3 diario,
  NO regla de tres). Usa la misma conversión que las rayas (`conv_valor` del json). Marcas con paso automático según el zoom.
- Sin sombreado (la banda ±N va solo en el texto).
- Control de vencimiento: si el gráfico es de otro contrato que la cinta (roll), no dibuja.

## Defaults (los eligió el operador, captura 08-10 17:5x)
QQQ majors OI, NQ muros OI, NDX muros vol, QQQ muros OI, FAM muros OI, QQQ zero est. vol, 3.0 NDX, TQQQ muros OI. El resto apagado.
ATAS guarda cada ajuste por NOMBRE en el workspace: para cambiar un default ya guardado hay que renombrar la propiedad.

## Versiones
- 4.0.0 primera, 4.0.1 rótulos agrupados por precio, 4.0.2 arreglos de la revisión adversarial (edad por fuente, OI de 2 sesiones, roll, lectura
  fallida no pisa la buena, hora por ticks, mapeo lineal de y), 4.0.3 etiquetas chicas + recuadro desplegable, 4.0.4 pestaña debajo del cartel
  de ATAS (margen 56), 4.0.5 doble eje elegible, 4.0.6 título del eje en la pestaña (el fondo del ChartArea cae detrás del eje de tiempo).

Sin validar: describe, no anticipa. Capturas en `capturas/`.
