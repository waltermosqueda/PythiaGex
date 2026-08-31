# -*- coding: utf-8 -*-
"""Respalda en el repositorio todo lo que vive fuera de el.

El proyecto empezo como un panel web y se bifurco: hoy tambien hay un
indicador de ATAS, una bitacora de sesiones, guias en PDF, herramientas
sueltas y veintiseis memorias con todo lo aprendido. Varias de esas cosas
vivian SOLO en el disco de la maquina:

    ~/.claude/projects/<proyecto>/memory/     26 memorias
    ../bitacora/                              las sesiones
    ../guias/                                 los PDF
    ../herramientas/                          scripts sueltos
    ../CLAUDE.md                              las instrucciones del proyecto

Si se rompe la maquina, o se limpia la carpeta de Claude, se perdia. Este
script las copia adentro del repositorio, que es lo unico que esta en la nube.

REDACCION. El repositorio es publico porque GitHub Pages lo necesita para
servir el panel gratis. Antes de copiar se tachan los identificadores de
cuenta: son de simulacion, pero no tienen por que estar publicados.

    python respaldar.py           # copia y muestra que cambio
    python respaldar.py --listar  # solo dice que falta respaldar
"""
import os
import re
import shutil
import sys

RAIZ = os.path.dirname(os.path.abspath(__file__))
FUERA = os.path.dirname(RAIZ)
DESTINO = os.path.join(RAIZ, "conocimiento")

MEMORIAS = os.path.expanduser(
    r"~\.claude\projects\C--Users-wmx-7-OneDrive-Escritorio-ATAS-nada\memory")

# Identificadores de cuenta. Son de simulacion, pero se tachan igual.
TACHAR = [
    (re.compile(r"\b(LDI|LFE|DEMO)[0-9A-Z]{2,}[-A-Z0-9]*\b"), "<cuenta>"),
    (re.compile(r"\b[0-9]{9,}\b"), "<numero>"),
]


def limpiar(texto):
    for rx, por in TACHAR:
        texto = rx.sub(por, texto)
    return texto


def copiar_md(origen, destino):
    """Copia un .md tachando lo que no debe publicarse."""
    with open(origen, encoding="utf-8") as f:
        t = f.read()
    limpio = limpiar(t)
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


def copiar_binario(origen, destino):
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    if os.path.exists(destino) and os.path.getsize(destino) == os.path.getsize(origen):
        return False
    shutil.copy2(origen, destino)
    return True


def main():
    solo_listar = "--listar" in sys.argv
    cambios, tachados = [], []

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
            dst = os.path.join(destino, nombre)
            if solo_listar:
                if not os.path.exists(dst):
                    cambios.append(os.path.relpath(dst, RAIZ))
                continue
            if nombre.endswith(".md"):
                cambio, tacho = copiar_md(ruta, dst)
                if tacho:
                    tachados.append(nombre)
            else:
                cambio = copiar_binario(ruta, dst)
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
        print("\nse tacharon identificadores en: " + ", ".join(sorted(set(tachados))))


if __name__ == "__main__":
    main()
