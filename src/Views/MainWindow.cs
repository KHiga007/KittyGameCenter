using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;
using KittyGameCenter.Controllers;

namespace KittyGameCenter.Views;
sealed class MainWindow : Form
{
    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; parameters.Style |= 0x00080000 | 0x00020000 | 0x00010000; return parameters; }
    }
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1, caption = 0x251621, text = 0xF8E9FF;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
    }
    readonly LibraryController controller;
    public MainWindow()
    {
        var web = new WebView2 { Dock = DockStyle.Fill };
        controller = new LibraryController(this, web);
        Text = "KittyGameCenter"; Width = 1280; Height = 850; MinimumSize = new(900, 650); StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(36, 20, 40);
        Padding = new Padding(5);
        Controls.Add(web);
        Controls.Add(new SakuraTitleBar(this));
        Shown += async (_, _) => { try { await controller.InitializeAsync(); } catch (Exception ex) { MessageBox.Show("No se pudo iniciar la interfaz WebView2: " + ex.Message, Text); } };
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x24 && m.LParam != IntPtr.Zero)
        {
            var monitor = Screen.FromHandle(m.HWnd);
            var limits = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
            limits.MaxPosition = new Point(monitor.WorkingArea.Left - monitor.Bounds.Left, monitor.WorkingArea.Top - monitor.Bounds.Top);
            limits.MaxSize = new Point(monitor.WorkingArea.Width, monitor.WorkingArea.Height);
            limits.MinTrackSize = new Point(MinimumSize.Width, MinimumSize.Height);
            Marshal.StructureToPtr(limits, m.LParam, false);
            m.Result = IntPtr.Zero;
            return;
        }
        if (m.Msg != 0x84 || WindowState == FormWindowState.Maximized || (int)m.Result != 1) return;
        var position = PointToClient(new Point(unchecked((short)((long)m.LParam & 0xffff)), unchecked((short)(((long)m.LParam >> 16) & 0xffff))));
        bool left = position.X < 5, right = position.X >= ClientSize.Width - 5, top = position.Y < 5, bottom = position.Y >= ClientSize.Height - 5;
        m.Result = (IntPtr)(top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 1);
    }
}
