# -*- coding: utf-8 -*-
"""Respalda en el repositorio todo lo que vive fuera de el.

El proyecto empezo como un panel web y se bifurco: hoy tambien hay un
indicador de ATAS, una bitacora de sesiones, guias en PDF, herramientas
sueltas y decenas de memorias con todo lo aprendido. Varias de esas cosas
vivian SOLO en el disco de la maquina:

    ~/.claude/projects/<proyecto>/memory/     las memorias
    ../bitacora/                              las sesiones
    ../guias/                                 los PDF
    ../herramientas/                          scripts sueltos
    ../CLAUDE.md                              las instrucciones del proyecto

Si se rompe la maquina, o se limpia la carpeta de Claude, se perdia. Este
script las copia adentro del repositorio.

REDACCION. El repositorio es PUBLICO (GitHub Pages lo necesita para servir el
panel gratis). Antes de copiar:
  - se tachan los identificadores de cuenta y el login de Rithmic (TACHAR, aca abajo);
  - se reemplazan los nombres de terceros ('la referencia', 'la directriz') y algunos
    datos personales. Esos patrones NO estan en este archivo publico: viven en el repo
    PRIVADO, en PythiaGex-privado/tachar_publico.json (o en la ruta de la variable de
    entorno PYTHIAGEX_TACHAR). Si ese archivo falta, el script no copia nada;
  - las memorias de EXCLUIR_PUBLICO (traen el resultado economico real del operador)
    no se copian: quedan solo en el repo privado (PythiaGex-privado/memorias).
Al final revisa conocimiento/ y herramientas/ enteros y avisa si quedo algo de eso.

    python -I respaldar.py           # copia, tacha y revisa
    python -I respaldar.py --listar  # solo dice que falta respaldar
"""
import json
import os
import re
import shutil
import sys

RAIZ = os.path.dirname(os.path.abspath(__file__))
FUERA = os.path.dirname(RAIZ)
DESTINO = os.path.join(RAIZ, "conocimiento")

MEMORIAS = os.path.expanduser(
    r"~\.claude\projects\C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada\memory")

# Archivo PRIVADO con los patrones de terceros y personales (no va a este repo).
TACHAR_PRIVADO = os.environ.get("PYTHIAGEX_TACHAR") or os.path.join(FUERA, "PythiaGex-privado", "tachar_publico.json")

# Identificadores de cuenta y login. Se tachan siempre, haya o no archivo privado.
TACHAR_CUENTAS = [
    (re.compile(r"\b(LDI|LFE|LFF|DEMO)[0-9A-Z]{2,}[-A-Z0-9]*\b"), "<cuenta>"),
    (re.compile(r"\bTEST[0-9]{3}\b"), "<cuenta>"),   # sufijo de la cuenta del fondeo (<cuenta>-...-TESTnnn)
    (re.compile(r"\bLT-[A-Z0-9]+(?:\.\.\.)?"), "<login>"),   # usuario de Rithmic (09-10: estaba en dos memorias)
]
TACHAR = TACHAR_CUENTAS + [
    (re.compile(r"\b[0-9]{9,}\b"), "<numero>"),
]

# Memorias que NO van al publico (09-10): traen las operaciones reales del operador, su resultado en USD, sus cuentas
# y los cierres de cuenta. Viven en la carpeta de memorias y en el repo privado. Si alguna ya estaba copiada, se borra
# de conocimiento/memorias, y su renglon se saca del indice MEMORY.md publico.
EXCLUIR_PUBLICO = {
    "estrategia-ronda-8-edge-personal.md",
    "estrategia-ronda-9-patron.md",
}

# Montos en USD de 4 o mas cifras: no se tachan solos (hay montos de mercado), pero se avisan para mirarlos a mano.
MONTO_USD = re.compile(r"(?:[-+]?\d{1,3}(?:\.\d{3})+\s*USD|USD\s*[-+]?\d{1,3}(?:\.\d{3})+)")

TEXTO = (".py", ".ps1", ".bat", ".cmd", ".js", ".json", ".txt", ".md", ".csv", ".cs", ".html")


def cargar_privado():
    """Patrones de terceros y personales desde el archivo privado. Sin el, no se copia nada."""
    if not os.path.isfile(TACHAR_PRIVADO):
        print("FALTA el archivo privado de patrones: %s" % TACHAR_PRIVADO)
        print("Sin el, las memorias saldrian con nombres de terceros. No se copio nada.")
        print("Traelo del repo privado (PythiaGex-privado/tachar_publico.json) o indica la ruta en PYTHIAGEX_TACHAR.")
        sys.exit(2)
    with open(TACHAR_PRIVADO, encoding="utf-8") as f:
        d = json.load(f)
    reglas, de_archivo = [], []
    for seccion in ("terceros", "personales"):
        for r in d.get(seccion, []):
            rx = re.compile(r["patron"], re.I)
            reglas.append((rx, r.get("por", "")))
            if r.get("archivo"):
                de_archivo.append((rx, r.get("por", "")))
    if not reglas:
        print("El archivo privado de patrones esta vacio: %s. No se copio nada." % TACHAR_PRIVADO)
        sys.exit(2)
    return reglas, de_archivo


PRIVADO, PRIVADO_ARCHIVO = [], []


def limpiar(texto):
    for rx, por in TACHAR:
        texto = rx.sub(por, texto)
    for rx, por in PRIVADO:
        texto = rx.sub(por, texto)
    return texto


def sin_indice_excluido(texto):
    """Saca del MEMORY.md publico los renglones que enlazan a una memoria excluida."""
    return "".join(l for l in texto.splitlines(True)
                   if not any("(%s)" % n in l for n in EXCLUIR_PUBLICO))


def nombre_publico(nombre):
    """El nombre de archivo tambien puede llevar el nombre de un tercero (<nombre>-tqqq-referencia.md -> tqqq-referencia.md)."""
    for rx, por in PRIVADO_ARCHIVO:
        nombre = rx.sub(por, nombre)
    return nombre


def copiar_md(origen, destino):
    """Copia un .md tachando lo que no debe publicarse."""
    with open(origen, encoding="utf-8") as f:
        t = f.read()
    limpio = limpiar(t)
    if os.path.basename(origen) == "MEMORY.md":
        limpio = sin_indice_excluido(limpio)
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    anterior = None
    if os.path.exists(destino):
        with open(destino, encoding="utf-8") as f:
            anterior = f.read()
    if anterior == limpio:
        return False, (limpio != t)
    with open(destino, "w", encoding="utf-8") as f:
        f.write(limpio)
    return True, (limpio != t)


def sensible_en(texto, con_numeros=False):
    """Que patrones sensibles aparecen en un texto (para codigo: sin el de numeros largos, que da falsos positivos)."""
    reglas = (TACHAR if con_numeros else TACHAR_CUENTAS) + PRIVADO
    return [rx.pattern[:24] for rx, _ in reglas if rx.search(texto)]


def copiar_binario(origen, destino):
    """Copia tal cual (codigo, PDF). Un archivo de texto con cuentas o nombres de terceros NO se copia: se avisa."""
    if origen.lower().endswith(TEXTO):
        try:
            with open(origen, encoding="utf-8", errors="ignore") as f:
                hay = sensible_en(f.read())
        except OSError:
            hay = []
        if hay:
            return None
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    if os.path.exists(destino) and os.path.getsize(destino) == os.path.getsize(origen):
        return False
    shutil.copy2(origen, destino)
    return True


def revisar(carpetas):
    """Escaneo final: nada de cuentas, login ni nombres de terceros en lo que queda en el repo."""
    hallazgos = []
    for c in carpetas:
        for base, dirs, archivos in os.walk(c):
            dirs[:] = [d for d in dirs if d not in ("__pycache__", "respaldos_ws")]
            for n in archivos:
                if not n.lower().endswith(TEXTO):
                    continue
                ruta = os.path.join(base, n)
                try:
                    with open(ruta, encoding="utf-8", errors="ignore") as f:
                        hay = sensible_en(f.read())
                except OSError:
                    continue
                hay += [rx.pattern[:24] for rx, _ in PRIVADO_ARCHIVO if rx.search(n)]
                if hay:
                    hallazgos.append((os.path.relpath(ruta, RAIZ), hay))
    return hallazgos


def main():
    global PRIVADO, PRIVADO_ARCHIVO
    PRIVADO, PRIVADO_ARCHIVO = cargar_privado()
    solo_listar = "--listar" in sys.argv
    cambios, tachados, no_copiados, montos, sacados = [], [], [], [], []

    tareas = [
        (MEMORIAS, os.path.join(DESTINO, "memorias"), ".md"),
        (os.path.join(FUERA, "bitacora"), os.path.join(DESTINO, "bitacora"), ".md"),
        (os.path.join(FUERA, "herramientas"), os.path.join(RAIZ, "herramientas"), None),
        (os.path.join(FUERA, "guias"), os.path.join(RAIZ, "guias"), None),
    ]

    for origen, destino, filtro in tareas:
        if not os.path.isdir(origen):
            print("  falta la carpeta: %s" % origen)
            continue
        for nombre in sorted(os.listdir(origen)):
            ruta = os.path.join(origen, nombre)
            if not os.path.isfile(ruta):
                continue
            if filtro and not nombre.endswith(filtro):
                continue
            dst = os.path.join(destino, nombre_publico(nombre))
            if nombre in EXCLUIR_PUBLICO:
                if os.path.exists(dst) and not solo_listar:
                    os.remove(dst)
                    sacados.append(os.path.relpath(dst, RAIZ))
                continue
            if solo_listar:
                if not os.path.exists(dst):
                    cambios.append(os.path.relpath(dst, RAIZ))
                continue
            if nombre.endswith(".md"):
                cambio, tacho = copiar_md(ruta, dst)
                if tacho:
                    tachados.append(nombre)
                if cambio:
                    with open(dst, encoding="utf-8") as f:
                        if MONTO_USD.search(f.read()):
                            montos.append(os.path.relpath(dst, RAIZ))
            else:
                cambio = copiar_binario(ruta, dst)
                if cambio is None:
                    no_copiados.append(os.path.relpath(ruta, FUERA))
                    cambio = False
            if cambio:
                cambios.append(os.path.relpath(dst, RAIZ))

    # las instrucciones del proyecto, que son el contexto de todo
    cl = os.path.join(FUERA, "CLAUDE.md")
    if os.path.isfile(cl) and not solo_listar:
        cambio, tacho = copiar_md(cl, os.path.join(DESTINO, "CLAUDE.md"))
        if cambio:
            cambios.append("conocimiento/CLAUDE.md")
        if tacho:
            tachados.append("CLAUDE.md")

    if solo_listar:
        print("sin respaldar: %d archivo(s)" % len(cambios))
        for c in cambios[:20]:
            print("   " + c)
        return

    print("respaldados o actualizados: %d" % len(cambios))
    for c in cambios[:40]:
        print("   " + c)
    if tachados:
        print("\nse tacharon identificadores o nombres en: " + ", ".join(sorted(set(tachados))))
    if sacados:
        print("\nSACADOS del publico (van solo al privado): " + ", ".join(sacados))
    if no_copiados:
        print("\nNO copiados (codigo con cuentas o nombres de terceros; van al privado): " + ", ".join(no_copiados))
    if montos:
        print("\nREVISAR A MANO (montos en USD de 4+ cifras; si son del operador, sumar a EXCLUIR_PUBLICO): "
              + ", ".join(montos))

    quedan = revisar([DESTINO, os.path.join(RAIZ, "herramientas"), os.path.join(RAIZ, "guias")])
    if quedan:
        print("\nATENCION, quedo algo sensible en el repo (no commitear hasta arreglarlo):")
        for ruta, hay in quedan:
            print("   %s  <- %s" % (ruta, ", ".join(hay)))
        sys.exit(1)
    print("\nrevision final: conocimiento/, herramientas/ y guias/ sin cuentas, login ni nombres de terceros")


if __name__ == "__main__":
    main()
