using LeDormeur.Localization;
using LeDormeur.Services;

namespace LeDormeur;

/// <summary>
/// Power / wake options: timers, Wake-on-LAN, last wake source, wake-armed devices.
/// </summary>
public sealed class OptionsForm : Form
{
    private const int FormWidth = 540;
    private const int FormHeight = 640;
    private const int ContentLeft = 24;
    private const int ContentWidth = 492;

    private readonly Label _lblTitle;
    private readonly Label _lblPlan;
    private readonly Panel _rowTimers;
    private readonly Label _lblWakeTimers;
    private readonly RadioButton _radioTimersOn;
    private readonly RadioButton _radioTimersOff;
    private readonly Panel _rowWol;
    private readonly Label _lblWol;
    private readonly RadioButton _radioWolOn;
    private readonly RadioButton _radioWolOff;
    private readonly Label _lblLastWakeSection;
    private readonly Label _lblLastWake;
    private readonly Button _btnRefreshWake;
    private readonly Label _lblDevicesSection;
    private readonly CheckedListBox _lstDevices;
    private readonly Label _lblDevicesHint;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly ToolTip _tip;

    private UiStrings _t;
    private bool _originalTimersAllowed;
    private bool _wolSupported;
    private bool _originalWolEnabled;
    private string _schemeName = string.Empty;
    private readonly Dictionary<string, bool> _originalDevices = new(StringComparer.OrdinalIgnoreCase);

    public OptionsForm(UiStrings strings)
    {
        _t = strings;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(FormWidth, FormHeight);
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

        _tip = new ToolTip { ShowAlways = true };

        _lblTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            Location = new Point(ContentLeft, 16)
        };

        _lblPlan = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 90, 160),
            Location = new Point(ContentLeft, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        _rowTimers = CreateToggleRow(80, out _lblWakeTimers, out _radioTimersOn, out _radioTimersOff);
        _rowWol = CreateToggleRow(112, out _lblWol, out _radioWolOn, out _radioWolOff);
        var sep1 = CreateSeparator(146);

        _lblLastWakeSection = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Location = new Point(ContentLeft, 156),
            Size = new Size(360, 22)
        };

        _btnRefreshWake = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 28),
            Location = new Point(ContentLeft + ContentWidth - 120, 152)
        };
        _btnRefreshWake.Click += (_, _) => RefreshLastWake();

        _lblLastWake = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(0, 90, 160),
            Location = new Point(ContentLeft, 186),
            Size = new Size(ContentWidth, 36)
        };

        var sep2 = CreateSeparator(226);

        _lblDevicesSection = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Location = new Point(ContentLeft, 238),
            Size = new Size(ContentWidth, 22)
        };

        _lstDevices = new CheckedListBox
        {
            Location = new Point(ContentLeft, 264),
            Size = new Size(ContentWidth, 262),
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Font = new Font("Segoe UI", 9F)
        };

        _lblDevicesHint = new Label
        {
            AutoSize = false,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(ContentLeft, 532),
            Size = new Size(ContentWidth, 32)
        };

        const int buttonHeight = 36;
        const int buttonBottomMargin = 20;
        var buttonTop = FormHeight - buttonBottomMargin - buttonHeight;

        _btnSave = new Button
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            Size = new Size(120, buttonHeight),
            Location = new Point(FormWidth - 24 - 100 - 16 - 120, buttonTop)
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Size = new Size(100, buttonHeight),
            Location = new Point(FormWidth - 24 - 100, buttonTop)
        };
        _btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(_lblTitle);
        Controls.Add(_lblPlan);
        Controls.Add(_rowTimers);
        Controls.Add(_rowWol);
        Controls.Add(sep1);
        Controls.Add(_lblLastWakeSection);
        Controls.Add(_btnRefreshWake);
        Controls.Add(_lblLastWake);
        Controls.Add(sep2);
        Controls.Add(_lblDevicesSection);
        Controls.Add(_lstDevices);
        Controls.Add(_lblDevicesHint);
        Controls.Add(_btnSave);
        Controls.Add(_btnCancel);

        ApplyLanguage(strings);
        LoadCurrentSettings();
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private static Panel CreateSeparator(int y) => new()
    {
        Location = new Point(ContentLeft, y),
        Size = new Size(ContentWidth, 1),
        BackColor = Color.FromArgb(228, 228, 228)
    };

    private static Panel CreateToggleRow(int y, out Label label, out RadioButton radioOn, out RadioButton radioOff)
    {
        var panel = new Panel
        {
            Location = new Point(ContentLeft, y),
            Size = new Size(ContentWidth, 28),
            BackColor = Color.White
        };

        label = new Label
        {
            AutoSize = false,
            Location = new Point(0, 4),
            Size = new Size(300, 22),
            AutoEllipsis = true
        };

        radioOn = new RadioButton
        {
            AutoSize = true,
            Location = new Point(330, 4)
        };

        radioOff = new RadioButton
        {
            AutoSize = true,
            Location = new Point(400, 4)
        };

        panel.Controls.Add(label);
        panel.Controls.Add(radioOn);
        panel.Controls.Add(radioOff);
        return panel;
    }

    public void ApplyLanguage(UiStrings strings)
    {
        _t = strings;
        Text = strings.OptionsWindowTitle;
        _lblTitle.Text = strings.OptionsTitle;
        _lblWakeTimers.Text = strings.OptionsWakeTimersLabel;
        _lblWol.Text = strings.OptionsWakeOnLanLabel;
        _radioTimersOn.Text = strings.OptionsOn;
        _radioTimersOff.Text = strings.OptionsOff;
        _radioWolOn.Text = strings.OptionsOn;
        _radioWolOff.Text = strings.OptionsOff;
        _lblLastWakeSection.Text = strings.OptionsLastWakeSection;
        _btnRefreshWake.Text = strings.OptionsLastWakeRefresh;
        _lblDevicesSection.Text = strings.OptionsDevicesSection;
        _lblDevicesHint.Text = strings.OptionsDevicesHint;
        _btnSave.Text = strings.OptionsSave;
        _btnCancel.Text = strings.OptionsCancel;

        _tip.SetToolTip(_lblWakeTimers, strings.OptionsWakeTimersHint);
        _tip.SetToolTip(_rowTimers, strings.OptionsWakeTimersHint);
        _tip.SetToolTip(_lblWol, strings.OptionsWakeOnLanHint);
        _tip.SetToolTip(_rowWol, strings.OptionsWakeOnLanHint);

        LayoutToggleRow(_rowTimers, _lblWakeTimers, _radioTimersOn, _radioTimersOff);
        LayoutToggleRow(_rowWol, _lblWol, _radioWolOn, _radioWolOff);
        LayoutRefreshButton();
        RefreshPlanLabel();
        RefreshLastWakeLabel();
    }

    private static void LayoutToggleRow(Panel panel, Label label, RadioButton radioOn, RadioButton radioOff)
    {
        const int right = ContentWidth;
        radioOff.Left = right - radioOff.PreferredSize.Width;
        radioOn.Left = radioOff.Left - 20 - radioOn.PreferredSize.Width;
        label.Width = Math.Max(120, radioOn.Left - 12);
        radioOn.Top = Math.Max(0, (panel.Height - radioOn.PreferredSize.Height) / 2);
        radioOff.Top = radioOn.Top;
    }

    private void LayoutRefreshButton()
    {
        var textWidth = TextRenderer.MeasureText(
            _btnRefreshWake.Text,
            _btnRefreshWake.Font,
            Size.Empty,
            TextFormatFlags.NoPadding).Width;
        var width = Math.Clamp(textWidth + 20, 100, 160);
        _btnRefreshWake.Width = width;
        _btnRefreshWake.Left = ContentLeft + ContentWidth - width;
        _lblLastWakeSection.Width = _btnRefreshWake.Left - ContentLeft - 8;
    }

    private void LoadCurrentSettings()
    {
        if (PowerSchemeService.TryGetWakeTimers(out var info))
        {
            _originalTimersAllowed = info.AcAllowed;
            _schemeName = info.SchemeName;
            _radioTimersOn.Checked = info.AcAllowed;
            _radioTimersOff.Checked = !info.AcAllowed;
            _radioTimersOn.Enabled = true;
            _radioTimersOff.Enabled = true;
        }
        else
        {
            _schemeName = string.Empty;
            _radioTimersOn.Enabled = false;
            _radioTimersOff.Enabled = false;
            _radioTimersOff.Checked = true;
        }

        var wol = WakeSourcesService.GetWakeOnLan();
        _wolSupported = wol.Supported;
        _originalWolEnabled = wol.Enabled;
        _radioWolOn.Enabled = wol.Supported;
        _radioWolOff.Enabled = wol.Supported;
        if (wol.Supported)
        {
            _radioWolOn.Checked = wol.Enabled;
            _radioWolOff.Checked = !wol.Enabled;
        }
        else
        {
            _radioWolOff.Checked = true;
            _tip.SetToolTip(_lblWol, _t.OptionsWakeOnLanUnsupported);
        }

        LoadDeviceList();
        RefreshPlanLabel();
        RefreshLastWake();
        _btnSave.Enabled = true;
    }

    private void LoadDeviceList()
    {
        _lstDevices.Items.Clear();
        _originalDevices.Clear();

        try
        {
            var devices = WakeSourcesService.GetWakeDevices();
            if (devices.Count == 0)
            {
                _lblDevicesHint.Text = _t.OptionsDevicesEmpty;
                return;
            }

            _lstDevices.BeginUpdate();
            try
            {
                foreach (var device in devices)
                {
                    _lstDevices.Items.Add(device.Name, device.Armed);
                    _originalDevices[device.Name] = device.Armed;
                }
            }
            finally
            {
                _lstDevices.EndUpdate();
            }
            _lblDevicesHint.Text = _t.OptionsDevicesHint;
        }
        catch
        {
            _lblDevicesHint.Text = _t.OptionsDevicesReadFailed;
        }
    }

    private void RefreshPlanLabel()
    {
        _lblPlan.Text = string.IsNullOrWhiteSpace(_schemeName)
            ? _t.OptionsPowerPlanUnknown
            : string.Format(_t.OptionsPowerPlan, _schemeName);
        LayoutHeader();
    }

    private void LayoutHeader()
    {
        const int gap = 12;
        const int right = ContentLeft + ContentWidth;

        _lblTitle.AutoSize = true;
        _lblPlan.AutoSize = true;

        var titleRight = _lblTitle.Left + _lblTitle.PreferredWidth;
        var planWidth = _lblPlan.PreferredWidth;
        var maxPlanWidth = Math.Max(80, right - titleRight - gap);

        if (planWidth > maxPlanWidth)
        {
            _lblPlan.AutoSize = false;
            _lblPlan.AutoEllipsis = true;
            _lblPlan.Size = new Size(maxPlanWidth, _lblPlan.PreferredHeight);
        }

        _lblPlan.Left = right - _lblPlan.Width;
        _lblPlan.Top = _lblTitle.Top + Math.Max(0, (_lblTitle.Height - _lblPlan.Height) / 2);
    }

    private void RefreshLastWake()
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            var info = WakeSourcesService.GetLastWake();
            _lblLastWake.Tag = info;
            RefreshLastWakeLabel();
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RefreshLastWakeLabel()
    {
        if (_lblLastWake.Tag is WakeSourcesService.LastWakeInfo { Found: true, Source: not null } info)
        {
            _lblLastWake.ForeColor = Color.FromArgb(0, 90, 160);
            _lblLastWake.Text = info.WakeTime is { } time
                ? string.Format(_t.OptionsLastWakeValueAt, info.Source, time.ToString("g"))
                : string.Format(_t.OptionsLastWakeValue, info.Source);
        }
        else
        {
            _lblLastWake.ForeColor = Color.DimGray;
            _lblLastWake.Text = _t.OptionsLastWakeUnknown;
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var anyChange = false;
        var accessDenied = false;
        var failed = false;

        var timersAllowed = _radioTimersOn.Checked;
        if (_radioTimersOn.Enabled && timersAllowed != _originalTimersAllowed)
        {
            anyChange = true;
            if (!PowerSchemeService.TrySetWakeTimers(timersAllowed, out var denied))
            {
                failed = true;
                accessDenied |= denied;
            }
            else
            {
                _originalTimersAllowed = timersAllowed;
            }
        }

        for (var i = 0; i < _lstDevices.Items.Count; i++)
        {
            if (_lstDevices.Items[i] is not string name)
                continue;

            var armed = _lstDevices.GetItemChecked(i);
            if (_originalDevices.TryGetValue(name, out var original) && original == armed)
                continue;

            anyChange = true;
            if (!WakeSourcesService.TrySetDeviceWake(name, armed, out var denied))
            {
                failed = true;
                accessDenied |= denied;
            }
            else
            {
                _originalDevices[name] = armed;
            }
        }

        // After the device list so the dedicated WoL switch wins for Ethernet adapters.
        var wolEnabled = _radioWolOn.Checked;
        if (_wolSupported && wolEnabled != _originalWolEnabled)
        {
            anyChange = true;
            if (!WakeSourcesService.TrySetWakeOnLan(wolEnabled, out var denied))
            {
                failed = true;
                accessDenied |= denied;
            }
            else
            {
                _originalWolEnabled = wolEnabled;
            }
        }

        if (failed)
        {
            MessageBox.Show(
                accessDenied ? _t.OptionsWakeTimersWriteFailedAdmin : _t.OptionsSomeSettingsFailed,
                _t.OptionsWindowTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!anyChange)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
