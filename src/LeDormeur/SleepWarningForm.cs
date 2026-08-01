using LeDormeur.Localization;

namespace LeDormeur;

/// <summary>
/// Top-most pre-sleep warning with live countdown and Postpone / Cancel actions.
/// </summary>
public sealed class SleepWarningForm : Form
{
    private readonly Label _lblTitle;
    private readonly Label _lblCountdown;
    private readonly Label _lblHint;
    private readonly Button _btnPostpone;
    private readonly Button _btnCancel;
    private readonly Button _btnContinue;
    private bool _closingByAction;

    public enum WarningResult
    {
        /// <summary>User closed or chose to continue; countdown keeps running.</summary>
        Continue,
        /// <summary>Extend the timer.</summary>
        Postpone,
        /// <summary>Abort timer and restore brightness.</summary>
        Cancel
    }

    public WarningResult Result { get; private set; } = WarningResult.Continue;

    public event EventHandler? PostponeClicked;
    public event EventHandler? CancelClicked;
    public event EventHandler? Dismissed;

    public SleepWarningForm(UiStrings strings)
    {
        Text = strings.WarningTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ShowInTaskbar = true;
        ClientSize = new Size(480, 210);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F);

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
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            Location = new Point(20, 16),
            Size = new Size(440, 28),
            Text = strings.WarningTitle
        };

        _lblCountdown = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 40, 40),
            Location = new Point(20, 52),
            Size = new Size(440, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "—"
        };

        _lblHint = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.DimGray,
            Location = new Point(20, 100),
            Size = new Size(440, 40),
            Text = strings.WarningHint
        };

        _btnPostpone = new Button
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            Text = strings.WarningPostpone,
            UseCompatibleTextRendering = false
        };
        _btnPostpone.FlatAppearance.BorderSize = 0;
        _btnPostpone.Click += (_, _) =>
        {
            Result = WarningResult.Postpone;
            _closingByAction = true;
            PostponeClicked?.Invoke(this, EventArgs.Empty);
        };

        _btnCancel = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Text = strings.WarningCancel
        };
        _btnCancel.Click += (_, _) =>
        {
            Result = WarningResult.Cancel;
            _closingByAction = true;
            CancelClicked?.Invoke(this, EventArgs.Empty);
        };

        _btnContinue = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Text = strings.WarningContinue
        };
        _btnContinue.Click += (_, _) =>
        {
            Result = WarningResult.Continue;
            _closingByAction = true;
            Dismissed?.Invoke(this, EventArgs.Empty);
            Close();
        };

        Controls.Add(_lblTitle);
        Controls.Add(_lblCountdown);
        Controls.Add(_lblHint);
        Controls.Add(_btnPostpone);
        Controls.Add(_btnCancel);
        Controls.Add(_btnContinue);

        LayoutButtons();
        FormClosing += SleepWarningForm_FormClosing;
    }

    public void ApplyLanguage(UiStrings strings)
    {
        Text = strings.WarningTitle;
        _lblTitle.Text = strings.WarningTitle;
        _lblHint.Text = strings.WarningHint;
        _btnPostpone.Text = strings.WarningPostpone;
        _btnCancel.Text = strings.WarningCancel;
        _btnContinue.Text = strings.WarningContinue;
        LayoutButtons();
    }

    /// <summary>
    /// Sizes the three action buttons so the longest label (e.g. "Reporter (+15 min)") fits fully.
    /// </summary>
    private void LayoutButtons()
    {
        const int margin = 20;
        const int gap = 10;
        const int height = 36;
        const int top = 155;
        const int horizontalPadding = 24;

        var buttons = new[] { _btnPostpone, _btnCancel, _btnContinue };
        var widths = buttons
            .Select(b =>
            {
                var textWidth = TextRenderer.MeasureText(
                    b.Text,
                    b.Font,
                    Size.Empty,
                    TextFormatFlags.NoPadding).Width;
                return Math.Max(100, textWidth + horizontalPadding);
            })
            .ToArray();

        // Prefer equal width based on the widest button so the row stays balanced
        var unit = widths.Max();
        var total = unit * 3 + gap * 2 + margin * 2;
        if (total > ClientSize.Width)
        {
            ClientSize = new Size(total, ClientSize.Height);
            _lblTitle.Width = ClientSize.Width - margin * 2;
            _lblCountdown.Width = ClientSize.Width - margin * 2;
            _lblHint.Width = ClientSize.Width - margin * 2;
        }

        var x = margin;
        foreach (var button in buttons)
        {
            button.SetBounds(x, top, unit, height);
            x += unit + gap;
        }
    }

    /// <summary>
    /// Updates the countdown line. remaining is time until sleep.
    /// </summary>
    public void UpdateCountdown(TimeSpan remaining, UiStrings strings)
    {
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        _lblCountdown.Text = string.Format(strings.WarningCountdown, totalSeconds);
    }

    private void SleepWarningForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // Closing with the window X → keep the timer running
        if (!_closingByAction)
        {
            Result = WarningResult.Continue;
            Dismissed?.Invoke(this, EventArgs.Empty);
        }
    }
}
