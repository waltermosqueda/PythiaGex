using System;
using PythiaGex;

// PRUEBAS DE BLACK-76.
//
// La gamma de este modelo alimenta TODOS los niveles del indicador. Si esta
// mal, el indicador dibuja numeros equivocados con total prolijidad, que es
// peor que no dibujar nada.
//
// SOBRE LOS UMBRALES. La primera version de este arnes marcaba tres fallas
// que no eran del codigo sino suyas, y se diagnostico antes de tocar nada:
//
//  - Exigia que la gamma cerrada coincidiera al 0,2 % con una diferencia
//    finita. Barriendo h el error dibuja una U -- 4,5 % con h chico, minimo
//    0,28 % en h=7,75, 56 % con h grande -- que es la firma del error
//    numerico, no de una formula mal escrita. El piso de ruido de la
//    diferencia finita es ~0,5 %, asi que pedirle 0,2 % era imposible.
//    Ademas la gamma cerrada NO usa la normal acumulada, solo la densidad,
//    que es un exponencial exacto: sale a precision de maquina.
//
//  - Contaba como falla que el despeje devolviera NaN en 8 casos. Los ocho
//    son opciones muy dentro del dinero a medio dia de vencer donde el precio
//    es EXACTAMENTE el intrinseco (exceso 0,0). No les queda valor temporal:
//    no hay IV que despejar y NaN es la respuesta correcta.
//
//  - Medía la vuelta sobre la IV en vez de sobre el precio. Donde el vega es
//    casi cero la IV no es identificable y muchas dan el mismo precio hasta
//    el ultimo bit. Medido sobre el precio la vuelta da 7e-12.

static class P
{
    static int fallas = 0;

    static void Ok(string nombre, double a, double b, double tol)
    {
        double d = Math.Abs(a - b);
        bool ok = d <= tol;
        if (!ok) fallas++;
        Console.WriteLine("{0,-46} {1,14:F8} {2,14:F8}  {3} ({4:E2})",
            nombre, a, b, ok ? "ok" : "FALLA", d);
    }

    static int Main()
    {
        Console.WriteLine(new string('=', 96));
        Console.WriteLine("PRUEBAS DE BLACK-76   (compila el mismo archivo que usa el indicador)");
        Console.WriteLine(new string('=', 96));
        Console.WriteLine("{0,-46} {1,14} {2,14}", "", "calculado", "esperado");

        Console.WriteLine();
        Console.WriteLine("-- 1. normal acumulada (Abramowitz-Stegun, error < 7,5e-8) --");
        Ok("N(0)", Black76.N(0), 0.5, 1e-7);
        Ok("N(1,96)", Black76.N(1.96), 0.9750021049, 1e-7);
        Ok("N(-1,96)", Black76.N(-1.96), 0.0249978951, 1e-7);
        Ok("N(1) - N(-1)  (una sigma)", Black76.N(1) - Black76.N(-1), 0.6826894921, 1e-7);
        Ok("simetria N(x) + N(-x) = 1", Black76.N(0.7) + Black76.N(-0.7), 1.0, 1e-12);

        // La prueba mas fuerte que hay sin tablas externas: se cumple por
        // arbitraje, para cualquier volatilidad.
        Console.WriteLine();
        Console.WriteLine("-- 2. paridad put-call:  C - P = e^(-rT)(F - K) --");
        foreach (var (F, K, T, s) in new[]
        {
            (7750.0, 7750.0, 1.0/365, 0.12),
            (7750.0, 7700.0, 5.0/365, 0.15),
            (7750.0, 7900.0, 30.0/365, 0.22),
            (100.0,  90.0,   1.0,      0.35),
        })
            Ok(string.Format("F={0} K={1} T={2:F4}", F, K, T),
               Black76.Precio(F,K,T,s,true) - Black76.Precio(F,K,T,s,false),
               Math.Exp(-Black76.R*T)*(F-K), 1e-6);

        Console.WriteLine();
        Console.WriteLine("-- 3. gamma contra diferencia finita (tol 1 %: su piso de ruido) --");
        foreach (var (F, K, T, s) in new[]
        {
            (7750.0, 7750.0, 1.0/365, 0.12),
            (7750.0, 7800.0, 7.0/365, 0.18),
            (7750.0, 7600.0, 30.0/365, 0.25),
        })
        {
            double h = F * 1e-3;   // el h que minimiza el error, medido
            double num = (Black76.Precio(F+h,K,T,s,true) - 2*Black76.Precio(F,K,T,s,true)
                        + Black76.Precio(F-h,K,T,s,true)) / (h*h);
            Ok(string.Format("gamma call F={0} K={1} T={2:F4}", F, K, T),
               Black76.Gamma(F,K,T,s), num, Math.Abs(num)*0.01);
            double nump = (Black76.Precio(F+h,K,T,s,false) - 2*Black76.Precio(F,K,T,s,false)
                         + Black76.Precio(F-h,K,T,s,false)) / (h*h);
            Ok("   y la del put es la misma", nump, num, Math.Abs(num)*1e-6);
        }

        Console.WriteLine();
        Console.WriteLine("-- 4. despeje de la IV: se mide sobre el PRECIO recuperado --");
        double peorPx=0, peorIvUtil=0; int tot=0, sinTiempo=0;
        for (double T = 0.5/365; T <= 30.0/365; T += 1.0/365)
        for (double K = 7500; K <= 8000; K += 25)
        foreach (double s in new[] { 0.08, 0.15, 0.30, 0.60 })
        foreach (bool call in new[] { true, false })
        {
            double px = Black76.Precio(7750, K, T, s, call);
            if (px < 0.05) continue;                       // por debajo del tick
            double intr = Math.Exp(-Black76.R*T)*(call ? Math.Max(0,7750-K) : Math.Max(0,K-7750));
            if (px - intr <= 0) { sinTiempo++; continue; } // sin valor temporal: no hay IV
            tot++;
            double iv = Black76.DespejarIV(px, 7750, K, T, call);
            if (double.IsNaN(iv)) { fallas++; Console.WriteLine("   sin solucion con valor temporal: K={0} T={1:F5}", K, T); continue; }
            peorPx = Math.Max(peorPx, Math.Abs(Black76.Precio(7750,K,T,iv,call) - px));
            if (Black76.Precio(7750,K,T,s+0.01,call) - px > 0.01)
                peorIvUtil = Math.Max(peorIvUtil, Math.Abs(iv-s));
        }
        Console.WriteLine("   {0} casos con valor temporal ({1} sin el, correctamente salteados)", tot, sinTiempo);
        Console.WriteLine("   peor error de PRECIO al volver ......... {0:E3}", peorPx);
        Console.WriteLine("   peor error de IV donde el vega importa . {0:E3}", peorIvUtil);
        if (peorPx > 1e-8)     { fallas++; Console.WriteLine("   FALLA: el precio no se recupera"); }
        if (peorIvUtil > 1e-8) { fallas++; Console.WriteLine("   FALLA: la IV no vuelve donde si es identificable"); }

        Console.WriteLine();
        Console.WriteLine("-- 5. cordura --");
        double bajo = Black76.DespejarIV(0.5, 7750, 7000, 1.0/365, true);
        Console.WriteLine("   call muy dentro del dinero a precio absurdo -> {0}",
            double.IsNaN(bajo) ? "NaN, correcto" : "DEVOLVIO " + bajo + " -- FALLA");
        if (!double.IsNaN(bajo)) fallas++;
        Ok("gamma con T=0 no explota", Black76.Gamma(7750,7750,0,0.12), 0.0, 1e-12);
        Ok("gamma con sigma=0 no explota", Black76.Gamma(7750,7750,0.01,0), 0.0, 1e-12);

        Console.WriteLine();
        Console.WriteLine(fallas == 0
            ? "PASA: las cinco pruebas dan bien."
            : "FALLA en " + fallas + " control(es). NO usar estos numeros.");
        return fallas == 0 ? 0 : 1;
    }
}
