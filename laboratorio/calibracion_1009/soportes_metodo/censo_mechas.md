# Censo de mechas (giro direccional) y prueba de numeros redondos

Generado por `censo_mechas.py` (09-10-2026). RR = tasa de extremo cuando la punta de la vela cae a <= TOL de un multiplo de G, dividida por la tasa cuando cae en cualquier otro precio (IC 90 % por bootstrap de sesiones). Recall = % de extremos a <= TOL de la grilla; cobertura geometrica = fraccion de precios pintada; ocupacion = % de puntas de vela que caen en la grilla.

## MNQ m1 (08-02..09-17)  (34 sesiones, 2026-08-02 22:00 a 2026-09-17 20:59)

**THETA 10 pts, noche**: 9076 extremos (266.9 por sesion, 17.22 por hora), con mecha >= 25 % del rango: 72.9 %; velas ambiguas (todas las sesiones y ventanas): 3630

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 0.933 [0.788-1.076] | 13.406 | 14.372 | 1283 | 1.90 | 2.25 | 2.03 | 0.842 | 0.934 |
| G100_tol2 | 0.963 [0.846-1.096] | 13.846 | 14.372 | 2405 | 3.67 | 4.25 | 3.80 | 0.863 | 0.965 |
| G50_tol1 | 1.043 [0.956-1.134] | 14.937 | 14.325 | 2785 | 4.58 | 4.50 | 4.40 | 1.019 | 1.041 |
| G50_tol2 | 1.014 [0.943-1.090] | 14.541 | 14.335 | 5261 | 8.43 | 8.50 | 8.32 | 0.992 | 1.013 |
| G25_tol1 | 1.039 [0.977-1.098] | 14.866 | 14.302 | 5610 | 9.19 | 9.00 | 8.87 | 1.021 | 1.036 |
| G25_tol2 | 1.011 [0.969-1.055] | 14.485 | 14.325 | 10604 | 16.92 | 17.00 | 16.77 | 0.996 | 1.009 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 1.80 % vs 1.85 % -> 0.969 (n 163); justo en el redondo (0) 0.29 % vs 0.27 % -> 1.053 (n 26); pasado (+0.25..+2) 1.59 % vs 1.68 % -> 0.946 (n 144); pasado (+2.25..+5) 3.00 % vs 2.76 % -> 1.085 (n 272)

**THETA 10 pts, dia**: 5621 extremos (165.3 por sesion, 25.79 por hora), con mecha >= 25 % del rango: 72.8 %; velas ambiguas (todas las sesiones y ventanas): 3630

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 1.078 [0.949-1.222] | 23.134 | 21.458 | 536 | 2.21 | 2.25 | 2.05 | 0.980 | 1.076 |
| G100_tol2 | 1.041 [0.922-1.181] | 22.338 | 21.456 | 1052 | 4.18 | 4.25 | 4.02 | 0.984 | 1.039 |
| G50_tol1 | 1.004 [0.904-1.109] | 21.574 | 21.488 | 1131 | 4.34 | 4.50 | 4.32 | 0.965 | 1.004 |
| G50_tol2 | 0.956 [0.883-1.034] | 20.622 | 21.570 | 2153 | 7.90 | 8.50 | 8.23 | 0.929 | 0.96 |
| G25_tol1 | 1.022 [0.938-1.112] | 21.931 | 21.448 | 2362 | 9.22 | 9.00 | 9.03 | 1.024 | 1.02 |
| G25_tol2 | 0.985 [0.926-1.050] | 21.231 | 21.545 | 4437 | 16.76 | 17.00 | 16.96 | 0.986 | 0.988 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 2.44 % vs 2.14 % -> 1.140 (n 137); justo en el redondo (0) 0.20 % vs 0.26 % -> 0.764 (n 11); pasado (+0.25..+2) 1.55 % vs 1.63 % -> 0.950 (n 87); pasado (+2.25..+5) 3.02 % vs 2.89 % -> 1.046 (n 170)

**THETA 20 pts, noche**: 3067 extremos (90.2 por sesion, 5.82 por hora), con mecha >= 25 % del rango: 73.0 %; velas ambiguas (todas las sesiones y ventanas): 775

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 1.095 [0.819-1.388] | 5.300 | 4.841 | 1283 | 2.22 | 2.25 | 2.03 | 0.985 | 1.093 |
| G100_tol2 | 1.066 [0.846-1.310] | 5.156 | 4.838 | 2405 | 4.04 | 4.25 | 3.80 | 0.951 | 1.063 |
| G50_tol1 | 1.101 [0.962-1.241] | 5.314 | 4.829 | 2785 | 4.83 | 4.50 | 4.40 | 1.072 | 1.096 |
| G50_tol2 | 1.021 [0.910-1.146] | 4.942 | 4.842 | 5261 | 8.48 | 8.50 | 8.32 | 0.997 | 1.019 |
| G25_tol1 | 1.114 [1.013-1.220] | 5.348 | 4.801 | 5610 | 9.78 | 9.00 | 8.87 | 1.087 | 1.103 |
| G25_tol2 | 1.009 [0.944-1.081] | 4.885 | 4.843 | 10604 | 16.89 | 17.00 | 16.77 | 0.993 | 1.007 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 2.18 % vs 1.85 % -> 1.179 (n 67); justo en el redondo (0) 0.52 % vs 0.27 % -> 1.918 (n 16); pasado (+0.25..+2) 1.34 % vs 1.68 % -> 0.797 (n 41); pasado (+2.25..+5) 3.16 % vs 2.76 % -> 1.145 (n 97)

**THETA 20 pts, dia**: 2727 extremos (80.2 por sesion, 12.51 por hora), con mecha >= 25 % del rango: 72.9 %; velas ambiguas (todas las sesiones y ventanas): 775

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 1.020 [0.810-1.278] | 10.634 | 10.422 | 536 | 2.09 | 2.25 | 2.05 | 0.929 | 1.02 |
| G100_tol2 | 0.956 [0.785-1.163] | 9.981 | 10.445 | 1052 | 3.85 | 4.25 | 4.02 | 0.906 | 0.957 |
| G50_tol1 | 0.948 [0.842-1.066] | 9.903 | 10.450 | 1131 | 4.11 | 4.50 | 4.32 | 0.913 | 0.95 |
| G50_tol2 | 0.887 [0.781-1.009] | 9.336 | 10.525 | 2153 | 7.37 | 8.50 | 8.23 | 0.867 | 0.895 |
| G25_tol1 | 0.999 [0.908-1.089] | 10.415 | 10.428 | 2362 | 9.02 | 9.00 | 9.03 | 1.002 | 0.999 |
| G25_tol2 | 0.972 [0.889-1.058] | 10.187 | 10.476 | 4437 | 16.57 | 17.00 | 16.96 | 0.975 | 0.977 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 2.24 % vs 2.14 % -> 1.047 (n 61); justo en el redondo (0) 0.33 % vs 0.26 % -> 1.288 (n 9); pasado (+0.25..+2) 1.28 % vs 1.63 % -> 0.788 (n 35); pasado (+2.25..+5) 3.12 % vs 2.89 % -> 1.078 (n 85)

**THETA 40 pts, noche**: 791 extremos (23.3 por sesion, 1.50 por hora), con mecha >= 25 % del rango: 75.5 %; velas ambiguas (todas las sesiones y ventanas): 106

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 1.061 [0.621-1.569] | 1.325 | 1.249 | 1283 | 2.15 | 2.25 | 2.03 | 0.955 | 1.059 |
| G100_tol2 | 1.101 [0.720-1.546] | 1.372 | 1.246 | 2405 | 4.17 | 4.25 | 3.80 | 0.982 | 1.097 |
| G50_tol1 | 1.248 [0.951-1.580] | 1.544 | 1.237 | 2785 | 5.44 | 4.50 | 4.40 | 1.208 | 1.234 |
| G50_tol2 | 1.070 [0.816-1.350] | 1.331 | 1.244 | 5261 | 8.85 | 8.50 | 8.32 | 1.041 | 1.064 |
| G25_tol1 | 1.140 [0.943-1.343] | 1.408 | 1.236 | 5610 | 9.99 | 9.00 | 8.87 | 1.110 | 1.126 |
| G25_tol2 | 0.958 [0.821-1.115] | 1.207 | 1.260 | 10604 | 16.18 | 17.00 | 16.77 | 0.952 | 0.965 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 2.15 % vs 1.85 % -> 1.160 (n 17); justo en el redondo (0) 0.25 % vs 0.27 % -> 0.930 (n 2); pasado (+0.25..+2) 1.77 % vs 1.68 % -> 1.055 (n 14); pasado (+2.25..+5) 2.78 % vs 2.76 % -> 1.007 (n 22)

**THETA 40 pts, dia**: 886 extremos (26.1 por sesion, 4.07 por hora), con mecha >= 25 % del rango: 72.8 %; velas ambiguas (todas las sesiones y ventanas): 106

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 0.935 [0.564-1.394] | 3.172 | 3.392 | 536 | 1.92 | 2.25 | 2.05 | 0.853 | 0.936 |
| G100_tol2 | 0.836 [0.587-1.152] | 2.852 | 3.410 | 1052 | 3.39 | 4.25 | 4.02 | 0.797 | 0.842 |
| G50_tol1 | 0.883 [0.658-1.125] | 3.006 | 3.405 | 1131 | 3.84 | 4.50 | 4.32 | 0.853 | 0.887 |
| G50_tol2 | 0.795 [0.623-0.991] | 2.740 | 3.446 | 2153 | 6.66 | 8.50 | 8.23 | 0.783 | 0.809 |
| G25_tol1 | 1.069 [0.875-1.283] | 3.599 | 3.367 | 2362 | 9.59 | 9.00 | 9.03 | 1.066 | 1.062 |
| G25_tol2 | 1.006 [0.875-1.153] | 3.403 | 3.384 | 4437 | 17.04 | 17.00 | 16.96 | 1.003 | 1.005 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 2.14 % vs 2.14 % -> 1.003 (n 19); justo en el redondo (0) 0.45 % vs 0.26 % -> 1.762 (n 4); pasado (+0.25..+2) 0.79 % vs 1.63 % -> 0.485 (n 7); pasado (+2.25..+5) 3.05 % vs 2.89 % -> 1.054 (n 27)

## MNQ m2 replica (>= 09-18)  (13 sesiones, 2026-09-17 22:00 a 2026-10-06 20:58)

**THETA 10 pts, noche**: 2688 extremos (206.8 por sesion, 13.34 por hora), con mecha >= 25 % del rango: 77.3 %; velas ambiguas (todas las sesiones y ventanas): 1172

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 0.980 [0.800-1.175] | 21.799 | 22.244 | 289 | 2.34 | 2.25 | 2.39 | 1.042 | 0.98 |
| G100_tol2 | 0.999 [0.862-1.129] | 22.201 | 22.235 | 536 | 4.43 | 4.25 | 4.43 | 1.042 | 0.999 |
| G50_tol1 | 1.011 [0.849-1.190] | 22.464 | 22.222 | 552 | 4.61 | 4.50 | 4.57 | 1.025 | 1.01 |
| G50_tol2 | 1.047 [0.919-1.176] | 23.183 | 22.146 | 1018 | 8.78 | 8.50 | 8.42 | 1.033 | 1.043 |
| G25_tol1 | 1.036 [0.923-1.142] | 22.969 | 22.162 | 1071 | 9.15 | 9.00 | 8.86 | 1.017 | 1.033 |
| G25_tol2 | 1.019 [0.909-1.142] | 22.587 | 22.161 | 2041 | 17.15 | 17.00 | 16.88 | 1.009 | 1.016 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 1.97 % vs 2.10 % -> 0.939 (n 53); justo en el redondo (0) 0.33 % vs 0.32 % -> 1.038 (n 9); pasado (+0.25..+2) 2.12 % vs 2.01 % -> 1.055 (n 57); pasado (+2.25..+5) 2.72 % vs 2.93 % -> 0.928 (n 73)

**THETA 10 pts, dia**: 1288 extremos (99.1 por sesion, 15.25 por hora), con mecha >= 25 % del rango: 76.8 %; velas ambiguas (todas las sesiones y ventanas): 1172

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 1.033 [0.690-1.408] | 26.250 | 25.401 | 80 | 1.63 | 2.25 | 1.58 | 0.725 | 1.033 |
| G100_tol2 | 1.110 [0.892-1.350] | 28.090 | 25.317 | 178 | 3.88 | 4.25 | 3.51 | 0.913 | 1.105 |
| G50_tol1 | 1.020 [0.779-1.300] | 25.907 | 25.395 | 193 | 3.88 | 4.50 | 3.81 | 0.863 | 1.019 |
| G50_tol2 | 1.126 [0.946-1.328] | 28.351 | 25.171 | 388 | 8.54 | 8.50 | 7.66 | 1.005 | 1.116 |
| G25_tol1 | 0.950 [0.845-1.052] | 24.242 | 25.523 | 429 | 8.07 | 9.00 | 8.46 | 0.897 | 0.954 |
| G25_tol2 | 1.042 [0.976-1.105] | 26.303 | 25.237 | 844 | 17.24 | 17.00 | 16.65 | 1.014 | 1.035 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 2.25 % vs 2.01 % -> 1.119 (n 29); justo en el redondo (0) 0.39 % vs 0.28 % -> 1.405 (n 5); pasado (+0.25..+2) 1.24 % vs 1.22 % -> 1.015 (n 16); pasado (+2.25..+5) 2.56 % vs 2.64 % -> 0.969 (n 33)

**THETA 20 pts, noche**: 1095 extremos (84.2 por sesion, 5.43 por hora), con mecha >= 25 % del rango: 76.2 %; velas ambiguas (todas las sesiones y ventanas): 335

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 0.798 [0.576-1.001] | 7.266 | 9.101 | 289 | 1.92 | 2.25 | 2.39 | 0.852 | 0.802 |
| G100_tol2 | 0.775 [0.578-1.034] | 7.090 | 9.148 | 536 | 3.47 | 4.25 | 4.43 | 0.817 | 0.783 |
| G50_tol1 | 0.979 [0.716-1.239] | 8.877 | 9.066 | 552 | 4.47 | 4.50 | 4.57 | 0.994 | 0.98 |
| G50_tol2 | 0.892 [0.672-1.144] | 8.153 | 9.140 | 1018 | 7.58 | 8.50 | 8.42 | 0.892 | 0.9 |
| G25_tol1 | 0.955 [0.773-1.135] | 8.683 | 9.093 | 1071 | 8.49 | 9.00 | 8.86 | 0.944 | 0.959 |
| G25_tol2 | 0.855 [0.683-1.068] | 7.937 | 9.285 | 2041 | 14.79 | 17.00 | 16.88 | 0.870 | 0.876 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 1.55 % vs 2.10 % -> 0.739 (n 17); justo en el redondo (0) 0.46 % vs 0.32 % -> 1.416 (n 5); pasado (+0.25..+2) 1.46 % vs 2.01 % -> 0.727 (n 16); pasado (+2.25..+5) 3.11 % vs 2.93 % -> 1.060 (n 34)

**THETA 20 pts, dia**: 819 extremos (63.0 por sesion, 9.70 por hora), con mecha >= 25 % del rango: 78.4 %; velas ambiguas (todas las sesiones y ventanas): 335

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 1.084 [0.638-1.590] | 17.500 | 16.139 | 80 | 1.71 | 2.25 | 1.58 | 0.760 | 1.083 |
| G100_tol2 | 1.153 [0.822-1.521] | 18.539 | 16.074 | 178 | 4.03 | 4.25 | 3.51 | 0.948 | 1.147 |
| G50_tol1 | 1.060 [0.720-1.453] | 17.098 | 16.123 | 193 | 4.03 | 4.50 | 3.81 | 0.895 | 1.058 |
| G50_tol2 | 1.198 [0.929-1.514] | 19.072 | 15.919 | 388 | 9.04 | 8.50 | 7.66 | 1.063 | 1.18 |
| G25_tol1 | 0.948 [0.778-1.124] | 15.385 | 16.232 | 429 | 8.06 | 9.00 | 8.46 | 0.895 | 0.952 |
| G25_tol2 | 1.059 [0.939-1.188] | 16.943 | 16.004 | 844 | 17.46 | 17.00 | 16.65 | 1.027 | 1.048 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 2.56 % vs 2.01 % -> 1.274 (n 21); justo en el redondo (0) 0.49 % vs 0.28 % -> 1.768 (n 4); pasado (+0.25..+2) 0.98 % vs 1.22 % -> 0.798 (n 8); pasado (+2.25..+5) 3.05 % vs 2.64 % -> 1.154 (n 25)

**THETA 40 pts, noche**: 268 extremos (20.6 por sesion, 1.33 por hora), con mecha >= 25 % del rango: 73.9 %; velas ambiguas (todas las sesiones y ventanas): 61

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 1.419 [0.889-2.039] | 3.114 | 2.195 | 289 | 3.36 | 2.25 | 2.39 | 1.493 | 1.405 |
| G100_tol2 | 1.010 [0.717-1.359] | 2.239 | 2.216 | 536 | 4.48 | 4.25 | 4.43 | 1.054 | 1.01 |
| G50_tol1 | 1.327 [0.982-1.683] | 2.899 | 2.184 | 552 | 5.97 | 4.50 | 4.57 | 1.327 | 1.308 |
| G50_tol2 | 1.070 [0.776-1.370] | 2.358 | 2.204 | 1018 | 8.96 | 8.50 | 8.42 | 1.054 | 1.064 |
| G25_tol1 | 1.105 [0.825-1.369] | 2.428 | 2.196 | 1071 | 9.70 | 9.00 | 8.86 | 1.078 | 1.095 |
| G25_tol2 | 0.889 [0.671-1.129] | 2.009 | 2.259 | 2041 | 15.30 | 17.00 | 16.88 | 0.900 | 0.906 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 2.61 % vs 2.10 % -> 1.243 (n 7); justo en el redondo (0) 1.12 % vs 0.32 % -> 3.470 (n 3); pasado (+0.25..+2) 0.75 % vs 2.01 % -> 0.371 (n 2); pasado (+2.25..+5) 2.99 % vs 2.93 % -> 1.019 (n 8)

**THETA 40 pts, dia**: 361 extremos (27.8 por sesion, 4.27 por hora), con mecha >= 25 % del rango: 74.2 %; velas ambiguas (todas las sesiones y ventanas): 61

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G100_tol1 | 1.594 [0.860-2.455] | 11.250 | 7.057 | 80 | 2.49 | 2.25 | 1.58 | 1.108 | 1.579 |
| G100_tol2 | 1.442 [0.960-1.931] | 10.112 | 7.014 | 178 | 4.99 | 4.25 | 3.51 | 1.173 | 1.42 |
| G50_tol1 | 1.248 [0.725-1.820] | 8.808 | 7.056 | 193 | 4.71 | 4.50 | 3.81 | 1.046 | 1.237 |
| G50_tol2 | 1.336 [0.906-1.818] | 9.278 | 6.944 | 388 | 9.97 | 8.50 | 7.66 | 1.173 | 1.303 |
| G25_tol1 | 0.945 [0.642-1.284] | 6.760 | 7.157 | 429 | 8.03 | 9.00 | 8.46 | 0.893 | 0.949 |
| G25_tol2 | 1.018 [0.818-1.242] | 7.227 | 7.102 | 844 | 16.90 | 17.00 | 16.65 | 0.994 | 1.015 |

Sobrepaso respecto de G100 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 3.05 % vs 2.01 % -> 1.514 (n 11); justo en el redondo (0) 0.55 % vs 0.28 % -> 2.006 (n 2); pasado (+0.25..+2) 1.39 % vs 1.22 % -> 1.132 (n 5); pasado (+2.25..+5) 2.49 % vs 2.64 % -> 0.943 (n 9)

## MES m1 (08-03..10-05)  (45 sesiones, 2026-08-03 22:00 a 2026-10-05 20:59)

**THETA 3 pts, noche**: 3972 extremos (88.3 por sesion, 5.69 por hora), con mecha >= 25 % del rango: 74.1 %; velas ambiguas (todas las sesiones y ventanas): 1126

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G25_tol0.5 | 1.127 [0.925-1.367] | 5.317 | 4.718 | 3893 | 5.21 | 5.00 | 4.65 | 1.042 | 1.12 |
| G25_tol1 | 1.085 [0.923-1.284] | 5.113 | 4.712 | 7022 | 9.04 | 9.00 | 8.39 | 1.004 | 1.077 |
| G10_tol0.5 | 1.072 [0.961-1.198] | 5.045 | 4.705 | 10029 | 12.74 | 12.50 | 11.98 | 1.019 | 1.063 |
| G10_tol1 | 1.090 [0.997-1.191] | 5.076 | 4.655 | 17966 | 22.96 | 22.50 | 21.47 | 1.020 | 1.07 |
| G5_tol0.5 | 1.037 [0.951-1.127] | 4.875 | 4.702 | 21068 | 25.86 | 25.00 | 25.17 | 1.034 | 1.027 |
| G5_tol1 | 1.041 [0.958-1.128] | 4.852 | 4.659 | 37720 | 46.07 | 45.00 | 45.07 | 1.024 | 1.022 |

Sobrepaso respecto de G25 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 7.40 % vs 7.35 % -> 1.007 (n 294); justo en el redondo (0) 1.23 % vs 0.94 % -> 1.319 (n 49); pasado (+0.25..+2) 8.03 % vs 7.34 % -> 1.094 (n 319); pasado (+2.25..+5) 12.29 % vs 10.97 % -> 1.120 (n 488)

**THETA 3 pts, dia**: 4719 extremos (104.9 por sesion, 16.30 por hora), con mecha >= 25 % del rango: 74.4 %; velas ambiguas (todas las sesiones y ventanas): 1126

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G25_tol0.5 | 0.895 [0.802-0.991] | 12.230 | 13.665 | 1946 | 5.04 | 5.00 | 5.60 | 1.009 | 0.9 |
| G25_tol1 | 0.881 [0.806-0.972] | 12.117 | 13.749 | 3491 | 8.96 | 9.00 | 10.05 | 0.996 | 0.892 |
| G10_tol0.5 | 0.949 [0.870-1.038] | 12.979 | 13.671 | 4330 | 11.91 | 12.50 | 12.46 | 0.953 | 0.955 |
| G10_tol1 | 0.954 [0.892-1.023] | 13.097 | 13.725 | 7765 | 21.55 | 22.50 | 22.35 | 0.958 | 0.964 |
| G5_tol0.5 | 0.976 [0.915-1.042] | 13.342 | 13.667 | 8837 | 24.98 | 25.00 | 25.44 | 0.999 | 0.982 |
| G5_tol1 | 0.950 [0.904-1.000] | 13.207 | 13.899 | 15772 | 44.14 | 45.00 | 45.40 | 0.981 | 0.972 |

Sobrepaso respecto de G25 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 8.05 % vs 8.62 % -> 0.935 (n 380); justo en el redondo (0) 1.02 % vs 1.09 % -> 0.932 (n 48); pasado (+0.25..+2) 8.31 % vs 9.20 % -> 0.903 (n 392); pasado (+2.25..+5) 12.02 % vs 12.88 % -> 0.933 (n 567)

**THETA 5 pts, noche**: 1478 extremos (32.8 por sesion, 2.12 por hora), con mecha >= 25 % del rango: 74.8 %; velas ambiguas (todas las sesiones y ventanas): 247

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G25_tol0.5 | 1.034 [0.794-1.329] | 1.824 | 1.763 | 3893 | 4.80 | 5.00 | 4.65 | 0.961 | 1.033 |
| G25_tol1 | 0.974 [0.773-1.235] | 1.723 | 1.770 | 7022 | 8.19 | 9.00 | 8.39 | 0.910 | 0.976 |
| G10_tol0.5 | 1.025 [0.865-1.214] | 1.805 | 1.761 | 10029 | 12.25 | 12.50 | 11.98 | 0.980 | 1.022 |
| G10_tol1 | 1.106 [0.958-1.272] | 1.909 | 1.727 | 17966 | 23.21 | 22.50 | 21.47 | 1.031 | 1.081 |
| G5_tol0.5 | 1.069 [0.949-1.185] | 1.856 | 1.736 | 21068 | 26.45 | 25.00 | 25.17 | 1.058 | 1.051 |
| G5_tol1 | 1.091 [0.969-1.213] | 1.850 | 1.697 | 37720 | 47.23 | 45.00 | 45.07 | 1.049 | 1.048 |

Sobrepaso respecto de G25 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 6.43 % vs 7.35 % -> 0.875 (n 95); justo en el redondo (0) 1.22 % vs 0.94 % -> 1.302 (n 18); pasado (+0.25..+2) 8.32 % vs 7.34 % -> 1.133 (n 123); pasado (+2.25..+5) 11.98 % vs 10.97 % -> 1.092 (n 177)

**THETA 5 pts, dia**: 2109 extremos (46.9 por sesion, 7.29 por hora), con mecha >= 25 % del rango: 75.1 %; velas ambiguas (todas las sesiones y ventanas): 247

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G25_tol0.5 | 0.963 [0.822-1.129] | 5.858 | 6.084 | 1946 | 5.41 | 5.00 | 5.60 | 1.081 | 0.965 |
| G25_tol1 | 0.948 [0.821-1.090] | 5.786 | 6.103 | 3491 | 9.58 | 9.00 | 10.05 | 1.064 | 0.953 |
| G10_tol0.5 | 1.014 [0.889-1.148] | 6.143 | 6.061 | 4330 | 12.61 | 12.50 | 12.46 | 1.009 | 1.012 |
| G10_tol1 | 0.985 [0.881-1.103] | 6.001 | 6.091 | 7765 | 22.10 | 22.50 | 22.35 | 0.982 | 0.988 |
| G5_tol0.5 | 0.971 [0.890-1.056] | 5.941 | 6.116 | 8837 | 24.89 | 25.00 | 25.44 | 0.996 | 0.979 |
| G5_tol1 | 0.945 [0.889-1.002] | 5.884 | 6.227 | 15772 | 44.00 | 45.00 | 45.40 | 0.978 | 0.969 |

Sobrepaso respecto de G25 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 7.97 % vs 8.62 % -> 0.925 (n 168); justo en el redondo (0) 1.04 % vs 1.09 % -> 0.956 (n 22); pasado (+0.25..+2) 8.39 % vs 9.20 % -> 0.912 (n 177); pasado (+2.25..+5) 12.33 % vs 12.88 % -> 0.957 (n 260)

**THETA 10 pts, noche**: 382 extremos (8.5 por sesion, 0.55 por hora), con mecha >= 25 % del rango: 74.9 %; velas ambiguas (todas las sesiones y ventanas): 15

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G25_tol0.5 | 1.073 [0.659-1.645] | 0.488 | 0.455 | 3893 | 4.97 | 5.00 | 4.65 | 0.995 | 1.069 |
| G25_tol1 | 0.864 [0.582-1.258] | 0.399 | 0.462 | 7022 | 7.33 | 9.00 | 8.39 | 0.814 | 0.874 |
| G10_tol0.5 | 1.157 [0.847-1.531] | 0.518 | 0.448 | 10029 | 13.61 | 12.50 | 11.98 | 1.089 | 1.136 |
| G10_tol1 | 1.111 [0.875-1.380] | 0.495 | 0.446 | 17966 | 23.30 | 22.50 | 21.47 | 1.035 | 1.085 |
| G5_tol0.5 | 1.040 [0.848-1.270] | 0.470 | 0.452 | 21068 | 25.92 | 25.00 | 25.17 | 1.037 | 1.03 |
| G5_tol1 | 1.041 [0.876-1.237] | 0.467 | 0.448 | 37720 | 46.07 | 45.00 | 45.07 | 1.024 | 1.022 |

Sobrepaso respecto de G25 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 8.12 % vs 7.35 % -> 1.105 (n 31); justo en el redondo (0) 0.79 % vs 0.94 % -> 0.839 (n 3); pasado (+0.25..+2) 6.54 % vs 7.34 % -> 0.891 (n 25); pasado (+2.25..+5) 14.40 % vs 10.97 % -> 1.313 (n 55)

**THETA 10 pts, dia**: 545 extremos (12.1 por sesion, 1.88 por hora), con mecha >= 25 % del rango: 75.0 %; velas ambiguas (todas las sesiones y ventanas): 15

| grilla | RR [IC90] | tasa en redondo % | tasa fuera % | puntas en redondo | recall % | cobertura % | ocupacion % | lift geom | lift ocup |
|---|---|---|---|---|---|---|---|---|---|
| G25_tol0.5 | 0.609 [0.360-0.913] | 0.976 | 1.604 | 1946 | 3.49 | 5.00 | 5.60 | 0.697 | 0.622 |
| G25_tol1 | 0.747 [0.521-1.026] | 1.203 | 1.610 | 3491 | 7.71 | 9.00 | 10.05 | 0.856 | 0.767 |
| G10_tol0.5 | 0.820 [0.646-1.016] | 1.316 | 1.605 | 4330 | 10.46 | 12.50 | 12.46 | 0.837 | 0.839 |
| G10_tol1 | 0.929 [0.760-1.132] | 1.481 | 1.594 | 7765 | 21.10 | 22.50 | 22.35 | 0.938 | 0.944 |
| G5_tol0.5 | 0.881 [0.754-1.018] | 1.426 | 1.618 | 8837 | 23.12 | 25.00 | 25.44 | 0.925 | 0.909 |
| G5_tol1 | 0.997 [0.875-1.133] | 1.566 | 1.571 | 15772 | 45.32 | 45.00 | 45.40 | 1.007 | 0.998 |

Sobrepaso respecto de G25 (razon = % de extremos en la banda / % de todas las puntas en la banda): antes (-2..-0.25) 6.61 % vs 8.62 % -> 0.767 (n 36); justo en el redondo (0) 0.73 % vs 1.09 % -> 0.673 (n 4); pasado (+0.25..+2) 7.71 % vs 9.20 % -> 0.838 (n 42); pasado (+2.25..+5) 11.93 % vs 12.88 % -> 0.926 (n 65)

