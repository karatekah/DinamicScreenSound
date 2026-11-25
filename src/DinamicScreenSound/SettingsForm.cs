using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed class SettingsForm : Form
{
    private readonly Dictionary<string, string?> saved;
    private readonly List<(DesktopDisplay Display, ComboBox Control)> choices = [];

    internal SettingsForm(Dictionary<string, string?> assignments)
    {
        saved = new Dictionary<string, string?>(assignments, StringComparer.OrdinalIgnoreCase);
        DesktopDisplay[] displays = DisplayCatalog.GetDisplays();
        Text = "DinamicScreenSound Settings";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(588, Math.Clamp(130 + 66 * displays.Length, 246, 520));
        MinimumSize = new Size(418, 240);
        Icon = SystemIcons.Application;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var instructions = new Label
        {
            Text = "Select an audio output for each display you want to control.",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 0, 3, 14)
        };
        layout.Controls.Add(instructions, 0, 0);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, TabIndex = 0 };
        layout.Controls.Add(scroll, 0, 1);
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(fields);
        IReadOnlyList<AudioEndpoint> outputs = CoreAudio.ListOutputs();
        foreach (DesktopDisplay display in displays)
        {
            AddDisplay(fields, display, outputs);
        }

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            TabIndex = 1
        };
        var cancel = new Button { Text = "&Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
        var save = new Button { Text = "&Save", AutoSize = true, TabIndex = 0 };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 2);
        AcceptButton = save;
        CancelButton = cancel;
        Shown += (_, _) => choices.FirstOrDefault().Control?.Select();
    }

    internal Dictionary<string, string?> Assignments => saved;

    private void AddDisplay(TableLayoutPanel fields, DesktopDisplay display, IReadOnlyList<AudioEndpoint> outputs)
    {
        int index = choices.Count;
        var label = new Label
        {
            Text = display.Label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabIndex = index * 2
        };
        var selection = new ComboBox
        {
            AccessibleName = $"Audio output for {display.Label}",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(3, 3, 3, 14),
            TabIndex = index * 2 + 1
        };
        bool hasAssignment = saved.TryGetValue(display.Key, out string? endpointId);
        OutputChoice.Populate(selection, outputs, "No assignment", hasAssignment, endpointId);

        fields.Controls.Add(label, 0, index * 2);
        fields.Controls.Add(selection, 0, index * 2 + 1);
        choices.Add((display, selection));
    }

    private void Save()
    {
        foreach (var (display, control) in choices)
        {
            var choice = (OutputChoice)control.SelectedItem!;
            if (choice.Assigned)
            {
                saved[display.Key] = choice.EndpointId;
            }
            else
            {
                saved.Remove(display.Key);
            }
        }

        try
        {
            OutputSettings.Save(saved);
            DialogResult = DialogResult.OK;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not save the output assignments. {error.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

}
