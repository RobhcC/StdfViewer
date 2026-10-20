using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StdfViewer
{
    public sealed class FieldRow
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Hex { get; set; }
        public string Status { get; set; }

        // 字段名 / 状态是稳定 token（代码里要比较），只在显示时翻译；值里只有空串占位需要翻译
        public string NameText
        {
            get
            {
                if (Name == StdfDecoder.NoLayoutToken)
                    return Loc.T("Str.Dec.NoLayout");
                if (Name == StdfDecoder.UnclaimedToken)
                    return Loc.T("Str.Dec.Unclaimed");
                return Name;
            }
        }

        public string ValueText
        {
            get { return Value == StdfDecoder.EmptyToken ? Loc.T("Str.Dec.Empty") : Value; }
        }

        public string StatusText
        {
            get { return Loc.Status(Status); }
        }
    }

    public sealed class DecodedRecord
    {
        public int Seq;
        public long Offset;
        public string RecordName;
        public string TypSub;
        public int DeclaredLen;
        public int Consumed;
        public string Status = "ok";
        public readonly List<FieldRow> Rows = new List<FieldRow>();
    }

    public static class StdfDecoder
    {
        private const int HexLimit = 48;

        public const string EmptyToken = "(empty)";
        public const string NoLayoutToken = "(no layout)";
        public const string UnclaimedToken = "(unclaimed)";

        public static DecodedRecord Decode(StdfFile file, int seq)
        {
            byte typ = file.TypOf(seq);
            byte sub = file.SubOf(seq);
            var rec = new DecodedRecord
            {
                Seq = seq,
                Offset = file.OffsetOf(seq),
                DeclaredLen = file.LengthOf(seq),
                RecordName = StdfRecordTable.DisplayName(typ, sub),
                TypSub = typ + "," + sub,
            };
            byte[] body = file.ReadBody(seq);

            byte[] head = file.BigEndian
                ? new byte[] { (byte)(rec.DeclaredLen >> 8), (byte)(rec.DeclaredLen & 0xFF), typ, sub }
                : new byte[] { (byte)(rec.DeclaredLen & 0xFF), (byte)(rec.DeclaredLen >> 8), typ, sub };
            rec.Rows.Add(new FieldRow { Index = 0, Name = "REC_LEN", Type = "U*2", Value = rec.DeclaredLen.ToString(CultureInfo.InvariantCulture), Hex = Hex(head, 0, 2), Status = "header" });
            rec.Rows.Add(new FieldRow { Index = 1, Name = "REC_TYP", Type = "U*1", Value = typ.ToString(CultureInfo.InvariantCulture), Hex = Hex(head, 2, 1), Status = "header" });
            rec.Rows.Add(new FieldRow { Index = 2, Name = "REC_SUB", Type = "U*1", Value = sub.ToString(CultureInfo.InvariantCulture), Hex = Hex(head, 3, 1), Status = "header" });

            StdfRecordDef def = StdfRecordTable.Find(typ, sub);
            if (def == null)
            {
                rec.Rows.Add(new FieldRow { Index = 3, Name = NoLayoutToken, Type = "", Value = body.Length + " bytes", Hex = Hex(body, 0, body.Length), Status = "unknown type" });
                rec.Status = Loc.T("Str.Dec.UnknownRecord", rec.TypSub);
                rec.Consumed = body.Length;
                return rec;
            }

            var cur = new Cursor(body, file.BigEndian);
            var counts = new Dictionary<string, long>();
            int omitted = 0;
            foreach (StdfField field in def.Fields)
            {
                int index = rec.Rows.Count;
                if (cur.Pos >= body.Length)
                {
                    rec.Rows.Add(new FieldRow { Index = index, Name = field.Name, Type = field.TypeText, Value = "", Hex = "", Status = "omitted" });
                    omitted++;
                    continue;
                }
                int start = cur.Pos;
                string value;
                string status = cur.Read(field, counts, out value);
                rec.Rows.Add(new FieldRow { Index = index, Name = field.Name, Type = field.TypeText, Value = value, Hex = Hex(body, start, cur.Pos - start), Status = status });
                if (status == "truncated")
                    break;
            }

            rec.Consumed = cur.Pos;
            if (cur.Pos < body.Length)
            {
                rec.Rows.Add(new FieldRow
                {
                    Index = rec.Rows.Count,
                    Name = UnclaimedToken,
                    Type = "",
                    Value = (body.Length - cur.Pos) + " bytes",
                    Hex = Hex(body, cur.Pos, body.Length - cur.Pos),
                    Status = "layout gap",
                });
                rec.Status = Loc.T("Str.Dec.Gap", body.Length - cur.Pos);
            }
            else if (omitted > 0)
            {
                rec.Status = Loc.T("Str.Dec.OptionalOmitted", omitted);
            }
            return rec;
        }

        private sealed class Cursor
        {
            private readonly byte[] _b;
            private readonly bool _be;
            private readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
            private int _pos;
            private long _last;

            public Cursor(byte[] b, bool bigEndian)
            {
                _b = b;
                _be = bigEndian;
            }

            public int Pos { get { return _pos; } }
            private int Left { get { return _b.Length - _pos; } }

            public string Read(StdfField f, Dictionary<string, long> counts, out string value)
            {
                if (f.CountField != null && f.Type != StdfType.Vn)
                    return ReadArray(f, counts, out value);
                if (f.Type == StdfType.Vn)
                    return ReadVn(CountOf(f, counts), out value);

                int need = StdfField.FixedSize(f.Type);
                if (need > 0 && Left < need)
                {
                    value = "";
                    _pos = _b.Length;
                    return "truncated";
                }

                string status;
                switch (f.Type)
                {
                    case StdfType.U1: _last = _b[_pos]; value = _last.ToString(CultureInfo.InvariantCulture); _pos++; status = ""; break;
                    case StdfType.I1: _last = unchecked((sbyte)_b[_pos]); value = _last.ToString(CultureInfo.InvariantCulture); _pos++; status = ""; break;
                    case StdfType.C1: _last = _b[_pos]; value = ShowChar((char)_b[_pos]); _pos++; status = ""; break;
                    case StdfType.B1:
                        _last = _b[_pos];
                        value = _last + " = " + Convert.ToString(_b[_pos], 2).PadLeft(8, '0');
                        _pos++;
                        status = "";
                        break;
                    case StdfType.U2: value = U2().ToString(CultureInfo.InvariantCulture); status = ""; break;
                    case StdfType.I2: value = unchecked((short)U2()).ToString(CultureInfo.InvariantCulture); status = ""; break;
                    case StdfType.U4: value = U4().ToString(CultureInfo.InvariantCulture); status = ""; break;
                    case StdfType.I4: value = unchecked((int)U4()).ToString(CultureInfo.InvariantCulture); status = ""; break;
                    case StdfType.R4: value = ReadR4().ToString("G9", CultureInfo.InvariantCulture); status = ""; break;
                    case StdfType.R8: value = BitConverter.Int64BitsToDouble((long)U8()).ToString("G17", CultureInfo.InvariantCulture); status = ""; break;
                    case StdfType.N1:
                        byte nb = _b[_pos];
                        value = (nb & 0x0F) + ", " + (nb >> 4);
                        _last = nb;
                        _pos++;
                        status = "";
                        break;
                    case StdfType.Cn: status = ReadCn(out value); break;
                    case StdfType.Bn: status = ReadBn(out value); break;
                    case StdfType.Dn: status = ReadDn(out value); break;
                    default: value = ""; _pos = _b.Length; status = "unsupported C*f"; break;
                }
                if (status == "" && f.IsTime)
                    value = _last + " = " + TimeText(_last);
                if (status == "" && counts != null && f.CountField == null && StdfField.FixedSize(f.Type) > 0)
                    counts[f.Name] = _last;
                return status;
            }

            private static string TimeText(long seconds)
            {
                if (seconds <= 0)
                    return Loc.T("Str.Dec.NotSet");
                return _fmt(seconds);
            }

            private static string _fmt(long seconds)
            {
                const long max = 4102444800L;
                if (seconds > max)
                    return Loc.T("Str.Dec.OutOfRange");
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local).AddSeconds(seconds).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }

            private long CountOf(StdfField f, Dictionary<string, long> counts)
            {
                long k;
                return f.CountField != null && counts.TryGetValue(f.CountField, out k) && k > 0 ? k : 0;
            }

            private string ReadArray(StdfField f, Dictionary<string, long> counts, out string value)
            {
                long k = CountOf(f, counts);
                if (k <= 0)
                {
                    value = "";
                    counts.TryGetValue(f.CountField, out long has);
                    return "k=" + has;
                }

                if (f.Type == StdfType.N1)
                {
                    int bytes = (int)((k + 1) / 2);
                    if (Left < bytes)
                    {
                        value = Nibbles(k);
                        _pos = _b.Length;
                        return "truncated";
                    }
                    value = Nibbles(k);
                    _pos += bytes;
                    return "";
                }

                int size = StdfField.FixedSize(f.Type);
                if (size > 0)
                {
                    long total = k * size;
                    if (total > int.MaxValue || Left < (int)total)
                    {
                        value = "";
                        _pos = _b.Length;
                        return "truncated";
                    }
                    value = Elements(f.Type, k);
                    return "";
                }

                if (f.Type == StdfType.Cn)
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < k; i++)
                    {
                        string v;
                        string st = ReadCn(out v);
                        if (i > 0)
                            sb.Append(", ");
                        sb.Append(v);
                        if (st == "truncated")
                        {
                            value = sb.ToString();
                            return "truncated";
                        }
                    }
                    value = sb.ToString();
                    return "";
                }

                value = "";
                _pos = _b.Length;
                return "unsupported array type";
            }

            private string Elements(StdfType t, long k)
            {
                var sb = new StringBuilder();
                int limit = k > 20 ? 12 : (int)k;
                for (int i = 0; i < limit; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    string v;
                    Read(new StdfField(null, t), null, out v);
                    sb.Append(v);
                }
                if (limit < k)
                {
                    sb.Append(", ...(k=").Append(k.ToString(CultureInfo.InvariantCulture)).Append(')');
                    int size = StdfField.FixedSize(t);
                    _pos += (int)((k - limit) * size);
                }
                return sb.ToString();
            }

            private string Nibbles(long k)
            {
                var sb = new StringBuilder();
                int start = _pos;
                for (int i = 0; i < k && start + i / 2 < _b.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    int b = _b[start + i / 2];
                    sb.Append(((i & 1) == 0 ? (b & 0x0F) : (b >> 4)).ToString(CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }

            private string ReadVn(long k, out string value)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < k; i++)
                {
                    if (Left < 1)
                    {
                        value = sb.ToString();
                        return "truncated";
                    }
                    int code = _b[_pos++];
                    StdfType t;
                    if (!TryVnType(code, out t))
                    {
                        value = sb.Length > 0 ? sb + "; ?" : "?";
                        _pos = _b.Length;
                        return "unknown V*n code " + code;
                    }
                    string v;
                    string st = Read(new StdfField(null, t), null, out v);
                    if (i > 0)
                        sb.Append("; ");
                    sb.Append(StdfField.BaseText(t)).Append('=').Append(v);
                    if (st != "")
                    {
                        value = sb.ToString();
                        return st;
                    }
                }
                value = sb.ToString();
                return "";
            }

            private static bool TryVnType(int code, out StdfType t)
            {
                switch (code)
                {
                    case 1: t = StdfType.U1; return true;
                    case 2: t = StdfType.I1; return true;
                    case 3: t = StdfType.U2; return true;
                    case 4: t = StdfType.I2; return true;
                    case 5: t = StdfType.U4; return true;
                    case 6: t = StdfType.I4; return true;
                    case 7: t = StdfType.R4; return true;
                    case 8: t = StdfType.R8; return true;
                    case 10: t = StdfType.Cn; return true;
                    case 11: t = StdfType.Bn; return true;
                    case 12: t = StdfType.Dn; return true;
                    default: t = StdfType.U1; return false;
                }
            }

            private string ReadCn(out string value)
            {
                if (Left < 1)
                {
                    value = "";
                    _pos = _b.Length;
                    return "truncated";
                }
                int n = _b[_pos];
                if (Left < 1 + n)
                {
                    value = "";
                    _pos = _b.Length;
                    return "truncated";
                }
                _pos++;
                value = n == 0 ? EmptyToken : Encoding.ASCII.GetString(_b, _pos, n);
                _pos += n;
                return "";
            }

            private string ReadBn(out string value)
            {
                if (Left < 1)
                {
                    value = "";
                    _pos = _b.Length;
                    return "truncated";
                }
                int n = _b[_pos];
                if (Left < 1 + n)
                {
                    value = "";
                    _pos = _b.Length;
                    return "truncated";
                }
                _pos++;
                value = n == 0 ? EmptyToken : n + " bytes = " + Hex(_b, _pos, n);
                _pos += n;
                return "";
            }

            private string ReadDn(out string value)
            {
                if (Left < 2)
                {
                    value = "";
                    _pos = _b.Length;
                    return "truncated";
                }
                int bits = (int)U2();
                int bytes = (bits + 7) / 8;
                if (Left < bytes)
                {
                    value = "";
                    _pos = _b.Length;
                    return "truncated";
                }
                var sb = new StringBuilder();
                sb.Append(bits).Append(" bits = ");
                int shown = Math.Min(bits, 64);
                for (int i = 0; i < shown; i++)
                    sb.Append((_b[_pos + i / 8] >> (i % 8)) & 1);
                if (bits > shown)
                    sb.Append("...");
                value = sb.ToString();
                _pos += bytes;
                return "";
            }

            private uint U2()
            {
                uint v = _be ? (uint)((_b[_pos] << 8) | _b[_pos + 1]) : (uint)(_b[_pos] | (_b[_pos + 1] << 8));
                _pos += 2;
                _last = v;
                return v;
            }

            private uint U4()
            {
                uint v = _be
                    ? (uint)((_b[_pos] << 24) | (_b[_pos + 1] << 16) | (_b[_pos + 2] << 8) | _b[_pos + 3])
                    : (uint)(_b[_pos] | (_b[_pos + 1] << 8) | (_b[_pos + 2] << 16) | _b[_pos + 3] << 24);
                _pos += 4;
                _last = v;
                return v;
            }

            private ulong U8()
            {
                ulong v = 0;
                if (_be)
                    for (int i = 0; i < 8; i++)
                        v = (v << 8) | _b[_pos + i];
                else
                    for (int i = 7; i >= 0; i--)
                        v = (v << 8) | _b[_pos + i];
                _pos += 8;
                _last = (long)v;
                return v;
            }

            private float ReadR4()
            {
                float v;
                if (_be)
                {
                    byte[] c = { _b[_pos + 3], _b[_pos + 2], _b[_pos + 1], _b[_pos] };
                    v = BitConverter.ToSingle(c, 0);
                }
                else
                {
                    v = BitConverter.ToSingle(_b, _pos);
                }
                _pos += 4;
                _last = 0;
                return v;
            }
        }

        private static string ShowChar(char c)
        {
            if (c == ' ')
                return "' ' (0x20)";
            if (char.IsControl(c))
                return "0x" + ((byte)c).ToString("X2");
            return "'" + c + "'";
        }

        private static string Hex(byte[] data, int start, int count)
        {
            if (count <= 0)
                return "";
            var sb = new StringBuilder();
            int end = Math.Min(start + count, data.Length);
            int shown = 0;
            for (int i = start; i < end && shown < HexLimit; i++, shown++)
            {
                if (shown > 0)
                    sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            if (end - start > shown)
                sb.Append(" ...");
            return sb.ToString();
        }
    }
}
