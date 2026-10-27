# clonar_5_0.py — PythiaGex 5.0 "Gamma ES" (27-10-2026): clon MECANICO de la 4.1 (atas/PythiaGexCuatro, la instalada: sha256 de la DLL
# 8cb1c5cb...) a atas/PythiaGexCinco, para ES/MES con SPX y SPY de CBOE. Mapeo de libros: NQ->ES, NDX->SPX, QQQ->SPY; TQQQ queda
# (protegido de los renombres) y lo saca a mano el paso siguiente.
#
# Que hace (y que NO):
#   1. Copia los .cs del csproj de la 4.1 (y _contratos/Contratos.cs) con carpetas y nombres renombrados (nq/->es/, ndx/->spx/, qqq/->spy/,
#      *Nq.cs->*Es.cs, *Ndx.cs->*Spx.cs, *Qqq.cs->*Spy.cs, FamiliaCuatroPantalla.cs->GammaCincoPantalla.cs).
#   2. En los archivos de la FAMILIA (_modulos/**, AdaptadoresFamilia.cs, IntegracionFamilia.cs, _contratos/Contratos.cs) renombra los tokens
#      MNQ->MES, NQ->ES, Nq->Es, nq->es, NDX->SPX, Ndx->Spx, ndx->spx, QQQ->SPY, Qqq->Spy, qqq->spy (TQQQ/Tqqq/tqqq intactos; LINQ intacto).
#   3. En el NUCLEO 3.0 (el resto) NO renombra tokens sueltos (rompería la deteccion de raiz MNQ->NQ / MES->ES de CadenaApi y las ramas
#      raiz=="NQ"/"ES"): solo los IDENTIFICADORES compuestos que la familia renombro (ILibroNq->ILibroEs, S_MUROS_NQ_oi->S_MUROS_ES_oi, ...),
#      para que compile. Las capas NDX/QQQ del nucleo (GammaHoyTresCapas/Rayas/MajorOi) quedan como estan: las adapta el paso semantico.
#   4. En todo: namespace PythiaGexCuatro->PythiaGexCinco, clase FamiliaCuatro->GammaCinco, datos PythiaGex4->PythiaGex5, logs
#      pythiagex4-->pythiagex5-, nombre visible "PythiaGex 4.0"->"PythiaGex 5.0", VERSION -> 5.0.0.
#   5. Los NUMEROS (radios, grillas, bandas, cotas) NO se tocan: los cambia el paso semantico con las mediciones de _diseno/.
# Uso: python -I herramientas/clonar_5_0.py [--pisar]   (sin --pisar aborta si atas/PythiaGexCinco ya tiene .cs; nunca borra _diseno/)
import os, re, sys, hashlib, json, shutil
from collections import Counter

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.normpath(os.path.join(AQUI, ".."))
SRC = os.path.join(RAIZ, "atas", "PythiaGexCuatro")
DST = os.path.join(RAIZ, "atas", "PythiaGexCinco")

def leer(p):
    with open(p, "r", encoding="utf-8-sig", newline="") as f:    # newline="": conserva los CRLF de la 4.1
        return f.read()

def escribir(p, t):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8", newline="") as f:
        f.write(t)

def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()

# ---------------------------------------------------------------- renombres de ruta
def ruta_nueva(rel):
    partes = rel.replace("\\", "/").split("/")
    mapa_dir = {"nq": "es", "ndx": "spx", "qqq": "spy"}
    partes = [mapa_dir.get(p, p) for p in partes[:-1]] + [partes[-1]]
    f = partes[-1]
    f = f.replace("FamiliaCuatroPantalla", "GammaCincoPantalla")
    f = re.sub(r"Nq(?=\.cs$|[A-Z])", "Es", f)
    f = re.sub(r"Ndx(?=\.cs$|[A-Z])", "Spx", f)
    f = re.sub(r"(?<![Tt])Qqq(?=\.cs$|[A-Z])", "Spy", f)
    partes[-1] = f
    return "/".join(partes)

# ---------------------------------------------------------------- renombres de la familia (tokens)
REGLAS_FAM = [
    (re.compile(r"MNQ"), "MES"),
    (re.compile(r"(?<![A-Z])NQ"), "ES"),                 # LINQ queda (I antes de NQ)
    (re.compile(r"Nq(?![a-z])"), "Es"),
    (re.compile(r"(?<![A-Za-z])nq(?![a-z])"), "es"),     # aunque / arranque quedan
    (re.compile(r"NDX"), "SPX"),
    (re.compile(r"Ndx"), "Spx"),
    (re.compile(r"(?<![A-Za-z])ndx(?![a-z])"), "spx"),
    (re.compile(r"(?<![Tt])QQQ"), "SPY"),
    (re.compile(r"(?<![Tt])Qqq"), "Spy"),
    (re.compile(r"(?<![TtA-Za-z])qqq(?![a-z])"), "spy"),
]

def renombrar_familia(t, cont):
    for rx, nuevo in REGLAS_FAM:
        t, n = rx.subn(nuevo, t)
        cont[rx.pattern] += n
    return t

RX_ID = re.compile(r"\b[A-Za-z_][A-Za-z0-9_]*\b")
SUELTOS = {"MNQ", "NQ", "Nq", "nq", "NDX", "Ndx", "ndx", "QQQ", "Qqq", "qqq"}

def mapa_identificadores(textos_fam_originales):
    """identificadores compuestos de la familia que cambian con las reglas: old -> new (para aplicarlos al nucleo)."""
    m = {}
    for t in textos_fam_originales:
        for tok in set(RX_ID.findall(t)):
            if tok in SUELTOS:
                continue
            nuevo = renombrar_familia(tok, Counter())
            if nuevo != tok:
                m[tok] = nuevo
    return m

# ---------------------------------------------------------------- renombres de infraestructura (todo)
REGLAS_INFRA = [
    ("PythiaGexCuatro", "PythiaGexCinco"),
    ("FamiliaCuatro", "GammaCinco"),
    ("PythiaGex4", "PythiaGex5"),
    ("pythiagex4", "pythiagex5"),
    ("PythiaGex 4.0 - Gamma Familia", "PythiaGex 5.0 - Gamma ES"),
    ("PythiaGex 4.0/3.0", "PythiaGex 5.0/3.0"),
    ("\"PythiaGex 4.0", "\"PythiaGex 5.0"),
    ("[Category(\"PythiaGex 4.0\")]", "[Category(\"PythiaGex 5.0\")]"),
]

def infra(t, cont):
    for a, b in REGLAS_INFRA:
        n = t.count(a)
        if n:
            t = t.replace(a, b); cont[a] += n
    t2, n = re.subn(r'private const string VERSION = "[^"]+";', 'private const string VERSION = "5.0.0";', t)
    cont["VERSION"] += n
    return t2

def es_familia(rel):
    r = rel.replace("\\", "/")
    return r.startswith("_modulos/") or r in ("AdaptadoresFamilia.cs", "IntegracionFamilia.cs", "_contratos/Contratos.cs")

def main():
    pisar = "--pisar" in sys.argv
    csproj = leer(os.path.join(SRC, "PythiaGexCuatro.csproj"))
    rels = re.findall(r'<Compile Include="([^"]+)"', csproj)
    rels = [r.replace("\\", "/") for r in rels]
    existentes = [os.path.join(dp, f) for dp, _, fs in os.walk(DST) for f in fs if f.endswith(".cs")] if os.path.isdir(DST) else []
    if existentes and not pisar:
        print("ABORTO: atas/PythiaGexCinco ya tiene %d .cs (usar --pisar para rehacer el clon mecanico)" % len(existentes)); sys.exit(2)

    originales = {r: leer(os.path.join(SRC, r)) for r in rels}
    idmap = mapa_identificadores([t for r, t in originales.items() if es_familia(r)])
    cont = Counter(); manif = {"origen": "atas/PythiaGexCuatro", "dll_origen_sha256": sha(os.path.join(SRC, "bin", "Release", "PythiaGexCuatro.dll")),
                              "archivos": [], "identificadores_nucleo": {}}
    rx_ids = re.compile(r"\b(" + "|".join(sorted(map(re.escape, idmap), key=len, reverse=True)) + r")\b") if idmap else None
    usados_nucleo = Counter()
    nuevos_rel = []
    for r in rels:
        t = originales[r]
        if es_familia(r):
            t = renombrar_familia(t, cont)
        elif rx_ids:
            def rep(m):
                usados_nucleo[m.group(1)] += 1
                return idmap[m.group(1)]
            t = rx_ids.sub(rep, t)
        t = infra(t, cont)
        rn = ruta_nueva(r)
        escribir(os.path.join(DST, rn), t)
        nuevos_rel.append(rn)
        manif["archivos"].append({"origen": r, "destino": rn, "sha256_origen": sha(os.path.join(SRC, r)), "familia": es_familia(r)})
    manif["identificadores_nucleo"] = dict(sorted(usados_nucleo.items()))

    # csproj
    cp = csproj
    for r, rn in zip(rels, nuevos_rel):
        cp = cp.replace('<Compile Include="%s" />' % r.replace("/", "\\"), '<Compile Include="%s" />' % rn.replace("/", "\\"))
    cp = cp.replace("<AssemblyName>PythiaGexCuatro</AssemblyName>", "<AssemblyName>PythiaGexCinco</AssemblyName>")
    cp = cp.replace("<RootNamespace>PythiaGexCuatro</RootNamespace>", "<RootNamespace>PythiaGexCinco</RootNamespace>")
    cp = re.sub(r"<Version>[^<]+</Version>", "<Version>5.0.0.0</Version>", cp)
    cp = re.sub(r"<AssemblyVersion>[^<]+</AssemblyVersion>", "<AssemblyVersion>5.0.0.0</AssemblyVersion>", cp)
    cp = re.sub(r"<FileVersion>[^<]+</FileVersion>", "<FileVersion>5.0.0.0</FileVersion>", cp)
    cp = re.sub(r"<InformationalVersion>[^<]+</InformationalVersion>", "<InformationalVersion>5.0.0</InformationalVersion>", cp)
    cp = cp.replace("<Product>PythiaGex 4.0 - Gamma Familia</Product>", "<Product>PythiaGex 5.0 - Gamma ES</Product>")
    cp = re.sub(r"<!-- 4\.1\.5d.*?-->\s*", "", cp, flags=re.S)
    cp = re.sub(r"<!-- 4\.1 \(B1\): AssemblyVersion FIJA.*?-->\s*", "", cp, flags=re.S)
    escribir(os.path.join(DST, "PythiaGexCinco.csproj"), cp)
    escribir(os.path.join(DST, "_diseno", "clon_5_0_manifiesto.json"), json.dumps(manif, indent=1, ensure_ascii=False))

    print("clon mecanico: %d archivos -> %s" % (len(nuevos_rel), DST))
    for k, v in sorted(cont.items(), key=lambda x: -x[1]):
        print("  %6d  %s" % (v, k))
    print("  identificadores de la familia renombrados tambien en el nucleo: %d distintos, %d usos" % (len(usados_nucleo), sum(usados_nucleo.values())))
    falta = [r for r in nuevos_rel if ('Include="%s"' % r.replace("/", "\\")) not in cp]
    if falta:
        print("ERROR: el csproj no quedo con estos archivos:", falta); sys.exit(1)

if __name__ == "__main__":
    main()
