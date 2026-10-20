using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace StdfViewer
{
    public sealed class SummaryField
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public sealed class SummaryBinRow
    {
        public string HeadNum { get; set; }
        public string SiteNum { get; set; }
        public string BinNum { get; set; }
        public string BinCount { get; set; }
        public string BinPf { get; set; }
        public string BinName { get; set; }
    }

    public sealed class SummarySection
    {
        public string Title { get; set; }

        public List<SummaryField> Fields { get; } = new List<SummaryField>();

        public List<SummaryBinRow> Bins { get; } = new List<SummaryBinRow>();

        public bool HasBins
        {
            get { return Bins.Count > 0; }
        }
    }

    public sealed class SummaryResult
    {
        public long FileLength;
        public int RecordCount;
        public int TypeCount;
        public long DutCount;
        public double YieldPercent = -1;
        public double ScanMs;
        public bool BigEndian;
        public List<SummarySection> Sections = new List<SummarySection>();
    }

    // 汇总页只做"读现成记录 + 展平"：解码复用 StdfDecoder，不再实现第二套字段解析
    public static class StdfSummary
    {
        // 统计区固定展示这些记录类型，顺序即显示顺序
        private static readonly int[] CountedRecords =
        {
            Key(5, 10), Key(5, 20), Key(15, 10), Key(15, 15), Key(15, 20), Key(10, 30),
            Key(50, 10), Key(50, 30), Key(1, 30), Key(1, 40), Key(1, 50),
            Key(2, 10), Key(2, 20), Key(2, 30),
        };

        public static SummaryResult Build(StdfFile file)
        {
            var result = new SummaryResult
            {
                FileLength = file.FileLength,
                RecordCount = file.Count,
                TypeCount = file.Types.Count,
                ScanMs = file.ScanTime.TotalMilliseconds,
                BigEndian = file.BigEndian,
            };

            result.Sections.Add(BuildFileSection(file));
            AddRecordSections(result, file, 1, 10, "MIR");
            AddRecordSections(result, file, 1, 20, "MRR");
            AddRecordSections(result, file, 1, 80, "SDR");
            AddWaferSections(result, file);
            AddRecordSections(result, file, 1, 30, "PCR");
            AddBinSection(result, file, 1, 40, "HBR", "HBIN_");
            AddBinSection(result, file, 1, 50, "SBR", "SBIN_");
            result.Sections.Add(BuildCountSection(file));
            ComputeKpi(result, file);
            return result;
        }

        private static SummarySection BuildFileSection(StdfFile file)
        {
            var section = new SummarySection { Title = Loc.T("Str.Summary.File") };
            Add(section, Loc.T("Str.File.Name"), Path.GetFileName(file.Path));
            Add(section, Loc.T("Str.File.Path"), file.Path);
            Add(section, Loc.T("Str.File.Size"), Number(file.FileLength) + " B");
            Add(section, Loc.T("Str.File.LastWrite"), LastWrite(file.Path));
            Add(section, Loc.T("Str.File.ScanTime"), file.ScanTime.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms");
            Add(section, Loc.T("Str.File.Endian"), EndianWord(file.BigEndian));
            if (file.CpuType >= 0)
                Add(section, "CPU_TYPE", file.CpuType.ToString(CultureInfo.InvariantCulture));
            if (file.StdfVersion >= 0)
                Add(section, "STDF_VER", file.StdfVersion.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(file.Note))
                Add(section, Loc.T("Str.File.Note"), file.Note);
            return section;
        }

        private static void AddRecordSections(SummaryResult result, StdfFile file, byte typ, byte sub, string name)
        {
            int[] seqs = file.SeqsOf(typ, sub);
            for (int i = 0; i < seqs.Length; i++)
                AddDecoded(result, file, seqs[i], name, i, seqs.Length);
        }

        private static void AddWaferSections(SummaryResult result, StdfFile file)
        {
            int[] wir = file.SeqsOf(2, 10);
            int[] wrr = file.SeqsOf(2, 20);
            int count = Math.Max(wir.Length, wrr.Length);
            for (int i = 0; i < count; i++)
            {
                if (i < wir.Length)
                    AddDecoded(result, file, wir[i], "WIR", i, count);
                if (i < wrr.Length)
                    AddDecoded(result, file, wrr[i], "WRR", i, count);
            }
            AddRecordSections(result, file, 2, 30, "WCR");
        }

        private static void AddDecoded(SummaryResult result, StdfFile file, int seq, string name, int index, int count)
        {
            DecodedRecord rec = StdfDecoder.Decode(file, seq);
            var section = new SummarySection { Title = OrdinalTitle(name, index, count) };
            AppendFields(section, rec);
            result.Sections.Add(section);
        }

        private static void AddBinSection(SummaryResult result, StdfFile file, byte typ, byte sub, string name, string prefix)
        {
            int[] seqs = file.SeqsOf(typ, sub);
            if (seqs.Length == 0)
                return;

            var section = new SummarySection { Title = name };
            foreach (int seq in seqs)
            {
                DecodedRecord rec = StdfDecoder.Decode(file, seq);
                section.Bins.Add(new SummaryBinRow
                {
                    HeadNum = FieldValue(rec, "HEAD_NUM"),
                    SiteNum = FieldValue(rec, "SITE_NUM"),
                    BinNum = FieldValue(rec, prefix + "NUM"),
                    BinCount = GroupedNumber(FieldValue(rec, prefix + "CNT")),
                    BinPf = PlainChar(FieldValue(rec, prefix + "PF")),
                    BinName = FieldValue(rec, prefix + "NAM"),
                });
            }
            result.Sections.Add(section);
        }

        private static SummarySection BuildCountSection(StdfFile file)
        {
            var section = new SummarySection { Title = Loc.T("Str.Summary.Counts") };
            Add(section, Loc.T("Str.Count.Records"), Number(file.Count));
            Add(section, Loc.T("Str.Count.Types"), file.Types.Count.ToString(CultureInfo.InvariantCulture));

            var counts = new Dictionary<int, long>();
            foreach (RecordTypeStat type in file.Types)
                counts[Key(type.Typ, type.Sub)] = type.Count;

            foreach (int key in CountedRecords)
            {
                long count;
                if (!counts.TryGetValue(key, out count) || count == 0)
                    continue;
                Add(section, StdfRecordTable.DisplayName((byte)(key >> 8), (byte)(key & 0xFF)), Number(count));
            }
            return section;
        }

        private static void ComputeKpi(SummaryResult result, StdfFile file)
        {
            result.DutCount = file.SeqsOf(5, 10).Length;
            if (result.DutCount == 0)
                result.DutCount = file.SeqsOf(5, 20).Length;

            long parts = 0;
            long goods = 0;
            SumYield(file, 1, 30, ref parts, ref goods);
            if (parts == 0)
                SumYield(file, 2, 20, ref parts, ref goods);
            if (parts > 0)
                result.YieldPercent = 100.0 * goods / parts;
        }

        private static void SumYield(StdfFile file, byte typ, byte sub, ref long parts, ref long goods)
        {
            foreach (int seq in file.SeqsOf(typ, sub))
            {
                DecodedRecord rec = StdfDecoder.Decode(file, seq);
                parts += FieldNumber(rec, "PART_CNT");
                goods += FieldNumber(rec, "GOOD_CNT");
            }
        }

        private static void AppendFields(SummarySection section, DecodedRecord rec)
        {
            foreach (FieldRow row in rec.Rows)
            {
                if (row.Status == "header" || row.Status == "layout gap" || row.Status == "unknown type")
                    continue;
                if (string.IsNullOrEmpty(row.Name) || row.Name[0] == '(')
                    continue;
                if (string.IsNullOrEmpty(row.Value) || row.Value == StdfDecoder.EmptyToken)
                    continue;
                Add(section, row.Name, row.Value);
            }
        }

        private static string FieldValue(DecodedRecord rec, string name)
        {
            foreach (FieldRow row in rec.Rows)
            {
                if (row.Name == name)
                    return row.Value ?? "";
            }
            return "";
        }

        private static long FieldNumber(DecodedRecord rec, string name)
        {
            long value;
            return long.TryParse(FieldValue(rec, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static string OrdinalTitle(string name, int index, int count)
        {
            return count > 1 ? name + " #" + (index + 1).ToString(CultureInfo.InvariantCulture) : name;
        }

        private static string PlainChar(string value)
        {
            if (value != null && value.Length >= 3 && value[0] == '\'' && value[value.Length - 1] == '\'')
                return value.Substring(1, value.Length - 2);
            return value ?? "";
        }

        private static string GroupedNumber(string value)
        {
            long number;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? Number(number)
                : value;
        }

        private static string Number(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string EndianWord(bool bigEndian)
        {
            return Loc.T(bigEndian ? "Str.Word.BigEndian" : "Str.Word.LittleEndian");
        }

        private static string LastWrite(string path)
        {
            try
            {
                return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return Loc.T("Str.Word.Na");
            }
        }

        private static void Add(SummarySection section, string name, string value)
        {
            section.Fields.Add(new SummaryField { Name = name, Value = value });
        }

        private static int Key(byte typ, byte sub)
        {
            return (typ << 8) | sub;
        }
    }
}
