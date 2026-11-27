namespace DinamicScreenSound;

internal sealed class FollowWindowForm : Form
{
    private readonly ListBox windows = new()
    {
        AccessibleName = "Open windows",
        Dock = DockStyle.Fill,
        HorizontalScrollbar = true,
        TabIndex = 1
    };

    internal FollowWindowForm(IReadOnlyDictionary<string, WindowIdentity> references,
        IReadOnlyDictionary<string, string?> exceptions)
    {
        Text = "Follow this window";
        Icon = AppIcons.Application;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 310);
        MinimumSize = new Size(440, 290);

        var instructions = new Label
        {
            Text = "Choose the window that should guide all audio from its app. The choice ends when the window closes. Fixed outputs take priority.",
            AutoSize = true,
            Margin = new Padding(3, 0, 3, 8)
        };
        var heading = new Label { Text = "Open &windows", AutoSize = true, TabIndex = 0 };
        var follow = new Button { Text = "&Follow window", AutoSize = true, TabIndex = 0 };
        var automatic = new Button { Text = "Use &automatic routing", AutoSize = true, TabIndex = 1 };
        var close = new Button { Text = "&Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 2 };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, TabIndex = 2 };
        buttons.Controls.AddRange([follow, automatic, close]);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(instructions, 0, 0);
        layout.Controls.Add(heading, 0, 1);
        layout.Controls.Add(windows, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        void FitInstructions() =>
            instructions.MaximumSize = new Size(Math.Max(1, layout.ClientSize.Width - layout.Padding.Horizontal - 6), 0);
        layout.SizeChanged += (_, _) => FitInstructions();
        FitInstructions();
        AcceptButton = follow;
        CancelButton = close;

        var displays = DisplayCatalog.GetDisplays().ToDictionary(display => display.DeviceName, StringComparer.OrdinalIgnoreCase);
        var paths = new Dictionary<int, string?>();
        foreach (DesktopWindow window in DesktopWindows.List(includeTitles: true))
        {
            if (!paths.TryGetValue(window.ProcessId, out string? path))
            {
                using var process = DesktopRouter.OpenProcess(window.ProcessId);
                path = process is null ? null : DesktopRouter.ReadPath(process);
                paths.Add(window.ProcessId, path);
            }

            if (path is null || string.IsNullOrWhiteSpace(window.Title))
            {
                continue;
            }

            string? display = window.DisplayName is { } name && displays.TryGetValue(name, out DesktopDisplay? found)
                ? found.Label : null;
            string status = exceptions.ContainsKey(path) ? "Fixed output" :
                references.TryGetValue(path, out WindowIdentity reference) && reference == window.Identity ? "Following" : "";
            windows.Items.Add(new WindowChoice(path, window, display, status));
        }

        void UpdateButtons()
        {
            var choice = windows.SelectedItem as WindowChoice;
            follow.Enabled = choice is not null && !exceptions.ContainsKey(choice.Path);
            automatic.Enabled = choice is not null && references.ContainsKey(choice.Path);
        }

        windows.SelectedIndexChanged += (_, _) => UpdateButtons();
        follow.Click += (_, _) => Apply(followWindow: true);
        automatic.Click += (_, _) => Apply(followWindow: false);
        if (windows.Items.Count == 0)
        {
            heading.Text = "No open windows found. Open the app you want to follow and try again.";
        }
        else
        {
            windows.SelectedIndex = 0;
        }

        UpdateButtons();
        Shown += (_, _) => (windows.Items.Count == 0 ? (Control)close : windows).Select();
    }

    internal string AppPath { get; private set; } = "";
    internal WindowIdentity? Reference { get; private set; }

    private void Apply(bool followWindow)
    {
        if (windows.SelectedItem is not WindowChoice choice)
        {
            return;
        }

        if (followWindow && !DesktopWindows.Exists(choice.Window.Identity))
        {
            MessageBox.Show(this, "That window has closed. Choose another window.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            windows.Items.Remove(choice);
            if (windows.Items.Count != 0)
            {
                windows.SelectedIndex = 0;
            }

            return;
        }

        AppPath = choice.Path;
        Reference = followWindow ? choice.Window.Identity : null;
        DialogResult = DialogResult.OK;
    }

    private sealed record WindowChoice(string Path, DesktopWindow Window, string? Display, string Status)
    {
        public override string ToString()
        {
            var parts = new[] { System.IO.Path.GetFileNameWithoutExtension(Path), Window.Title, Display, Status };
            return string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
        }
    }
}
