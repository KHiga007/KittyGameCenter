using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KittyGameCenter.Views;
sealed class SakuraTitleBar : Panel
{
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    readonly MainWindow window;
    readonly Button maximize;
    public SakuraTitleBar(MainWindow owner)
    {
        window = owner;
        Dock = DockStyle.Top; Height = 42; DoubleBuffered = true;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 138, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        var minimize = WindowButton("\uE921", "Minimizar");
        maximize = WindowButton("\uE922", "Maximizar");
        var close = WindowButton("\uE8BB", "Cerrar");
        minimize.Click += (_, _) => window.WindowState = FormWindowState.Minimized;
        maximize.Click += (_, _) => ToggleMaximize();
        close.Click += (_, _) => window.Close();
        close.FlatAppearance.MouseOverBackColor = Color.FromArgb(158, 66, 105);
        actions.Controls.AddRange([minimize, maximize, close]); Controls.Add(actions);
        var title = new Label { Text = "KittyGameCenter", ForeColor = Color.FromArgb(255, 226, 241), Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Location = new Point(40, 11), BackColor = Color.Transparent };
        Controls.Add(title);
        var edition = new Label { Text = "SAKURA EDITION", ForeColor = Color.FromArgb(199, 147, 178), Font = new Font("Segoe UI", 7.5f), AutoSize = true, Location = new Point(165, 14), BackColor = Color.Transparent };
        Controls.Add(edition);
        MouseDown += Drag; title.MouseDown += Drag; edition.MouseDown += Drag;
        DoubleClick += (_, _) => ToggleMaximize(); title.DoubleClick += (_, _) => ToggleMaximize(); edition.DoubleClick += (_, _) => ToggleMaximize();
        window.Resize += (_, _) => { maximize.Text = window.WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922"; maximize.AccessibleName = window.WindowState == FormWindowState.Maximized ? "Restaurar" : "Maximizar"; };
    }
    Button WindowButton(string glyph, string name)
    {
        var button = new Button { Text = glyph, AccessibleName = name, Width = 46, Height = 42, Margin = Padding.Empty, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.FromArgb(250, 213, 233), Font = new Font("Segoe MDL2 Assets", 10), Cursor = Cursors.Hand, TabStop = true };
        button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = Color.FromArgb(82, 42, 70); button.FlatAppearance.MouseDownBackColor = Color.FromArgb(114, 59, 91);
        return button;
    }
    void ToggleMaximize()
    {
        window.WindowState = window.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }
    void Drag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || e.Clicks > 1) return;
        if (window.WindowState == FormWindowState.Maximized)
        {
            var ratio = (double)(Cursor.Position.X - window.Left) / Math.Max(window.Width, 1);
            window.WindowState = FormWindowState.Normal;
            window.Location = new Point(Cursor.Position.X - (int)(window.Width * ratio), Cursor.Position.Y - 20);
        }
        ReleaseCapture(); SendMessage(window.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var gradient = new LinearGradientBrush(ClientRectangle, Color.FromArgb(42, 22, 43), Color.FromArgb(27, 15, 35), 0f);
        e.Graphics.FillRectangle(gradient, ClientRectangle);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var petals = new SolidBrush(Color.FromArgb(244, 172, 207));
        for (int i = 0; i < 5; i++) { double angle = i * Math.PI * 2 / 5 - Math.PI / 2; e.Graphics.FillEllipse(petals, 19 + (float)Math.Cos(angle) * 5 - 3, 21 + (float)Math.Sin(angle) * 5 - 3, 6, 6); }
        using var center = new SolidBrush(Color.FromArgb(255, 225, 191)); e.Graphics.FillEllipse(center, 17, 19, 4, 4);
    }
}
