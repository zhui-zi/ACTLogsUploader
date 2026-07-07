using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ACTLogsUploader.Config;
using ACTLogsUploader.Logging;

namespace ACTLogsUploader.UI
{
    internal sealed class ConfigTab
    {
        private static readonly int[] RegionServerOrRegion = { 1, 2, 3, 6, 1 };
        private static readonly string[] RegionCodes = { "NA", "EU", "JP", "OC", "CN" };
        private static readonly int[] DeleteDays = { 0, 7, 14, 30, 60, 90 };
        private const long SplitPartBytes = 40L * 1024 * 1024;
        private const string RepoUrl = "https://github.com/zhui-zi/ACTLogsUploader";

        private readonly Plugin _plugin;
        private readonly PluginSettings _settings;

        private ComboBox _language, _target, _region, _visibility, _guild, _autoDelete;
        private TextBox _email, _password, _logFolder, _description, _log;
        private CheckBox _remember, _autoLogin, _autoUpload, _uploadPrev, _autoArchive;
        private Button _save, _login, _upload, _uploadFile, _uploadSpecific, _startLive, _stopLive, _split, _archiveNow, _deleteArchived, _github;
        private readonly List<KeyValuePair<Label, string>> _rowLabels = new List<KeyValuePair<Label, string>>();
        private readonly List<string> _guildIds = new List<string>();
        private System.Windows.Forms.Timer _liveTimer;

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

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
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

            _language = Combo(120);
            _language.Items.AddRange(new object[] { "English", "中文" });
            _language.SelectedIndexChanged += (s, e) =>
            {
                Loc.Current = _language.SelectedIndex == 1 ? Language.Chinese : Language.English;
                _settings.Language = Loc.Current;
                Relocalize();
            };
            _github = Btn("btn.github", (s, e) => OpenUrl(RepoUrl));
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionLabel = new Label { Text = $"v{version.Major}.{version.Minor}.{version.Build}", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 8, 3, 3) };
            var topRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            topRow.Controls.AddRange(new Control[] { _language, _github, versionLabel });
            AddRow("lbl.language", topRow);

            _target = Combo(200);
            _target.Items.AddRange(TargetItems());
            AddRow("lbl.target", _target);

            _email = new TextBox();
            AddRow("lbl.email", _email);

            _password = new TextBox { UseSystemPasswordChar = true };
            AddRow("lbl.password", _password);

            _remember = new CheckBox { Text = Loc.T("chk.remember"), AutoSize = true };
            AddRow(null, _remember);

            _autoLogin = new CheckBox { Text = Loc.T("chk.autoLogin"), AutoSize = true };
            AddRow(null, _autoLogin);

            _autoUpload = new CheckBox { Text = Loc.T("chk.autoUpload"), AutoSize = true };
            AddRow(null, _autoUpload);

            _region = Combo(160);
            _region.Items.AddRange(RegionItems());
            AddRow("lbl.region", _region);

            _visibility = Combo(160);
            _visibility.Items.AddRange(VisItems());
            AddRow("lbl.visibility", _visibility);

            _guild = Combo(240);
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
            _save = Btn("btn.save", (s, e) => { ApplyToSettings(); _settings.Save(); Log(Loc.T("st.settingsSaved")); });
            _login = Btn("btn.login", async (s, e) => await Guarded(_login, async () => { ApplyToSettings(); _settings.Save(); if (await _plugin.LoginAsync()) { RefreshGuilds(); _plugin.MaybeStartAutoUpload(); } }));
            _upload = Btn("btn.uploadLatest", async (s, e) => await Guarded(_upload, async () => { ApplyToSettings(); await _plugin.UploadLatestAsync(_description.Text); }));
            _uploadFile = Btn("btn.uploadFile", async (s, e) => await UploadPickedFile());
            _uploadSpecific = Btn("btn.uploadSpecific", async (s, e) => await UploadSpecificFights());
            _startLive = Btn("btn.startLive", (s, e) => { ApplyToSettings(); _plugin.StartLive(_description.Text); UpdateLiveButtons(); });
            _stopLive = Btn("btn.stopLive", (s, e) => { _plugin.StopLive(); UpdateLiveButtons(); });
            _stopLive.Enabled = false;
            _split = Btn("btn.splitLog", async (s, e) => await SplitLogAction());
            buttons.Controls.AddRange(new Control[] { _save, _login, _upload, _uploadFile, _uploadSpecific, _startLive, _stopLive, _split });
            AddRow(null, buttons);

            _autoArchive = new CheckBox { Text = Loc.T("chk.autoArchive"), AutoSize = true };
            AddRow("sec.maintenance", _autoArchive);

            _autoDelete = Combo(160);
            _autoDelete.Items.AddRange(DeleteItems());
            AddRow("lbl.autoDelete", _autoDelete);

            var maintButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _archiveNow = Btn("btn.archiveNow", (s, e) => { ApplyToSettings(); _plugin.ArchiveNow(); });
            _deleteArchived = Btn("btn.deleteArchived", (s, e) => { _plugin.DeleteArchivedNow(); });
            maintButtons.Controls.AddRange(new Control[] { _archiveNow, _deleteArchived });
            AddRow(null, maintButtons);

            _log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 220, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            AddRow("lbl.log", _log);

            tab.Controls.Add(root);
            tab.ResumeLayout(true);

            LoadFromSettings();
            PluginLog.Sink = AppendLog;

            // Keep the live-logging buttons in sync with the actual state (auto-start,
            // manual start/stop, and self-end all reflect here).
            _liveTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _liveTimer.Tick += (s, e) => UpdateLiveButtons();
            _liveTimer.Start();
        }

        public void Cleanup()
        {
            _liveTimer?.Stop();
            _liveTimer?.Dispose();
            _liveTimer = null;
        }

        private static ComboBox Combo(int width) => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };

        private static Button Btn(string key, EventHandler onClick)
        {
            var b = new Button { Text = Loc.T(key), AutoSize = true };
            b.Click += onClick;
            return b;
        }

        private static object[] TargetItems() => new object[] { Loc.T("target.global"), Loc.T("target.china") };
        private static object[] RegionItems() => new object[] { "NA", "EU", "JP", "OC", Loc.T("region.cn") };
        private static object[] VisItems() => new object[] { Loc.T("vis.public"), Loc.T("vis.private"), Loc.T("vis.unlisted") };
        private static object[] DeleteItems() => new object[] { Loc.T("del.never"), Loc.T("del.days", 7), Loc.T("del.days", 14), Loc.T("del.days", 30), Loc.T("del.days", 60), Loc.T("del.days", 90) };

        private void Relocalize()
        {
            foreach (var kv in _rowLabels) kv.Key.Text = Loc.T(kv.Value);
            _save.Text = Loc.T("btn.save");
            _login.Text = Loc.T("btn.login");
            _upload.Text = Loc.T("btn.uploadLatest");
            _uploadFile.Text = Loc.T("btn.uploadFile");
            _uploadSpecific.Text = Loc.T("btn.uploadSpecific");
            _startLive.Text = Loc.T("btn.startLive");
            _stopLive.Text = Loc.T("btn.stopLive");
            _split.Text = Loc.T("btn.splitLog");
            _github.Text = Loc.T("btn.github");
            _archiveNow.Text = Loc.T("btn.archiveNow");
            _deleteArchived.Text = Loc.T("btn.deleteArchived");
            _remember.Text = Loc.T("chk.remember");
            _autoLogin.Text = Loc.T("chk.autoLogin");
            _autoUpload.Text = Loc.T("chk.autoUpload");
            _uploadPrev.Text = Loc.T("chk.uploadPrev");
            _autoArchive.Text = Loc.T("chk.autoArchive");
            Repopulate(_target, TargetItems());
            Repopulate(_region, RegionItems());
            Repopulate(_visibility, VisItems());
            Repopulate(_autoDelete, DeleteItems());
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

        private async Task UploadPickedFile()
        {
            ApplyToSettings();
            var path = PickLogFile();
            if (path == null) return;
            await Guarded(_uploadFile, () => _plugin.UploadFileAsync(path, _description.Text));
        }

        private async Task UploadSpecificFights()
        {
            ApplyToSettings();
            var path = PickLogFile();
            if (path == null) return;
            await Guarded(_uploadSpecific, async () =>
            {
                var uploads = await _plugin.PrepareAsync(path);
                if (uploads == null || uploads.Count == 0) return;
                var labels = uploads.Select((u, i) => $"{i + 1}. {u.Fight.Name}").ToList();
                using (var form = new FightPickerForm(labels))
                {
                    if (form.ShowDialog() != DialogResult.OK) return;
                    var selected = form.SelectedIndices().Select(k => uploads[k]).ToList();
                    if (selected.Count == 0) return;
                    await _plugin.UploadPreparedAsync(Path.GetFileName(path), selected, _description.Text);
                }
            });
        }

        private async Task SplitLogAction()
        {
            var path = PickLogFile();
            if (path == null) return;
            await Guarded(_split, () => Task.Run(() => _plugin.SplitLog(path, SplitPartBytes)));
        }

        private void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(url); }
            catch (Exception ex) { Log(Loc.T("st.error", ex.Message)); }
        }

        private string PickLogFile()
        {
            using (var dlg = new OpenFileDialog { Filter = "FFXIV logs (Network_*.log)|Network_*.log|Log files (*.log)|*.log|All files (*.*)|*.*" })
            {
                var dir = _plugin.ResolveLogDirectory();
                if (!string.IsNullOrEmpty(dir)) dlg.InitialDirectory = dir;
                return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
            }
        }

        private void LoadFromSettings()
        {
            _language.SelectedIndex = _settings.Language == Language.Chinese ? 1 : 0;
            _target.SelectedIndex = _settings.Target == FFLogsTarget.China ? 1 : 0;
            _email.Text = _settings.Email;
            _password.Text = _settings.Password;
            _remember.Checked = _settings.RememberCredentials;
            _autoLogin.Checked = _settings.AutoLogin;
            _autoUpload.Checked = _settings.AutoUpload;
            _region.SelectedIndex = Math.Max(0, Array.IndexOf(RegionCodes, _settings.RegionCode));
            _visibility.SelectedIndex = Math.Min(Math.Max(0, _settings.Visibility), 2);
            _logFolder.Text = _settings.LogDirectory;
            _uploadPrev.Checked = _settings.UploadPreviousFights;
            _autoArchive.Checked = _settings.AutoArchive;
            _autoDelete.SelectedIndex = Math.Max(0, Array.IndexOf(DeleteDays, _settings.AutoDeleteArchivedDays));
        }

        private void ApplyToSettings()
        {
            _settings.Language = Loc.Current;
            _settings.Target = _target.SelectedIndex == 1 ? FFLogsTarget.China : FFLogsTarget.Global;
            _settings.Email = _email.Text.Trim();
            _settings.RememberCredentials = _remember.Checked;
            _settings.AutoLogin = _autoLogin.Checked;
            _settings.AutoUpload = _autoUpload.Checked;
            _settings.Password = _remember.Checked ? _password.Text : string.Empty;

            int ri = Math.Max(0, _region.SelectedIndex);
            _settings.Region = RegionServerOrRegion[ri];
            _settings.RegionCode = RegionCodes[ri];
            _settings.Visibility = Math.Max(0, _visibility.SelectedIndex);
            _settings.GuildId = GetSelectedGuildId();
            _settings.LogDirectory = _logFolder.Text.Trim();
            _settings.UploadPreviousFights = _uploadPrev.Checked;
            _settings.AutoArchive = _autoArchive.Checked;
            _settings.AutoDeleteArchivedDays = DeleteDays[Math.Max(0, _autoDelete.SelectedIndex)];
        }

        private string GetSelectedGuildId()
        {
            int idx = _guild.SelectedIndex - 1; // 0 = Personal Logs
            if (idx >= 0 && idx < _guildIds.Count) return _guildIds[idx];
            return string.Empty;
        }

        public void OnLoggedIn()
        {
            RefreshGuilds();
            UpdateLiveButtons();
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
            if (_startLive == null || _startLive.IsDisposed) return;
            bool live = _plugin.IsLiveLogging;
            if (_startLive.Enabled == !live && _stopLive.Enabled == live) return;
            _startLive.Enabled = !live;
            _stopLive.Enabled = live;
        }

        private async Task Guarded(Button b, Func<Task> action)
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
