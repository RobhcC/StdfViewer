using System;
using System.Collections.Generic;
using System.IO;

namespace StdfViewer
{
    public sealed class RecordTypeStat
    {
        public byte Typ { get; set; }
        public byte Sub { get; set; }
        public string Name { get; set; }
        public string TypSub { get { return Typ + "," + Sub; } }
        public long Count { get; set; }
        public long Bytes { get; set; }
        public int FirstSeq { get; set; }
        public int[] Seqs { get; set; } = Array.Empty<int>();
    }

    public sealed class StdfFile
    {
        private const int BufferSize = 1 << 20;

        private FileStream _stream;
        private readonly string _path;
        private long[] _offsets = new long[4096];
        private int[] _lengths = new int[4096];
        private byte[] _typs = new byte[4096];
        private byte[] _subs = new byte[4096];
        private readonly Dictionary<int, List<int>> _seqsByType = new Dictionary<int, List<int>>();

        private StdfFile(string path)
        {
            _path = path;
        }

        public string Path { get { return _path; } }
        public long FileLength { get; private set; }
        public int Count { get; private set; }
        public bool BigEndian { get; private set; }
        public int CpuType { get; private set; } = -1;
        public int StdfVersion { get; private set; } = -1;
        public string Note { get; private set; } = "";
        public List<RecordTypeStat> Types { get; } = new List<RecordTypeStat>();
        public TimeSpan ScanTime { get; private set; }

        public static StdfFile Scan(string path)
        {
            DateTime started = DateTime.UtcNow;
            var file = new StdfFile(path);
            try
            {
                file._stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize);
                file.Read();
                file.ReadFar();
            }
            catch (Exception ex)
            {
                file.Note = ex.Message;
            }
            file.Close();
            file.Seal(started);
            return file;
        }

        public void Close()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        private void Read()
        {
            FileLength = _stream.Length;
            var buf = new byte[BufferSize];
            long basePos = 0;
            int bufLen = Fill(buf, 0);
            long pos = 0;
            bool probed = false;

            while (pos + 4 <= FileLength)
            {
                int rel = (int)(pos - basePos);
                if (rel + 4 > bufLen)
                {
                    basePos = pos;
                    bufLen = Fill(buf, pos);
                    rel = 0;
                }
                if (bufLen < 4)
                    break;

                int len = Len(buf, rel, BigEndian);
                byte typ = buf[rel + 2];
                byte sub = buf[rel + 3];

                if (!probed)
                {
                    probed = true;
                    int beLen = Len(buf, rel, true);
                    if ((!Plausible(len, pos) || !Known(typ, sub)) && Plausible(beLen, pos) && Known(buf[rel + 2], buf[rel + 3]))
                    {
                        BigEndian = true;
                        len = beLen;
                    }
                }

                if (!Plausible(len, pos))
                {
                    if (len > 0 && pos + 4 < FileLength)
                    {
                        Add(pos, (int)(FileLength - pos - 4), typ, sub);
                        Note = "末尾记录不完整：REC_LEN=" + len + "，实际只剩 " + (FileLength - pos - 4) + " 字节";
                    }
                    else
                    {
                        Note = "偏移 " + pos + " 处记录头无法解析（REC_LEN=" + len + "），扫描在此停止";
                    }
                    return;
                }

                Add(pos, len, typ, sub);
                pos += 4 + len;
            }

            if (pos < FileLength)
                Note = "文件末尾残留 " + (FileLength - pos) + " 字节，不足一个记录头";
        }

        private int Fill(byte[] buf, long from)
        {
            _stream.Seek(from, SeekOrigin.Begin);
            int read = 0;
            while (read < buf.Length)
            {
                int n = _stream.Read(buf, read, buf.Length - read);
                if (n <= 0)
                    break;
                read += n;
            }
            return read;
        }

        private static int Len(byte[] buf, int rel, bool be)
        {
            return be ? ((buf[rel] << 8) | buf[rel + 1]) : (buf[rel] | (buf[rel + 1] << 8));
        }

        private bool Plausible(int len, long pos)
        {
            return len >= 0 && pos + 4 + len <= FileLength;
        }

        private static bool Known(byte typ, byte sub)
        {
            return StdfRecordTable.Find(typ, sub) != null;
        }

        private void ReadFar()
        {
            if (Count == 0 || TypOf(0) != 0 || LengthOf(0) < 2)
                return;
            byte[] body = ReadBody(0);
            CpuType = body[0];
            StdfVersion = body[1];
            if (IsBigEndianCpu(CpuType) != BigEndian)
                Note = "CPU_TYPE=" + CpuType + " 与记录头实测字节序不一致，按实测字节序解码";
        }

        // 已知的大端 CPU_TYPE；其余按小端处理
        private static bool IsBigEndianCpu(int cpuType)
        {
            return cpuType == 1 || cpuType == 5 || cpuType == 6 || cpuType == 11 || cpuType == 16 || cpuType == 17;
        }

        private void Add(long offset, int len, byte typ, byte sub)
        {
            if (Count == _offsets.Length)
            {
                int next = _offsets.Length * 2;
                Array.Resize(ref _offsets, next);
                Array.Resize(ref _lengths, next);
                Array.Resize(ref _typs, next);
                Array.Resize(ref _subs, next);
            }
            _offsets[Count] = offset;
            _lengths[Count] = len;
            _typs[Count] = typ;
            _subs[Count] = sub;
            Count++;

            int key = (typ << 8) | sub;
            List<int> seqs;
            if (!_seqsByType.TryGetValue(key, out seqs))
            {
                seqs = new List<int>();
                _seqsByType[key] = seqs;
            }
            seqs.Add(Count - 1);
        }

        private void Seal(DateTime started)
        {
            Array.Resize(ref _offsets, Count);
            Array.Resize(ref _lengths, Count);
            Array.Resize(ref _typs, Count);
            Array.Resize(ref _subs, Count);

            foreach (KeyValuePair<int, List<int>> kv in _seqsByType)
            {
                byte typ = (byte)(kv.Key >> 8);
                byte sub = (byte)kv.Key;
                long bytes = 0;
                foreach (int seq in kv.Value)
                    bytes += 4 + _lengths[seq];
                Types.Add(new RecordTypeStat
                {
                    Typ = typ,
                    Sub = sub,
                    Name = StdfRecordTable.DisplayName(typ, sub),
                    Count = kv.Value.Count,
                    Bytes = bytes,
                    FirstSeq = kv.Value[0] + 1,
                    Seqs = kv.Value.ToArray(),
                });
            }
            Types.Sort(delegate(RecordTypeStat a, RecordTypeStat b)
            {
                int byTyp = a.Typ.CompareTo(b.Typ);
                return byTyp != 0 ? byTyp : a.Sub.CompareTo(b.Sub);
            });
            ScanTime = DateTime.UtcNow - started;
        }

        public long OffsetOf(int seq) { return _offsets[seq]; }
        public int LengthOf(int seq) { return _lengths[seq]; }
        public byte TypOf(int seq) { return _typs[seq]; }
        public byte SubOf(int seq) { return _subs[seq]; }

        public int[] SeqsOf(byte typ, byte sub)
        {
            List<int> seqs;
            return _seqsByType.TryGetValue((typ << 8) | sub, out seqs) ? seqs.ToArray() : Array.Empty<int>();
        }

        public byte[] ReadBody(int seq)
        {
            int len = _lengths[seq];
            var body = new byte[len];
            if (len == 0)
                return body;
            FileStream stream = _stream ?? (_stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16));
            stream.Seek(_offsets[seq] + 4, SeekOrigin.Begin);
            int read = 0;
            while (read < len)
            {
                int n = stream.Read(body, read, len - read);
                if (n <= 0)
                    break;
                read += n;
            }
            return body;
        }
    }
}
