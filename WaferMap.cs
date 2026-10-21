using System;
using System.Collections.Generic;
using System.Globalization;

namespace StdfViewer
{
    public sealed class WaferDie
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int HardBin { get; set; }
        public int SiteNum { get; set; }
        public int Retests { get; set; }
    }

    public sealed class WaferMapWafer
    {
        private readonly Dictionary<long, WaferDie> _byCoord = new Dictionary<long, WaferDie>();

        public int Number { get; set; }
        public string WaferId { get; set; } = "";
        public char Flat { get; set; } = ' ';
        public long PartCount { get; set; }
        public long GoodCount { get; set; }
        public int RetestCount { get; set; }
        public int MinX { get; private set; }
        public int MaxX { get; private set; }
        public int MinY { get; private set; }
        public int MaxY { get; private set; }
        public List<WaferDie> Dice { get; } = new List<WaferDie>();
        public Dictionary<int, int> BinCounts { get; } = new Dictionary<int, int>();

        public string Title
        {
            get
            {
                string title = "#" + Number.ToString(CultureInfo.InvariantCulture);
                return WaferId.Length == 0 ? title : title + "  " + WaferId;
            }
        }

        public void AddDie(int x, int y, int hardBin, int siteNum)
        {
            long key = ((long)x << 32) | (uint)y;
            WaferDie die;
            if (_byCoord.TryGetValue(key, out die))
            {
                die.HardBin = hardBin;
                die.SiteNum = siteNum;
                die.Retests++;
                RetestCount++;
                return;
            }
            die = new WaferDie { X = x, Y = y, HardBin = hardBin, SiteNum = siteNum };
            _byCoord.Add(key, die);
            Dice.Add(die);
        }

        public WaferDie GetDie(int x, int y)
        {
            WaferDie die;
            return _byCoord.TryGetValue(((long)x << 32) | (uint)y, out die) ? die : null;
        }

        public void Finish()
        {
            if (Dice.Count == 0)
                return;
            MinX = MaxX = Dice[0].X;
            MinY = MaxY = Dice[0].Y;
            foreach (WaferDie die in Dice)
            {
                if (die.X < MinX) MinX = die.X;
                if (die.X > MaxX) MaxX = die.X;
                if (die.Y < MinY) MinY = die.Y;
                if (die.Y > MaxY) MaxY = die.Y;
            }
            BinCounts.Clear();
            foreach (WaferDie die in Dice)
            {
                int count;
                BinCounts.TryGetValue(die.HardBin, out count);
                BinCounts[die.HardBin] = count + 1;
            }
        }
    }

    public sealed class WaferMap
    {
        public List<WaferMapWafer> Wafers { get; } = new List<WaferMapWafer>();

        public static WaferMap Build(StdfFile file)
        {
            var map = new WaferMap();
            WaferMapWafer current = null;
            WaferMapWafer pending = null;

            for (int seq = 0; seq < file.Count; seq++)
            {
                byte typ = file.TypOf(seq);
                byte sub = file.SubOf(seq);

                if (typ == 2 && sub == 10)
                {
                    current = new WaferMapWafer { Number = map.Wafers.Count + 1 };
                    if (pending != null)
                    {
                        CopyConfig(pending, current);
                        pending = null;
                    }
                    DecodedRecord rec = StdfDecoder.Decode(file, seq);
                    current.WaferId = Text(Field(rec, "WAFER_ID"));
                    map.Wafers.Add(current);
                }
                else if (typ == 2 && sub == 30)
                {
                    DecodedRecord rec = StdfDecoder.Decode(file, seq);
                    if (current == null)
                    {
                        pending = new WaferMapWafer();
                        ApplyConfig(pending, rec);
                    }
                    else
                    {
                        ApplyConfig(current, rec);
                    }
                }
                else if (typ == 2 && sub == 20)
                {
                    if (current != null)
                    {
                        DecodedRecord rec = StdfDecoder.Decode(file, seq);
                        current.PartCount = Number(Field(rec, "PART_CNT"));
                        current.GoodCount = Number(Field(rec, "GOOD_CNT"));
                        if (current.WaferId.Length == 0)
                            current.WaferId = Text(Field(rec, "WAFER_ID"));
                    }
                }
                else if (typ == 5 && sub == 20)
                {
                    if (current == null)
                    {
                        current = new WaferMapWafer { Number = map.Wafers.Count + 1 };
                        if (pending != null)
                        {
                            CopyConfig(pending, current);
                            pending = null;
                        }
                        map.Wafers.Add(current);
                    }
                    byte[] body = file.ReadBody(seq);
                    AddPrr(current, body, file.BigEndian);
                }
            }

            for (int i = map.Wafers.Count - 1; i >= 0; i--)
            {
                if (map.Wafers[i].Dice.Count == 0)
                    map.Wafers.RemoveAt(i);
            }
            for (int i = 0; i < map.Wafers.Count; i++)
            {
                map.Wafers[i].Number = i + 1;
                map.Wafers[i].Finish();
            }
            return map;
        }

        private static void AddPrr(WaferMapWafer wafer, byte[] body, bool bigEndian)
        {
            // V4 PRR 固定前缀 17 字节：HARD_BIN@5，X_COORD@9，Y_COORD@11；同坐标重测以后一条为准
            if (body.Length < 13)
                return;
            int hardBin = U2(body, 5, bigEndian);
            int x = unchecked((short)U2(body, 9, bigEndian));
            int y = unchecked((short)U2(body, 11, bigEndian));
            wafer.AddDie(x, y, hardBin, body[1]);
        }

        private static int U2(byte[] body, int offset, bool bigEndian)
        {
            return bigEndian
                ? (body[offset] << 8) | body[offset + 1]
                : body[offset] | (body[offset + 1] << 8);
        }

        private static void ApplyConfig(WaferMapWafer wafer, DecodedRecord rec)
        {
            string id = Text(Field(rec, "WAFER_ID"));
            if (wafer.WaferId.Length == 0)
                wafer.WaferId = id;

            char flat = FlatChar(Field(rec, "WF_FLAT"));
            if (flat != ' ')
                wafer.Flat = flat;
        }

        private static void CopyConfig(WaferMapWafer from, WaferMapWafer to)
        {
            to.WaferId = from.WaferId;
            to.Flat = from.Flat;
        }

        private static char FlatChar(string value)
        {
            foreach (char c in value)
            {
                char upper = char.ToUpperInvariant(c);
                if (upper == 'U' || upper == 'D' || upper == 'L' || upper == 'R')
                    return upper;
            }
            return ' ';
        }

        private static string Field(DecodedRecord rec, string name)
        {
            foreach (FieldRow row in rec.Rows)
            {
                if (row.Name == name)
                    return row.Value ?? "";
            }
            return "";
        }

        private static string Text(string value)
        {
            return value == StdfDecoder.EmptyToken ? "" : value;
        }

        private static long Number(string value)
        {
            long number;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : 0;
        }
    }
}
