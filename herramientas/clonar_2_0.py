# -*- coding: utf-8 -*-
"""Crea (o rehace desde cero) el clon "PythiaGex 2.0": atas/PythiaGexDos, un ensamblado APARTE (PythiaGexDos.dll) con los mismos
fuentes de Gamma Hoy y sus dependencias, otro namespace, otros nombres en ATAS ("PythiaGex 2.0 - ..."), otra carpeta de datos
(%APPDATA%\\ATAS\\PythiaGex2) y otros logs (pythiagex2-*). La produccion (PythiaGexNiveles.dll) no se toca.
Uso: python herramientas/clonar_2_0.py   (solo la primera vez: despues el clon evoluciona solo; volver a correrlo PISA el clon)."""
import io, os, re, shutil, sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ORI = os.path.join(RAIZ, "atas", "PythiaGexNiveles")
DST = os.path.join(RAIZ, "atas", "PythiaGexDos")
EXCLUIR = {"FlujoClaro.cs", "FlujoClaroSonda.cs", "FlujoClaroVerdes.cs", "FlujoClaroTablero.cs", "VerdesNucleo.cs"}   # Flujo Claro queda solo en prod

if os.path.exists(DST) and "--pisar" not in sys.argv:
    sys.exit("ya existe " + DST + " (usar --pisar para rehacerlo desde prod; se pierden los cambios del clon)")
if os.path.exists(DST):
    shutil.rmtree(DST)
os.makedirs(DST)

archivos = []
for n in sorted(os.listdir(ORI)):
    if not n.endswith(".cs") or n in EXCLUIR:
        continue
    s = io.open(os.path.join(ORI, n), encoding="utf-8").read()
    s = re.sub(r"\bnamespace PythiaGex\b", "namespace PythiaGexDos", s)
    s = re.sub(r"\busing PythiaGex;", "using PythiaGexDos;", s)
    s = re.sub(r"(?<![\w.])PythiaGex\.(?=[A-Z])", "PythiaGexDos.", s)
    s = s.replace('[DisplayName("PythiaGex - ', '[DisplayName("PythiaGex 2.0 - ')
    s = s.replace('[Category("PythiaGex")]', '[Category("PythiaGex 2.0")]')
    s = re.sub(r'"ATAS",\s*"PythiaGex"', '"ATAS", "PythiaGex2"', s)
    s = s.replace('"pythiagex-', '"pythiagex2-')
    s = s.replace("Gamma Hoy 1.11d (roll: weekly del viernes con Z6; estado del roll atomico; rearme al vencer la trimestral) arranca",
                  "Gamma Hoy 2.0.0 (clon de 1.11d: ensamblado y carpeta de datos propios) arranca")
    io.open(os.path.join(DST, n), "w", encoding="utf-8", newline="").write(s)
    archivos.append(n)

csproj = io.open(os.path.join(ORI, "PythiaGexNiveles.csproj"), encoding="utf-8").read()
csproj = csproj.replace("<AssemblyName>PythiaGexNiveles</AssemblyName>", "<AssemblyName>PythiaGexDos</AssemblyName>")
csproj = csproj.replace("<RootNamespace>PythiaGex</RootNamespace>", "<RootNamespace>PythiaGexDos</RootNamespace>")
csproj = csproj.replace("<Product>PythiaGex - Niveles Gamma</Product>", "<Product>PythiaGex 2.0</Product>")
csproj = csproj.replace("<Version>1.0.0</Version>", "<Version>2.0.0</Version>")
csproj = re.sub(r"  <ItemGroup>\n(    <Compile Include=\"[^\"]+\" />\n)+  </ItemGroup>",
                "  <ItemGroup>\n" + "".join('    <Compile Include="%s" />\n' % n for n in archivos) + "  </ItemGroup>", csproj, count=1)
io.open(os.path.join(DST, "PythiaGexDos.csproj"), "w", encoding="utf-8", newline="").write(csproj)

io.open(os.path.join(DST, "README.md"), "w", encoding="utf-8", newline="\n").write("""# PythiaGex 2.0 (clon de Gamma Hoy)

Ensamblado APARTE (`PythiaGexDos.dll`) creado el 18-09-2026 con `herramientas/clonar_2_0.py` a partir de `PythiaGexNiveles`
(Gamma Hoy 1.11d). Regla del operador: la produccion (`PythiaGexNiveles.dll`, "PythiaGex - Gamma Hoy") NO se toca mas; todo
arreglo y toda feature nueva va aca, y si algo se rompe se vuelve a agregar la original.

- Nombres en ATAS: "PythiaGex 2.0 - Gamma Hoy" (categoria "PythiaGex 2.0").
- Datos propios: `%APPDATA%\\ATAS\\PythiaGex2\\` (estela, cadenas, viva, flujo...) y logs `%APPDATA%\\ATAS\\pythiagex2-*.log`.
- Los ajustes NO se comparten con la original (ATAS los guarda por nombre de indicador).
- Flujo Claro no esta en este ensamblado: sigue en prod. Sus "verdes" leen la estela de PROD (`PythiaGex\\estela`).
- OJO: dos Gamma Hoy en el mismo grafico (original + 2.0) duplican las suscripciones a Rithmic. Para probar, agregar 2.0 en UN grafico.
- El instalador `herramientas/reiniciar_e_instalar.ps1` copia los dos DLL si existen.
""")
print("clon creado:", DST, "|", len(archivos), "archivos")
