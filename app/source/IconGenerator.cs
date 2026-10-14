using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Reproducible, font-independent icon artwork. All glyphs are vector geometry.
// The contact sheet alone uses Segoe UI for its labels.
public static class IconGenerator
{
    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };
    private static readonly Color White = Color.FromArgb(240, 245, 250);
    private static readonly Color[] First = {
        Color.FromArgb(65, 229, 197), Color.FromArgb(193, 159, 255), Color.FromArgb(255, 131, 130)
    };
    private static readonly Color[] Second = {
        Color.FromArgb(246, 184, 75), Color.FromArgb(132, 81, 244), Color.FromArgb(218, 44, 76)
    };

    public static int Generate(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: IconGenerator <assets-directory> <contact-sheet.png>"); return 2; }
        string assets = Path.GetFullPath(args[0]);
        string preview = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(Path.GetDirectoryName(preview));
        string[] targets = { "codex", "deepseek", "haavk" };
        foreach (string target in targets)
        {
            for (int variant = 1; variant <= 3; variant++)
            {
                string name = target + "-" + variant.ToString("00");
                WriteIcon(Path.Combine(assets, name + ".ico"), target, variant);
                using (Bitmap image = Render(target, variant, 256)) image.Save(Path.Combine(assets, name + ".png"), ImageFormat.Png);
            }
        }
        WriteIcon(Path.Combine(assets, "codex.ico"), "codex", 0);
        WriteIcon(Path.Combine(assets, "deepseek.ico"), "deepseek", 0);
        WriteIcon(Path.Combine(assets, "launch.ico"), "haavk", 0);
        WriteContactSheet(preview);
        Console.WriteLine("Created 12 ICO files, 9 PNG previews, and the contact sheet.");
        Console.WriteLine("Validated every ICO: 16, 24, 32, 48, 64, 128, 256 px; 32-bit PNG frames; transparent corners.");
        return 0;
    }

    private static Bitmap Render(string target, int variant, int size)
    {
        // Oversampling keeps diagonal edges and tiny badges clean at desktop sizes.
        int renderSize = Math.Max(256, size * 4);
        using (Bitmap large = new Bitmap(renderSize, renderSize, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(large))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.ScaleTransform(renderSize / 256f, renderSize / 256f);
                DrawIcon(graphics, target, variant);
            }
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(large, new Rectangle(0, 0, size, size), 0, 0, renderSize, renderSize, GraphicsUnit.Pixel);
            }
            return result;
        }
    }

    private static void DrawIcon(Graphics graphics, string target, int variant)
    {
        int palette = Math.Max(0, variant - 1);
        Color first = First[palette];
        Color second = Second[palette];
        using (GraphicsPath tile = RoundedRectangle(8, 8, 240, 240, 49))
        using (LinearGradientBrush background = new LinearGradientBrush(new PointF(30, 10), new PointF(225, 248), Color.FromArgb(26, 35, 49), Color.FromArgb(10, 15, 24)))
        using (Pen border = new Pen(Color.FromArgb(130, first), 2))
        {
            graphics.FillPath(background, tile);
            graphics.DrawPath(border, tile);
        }

        PointF[] h = {
            new PointF(54, 41), new PointF(91, 41), new PointF(82, 88), new PointF(161, 88),
            new PointF(170, 41), new PointF(207, 41), new PointF(185, 153), new PointF(148, 153),
            new PointF(155, 119), new PointF(76, 119), new PointF(69, 153), new PointF(32, 153)
        };
        using (LinearGradientBrush mark = new LinearGradientBrush(new PointF(45, 48), new PointF(199, 157), first, second))
            graphics.FillPolygon(mark, h);

        using (Pen divider = new Pen(Color.FromArgb(45, first), 1.5f))
            graphics.DrawLine(divider, 43, 165, 214, 165);

        if (variant > 0)
        {
            DrawDigit(graphics, 0, 45, 177);
            DrawDigit(graphics, variant, 89, 177);
        }
        else
        {
            for (int index = 0; index < 3; index++)
            using (GraphicsPath chip = RoundedRectangle(45 + index * 32, 188, 23, 27, 6))
            using (SolidBrush brush = new SolidBrush(index == 0 ? First[0] : Second[index]))
                graphics.FillPath(brush, chip);
        }

        using (SolidBrush background = new SolidBrush(Color.FromArgb(35, 43, 57)))
        using (Pen outline = new Pen(Color.FromArgb(190, first), 2))
        {
            graphics.FillEllipse(background, 163, 172, 65, 65);
            graphics.DrawEllipse(outline, 163, 172, 65, 65);
        }
        using (Pen glyph = new Pen(White, 6.8f))
        {
            glyph.StartCap = LineCap.Round;
            glyph.EndCap = LineCap.Round;
            glyph.LineJoin = LineJoin.Round;
            if (target == "codex") graphics.DrawArc(glyph, 181, 187, 29, 34, 46, 268);
            else if (target == "deepseek")
            {
                using (GraphicsPath d = new GraphicsPath())
                {
                    d.AddLine(181, 187, 193, 187);
                    d.AddBezier(193, 187, 216, 187, 216, 221, 193, 221);
                    d.AddLine(193, 221, 181, 221);
                    d.CloseFigure();
                    graphics.DrawPath(glyph, d);
                }
            }
            else
            {
                using (SolidBrush play = new SolidBrush(White))
                    graphics.FillPolygon(play, new PointF[] { new PointF(185, 186), new PointF(185, 222), new PointF(213, 204) });
            }
        }
    }

    private static void DrawDigit(Graphics graphics, int digit, float x, float y)
    {
        GraphicsState saved = graphics.Save();
        graphics.TranslateTransform(x, y);
        using (SolidBrush brush = new SolidBrush(White))
        {
            if (digit == 0)
            {
                using (GraphicsPath zero = new GraphicsPath(FillMode.Alternate))
                using (GraphicsPath outside = RoundedRectangle(0, 0, 35, 50, 8))
                using (GraphicsPath inside = RoundedRectangle(10, 10, 15, 30, 3))
                {
                    zero.AddPath(outside, false);
                    zero.AddPath(inside, false);
                    graphics.FillPath(brush, zero);
                }
            }
            else
            {
                PointF[] points;
                if (digit == 1) points = Points(2, 11, 16, 0, 27, 0, 27, 40, 35, 40, 35, 50, 3, 50, 3, 40, 16, 40, 16, 13, 8, 19);
                else if (digit == 2) points = Points(0, 0, 35, 0, 35, 27, 10, 41, 35, 41, 35, 50, 0, 50, 0, 34, 25, 20, 25, 10, 0, 10);
                else points = Points(0, 0, 35, 0, 35, 50, 0, 50, 0, 40, 25, 40, 25, 30, 7, 30, 7, 20, 25, 20, 25, 10, 0, 10);
                graphics.FillPolygon(brush, points);
            }
        }
        graphics.Restore(saved);
    }

    private static PointF[] Points(params float[] coordinates)
    {
        PointF[] result = new PointF[coordinates.Length / 2];
        for (int i = 0; i < result.Length; i++) result[i] = new PointF(coordinates[i * 2], coordinates[i * 2 + 1]);
        return result;
    }

    private static GraphicsPath RoundedRectangle(float x, float y, float width, float height, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void WriteIcon(string path, string target, int variant)
    {
        List<byte[]> frames = new List<byte[]>();
        foreach (int size in Sizes)
        using (Bitmap image = Render(target, variant, size))
        using (MemoryStream buffer = new MemoryStream())
        {
            image.Save(buffer, ImageFormat.Png);
            frames.Add(buffer.ToArray());
        }
        using (BinaryWriter output = new BinaryWriter(File.Create(path)))
        {
            output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)Sizes.Length);
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                output.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                output.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                output.Write((byte)0); output.Write((byte)0);
                output.Write((ushort)1); output.Write((ushort)32);
                output.Write(frames[i].Length); output.Write(offset);
                offset += frames[i].Length;
            }
            foreach (byte[] frame in frames) output.Write(frame);
        }
        ValidateIcon(path);
    }

    private static void ValidateIcon(string path)
    {
        using (BinaryReader input = new BinaryReader(File.OpenRead(path)))
        {
            if (input.ReadUInt16() != 0 || input.ReadUInt16() != 1 || input.ReadUInt16() != Sizes.Length)
                throw new InvalidDataException("Invalid ICO header: " + path);
            for (int i = 0; i < Sizes.Length; i++)
            {
                input.BaseStream.Position = 6 + i * 16;
                int width = input.ReadByte(); if (width == 0) width = 256;
                int height = input.ReadByte(); if (height == 0) height = 256;
                input.ReadUInt16();
                int planes = input.ReadUInt16();
                int bits = input.ReadUInt16();
                int length = input.ReadInt32();
                int offset = input.ReadInt32();
                if (width != Sizes[i] || height != Sizes[i] || planes != 1 || bits != 32)
                    throw new InvalidDataException("Invalid ICO frame directory: " + path);
                input.BaseStream.Position = offset;
                byte[] data = input.ReadBytes(length);
                using (MemoryStream buffer = new MemoryStream(data))
                using (Bitmap frame = new Bitmap(buffer))
                {
                    if (frame.Width != width || frame.Height != height || frame.GetPixel(0, 0).A != 0 || frame.GetPixel(width - 1, height - 1).A != 0 || frame.GetPixel(width / 2, height / 2).A != 255)
                        throw new InvalidDataException("Invalid frame pixels: " + path);
                }
            }
        }
        using (Icon shellIcon = new Icon(path, 32, 32))
            if (shellIcon.Width != 32 || shellIcon.Height != 32) throw new InvalidDataException("Windows icon reader could not select 32px frame.");
    }

    private static void WriteContactSheet(string path)
    {
        using (Bitmap sheet = new Bitmap(1240, 1040, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font title = new Font("Segoe UI", 26, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font heading = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font caption = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel))
        using (SolidBrush bright = new SolidBrush(White))
        using (SolidBrush muted = new SolidBrush(Color.FromArgb(148, 161, 181)))
        {
            graphics.Clear(Color.FromArgb(14, 20, 30));
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawString("HAAVK / LAUNCH ICONS", title, bright, 40, 28);
            graphics.DrawString("Three versions. One visual family.  /  Native sizes: 16 · 24 · 32 · 48 · 64 · 128 · 256 px", caption, muted, 42, 70);
            string[] targets = { "codex", "deepseek", "haavk" };
            string[] labels = { "CODEX", "DEEPSEEK", "PREVIEW" };
            for (int column = 0; column < 3; column++) graphics.DrawString(labels[column], heading, bright, 218 + column * 340, 119);
            for (int variant = 1; variant <= 3; variant++)
            {
                int y = 168 + (variant - 1) * 230;
                using (SolidBrush swatch = new SolidBrush(First[variant - 1]))
                {
                    graphics.FillRectangle(swatch, 42, y + 8, 5, 42);
                    graphics.DrawString(variant.ToString("00"), title, bright, 61, y + 4);
                    graphics.DrawString(variant == 1 ? "TEAL / AMBER" : variant == 2 ? "PURPLE" : "RED", caption, muted, 61, y + 49);
                }
                for (int column = 0; column < 3; column++)
                {
                    int x = 216 + column * 340;
                    using (Bitmap icon = Render(targets[column], variant, 128)) graphics.DrawImageUnscaled(icon, x + 25, y);
                    int thumbX = x;
                    foreach (int size in new int[] { 16, 24, 32, 48 })
                    {
                        using (Bitmap icon = Render(targets[column], variant, size)) graphics.DrawImageUnscaled(icon, thumbX, y + 151 + (48 - size) / 2);
                        graphics.DrawString(size.ToString(), caption, muted, thumbX, y + 204);
                        thumbX += size + 18;
                    }
                }
            }
            using (Pen line = new Pen(Color.FromArgb(47, 60, 79))) graphics.DrawLine(line, 40, 877, 1200, 877);
            graphics.DrawString("COMMON ICONS", heading, bright, 42, 909);
            for (int column = 0; column < 3; column++)
            {
                int x = 290 + column * 290;
                using (Bitmap icon = Render(targets[column], 0, 64)) graphics.DrawImageUnscaled(icon, x, 901);
                graphics.DrawString(column == 2 ? "launch.ico" : targets[column] + ".ico", caption, muted, x + 80, 924);
            }
            graphics.DrawString("Vector artwork · transparent rounded tiles · C / D / play badges", caption, muted, 42, 993);
            sheet.Save(path, ImageFormat.Png);
        }
    }
}
