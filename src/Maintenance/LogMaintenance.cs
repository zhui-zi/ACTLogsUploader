using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ACTLogsUploader.Logging;

namespace ACTLogsUploader.Maintenance
{
    // Local combat-log housekeeping: archive old logs to zips and delete old archives.
    public static class LogMaintenance
    {
        public static string ArchiveDir(string logDir) => Path.Combine(logDir, "Archive");

        // Zip every *.log not modified within olderThanDays into ArchiveDir, then delete the original.
        public static int ArchiveOldLogs(string logDir, int olderThanDays)
        {
            if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir)) return 0;
            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, olderThanDays));
            var archiveDir = ArchiveDir(logDir);
            Directory.CreateDirectory(archiveDir);

            int count = 0;
            foreach (var log in Directory.GetFiles(logDir, "*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(log) > cutoff) continue;
                    var zipPath = Path.Combine(archiveDir, Path.GetFileNameWithoutExtension(log) + ".zip");
                    if (File.Exists(zipPath)) zipPath = Path.Combine(archiveDir,
                        Path.GetFileNameWithoutExtension(log) + "_" + DateTime.UtcNow.Ticks + ".zip");

                    using (var fs = File.Create(zipPath))
                    using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        var entry = zip.CreateEntry(Path.GetFileName(log), CompressionLevel.Optimal);
                        using (var es = entry.Open())
                        using (var ls = File.OpenRead(log))
                            ls.CopyTo(es);
                    }
                    File.Delete(log);
                    count++;
                }
                catch (Exception ex) { PluginLog.Warn($"[Archive] {Path.GetFileName(log)}: {ex.Message}"); }
            }
            if (count > 0) PluginLog.Info($"[Archive] Archived {count} log(s)");
            return count;
        }

        // Delete archived zips older than olderThanDays (0 = delete all).
        public static int DeleteArchived(string logDir, int olderThanDays)
        {
            var archiveDir = ArchiveDir(logDir);
            if (!Directory.Exists(archiveDir)) return 0;
            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, olderThanDays));

            int count = 0;
            foreach (var zip in Directory.GetFiles(archiveDir, "*.zip"))
            {
                try
                {
                    if (olderThanDays > 0 && File.GetLastWriteTimeUtc(zip) > cutoff) continue;
                    File.Delete(zip);
                    count++;
                }
                catch (Exception ex) { PluginLog.Warn($"[Archive] delete {Path.GetFileName(zip)}: {ex.Message}"); }
            }
            if (count > 0) PluginLog.Info($"[Archive] Deleted {count} archived log(s)");
            return count;
        }

        // Split a log into parts of about maxBytes each, prepending the file's setup lines
        // (zone/player/combatant records before the first ability line) so each part parses.
        public static int SplitLog(string logPath, long maxBytes)
        {
            if (!File.Exists(logPath) || maxBytes < 1_000_000) return 0;
            var lines = File.ReadAllLines(logPath);
            var header = lines.TakeWhile(IsSetupLine).ToArray();

            var dir = Path.GetDirectoryName(logPath);
            var baseName = Path.GetFileNameWithoutExtension(logPath);

            int part = 0, i = header.Length;
            while (i < lines.Length)
            {
                part++;
                var outPath = Path.Combine(dir, $"{baseName}.part{part}.log");
                long written = 0;
                using (var w = new StreamWriter(outPath, false))
                {
                    foreach (var h in header) { w.WriteLine(h); written += h.Length + 2; }
                    for (; i < lines.Length; i++)
                    {
                        w.WriteLine(lines[i]);
                        written += lines[i].Length + 2;
                        if (written >= maxBytes) { i++; break; }
                    }
                }
            }
            PluginLog.Info($"[Split] {Path.GetFileName(logPath)} -> {part} part(s)");
            return part;
        }

        private static bool IsSetupLine(string line)
        {
            // ACT line codes: 01 ZoneChange, 02 ChangePrimaryPlayer, 03 AddCombatant, 12 PlayerStats.
            return line.StartsWith("01|") || line.StartsWith("02|") ||
                   line.StartsWith("03|") || line.StartsWith("12|");
        }
    }
}
