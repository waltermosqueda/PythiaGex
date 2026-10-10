# -*- coding: utf-8 -*-
"""grupo_arriba_415c.py — REEMPLAZADO por grupo_arriba_415e.py (PythiaGex 4.1.5e, 09-10-2026).

Historia: la 4.1.5c (09-10) creo el grupo de arriba del dialogo ("0. PRENDER / APAGAR (todo lo que se dibuja)", 70 casillas, nombres cortos,
Order 0..69, los demas grupos +1000) y la 4.1.5d le sumo 31 (los bool de dibujo de la 3.0 que habian quedado en 9.x y las listas de 'ver').
Desde la 4.1.5e la fuente de verdad es herramientas/grupo_arriba_415e.py: el mismo grupo con la JERARQUIA visible (cada llave Tres41* antes de
sus dependientes '↳ ...', la primera frase de cada descripcion dice que llave necesita). Correr ESTE script con su lista vieja volveria a los
nombres de la 4.1.5d y borraria la jerarquia: por eso ahora solo avisa y corre el nuevo (mismos argumentos).
Uso: python -I herramientas/grupo_arriba_415e.py [--verificar] [--anchos]
"""
import os, runpy, sys

if __name__ == "__main__":
    nuevo = os.path.join(os.path.dirname(os.path.abspath(__file__)), "grupo_arriba_415e.py")
    print("grupo_arriba_415c.py esta reemplazado por grupo_arriba_415e.py (4.1.5e): corro ese.")
    sys.argv[0] = nuevo
    runpy.run_path(nuevo, run_name="__main__")
