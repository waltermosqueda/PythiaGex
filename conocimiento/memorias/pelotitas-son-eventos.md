---
name: pelotitas-son-eventos
description: "Las pelotitas de GAMMAlito no son niveles: son operaciones grandes. Y las barras sí se mueven verticalmente, poco pero de verdad. Medido sobre 2443 cuadros."
metadata:
  type: project
---

Medido el 2026-09-04 sobre **2443 cuadros** de dos videos, con detector de color
propio y control de paneo. Confirma las dos observaciones del operador.

## Las pelotitas son EVENTOS, no niveles

En el video web (ES_SPX, 2 minutos, el modo dice **BIGTRADE** en pantalla) las
pelotitas azules viven adentro del histograma de la izquierda. Sobre 39.561
pelotitas:

- **43% de las barras tiene dos, 13% tiene tres, algunas cinco.** Un nivel no
  puede estar en dos lugares; una operacion si se repite.
- **Van de 6 a 77 por cuadro** y la cantidad cambia en el 27% de los cuadros.
- **El 43% cae mas alla del extremo de su propia barra**, asi que no son una
  fraccion del largo.

Son las operaciones grandes, no niveles de soporte o resistencia.

## Las barras SI se mueven verticalmente

En el video de NinjaTrader (MNQ DEC25, 30 segundos) se compararon **1020 pares
de cuadros con la rejilla del eje identica pixel a pixel** -- o sea con el eje
demostrablemente quieto:

- se movieron el **4,2%** de las barras izquierdas y el **3,3%** de las derechas
- descontando el suavizado del dibujo: corrimiento tipico **1,4 a 1,6 puntos**
  de MNQ, maximo 5,5

**Control de paneo:** en **816 pares, cero veces** se movieron todas las barras
lo mismo. Si fuera camara se moverian juntas. Es dato.

## Por que las nuestras salen horizontales

Nuestro propio registro, 731 muestras de ES en 399 minutos seguidos, emparejando
los niveles **por precio** (ver la advertencia de abajo): las dominantes **se
quedan clavadas en el 41% de las muestras** y el paso tipico es de **0,001
puntos**. O sea: no se mueven.

La causa es estructural: **elegir**. Mientras gana el mismo strike, el nivel
elegido no se mueve nada.

GAMMAlito **no elige**: se midieron **284 lineas de nivel** del producto real,
cada una por separado y sobre el amarillo (ninguna vela de ese grafico es
amarilla, asi que no se contamina) y **ninguna es plana -- cero de 284**. Toman
unas 13 alturas distintas por pantalla, con escalones de 2,4 px.

## LA TRAMPA DE MEDICION QUE ME COMI (dos veces el mismo dia)

Los picos se publican **ordenados por peso, no por precio**. Comparar "el nivel
3" de una muestra contra "el nivel 3" de la siguiente compara dos cumulos
distintos cada vez que se pasan en fuerza, e **inventa saltos enormes que no
existen**.

Asi llegue a afirmar "saltos de hasta 117 puntos", y lo repeti varias veces y lo
deje escrito aca. **Es falso.** Emparejando por precio, el salto mas grande de la
version vieja era de **7,56 puntos**.

Regla: cualquier serie que se publique ordenada por una cosa y se compare por
otra hay que emparejarla explicitamente. Nunca por posicion en la lista.

## Dos errores mios corregidos en el camino

0. **El "salto de 117 puntos" no existia** (ver arriba): artefacto de
   emparejar por posicion en la lista en vez de por precio.
1. **La escala estaba mal por tres veces.** El detector agarraba una linea de
   rejilla de cada tres. Las etiquetas del eje dan 87,5 px cada 20 puntos:
   **1 px = 0,229 puntos**, no 0,076.
2. **El "puntito blanco" del video de NinjaTrader no es un punto: es un numerito
   impreso sobre la barra.** Lo detecte agrandando la imagen antes de sacar
   ninguna formula.

## Lo que NO esta medido

- **Por que** se mueven las barras 1,5 puntos. No hay mecanismo demostrado.
- **Que codifica la posicion horizontal** de la pelotita: tamano o momento.

**Why:** el operador venia dudando de que nuestras dominantes cumplieran su
funcion, y tenia razon por un motivo que no era el que creiamos: no es que esten
mal ubicadas, es que estamos dibujando otra cosa.

**How to apply:** el cambio concreto es dejar de elegir seis picos y dibujar el
perfil entero sobre la rejilla de strikes. Ver [[radar-dominantes-bigtrades]],
[[dominantes-no-son-linea]] y [[calcular-gex-propio]].
