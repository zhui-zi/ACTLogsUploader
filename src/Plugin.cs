using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using ACTLogsUploader.Config;
using ACTLogsUploader.Logging;
using ACTLogsUploader.Maintenance;
using ACTLogsUploader.UI;
using ACTLogsUploader.Upload;

namespace ACTLogsUploader
{
    public class Plugin : IActPluginV1
    {
        static Plugin()
        {
            Bootstrap.Initialize();
        }

        private Label _statusLabel;
        private PluginSettings _settings;
        private FFLogsClient _client;
        private ConfigTab _configTab;
        private System.Threading.Timer _maintTimer;

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            _statusLabel = pluginStatusText;
            try
            {
                _settings = PluginSettings.Load();
                Loc.Current = _settings.Language;
                _configTab = new ConfigTab(this, _settings);
                _configTab.Build(pluginScreenSpace);
                _maintTimer = new System.Threading.Timer(_ => RunAutoMaintenance(), null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(6));
                SetStatus(Loc.T("st.ready", _settings.Target));
                RunAutoStart();
            }
            catch (Exception ex)
            {
                SetStatus(Loc.T("st.initFailed", ex.Message));
                PluginLog.Error("InitPlugin failed", ex);
            }
        }

        public void DeInitPlugin()
        {
            try
            {
                _settings?.Save();
                _configTab?.Cleanup();
                _maintTimer?.Dispose();
                _maintTimer = null;
                _client?.StopLiveLog();
                _client?.Dispose();
                _client = null;
                PluginLog.Sink = null;
                SetStatus(Loc.T("st.unloaded"));
            }
            catch (Exception ex)
            {
                PluginLog.Error("DeInitPlugin failed", ex);
            }
        }

        public FFLogsClient Client => _client;
        public bool IsLiveLogging => _client?.IsLiveLogging ?? false;

        public async Task<bool> LoginAsync()
        {
            RecreateClientIfNeeded();
            if (string.IsNullOrWhiteSpace(_settings.Email) || string.IsNullOrEmpty(_settings.Password))
            {
                SetStatus(Loc.T("st.enterCreds"));
                return false;
            }
            SetStatus(Loc.T("st.loggingIn"));
            var ok = await _client.LoginAsync(_settings.Email, _settings.Password);
            SetStatus(ok ? Loc.T("st.loggedInAs", _client.Username, _settings.Target) : Loc.T("st.loginFailed"));
            return ok;
        }

        public Task UploadLatestAsync(string description)
        {
            var dir = ResolveLogDirectory();
            if (string.IsNullOrEmpty(dir)) { SetStatus(Loc.T("st.noLogFolder")); return Task.CompletedTask; }
            var logPath = LogFileHelper.GetLatestLogFileFromPath(dir);
            return UploadFileAsync(logPath, description);
        }

        public async Task UploadFileAsync(string logPath, string description)
        {
            if (!EnsureLoggedIn()) return;
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath)) { SetStatus(Loc.T("st.logFileNotFound")); return; }
            try
            {
                SetStatus(Loc.T("st.uploading", Path.GetFileName(logPath)));
                var code = await _client.UploadLogAsync(
                    logPath, _settings.Region, _settings.RegionCode, _settings.Visibility,
                    _settings.GuildId, description ?? "");
                SetStatus(Loc.T("st.uploaded", $"{_settings.BaseUrl}/reports/{code}"));
            }
            catch (Exception ex)
            {
                PluginLog.Error("Upload failed", ex);
                SetStatus(Loc.T("st.uploadFailed", ex.Message));
            }
        }

        public void StartLive(string description) => StartLiveCore(description, _settings.UploadPreviousFights);

        private void StartLiveCore(string description, bool uploadPreviousFights)
        {
            if (!EnsureLoggedIn()) return;
            var dir = ResolveLogDirectory();
            if (string.IsNullOrEmpty(dir)) { SetStatus(Loc.T("st.noLogFolder")); return; }
            _client.StartLiveLog(dir, _settings.Region, _settings.RegionCode, _settings.Visibility,
                _settings.GuildId, description ?? "", uploadPreviousFights);
            SetStatus(Loc.T("st.liveStarted"));
        }

        // Auto-login on load (if enabled + credentials saved), then auto-start live logging.
        private async void RunAutoStart()
        {
            try
            {
                if (_settings.AutoLogin)
                {
                    if (_settings.RememberCredentials && !string.IsNullOrWhiteSpace(_settings.Email) && !string.IsNullOrEmpty(_settings.Password))
                    {
                        if (await LoginAsync()) _configTab?.OnLoggedIn();
                    }
                    else
                    {
                        SetStatus(Loc.T("st.autoLoginSkipped"));
                    }
                }
                MaybeStartAutoUpload();
            }
            catch (Exception ex) { PluginLog.Error("Auto-start failed", ex); }
        }

        // Start live logging automatically (new fights only) when auto-upload is on.
        public void MaybeStartAutoUpload()
        {
            if (_settings.AutoUpload && _client != null && _client.IsLoggedIn && !_client.IsLiveLogging)
                StartLiveCore("", false);
        }

        public void StopLive()
        {
            _client?.StopLiveLog();
            SetStatus(Loc.T("st.liveStopping"));
        }

        // Parse a log into its fights (no report created yet) for the fight picker.
        public async Task<List<ParserEngine.FightUpload>> PrepareAsync(string logPath)
        {
            if (!EnsureLoggedIn()) return null;
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath)) { SetStatus(Loc.T("st.logFileNotFound")); return null; }
            try
            {
                SetStatus(Loc.T("st.parsing", Path.GetFileName(logPath)));
                var list = await _client.PrepareUploadsAsync(logPath, _settings.RegionCode);
                SetStatus(Loc.T("st.parsedFights", list.Count));
                return list;
            }
            catch (Exception ex)
            {
                PluginLog.Error("Parse failed", ex);
                SetStatus(Loc.T("st.uploadFailed", ex.Message));
                return null;
            }
        }

        public async Task UploadPreparedAsync(string fileName, IList<ParserEngine.FightUpload> selected, string description)
        {
            if (!EnsureLoggedIn() || selected == null || selected.Count == 0) return;
            try
            {
                SetStatus(Loc.T("st.uploading", fileName));
                var code = await _client.UploadPreparedAsync(fileName, selected, _settings.Region, _settings.Visibility, _settings.GuildId, description ?? "");
                SetStatus(Loc.T("st.uploaded", $"{_settings.BaseUrl}/reports/{code}"));
            }
            catch (Exception ex)
            {
                PluginLog.Error("Upload failed", ex);
                SetStatus(Loc.T("st.uploadFailed", ex.Message));
            }
        }

        public void ArchiveNow()
        {
            var dir = ResolveLogDirectory();
            if (string.IsNullOrEmpty(dir)) { SetStatus(Loc.T("st.noLogFolder")); return; }
            int n = LogMaintenance.ArchiveOldLogs(dir, _settings.AutoArchiveDays);
            SetStatus(Loc.T("st.archived", n));
        }

        public void DeleteArchivedNow()
        {
            var dir = ResolveLogDirectory();
            if (string.IsNullOrEmpty(dir)) { SetStatus(Loc.T("st.noLogFolder")); return; }
            int n = LogMaintenance.DeleteArchived(dir, 0);
            SetStatus(Loc.T("st.deletedArchived", n));
        }

        public void SplitLog(string logPath, long maxBytes)
        {
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath)) { SetStatus(Loc.T("st.logFileNotFound")); return; }
            int n = LogMaintenance.SplitLog(logPath, maxBytes);
            SetStatus(Loc.T("st.split", n));
        }

        private void RunAutoMaintenance()
        {
            try
            {
                var dir = ResolveLogDirectory();
                if (string.IsNullOrEmpty(dir)) return;
                if (_settings.AutoArchive) LogMaintenance.ArchiveOldLogs(dir, _settings.AutoArchiveDays);
                if (_settings.AutoDeleteArchivedDays > 0) LogMaintenance.DeleteArchived(dir, _settings.AutoDeleteArchivedDays);
            }
            catch (Exception ex) { PluginLog.Warn("[Maint] " + ex.Message); }
        }

        // Explicit override, else ACT's current log directory, else the default ACT path.
        public string ResolveLogDirectory()
        {
            if (!string.IsNullOrEmpty(_settings.LogDirectory) && Directory.Exists(_settings.LogDirectory))
                return _settings.LogDirectory;
            try
            {
                var lf = ActGlobals.oFormActMain.LogFilePath;
                if (!string.IsNullOrEmpty(lf))
                {
                    var d = Path.GetDirectoryName(lf);
                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
                }
            }
            catch { }
            return LogFileHelper.AutoDetectLogDirectory();
        }

        private void RecreateClientIfNeeded()
        {
            if (_client == null)
            {
                _client = new FFLogsClient(_settings.BaseUrl);
                return;
            }
            if (!_client.HttpClient.BaseAddress.ToString().TrimEnd('/').Equals(_settings.BaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                _client.Dispose();
                _client = new FFLogsClient(_settings.BaseUrl);
            }
        }

        private bool EnsureLoggedIn()
        {
            if (_client != null && _client.IsLoggedIn) return true;
            SetStatus(Loc.T("st.notLoggedIn"));
            return false;
        }

        private void SetStatus(string text)
        {
            // Output to the tab's log box, and to ACT's plugin-listing status label.
            PluginLog.Info(text);
            if (_statusLabel == null) return;
            try
            {
                if (_statusLabel.InvokeRequired)
                    _statusLabel.BeginInvoke((Action)(() => _statusLabel.Text = text));
                else
                    _statusLabel.Text = text;
            }
            catch { }
        }
    }
}
