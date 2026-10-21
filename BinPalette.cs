using System.Windows.Media;

namespace StdfViewer
{
    public static class BinPalette
    {
        private static readonly SolidColorBrush BinZeroBrush = Frozen(0xC7, 0xCE, 0xD8);
        private static readonly SolidColorBrush[] BinBrushes =
        {
            Frozen(0x2F, 0x9E, 0x5B), Frozen(0xD9, 0x53, 0x4F), Frozen(0x3B, 0x7B, 0xD9),
            Frozen(0xE0, 0xA0, 0x30), Frozen(0x8A, 0x63, 0xD2), Frozen(0x2A, 0xA7, 0xA0),
            Frozen(0xD2, 0x67, 0x9B), Frozen(0x8A, 0x6D, 0x3B), Frozen(0x6C, 0x7A, 0x89),
            Frozen(0xC9, 0xA2, 0x27), Frozen(0x4F, 0x8A, 0x5B), Frozen(0xA3, 0x5B, 0xA3),
        };

        public static SolidColorBrush BrushForBin(int bin)
        {
            return bin <= 0 ? BinZeroBrush : BinBrushes[(bin - 1) % BinBrushes.Length];
        }

        private static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
