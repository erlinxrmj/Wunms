using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Wumms
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(13, 14, 18);
        public static readonly Color Card = Color.FromArgb(22, 24, 31);
        public static readonly Color Panel2 = Color.FromArgb(34, 37, 47);
        public static readonly Color Hover = Color.FromArgb(46, 50, 63);
        public static readonly Color Line = Color.FromArgb(40, 44, 56);
        public static readonly Color Line2 = Color.FromArgb(70, 75, 92);
        public static readonly Color Track = Color.FromArgb(40, 43, 54);
        public static readonly Color Text = Color.FromArgb(238, 240, 246);
        public static readonly Color Sub = Color.FromArgb(138, 144, 163);
        public static readonly Color Accent = Color.FromArgb(255, 64, 129);
        public static readonly Color Accent2 = Color.FromArgb(130, 100, 255);
        public static readonly Color Off = Color.FromArgb(64, 68, 82);

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }

    public static class Gfx
    {
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2f);
            if (rad <= 0.5f || r.Width <= 1 || r.Height <= 1)
            {
                p.AddRectangle(r);
                return p;
            }
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    public abstract class SmoothControl : Control
    {
        protected SmoothControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        protected float S => DeviceDpi / 96f;
    }

    /// <summary>Abgerundete Karte als Hintergrund.</summary>
    public class Card : Panel
    {
        public Card()
        {
            DoubleBuffered = true;
            BackColor = Theme.Card;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            using var path = Gfx.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 14 * s);
            using var b = new SolidBrush(Theme.Card);
            using var pen = new Pen(Theme.Line);
            g.FillPath(b, path);
            g.DrawPath(pen, path);
        }
    }

    /// <summary>Ein/Aus-Schalter mit Animation.</summary>
    public class ToggleSwitch : SmoothControl
    {
        private bool on;
        private float anim;
        private readonly Timer timer = new Timer { Interval = 15 };

        public Color OnColor { get; set; } = Theme.Accent;
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            Cursor = Cursors.Hand;
            BackColor = Theme.Bg;
            timer.Tick += (s, e) =>
            {
                float target = on ? 1f : 0f;
                anim += Math.Sign(target - anim) * 0.14f;
                if (Math.Abs(target - anim) < 0.14f) { anim = target; timer.Stop(); }
                Invalidate();
            };
        }

        public bool Checked
        {
            get => on;
            set
            {
                if (on == value) return;
                on = value;
                timer.Start();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetSilent(bool value)
        {
            on = value;
            anim = value ? 1f : 0f;
            timer.Stop();
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(1, 1, Width - 2, Height - 2);
            float pad = Math.Max(3f, r.Height * 0.1f);
            using (var path = Gfx.Round(r, r.Height / 2))
            using (var b = new SolidBrush(Theme.Lerp(Theme.Off, OnColor, anim)))
                g.FillPath(b, path);
            float d = r.Height - 2 * pad;
            float x = r.X + pad + anim * (r.Width - 2 * pad - d);
            using var kb = new SolidBrush(Color.White);
            g.FillEllipse(kb, x, r.Y + pad, d, d);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Schieberegler (waagrecht oder senkrecht). Doppelklick = zurücksetzen.</summary>
    public class Slider : SmoothControl
    {
        private double value;
        private bool drag, hover;

        public bool Vertical { get; set; }
        public bool Bipolar { get; set; }
        public double Minimum { get; set; } = 0;
        public double Maximum { get; set; } = 100;
        public double Step { get; set; } = 0.5;
        public double DefaultValue { get; set; } = 0;
        public Color Accent { get; set; } = Theme.Accent;
        public bool Dimmed { get; set; }

        public event EventHandler ValueChanged;

        public Slider()
        {
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
        }

        public double Value
        {
            get => value;
            set
            {
                double v = Fix(value);
                if (Math.Abs(v - this.value) < 1e-9) return;
                this.value = v;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetValueSilent(double v)
        {
            value = Fix(v);
            Invalidate();
        }

        private double Fix(double v)
        {
            if (Step > 0) v = Math.Round(v / Step) * Step;
            return Math.Clamp(v, Minimum, Maximum);
        }

        private float Radius => (drag || hover ? 9f : 8f) * S;

        private void SetFromPoint(Point p)
        {
            float r = 9f * S;
            double t = Vertical
                ? (Height - r - p.Y) / Math.Max(1.0, Height - 2 * r)
                : (p.X - r) / Math.Max(1.0, Width - 2 * r);
            Value = Minimum + Math.Clamp(t, 0, 1) * (Maximum - Minimum);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            drag = true;
            Capture = true;
            Focus();
            SetFromPoint(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag) SetFromPoint(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            drag = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value += Math.Sign(e.Delta) * Step;
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            Value = DefaultValue;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float tw = 6f * S;
            float r = Radius;
            float m = 9f * S;
            double range = Math.Max(1e-9, Maximum - Minimum);
            float t = (float)((value - Minimum) / range);
            float t0 = Bipolar ? (float)((0 - Minimum) / range) : 0f;
            Color acc = Dimmed ? Theme.Line2 : Accent;

            using var trackBrush = new SolidBrush(Theme.Track);
            using var accBrush = new SolidBrush(acc);
            using var thumbBrush = new SolidBrush(Dimmed ? Theme.Sub : Color.White);
            using var ring = new Pen(acc, 2.5f * S);

            PointF thumb;
            if (Vertical)
            {
                float cx = Width / 2f, top = m, len = Height - 2 * m, bot = top + len;
                using (var p = Gfx.Round(new RectangleF(cx - tw / 2, top, tw, len), tw / 2)) g.FillPath(trackBrush, p);
                float y = bot - t * len, y0 = bot - t0 * len;
                float a = Math.Min(y, y0), b = Math.Max(y, y0);
                if (b - a > 1) using (var p = Gfx.Round(new RectangleF(cx - tw / 2, a, tw, b - a), tw / 2)) g.FillPath(accBrush, p);
                if (Bipolar) using (var pen = new Pen(Theme.Line2, 1.5f * S)) g.DrawLine(pen, cx - 9 * S, y0, cx + 9 * S, y0);
                thumb = new PointF(cx, y);
            }
            else
            {
                float cy = Height / 2f, left = m, len = Width - 2 * m;
                using (var p = Gfx.Round(new RectangleF(left, cy - tw / 2, len, tw), tw / 2)) g.FillPath(trackBrush, p);
                float x = left + t * len, x0 = left + t0 * len;
                float a = Math.Min(x, x0), b = Math.Max(x, x0);
                if (b - a > 1) using (var p = Gfx.Round(new RectangleF(a, cy - tw / 2, b - a, tw), tw / 2)) g.FillPath(accBrush, p);
                thumb = new PointF(x, cy);
            }

            g.FillEllipse(thumbBrush, thumb.X - r, thumb.Y - r, 2 * r, 2 * r);
            g.DrawEllipse(ring, thumb.X - r, thumb.Y - r, 2 * r, 2 * r);
        }
    }

    public class FlatButton : SmoothControl
    {
        private bool hover, down;

        public FlatButton()
        {
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
            Font = new Font("Segoe UI", 9f);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = !Enabled ? Theme.Card : down ? Theme.Line2 : hover ? Theme.Hover : Theme.Panel2;
            using (var p = Gfx.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 8 * S))
            using (var b = new SolidBrush(bg))
            using (var pen = new Pen(Theme.Line))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? Theme.Text : Theme.Off,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Zeigt die Klangkurve über alle Frequenzen.</summary>
    public class CurveView : SmoothControl
    {
        private const double FMin = 20, FMax = 20000, DbMin = -15, DbMax = 27;

        public Func<double, double> Response { get; set; }
        public bool Active { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);

            using (var path = Gfx.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 14 * S))
            using (var b = new SolidBrush(Theme.Card))
            using (var pen = new Pen(Theme.Line))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }

            var plot = new RectangleF(52 * S, 16 * S, Width - 70 * S, Height - 42 * S);
            float Y(double db) => plot.Bottom - (float)((db - DbMin) / (DbMax - DbMin)) * plot.Height;
            float X(double f) => plot.Left + (float)(Math.Log10(f / FMin) / Math.Log10(FMax / FMin)) * plot.Width;

            using var font = new Font("Segoe UI", 7.5f);
            using var grid = new Pen(Theme.Line, 1f);
            using var zero = new Pen(Theme.Line2, 1.2f);

            foreach (double db in new[] { -12.0, 0, 12, 24 })
            {
                float y = Y(db);
                g.DrawLine(db == 0 ? zero : grid, plot.Left, y, plot.Right, y);
                string t = (db > 0 ? "+" : "") + db.ToString("0") + " dB";
                TextRenderer.DrawText(g, t, font, new Rectangle(0, (int)(y - 8 * S), (int)(46 * S), (int)(16 * S)),
                    Theme.Sub, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            foreach (var (f, label) in new[] { (50.0, "50"), (100.0, "100"), (200.0, "200"), (500.0, "500"),
                                               (1000.0, "1k"), (2000.0, "2k"), (5000.0, "5k"), (10000.0, "10k") })
            {
                float x = X(f);
                g.DrawLine(grid, x, plot.Top, x, plot.Bottom);
                TextRenderer.DrawText(g, label, font, new Rectangle((int)(x - 20 * S), (int)(plot.Bottom + 4 * S), (int)(40 * S), (int)(16 * S)),
                    Theme.Sub, TextFormatFlags.HorizontalCenter);
            }

            if (Response == null) return;

            int n = Math.Max(2, (int)plot.Width);
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                double f = FMin * Math.Pow(FMax / FMin, i / (double)(n - 1));
                double db = Math.Clamp(Response(f), DbMin - 2, DbMax + 2);
                pts[i] = new PointF(plot.Left + i * plot.Width / (n - 1), Y(db));
            }

            Color c = Active ? Theme.Accent : Theme.Line2;
            g.SetClip(plot);
            using (var fill = new GraphicsPath())
            {
                fill.AddLines(pts);
                fill.AddLine(pts[n - 1].X, pts[n - 1].Y, plot.Right, Y(0));
                fill.AddLine(plot.Right, Y(0), plot.Left, Y(0));
                fill.CloseFigure();
                using var fb = new SolidBrush(Color.FromArgb(Active ? 70 : 35, c));
                g.FillPath(fb, fill);
            }
            using (var cp = new Pen(c, 2.5f * S) { LineJoin = LineJoin.Round })
                g.DrawLines(cp, pts);
            g.ResetClip();

            if (!Active)
            {
                using var f2 = new Font("Segoe UI", 9f);
                TextRenderer.DrawText(g, "AUS – du hörst gerade den Originalton (Vorschau der Kurve)", f2,
                    new Rectangle((int)plot.Left, (int)plot.Top, (int)plot.Width, (int)(20 * S)),
                    Theme.Sub, TextFormatFlags.Right | TextFormatFlags.Top);
            }
        }
    }
}
