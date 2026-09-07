# PythiaFlow — Nodos de Volumen

Indicador para ATAS. Fuente: `NodosDeVolumen.cs`. Sale dentro de `PythiaVwap.dll`.
Sirve igual en **ES, MES, NQ y MNQ** sin tocar un solo ajuste, y mas abajo se
explica por que.

---

## La pieza que faltaba de una misma estrategia

No es un indicador suelto. Es la tercera pata de lo que arma el mismo autor de
NinjaTrader en el que venimos mirando:

1. **VWAP anclado con bandas** — el contexto: caro o barato respecto de donde
   se negocio de verdad. Ya esta: `PythiaVWAP - VWAP Anclado`.
2. **Coloreado de order flow** — quien empuja: verde mientras el delta acumulado
   siga para el mismo lado, rojo cuando revierte. Ya esta:
   `PythiaFlow - Tendencia de Order Flow`.
3. **Nodos de volumen** — las referencias horizontales: donde ya se opero mucho.
   Es esto.

Se usan las tres juntas sobre barras de **Delta** (en ATAS, el selector de
periodo trae 500 / 1000 / 1500), que es el equivalente del `500/500 Order Flow
Delta` de NinjaTrader.

## De donde sale, exactamente

De dos capturas de ese indicador sobre MNQ 09-26. En la primera hay lineas
amarillas horizontales y, al lado, la escalera de volumen por precio con varias
filas subrayadas a mano. **Las filas subrayadas son las de los numeros grandes**
—220 y 237 en 29250, 176 y 161 en 29300, 226 en 29400, 317 y 408 en 29500— y
las lineas amarillas caen sobre esos mismos precios.

O sea: las lineas no esconden ninguna formula. Son los precios donde el volumen
se apelmazo. Eso se reconstruye entero con `IndicatorCandle.GetAllPriceLevels()`,
que da, precio por precio de cada vela, el **volumen**, el **Ask** y el **Bid**.

Lo que sigue es reconstruccion a partir de lo que se ve, no una copia del codigo
del autor: no lo tenemos. Los ajustes quedan a la vista para poder discutirlos.

## Que dibuja

**Las lineas.** Suma el volumen de cada precio a lo largo de las ultimas N velas
cerradas y marca los precios que se despegan del resto. El grosor y el brillo
cuentan la fuerza, asi que se lee la jerarquia sin tener que mirar el numero. La
etiqueta trae el precio, el volumen acumulado abreviado y el delta neto de ese
precio: un nodo con delta muy positivo se armo con compradores agresivos, uno
con delta negativo con vendedores.

**Las flechas.** Marcan **absorcion** en un extremo: entro agresion contra el
piso (o el techo) y el precio no la siguio. Las condiciones son tres y tienen
que darse juntas:

- el extremo es un **pivote confirmado**, con velas mas altas (o mas bajas) a
  los dos lados;
- en los ticks del extremo hay mucho mas volumen que en un precio tipico de esa
  misma vela;
- el delta ahi va **en contra** del giro (venden contra el piso) y sin embargo
  la vela cierra del otro lado de su rango.

La confirmacion llega tarde a proposito. Un extremo sin velas del otro lado
todavia no es un extremo, y marcarlo seria dibujar una señal que despues se
borra sola.

## Por que los umbrales son relativos y no en contratos

La captura original tiene **500** y **800** metidos como numeros fijos. Un umbral
fijo sirve para un instrumento y para un dia. NQ y ES no mueven el mismo volumen,
MNQ y MES menos todavia, y una rueda tranquila no se parece a un dia de dato.

Aca todo se mide contra la **mediana del propio instrumento en la ventana**:

- un precio es nodo si acumulo **N veces** el volumen del precio tipico;
- hay agresion en el extremo si junto **N veces** el volumen de un precio tipico
  de esa vela.

Se usa mediana y no promedio a proposito: el promedio lo levanta el mismo nodo
que estamos buscando, asi que el umbral se correria hacia arriba justo cuando
aparece lo que queremos detectar.

La caja arriba a la izquierda muestra **a cuantos contratos equivale el umbral
en ese momento**. Es para poder auditarlo: si el numero no tiene sentido, se ve
sin tener que adivinar.

## El aviso de numero redondo

Cada nodo dice si ademas cae en un multiplo de 10, 25, 50 o 100.

No es decoracion. Midiendo SPX el 2026-09-03 con cinta real salio que la
velocidad de la cinta es **1,102** veces lo normal sobre los multiplos de 50,
**1,012** sobre los de 10 y **0,993** sobre los de 5 — **sin mirar volumen ni
gamma**, solo por ser redondos. Asi que si un nodo ademas es redondo, parte de
su fuerza puede venir de ahi y no del volumen. El indicador lo avisa en vez de
dejar que uno se lo atribuya al volumen.

## Lo que NO dice

Donde va a ir el precio. Un nodo dice que ahi ya se opero mucho, o sea donde
hay interes; no de que lado se va a resolver. La absorcion dice que alguien se
comio una agresion, no que el giro vaya a funcionar.

## Ajustes que importan

- **Velas hacia atras** (300). Mas velas = nodos mas estables y mas viejos.
- **Fuerza minima** (2,5 veces la mediana). Empezo en 4,0 y con 300 velas de
  5 minutos NO pasaba ningun precio: el perfil de 25 horas es mas chato de
  lo que uno supone. Medido, no elegido a ojo.
- **Minimo que se muestra igual** (3). Si ninguno llega al umbral, dibuja los
  mas cargados en punteado. Una pantalla vacia no se distingue de un
  indicador roto.
- **Fusionar precios pegados** (2 ticks). Sin esto, un apelmazamiento sale como
  tres lineas juntas.
- **Velas a cada lado del pivote** (3). Mas alto = menos señales y mas tarde.
- **Donde tiene que cerrar** (0,6). Cuanto tiene que recuperar la vela para que
  el extremo cuente como absorbido.

## Costo

El perfil se rehace **una vez por vela cerrada**, no en cada tick: recorrer 300
velas con su footprint en cada actualizacion voltearia el grafico.

---

## Dos errores que costaron caro, y como se ven si vuelven

**1. Colgaba la plataforma al ponerlo en un grafico de 1 minuto.**

Cuando ATAS calcula el historico llama a `OnCalculate` **una vez por cada vela
del grafico**. Un MNQ de un minuto tiene miles. El perfil se rehacia en cada
una: miles de recorridos de 300 velas con su footprint, millones de operaciones
antes de dibujar el primer pixel. En MES de 5 minutos, con pocas velas, no se
noto; en MNQ de 1 minuto ATAS quedo clavado en "Loading..." y hubo que matarlo.

Arreglado con `if (bar < CurrentBar - 1) return;` antes de rehacer el perfil: el
perfil solo tiene sentido para el momento actual, asi que se calcula una sola
vez, cuando el recorrido llega al final.

**Sintoma si vuelve:** "Loading..." eterno al agregarlo, en el grafico de menor
temporalidad.

**2. Mover un ajuste no hacia nada con el mercado cerrado.**

Sin ticks entrando, `OnCalculate` **no se ejecuta**. Cambiar "fuerza minima" no
recalculaba nada y la caja seguia mostrando el umbral viejo: parecia que el
ajuste estaba roto. Arreglado recalculando tambien en `OnRender` cuando cambia
la firma de los ajustes -- el render corre siempre.

**Sintoma si vuelve:** se cambia un numero, se aprieta Apply y la caja sigue
mostrando el valor anterior.

## Como se ve funcionando, medido

Con los mismos ajustes en los dos instrumentos, el 2026-09-04 con el mercado
cerrado:

- **MES 5 minutos:** precio tipico 4,1K, umbral 10,3K, 13 precios pasaron, 3
  nodos dibujados: 7.721,25 (63,5K, delta +850), 7.720,25 (48,3K, delta -2,3K)
  y 7.718,00 (43,8K, delta +323).
- **MNQ 1 minuto:** precio tipico 1,1K, umbral 2,7K, ninguno paso, 1 nodo
  dibujado en punteado: 29.537,50 (7K, delta -557).

Los umbrales salieron casi cuatro veces mas chicos en MNQ **sin tocar un solo
ajuste**. Eso es la calibracion relativa haciendo su trabajo.

El nodo del medio en MES es el ejemplo de para que sirve el delta: 48 mil
contratos con delta **negativo**, o sea que ese precio se armo con vendedores
agresivos. El de arriba, con delta positivo. El indicador original de la
captura no distingue eso.
