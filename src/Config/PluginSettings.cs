using System;
using System.IO;
using System.Xml.Serialization;
using ACTLogsUploader.Upload;

namespace ACTLogsUploader.Config
{
    public enum FFLogsTarget
    {
        Global, // www.fflogs.com
        China,  // cn.fflogs.com
    }

    [Serializable]
    public sealed class PluginSettings
    {
        public Language Language { get; set; } = Language.Chinese;
        public FFLogsTarget Target { get; set; } = FFLogsTarget.China;
        public string Email { get; set; } = string.Empty;
        public byte[] EncryptedPassword { get; set; }
        public bool RememberCredentials { get; set; } = true;

        // serverOrRegion for create-report. Global: NA=1, EU=2, JP=3, OC=6. CN value is
        // undocumented; defaults to 1.
        public int Region { get; set; } = 1;

        // Region code passed to the JS parser (NA/EU/JP/OC/CN).
        public string RegionCode { get; set; } = "CN";

        // 0 = Public, 1 = Private, 2 = Unlisted.
        public int Visibility { get; set; } = 1;

        public string GuildId { get; set; } = string.Empty;

        // FFXIVLogs folder. Empty = auto-detect.
        public string LogDirectory { get; set; } = string.Empty;

        public bool UploadPreviousFights { get; set; } = true;

        [XmlIgnore]
        public string BaseUrl => Target == FFLogsTarget.China
            ? "https://cn.fflogs.com"
            : "https://www.fflogs.com";

        private string _cachedPassword;

        [XmlIgnore]
        public string Password
        {
            get
            {
                if (_cachedPassword == null)
                    _cachedPassword = Dpapi.Decrypt(EncryptedPassword) ?? string.Empty;
                return _cachedPassword;
            }
            set
            {
                _cachedPassword = value ?? string.Empty;
                EncryptedPassword = string.IsNullOrEmpty(value) ? null : Dpapi.Encrypt(value);
            }
        }

        [XmlIgnore]
        public static string DefaultConfigPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Advanced Combat Tracker", "Config", "ACTLogsUploader.config.xml");

        public static PluginSettings Load(string path = null)
        {
            path = path ?? DefaultConfigPath;
            try
            {
                if (File.Exists(path))
                {
                    using (var fs = File.OpenRead(path))
                    {
                        var ser = new XmlSerializer(typeof(PluginSettings));
                        return (PluginSettings)ser.Deserialize(fs) ?? new PluginSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.PluginLog.Warn($"Failed to load settings: {ex.Message}");
            }
            return new PluginSettings();
        }

        public void Save(string path = null)
        {
            path = path ?? DefaultConfigPath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var fs = File.Create(path))
                {
                    var ser = new XmlSerializer(typeof(PluginSettings));
                    ser.Serialize(fs, this);
                }
            }
            catch (Exception ex)
            {
                Logging.PluginLog.Warn($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
