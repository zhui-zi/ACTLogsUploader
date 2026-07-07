using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using ACTLogsUploader.Logging;

namespace ACTLogsUploader
{
    // Single-DLL support: managed dependencies are embedded as embed/<name>.dll resources
    // and resolved from memory; the native ClearScriptV8.win-x64.dll is extracted to a temp
    // folder and located via ClearScript's AuxiliarySearchPath.
    internal static class Bootstrap
    {
        private const string ResourcePrefix = "embed/";
        private const string NativeLib = "ClearScriptV8.win-x64.dll";

        private static readonly ConcurrentDictionary<string, Assembly> Cache =
            new ConcurrentDictionary<string, Assembly>();
        private static bool _done;

        public static void Initialize()
        {
            if (_done) return;
            _done = true;

            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;
            try
            {
                var nativeDir = ExtractNative();
                ConfigureClearScript(nativeDir);
            }
            catch (Exception ex)
            {
                PluginLog.Error("Native V8 setup failed", ex);
            }
        }

        private static Assembly ResolveEmbedded(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            return Cache.GetOrAdd(name, n =>
            {
                try
                {
                    var self = typeof(Bootstrap).Assembly;
                    using (var st = self.GetManifestResourceStream(ResourcePrefix + n + ".dll"))
                    {
                        if (st == null) return null;
                        var bytes = ReadAll(st);
                        return Assembly.Load(bytes);
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Warn($"[Bootstrap] Failed to load {n}: {ex.Message}");
                    return null;
                }
            });
        }

        private static string ExtractNative()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ACTLogsUploader", "native");
            Directory.CreateDirectory(dir);

            var self = typeof(Bootstrap).Assembly;
            using (var st = self.GetManifestResourceStream(ResourcePrefix + NativeLib))
            {
                if (st == null) return Path.GetDirectoryName(self.Location);

                var bytes = ReadAll(st);
                var dest = Path.Combine(dir, NativeLib);
                if (!File.Exists(dest) || new FileInfo(dest).Length != bytes.Length)
                {
                    File.WriteAllBytes(dest, bytes);
                    PluginLog.Info($"[Bootstrap] Extracted {NativeLib} -> {dir}");
                }
            }
            return dir;
        }

        private static void ConfigureClearScript(string nativeDir)
        {
            Microsoft.ClearScript.HostSettings.AuxiliarySearchPath = nativeDir;
        }

        private static byte[] ReadAll(System.IO.Stream st)
        {
            var bytes = new byte[st.Length];
            int off = 0, r;
            while ((r = st.Read(bytes, off, bytes.Length - off)) > 0) off += r;
            return bytes;
        }
    }
}
