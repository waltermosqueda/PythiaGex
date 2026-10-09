# CONGELADO — probador 1 (grupo 1)

Escrito 2026-10-09T05:12:03+00:00 UTC (2026-10-09T02:12:03 ART), ANTES de construir niveles de PRUEBA y antes de llamar al arnes con abrir_prueba=True.

- sello: `probador1/finalistas.json`, sha256 `7503509abcb1b805aa4c467b1e4c801180a7dbb27dd202946ab56f2b3141668e`
- parametros libres: ninguno (PREREGISTRO sec. 6 (3)). Lo que se congela es el codigo y las interpretaciones de abajo.
- finalistas del grupo 1 por ventana: noche ninguna; dia ninguna
- van a PRUEBA: C01_DOS_QQQ, C02_DOS_NDX, C03_DOS_QQQ_C41 (n_azar = 2000, n_boot = 2000)
- no van: C05 (control), C01s dos_log (sensibilidad informativa)

## Interpretaciones declaradas (las mas conservadoras que encontre)

- **mas_cerca**: min(dias - env >= 0) sobre TODOS los vencimientos de la foto (fotos().vencs), como receta_2_0:109 (cadena['vencimientos']); las filas guardadas son solo las de la banda +-6 %.
- **iv_faltante**: iv NaN o <= 0 -> gamma 0 (receta_2_0:49 devuelve 0 con iv <= 0).
- **vol_oi_faltante**: vol/oi NaN -> 0.
- **ahora**: el instante de la cuenta es t (apertura de la vela en la que vale la raya): env se mide de 'generado' a t.
- **C04_rueda**: D = la rueda (fecha NY habil) mas reciente con t >= 16:00 NY de D y >= 20 muestras vivas con t_spot 09:35-15:59 NY y 'generado' <= t (causal); si el contrato de esa rueda no es el del grafico en t (roll) no hay raya (igual que la base NDX 'cuatro', cargar.py:322-324). 09:30-09:34 NY usa la deriva (la 'cuatro' arranca 09:35).
- **C05_cantidad**: en cada cambio del CONJUNTO de strikes de C03 (o primer minuto con rayas) se sortean n = |C03(t)| strikes sin reemplazo del pozo (|K*rho_cuatro - F| <= R y sum_Hoy(vol_c + vol_p) > 0, K ordenados); si el pozo tiene menos de n se sortean los que haya (en minutos de caida a OI de C03 el pozo puede estar vacio: sin rayas). Los strikes se mantienen (aunque salgan del radio) mientras C03 no cambie; solo hay rayas de C05 en minutos con rayas de C03; nivel = K * rho_cuatro(t).
- **C06_G**: G(t) con radio 0,02*F SIN el tope de 100 pts (asi lo escribe la sec. 7 para C06), GEX_oi + GEX_vol de Hoy.

## sha256 del codigo

- `candidatas.py` cc380a119f251579eff1b15013eee478ada2abe477539a2844752816f392e331
- `caso.py` 9363f13e1ffb4d775a1b822ab3b2aaa9babf6c36c307f15adaec6462ed04f24b
- `congelar.py` 0765fc1f40510337b155a41a9fca9c24a8b86f13f679914f5e8aac5070faaec4
- `entrenar.py` 3d6b1cd1a59eeca620897e07da4d77c67ab3ab3c20962c44d9682e649adec20a
- `probar.py` 3f4484ce582e0374da8cc737d89f853a6ad5aba30d4425f000652deaddd40088
- `verificar.py` d0a81234c463cea4135d47c49021528c863c8549f522ab4725ecf9da5c8d27f5

## sha256 de los resultados de ENTRENAMIENTO

- `C01_DOS_QQQ.json` 647534223a6a005c8f3456f18e091800e8ddba49cafd151b05506d5f17824cb0
- `C01_DOS_QQQ_m2.json` c50e50f5615d0bdd0af20130961df401b8da7837b4e27ea7216656c039dbaf19
- `C01s_DOS_QQQ_doslog.json` a6fd965ecf3cbbff19360e402486f60ea11ae8c1465a7498acbd33236bb4e614
- `C02_DOS_NDX.json` f2d3c509a2f89aedc24d2c240e61496e07ef7704c196fd6081388b1c9e80789c
- `C02_DOS_NDX_m2.json` d385607a2b852ec26cc27c6391c076cd74580b94d90986ca942f7a1f4eb28448
- `C03_DOS_QQQ_C41.json` 191a941f7104e7fdbc6919ad65a641f44b95817c5b35817800e23125a295559b
- `C03_DOS_QQQ_C41_m2.json` 781e579b0de6be2461e882f5809738726cc232fddfdc142f65c1c124159838b9
- `C04_DOS_QQQ_DERIVA.json` a8000676dff13db207579eefd1dc216f56cda144ba790b0db03b24871c18ec2a
- `C04_DOS_QQQ_DERIVA_m2.json` 98a274c84728cb35c443d8ca09d1dd7b73c955a23dd167545dfdabc0be02ef2e
- `C05_STRIKE_AZAR_QQQ.json` 045087ed3c5c8c9d1046344ee266261856c9c75c7ce1dba1f8ec634e1dfda395
- `C06_REGIMEN_POS_QQQ.json` d11f9f2d3136b42a183566b7cfb4e6a4159f3765df27dffaada7699b02d1f420
- `C06_REGIMEN_POS_QQQ_m2.json` c988f9745ff0e7f23a724e78dfc82d4e25faef7ac0aa877e8aae2dacb37fc06b
- `niveles_resumen.json` 38b6bff81e58b5b3e0d7418e295541bfff4fd0c8e582e0e027093a014e2a7a29
- `seleccion.json` b11cf1973ad2a307041d4298c8c102a694efc776adcdb97e61b0b9d67de685f5
- `sensibilidad_m2.json` 5dded7f4d40263609e6283e95a1f7a0c015eac17d52ec86ad14ecd541862e3f7
