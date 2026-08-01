using LeDormeur.Localization;

namespace LeDormeur;

/// <summary>
/// "Are you still there?" prompt with countdown before automatic sleep.
/// </summary>
public sealed class StillThereForm : Form
{
    private readonly Label _lblTitle;
    private readonly Label _lblMessage;
    private readonly Label _lblCountdown;
    private readonly Button _btnHere;
    private readonly System.Windows.Forms.Timer _timer;
    private DateTime _deadline;
    private bool _closingByAction;

    public enum StillThereResult
    {
        StillHere,
        TimedOut
    }

    public StillThereResult Result { get; private set; } = StillThereResult.TimedOut;

    public event EventHandler? StillHereClicked;
    public event EventHandler? TimedOut;

    public StillThereForm(UiStrings strings, TimeSpan timeout)
    {
        Text = strings.StillThereTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ShowInTaskbar = true;
        ClientSize = new Size(460, 220);
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

        _deadline = DateTime.Now + timeout;

        _lblTitle = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            Location = new Point(20, 16),
            Size = new Size(420, 32),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = strings.StillThereTitle
        };

        _lblMessage = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI", 10F),
            Location = new Point(20, 56),
            Size = new Size(420, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = strings.StillThereMessage
        };

        _lblCountdown = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 40, 40),
            Location = new Point(20, 100),
            Size = new Size(420, 36),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "—"
        };

        _btnHere = new Button
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(16, 124, 16),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Text = strings.StillThereYes
        };
        _btnHere.FlatAppearance.BorderSize = 0;
        _btnHere.Click += (_, _) =>
        {
            Result = StillThereResult.StillHere;
            _closingByAction = true;
            StillHereClicked?.Invoke(this, EventArgs.Empty);
            Close();
        };

        Controls.Add(_lblTitle);
        Controls.Add(_lblMessage);
        Controls.Add(_lblCountdown);
        Controls.Add(_btnHere);

        LayoutButton();
        UpdateCountdownText(strings);

        _timer = new System.Windows.Forms.Timer { Interval = 250 };
        _timer.Tick += (_, _) =>
        {
            var remaining = _deadline - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                _timer.Stop();
                if (!_closingByAction)
                {
                    Result = StillThereResult.TimedOut;
                    _closingByAction = true;
                    TimedOut?.Invoke(this, EventArgs.Empty);
                    Close();
                }
                return;
            }

            UpdateCountdownText(strings);
        };
        _timer.Start();

        FormClosing += (_, _) =>
        {
            _timer.Stop();
            // Closing the window means the user is present — cancel auto-sleep for today
            if (!_closingByAction)
            {
                Result = StillThereResult.StillHere;
                StillHereClicked?.Invoke(this, EventArgs.Empty);
            }
        };

        Load += (_, _) => LayoutButton();
    }

    private void LayoutButton()
    {
        const int margin = 20;
        const int bottom = 20;
        const int height = 44;
        _btnHere.SetBounds(
            margin,
            ClientSize.Height - bottom - height,
            ClientSize.Width - margin * 2,
            height);
    }

    private void UpdateCountdownText(UiStrings strings)
    {
        var remaining = _deadline - DateTime.Now;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var mm = totalSeconds / 60;
        var ss = totalSeconds % 60;
        _lblCountdown.Text = string.Format(strings.StillThereCountdown, mm, ss);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }
}
