using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StdfViewer
{
    // 历史快照里的一条类型统计（注意：不要把 StdfFile 的 RecordTypeStat 直接序列化，
    // 它带 Seqs 数组，会把几十万个 int 写进 json）
    public sealed class HistoryTypeStat
    {
        public int Typ { get; set; }
        public int Sub { get; set; }
        public string Name { get; set; } = "";
        public long Count { get; set; }
        public long Bytes { get; set; }
        public int FirstSeq { get; set; }

        [JsonIgnore]
        public string TypSub { get { return Typ + "," + Sub; } }
    }

    // 一次解析结果的快照：点历史条目时只展示这份数据，不重新读磁盘
    public sealed class HistoryEntry
    {
        public string Path { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Size { get; set; }
        public string LastWriteUtc { get; set; } = "";
        public string ParsedAt { get; set; } = "";
        public int Records { get; set; }
        public int TypeCount { get; set; }
        public int CpuType { get; set; }
        public int StdfVer { get; set; }
        public bool BigEndian { get; set; }
        public string Note { get; set; } = "";
        public double ScanMs { get; set; }
        public List<HistoryTypeStat> Types { get; set; } = new List<HistoryTypeStat>();
    }

    public sealed class HistoryFile
    {
        public int Version { get; set; } = 1;
        public List<HistoryEntry> Entries { get; set; } = new List<HistoryEntry>();
    }

    // history.json 放在 exe 同目录（bin 下）。目录不可写时只记错误、不回退写 C 盘。
    public sealed class HistoryStore
    {
        private const int MaxEntries = 200;

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly ObservableCollection<HistoryEntry> _entries = new ObservableCollection<HistoryEntry>();

        public ObservableCollection<HistoryEntry> Entries { get { return _entries; } }

        // 读写失败时这里会有原因，界面拿去显示，不弹异常
        public string LastError { get; private set; } = "";

        public static string FilePath
        {
            get { return System.IO.Path.Combine(AppContext.BaseDirectory, "history.json"); }
        }

        public static HistoryEntry FromScan(StdfFile file)
        {
            var entry = new HistoryEntry
            {
                Path = file.Path,
                FileName = System.IO.Path.GetFileName(file.Path),
                Size = file.FileLength,
                LastWriteUtc = LastWrite(file.Path),
                ParsedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Records = file.Count,
                TypeCount = file.Types.Count,
                CpuType = file.CpuType,
                StdfVer = file.StdfVersion,
                BigEndian = file.BigEndian,
                Note = file.Note,
                ScanMs = file.ScanTime.TotalMilliseconds,
            };
            foreach (RecordTypeStat t in file.Types)
            {
                entry.Types.Add(new HistoryTypeStat
                {
                    Typ = t.Typ,
                    Sub = t.Sub,
                    Name = t.Name,
                    Count = t.Count,
                    Bytes = t.Bytes,
                    FirstSeq = t.FirstSeq,
                });
            }
            return entry;
        }

        private static string LastWrite(string path)
        {
            try
            {
                return File.GetLastWriteTimeUtc(path).ToString("O", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "";
            }
        }

        public void Load()
        {
            _entries.Clear();
            LastError = "";
            try
            {
                string path = FilePath;
                if (!File.Exists(path))
                    return;
                HistoryFile file = JsonSerializer.Deserialize<HistoryFile>(File.ReadAllText(path, Encoding.UTF8), Json);
                if (file == null || file.Entries == null)
                    return;
                foreach (HistoryEntry e in file.Entries)
                {
                    if (e == null)
                        continue;
                    if (e.Types == null)
                        e.Types = new List<HistoryTypeStat>();
                    if (e.Path == null) e.Path = "";
                    if (e.FileName == null) e.FileName = "";
                    if (e.LastWriteUtc == null) e.LastWriteUtc = "";
                    if (e.ParsedAt == null) e.ParsedAt = "";
                    if (e.Note == null) e.Note = "";
                    _entries.Add(e);
                }
            }
            catch (Exception ex)
            {
                _entries.Clear();
                LastError = "history.json 读取失败：" + ex.Message;
            }
        }

        // 去重键 = 路径 + 大小 + 最后写入时间：同一条路径的不同版本各留一条，方便比新旧两版
        public void Record(StdfFile file)
        {
            Upsert(FromScan(file));
        }

        public void Upsert(HistoryEntry entry)
        {
            int found = -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                HistoryEntry e = _entries[i];
                if (e.Path == entry.Path && e.Size == entry.Size && e.LastWriteUtc == entry.LastWriteUtc)
                {
                    found = i;
                    break;
                }
            }
            if (found >= 0)
                _entries[found] = entry;
            else
                _entries.Insert(0, entry);

            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(_entries.Count - 1);

            Save();
        }

        public void Remove(HistoryEntry entry)
        {
            if (!_entries.Remove(entry))
                return;
            Save();
        }

        private void Save()
        {
            try
            {
                var file = new HistoryFile { Version = 1, Entries = new List<HistoryEntry>(_entries) };
                string json = JsonSerializer.Serialize(file, Json);
                string path = FilePath;
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tmp, path, null);
                    }
                    catch
                    {
                        File.Copy(tmp, path, true);
                        File.Delete(tmp);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                }
                LastError = "";
            }
            catch (Exception ex)
            {
                LastError = FilePath + " 不可写，历史记录未保存：" + ex.Message;
            }
        }
    }
}
