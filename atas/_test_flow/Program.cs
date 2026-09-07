using System;
using System.Collections.Generic;
using System.Globalization;
using PythiaVwap;

// PRUEBAS DE LA REGLA DE ABSORCION.
//
// Lo primero que tiene que demostrar este arnes es el DEFECTO de la regla
// vieja: que una vela pareja dispara. Si esa prueba no falla con la regla
// vieja, el diagnostico estaba mal y no hay que cambiar nada.
//
// Despues prueba que la regla nueva:
//   - NO dispara con una vela pareja (ni con 4 niveles ni con 12)
//   - SI dispara con volumen apelmazado en el extremo, agresion en contra y
//     cierre del otro lado
//   - NO dispara con rango chico, vela flaca, agresion a favor o cierre del
//     mismo lado
//   - es simetrica entre piso y techo

static class P
{
    static int fallas = 0;
    static readonly CultureInfo C = CultureInfo.InvariantCulture;

    static void Ok(string nombre, bool esperado, bool dado, string detalle = "")
    {
        bool ok = esperado == dado;
        if (!ok) fallas++;
        Console.WriteLine("{0,-58} esperado={1,-5} dado={2,-5} {3}  {4}",
            nombre, esperado, dado, ok ? "ok" : "FALLA", detalle);
    }

    // vela de N niveles desde `low` de a `tick`, con volumen y delta por nivel
    static List<NivelPV> Vela(decimal low, decimal tick, decimal[] vols, decimal[] deltas)
    {
        var l = new List<NivelPV>();
        for (int i = 0; i < vols.Length; i++)
            l.Add(new NivelPV { Precio = low + tick * i, Volumen = vols[i], Delta = deltas[i] });
        return l;
    }

    static int Main()
    {
        const decimal tick = 0.25m;
        const int ticksExt = 2, rangoMin = 6;
        const double fuerza = 3.0, cierreMin = 0.6;

        // ---------- 1. EL DEFECTO DE LA REGLA VIEJA ----------
        // vela pareja de 8 niveles (rango 7 ticks), 100 contratos por nivel,
        // vendedores pegando en los tres de abajo, cierre en el 80 % del rango
        var pareja = Vela(7700m, tick,
            new decimal[] { 100, 100, 100, 100, 100, 100, 100, 100 },
            new decimal[] { -30, -30, -30, 10, 10, 10, 10, 10 });
        decimal low = 7700m, high = 7700m + tick * 7, closeArriba = low + (high - low) * 0.8m;

        bool vieja = AbsorcionRegla.ReglaVieja(pareja, low, high, closeArriba, true, tick, ticksExt, fuerza, cierreMin);
        Ok("REGLA VIEJA dispara con vela PAREJA (el defecto)", true, vieja,
           "3 niveles x promedio = exactamente el umbral 3");

        var r = AbsorcionRegla.Evaluar(pareja, low, high, closeArriba, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 500m);
        Ok("regla nueva NO dispara con vela pareja", false, r.Dispara, r.Linea(C));
        Ok("  concentracion de la vela pareja es 1,0", true, Math.Abs(r.Concentracion - 1.0) < 1e-9,
           r.Concentracion.ToString("0.000", C));

        // pareja de 12 niveles: tampoco
        var pareja12 = Vela(7700m, tick, Rep(12, 80), Rep(12, -5));
        r = AbsorcionRegla.Evaluar(pareja12, 7700m, 7700m + tick * 11, 7700m + tick * 10, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 500m);
        Ok("regla nueva NO dispara con vela pareja de 12 niveles", false, r.Dispara, r.Linea(C));

        // ---------- 2. ABSORCION DE VERDAD EN EL PISO ----------
        // 900 contratos pegados en los 3 niveles del piso, 60 por nivel en el
        // resto, vendedores agresivos abajo, cierre en el 75 %
        var apelmazada = Vela(7700m, tick,
            new decimal[] { 400, 300, 200, 60, 60, 60, 60, 60 },
            new decimal[] { -150, -90, -40, 5, 5, 5, 5, 5 });
        high = 7700m + tick * 7; closeArriba = low + (high - low) * 0.75m;
        r = AbsorcionRegla.Evaluar(apelmazada, low, high, closeArriba, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 500m);
        Ok("absorcion real en el PISO dispara", true, r.Dispara, r.Linea(C));
        Ok("  concentracion = (900/3)/(300/5) = 5,0", true, Math.Abs(r.Concentracion - 5.0) < 1e-9,
           r.Concentracion.ToString("0.000", C));

        // ---------- 3. LO QUE NO TIENE QUE DISPARAR ----------
        // rango chico (4 ticks < 6)
        var chica = Vela(7700m, tick, new decimal[] { 400, 300, 200, 60, 60 }, new decimal[] { -150, -90, -40, 5, 5 });
        r = AbsorcionRegla.Evaluar(chica, 7700m, 7701m, 7700.75m, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 500m);
        Ok("rango chico NO dispara", false, r.Dispara, r.Motivo);

        // vela flaca: 1200 contratos contra mediana reciente 5000
        r = AbsorcionRegla.Evaluar(apelmazada, low, high, closeArriba, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 5000m);
        Ok("vela flaca (bajo la mediana reciente) NO dispara", false, r.Dispara, r.Motivo);

        // compradores en el piso (delta positivo abajo): no es absorcion de venta
        var compranAbajo = Vela(7700m, tick,
            new decimal[] { 400, 300, 200, 60, 60, 60, 60, 60 },
            new decimal[] { 150, 90, 40, 5, 5, 5, 5, 5 });
        r = AbsorcionRegla.Evaluar(compranAbajo, low, high, closeArriba, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 500m);
        Ok("agresion A FAVOR del piso NO dispara", false, r.Dispara, r.Motivo);

        // cierre abajo (no recupero): momentum, no absorcion
        r = AbsorcionRegla.Evaluar(apelmazada, low, high, low + (high - low) * 0.2m, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 500m);
        Ok("cierre en el mismo lado del extremo NO dispara", false, r.Dispara, r.Motivo);

        // ---------- 4. SIMETRIA: EL TECHO ----------
        var techo = Vela(7700m, tick,
            new decimal[] { 60, 60, 60, 60, 60, 200, 300, 400 },
            new decimal[] { -5, -5, -5, -5, -5, 40, 90, 150 });
        decimal closeAbajo = low + (high - low) * 0.25m;
        r = AbsorcionRegla.Evaluar(techo, low, high, closeAbajo, false, tick, ticksExt, rangoMin, fuerza, cierreMin, 500m);
        Ok("absorcion real en el TECHO dispara", true, r.Dispara, r.Linea(C));
        r = AbsorcionRegla.Evaluar(techo, low, high, closeAbajo, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 500m);
        Ok("la misma vela evaluada como PISO no dispara", false, r.Dispara, r.Motivo);

        // ---------- 5. CASO REAL DEL DOMINGO: velas de 2-3 ticks ----------
        // MES 1 minuto en Asia: rango 3 ticks, 4 niveles, ~15 contratos
        var asia = Vela(7721m, tick, new decimal[] { 20, 15, 18, 12 }, new decimal[] { -8, -3, 4, 2 });
        bool viejaAsia = AbsorcionRegla.ReglaVieja(asia, 7721m, 7721.75m, 7721.5m, true, tick, ticksExt, fuerza, cierreMin);
        r = AbsorcionRegla.Evaluar(asia, 7721m, 7721.75m, 7721.5m, true, tick, ticksExt, rangoMin, fuerza, cierreMin, 65m);
        Ok("vela de Asia (3 ticks): la vieja disparaba", true, viejaAsia, "asi salieron 13 flechas en 84 velas");
        Ok("vela de Asia (3 ticks): la nueva NO dispara", false, r.Dispara, r.Motivo);

        Console.WriteLine();
        Console.WriteLine(fallas == 0 ? "TODO OK" : fallas + " FALLA(S)");
        return fallas == 0 ? 0 : 1;
    }

    static decimal[] Rep(int n, decimal v) { var a = new decimal[n]; for (int i = 0; i < n; i++) a[i] = v; return a; }
}
