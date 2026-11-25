using System;
using System.Drawing;

namespace Gravity
{
    internal static partial class Theme
    {
        public static Color Accent { get; private set; }
        public static Color AccentHot { get; private set; }
        public static Color AccentSoft { get; private set; }
        public static Color SidebarSelection { get; private set; }
        public static Color OutlineSelectedText { get; private set; }
        public static Color ThumbnailSelection { get; private set; }
        public static Color Selection { get; private set; }
        public static Color Hover { get; private set; }
        public static Color MenuHover { get; private set; }
        public static Color HoverBorder { get; private set; }
        public static Color Pressed { get; private set; }
        public static Color RecentHover { get; private set; }
        public static Color AccentHint { get; private set; }
        public static string AccentHex { get; private set; }

        static Theme() { SetAccent(IconColor.DefaultHex); }

        public static void SetAccent(string hex)
        {
            AccentHex = IconColor.Normalize(hex);
            if (AccentHex == IconColor.DefaultHex)
            {
                Accent = Color.FromArgb(0x17, 0x4B, 0x8B);
                AccentHot = Color.FromArgb(0x08, 0x47, 0xAE);
                AccentSoft = Color.FromArgb(0xE2, 0xEC, 0xF8);
                SidebarSelection = Color.FromArgb(0xE2, 0xEA, 0xF5);
                OutlineSelectedText = Color.FromArgb(0x15, 0x3E, 0x72);
                ThumbnailSelection = Color.FromArgb(0xDC, 0xE8, 0xF7);
                Selection = Color.FromArgb(80, 0x1A, 0x73, 0xE8);
                Hover = Color.FromArgb(0xE2, 0xE8, 0xF0);
                MenuHover = Color.FromArgb(0xE8, 0xEF, 0xF9);
                HoverBorder = Color.FromArgb(0xC6, 0xD7, 0xEE);
                Pressed = Color.FromArgb(0xD4, 0xE1, 0xF1);
                RecentHover = Color.FromArgb(0xEE, 0xF4, 0xFC);
                AccentHint = Color.FromArgb(0xC8, 0xDD, 0xF3);
                return;
            }
            var seed = IconColor.Parse(AccentHex);
            // Pale colors still need visible selection washes; grayscale choices stay grayscale.
            var tone = Readable(seed, Color.White, 2);
            AccentSoft = Mix(tone, Color.White, .90);
            SidebarSelection = Mix(tone, Color.White, .85);
            ThumbnailSelection = Mix(tone, Color.White, .83);
            Hover = Mix(tone, Color.White, .87);
            MenuHover = Mix(tone, Color.White, .91);
            HoverBorder = Mix(tone, Color.White, .68);
            Pressed = Mix(tone, Color.White, .77);
            RecentHover = Mix(tone, Color.White, .94);
            Accent = Readable(Readable(seed, Pressed, 4.5), ViewBack, 4.5);
            AccentHot = Mix(Accent, Color.Black, .15);
            OutlineSelectedText = Mix(Accent, Color.Black, .20);
            Selection = Color.FromArgb(80, tone);
            AccentHint = Color.White;
        }

        private static Color Mix(Color a, Color b, double amount)
        {
            return Color.FromArgb((int)Math.Round(a.R + (b.R - a.R) * amount),
                (int)Math.Round(a.G + (b.G - a.G) * amount), (int)Math.Round(a.B + (b.B - a.B) * amount));
        }

        private static Color Readable(Color color, Color background, double minimum)
        {
            var result = color;
            for (int step = 1; Contrast(result, background) < minimum && step <= 100; step++)
                result = Mix(color, Color.Black, step / 100.0);
            return result;
        }

        internal static double Contrast(Color a, Color b)
        {
            double x = Luminance(a), y = Luminance(b);
            return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
        }

        private static double Luminance(Color color)
        {
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }

        private static double Linear(byte channel)
        {
            double v = channel / 255.0;
            return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
        }
    }
}
