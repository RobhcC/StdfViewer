using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace StdfViewer
{
    // 界面文案的唯一入口：XAML 用 DynamicResource 取同一个键，代码用 T(...) 取。
    // 切换语言就是换一份 ResourceDictionary，再通知窗口重刷代码生成的那部分文字。
    public static class Loc
    {
        public const string English = "en-US";
        public const string Chinese = "zh-CN";

        private const string DictionaryPrefix = "pack://application:,,,/StdfViewer;component/Themes/Strings.";

        private static string _language = English;

        // 字符串另存一份普通字典：后台线程（扫描、写库）也能安全取文案
        private static Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.Ordinal);

        public static event EventHandler LanguageChanged;

        public static string Language
        {
            get { return _language; }
        }

        public static bool IsChinese
        {
            get { return _language == Chinese; }
        }

        public static void Initialize()
        {
            string saved = UiSettings.LoadLanguage();
            Apply(string.IsNullOrEmpty(saved) ? DetectSystem() : Normalize(saved), false);
        }

        public static void SetLanguage(string language)
        {
            string normalized = Normalize(language);
            if (normalized == _language)
                return;
            Apply(normalized, true);
            UiSettings.SaveLanguage(normalized);
        }

        public static string T(string key)
        {
            string value;
            return _strings.TryGetValue(key, out value) ? value : key;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, T(key), args);
        }

        // 解码状态是稳定 token（代码里要比较），显示时才翻译；未知 token 原样返回
        public static string Status(string token)
        {
            switch (token)
            {
                case "":
                    return "";
                case "ok":
                    return T("Str.St.Ok");
                case "header":
                    return T("Str.St.Header");
                case "omitted":
                    return T("Str.St.Omitted");
                case "truncated":
                    return T("Str.St.Truncated");
                case "layout gap":
                    return T("Str.St.LayoutGap");
                case "unknown type":
                    return T("Str.St.UnknownType");
                default:
                    return token;
            }
        }

        private static void Apply(string language, bool notify)
        {
            _language = language;
            ResourceDictionary dictionary = new ResourceDictionary
            {
                Source = new Uri(DictionaryPrefix + language + ".xaml", UriKind.Absolute),
            };

            // 先备好纯字符串表，再换资源字典：两边不会读到半份
            var strings = new Dictionary<string, string>(dictionary.Count, StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                string text = entry.Value as string;
                if (text != null)
                    strings[(string)entry.Key] = text;
            }
            _strings = strings;

            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = 0; i < merged.Count; i++)
            {
                Uri source = merged[i].Source;
                if (source != null && source.OriginalString.IndexOf("/Strings.", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    merged[i] = dictionary;
                    if (notify)
                        LanguageChanged?.Invoke(null, EventArgs.Empty);
                    return;
                }
            }

            merged.Add(dictionary);
            if (notify)
                LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        private static string DetectSystem()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? Chinese : English;
        }

        private static string Normalize(string language)
        {
            return language == Chinese ? Chinese : English;
        }
    }
}
