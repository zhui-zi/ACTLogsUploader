using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACTLogsUploader.Logging;

namespace ACTLogsUploader.Upload
{
    public static class LogFileHelper
    {
        public static string AutoDetectLogDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var actLogs = Path.Combine(appData, "Advanced Combat Tracker", "FFXIVLogs");
            return Directory.Exists(actLogs) ? actLogs : string.Empty;
        }

        public static string GetLatestLogFileFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (File.Exists(path)) return path;
            if (Directory.Exists(path))
            {
                try
                {
                    var files = Directory.GetFiles(path, "*.log");
                    if (files.Length > 0)
                        return files.Select(f => new FileInfo(f))
                                    .OrderByDescending(fi => fi.LastWriteTime)
                                    .First().FullName;
                }
                catch (Exception ex) { PluginLog.Warn($"[LogFileHelper] {ex.Message}"); }
            }
            return path;
        }

        // FileShare.ReadWrite so ACT can keep writing while we read.
        public static async Task<string[]> ReadAllLinesSharedAsync(string path)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
            {
                string line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    lines.Add(line);
            }
            return lines.ToArray();
        }

        public static async Task<(List<string> lines, long newPosition)> ReadNewLinesSharedAsync(string logPath, long position)
        {
            var lines = new List<string>();
            var newPosition = position;
            try
            {
                using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < position) newPosition = 0; // rotated/truncated
                    stream.Seek(newPosition, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                        {
                            var cleanLine = line.Replace("\0", "").Trim();
                            if (!string.IsNullOrEmpty(cleanLine))
                                lines.Add(cleanLine);
                        }
                        newPosition = stream.Position;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warn($"[LogFileHelper] Read error: {ex.Message}");
            }
            return (lines, newPosition);
        }
    }
}
