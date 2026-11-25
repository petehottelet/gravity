using System.Drawing;
using System.Linq;

namespace Gravity.Tests
{
    internal static class IconTests
    {
        internal static void Run()
        {
            var resources = typeof(LucideIcons).Assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("Lucide.") && n.EndsWith(".svg")).ToArray();
            SelfTest.Check(resources.Length == 38, "all Lucide assets are embedded for offline rendering");
            foreach (var resource in resources)
            {
                string name = resource.Substring(7, resource.Length - 11);
                foreach (int size in new[] { 16, 20, 40 })
                    using (var image = LucideIcons.Create(name, size, Theme.Text))
                    {
                        int ink = 0;
                        for (int y = 0; y < size; y++)
                            for (int x = 0; x < size; x++) if (image.GetPixel(x, y).A > 32) ink++;
                        SelfTest.Check(ink > size / 3 && ink < size * size * .8, name + " renders visible strokes at " + size + " pixels");
                    }
            }
            using (var left = LucideIcons.Create("rotate-ccw", 40, Color.Black))
            using (var right = LucideIcons.Create("rotate-cw", 40, Color.Black))
            {
                int difference = 0;
                for (int y = 0; y < 40; y++)
                    for (int x = 0; x < 40; x++) if (left.GetPixel(x, y) != right.GetPixel(x, y)) difference++;
                SelfTest.Check(difference > 100, "rotation actions have distinct directional icons");
            }
        }
    }
}
