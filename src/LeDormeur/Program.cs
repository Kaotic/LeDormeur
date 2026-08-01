using LeDormeur.Localization;
using LeDormeur.Services;

namespace LeDormeur;

static class Program
{
    /// <summary>Named mutex ensuring only one process runs per user session.</summary>
    private const string MutexName = @"Local\LeDormeur.SingleInstance";

    /// <summary>Event used by a second launch to ask the first instance to show its window.</summary>
    internal const string ActivateEventName = @"Local\LeDormeur.SingleInstance.Activate";

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Dev-only flags (e.g. --dry-run). Not shown in the UI.
        DevOptions.Initialize(args);

        using var mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);
        if (!createdNew)
        {
            // Another instance is already running: ask it to restore, then exit.
            NotifyExistingInstanceOrWarn();
            return;
        }

        using var activateEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: ActivateEventName);

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        using var form = new Form1();
        using var cts = new CancellationTokenSource();
        var listener = StartActivateListener(form, activateEvent, cts.Token);

        try
        {
            Application.Run(form);
        }
        finally
        {
            cts.Cancel();
            try
            {
                // Unblock WaitOne so the listener can exit promptly.
                activateEvent.Set();
                listener.Join(millisecondsTimeout: 1000);
            }
            catch
            {
                // Ignore shutdown races.
            }
        }
    }

    private static Thread StartActivateListener(Form1 form, EventWaitHandle activateEvent, CancellationToken token)
    {
        var thread = new Thread(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!activateEvent.WaitOne(500))
                        continue;

                    if (token.IsCancellationRequested)
                        break;

                    if (form.IsDisposed)
                        break;

                    form.BeginInvoke(form.BringToFrontFromAnotherInstance);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    // Form may be closing; keep listening until cancelled.
                }
            }
        })
        {
            IsBackground = true,
            Name = "LeDormeur.SingleInstance.Activate"
        };

        thread.Start();
        return thread;
    }

    private static void NotifyExistingInstanceOrWarn()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(ActivateEventName);
            existing.Set();
            return;
        }
        catch
        {
            // Event missing (rare race) — fall through to a message box.
        }

        var settings = AppSettings.Load();
        var language = !string.IsNullOrWhiteSpace(settings.Language)
            ? AppLanguageExtensions.FromCode(settings.Language)
            : AppLanguageExtensions.DetectFromSystem();
        var t = UiStrings.For(language);

        MessageBox.Show(
            t.AlreadyRunningMessage,
            t.AlreadyRunningTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
