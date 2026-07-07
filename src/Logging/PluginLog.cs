using System;

namespace ACTLogsUploader.Logging
{
    // Routes to the config-tab log box (via Sink) and Debug output.
    public static class PluginLog
    {
        public static Action<string> Sink;
        public static bool VerboseEnabled = false;

        public static void Debug(string message) { if (VerboseEnabled) Write("DEBUG", message); }
        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(string message, Exception ex) => Write("ERROR", $"{message} :: {Flatten(ex)}");

        private static string Flatten(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (sb.Length > 0) sb.Append(" -> ");
                sb.Append(e.GetType().Name).Append(": ").Append(e.Message);
            }
            return sb.ToString();
        }

        private static void Write(string level, string message)
        {
            var line = $"[ACTLogsUploader] {DateTime.Now:HH:mm:ss} {level}  {message}";
            System.Diagnostics.Debug.WriteLine(line);
            try { Sink?.Invoke(line); } catch { }
        }
    }
}
