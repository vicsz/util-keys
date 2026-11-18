namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Linq;

    // Draws every key: a muted label at the top, a big value, and a graph or bar at the bottom.
    // The keypad asks for 116 x 116 images, so the layout is written at that size.

    public static class KeyArt
    {
        public const UInt32 Text = 0xE7EDF5;
        public const UInt32 Muted = 0x8A95A5;
        public const UInt32 Green = 0x37D39A;
        public const UInt32 Amber = 0xFFB547;
        public const UInt32 Red = 0xFF5D5D;
        public const UInt32 Blue = 0x49A8FF;
        public const UInt32 Purple = 0xB48CFF;

        private const UInt32 Surface = 0x11161E;
        private const UInt32 Dim = 0x3A4653;
        private const Int32 DesignSize = 116;
        private const String Semibold = "Helvetica Neue Medium";

        // The SDK turns one-colour SVGs white. This invisible second colour keeps ours.
        private const String KeepColor = "<circle r='0' fill='#000001' stroke='none'/>";

        // Draws a key. Each line is centred on its y. Shapes are SVG drawn on the 116 grid, under the text.
        public static BitmapImage Key(PluginImageSize imageSize, String label, String shapes, params Line[] lines)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                var scale = (Single)builder.Width / DesignSize;
                builder.Clear(BitmapColor.FromRgb(Surface));

                if (!String.IsNullOrEmpty(shapes))
                {
                    var pixels = Scaled(DesignSize, scale);
                    var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='{pixels}' height='{pixels}' viewBox='0 0 {DesignSize} {DesignSize}'>{shapes}{KeepColor}</svg>";
                    builder.DrawImage(BitmapImage.FromSvg(svg), 0, 0, pixels, pixels);
                }

                DrawLine(builder, new Line(label, 16, 14, Muted), scale);

                foreach (var line in lines)
                {
                    DrawLine(builder, line, scale);
                }

                return builder.ToImage();
            }
        }

        // A filled line graph of values between 0 and max, newest on the right, sitting on y1.
        // A short history is drawn from the right, so the graph fills in as samples arrive.
        public static String Graph(Double[] values, Double max, UInt32 color, Int32 y0, Int32 y1)
        {
            if (values.Length < 2 || max <= 0)
            {
                return String.Empty;
            }

            const Single x0 = 10, x1 = 106;
            var step = (x1 - x0) / (History.Size - 1);
            var start = x1 - (step * (values.Length - 1));
            var points = values.Select((value, i) => FormattableString.Invariant(
                $"{start + (step * i):0.#},{y1 - ((y1 - y0) * Math.Clamp(value / max, 0, 1)):0.#}")).ToArray();
            var line = String.Join(" ", points);

            return FormattableString.Invariant($"<path d='M{start:0.#},{y1} L{String.Join(" L", points)} L{x1},{y1} Z' fill='{Hex(color)}' fill-opacity='0.22'/>") +
                $"<polyline points='{line}' fill='none' stroke='{Hex(color)}' stroke-width='2' stroke-linejoin='round' stroke-linecap='round'/>";
        }

        // A rounded bar filled from the left. fraction is 0 to 1.
        public static String Bar(Double fraction, UInt32 color, Int32 y)
        {
            const Single x0 = 14, x1 = 102, h = 8;
            var width = (x1 - x0) * (Single)Math.Clamp(fraction, 0, 1);
            var bar = FormattableString.Invariant($"<rect x='{x0}' y='{y}' width='{x1 - x0}' height='{h}' rx='{h / 2}' fill='{Hex(Dim)}'/>");

            // A sliver narrower than the bar's height would draw as a squashed dot, so it starts at a full circle.
            return width <= 0 ? bar : bar + FormattableString.Invariant(
                $"<rect x='{x0}' y='{y}' width='{Math.Max(h, width):0.#}' height='{h}' rx='{h / 2}' fill='{Hex(color)}'/>");
        }

        // A battery outline filled to fraction.
        public static String Battery(Double fraction, UInt32 color)
        {
            var width = 46 * (Single)Math.Clamp(fraction, 0, 1);
            return $"<rect x='30' y='64' width='52' height='22' rx='5' fill='none' stroke='{Hex(Muted)}' stroke-width='2'/>" +
                $"<rect x='83' y='70' width='4' height='10' rx='1.5' fill='{Hex(Muted)}'/>" +
                (width > 0 ? FormattableString.Invariant($"<rect x='33' y='67' width='{width:0.#}' height='16' rx='3' fill='{Hex(color)}'/>") : String.Empty);
        }

        // Four rising bars, the first lit ones in color.
        public static String SignalBars(Int32 lit, UInt32 color)
        {
            var bars = String.Empty;

            for (var i = 0; i < 4; i++)
            {
                var height = (i + 1) * 6;
                bars += $"<rect x='{40 + (i * 10)}' y='{100 - height}' width='6' height='{height}' rx='1.5' fill='{Hex(i < lit ? color : Dim)}'/>";
            }

            return bars;
        }

        public static String Hex(UInt32 color) => $"#{color:X6}";

        private static void DrawLine(BitmapBuilder builder, Line line, Single scale)
        {
            var height = (Int32)(line.Size * 1.6);
            builder.DrawText(line.Text, 0, Scaled(line.Y - (height / 2), scale), builder.Width, Scaled(height, scale),
                BitmapColor.FromRgb(line.Color), Scaled(line.Size, scale), -1, -1, line.IsBold ? Semibold : null);
        }

        private static Int32 Scaled(Int32 value, Single scale) => (Int32)Math.Round(value * scale);
    }

    public readonly record struct Line(String Text, Int32 Y, Int32 Size, UInt32 Color, Boolean IsBold = false);
}
