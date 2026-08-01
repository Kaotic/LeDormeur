using LeDormeur.Localization;
using LeDormeur.Services;
using Microsoft.Win32;

namespace LeDormeur;

public partial class Form1 : Form
{
    private BrightnessService? _brightness;
    private AppSettings _settings = new();
    private UiStrings _t = UiStrings.French;
    private AppLanguage _language = AppLanguage.French;

    private DateTime _endTime;
    private TimeSpan _totalDuration;
    private byte _startLevel;
    private byte _targetLevel;
    private int _percentToRemove;
    private bool _running;
    private bool _loadingUi;
    private bool _pendingBrightnessRestore;
    private bool _warningShown;
    private SleepWarningForm? _warningForm;
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private ToolStripMenuItem? _trayOpenItem;
    private ToolStripMenuItem? _trayCancelItem;
    private ToolStripMenuItem? _trayExitItem;
    private bool _reallyExit;
    private bool _trayBalloonShown;
    private ToolStripMenuItem? _trayAutoModeItem;
    private StillThereForm? _stillThereForm;
    private DateOnly? _autoModeFiredDate;
    private bool _autoModePromptActive;

    /// <summary>Show the pre-sleep warning this long before the end.</summary>
    private static readonly TimeSpan WarningLeadTime = TimeSpan.FromMinutes(2);

    /// <summary>How long "Postpone" adds to the timer.</summary>
    private static readonly TimeSpan PostponeDuration = TimeSpan.FromMinutes(15);

    /// <summary>How long after the scheduled clock time auto mode may still fire.</summary>
    private static readonly TimeSpan AutoModeFireWindow = TimeSpan.FromMinutes(2);

    public Form1()
    {
        InitializeComponent();
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
                Icon = Icon.ExtractAssociatedIcon(path);
        }
        catch { }
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        _brightness = new BrightnessService();
        _settings = AppSettings.Load();

        InitTrayIcon();

        _loadingUi = true;
        PopulateLanguageCombo();

        var hadSavedLanguage = !string.IsNullOrWhiteSpace(_settings.Language);
        var language = hadSavedLanguage
            ? AppLanguageExtensions.FromCode(_settings.Language)
            : AppLanguageExtensions.DetectFromSystem();
        _settings.Language = language.ToCode();

        ApplySettingsToControls();
        ApplyLanguage(language, persist: false);
        _loadingUi = false;

        // Persist auto-detected language once (SaveSettingsFromUi is a no-op while loading).
        if (!hadSavedLanguage)
            _settings.Save();

        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

        UpdateModeLabel();
        UpdateDynamicExample();
        LayoutVersionLabel();
        UpdateTrayUi();
        UpdateAutoModeStatusLabel();
        lblBrightnessValue.Text = $"{trackBrightness.Value} %";
        if (!_settings.AutoModeEnabled)
            lblStatus.Text = _t.Ready;

        timerAutoMode.Start();
    }

    private void InitTrayIcon()
    {
        _trayOpenItem = new ToolStripMenuItem();
        _trayOpenItem.Click += (_, _) => RestoreFromTray();

        _trayCancelItem = new ToolStripMenuItem();
        _trayCancelItem.Click += (_, _) => CancelSession();

        _trayAutoModeItem = new ToolStripMenuItem();
        _trayAutoModeItem.Click += (_, _) => OpenAutoModeWindow();

        _trayExitItem = new ToolStripMenuItem();
        _trayExitItem.Click += (_, _) => ExitFromTray();

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add(_trayOpenItem);
        _trayMenu.Items.Add(_trayCancelItem);
        _trayMenu.Items.Add(_trayAutoModeItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(_trayExitItem);

        _trayIcon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = _trayMenu,
            Text = "Le Dormeur"
        };

        if (Icon is not null)
            _trayIcon.Icon = (Icon)Icon.Clone();
        else
        {
            try
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path))
                    _trayIcon.Icon = Icon.ExtractAssociatedIcon(path);
            }
            catch
            {
                _trayIcon.Icon = SystemIcons.Application;
            }
        }

        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                RestoreFromTray();
        };
    }

    private void Form1_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
            MinimizeToTray(showBalloon: true);
    }

    private void MinimizeToTray(bool showBalloon)
    {
        Hide();
        ShowInTaskbar = false;

        if (_trayIcon is not null)
            _trayIcon.Visible = true;

        if (showBalloon && !_trayBalloonShown && _trayIcon is not null)
        {
            _trayBalloonShown = true;
            try
            {
                _trayIcon.BalloonTipTitle = _t.TrayMinimizedBalloonTitle;
                _trayIcon.BalloonTipText = _t.TrayMinimizedBalloonText;
                _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                _trayIcon.ShowBalloonTip(2500);
            }
            catch
            {
                // Balloon tips can be disabled by the OS
            }
        }

        UpdateTrayUi();
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        UpdateTrayUi();
    }

    private void ExitFromTray()
    {
        if (_running)
        {
            var result = MessageBox.Show(
                _t.QuitMessage,
                _t.QuitTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
                return;

            StopTimer(restoreBrightness: true);
        }

        _reallyExit = true;
        Close();
    }

    private void UpdateTrayUi()
    {
        if (_trayIcon is null || _trayOpenItem is null || _trayCancelItem is null || _trayExitItem is null)
            return;

        _trayOpenItem.Text = _t.TrayOpen;
        _trayCancelItem.Text = _t.TrayCancel;
        _trayExitItem.Text = _t.TrayExit;
        _trayCancelItem.Enabled = _running;
        _trayCancelItem.Visible = true;

        if (_trayAutoModeItem is not null)
            _trayAutoModeItem.Text = _t.TrayAutoMode;

        // NotifyIcon.Text max length is 63 characters
        string tip;
        if (_running)
        {
            var remaining = _endTime - DateTime.Now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            var time = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            tip = string.Format(_t.TrayRunning, time);
        }
        else if (_settings.AutoModeEnabled)
        {
            tip = string.Format(
                _t.TrayAutoArmed,
                $"{_settings.AutoModeHour:D2}:{_settings.AutoModeMinute:D2}");
        }
        else
        {
            tip = _t.TrayIdle;
        }

        if (tip.Length > 63)
            tip = tip[..63];

        _trayIcon.Text = tip;
    }

    private void UpdateAutoModeStatusLabel()
    {
        if (_running || _autoModePromptActive)
            return;

        if (_settings.AutoModeEnabled)
        {
            lblStatus.Text = string.Format(
                _t.AutoModeArmedStatus,
                $"{_settings.AutoModeHour:D2}:{_settings.AutoModeMinute:D2}");
        }
    }

    private void LayoutVersionLabel()
    {
        lblVersion.Text = AppVersion.Display;
        lblVersion.AutoSize = true;
        lblVersion.BringToFront();

        const int marginRight = 12;
        const int marginBottom = 8;
        lblVersion.Left = ClientSize.Width - marginRight - lblVersion.PreferredWidth;
        lblVersion.Top = ClientSize.Height - marginBottom - lblVersion.PreferredHeight;
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume || !_pendingBrightnessRestore)
            return;

        // PowerModeChanged may fire off the UI thread
        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(() => RestoreBrightnessAfterSleepSession());
            return;
        }

        RestoreBrightnessAfterSleepSession();
    }

    private void PopulateLanguageCombo()
    {
        cmbLanguage.Items.Clear();
        foreach (var lang in AppLanguageExtensions.All)
            cmbLanguage.Items.Add(lang.DisplayName());
    }

    private void LayoutLanguageControls()
    {
        const int rightMargin = 16;
        const int gap = 10;

        cmbLanguage.Left = ClientSize.Width - rightMargin - cmbLanguage.Width;
        var labelWidth = TextRenderer.MeasureText(
            lblLanguage.Text,
            lblLanguage.Font,
            Size.Empty,
            TextFormatFlags.NoPadding).Width;
        lblLanguage.Left = cmbLanguage.Left - gap - labelWidth;
    }

    private void ApplySettingsToControls()
    {
        numHours.Value = Math.Clamp(_settings.Hours, (int)numHours.Minimum, (int)numHours.Maximum);
        numMinutes.Value = Math.Clamp(_settings.Minutes, (int)numMinutes.Minimum, (int)numMinutes.Maximum);
        trackBrightness.Value = Math.Clamp(_settings.BrightnessToRemove, trackBrightness.Minimum, trackBrightness.Maximum);

        var lang = AppLanguageExtensions.FromCode(_settings.Language);
        var index = Array.IndexOf(AppLanguageExtensions.All, lang);
        cmbLanguage.SelectedIndex = index >= 0 ? index : 0;
    }

    private void ApplyLanguage(AppLanguage language, bool persist)
    {
        _language = language;
        _t = UiStrings.For(language);

        Text = _t.WindowTitle;
        lblTitle.Text = _t.AppTitle;
        lblLanguage.Text = _t.LanguageLabel;
        LayoutLanguageControls();
        lblDuration.Text = _t.DurationLabel;
        lblHours.Text = _t.HoursUnit;
        lblMinutes.Text = _t.MinutesUnit;
        lblBrightness.Text = _t.BrightnessLabel;
        btnStart.Text = _t.Start;
        btnCancel.Text = _t.Cancel;
        btnAutoMode.Text = _t.AutoModeButton;

        if (!_running && !_autoModePromptActive)
        {
            if (_settings.AutoModeEnabled)
                UpdateAutoModeStatusLabel();
            else
                lblStatus.Text = _t.Ready;
        }

        UpdateModeLabel();
        UpdateDynamicExample();
        _warningForm?.ApplyLanguage(_t);
        UpdateTrayUi();

        if (persist)
        {
            _settings.Language = language.ToCode();
            SaveSettingsFromUi();
        }
    }

    private void UpdateModeLabel()
    {
        if (_brightness is null)
        {
            lblMode.Text = _t.ModeDetecting;
            return;
        }

        lblMode.Text = _brightness.UsesWmi ? _t.ModeWmi : _t.ModeGamma;
    }

    private void UpdateDynamicExample()
    {
        var hours = (int)numHours.Value;
        var minutes = (int)numMinutes.Value;
        var percent = trackBrightness.Value;
        var start = _brightness?.CurrentBrightness ?? _brightness?.StartBrightness ?? (byte)100;
        var end = Math.Max(0, start - percent);
        var durationText = FormatDuration(hours, minutes);

        lblBrightnessHint.Text = string.Format(
            _t.ExampleHint,
            percent,
            durationText,
            start,
            end);
    }

    private string FormatDuration(int hours, int minutes)
    {
        if (hours > 0 && minutes > 0)
            return string.Format(_t.DurationHoursAndMinutes, hours, minutes);
        if (hours > 0)
            return string.Format(_t.DurationHoursOnly, hours);
        if (minutes > 0)
            return string.Format(_t.DurationMinutesOnly, minutes);
        return _t.DurationZero;
    }

    private void SaveSettingsFromUi()
    {
        if (_loadingUi) return;

        _settings.Hours = (int)numHours.Value;
        _settings.Minutes = (int)numMinutes.Value;
        _settings.BrightnessToRemove = trackBrightness.Value;
        _settings.Language = _language.ToCode();
        _settings.Save();
    }

    private void settings_ValueChanged(object? sender, EventArgs e)
    {
        if (_loadingUi) return;
        UpdateDynamicExample();
        SaveSettingsFromUi();
    }

    private void trackBrightness_ValueChanged(object? sender, EventArgs e)
    {
        lblBrightnessValue.Text = $"{trackBrightness.Value} %";
        if (_loadingUi) return;
        UpdateDynamicExample();
        SaveSettingsFromUi();
    }

    private void cmbLanguage_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingUi || cmbLanguage.SelectedIndex < 0) return;
        if (cmbLanguage.SelectedIndex >= AppLanguageExtensions.All.Length) return;

        var language = AppLanguageExtensions.All[cmbLanguage.SelectedIndex];
        if (language == _language) return;
        ApplyLanguage(language, persist: true);
    }

    private void btnStart_Click(object? sender, EventArgs e)
    {
        var hours = (int)numHours.Value;
        var minutes = (int)numMinutes.Value;
        _totalDuration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);

        if (_totalDuration <= TimeSpan.Zero)
        {
            MessageBox.Show(
                _t.InvalidDurationMessage,
                _t.InvalidDurationTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        SaveSettingsFromUi();

        _percentToRemove = trackBrightness.Value;
        _brightness!.CaptureStartLevel();
        _startLevel = _brightness.StartBrightness;
        _targetLevel = (byte)Math.Max(0, _startLevel - _percentToRemove);
        _endTime = DateTime.Now + _totalDuration;
        _running = true;
        _warningShown = false;
        CloseWarningDialog();

        SetControlsEnabled(running: true);
        progressBar.Value = 0;
        UpdateBrightnessForProgress(0);
        UpdateStatusUi();
        UpdateTrayUi();

        timerTick.Start();
    }

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        CancelSession();
    }

    private void btnAutoMode_Click(object? sender, EventArgs e)
    {
        OpenAutoModeWindow();
    }

    private void OpenAutoModeWindow()
    {
        using var form = new AutoModeForm(_settings, _t);
        form.ShowDialog(this);
        // Settings already saved on OK inside the form
        UpdateAutoModeStatusLabel();
        UpdateTrayUi();
    }

    private void CancelSession()
    {
        CloseWarningDialog();
        StopTimer(restoreBrightness: true);
        lblStatus.Text = _t.Cancelled;
        lblRemaining.Text = string.Empty;
        progressBar.Value = 0;
        UpdateDynamicExample();
        UpdateAutoModeStatusLabel();
        UpdateTrayUi();
    }

    private void timerAutoMode_Tick(object? sender, EventArgs e)
    {
        TryTriggerAutoMode();
    }

    /// <summary>
    /// Fires once per day around the configured clock time if auto mode is enabled.
    /// </summary>
    private void TryTriggerAutoMode()
    {
        if (!_settings.AutoModeEnabled)
            return;
        if (_autoModePromptActive || _stillThereForm is { IsDisposed: false })
            return;
        if (_running)
            return;

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_autoModeFiredDate == today)
            return;

        var now = DateTime.Now;
        var scheduled = now.Date
            .AddHours(_settings.AutoModeHour)
            .AddMinutes(_settings.AutoModeMinute);

        // Only fire within a short window after the scheduled time
        if (now < scheduled || now > scheduled + AutoModeFireWindow)
            return;

        _autoModeFiredDate = today;
        StartStillTherePrompt();
    }

    private void StartStillTherePrompt()
    {
        if (_autoModePromptActive)
            return;

        _autoModePromptActive = true;
        CloseStillThereDialog();

        var timeout = TimeSpan.FromMinutes(Math.Max(1, _settings.AutoModeTimeoutMinutes));
        var dialog = new StillThereForm(_t, timeout);
        dialog.StillHereClicked += (_, _) => OnStillThereStillHere();
        dialog.TimedOut += (_, _) => OnStillThereSleep();
        dialog.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_stillThereForm, dialog))
                _stillThereForm = null;
        };

        _stillThereForm = dialog;

        try
        {
            RestoreFromTray();
            if (_trayIcon is not null)
            {
                _trayIcon.BalloonTipTitle = _t.StillThereBalloonTitle;
                _trayIcon.BalloonTipText = _t.StillThereBalloonText;
                _trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                _trayIcon.ShowBalloonTip(5000);
            }
        }
        catch
        {
            // ignore focus / balloon issues
        }

        dialog.Show(this);
        UpdateTrayUi();
    }

    private void OnStillThereStillHere()
    {
        _autoModePromptActive = false;
        CloseStillThereDialog();
        lblStatus.Text = _t.AutoModeStillHereStatus;
        UpdateTrayUi();
    }

    private void OnStillThereSleep()
    {
        _autoModePromptActive = false;
        CloseStillThereDialog();
        lblStatus.Text = _t.AutoModeSleepingStatus;
        Application.DoEvents();
        Thread.Sleep(400);
        PutPcToSleep(fromManualTimer: false);
    }

    private void CloseStillThereDialog()
    {
        if (_stillThereForm is null)
            return;

        var form = _stillThereForm;
        _stillThereForm = null;
        try
        {
            if (!form.IsDisposed)
                form.Close();
        }
        catch
        {
            // ignore
        }

        try
        {
            form.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    private void timerTick_Tick(object? sender, EventArgs e)
    {
        if (!_running) return;

        var remaining = _endTime - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            CloseWarningDialog();
            EnterSleep();
            return;
        }

        MaybeShowSleepWarning(remaining);

        if (_warningForm is { IsDisposed: false })
            _warningForm.UpdateCountdown(remaining, _t);

        var elapsed = _totalDuration - remaining;
        var progress = Math.Clamp(elapsed.TotalSeconds / _totalDuration.TotalSeconds, 0.0, 1.0);

        progressBar.Value = (int)(progress * 100);
        UpdateBrightnessForProgress(progress);
        UpdateStatusUi();
        UpdateTrayUi();
    }

    /// <summary>
    /// Opens the warning dialog when remaining time reaches the lead threshold.
    /// For short sessions (&lt;= 2 min), warn at half the duration (min 15 s).
    /// </summary>
    private void MaybeShowSleepWarning(TimeSpan remaining)
    {
        if (_warningShown || _warningForm is { IsDisposed: false })
            return;

        var threshold = GetWarningThreshold(_totalDuration);
        if (remaining > threshold)
            return;

        _warningShown = true;
        ShowSleepWarning(remaining);
    }

    private static TimeSpan GetWarningThreshold(TimeSpan totalDuration)
    {
        if (totalDuration > WarningLeadTime)
            return WarningLeadTime;

        // Short timer: warn for the second half, at least 15 seconds before the end
        var half = TimeSpan.FromSeconds(totalDuration.TotalSeconds / 2.0);
        var minLead = TimeSpan.FromSeconds(15);
        return half > minLead ? half : minLead;
    }

    private void ShowSleepWarning(TimeSpan remaining)
    {
        CloseWarningDialog();

        var dialog = new SleepWarningForm(_t);
        dialog.UpdateCountdown(remaining, _t);
        dialog.PostponeClicked += (_, _) => PostponeSession();
        dialog.CancelClicked += (_, _) => CancelSession();
        dialog.Dismissed += (_, _) =>
        {
            // User chose Continue or closed the window — keep counting down
            if (ReferenceEquals(_warningForm, dialog))
                _warningForm = null;
        };
        dialog.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_warningForm, dialog))
                _warningForm = null;
        };

        _warningForm = dialog;

        try
        {
            // Bring the app back if it was in the tray
            RestoreFromTray();
        }
        catch
        {
            // ignore focus issues
        }

        dialog.Show(this);
    }

    private void PostponeSession()
    {
        if (!_running) return;

        // Hold current brightness, re-aim toward the original target over (remaining + 15 min)
        var remaining = _endTime - DateTime.Now;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var current = _brightness?.CurrentBrightness ?? _startLevel;
        _startLevel = current;
        _percentToRemove = Math.Max(0, current - _targetLevel);
        _totalDuration = remaining + PostponeDuration;
        _endTime = DateTime.Now + _totalDuration;
        _warningShown = false;

        CloseWarningDialog();
        UpdateStatusUi();
        UpdateTrayUi();
        // Brief feedback (overwritten on next tick by running status)
        lblStatus.Text = _t.PostponedStatus;
    }

    private void CloseWarningDialog()
    {
        if (_warningForm is null)
            return;

        var form = _warningForm;
        _warningForm = null;
        try
        {
            if (!form.IsDisposed)
                form.Close();
        }
        catch
        {
            // ignore
        }

        try
        {
            form.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    private void EnterSleep()
    {
        UpdateBrightnessForProgress(1.0);
        progressBar.Value = 100;
        lblRemaining.Text = _t.TimeElapsedSleeping;
        lblStatus.Text = _t.SleepingPc;
        timerTick.Stop();
        _running = false;
        _warningShown = false;
        SetControlsEnabled(running: false);

        Application.DoEvents();
        Thread.Sleep(500);

        PutPcToSleep(fromManualTimer: true);
    }

    /// <summary>
    /// Puts the PC to sleep (or dry-run). When fromManualTimer, restores session brightness on wake.
    /// </summary>
    private void PutPcToSleep(bool fromManualTimer)
    {
        // Dev-only: full flow without putting the PC to sleep (--dry-run / launch profile).
        if (DevOptions.DryRun)
        {
            System.Diagnostics.Debug.WriteLine(
                "[LeDormeur] Dry-run: ForceSleep skipped.");
            if (fromManualTimer)
                _brightness?.RestoreStartLevel();
            lblStatus.Text = "Dry-run complete — sleep skipped (dev).";
            lblRemaining.Text = string.Empty;
            progressBar.Value = 0;
            UpdateDynamicExample();
            UpdateAutoModeStatusLabel();
            UpdateTrayUi();
            RestoreFromTray();
            return;
        }

        if (fromManualTimer)
            _pendingBrightnessRestore = true;

        var ok = PowerService.ForceSleep();
        if (!ok)
        {
            MessageBox.Show(
                _t.SleepFailedMessage,
                _t.SleepFailedTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            if (fromManualTimer)
                RestoreBrightnessAfterSleepSession(failedSleep: true);
            else
            {
                lblStatus.Text = _t.SleepFailedStatus;
                UpdateAutoModeStatusLabel();
            }
        }
        else if (fromManualTimer)
        {
            // SetSuspendState only returns after wake — restore here.
            RestoreBrightnessAfterSleepSession(failedSleep: false);
        }
        else
        {
            lblStatus.Text = _t.SleepOkStatus;
            UpdateAutoModeStatusLabel();
            UpdateTrayUi();
        }
    }

    private void UpdateBrightnessForProgress(double progress)
    {
        var target = _startLevel - (_percentToRemove * progress);
        var level = (byte)Math.Clamp((int)Math.Round(target), 0, 100);
        _brightness?.SetBrightnessPercent(level);
    }

    private void UpdateStatusUi()
    {
        var remaining = _endTime - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        var level = _brightness?.CurrentBrightness ?? 0;
        lblStatus.Text = string.Format(_t.RunningStatus, level, _startLevel, _targetLevel);
        lblRemaining.Text = string.Format(
            _t.Remaining,
            $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}");
    }

    private void StopTimer(bool restoreBrightness)
    {
        timerTick.Stop();
        _running = false;
        _pendingBrightnessRestore = false;
        _warningShown = false;
        CloseWarningDialog();
        SetControlsEnabled(running: false);

        if (restoreBrightness)
        {
            _brightness?.RestoreStartLevel();
        }
    }

    private void RestoreBrightnessAfterSleepSession(bool failedSleep = false)
    {
        if (!_pendingBrightnessRestore)
            return;

        _pendingBrightnessRestore = false;
        _brightness?.RestoreStartLevel();

        lblStatus.Text = failedSleep
            ? _t.SleepFailedStatusRestored
            : string.Format(_t.BrightnessRestoredAfterWake, _startLevel);

        lblRemaining.Text = string.Empty;
        progressBar.Value = 0;
        UpdateDynamicExample();
        UpdateTrayUi();
        RestoreFromTray();
    }

    private void SetControlsEnabled(bool running)
    {
        numHours.Enabled = !running;
        numMinutes.Enabled = !running;
        trackBrightness.Enabled = !running;
        cmbLanguage.Enabled = !running;
        btnStart.Enabled = !running;
        btnCancel.Enabled = running;
        UpdateTrayUi();
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // Close (X) → minimize to tray instead of quitting (Exit only from tray menu)
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MinimizeToTray(showBalloon: true);
            return;
        }

        if (_running)
        {
            var result = MessageBox.Show(
                _t.QuitMessage,
                _t.QuitTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                _reallyExit = false;
                return;
            }

            StopTimer(restoreBrightness: true);
        }
        else if (_pendingBrightnessRestore)
        {
            // Closing during/after sleep before restore completed
            RestoreBrightnessAfterSleepSession();
        }

        CloseWarningDialog();
        CloseStillThereDialog();
        timerAutoMode.Stop();
        DisposeTrayIcon();
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        SaveSettingsFromUi();
        _brightness?.Dispose();
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null)
            return;

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
        _trayMenu?.Dispose();
        _trayMenu = null;
    }
}
