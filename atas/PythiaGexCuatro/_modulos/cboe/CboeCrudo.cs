// CboeCrudo.cs — PythiaGex 4.1, modulo cboe (B2). Sin referencias a ATAS.
// Lee el JSON crudo de CBOE (delayed_quotes/options/<SYM>.json) con Utf8JsonReader, en UNA pasada sobre el buffer, sin armar arbol
// y sin crear un string por contrato (NDX trae ~16.000): el simbolo OCC se decodifica directo de los bytes.
// Semantica de Python que se respeta (pythiagex/cadena_atas.py y base.py):
//   oi  = o.get("open_interest") or 0   -> nulo o 0.0 cuentan como int 0
//   vol = o.get("volume") or 0
//   iv  = o.get("iv") or 0.0
//   bid = o.get("bid") (None si nulo; 0 vale)       ask: "not a" -> sin mid
//   last_trade_price: "v if v else None"
//   ultimo_trade = max(last_trade_time or "") de TODOS los contratos, antes de filtrar
using System;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PythiaGexCuatro.Cboe
{
    /// <summary>Un numero del JSON tal como lo veria Python: presente o nulo, y si era int o float (para escribir igual).</summary>
    public struct NumCboe
    {
        public double V;          // NaN si nulo o ausente
        public bool Hay;          // false = None en Python
        public bool Entero;       // el token no tenia '.', 'e' ni 'E'
        public static readonly NumCboe Nada = new NumCboe { V = double.NaN };
        public override string ToString() => Hay ? PyCboe.Num(V, Entero) : "null";
    }

    /// <summary>La cadena cruda de CBOE, ya leida. Los contratos van en arreglos paralelos (sin un objeto por contrato).</summary>
    public sealed class CrudoCboe
    {
        public string Timestamp;                       // raiz "timestamp" (UTC, "yyyy-MM-dd HH:mm:ss")
        public string Simbolo;                         // raiz "symbol"
        public NumCboe CurrentPrice = NumCboe.Nada, Close = NumCboe.Nada, PrevDayClose = NumCboe.Nada, PriceChange = NumCboe.Nada,
                       Bid = NumCboe.Nada, Ask = NumCboe.Nada, Open = NumCboe.Nada;
        public string LastTradeTime;                   // data.last_trade_time (NY sin zona) o null
        public string UltimoTrade = "";                // max(last_trade_time or "") de todos los contratos

        public int N;                                  // contratos leidos (todos, incluso los que no parsean)
        public int[] Fecha;                            // yyyymmdd del vencimiento; 0 = simbolo que no cumple la regex OCC
        public bool[] EsCall;
        public double[] K;
        public double[] Iv; public bool[] IvEntero;    // ya con "or 0.0"
        public double[] Oi; public bool[] OiEntero;    // ya con "or 0"
        public double[] Vol; public bool[] VolEntero;  // ya con "or 0"
        public double[] BidO, AskO, LastPx;            // NaN = None (bid), ask/last: NaN = None o 0 se tratan en Medir

        public int ContratosValidos { get { int n = 0; for (int i = 0; i < N; i++) if (Fecha[i] != 0) n++; return n; } }

        private void Crecer(int min)
        {
            int cap = Fecha == null ? 0 : Fecha.Length;
            if (cap >= min) return;
            int nc = Math.Max(min, Math.Max(1024, cap * 2));
            Array.Resize(ref Fecha, nc); Array.Resize(ref EsCall, nc); Array.Resize(ref K, nc);
            Array.Resize(ref Iv, nc); Array.Resize(ref IvEntero, nc); Array.Resize(ref Oi, nc); Array.Resize(ref OiEntero, nc);
            Array.Resize(ref Vol, nc); Array.Resize(ref VolEntero, nc); Array.Resize(ref BidO, nc); Array.Resize(ref AskO, nc); Array.Resize(ref LastPx, nc);
        }

        // ------------------------------------------------------------------ lectura

        /// <summary>Lee el JSON crudo (UTF-8). Tira JsonException si no es JSON.</summary>
        public static CrudoCboe Leer(ReadOnlySpan<byte> utf8)
        {
            var c = new CrudoCboe();
            var rd = new Utf8JsonReader(utf8, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, MaxDepth = 64 });
            if (!rd.Read() || rd.TokenType != JsonTokenType.StartObject) throw new JsonException("la raiz no es un objeto");
            byte[] maxUt = null; int maxUtLen = 0;
            while (rd.Read())
            {
                if (rd.TokenType == JsonTokenType.EndObject) break;
                if (rd.TokenType != JsonTokenType.PropertyName) continue;
                if (rd.ValueTextEquals("timestamp")) { rd.Read(); c.Timestamp = rd.TokenType == JsonTokenType.String ? rd.GetString() : null; if (rd.TokenType == JsonTokenType.StartObject || rd.TokenType == JsonTokenType.StartArray) rd.Skip(); }
                else if (rd.ValueTextEquals("symbol")) { rd.Read(); c.Simbolo = rd.TokenType == JsonTokenType.String ? rd.GetString() : null; if (rd.TokenType == JsonTokenType.StartObject || rd.TokenType == JsonTokenType.StartArray) rd.Skip(); }
                else if (rd.ValueTextEquals("data"))
                {
                    rd.Read();
                    if (rd.TokenType == JsonTokenType.StartObject) LeerData(ref rd, c, ref maxUt, ref maxUtLen);
                    else rd.Skip();
                }
                else { rd.Read(); rd.Skip(); }
            }
            c.UltimoTrade = maxUt == null ? "" : Encoding.UTF8.GetString(maxUt, 0, maxUtLen);
            return c;
        }

        private static void LeerData(ref Utf8JsonReader rd, CrudoCboe c, ref byte[] maxUt, ref int maxUtLen)
        {
            while (rd.Read())
            {
                if (rd.TokenType == JsonTokenType.EndObject) return;
                if (rd.TokenType != JsonTokenType.PropertyName) continue;
                if (rd.ValueTextEquals("options"))
                {
                    rd.Read();
                    if (rd.TokenType != JsonTokenType.StartArray) { rd.Skip(); continue; }
                    while (rd.Read() && rd.TokenType != JsonTokenType.EndArray)
                    {
                        if (rd.TokenType == JsonTokenType.StartObject) LeerOpcion(ref rd, c, ref maxUt, ref maxUtLen);
                        else rd.Skip();
                    }
                }
                else if (rd.ValueTextEquals("current_price")) { rd.Read(); c.CurrentPrice = Num(ref rd); }
                else if (rd.ValueTextEquals("close")) { rd.Read(); c.Close = Num(ref rd); }
                else if (rd.ValueTextEquals("prev_day_close")) { rd.Read(); c.PrevDayClose = Num(ref rd); }
                else if (rd.ValueTextEquals("price_change")) { rd.Read(); c.PriceChange = Num(ref rd); }
                else if (rd.ValueTextEquals("bid")) { rd.Read(); c.Bid = Num(ref rd); }
                else if (rd.ValueTextEquals("ask")) { rd.Read(); c.Ask = Num(ref rd); }
                else if (rd.ValueTextEquals("open")) { rd.Read(); c.Open = Num(ref rd); }
                else if (rd.ValueTextEquals("last_trade_time")) { rd.Read(); c.LastTradeTime = rd.TokenType == JsonTokenType.String ? rd.GetString() : null; if (rd.TokenType == JsonTokenType.StartObject || rd.TokenType == JsonTokenType.StartArray) rd.Skip(); }
                else { rd.Read(); rd.Skip(); }
            }
        }

        private static void LeerOpcion(ref Utf8JsonReader rd, CrudoCboe c, ref byte[] maxUt, ref int maxUtLen)
        {
            int i = c.N;
            c.Crecer(i + 1);
            c.N = i + 1;
            c.Fecha[i] = 0; c.EsCall[i] = false; c.K[i] = double.NaN;
            c.Iv[i] = 0.0; c.IvEntero[i] = false; c.Oi[i] = 0; c.OiEntero[i] = true; c.Vol[i] = 0; c.VolEntero[i] = true;
            c.BidO[i] = double.NaN; c.AskO[i] = double.NaN; c.LastPx[i] = double.NaN;
            while (rd.Read())
            {
                if (rd.TokenType == JsonTokenType.EndObject) return;
                if (rd.TokenType != JsonTokenType.PropertyName) continue;
                if (rd.ValueTextEquals("option"))
                {
                    rd.Read();
                    if (rd.TokenType == JsonTokenType.String)
                    {
                        if (rd.ValueIsEscaped) { var s = Encoding.UTF8.GetBytes(rd.GetString() ?? ""); ParseOcc(s, c, i); }
                        else ParseOcc(rd.ValueSpan, c, i);
                    }
                    else if (rd.TokenType == JsonTokenType.StartObject || rd.TokenType == JsonTokenType.StartArray) rd.Skip();
                }
                else if (rd.ValueTextEquals("iv"))
                {
                    rd.Read(); var n = Num(ref rd);
                    if (n.Hay && n.V != 0) { c.Iv[i] = n.V; c.IvEntero[i] = n.Entero; }     // "or 0.0"
                }
                else if (rd.ValueTextEquals("open_interest"))
                {
                    rd.Read(); var n = Num(ref rd);
                    if (n.Hay && n.V != 0) { c.Oi[i] = n.V; c.OiEntero[i] = n.Entero; }     // "or 0"
                }
                else if (rd.ValueTextEquals("volume"))
                {
                    rd.Read(); var n = Num(ref rd);
                    if (n.Hay && n.V != 0) { c.Vol[i] = n.V; c.VolEntero[i] = n.Entero; }
                }
                else if (rd.ValueTextEquals("bid")) { rd.Read(); var n = Num(ref rd); c.BidO[i] = n.Hay ? n.V : double.NaN; }
                else if (rd.ValueTextEquals("ask")) { rd.Read(); var n = Num(ref rd); c.AskO[i] = n.Hay ? n.V : double.NaN; }
                else if (rd.ValueTextEquals("last_trade_price")) { rd.Read(); var n = Num(ref rd); c.LastPx[i] = n.Hay ? n.V : double.NaN; }
                else if (rd.ValueTextEquals("last_trade_time"))
                {
                    rd.Read();
                    if (rd.TokenType == JsonTokenType.String)
                    {
                        ReadOnlySpan<byte> v;
                        byte[] tmp = null;
                        if (rd.ValueIsEscaped) { tmp = Encoding.UTF8.GetBytes(rd.GetString() ?? ""); v = tmp; } else v = rd.ValueSpan;
                        // max lexicografico (Python compara por punto de codigo; ASCII = orden de bytes)
                        if (maxUt == null || v.SequenceCompareTo(new ReadOnlySpan<byte>(maxUt, 0, maxUtLen)) > 0)
                        {
                            if (maxUt == null || maxUt.Length < v.Length) maxUt = new byte[Math.Max(32, v.Length)];
                            v.CopyTo(maxUt); maxUtLen = v.Length;
                        }
                    }
                    else if (rd.TokenType == JsonTokenType.StartObject || rd.TokenType == JsonTokenType.StartArray) rd.Skip();
                }
                else { rd.Read(); rd.Skip(); }
            }
        }

        private static NumCboe Num(ref Utf8JsonReader rd)
        {
            if (rd.TokenType == JsonTokenType.Number)
            {
                // se lee siempre de un span contiguo (Leer recibe ReadOnlySpan): nunca hay ValueSequence; sin asignar memoria por numero
                bool entero = rd.ValueSpan.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0;
                double v = rd.GetDouble();
                return new NumCboe { V = v, Hay = true, Entero = entero };
            }
            if (rd.TokenType == JsonTokenType.StartObject || rd.TokenType == JsonTokenType.StartArray) rd.Skip();
            return NumCboe.Nada;   // null, string, bool: para Python no es un numero usable
        }

        /// <summary>^([A-Z]+)(\d{2})(\d{2})(\d{2})([CP])(\d{8})$ (exposicion.parse_occ). Fecha invalida -> se descarta el contrato
        /// (en Python reventaria construir entero).</summary>
        private static void ParseOcc(ReadOnlySpan<byte> s, CrudoCboe c, int i)
        {
            int n = s.Length;
            if (n < 16) return;
            int raiz = 0;
            while (raiz < n && s[raiz] >= (byte)'A' && s[raiz] <= (byte)'Z') raiz++;
            if (raiz < 1 || n - raiz != 15) return;
            for (int j = 0; j < 6; j++) if (!Dig(s[raiz + j])) return;
            byte cp = s[raiz + 6];
            if (cp != (byte)'C' && cp != (byte)'P') return;
            long k = 0;
            for (int j = 7; j < 15; j++) { if (!Dig(s[raiz + j])) return; k = k * 10 + (s[raiz + j] - (byte)'0'); }
            int yy = (s[raiz] - 48) * 10 + (s[raiz + 1] - 48), mm = (s[raiz + 2] - 48) * 10 + (s[raiz + 3] - 48), dd = (s[raiz + 4] - 48) * 10 + (s[raiz + 5] - 48);
            int anio = 2000 + yy;
            if (mm < 1 || mm > 12 || dd < 1 || dd > DateTime.DaysInMonth(anio, mm)) return;
            c.Fecha[i] = anio * 10000 + mm * 100 + dd;
            c.EsCall[i] = cp == (byte)'C';
            c.K[i] = k / 1000.0;          // int(k) / 1000.0 de Python: division IEEE correctamente redondeada
        }

        private static bool Dig(byte b) => b >= (byte)'0' && b <= (byte)'9';

        // ------------------------------------------------------------------ fechas de los contratos

        public static DateTime FechaDe(int yyyymmdd) => new DateTime(yyyymmdd / 10000, yyyymmdd / 100 % 100, yyyymmdd % 100, 0, 0, 0, DateTimeKind.Utc);
        public static string FechaIso(int yyyymmdd) => (yyyymmdd / 10000).ToString("0000", CultureInfo.InvariantCulture) + "-" + (yyyymmdd / 100 % 100).ToString("00", CultureInfo.InvariantCulture) + "-" + (yyyymmdd % 100).ToString("00", CultureInfo.InvariantCulture);
        /// <summary>Vencimiento a las 16:00 NY de esa fecha, en UTC (exposicion.parse_occ / hora_cierre_utc).</summary>
        public static DateTime VencUtc(int yyyymmdd) => HoraNyCboe.CierreUtc(yyyymmdd / 10000, yyyymmdd / 100 % 100, yyyymmdd % 100);
    }
}
