namespace LeDormeur;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblTitle = new Label();
        lblLanguage = new Label();
        cmbLanguage = new ComboBox();
        lblDuration = new Label();
        numHours = new NumericUpDown();
        lblHours = new Label();
        numMinutes = new NumericUpDown();
        lblMinutes = new Label();
        lblBrightness = new Label();
        trackBrightness = new TrackBar();
        lblBrightnessValue = new Label();
        lblBrightnessHint = new Label();
        btnStart = new Button();
        btnCancel = new Button();
        btnAutoMode = new Button();
        progressBar = new ProgressBar();
        lblStatus = new Label();
        lblRemaining = new Label();
        lblMode = new Label();
        lblVersion = new Label();
        timerTick = new System.Windows.Forms.Timer(components);
        timerAutoMode = new System.Windows.Forms.Timer(components);
        ((System.ComponentModel.ISupportInitialize)numHours).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numMinutes).BeginInit();
        ((System.ComponentModel.ISupportInitialize)trackBrightness).BeginInit();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(24, 16);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(120, 30);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Le Dormeur";
        //
        // lblLanguage
        //
        lblLanguage.AutoSize = true;
        lblLanguage.Font = new Font("Segoe UI", 9F);
        lblLanguage.Location = new Point(262, 22);
        lblLanguage.Name = "lblLanguage";
        lblLanguage.Size = new Size(48, 15);
        lblLanguage.TabIndex = 16;
        lblLanguage.Text = "Langue";
        //
        // cmbLanguage
        //
        cmbLanguage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cmbLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbLanguage.Font = new Font("Segoe UI", 9F);
        cmbLanguage.FormattingEnabled = true;
        cmbLanguage.Location = new Point(328, 18);
        cmbLanguage.Name = "cmbLanguage";
        cmbLanguage.Size = new Size(120, 23);
        cmbLanguage.TabIndex = 17;
        cmbLanguage.SelectedIndexChanged += cmbLanguage_SelectedIndexChanged;
        //
        // lblDuration
        //
        lblDuration.AutoSize = true;
        lblDuration.Font = new Font("Segoe UI", 10F);
        lblDuration.Location = new Point(28, 64);
        lblDuration.Name = "lblDuration";
        lblDuration.Size = new Size(186, 19);
        lblDuration.TabIndex = 1;
        lblDuration.Text = "Durée avant la mise en veille";
        //
        // numHours
        //
        numHours.Font = new Font("Segoe UI", 12F);
        numHours.Location = new Point(32, 94);
        numHours.Maximum = new decimal(new int[] { 12, 0, 0, 0 });
        numHours.Name = "numHours";
        numHours.Size = new Size(70, 29);
        numHours.TabIndex = 2;
        numHours.Value = new decimal(new int[] { 1, 0, 0, 0 });
        numHours.ValueChanged += settings_ValueChanged;
        //
        // lblHours
        //
        lblHours.AutoSize = true;
        lblHours.Font = new Font("Segoe UI", 10F);
        lblHours.Location = new Point(108, 99);
        lblHours.Name = "lblHours";
        lblHours.Size = new Size(48, 19);
        lblHours.TabIndex = 3;
        lblHours.Text = "heure(s)";
        //
        // numMinutes
        //
        numMinutes.Font = new Font("Segoe UI", 12F);
        numMinutes.Location = new Point(180, 94);
        numMinutes.Maximum = new decimal(new int[] { 59, 0, 0, 0 });
        numMinutes.Name = "numMinutes";
        numMinutes.Size = new Size(70, 29);
        numMinutes.TabIndex = 4;
        numMinutes.ValueChanged += settings_ValueChanged;
        //
        // lblMinutes
        //
        lblMinutes.AutoSize = true;
        lblMinutes.Font = new Font("Segoe UI", 10F);
        lblMinutes.Location = new Point(256, 99);
        lblMinutes.Name = "lblMinutes";
        lblMinutes.Size = new Size(62, 19);
        lblMinutes.TabIndex = 5;
        lblMinutes.Text = "minute(s)";
        //
        // lblBrightness
        //
        lblBrightness.AutoSize = true;
        lblBrightness.Font = new Font("Segoe UI", 10F);
        lblBrightness.Location = new Point(28, 144);
        lblBrightness.Name = "lblBrightness";
        lblBrightness.Size = new Size(250, 19);
        lblBrightness.TabIndex = 6;
        lblBrightness.Text = "Luminosité à enlever sur cette durée (%)";
        //
        // trackBrightness
        //
        trackBrightness.LargeChange = 10;
        trackBrightness.Location = new Point(24, 172);
        trackBrightness.Maximum = 100;
        trackBrightness.Name = "trackBrightness";
        trackBrightness.Size = new Size(320, 45);
        trackBrightness.TabIndex = 7;
        trackBrightness.TickFrequency = 10;
        trackBrightness.Value = 50;
        trackBrightness.ValueChanged += trackBrightness_ValueChanged;
        //
        // lblBrightnessValue
        //
        lblBrightnessValue.AutoSize = true;
        lblBrightnessValue.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        lblBrightnessValue.Location = new Point(350, 176);
        lblBrightnessValue.Name = "lblBrightnessValue";
        lblBrightnessValue.Size = new Size(48, 21);
        lblBrightnessValue.TabIndex = 8;
        lblBrightnessValue.Text = "50 %";
        //
        // lblBrightnessHint
        //
        lblBrightnessHint.Font = new Font("Segoe UI", 8.5F);
        lblBrightnessHint.ForeColor = Color.DimGray;
        lblBrightnessHint.Location = new Point(28, 214);
        lblBrightnessHint.Name = "lblBrightnessHint";
        lblBrightnessHint.Size = new Size(410, 48);
        lblBrightnessHint.TabIndex = 9;
        lblBrightnessHint.Text = "";
        //
        // btnStart
        //
        btnStart.BackColor = Color.FromArgb(0, 120, 215);
        btnStart.FlatAppearance.BorderSize = 0;
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        btnStart.ForeColor = Color.White;
        btnStart.Location = new Point(32, 274);
        btnStart.Name = "btnStart";
        btnStart.Size = new Size(160, 40);
        btnStart.TabIndex = 10;
        btnStart.Text = "Démarrer";
        btnStart.UseVisualStyleBackColor = false;
        btnStart.Click += btnStart_Click;
        //
        // btnCancel
        //
        btnCancel.Enabled = false;
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("Segoe UI", 11F);
        btnCancel.Location = new Point(208, 274);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(120, 40);
        btnCancel.TabIndex = 11;
        btnCancel.Text = "Annuler";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Click += btnCancel_Click;
        //
        // btnAutoMode
        //
        btnAutoMode.FlatStyle = FlatStyle.Flat;
        btnAutoMode.Font = new Font("Segoe UI", 10F);
        btnAutoMode.Location = new Point(340, 274);
        btnAutoMode.Name = "btnAutoMode";
        btnAutoMode.Size = new Size(110, 40);
        btnAutoMode.TabIndex = 19;
        btnAutoMode.Text = "Mode auto...";
        btnAutoMode.UseVisualStyleBackColor = true;
        btnAutoMode.Click += btnAutoMode_Click;
        //
        // progressBar
        //
        progressBar.Location = new Point(32, 334);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(400, 18);
        progressBar.TabIndex = 12;
        //
        // lblStatus
        //
        lblStatus.AutoSize = true;
        lblStatus.Font = new Font("Segoe UI", 9.5F);
        lblStatus.Location = new Point(32, 364);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(53, 17);
        lblStatus.TabIndex = 13;
        lblStatus.Text = "Prêt.";
        //
        // lblRemaining
        //
        lblRemaining.AutoSize = true;
        lblRemaining.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        lblRemaining.Location = new Point(32, 389);
        lblRemaining.Name = "lblRemaining";
        lblRemaining.Size = new Size(0, 19);
        lblRemaining.TabIndex = 14;
        //
        // lblMode
        //
        lblMode.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblMode.Font = new Font("Segoe UI", 8F);
        lblMode.ForeColor = Color.DimGray;
        lblMode.Location = new Point(28, 419);
        lblMode.Name = "lblMode";
        lblMode.Size = new Size(340, 32);
        lblMode.TabIndex = 15;
        lblMode.Text = "Mode luminosité : détection…";
        //
        // lblVersion
        //
        lblVersion.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        lblVersion.AutoSize = true;
        lblVersion.Font = new Font("Segoe UI", 8F);
        lblVersion.ForeColor = Color.FromArgb(160, 160, 160);
        lblVersion.Location = new Point(410, 440);
        lblVersion.Name = "lblVersion";
        lblVersion.Size = new Size(38, 13);
        lblVersion.TabIndex = 18;
        lblVersion.Text = "v1.0.0";
        lblVersion.TextAlign = ContentAlignment.BottomRight;
        //
        // timerTick
        //
        timerTick.Interval = 1000;
        timerTick.Tick += timerTick_Tick;
        //
        // timerAutoMode
        //
        timerAutoMode.Interval = 15000;
        timerAutoMode.Tick += timerAutoMode_Tick;
        //
        // Form1
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(480, 468);
        Controls.Add(lblVersion);
        Controls.Add(cmbLanguage);
        Controls.Add(lblLanguage);
        Controls.Add(lblMode);
        Controls.Add(lblRemaining);
        Controls.Add(lblStatus);
        Controls.Add(progressBar);
        Controls.Add(btnAutoMode);
        Controls.Add(btnCancel);
        Controls.Add(btnStart);
        Controls.Add(lblBrightnessHint);
        Controls.Add(lblBrightnessValue);
        Controls.Add(trackBrightness);
        Controls.Add(lblBrightness);
        Controls.Add(lblMinutes);
        Controls.Add(numMinutes);
        Controls.Add(lblHours);
        Controls.Add(numHours);
        Controls.Add(lblDuration);
        Controls.Add(lblTitle);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimumSize = new Size(496, 508);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Le Dormeur — luminosité + veille";
        FormClosing += Form1_FormClosing;
        Load += Form1_Load;
        Resize += Form1_Resize;
        ((System.ComponentModel.ISupportInitialize)numHours).EndInit();
        ((System.ComponentModel.ISupportInitialize)numMinutes).EndInit();
        ((System.ComponentModel.ISupportInitialize)trackBrightness).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblLanguage;
    private ComboBox cmbLanguage;
    private Label lblDuration;
    private NumericUpDown numHours;
    private Label lblHours;
    private NumericUpDown numMinutes;
    private Label lblMinutes;
    private Label lblBrightness;
    private TrackBar trackBrightness;
    private Label lblBrightnessValue;
    private Label lblBrightnessHint;
    private Button btnStart;
    private Button btnCancel;
    private Button btnAutoMode;
    private ProgressBar progressBar;
    private Label lblStatus;
    private Label lblRemaining;
    private Label lblMode;
    private Label lblVersion;
    private System.Windows.Forms.Timer timerTick;
    private System.Windows.Forms.Timer timerAutoMode;
}
