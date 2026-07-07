using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using ACTLogsUploader.Config;
using ACTLogsUploader.Logging;
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

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            _statusLabel = pluginStatusText;
            try
            {
                _settings = PluginSettings.Load();
                _configTab = new ConfigTab(this, _settings);
                _configTab.Build(pluginScreenSpace);
                SetStatus($"Ready. Target={_settings.Target}. Not logged in.");
            }
            catch (Exception ex)
            {
                SetStatus("Init failed: " + ex.Message);
                PluginLog.Error("InitPlugin failed", ex);
            }
        }

        public void DeInitPlugin()
        {
            try
            {
                _settings?.Save();
                _client?.StopLiveLog();
                _client?.Dispose();
                _client = null;
                PluginLog.Sink = null;
                SetStatus("Unloaded.");
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
                SetStatus("Enter email and password first.");
                return false;
            }
            SetStatus("Logging in...");
            var ok = await _client.LoginAsync(_settings.Email, _settings.Password);
            SetStatus(ok ? $"Logged in as {_client.Username} ({_settings.Target})." : "Login failed - see log.");
            return ok;
        }

        public Task UploadLatestAsync(string description)
        {
            var dir = ResolveLogDirectory();
            if (string.IsNullOrEmpty(dir)) { SetStatus("No FFXIVLogs folder found."); return Task.CompletedTask; }
            var logPath = LogFileHelper.GetLatestLogFileFromPath(dir);
            return UploadFileAsync(logPath, description);
        }

        public async Task UploadFileAsync(string logPath, string description)
        {
            if (!EnsureLoggedIn()) return;
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath)) { SetStatus("Log file not found."); return; }
            try
            {
                SetStatus($"Uploading {Path.GetFileName(logPath)}...");
                var code = await _client.UploadLogAsync(
                    logPath, _settings.Region, _settings.RegionCode, _settings.Visibility,
                    _settings.GuildId, description ?? "");
                SetStatus($"Uploaded: {_settings.BaseUrl}/reports/{code}");
            }
            catch (Exception ex)
            {
                PluginLog.Error("Upload failed", ex);
                SetStatus("Upload failed: " + ex.Message);
            }
        }

        public void StartLive(string description)
        {
            if (!EnsureLoggedIn()) return;
            var dir = ResolveLogDirectory();
            if (string.IsNullOrEmpty(dir)) { SetStatus("No FFXIVLogs folder found."); return; }
            _client.StartLiveLog(dir, _settings.Region, _settings.RegionCode, _settings.Visibility,
                _settings.GuildId, description ?? "", _settings.UploadPreviousFights);
            SetStatus("Live logging started.");
        }

        public void StopLive()
        {
            _client?.StopLiveLog();
            SetStatus("Live logging stopping...");
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
            SetStatus("Not logged in - click Login first.");
            return false;
        }

        private void SetStatus(string text)
        {
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
