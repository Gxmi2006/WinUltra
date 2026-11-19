// WinUltra: the desktop clock. The weekday in big spaced letters, with the date and time underneath.
// It sits behind your windows and clicks pass straight through it.
// Right-click it on the desktop and choose Edit mode to drag it somewhere else or resize it
// (drag the corner handle or use the mouse wheel); press Esc or click away when you're done.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinUltra.DeskClock
{

static class Settings
{
    public static readonly Color TextColor = Color.White;
    public const string DayFont = "WinUltra.Anurati.otf";         // fonts built into the exe
    public const string DetailFont = "WinUltra.Montserrat.ttf";
    public const float DaySize = 104;
    public const float DayTracking = 0.28f;     // extra space between letters, as a share of the font size
    public const float DateSize = 30;
    public const float TimeSize = 28;
    public const float DetailTracking = 0.14f;
    public const float LineGap = 30;
    public const int ShadowAlpha = 15;          // 0 turns the shadow off
    public const float DefaultScale = 0.6f;
    public const float MinScale = 0.25f, MaxScale = 2f;
    public const string LayoutFile = "clock-layout.txt";
}

static class Native
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int size);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr dst, ref Point pos, ref Size size, IntPtr src, ref Point srcPos, int key, ref Blend blend, int flags);
    [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Blend { public byte Op, Flags, Alpha, Format; }

    public const int ExStyle = -20, Transparent = 0x20, NoActivate = 0x8000000;
}

// Size and position, kept in %LOCALAPPDATA%\WinUltra so they survive a restart.
class ClockLayout
{
    public float Scale = Settings.DefaultScale;
    public bool HasCentre;
    public PointF Centre;

    static string FilePath { get { return Path.Combine(WinUltra.App.DataDir, Settings.LayoutFile); } }

    public static ClockLayout Load()
    {
        var layout = new ClockLayout();
        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var parts = line.Split('=');
                if (parts.Length != 2) continue;
                float value;
                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
                if (parts[0] == "scale") layout.Scale = Math.Max(Settings.MinScale, Math.Min(Settings.MaxScale, value));
                if (parts[0] == "x") { layout.Centre.X = value; layout.HasCentre = true; }
                if (parts[0] == "y") layout.Centre.Y = value;
            }
        }
        catch (IOException) { }
        return layout;
    }

    public void Save()
    {
        var c = CultureInfo.InvariantCulture;
        var lines = HasCentre
            ? new[] { "scale=" + Scale.ToString(c), "x=" + Centre.X.ToString(c), "y=" + Centre.Y.ToString(c) }
            : new[] { "scale=" + Scale.ToString(c) };
        try { Directory.CreateDirectory(WinUltra.App.DataDir); File.WriteAllLines(FilePath, lines); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

class ClockForm : Form
{
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly StringBuilder className = new StringBuilder(32);
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly ClockLayout layout = ClockLayout.Load();
    int shownMinute = -1;
    bool wasOnDesktop, editing;
    Rectangle bounds;

    // Dragging and resizing in edit mode.
    enum Drag { None, Move, Resize }
    Drag drag;
    Point dragStart;
    Rectangle boundsAtStart;
    float scaleAtStart;

    public ClockForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        Text = "WinUltra Clock";

        menu.Items.Add("Edit mode", null, delegate { StartEdit(); });
        menu.Items.Add("Reset size and position", null, delegate
        {
            layout.Scale = Settings.DefaultScale;
            layout.HasCentre = false;
            layout.Save();
            Redraw(DateTime.Now, true);
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Hide clock", null, delegate { WinUltra.App.SetClock(false); });

        timer.Interval = 500;
        timer.Tick += delegate { Tick(); };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { shownMinute = -1; };
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | Native.Transparent | 0x80 | Native.NoActivate;    // layered, click-through, tool window, no activate
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Tick();
        timer.Start();
    }

    void Tick()
    {
        var now = DateTime.Now;
        if (now.Minute != shownMinute)
        {
            shownMinute = now.Minute;
            Redraw(now, true);
            PlaceInZOrder();
        }

        className.Length = 0;
        Native.GetClassName(Native.GetForegroundWindow(), className, className.Capacity);
        string name = className.ToString();
        bool onDesktop = name == "WorkerW" || name == "Progman";
        if (onDesktop != wasOnDesktop && !editing)
        {
            wasOnDesktop = onDesktop;
            // On the desktop the clock takes clicks on its own pixels, so it can be right-clicked.
            SetExStyle(Native.Transparent, !onDesktop);
            PlaceInZOrder();
        }
    }

    // Behind every window normally, but raised while the desktop has focus or while editing,
    // because Show Desktop (Win+D) lifts the desktop layer over anything at the bottom.
    void PlaceInZOrder()
    {
        // HWND_TOP = 0; HWND_BOTTOM = 1; SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE = 0x13
        Native.SetWindowPos(Handle, wasOnDesktop || editing ? IntPtr.Zero : new IntPtr(1), 0, 0, 0, 0, 0x13);
    }

    void SetExStyle(int flag, bool on)
    {
        int style = Native.GetWindowLong(Handle, Native.ExStyle);
        Native.SetWindowLong(Handle, Native.ExStyle, on ? style | flag : style & ~flag);
    }

    void StartEdit()
    {
        editing = true;
        SetExStyle(Native.Transparent, false);
        SetExStyle(Native.NoActivate, false);
        Redraw(DateTime.Now, false);
        PlaceInZOrder();
        Native.SetForegroundWindow(Handle);    // so Esc reaches the clock
    }

    void EndEdit()
    {
        if (!editing) return;
        editing = false;
        drag = Drag.None;
        layout.Save();
        SetExStyle(Native.NoActivate, true);
        SetExStyle(Native.Transparent, !wasOnDesktop);
        Redraw(DateTime.Now, true);
        PlaceInZOrder();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) EndEdit();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (!menu.Visible) EndEdit();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
        {
            menu.Items[0].Text = editing ? "Done editing" : "Edit mode";
            menu.Show(Cursor.Position);
        }
        if (e.Button == MouseButtons.Left && drag != Drag.None)
        {
            drag = Drag.None;
            layout.Save();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!editing || e.Button != MouseButtons.Left) return;
        dragStart = Cursor.Position;
        boundsAtStart = bounds;
        scaleAtStart = layout.Scale;
        int handle = Painter.HandleSize(bounds.Size);
        drag = e.X >= bounds.Width - handle && e.Y >= bounds.Height - handle ? Drag.Resize : Drag.Move;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!editing) return;
        int handle = Painter.HandleSize(bounds.Size);
        bool overHandle = e.X >= bounds.Width - handle && e.Y >= bounds.Height - handle;
        Cursor = drag == Drag.Resize || (drag == Drag.None && overHandle) ? Cursors.SizeNWSE : Cursors.SizeAll;

        var p = Cursor.Position;
        if (drag == Drag.Move)
        {
            layout.HasCentre = true;
            layout.Centre = new PointF(
                boundsAtStart.X + boundsAtStart.Width / 2f + (p.X - dragStart.X),
                boundsAtStart.Y + boundsAtStart.Height / 2f + (p.Y - dragStart.Y));
            MoveTo(new Point(boundsAtStart.X + p.X - dragStart.X, boundsAtStart.Y + p.Y - dragStart.Y));
        }
        else if (drag == Drag.Resize)
        {
            // Keep the top-left corner where it is and grow or shrink from the handle.
            float factor = Math.Max(0.1f, (boundsAtStart.Width + p.X - dragStart.X) / (float)boundsAtStart.Width);
            float scale = Math.Max(Settings.MinScale, Math.Min(Settings.MaxScale, scaleAtStart * factor));
            if (Math.Abs(scale - layout.Scale) < 0.005f) return;
            ResizeTo(scale, new Point(boundsAtStart.X, boundsAtStart.Y));
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!editing) return;
        float scale = layout.Scale * (e.Delta > 0 ? 1.06f : 1 / 1.06f);
        scale = Math.Max(Settings.MinScale, Math.Min(Settings.MaxScale, scale));
        layout.HasCentre = true;
        layout.Centre = new PointF(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
        layout.Scale = scale;
        Redraw(DateTime.Now, false);
        layout.Save();
    }

    void ResizeTo(float scale, Point topLeft)
    {
        layout.Scale = scale;
        using (var bitmap = Painter.Paint(DateTime.Now, scale, true))
        {
            layout.HasCentre = true;
            layout.Centre = new PointF(topLeft.X + bitmap.Width / 2f, topLeft.Y + bitmap.Height / 2f);
            Present(bitmap, topLeft);
        }
    }

    void MoveTo(Point topLeft)
    {
        Native.SetWindowPos(Handle, IntPtr.Zero, topLeft.X, topLeft.Y, 0, 0, 0x11);    // SWP_NOSIZE | SWP_NOACTIVATE
        bounds.Location = topLeft;
    }

    void Redraw(DateTime now, bool trimMemory)
    {
        using (var bitmap = Painter.Paint(now, layout.Scale, editing))
        {
            var screen = Screen.PrimaryScreen.Bounds;
            var centre = layout.HasCentre ? layout.Centre : new PointF(screen.X + screen.Width / 2f, screen.Y + screen.Height / 2f);
            var pos = new Point((int)(centre.X - bitmap.Width / 2f), (int)(centre.Y - bitmap.Height / 2f));

            // Keep it on screen if the resolution changed since it was placed.
            pos.X = Math.Max(screen.X, Math.Min(screen.Right - bitmap.Width, pos.X));
            pos.Y = Math.Max(screen.Y, Math.Min(screen.Bottom - bitmap.Height, pos.Y));
            Present(bitmap, pos);
        }

        if (trimMemory)
        {
            // Nothing changes for another minute, so hand the drawing memory back.
            GC.Collect();
            Native.SetProcessWorkingSetSize(Native.GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
        }
    }

    void Present(Bitmap bitmap, Point pos)
    {
        var size = bitmap.Size;
        var origin = Point.Empty;
        var blend = new Native.Blend { Alpha = 255, Format = 1 };    // AC_SRC_ALPHA

        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr old = Native.SelectObject(memDc, hBitmap);
        Native.UpdateLayeredWindow(Handle, screenDc, ref pos, ref size, memDc, ref origin, 0, ref blend, 2);    // ULW_ALPHA
        Native.SelectObject(memDc, old);
        Native.DeleteObject(hBitmap);
        Native.DeleteDC(memDc);
        Native.ReleaseDC(IntPtr.Zero, screenDc);
        bounds = new Rectangle(pos, size);
    }
}

static class Painter
{
    static readonly PrivateFontCollection fonts = new PrivateFontCollection();
    static readonly FontFamily dayFamily = Load(Settings.DayFont);
    static readonly FontFamily detailFamily = Load(Settings.DetailFont);
    static readonly string timeFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H") ? "HH:mm" : "h:mm tt";
    static readonly float dpi = GetDpiScale();

    static float GetDpiScale()
    {
        using (var screen = Graphics.FromHwnd(IntPtr.Zero)) return screen.DpiX / 96f;
    }

    // The font bytes stay in memory for the life of the program, as GDI+ requires.
    static FontFamily Load(string resource)
    {
        byte[] data;
        using (var stream = typeof(Painter).Assembly.GetManifestResourceStream(resource))
        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            data = ms.ToArray();
        }
        IntPtr memory = Marshal.AllocCoTaskMem(data.Length);
        Marshal.Copy(data, 0, memory, data.Length);
        using (var single = new PrivateFontCollection())
        {
            single.AddMemoryFont(memory, data.Length);
            string name = single.Families[0].Name;
            fonts.AddMemoryFont(memory, data.Length);
            foreach (var family in fonts.Families) if (family.Name == name) return family;
        }
        throw new FileNotFoundException("Font not loaded", resource);
    }

    public static int HandleSize(Size size)
    {
        int handle = (int)(Math.Min(size.Width, size.Height) * 0.12f);
        return Math.Max((int)(14 * dpi), Math.Min(handle, (int)(28 * dpi)));
    }

    // Adds text one letter at a time so each gap can be widened.
    static GraphicsPath SpacedText(Graphics g, string text, FontFamily family, float em, float tracking)
    {
        var path = new GraphicsPath();
        var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        using (var font = new Font(family, em, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            float x = 0;
            foreach (char c in text)
            {
                string s = c.ToString();
                path.AddString(s, family, 0, em, new PointF(x, 0), format);
                x += g.MeasureString(s, font, PointF.Empty, format).Width + tracking * em;
            }
        }
        return path;
    }

    public static Bitmap Paint(DateTime now, float userScale, bool editing)
    {
        float scale = dpi * userScale;

        var culture = CultureInfo.InvariantCulture;
        string dayText = now.ToString("dddd", culture).ToUpperInvariant();
        string dateText = now.ToString("d MMMM, yyyy.", culture).ToUpperInvariant();
        string timeText = "- " + now.ToString(timeFormat, culture).ToUpperInvariant() + " -";

        GraphicsPath day, date, time;
        using (var measure = Graphics.FromHwnd(IntPtr.Zero))
        {
            day = SpacedText(measure, dayText, dayFamily, Settings.DaySize * scale, Settings.DayTracking);
            date = SpacedText(measure, dateText, detailFamily, Settings.DateSize * scale, Settings.DetailTracking);
            time = SpacedText(measure, timeText, detailFamily, Settings.TimeSize * scale, Settings.DetailTracking);
        }

        var lines = new[] { day, date, time };
        float gap = Settings.LineGap * scale, pad = 30 * scale;
        float width = 0, height = gap * (lines.Length - 1);
        foreach (var line in lines)
        {
            var box = line.GetBounds();
            width = Math.Max(width, box.Width);
            height += box.Height;
        }

        var bitmap = new Bitmap((int)(width + 2 * pad), (int)(height + 2 * pad), PixelFormat.Format32bppArgb);
        using (var all = new GraphicsPath())
        {
            float top = pad;
            foreach (var line in lines)
            {
                var box = line.GetBounds();
                using (var m = new Matrix())
                {
                    m.Translate((bitmap.Width - box.Width) / 2 - box.X, top - box.Y);
                    line.Transform(m);
                }
                all.AddPath(line, false);
                top += box.Height + gap;
            }

            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (editing) DrawEditFrame(g, bitmap.Size);
                if (Settings.ShadowAlpha > 0)
                {
                    for (int w = 16; w >= 4; w -= 4)
                    {
                        using (var pen = new Pen(Color.FromArgb(Settings.ShadowAlpha, Color.Black), w * scale))
                        {
                            pen.LineJoin = LineJoin.Round;
                            g.DrawPath(pen, all);
                        }
                    }
                }
                using (var brush = new SolidBrush(Settings.TextColor)) g.FillPath(brush, all);
            }
        }
        foreach (var line in lines) line.Dispose();
        return bitmap;
    }

    // A faint fill so the whole box catches the mouse, a dashed outline and a corner handle.
    static void DrawEditFrame(Graphics g, Size size)
    {
        float unit = dpi;
        var box = new RectangleF(unit, unit, size.Width - 2 * unit, size.Height - 2 * unit);
        using (var fill = new SolidBrush(Color.FromArgb(28, 255, 255, 255))) g.FillRectangle(fill, box);
        using (var pen = new Pen(Color.FromArgb(170, 255, 255, 255), 1.5f * unit))
        {
            pen.DashStyle = DashStyle.Dash;
            g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
        }
        int handle = HandleSize(size);
        var corner = new[] {
            new PointF(size.Width - unit, size.Height - handle),
            new PointF(size.Width - unit, size.Height - unit),
            new PointF(size.Width - handle, size.Height - unit) };
        using (var brush = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillPolygon(brush, corner);
    }
}

static class ClockPreview
{
    // "WinUltra.exe --clock-preview out.png" saves one frame of the clock.
    public static void Save(string file, bool editing)
    {
        var layout = ClockLayout.Load();
        using (var frame = Painter.Paint(DateTime.Now, layout.Scale, editing)) frame.Save(file, ImageFormat.Png);
    }
}

}
