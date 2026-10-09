# -*- coding: utf-8 -*-
"""clonar_4_0.py — 08-10-2026. Clona la 3.0 (atas/PythiaGexTres, 3.6.9) en atas/PythiaGexCuatro para la 4.1 UNICA E INDEPENDIENTE (pedido del
operador: un solo indicador, todo calculado adentro, sin programas externos). La 3.0 NO se toca (queda de respaldo).

Que hace (reproducible; no borra nada):
  1. Respaldar el visor 4.0.6 (FamiliaCuatro.cs, DatosCuatro.cs, PythiaGexCuatro.csproj, README.md) en atas/PythiaGexCuatro/_visor_4_0_6/
     (si ya existe el respaldo, no lo pisa). capturas/, DISENO_4_1.md, _contratos/ y _modulos/ quedan donde estan.
  2. Copiar los .cs de la 3.0 (menos SondaApi.cs, que es otro indicador) y renombrar lo que chocaria con la 3.0 si corren a la vez:
     namespace, DisplayName/Category, carpeta de datos PythiaGex3 -> PythiaGex4, logs pythiagex3- -> pythiagex4-, centinela, carpeta de la
     cinta (propia, en %APPDATA%/ATAS/PythiaGex4/cinta), nombre del hilo de la cinta, indicador.json propio y apagado por defecto, textos.
  3. Escribir el csproj (AssemblyName/RootNamespace PythiaGexCuatro, Version 4.1.0, sin SondaApi.cs).
  4. Informar cada reemplazo hecho (y fallar si un reemplazo esperado no aparece: la 3.0 cambio y hay que revisar).
Uso: python -I herramientas/clonar_4_0.py   (despues: dotnet build -c Release -o bin/Release en atas/PythiaGexCuatro)"""
import os, re, shutil, sys

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
ORIG = os.path.join(RAIZ, "atas", "PythiaGexTres")
DEST = os.path.join(RAIZ, "atas", "PythiaGexCuatro")
VISOR = os.path.join(DEST, "_visor_4_0_6")
EXCLUIR = {"SondaApi.cs"}

APPDATA_EXPR = 'System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "ATAS", "PythiaGex4", '

# (archivo o "*", viejo, nuevo, minimo de apariciones esperadas)
REEMPLAZOS = [
    ("*", "namespace PythiaGexTres", "namespace PythiaGexCuatro", 1),
    ("GammaHoyTres.cs", '[DisplayName("PythiaGex 3.0 - Gamma Hoy")]', '[DisplayName("PythiaGex 4.0 - Gamma Familia")]', 1),
    ("GammaHoyTres.cs", '[Category("PythiaGex 3.0")]', '[Category("PythiaGex 4.0")]', 1),
    ("GammaHoyTres.cs", 'private const string VERSION = "3.6.9";', 'private const string VERSION = "4.1.0";', 1),
    ("Registro.cs", 'Path.Combine(CarpetaAtas, "PythiaGex3")', 'Path.Combine(CarpetaAtas, "PythiaGex4")', 1),
    ("Registro.cs", '"pythiagex3-" + nombre', '"pythiagex4-" + nombre', 1),
    ("Centinela.cs", '"pythiagex3-centinela-"', '"pythiagex4-centinela-"', 1),
    ("GammaHoyTresCinta.cs",
     'public string Cinta3Carpeta { get; set; } = @"C:\\Users\\wmx_7\\OneDrive\\Escritorio\\ATAS nada\\PythiaGex\\profundidad\\estado\\cinta";',
     'public string Cinta3Carpeta { get; set; } = ' + APPDATA_EXPR + '"cinta");', 1),
    ("GammaHoyTresCinta.cs",
     'string.IsNullOrWhiteSpace(Cinta3Carpeta) ? @"C:\\Users\\wmx_7\\OneDrive\\Escritorio\\ATAS nada\\PythiaGex\\profundidad\\estado\\cinta" : Cinta3Carpeta.Trim();',
     'string.IsNullOrWhiteSpace(Cinta3Carpeta) ? ' + APPDATA_EXPR + '"cinta") : Cinta3Carpeta.Trim();', 1),
    ("GammaHoyTresCinta.cs", 'Name = "PythiaGex3 cinta"', 'Name = "PythiaGex4 cinta"', 1),
    ("GammaHoyTresEstado.cs", 'public bool Estado3Pulso { get; set; } = true;', 'public bool Estado3Pulso { get; set; } = false;   // 4.1: nadie de afuera lee la 4.0', 1),
    ("GammaHoyTresEstado.cs",
     'public string Estado3Ruta { get; set; } = @"C:\\Users\\wmx_7\\OneDrive\\Escritorio\\ATAS nada\\PythiaGex\\profundidad\\estado\\indicador.json";',
     'public string Estado3Ruta { get; set; } = ' + APPDATA_EXPR + '"indicador.json");', 1),
    ("GammaHoyTres.cs", '"PythiaGex 3.0: el grafico es "', '"PythiaGex 4.0: el grafico es "', 1),
    ("GammaHoyTres.cs", 'string pre = "PythiaGex 3.0 · "', 'string pre = "PythiaGex 4.0 · "', 1),
]
# textos de ayuda que nombran la carpeta vieja (no cambian el comportamiento, pero no deben mentir)
TEXTOS = [("PythiaGex3\\\\", "PythiaGex4\\\\"), ("pythiagex3-centinela", "pythiagex4-centinela"), ("PythiaGex3\\", "PythiaGex4\\")]


def main():
    if not os.path.isdir(ORIG):
        sys.exit("no existe " + ORIG)
    os.makedirs(DEST, exist_ok=True)
    # 1. respaldo del visor
    if not os.path.isdir(VISOR):
        os.makedirs(VISOR)
        for n in ("FamiliaCuatro.cs", "DatosCuatro.cs", "PythiaGexCuatro.csproj", "README.md"):
            p = os.path.join(DEST, n)
            if os.path.exists(p):
                shutil.move(p, os.path.join(VISOR, n))
                print("respaldado:", n, "-> _visor_4_0_6/")
    else:
        print("respaldo del visor ya existia: no lo toco")
        for n in ("FamiliaCuatro.cs", "DatosCuatro.cs"):
            p = os.path.join(DEST, n)
            if os.path.exists(p):
                os.remove(p); print("quitado del proyecto (ya respaldado):", n)
    for d in ("obj",):
        p = os.path.join(DEST, d)
        if os.path.isdir(p):
            shutil.rmtree(p); print("borrado", d, "(artefactos de compilacion del visor)")
    # 2. copiar y renombrar
    archivos = sorted(f for f in os.listdir(ORIG) if f.endswith(".cs") and f not in EXCLUIR)
    textos = {}
    for f in archivos:
        textos[f] = open(os.path.join(ORIG, f), encoding="utf-8-sig").read()
    for (arch, viejo, nuevo, minimo) in REEMPLAZOS:
        objetivo = archivos if arch == "*" else [arch]
        total = 0
        for f in objetivo:
            n = textos[f].count(viejo)
            if n:
                textos[f] = textos[f].replace(viejo, nuevo); total += n
        print("%-24s %3d x  %s" % (arch, total, viejo[:70]))
        if total < minimo:
            sys.exit("FALTA el reemplazo esperado (la 3.0 cambio?): " + viejo)
    for f in archivos:
        for viejo, nuevo in TEXTOS:
            textos[f] = textos[f].replace(viejo, nuevo)
        if "PythiaGexTres" in textos[f] or '"PythiaGex3"' in textos[f]:
            print("OJO: quedan menciones a PythiaGexTres/PythiaGex3 en", f)
        open(os.path.join(DEST, f), "w", encoding="utf-8").write(textos[f])
    print("copiados %d archivos .cs (sin %s)" % (len(archivos), ", ".join(sorted(EXCLUIR))))
    # 3. csproj
    cs = open(os.path.join(ORIG, "PythiaGexTres.csproj"), encoding="utf-8-sig").read()
    cs = cs.replace("<AssemblyName>PythiaGexTres</AssemblyName>", "<AssemblyName>PythiaGexCuatro</AssemblyName>")
    cs = cs.replace("<RootNamespace>PythiaGexTres</RootNamespace>", "<RootNamespace>PythiaGexCuatro</RootNamespace>")
    cs = re.sub(r"<Version>[^<]*</Version>", "<Version>4.1.0</Version>", cs)
    cs = re.sub(r"<Product>[^<]*</Product>", "<Product>PythiaGex 4.0 - Gamma Familia</Product>", cs)
    cs = re.sub(r'\s*<Compile Include="SondaApi\.cs" />', "", cs)
    if "PythiaGexCuatro" not in cs:
        sys.exit("el csproj no quedo renombrado")
    open(os.path.join(DEST, "PythiaGexCuatro.csproj"), "w", encoding="utf-8").write(cs)
    print("csproj escrito (AssemblyName PythiaGexCuatro, 4.1.0, sin SondaApi.cs)")


if __name__ == "__main__":
    main()
