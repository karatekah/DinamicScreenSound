using System.Runtime.InteropServices;
using System.Text.Json;

namespace DinamicScreenSound;

internal sealed class TrayApplication : ApplicationContext
{
    private const string AppName = "DinamicScreenSound";
    private readonly DesktopRouter router = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 160 };
    private readonly ContextMenuStrip menu = new();
    private readonly ToolStripMenuItem toggleItem = new();
    private readonly NotifyIcon icon;
    private Dictionary<string, string?> assignments;
    private SettingsForm? settings;
    private bool disposed;

    private TrayApplication()
    {
        try
        {
            assignments = OutputSettings.Load();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            ShowWarning("Could not read the saved output assignments. Open Settings to configure the displays.");
            assignments = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        menu.Items.Add("&Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("E&xit", null, (_, _) => ExitThread());
        toggleItem.Click += (_, _) => ToggleRouting();
        icon = new NotifyIcon { Icon = SystemIcons.Application, ContextMenuStrip = menu };
        icon.DoubleClick += (_, _) => OpenSettings();
        timer.Tick += (_, _) => UpdateRouting();
        RefreshStatus();
        icon.Visible = true;
        if (assignments.Count == 0)
        {
            Application.Idle += OpenFirstTimeSettings;
        }
    }

    internal static int Run()
    {
        using var singleInstance = new Mutex(true, @"Local\DinamicScreenSound.Tray", out bool first);
        if (!first)
        {
            MessageBox.Show("DinamicScreenSound is running in the notification area.", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        using var application = new TrayApplication();
        Application.Run(application);
        return 0;
    }

    private void OpenFirstTimeSettings(object? sender, EventArgs args)
    {
        Application.Idle -= OpenFirstTimeSettings;
        OpenSettings();
    }

    private void OpenSettings()
    {
        if (settings is not null)
        {
            settings.Activate();
            return;
        }

        try
        {
            using var form = new SettingsForm(assignments);
            settings = form;
            if (form.ShowDialog() == DialogResult.OK)
            {
                assignments = form.Assignments;
                if (assignments.Count == 0)
                {
                    timer.Stop();
                }

                router.ResetSamples();
                RefreshStatus();
            }
        }
        catch (COMException)
        {
            ShowWarning("Could not list the audio outputs. Open Settings again after Windows reconnects the devices.");
        }
        finally
        {
            settings = null;
        }
    }

    private void ToggleRouting()
    {
        if (timer.Enabled)
        {
            timer.Stop();
        }
        else if (assignments.Count > 0)
        {
            router.ResetSamples();
            timer.Start();
        }
        else
        {
            OpenSettings();
        }

        RefreshStatus();
    }

    private void UpdateRouting()
    {
        try
        {
            SetTooltip(router.Update(assignments) ? "Routing active" : "Some apps could not be routed");
        }
        catch (COMException)
        {
            SetTooltip("Waiting for audio devices");
        }
    }

    private void RefreshStatus()
    {
        string label = timer.Enabled ? "&Pause routing" : "&Start routing";
        if (toggleItem.Text != label)
        {
            toggleItem.Text = label;
        }

        SetTooltip(timer.Enabled ? "Routing active" : "Routing paused");
    }

    private void SetTooltip(string status)
    {
        string text = $"{AppName} · {status}";
        if (icon.Text != text)
        {
            icon.Text = text;
        }
    }

    protected override void ExitThreadCore()
    {
        timer.Stop();
        settings?.Close();
        if (router.Restore() > 0)
        {
            ShowWarning("Some audio outputs could not be restored. Review the affected apps in the Windows volume mixer.");
        }

        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            Application.Idle -= OpenFirstTimeSettings;
            timer.Dispose();
            icon.Dispose();
            menu.Dispose();
            router.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void ShowWarning(string message) => MessageBox.Show(message, AppName,
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
