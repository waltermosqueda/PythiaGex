// ExtrasCompuestos.cs — PythiaGex 4.1.5 (09-10-2026), modulo fam. Varias fuentes de series extra para el motor (OpcionesMotorFamilia.Extras), en
// orden: el indicador pasa (Replica20, ClasicaNdx). La PRIMERA puede reemplazar r.Extra entero (la Replica20 lo hace al guardar); las siguientes
// tienen que AGREGAR su serie sin tocar las otras (ClasicaNdx.Guardar copia lo que hay). Una que falla no tira abajo a las demas (se loguea).
// IExtrasRehacer: Generacion sube si sube la de cualquier hijo; Rehacer llama SOLO a los hijos cuya generacion cambio desde la ultima lectura (asi
// un cambio en la base de la clasica no rehace la razon de la 2.0 ni al reves). IExtrasParciales: un minuto del archivo sin ninguna extra se
// completa con todos los hijos (como antes); uno que ya trae algunas, solo con los hijos que dicen que les falta la suya.
// Sin referencias a ATAS ni a los hijos concretos (solo las interfaces del motor).
using System;
using System.Collections.Generic;

namespace PythiaGexCuatro.Familia
{
    public sealed class ExtrasCompuestos : IExtrasMinuto, IExtrasRehacer, IExtrasParciales
    {
        private readonly IExtrasMinuto[] _h;
        private readonly long[] _ack;
        private readonly bool[] _pend;
        private long _gen;
        private DateTime _desde = DateTime.MaxValue;
        /// <summary>Donde loguear la falla de un hijo (nombre del hijo y lugar, excepcion). null = se traga.</summary>
        public Action<string, Exception> Error;
        public IReadOnlyList<IExtrasMinuto> Hijos => _h;

        public ExtrasCompuestos(params IExtrasMinuto[] hijos)
        {
            var l = new List<IExtrasMinuto>();
            if (hijos != null) foreach (var h in hijos) if (h != null) l.Add(h);
            _h = l.ToArray();
            _ack = new long[_h.Length]; _pend = new bool[_h.Length];
            for (int i = 0; i < _h.Length; i++) if (_h[i] is IExtrasRehacer rh) try { _ack[i] = rh.Generacion; } catch { }
        }

        private void Fallo(int i, string donde, Exception e) { try { Error?.Invoke(_h[i].GetType().Name + "." + donde, e); } catch { } }

        public void Calcular(RegistroMinuto r, DateTime tUtc, double fut, IReadOnlyList<LibroMinuto> bk)
        {
            for (int i = 0; i < _h.Length; i++)
                try { _h[i].Calcular(r, tUtc, fut, bk); } catch (Exception e) { Fallo(i, "Calcular", e); }
        }

        public bool Completar(RegistroMinuto r, DateTime tUtc)
        {
            if (r == null) return false;
            bool sinNada = !r.TieneExtra, algo = false;
            for (int i = 0; i < _h.Length; i++)
            {
                bool falta;
                try { falta = sinNada || (_h[i] is IExtrasParciales p && p.Falta(r)); } catch (Exception e) { Fallo(i, "Falta", e); continue; }
                if (!falta) continue;
                try { algo |= _h[i].Completar(r, tUtc); } catch (Exception e) { Fallo(i, "Completar", e); }
            }
            return algo;
        }

        public bool Falta(RegistroMinuto r)
        {
            if (r == null) return false;
            if (!r.TieneExtra) return true;
            for (int i = 0; i < _h.Length; i++)
                try { if (_h[i] is IExtrasParciales p && p.Falta(r)) return true; } catch (Exception e) { Fallo(i, "Falta", e); }
            return false;
        }

        /// <summary>Lee la generacion de cada hijo: los que cambiaron quedan "pendientes" (Rehacer los llama) hasta la proxima lectura.</summary>
        public long Generacion
        {
            get
            {
                bool cambio = false; DateTime desde = DateTime.MaxValue;
                for (int i = 0; i < _h.Length; i++)
                {
                    _pend[i] = false;
                    if (!(_h[i] is IExtrasRehacer rh)) continue;
                    long g;
                    try { g = rh.Generacion; } catch (Exception e) { Fallo(i, "Generacion", e); continue; }
                    if (g == _ack[i]) continue;
                    _ack[i] = g; _pend[i] = true; cambio = true;
                    try { var d = rh.RehacerDesdeUtc; if (d < desde) desde = d; } catch (Exception e) { Fallo(i, "RehacerDesdeUtc", e); desde = DateTime.MinValue; }
                }
                if (cambio) { _gen++; _desde = desde; }
                return _gen;
            }
        }

        public DateTime RehacerDesdeUtc => _desde;

        public bool Rehacer(RegistroMinuto r, DateTime tUtc)
        {
            bool c = false;
            for (int i = 0; i < _h.Length; i++)
                if (_pend[i] && _h[i] is IExtrasRehacer rh)
                    try { c |= rh.Rehacer(r, tUtc); } catch (Exception e) { Fallo(i, "Rehacer", e); }
            return c;
        }
    }
}
