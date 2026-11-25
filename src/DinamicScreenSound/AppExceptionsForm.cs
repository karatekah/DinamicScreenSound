using System.Diagnostics;

namespace DinamicScreenSound;

internal sealed class AppExceptionsForm : Form
{
    private readonly ComboBox applications = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        AccessibleName = "Application",
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        TabIndex = 1
    };
    private readonly ComboBox output = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        AccessibleName = "Fixed output",
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        TabIndex = 4
    };
    private readonly IReadOnlyList<AudioEndpoint> outputs = CoreAudio.ListOutputs();
    private readonly Dictionary<string, string?> saved;
    private bool changingApplication;

    internal AppExceptionsForm(Dictionary<string, string?> exceptions)
    {
        saved = new Dictionary<string, string?>(exceptions, StringComparer.OrdinalIgnoreCase);
        Text = "DinamicScreenSound App Exceptions";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(588, 265);
        MinimumSize = new Size(418, 280);
        Icon = SystemIcons.Application;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int index = 0; index < 4; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var instructions = new Label
        {
            Text = "Choose an application and the audio output it should use on any display.",
            AutoSize = true,
            Margin = new Padding(3, 0, 3, 14)
        };
        layout.Controls.Add(instructions, 0, 0);
        layout.SetColumnSpan(instructions, 2);
        void FitInstructions() => instructions.MaximumSize = new Size(Math.Max(1, layout.ClientSize.Width - layout.Padding.Horizontal - 6), 0);
        layout.SizeChanged += (_, _) => FitInstructions();
        FitInstructions();
        layout.Controls.Add(new Label { Text = "&Application", AutoSize = true, TabIndex = 0 }, 0, 1);
        layout.Controls.Add(applications, 0, 2);
        var browse = new Button { Text = "&Browse…", AutoSize = true, TabIndex = 2 };
        browse.Click += (_, _) => BrowseApplications();
        layout.Controls.Add(browse, 1, 2);
        layout.Controls.Add(new Label { Text = "Fixed &output", AutoSize = true, TabIndex = 3, Margin = new Padding(3, 13, 3, 3) }, 0, 3);
        var outputRow = new Panel { Dock = DockStyle.Fill, TabIndex = 4 };
        output.Dock = DockStyle.Top;
        outputRow.Controls.Add(output);
        layout.Controls.Add(outputRow, 0, 4);
        layout.SetColumnSpan(outputRow, 2);

        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, FlowDirection = FlowDirection.RightToLeft, TabIndex = 5 };
        var cancel = new Button { Text = "&Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
        var save = new Button { Text = "&Save", AutoSize = true, TabIndex = 0 };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 2);
        AcceptButton = save;
        CancelButton = cancel;

        applications.SelectedIndexChanged += (_, _) => ShowSelectedOutput();
        output.SelectedIndexChanged += (_, _) => RecordSelectedOutput();
        PopulateApplications();
        Shown += (_, _) => (applications.Items.Count > 0 ? (Control)applications : browse).Select();
    }

    internal Dictionary<string, string?> Exceptions => saved;

    private void PopulateApplications()
    {
        var paths = new HashSet<string>(saved.Keys, StringComparer.OrdinalIgnoreCase);
        IEnumerable<int> pids = DesktopWindows.List().Select(window => window.ProcessId)
            .Concat(CoreAudio.ListActiveSessions().Select(session => (int)session.ProcessId));
        foreach (int pid in pids.Distinct())
        {
            if (pid == Environment.ProcessId)
            {
                continue;
            }

            using Process? process = DesktopRouter.OpenProcess(pid);
            if (process is not null && DesktopRouter.ReadPath(process) is { } path)
            {
                paths.Add(path);
            }
        }

        foreach (string path in paths.OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase))
        {
            applications.Items.Add(new ApplicationChoice(path));
        }

        if (applications.Items.Count > 0)
        {
            applications.SelectedIndex = 0;
        }
        else
        {
            ShowSelectedOutput();
        }
    }

    private void ShowSelectedOutput()
    {
        changingApplication = true;
        try
        {
            if (applications.SelectedItem is not ApplicationChoice application)
            {
                output.Enabled = false;
                return;
            }

            output.Enabled = true;
            output.AccessibleName = $"Fixed output for {Path.GetFileName(application.Path)}";
            bool assigned = saved.TryGetValue(application.Path, out string? endpoint);
            OutputChoice.Populate(output, outputs, "Follow display", assigned, endpoint);
        }
        finally
        {
            changingApplication = false;
        }
    }

    private void RecordSelectedOutput()
    {
        if (changingApplication || applications.SelectedItem is not ApplicationChoice application ||
            output.SelectedItem is not OutputChoice selection)
        {
            return;
        }

        if (selection.Assigned)
        {
            saved[application.Path] = selection.EndpointId;
        }
        else
        {
            saved.Remove(application.Path);
        }
    }

    private void BrowseApplications()
    {
        using var dialog = new OpenFileDialog { Title = "Choose an application", Filter = "Applications (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!string.Equals(Path.GetExtension(dialog.FileName), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Choose a Windows application (.exe).", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationChoice? choice = applications.Items.Cast<ApplicationChoice>().FirstOrDefault(item =>
            string.Equals(item.Path, dialog.FileName, StringComparison.OrdinalIgnoreCase));
        if (choice is null)
        {
            choice = new ApplicationChoice(dialog.FileName);
            applications.Items.Add(choice);
        }

        applications.SelectedItem = choice;
        output.Select();
    }

    private void Save()
    {
        try
        {
            OutputSettings.SaveExceptions(saved);
            DialogResult = DialogResult.OK;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not save the application exceptions. {error.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed record ApplicationChoice(string Path)
    {
        public override string ToString() => $"{System.IO.Path.GetFileName(Path)} ({System.IO.Path.GetDirectoryName(Path)})";
    }
}
