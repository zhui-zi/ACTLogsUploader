using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ACTLogsUploader.Config;
using ACTLogsUploader.Logging;

namespace ACTLogsUploader.UI
{
    internal sealed class ConfigTab
    {
        private static readonly int[] RegionServerOrRegion = { 1, 2, 3, 6, 1 };
        private static readonly string[] RegionCodes = { "NA", "EU", "JP", "OC", "CN" };

        private readonly Plugin _plugin;
        private readonly PluginSettings _settings;

        private ComboBox _language, _target, _region, _visibility, _guild;
        private TextBox _email, _password, _logFolder, _description, _log;
        private CheckBox _remember, _uploadPrev;
        private Button _save, _login, _upload, _uploadFile, _startLive, _stopLive;
        private readonly List<KeyValuePair<Label, string>> _rowLabels = new List<KeyValuePair<Label, string>>();
        private readonly List<string> _guildIds = new List<string>();

        public ConfigTab(Plugin plugin, PluginSettings settings)
        {
            _plugin = plugin;
            _settings = settings;
        }

        public void Build(TabPage tab)
        {
            Loc.Current = _settings.Language;
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
            void AddRow(string labelKey, Control control)
            {
                var label = new Label { Text = labelKey == null ? "" : Loc.T(labelKey), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };
                if (labelKey != null) _rowLabels.Add(new KeyValuePair<Label, string>(label, labelKey));
                root.Controls.Add(label, 0, row);
                control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                control.Margin = new Padding(3, 5, 3, 3);
                root.Controls.Add(control, 1, row);
                row++;
            }

            _language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            _language.Items.AddRange(new object[] { "English", "中文" });
            _language.SelectedIndexChanged += (s, e) =>
            {
                Loc.Current = _language.SelectedIndex == 1 ? Language.Chinese : Language.English;
                _settings.Language = Loc.Current;
                Relocalize();
            };
            AddRow("lbl.language", _language);

            _target = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            _target.Items.AddRange(TargetItems());
            AddRow("lbl.target", _target);

            _email = new TextBox();
            AddRow("lbl.email", _email);

            _password = new TextBox { UseSystemPasswordChar = true };
            AddRow("lbl.password", _password);

            _remember = new CheckBox { Text = Loc.T("chk.remember"), AutoSize = true };
            AddRow(null, _remember);

            _region = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            _region.Items.AddRange(RegionItems());
            AddRow("lbl.region", _region);

            _visibility = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            _visibility.Items.AddRange(VisItems());
            AddRow("lbl.visibility", _visibility);

            _guild = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            _guild.Items.Add(Loc.T("guild.personal"));
            _guild.SelectedIndex = 0;
            AddRow("lbl.uploadTo", _guild);

            var folderPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _logFolder = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            var browse = new Button { Text = "...", Width = 30 };
            browse.Click += (s, e) => { using (var dlg = new FolderBrowserDialog()) { if (dlg.ShowDialog() == DialogResult.OK) _logFolder.Text = dlg.SelectedPath; } };
            folderPanel.Controls.Add(_logFolder, 0, 0);
            folderPanel.Controls.Add(browse, 1, 0);
            AddRow("lbl.logFolder", folderPanel);

            _description = new TextBox();
            AddRow("lbl.description", _description);

            _uploadPrev = new CheckBox { Text = Loc.T("chk.uploadPrev"), AutoSize = true };
            AddRow(null, _uploadPrev);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _save = new Button { Text = Loc.T("btn.save"), AutoSize = true };
            _save.Click += (s, e) => { ApplyToSettings(); _settings.Save(); Log(Loc.T("st.settingsSaved")); };
            _login = new Button { Text = Loc.T("btn.login"), AutoSize = true };
            _login.Click += async (s, e) => await Guarded(_login, async () => { ApplyToSettings(); _settings.Save(); if (await _plugin.LoginAsync()) RefreshGuilds(); });
            _upload = new Button { Text = Loc.T("btn.uploadLatest"), AutoSize = true };
            _upload.Click += async (s, e) => await Guarded(_upload, async () => { ApplyToSettings(); await _plugin.UploadLatestAsync(_description.Text); });
            _uploadFile = new Button { Text = Loc.T("btn.uploadFile"), AutoSize = true };
            _uploadFile.Click += async (s, e) => await UploadPickedFile();
            _startLive = new Button { Text = Loc.T("btn.startLive"), AutoSize = true };
            _startLive.Click += (s, e) => { ApplyToSettings(); _plugin.StartLive(_description.Text); UpdateLiveButtons(); };
            _stopLive = new Button { Text = Loc.T("btn.stopLive"), AutoSize = true, Enabled = false };
            _stopLive.Click += (s, e) => { _plugin.StopLive(); UpdateLiveButtons(); };
            buttons.Controls.AddRange(new Control[] { _save, _login, _upload, _uploadFile, _startLive, _stopLive });
            AddRow(null, buttons);

            _log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 180, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            AddRow("lbl.log", _log);

            tab.Controls.Add(root);
            tab.ResumeLayout(true);

            LoadFromSettings();
            PluginLog.Sink = AppendLog;
        }

        private static object[] TargetItems() => new object[] { Loc.T("target.global"), Loc.T("target.china") };
        private static object[] RegionItems() => new object[] { "NA", "EU", "JP", "OC", Loc.T("region.cn") };
        private static object[] VisItems() => new object[] { Loc.T("vis.public"), Loc.T("vis.private"), Loc.T("vis.unlisted") };

        private void Relocalize()
        {
            foreach (var kv in _rowLabels) kv.Key.Text = Loc.T(kv.Value);
            _save.Text = Loc.T("btn.save");
            _login.Text = Loc.T("btn.login");
            _upload.Text = Loc.T("btn.uploadLatest");
            _uploadFile.Text = Loc.T("btn.uploadFile");
            _startLive.Text = Loc.T("btn.startLive");
            _stopLive.Text = Loc.T("btn.stopLive");
            _remember.Text = Loc.T("chk.remember");
            _uploadPrev.Text = Loc.T("chk.uploadPrev");
            Repopulate(_target, TargetItems());
            Repopulate(_region, RegionItems());
            Repopulate(_visibility, VisItems());
            if (_guild.Items.Count > 0)
            {
                int gi = _guild.SelectedIndex;
                _guild.Items[0] = Loc.T("guild.personal");
                _guild.SelectedIndex = gi;
            }
        }

        private static void Repopulate(ComboBox combo, object[] items)
        {
            int idx = combo.SelectedIndex;
            combo.Items.Clear();
            combo.Items.AddRange(items);
            if (idx >= 0 && idx < items.Length) combo.SelectedIndex = idx;
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
            _language.SelectedIndex = _settings.Language == Language.Chinese ? 1 : 0;
            _target.SelectedIndex = _settings.Target == FFLogsTarget.China ? 1 : 0;
            _email.Text = _settings.Email;
            _password.Text = _settings.Password;
            _remember.Checked = _settings.RememberCredentials;
            _region.SelectedIndex = Math.Max(0, Array.IndexOf(RegionCodes, _settings.RegionCode));
            _visibility.SelectedIndex = Math.Min(Math.Max(0, _settings.Visibility), 2);
            _logFolder.Text = _settings.LogDirectory;
            _uploadPrev.Checked = _settings.UploadPreviousFights;
        }

        private void ApplyToSettings()
        {
            _settings.Language = Loc.Current;
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
            _guild.Items.Add(Loc.T("guild.personal"));
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
            catch (Exception ex) { Log(Loc.T("st.error", ex.Message)); }
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
