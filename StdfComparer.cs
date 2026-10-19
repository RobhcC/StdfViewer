using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StdfViewer
{
    public sealed class CompareTypeRow
    {
        public string TypSub { get; set; }
        public string Name { get; set; }
        public long LeftCount { get; set; }
        public long RightCount { get; set; }
        public long Delta { get; set; }
        public string Status { get; set; }
    }

    // 记录流按"相邻同类型合并"后的一个段
    public struct StructRun
    {
        public byte Typ;
        public byte Sub;
        public int Count;
    }

    public sealed class StructureCompare
    {
        public int LeftSegments;
        public int RightSegments;
        public int CommonPrefix;
        public int CommonSuffix;
        public int LeftWindow;
        public int RightWindow;
        public bool Identical;
        public string LeftWindowText = "";
        public string RightWindowText = "";
    }

    public static class StdfComparer
    {
        public const string OnlyLeft = "only left";
        public const string OnlyRight = "only right";

        private const int WindowTextSegments = 40;

        // 类型层差异：以 typ,sub 为键合并两侧，Status 标出"仅一侧有"
        public static List<CompareTypeRow> CompareTypes(IList<HistoryTypeStat> left, IList<HistoryTypeStat> right)
        {
            var map = new SortedDictionary<int, CompareTypeRow>();
            if (left != null)
            {
                foreach (HistoryTypeStat t in left)
                {
                    var row = new CompareTypeRow { TypSub = t.TypSub, Name = t.Name, LeftCount = t.Count };
                    map[(t.Typ << 8) | t.Sub] = row;
                }
            }
            if (right != null)
            {
                foreach (HistoryTypeStat t in right)
                {
                    int key = (t.Typ << 8) | t.Sub;
                    CompareTypeRow row;
                    if (!map.TryGetValue(key, out row))
                    {
                        row = new CompareTypeRow { TypSub = t.TypSub, Name = t.Name };
                        map[key] = row;
                    }
                    row.RightCount = t.Count;
                }
            }

            var rows = new List<CompareTypeRow>(map.Count);
            foreach (KeyValuePair<int, CompareTypeRow> kv in map)
            {
                CompareTypeRow row = kv.Value;
                int typ = kv.Key >> 8;
                int sub = kv.Key & 0xFF;
                if (string.IsNullOrEmpty(row.Name))
                    row.Name = StdfRecordTable.DisplayName((byte)typ, (byte)sub);
                row.Delta = row.LeftCount - row.RightCount;
                if (row.LeftCount > 0 && row.RightCount > 0)
                    row.Status = "";
                else if (row.LeftCount > 0)
                    row.Status = OnlyLeft;
                else
                    row.Status = OnlyRight;
                rows.Add(row);
            }
            return rows;
        }

        // 类型"首次出现顺序"对比：两侧各自按 firstSeq 排序后逐项比
        public static string CompareTypeOrder(IList<HistoryTypeStat> left, IList<HistoryTypeStat> right)
        {
            List<string> la = OrderedNames(left);
            List<string> lb = OrderedNames(right);
            int min = Math.Min(la.Count, lb.Count);
            for (int i = 0; i < min; i++)
            {
                if (la[i] != lb[i])
                {
                    return "type order: differs (left " + la.Count + " types, right " + lb.Count + " types)"
                        + "; first difference at #" + (i + 1) + ": left " + la[i] + ", right " + lb[i];
                }
            }
            if (la.Count != lb.Count)
            {
                string extra = la.Count > lb.Count ? "left" : "right";
                string missing = la.Count > lb.Count ? "right" : "left";
                return "type order: same up to #" + min + ", " + extra + " has " + Math.Abs(la.Count - lb.Count)
                    + " more types than " + missing;
            }
            return "type order: identical (" + la.Count + " types)";
        }

        private static List<string> OrderedNames(IList<HistoryTypeStat> types)
        {
            var list = new List<HistoryTypeStat>();
            if (types != null)
                list.AddRange(types);
            list.Sort(delegate(HistoryTypeStat a, HistoryTypeStat b) { return a.FirstSeq.CompareTo(b.FirstSeq); });

            var names = new List<string>(list.Count);
            foreach (HistoryTypeStat t in list)
            {
                string n = t.Name;
                if (string.IsNullOrEmpty(n))
                    n = StdfRecordTable.DisplayName((byte)t.Typ, (byte)t.Sub);
                names.Add(n);
            }
            return names;
        }

        // 记录流 → RLE 段列表（TypOf/SubOf 是纯数组索引，32 万次只需几毫秒）
        public static List<StructRun> BuildRuns(StdfFile file)
        {
            var runs = new List<StructRun>();
            int n = file.Count;
            for (int seq = 0; seq < n; seq++)
            {
                byte typ = file.TypOf(seq);
                byte sub = file.SubOf(seq);
                int last = runs.Count - 1;
                if (last >= 0)
                {
                    StructRun tail = runs[last];
                    if (tail.Typ == typ && tail.Sub == sub)
                    {
                        tail.Count++;
                        runs[last] = tail;
                        continue;
                    }
                }
                var run = new StructRun { Typ = typ, Sub = sub, Count = 1 };
                runs.Add(run);
            }
            return runs;
        }

        // 先裁掉公共前缀与公共后缀，只报告差异窗口，避免"首个差异之后全是噪声"
        public static StructureCompare CompareStructure(List<StructRun> left, List<StructRun> right)
        {
            var r = new StructureCompare();
            r.LeftSegments = left.Count;
            r.RightSegments = right.Count;

            int min = Math.Min(left.Count, right.Count);
            int p = 0;
            while (p < min && Same(left[p], right[p]))
                p++;

            int endLeft = left.Count;
            int endRight = right.Count;
            while (endLeft > p && endRight > p && Same(left[endLeft - 1], right[endRight - 1]))
            {
                endLeft--;
                endRight--;
            }

            r.CommonPrefix = p;
            r.CommonSuffix = left.Count - endLeft;
            r.LeftWindow = endLeft - p;
            r.RightWindow = endRight - p;
            r.Identical = r.LeftWindow == 0 && r.RightWindow == 0;
            r.LeftWindowText = RunsText(left, p, Math.Min(r.LeftWindow, WindowTextSegments));
            r.RightWindowText = RunsText(right, p, Math.Min(r.RightWindow, WindowTextSegments));
            return r;
        }

        private static bool Same(StructRun a, StructRun b)
        {
            return a.Typ == b.Typ && a.Sub == b.Sub && a.Count == b.Count;
        }

        public static string RunsText(List<StructRun> runs, int from, int count)
        {
            if (count <= 0)
                return "";
            var sb = new StringBuilder();
            int end = Math.Min(from + count, runs.Count);
            for (int i = from; i < end; i++)
            {
                if (i > from)
                    sb.Append(", ");
                sb.Append(StdfRecordTable.DisplayName(runs[i].Typ, runs[i].Sub));
                if (runs[i].Count > 1)
                    sb.Append('×').Append(runs[i].Count.ToString(CultureInfo.InvariantCulture));
            }
            if (end < runs.Count)
                sb.Append(", ...");
            return sb.ToString();
        }
    }
}
