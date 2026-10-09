---
name: investigacion-formula-extremos-1009
description: "Investigación científica 09-10 (web + 19 noches propias, pre-registrada, contra placebo, fuera de muestra): ninguna fórmula (2.0, 4.1 ni 8 nuevas) hace girar al precio en la raya más que el azar; la conversión correcta es la sincronizada de la 4.1; hacen falta ~40-60 noches para ver 4-6 pp"
metadata:
  node_type: memory
  type: project
  originSessionId: f5879819-e2a4-405b-87b0-959ace090d7b
  modified: 2026-10-09T05:41:03.933Z
---

Informe completo: PythiaGex/laboratorio/calibracion_1009/INFORME.md (PREREGISTRO.md con sha256; juez del operador en
criterio_operador/juez/juez_operador.py; receta exacta de la 2.0 en receta_2_0/ con paridad 137/138).

- **Literatura:** nadie midió giros intradía en la dominante de GEX contra placebo. Pin existe pero chico, al cierre del vencimiento y
  con entrega física (Golez-Jackwerth 2012: futuros del S&P sí, SPX no). El signo decide imán/repulsión (Avellaneda-Lipkin 2003);
  separar por signo no cambió nada en nuestros datos. Lo medido de verdad es el RÉGIMEN (Baltussen 2021, Amaya-Pearson-Vasquez 2025),
  no el lugar. Osler 2000 (Fed NY): S/R publicados rebotan 60,8 % contra 56,2 % al azar (+4,6 pp) con miles de toques.
- **Proveedores** (SpotGamma, MenthorQ, GEXbot...): publican tasas de "aguante" sin placebo; un borde a ~1-1,6 desvíos da lo mismo al azar.
  El "King node" de los practicantes (mayor |GEX| sin signo) = la selección de la 2.0.
- **Medido (19 noches, vara "gira en la raya", modo tolerante con falso rompimiento):** 2.0 y 4.1 ganan 0 de 18 pruebas en PRUEBA
  (p mínimo 0,148 sin ajustar); 8 fórmulas nuevas 0 de 8 ni en entrenamiento; rayas reales a ~1 pp de su placebo.
- **Conversión:** la sincronizada de la 4.1 es la correcta; la de la 2.0 (MNQ 16:14 / QQQ de las 20:00) erra mediana 27 pts por noche,
  p90 169 pts en K750. La C8 tiene sesgo +5,8 pts contra la mañana siguiente (escalón de sesión ~0,0077): corrección sin validar con la vara.
- **Muestra:** con 6 noches de PRUEBA solo se ven ventajas de 15-24 pp; para 5 pp con 1 hipótesis hacen falta 38-44 noches útiles
  (fines de diciembre), con 3 hipótesis 55-63 (fines de enero 2027).
- Después vino la vara de 20 pts del operador ([[objetivo-dominantes-extremo-que-aguanta]]): campeonato en
  laboratorio/calibracion_1009/campeonato20/ (PODIO.md). Ver [[laboratorio-formulas]], [[busqueda-estrategia-sin-parar]].
- **Con la vara del operador (punta del extremo + aguanta con falso rompimiento + recorrido), 19 noches, ventana congelada
  (criterio_operador/, esceptico incluido):** la 2.0 dibujada 47,5 % sostenidas (306 llegadas, n efectivo ~56 raya-noche) contra
  ~45 % de corridas/azar: no se distingue, y la poca ventaja depende de la noche 09-10. La noche 08→09-10 la 2.0 sí hizo lo que él pide
  (31083,50: 6 techos sostenidos, 8/9, llegó a la opuesta 31042,06 todas las veces), pero fue UNA noche y por su error de conversión
  (+6,8 pts). Híbrido (selección 2.0 + razón C8) y las QQQ de la 4.1: igual al azar. Niveles de precio solos (redondos, VWAP, pivotes):
  tampoco. Único candidato a seguir: el zero de QQQ (recorrido tras sostener percentil 96-99,6) con precisión peor y pocos casos.
  El esceptico encontró un bug en r20 (cobertura dividida dos veces): la "cobertura significativa" de la 2.0 se cae.
- **CAMPEONATO 20 PTS (vara de scalping, 09-10 07:16Z, campeonato20/PODIO.md, 2 escépticos con juez propio):** NADA sobrevive
  fuera de muestra (Holm MIRAR mejor p 0,419; PRUEBA 1,0). Tasa base: una raya cualquiera tocada aguanta 20 pts ~38-41 % de noche y
  ~41-47 % de día. Lo que asomó en MIRAR cayó: R20_NDX (ruido de base), MUROS_NQ_vol (era el número redondo de 100), CONF (parpadeo).
  "Mapa" (arriba del azar en las dos mitades, sin significancia): MUROS_NQ_oi (43,1→43,3 %, la más pareja), MUROS_NDX_vol (o solo P),
  MAJORS_NDX_vol. Más ruido: ZTP_NDX (~27 falsas/noche), 2.0 QQQ (17-21 falsas/noche, 6-8 rayas/h; debajo del azar), R20_NDX,
  MUROS_NDX_oi, MAJORS_NQ_oi. Ejemplos del operador: E1 NDX 31040,74 acierto (127 pts), E2 NQ P OI 31100 acierto, E3 cruce 31064
  3 aciertos/2 falsas, E4 31112 sin llegada, E5 NQ M- 31050 2 aciertos. Para ver +5 pp: 24-38 noches NUEVAS con 1 candidata
  pre-registrada (34-54 con 3), desde la sesión del 10-10, con ATAS abierto toda la noche (libro NQ) y controles de grilla redonda.
  Propuesta de apagar por ruido = SUYA la decisión (avisar + captura antes/después).
