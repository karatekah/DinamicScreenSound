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
    private Dictionary<string, string?> exceptions;
    private Form? openDialog;
    private bool disposed;

    private TrayApplication()
    {
        assignments = LoadRules(OutputSettings.Load, "Could not read the saved display outputs. Open Settings to configure the displays.");
        exceptions = LoadRules(OutputSettings.LoadExceptions, "Could not read the saved application exceptions. Open App Exceptions to configure them.");

        menu.Items.Add("&Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add("&App Exceptions…", null, (_, _) => OpenExceptions());
        menu.Items.Add("&Follow this window…", null, (_, _) => OpenFollowWindow());
        menu.Items.Add(toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("E&xit", null, (_, _) => ExitThread());
        toggleItem.Click += (_, _) => ToggleRouting();
        icon = new NotifyIcon { ContextMenuStrip = menu };
        icon.DoubleClick += (_, _) => OpenSettings();
        timer.Tick += (_, _) => UpdateRouting();
        RefreshStatus();
        icon.Visible = true;
        if (!HasRules)
        {
            Application.Idle += OpenFirstTimeSettings;
        }
    }

    private bool HasRules => assignments.Count != 0 || exceptions.Count != 0;

    private static Dictionary<string, string?> LoadRules(Func<Dictionary<string, string?>> load, string warning)
    {
        try
        {
            return load();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            ShowWarning(warning);
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
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

    private void OpenSettings() => OpenConfiguration(() => new SettingsForm(assignments), form => assignments = form.Assignments);

    private void OpenExceptions() => OpenConfiguration(() => new AppExceptionsForm(exceptions), form => exceptions = form.Exceptions);

    private void OpenFollowWindow() => OpenConfiguration(() => new FollowWindowForm(router.References, exceptions),
        form => router.SetReference(form.AppPath, form.Reference));

    private void OpenConfiguration<T>(Func<T> create, Action<T> accept) where T : Form
    {
        if (openDialog is not null)
        {
            openDialog.Activate();
            return;
        }

        try
        {
            using T form = create();
            openDialog = form;
            if (form.ShowDialog() == DialogResult.OK)
            {
                accept(form);
                if (!HasRules)
                {
                    timer.Stop();
                }

                router.ResetSamples();
                RefreshStatus();
            }
        }
        catch (COMException)
        {
            ShowWarning("Could not list audio devices. Try again after Windows reconnects the devices.");
        }
        finally
        {
            openDialog = null;
        }
    }

    private void ToggleRouting()
    {
        if (timer.Enabled)
        {
            timer.Stop();
        }
        else if (HasRules)
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
            var (complete, sharedAcrossDisplays) = router.Update(assignments, exceptions);
            SetTooltip((complete, sharedAcrossDisplays) switch
            {
                (false, _) => "Some apps could not be routed",
                (true, true) => "Shared app audio spans displays",
                _ => "Routing active"
            });
        }
        catch (COMException)
        {
            SetTooltip("Waiting for audio devices");
        }
    }

    private void RefreshStatus()
    {
        Icon statusIcon = timer.Enabled ? AppIcons.Application : AppIcons.Paused;
        if (icon.Icon != statusIcon)
        {
            icon.Icon = statusIcon;
        }

        string label = timer.Enabled ? "Pause &routing" : "Start &routing";
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
        openDialog?.Close();
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
