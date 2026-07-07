using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ACTLogsUploader.Config;
using ACTLogsUploader.Logging;

namespace ACTLogsUploader.UI
{
    internal sealed class ConfigTab
    {
        private static readonly string[] RegionNames = { "NA", "EU", "JP", "OC", "CN (国服)" };
        private static readonly int[] RegionServerOrRegion = { 1, 2, 3, 6, 1 };
        private static readonly string[] RegionCodes = { "NA", "EU", "JP", "OC", "CN" };
        private static readonly string[] Visibilities = { "Public", "Private", "Unlisted" };

        private readonly Plugin _plugin;
        private readonly PluginSettings _settings;

        private ComboBox _target, _region, _visibility, _guild;
        private TextBox _email, _password, _logFolder, _description, _log;
        private CheckBox _remember, _uploadPrev;
        private Button _login, _upload, _uploadFile, _startLive, _stopLive;
        private readonly List<string> _guildIds = new List<string>();

        public ConfigTab(Plugin plugin, PluginSettings settings)
        {
            _plugin = plugin;
            _settings = settings;
        }

        public void Build(TabPage tab)
        {
            tab.SuspendLayout();
            tab.Text = "FFLogs Uploader";

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12),
                AutoScroll = true,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;
            void AddRow(string label, Control control)
            {
                root.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, row);
                control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                control.Margin = new Padding(3, 5, 3, 3);
                root.Controls.Add(control, 1, row);
                row++;
            }

            _target = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            _target.Items.AddRange(new object[] { "Global (fflogs.com)", "China 国服 (cn.fflogs.com)" });
            AddRow("Target", _target);

            _email = new TextBox();
            AddRow("Email", _email);

            _password = new TextBox { UseSystemPasswordChar = true };
            AddRow("Password", _password);

            _remember = new CheckBox { Text = "Remember credentials (password stored DPAPI-encrypted)", AutoSize = true };
            AddRow("", _remember);

            _region = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            _region.Items.AddRange(RegionNames);
            AddRow("Region", _region);

            _visibility = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            _visibility.Items.AddRange(Visibilities);
            AddRow("Visibility", _visibility);

            _guild = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            _guild.Items.Add("Personal Logs");
            _guild.SelectedIndex = 0;
            AddRow("Upload to", _guild);

            var folderPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _logFolder = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            var browse = new Button { Text = "...", Width = 30 };
            browse.Click += (s, e) => { using (var dlg = new FolderBrowserDialog()) { if (dlg.ShowDialog() == DialogResult.OK) _logFolder.Text = dlg.SelectedPath; } };
            folderPanel.Controls.Add(_logFolder, 0, 0);
            folderPanel.Controls.Add(browse, 1, 0);
            AddRow("Log folder", folderPanel);

            _description = new TextBox();
            AddRow("Description", _description);

            _uploadPrev = new CheckBox { Text = "Include existing fights in the log when uploading / going live", AutoSize = true };
            AddRow("", _uploadPrev);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            var save = new Button { Text = "Save", AutoSize = true };
            save.Click += (s, e) => { ApplyToSettings(); _settings.Save(); Log("Settings saved."); };
            _login = new Button { Text = "Login", AutoSize = true };
            _login.Click += async (s, e) => await Guarded(_login, async () => { ApplyToSettings(); _settings.Save(); if (await _plugin.LoginAsync()) RefreshGuilds(); });
            _upload = new Button { Text = "Upload latest log", AutoSize = true };
            _upload.Click += async (s, e) => await Guarded(_upload, async () => { ApplyToSettings(); await _plugin.UploadLatestAsync(_description.Text); });
            _uploadFile = new Button { Text = "Upload file...", AutoSize = true };
            _uploadFile.Click += async (s, e) => await UploadPickedFile();
            _startLive = new Button { Text = "Start live", AutoSize = true };
            _startLive.Click += (s, e) => { ApplyToSettings(); _plugin.StartLive(_description.Text); UpdateLiveButtons(); };
            _stopLive = new Button { Text = "Stop live", AutoSize = true, Enabled = false };
            _stopLive.Click += (s, e) => { _plugin.StopLive(); UpdateLiveButtons(); };
            buttons.Controls.AddRange(new Control[] { save, _login, _upload, _uploadFile, _startLive, _stopLive });
            AddRow("", buttons);

            _log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 180, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            AddRow("Log", _log);

            tab.Controls.Add(root);
            tab.ResumeLayout(true);

            LoadFromSettings();
            PluginLog.Sink = AppendLog;
        }

        private async System.Threading.Tasks.Task UploadPickedFile()
        {
            ApplyToSettings();
            string path;
            using (var dlg = new OpenFileDialog { Filter = "FFXIV logs (Network_*.log)|Network_*.log|Log files (*.log)|*.log|All files (*.*)|*.*" })
            {
                var dir = _plugin.ResolveLogDirectory();
                if (!string.IsNullOrEmpty(dir)) dlg.InitialDirectory = dir;
                if (dlg.ShowDialog() != DialogResult.OK) return;
                path = dlg.FileName;
            }
            await Guarded(_uploadFile, () => _plugin.UploadFileAsync(path, _description.Text));
        }

        private void LoadFromSettings()
        {
            _target.SelectedIndex = _settings.Target == FFLogsTarget.China ? 1 : 0;
            _email.Text = _settings.Email;
            _password.Text = _settings.Password;
            _remember.Checked = _settings.RememberCredentials;
            _region.SelectedIndex = Math.Max(0, Array.IndexOf(RegionCodes, _settings.RegionCode));
            _visibility.SelectedIndex = Math.Min(Math.Max(0, _settings.Visibility), Visibilities.Length - 1);
            _logFolder.Text = _settings.LogDirectory;
            _uploadPrev.Checked = _settings.UploadPreviousFights;
        }

        private void ApplyToSettings()
        {
            _settings.Target = _target.SelectedIndex == 1 ? FFLogsTarget.China : FFLogsTarget.Global;
            _settings.Email = _email.Text.Trim();
            _settings.RememberCredentials = _remember.Checked;
            _settings.Password = _remember.Checked ? _password.Text : string.Empty;

            int ri = Math.Max(0, _region.SelectedIndex);
            _settings.Region = RegionServerOrRegion[ri];
            _settings.RegionCode = RegionCodes[ri];
            _settings.Visibility = Math.Max(0, _visibility.SelectedIndex);
            _settings.GuildId = GetSelectedGuildId();
            _settings.LogDirectory = _logFolder.Text.Trim();
            _settings.UploadPreviousFights = _uploadPrev.Checked;
        }

        private string GetSelectedGuildId()
        {
            int idx = _guild.SelectedIndex - 1; // 0 = Personal Logs
            if (idx >= 0 && idx < _guildIds.Count) return _guildIds[idx];
            return string.Empty;
        }

        private void RefreshGuilds()
        {
            if (_guild.InvokeRequired) { _guild.BeginInvoke((Action)RefreshGuilds); return; }
            _guild.Items.Clear();
            _guildIds.Clear();
            _guild.Items.Add("Personal Logs");
            var client = _plugin.Client;
            if (client != null)
            {
                foreach (var g in client.Guilds)
                {
                    _guild.Items.Add(g.Name);
                    _guildIds.Add(g.Id);
                }
            }
            _guild.SelectedIndex = 0;
        }

        private void UpdateLiveButtons()
        {
            bool live = _plugin.IsLiveLogging;
            _startLive.Enabled = !live;
            _stopLive.Enabled = live;
        }

        private async System.Threading.Tasks.Task Guarded(Button b, Func<System.Threading.Tasks.Task> action)
        {
            b.Enabled = false;
            try { await action(); }
            catch (Exception ex) { Log("Error: " + ex.Message); }
            finally { b.Enabled = true; UpdateLiveButtons(); }
        }

        private void Log(string s) => PluginLog.Info(s);

        private void AppendLog(string line)
        {
            if (_log == null || _log.IsDisposed) return;
            if (_log.InvokeRequired) { _log.BeginInvoke((Action)(() => AppendLog(line))); return; }
            _log.AppendText(line + Environment.NewLine);
        }
    }
}
