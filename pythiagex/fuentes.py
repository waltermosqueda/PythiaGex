# -*- coding: utf-8 -*-
"""Bajada de cadenas de opciones.

Fuente primaria: CDN publico de CBOE. Sin API key, sin login.
Se guarda siempre el crudo comprimido: si manana cambian el formato,
el historico no se pierde.
"""
import gzip, json, os, urllib.request, datetime as dt

CDN = "https://cdn.cboe.com/api/global/delayed_quotes/options/{}.json"
UA  = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
       "(KHTML, like Gecko) Chrome/120.0 Safari/537.36")

# Los indices llevan guion bajo adelante; los ETF no.
SIMBOLOS = {
    "SPX": "_SPX", "NDX": "_NDX", "RUT": "_RUT", "VIX": "_VIX",
    "SPY": "SPY",  "QQQ": "QQQ",  "IWM": "IWM",  "DIA": "DIA",
}

def normalizar(sym: str) -> str:
    s = sym.upper().lstrip("^_")
    return SIMBOLOS.get(s, s)

def bajar(sym: str, cache_dir: str = "datos/cache", guardar: bool = True) -> dict:
    """Devuelve la cadena cruda tal como la publica CBOE."""
    s = normalizar(sym)
    # gzip (16-09): sin Accept-Encoding el CDN mandaba el JSON crudo (NDX ~8 MB, SPX ~20 MB cada 75 s desde la PC
    # del operador: rafagas de 2-3 MB/s que coincidian con cortes de la conexion de datos de Rithmic). Comprimido
    # pesa 0,8 / 1,7 MB. Se lee de a trozos con una pausa corta para no saturar la bajada del operador.
    req = urllib.request.Request(CDN.format(s), headers={"User-Agent": UA, "Accept-Encoding": "gzip"})
    with urllib.request.urlopen(req, timeout=60) as r:
        trozos = []
        while True:
            t = r.read(256 * 1024)
            if not t: break
            trozos.append(t)
            import time as _t; _t.sleep(0.05)   # ~5 MB/s de tope, sin rafagas
        crudo = b"".join(trozos)
        if (r.headers.get("Content-Encoding") or "").lower() == "gzip" or crudo[:2] == bytes([0x1f, 0x8b]):
            crudo = gzip.decompress(crudo)
    if guardar:
        os.makedirs(cache_dir, exist_ok=True)
        sello = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%d-%H%M%S")
        ruta = os.path.join(cache_dir, f"{s}-{sello}.json.gz")
        with gzip.open(ruta, "wb", compresslevel=6) as f:
            f.write(crudo)
    return json.loads(crudo)

def leer_cache(ruta: str) -> dict:
    """Relee una corrida guardada. Sirve para backtest y para el slider de historia."""
    abrir = gzip.open if ruta.endswith(".gz") else open
    with abrir(ruta, "rb") as f:
        return json.loads(f.read())
