# -*- coding: utf-8 -*-
"""ws_apagar_zest_qqq_0910.py — pedido del operador (09-10-2026 ~03:05 ART): "esta podes desactivarla de la vista predeterminada (31057) ...
la idea no es que la saques del codigo sino de la visualizacion predeterminada en pantalla, porque como estamos en investigacion por ahi la
quiero ver mas adelante". Apaga SOLO la casilla S_ZEST_QQQ_vol ("QQQ 0G") en el workspace 'MNQ liviano'; la serie se sigue calculando.
Correr con ATAS cerrado (instalar_4_0.ps1 -SinDll -AntesDeLanzar)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ws_apagar_series_0910 import main

if __name__ == "__main__":
    sys.exit(main(["ZEST_QQQ_vol"]))
