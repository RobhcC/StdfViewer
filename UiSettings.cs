using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StdfViewer
{
    // settings.json 与 history.json 同目录、同一套原子写；只存界面偏好，读写失败不影响功能
    public static class UiSettings
    {
        private sealed class SettingsFile
        {
            public int Version { get; set; } = 1;

            public string Language { get; set; } = "";
        }

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string LastError { get; private set; } = "";

        public static string FilePath
        {
            get { return Path.Combine(AppContext.BaseDirectory, "settings.json"); }
        }

        public static string LoadLanguage()
        {
            LastError = "";
            try
            {
                string path = FilePath;
                if (!File.Exists(path))
                    return "";
                SettingsFile file = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(path, Encoding.UTF8), Json);
                return file == null || file.Language == null ? "" : file.Language;
            }
            catch (Exception ex)
            {
                LastError = FilePath + ": " + ex.Message;
                return "";
            }
        }

        public static void SaveLanguage(string language)
        {
            LastError = "";
            try
            {
                string json = JsonSerializer.Serialize(new SettingsFile { Language = language }, Json);
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
            }
            catch (Exception ex)
            {
                LastError = FilePath + ": " + ex.Message;
            }
        }
    }
}
