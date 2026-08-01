using LeDormeur.Localization;
using LeDormeur.Services;

namespace LeDormeur;

/// <summary>
/// Configuration window for automatic "still there?" schedule.
/// </summary>
public sealed class AutoModeForm : Form
{
    private readonly AppSettings _settings;
    private readonly CheckBox _chkEnabled;
    private readonly DateTimePicker _timePicker;
    private readonly NumericUpDown _numTimeout;
    private readonly Label _lblTitle;
    private readonly Label _lblTime;
    private readonly Label _lblTimeout;
    private readonly Label _lblTimeoutUnit;
    private readonly Label _lblHint;
    private readonly Label _lblNext;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private UiStrings _t;

    public AutoModeForm(AppSettings settings, UiStrings strings)
    {
        _settings = settings;
        _t = strings;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 380);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(0, 0, 0, 8);

        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
                Icon = Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            // ignore
        }

        _lblTitle = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            Location = new Point(24, 16),
            Size = new Size(372, 28)
        };

        // Text is set on the CheckBox itself so box + label stay aligned
        _chkEnabled = new CheckBox
        {
            AutoSize = true,
            Location = new Point(24, 58),
            Checked = settings.AutoModeEnabled,
            UseCompatibleTextRendering = false,
            TextAlign = ContentAlignment.MiddleLeft,
            CheckAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 2, 0, 0)
        };

        _lblTime = new Label
        {
            AutoSize = true,
            Location = new Point(24, 100)
        };

        _timePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Location = new Point(24, 126),
            Width = 100,
            Value = DateTime.Today.AddHours(settings.AutoModeHour).AddMinutes(settings.AutoModeMinute)
        };

        _lblTimeout = new Label
        {
            AutoSize = true,
            Location = new Point(24, 168)
        };

        _numTimeout = new NumericUpDown
        {
            Location = new Point(24, 194),
            Minimum = 1,
            Maximum = 120,
            Value = Math.Clamp(settings.AutoModeTimeoutMinutes, 1, 120),
            Width = 70
        };

        _lblTimeoutUnit = new Label
        {
            AutoSize = true,
            Location = new Point(104, 198)
        };

        _lblNext = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 90, 160),
            Location = new Point(24, 236),
            Size = new Size(372, 22)
        };

        _lblHint = new Label
        {
            AutoSize = false,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(24, 260),
            Size = new Size(372, 48)
        };

        // Keep buttons fully visible with bottom margin
        const int buttonHeight = 36;
        const int buttonBottomMargin = 20;
        var buttonTop = 380 - buttonBottomMargin - buttonHeight;

        _btnSave = new Button
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            Size = new Size(120, buttonHeight),
            Location = new Point(160, buttonTop)
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Size = new Size(100, buttonHeight),
            Location = new Point(290, buttonTop)
        };
        _btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _chkEnabled.CheckedChanged += (_, _) => RefreshNextLabel();
        _timePicker.ValueChanged += (_, _) => RefreshNextLabel();
        _numTimeout.ValueChanged += (_, _) => RefreshNextLabel();

        Controls.Add(_lblTitle);
        Controls.Add(_chkEnabled);
        Controls.Add(_lblTime);
        Controls.Add(_timePicker);
        Controls.Add(_lblTimeout);
        Controls.Add(_numTimeout);
        Controls.Add(_lblTimeoutUnit);
        Controls.Add(_lblNext);
        Controls.Add(_lblHint);
        Controls.Add(_btnSave);
        Controls.Add(_btnCancel);

        ApplyLanguage(strings);
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    public void ApplyLanguage(UiStrings strings)
    {
        _t = strings;
        Text = strings.AutoModeWindowTitle;
        _lblTitle.Text = strings.AutoModeTitle;
        _chkEnabled.Text = strings.AutoModeEnable;
        _lblTime.Text = strings.AutoModeTime;
        _lblTimeout.Text = strings.AutoModeTimeout;
        _lblTimeoutUnit.Text = strings.AutoModeTimeoutUnit;
        _lblHint.Text = strings.AutoModeHint;
        _btnSave.Text = strings.AutoModeSave;
        _btnCancel.Text = strings.AutoModeCancel;
        RefreshNextLabel();
    }

    private void RefreshNextLabel()
    {
        if (!_chkEnabled.Checked)
        {
            _lblNext.Text = _t.AutoModeDisabledStatus;
            return;
        }

        var time = _timePicker.Value.TimeOfDay;
        var timeout = (int)_numTimeout.Value;
        _lblNext.Text = string.Format(
            _t.AutoModeNextStatus,
            $"{time.Hours:D2}:{time.Minutes:D2}",
            timeout);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        _settings.AutoModeEnabled = _chkEnabled.Checked;
        _settings.AutoModeHour = _timePicker.Value.Hour;
        _settings.AutoModeMinute = _timePicker.Value.Minute;
        _settings.AutoModeTimeoutMinutes = (int)_numTimeout.Value;
        _settings.Save();

        DialogResult = DialogResult.OK;
        Close();
    }
}
