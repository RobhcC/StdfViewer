using System.Globalization;

namespace StdfViewer.ViewModels
{
    internal static class UiFormat
    {
        public static string N(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string EndianWord(bool bigEndian)
        {
            return Loc.T(bigEndian ? "Str.Word.BigEndian" : "Str.Word.LittleEndian");
        }
    }
}
