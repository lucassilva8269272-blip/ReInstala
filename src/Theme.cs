using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ReInstala
{
    internal static class Theme
    {
        public static float Scale = 1f;
        public static float FontScale = 1f;

        public static bool Dark;

        public static int Px(int value)
        {
            return (int)Math.Round(value * Scale);
        }

        public static int Px(float value)
        {
            return (int)Math.Round(value * Scale);
        }

        public static Color Background = Color.FromArgb(246, 247, 251);
        public static Color Surface = Color.White;
        public static Color Border = Color.FromArgb(227, 232, 240);
        public static Color BorderSoft = Color.FromArgb(237, 241, 247);
        public static Color Text = Color.FromArgb(23, 30, 46);
        public static Color Muted = Color.FromArgb(108, 120, 143);
        public static Color Accent = Color.FromArgb(83, 74, 229);
        public static Color AccentDark = Color.FromArgb(67, 58, 202);
        public static Color AccentSoft = Color.FromArgb(240, 239, 255);
        public static Color AccentDim = Color.FromArgb(195, 190, 250);
        public static Color Success = Color.FromArgb(20, 141, 96);
        public static Color SuccessSoft = Color.FromArgb(228, 246, 238);
        public static Color Danger = Color.FromArgb(205, 63, 82);
        public static Color DangerSoft = Color.FromArgb(253, 234, 237);
        public static Color Warn = Color.FromArgb(168, 112, 16);
        public static Color WarnSoft = Color.FromArgb(255, 246, 226);
        public static Color Info = Color.FromArgb(28, 100, 190);
        public static Color InfoSoft = Color.FromArgb(233, 242, 253);
        public static Color RowAlt = Color.FromArgb(251, 251, 253);
        public static Color Hover = Color.FromArgb(244, 245, 251);
        public static Color RowChecked = Color.FromArgb(249, 248, 255);
        public static Color HeaderRow = Color.FromArgb(249, 250, 252);
        public static Color ProgressTrack = Color.FromArgb(233, 237, 245);
        public static Color SegmentedBack = Color.FromArgb(235, 238, 245);
        public static Color CheckBorder = Color.FromArgb(192, 200, 215);
        public static Color CodeText = Color.FromArgb(120, 130, 150);
        public static Color ThumbShadow = Color.FromArgb(24, 25, 45, 70);
        public static Color ThumbBorder = Color.FromArgb(18, 25, 45, 70);

        public static void ApplyLight()
        {
            Dark = false;
            Background = Color.FromArgb(246, 247, 251);
            Surface = Color.White;
            Border = Color.FromArgb(227, 232, 240);
            BorderSoft = Color.FromArgb(237, 241, 247);
            Text = Color.FromArgb(23, 30, 46);
            Muted = Color.FromArgb(108, 120, 143);
            Accent = Color.FromArgb(83, 74, 229);
            AccentDark = Color.FromArgb(67, 58, 202);
            AccentSoft = Color.FromArgb(240, 239, 255);
            AccentDim = Color.FromArgb(195, 190, 250);
            Success = Color.FromArgb(20, 141, 96);
            SuccessSoft = Color.FromArgb(228, 246, 238);
            Danger = Color.FromArgb(205, 63, 82);
            DangerSoft = Color.FromArgb(253, 234, 237);
            Warn = Color.FromArgb(168, 112, 16);
            WarnSoft = Color.FromArgb(255, 246, 226);
            Info = Color.FromArgb(28, 100, 190);
            InfoSoft = Color.FromArgb(233, 242, 253);
            RowAlt = Color.FromArgb(251, 251, 253);
            Hover = Color.FromArgb(244, 245, 251);
            RowChecked = Color.FromArgb(249, 248, 255);
            HeaderRow = Color.FromArgb(249, 250, 252);
            ProgressTrack = Color.FromArgb(233, 237, 245);
            SegmentedBack = Color.FromArgb(235, 238, 245);
            CheckBorder = Color.FromArgb(192, 200, 215);
            CodeText = Color.FromArgb(120, 130, 150);
            ThumbShadow = Color.FromArgb(24, 25, 45, 70);
            ThumbBorder = Color.FromArgb(18, 25, 45, 70);
        }

        public static void ApplyDark()
        {
            Dark = true;
            Background = Color.FromArgb(19, 21, 27);
            Surface = Color.FromArgb(27, 30, 38);
            Border = Color.FromArgb(45, 50, 63);
            BorderSoft = Color.FromArgb(37, 42, 54);
            Text = Color.FromArgb(232, 235, 243);
            Muted = Color.FromArgb(152, 161, 179);
            Accent = Color.FromArgb(124, 116, 255);
            AccentDark = Color.FromArgb(96, 88, 226);
            AccentSoft = Color.FromArgb(37, 40, 64);
            AccentDim = Color.FromArgb(74, 70, 120);
            Success = Color.FromArgb(74, 214, 158);
            SuccessSoft = Color.FromArgb(21, 51, 42);
            Danger = Color.FromArgb(255, 128, 144);
            DangerSoft = Color.FromArgb(58, 33, 41);
            Warn = Color.FromArgb(232, 178, 90);
            WarnSoft = Color.FromArgb(58, 47, 25);
            Info = Color.FromArgb(130, 178, 255);
            InfoSoft = Color.FromArgb(27, 42, 66);
            RowAlt = Color.FromArgb(30, 34, 43);
            Hover = Color.FromArgb(37, 42, 57);
            RowChecked = Color.FromArgb(34, 36, 58);
            HeaderRow = Color.FromArgb(30, 34, 43);
            ProgressTrack = Color.FromArgb(38, 43, 54);
            SegmentedBack = Color.FromArgb(32, 36, 46);
            CheckBorder = Color.FromArgb(60, 67, 84);
            CodeText = Color.FromArgb(142, 153, 172);
            ThumbShadow = Color.FromArgb(80, 0, 0, 0);
            ThumbBorder = Color.FromArgb(70, 0, 0, 0);
        }

        public static Font Font(float size)
        {
            return new Font("Segoe UI", Math.Max(5f, size * FontScale), FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Bold(float size)
        {
            return new Font("Segoe UI", Math.Max(5f, size * FontScale), FontStyle.Bold, GraphicsUnit.Point);
        }

        public static Font Mono(float size)
        {
            return new Font("Consolas", Math.Max(5f, size * FontScale), FontStyle.Regular, GraphicsUnit.Point);
        }

        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Max(1f, radius * 2f);
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, RectangleF rect, float radius, Color color)
        {
            using (GraphicsPath path = RoundedRect(rect, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void StrokeRounded(Graphics g, RectangleF rect, float radius, Color color, float width)
        {
            RectangleF inset = new RectangleF(rect.X + width / 2f, rect.Y + width / 2f, rect.Width - width, rect.Height - width);
            using (GraphicsPath path = RoundedRect(inset, Math.Max(1f, radius - width / 2f)))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text ?? string.Empty, font, bounds, color, flags | TextFormatFlags.NoPrefix);
        }

        public static Color Mix(Color from, Color to, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * amount),
                (int)(from.G + (to.G - from.G) * amount),
                (int)(from.B + (to.B - from.B) * amount));
        }

        public static void DrawLogo(Graphics g, Rectangle bounds)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF rect = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            using (GraphicsPath path = RoundedRect(rect, bounds.Width * 0.26f))
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.FromArgb(112, 96, 248), Color.FromArgb(61, 48, 190), 45f))
            {
                g.FillPath(brush, path);
            }

            float w = bounds.Width;
            float cx = bounds.X + w / 2f;
            float top = bounds.Y + w * 0.22f;
            float mid = bounds.Y + w * 0.56f;
            float trayTop = bounds.Y + w * 0.58f;
            float trayBottom = bounds.Y + w * 0.76f;
            float trayLeft = bounds.X + w * 0.24f;
            float trayRight = bounds.X + w * 0.76f;
            float penWidth = Math.Max(1.6f, w * 0.075f);

            using (Pen pen = new Pen(Color.White, penWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                g.DrawLine(pen, cx, top, cx, mid);
                using (GraphicsPath head = new GraphicsPath())
                {
                    head.AddLine(cx - w * 0.13f, mid - w * 0.13f, cx, mid);
                    head.AddLine(cx + w * 0.13f, mid - w * 0.13f, cx, mid);
                    g.DrawPath(pen, head);
                }

                using (GraphicsPath tray = new GraphicsPath())
                {
                    float r = w * 0.07f;
                    tray.AddLine(trayLeft, trayTop, trayLeft, trayBottom - r);
                    tray.AddArc(trayLeft, trayBottom - 2 * r, 2 * r, 2 * r, 180f, 90f);
                    tray.AddLine(trayLeft + r, trayBottom, trayRight - r, trayBottom);
                    tray.AddArc(trayRight - 2 * r, trayBottom - 2 * r, 2 * r, 2 * r, 90f, 90f);
                    tray.AddLine(trayRight, trayBottom - r, trayRight, trayTop);
                    g.DrawPath(pen, tray);
                }
            }
        }
    }
}
