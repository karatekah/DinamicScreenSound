using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed class DisplayIdentifier : Form
{
    internal DisplayIdentifier(DesktopDisplay display)
    {
        Text = $"Display {display.Number}";
        AccessibleName = Text;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = SystemColors.Info;
        ForeColor = SystemColors.InfoText;
        var screen = display.Bounds;
        int height = Math.Clamp(screen.Height / 8, 72, 200);
        int inset = height / 4;
        Bounds = new Rectangle(screen.Left + inset, screen.Top + inset, height * 2, height);
        Number = display.Number;
    }

    private string Number { get; }

    internal void ShowOnDisplay(Form owner)
    {
        Owner = owner;
        // Form.Show can activate an owned window inside the Settings modal loop.
        const int swShowNoActivate = 4;
        ShowWindow(Handle, swShowNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExNoActivate = 0x08000000;
            const int wsExTopmost = 0x00000008;
            var parameters = base.CreateParams;
            parameters.ExStyle |= wsExNoActivate | wsExTopmost;
            return parameters;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var font = new Font(Font.FontFamily, ClientSize.Height * 0.65f, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, Number, font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, ForeColor, ButtonBorderStyle.Solid);
    }
}
