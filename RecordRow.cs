using System.Collections.Generic;
using System.Globalization;

namespace StdfViewer
{
    public sealed class RecordRow
    {
        public int Seq { get; set; }
        public int No { get; set; }
        public string Offset { get; set; }
        public string TypSub { get; set; }
        public string Name { get; set; }
        public int Len { get; set; }
    }

    // 列表最多展开这么多行，再多就要求按类型过滤，避免一次生成几百万个行对象
    public static class RecordListBuilder
    {
        public static List<RecordRow> Build(StdfFile file, int[] seqs, int limit)
        {
            int total = seqs == null ? file.Count : seqs.Length;
            int n = System.Math.Min(limit, total);
            var rows = new List<RecordRow>(n);
            for (int i = 0; i < n; i++)
            {
                int seq = seqs != null ? seqs[i] : i;
                byte typ = file.TypOf(seq);
                byte sub = file.SubOf(seq);
                rows.Add(new RecordRow
                {
                    Seq = seq,
                    No = seq + 1,
                    Offset = "0x" + file.OffsetOf(seq).ToString("X", CultureInfo.InvariantCulture),
                    TypSub = typ + "," + sub,
                    Name = StdfRecordTable.DisplayName(typ, sub),
                    Len = file.LengthOf(seq),
                });
            }
            return rows;
        }
    }
}
