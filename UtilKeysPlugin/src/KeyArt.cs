namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Linq;

    // Draws every key: a muted label at the top, a big value, and a graph, bar or icon at the bottom. Nothing else.
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

        public const UInt32 Dim = 0x3A4653;

        // How many frames the cup's steam takes to loop.
        public const Int32 SteamFrames = 10;

        private const UInt32 Surface = 0x11161E;
        private const Int32 DesignSize = 116;
        private const String Semibold = "Helvetica Neue Medium";

        // The SDK turns one-colour SVGs white. This invisible second colour keeps ours.
        private const String KeepColor = "<circle r='0' fill='#000001' stroke='none'/>";

        // Draws a key. Each line is centred on its y, across its own width. Shapes are SVG drawn on the 116 grid, under the text.
        public static BitmapImage Key(PluginImageSize imageSize, String label, String shapes, params Line[] lines) =>
            Key(imageSize, label, shapes, null, lines);

        // The same, with a picture (a PNG) drawn over the shapes; picture.X, Y and Size are on the 116 grid.
        public static BitmapImage Key(PluginImageSize imageSize, String label, String shapes, (Byte[] Png, Int32 X, Int32 Y, Int32 Size)? picture, params Line[] lines)
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

                if (picture is (Byte[] png, Int32 x, Int32 y, Int32 size) && png != null)
                {
                    builder.DrawImage(png, Scaled(x, scale), Scaled(y, scale), Scaled(size, scale), Scaled(size, scale));
                }

                DrawLine(builder, new Line(label, 15, 16, Muted), scale);

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

        // A ring centred on x, y that fills clockwise from the top. fraction is 0 to 1; anything over 1 is a full ring.
        public static String Ring(Single x, Single y, Single radius, Double fraction, UInt32 color)
        {
            const Single Stroke = 3.5f;
            var track = FormattableString.Invariant($"<circle cx='{x}' cy='{y}' r='{radius}' fill='none' stroke='{Hex(Dim)}' stroke-width='{Stroke}'/>");
            var f = Math.Clamp(fraction, 0, 1);

            if (f >= 1)
            {
                return track + FormattableString.Invariant($"<circle cx='{x}' cy='{y}' r='{radius}' fill='none' stroke='{Hex(color)}' stroke-width='{Stroke}'/>");
            }

            if (f <= 0)
            {
                return track;
            }

            var angle = (2 * Math.PI * f) - (Math.PI / 2);
            return track + FormattableString.Invariant(
                $"<path d='M{x},{y - radius} A{radius},{radius} 0 {(f > 0.5 ? 1 : 0)} 1 {x + (radius * Math.Cos(angle)):0.#},{y + (radius * Math.Sin(angle)):0.#}' fill='none' stroke='{Hex(color)}' stroke-width='{Stroke}' stroke-linecap='round'/>");
        }

        // A microphone centred on x with its capsule's middle near y. Muted draws a slash through it.
        public static String Microphone(Single x, Single y, UInt32 color, Boolean muted)
        {
            String At(Single dx, Single dy) => FormattableString.Invariant($"{x + dx:0.#},{y + dy:0.#}");
            var line = $"fill='none' stroke='{Hex(color)}' stroke-width='2.8' stroke-linecap='round'";
            var mic = FormattableString.Invariant($"<rect x='{x - 6}' y='{y - 17}' width='12' height='20' rx='6' fill='{Hex(color)}'/>") +
                $"<path d='M{At(-11, -3)} a11,11 0 0 0 22,0 M{At(0, 8)} v7 M{At(-6, 15)} h12' {line}/>";

            return !muted ? mic : mic +
                $"<path d='M{At(-15, -17)} L{At(15, 15)}' stroke='{Hex(Surface)}' stroke-width='7' stroke-linecap='round'/>" +
                $"<path d='M{At(-15, -17)} L{At(15, 15)}' stroke='{Hex(color)}' stroke-width='3' stroke-linecap='round'/>";
        }

        // A battery outline 22 high with its top at y, filled to fraction. x is its left edge; the body is 48 wide.
        // A bolt marks charging.
        public static String Battery(Int32 x, Int32 y, Double fraction, UInt32 color, Boolean charging)
        {
            var width = 42 * (Single)Math.Clamp(fraction, 0, 1);
            var bolt = FormattableString.Invariant($"M{x + 28},{y + 1} L{x + 16},{y + 12} H{x + 23} L{x + 20},{y + 21} L{x + 32},{y + 10} H{x + 25} Z");
            return $"<rect x='{x}' y='{y}' width='48' height='22' rx='5' fill='none' stroke='{Hex(Muted)}' stroke-width='2'/>" +
                $"<rect x='{x + 49}' y='{y + 6}' width='4' height='10' rx='1.5' fill='{Hex(Muted)}'/>" +
                (width > 0 ? FormattableString.Invariant($"<rect x='{x + 3}' y='{y + 3}' width='{width:0.#}' height='16' rx='3' fill='{Hex(color)}'/>") : String.Empty) +
                (charging ? $"<path d='{bolt}' fill='{Hex(Text)}' stroke='{Hex(Surface)}' stroke-width='1.5' stroke-linejoin='round'/>" : String.Empty);
        }

        // The Wi-Fi fan: a dot and three arcs above it, centred on x with the dot at y. Unlit arcs are dim.
        public static String WifiSymbol(Single x, Single y, Int32 litArcs, UInt32 color)
        {
            var symbol = FormattableString.Invariant($"<circle cx='{x}' cy='{y}' r='2.8' fill='{Hex(color)}'/>");

            for (var arc = 0; arc < 3; arc++)
            {
                var radius = 6.5f * (arc + 1);
                var reach = radius * MathF.Sqrt(0.5f);
                symbol += FormattableString.Invariant(
                    $"<path d='M{x - reach:0.#},{y - reach:0.#} A{radius},{radius} 0 0 1 {x + reach:0.#},{y - reach:0.#}' fill='none' stroke='{Hex(arc < litArcs ? color : Dim)}' stroke-width='3.8' stroke-linecap='round'/>");
            }

            return symbol;
        }

        // A coffee cup on a saucer, x and y the top-left of its rim; the cup is 28 wide and 25 tall with the saucer.
        // With a frame, three wisps of steam rise and fade one after another; frames loop every SteamFrames.
        public static String Cup(Int32 x, Int32 y, UInt32 color, Int32? steamFrame)
        {
            String At(Single dx, Single dy) => FormattableString.Invariant($"{x + dx:0.#},{y + dy:0.#}");
            var cup = $"<path d='M{At(0, 0)} h28 v8 a12 12 0 0 1 -12 12 h-4 a12 12 0 0 1 -12 -12 z' fill='none' stroke='{Hex(color)}' stroke-width='3' stroke-linejoin='round'/>" +
                $"<path d='M{At(28, 3)} h3 a5.5 5.5 0 0 1 0 11 h-4' fill='none' stroke='{Hex(color)}' stroke-width='2.8' stroke-linecap='round'/>" +
                $"<path d='M{At(-3, 25)} h34' stroke='{Hex(color)}' stroke-width='2.6' stroke-linecap='round'/>";

            if (steamFrame is not Int32 frame)
            {
                return cup;
            }

            foreach (var (dx, offset) in new[] { (6, 0.0), (14, 1 / 3.0), (22, 2 / 3.0) })
            {
                var phase = ((Double)frame / SteamFrames + offset) % 1;
                cup += FormattableString.Invariant(
                    $"<path d='M{At(dx, -4 - (Single)(6 * phase))} q-2.5,-2.5 0,-5 q2.5,-2.5 0,-5' fill='none' stroke='{Hex(color)}' stroke-width='2.2' stroke-linecap='round' opacity='{Math.Sin(Math.PI * phase):0.##}'/>");
            }

            return cup;
        }

        // A plain terminal tile, size across with its top-left at x, y: stands in for a process that has no app icon.
        public static String TerminalTile(Int32 x, Int32 y, Int32 size)
        {
            var scale = FormattableString.Invariant($"{size / 34.0:0.###}");
            return $"<g transform='translate({x} {y}) scale({scale})'><rect x='1' y='1' width='32' height='32' rx='8' fill='#2A323D' stroke='{Hex(Dim)}' stroke-width='1.5'/>" +
                $"<path d='M9,11 l6,6 l-6,6' fill='none' stroke='{Hex(Muted)}' stroke-width='2.6' stroke-linecap='round' stroke-linejoin='round'/>" +
                $"<path d='M18,24 h8' stroke='{Hex(Muted)}' stroke-width='2.6' stroke-linecap='round'/></g>";
        }

        // An Ethernet socket centred on x, y: a box with a latch notch and four pins.
        public static String EthernetSocket(Single x, Single y, UInt32 color)
        {
            String At(Single dx, Single dy) => FormattableString.Invariant($"{x + dx:0.#},{y + dy:0.#}");
            var pins = String.Empty;

            foreach (var dx in new[] { -9f, -3f, 3f, 9f })
            {
                pins += $"<path d='M{At(dx, 5)} L{At(dx, 10)}' stroke='{Hex(color)}' stroke-width='2.2' stroke-linecap='round'/>";
            }

            return $"<path d='M{At(-17, 12)} L{At(-17, -7)} L{At(-10, -7)} L{At(-10, -12)} L{At(10, -12)} L{At(10, -7)} L{At(17, -7)} L{At(17, 12)} Z' " +
                $"fill='none' stroke='{Hex(color)}' stroke-width='3.5' stroke-linejoin='round'/>{pins}";
        }

        public static String Hex(UInt32 color) => $"#{color:X6}";

        private static void DrawLine(BitmapBuilder builder, Line line, Single scale)
        {
            var height = (Int32)(line.Size * 1.6);
            builder.DrawText(line.Text, Scaled(line.X, scale), Scaled(line.Y - (height / 2), scale), Scaled(line.Width, scale), Scaled(height, scale),
                BitmapColor.FromRgb(line.Color), Scaled(line.Size, scale), -1, -1, line.IsBold ? Semibold : null);
        }

        private static Int32 Scaled(Int32 value, Single scale) => (Int32)Math.Round(value * scale);
    }

    public readonly record struct Line(String Text, Int32 Y, Int32 Size, UInt32 Color, Boolean IsBold = false, Int32 X = 0, Int32 Width = 116);

    // Where things sit, the same on every key so values line up across the keypad: the label, the value on one
    // shared row, then the graphic centred in the zone below. Text draws a little above its y, so the y values
    // put each size's letters at the same height.
    public static class Layout
    {
        public const Int32 Value = 47;
        public const Int32 SmallValue = 46;
        public const Int32 GraphTop = 64;
        public const Int32 GraphBottom = 104;

        // The middle of the zone below the value, for anything shorter than a graph.
        public const Int32 ZoneMiddle = 84;
        public const Int32 BarTop = ZoneMiddle - 4;
    }
}
