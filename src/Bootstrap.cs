using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ACTLogsUploader
{
    // ACT can load plugins in a context that doesn't probe the plugin's own directory for
    // dependencies. Register the resolver before ClearScript is first used.
    internal static class Bootstrap
    {
#if SINGLEFILE
        private const string Prefix = "embed/";
        private const string Native = "ClearScriptV8.win-x64.dll";
        private const string IcuName = "ClearScript.V8.ICUData";
        private static Assembly _icu;
#else
        private static readonly HashSet<string> ManagedDependencies =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ClearScript.Core",
                "ClearScript.V8",
                "ClearScript.V8.ICUData",
                "Microsoft.Bcl.AsyncInterfaces",
                "Newtonsoft.Json",
                "System.Buffers",
                "System.Memory",
                "System.Numerics.Vectors",
                "System.Runtime.CompilerServices.Unsafe",
                "System.Text.Encodings.Web",
                "System.Text.Json",
                "System.Threading.Tasks.Extensions",
                "System.ValueTuple",
            };
#endif

        private static bool _done;

        public static void Init()
        {
            if (_done) return;
            _done = true;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        // Keep ClearScript references out of Init so ACT can instantiate the plugin before
        // managed and native V8 dependencies are needed.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PrepareV8()
        {
            try
            {
#if SINGLEFILE
                Microsoft.ClearScript.HostSettings.AuxiliarySearchPath = ExtractNative();
#else
                var pluginDir = GetPluginDirectory();
                if (!string.IsNullOrEmpty(pluginDir))
                    Microsoft.ClearScript.HostSettings.AuxiliarySearchPath = pluginDir;
#endif
            }
            catch (Exception ex)
            {
                Logging.PluginLog.Error("Dependency setup failed", ex);
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
#if SINGLEFILE
            if (name != IcuName) return null;
            if (_icu != null) return _icu;
            var bytes = ReadEmbedded(Prefix + IcuName + ".dll");
            return _icu = bytes != null ? Assembly.Load(bytes) : null;
#else
            if (!ManagedDependencies.Contains(name)) return null;
            var pluginDir = GetPluginDirectory();
            if (string.IsNullOrEmpty(pluginDir)) return null;
            var path = Path.Combine(pluginDir, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
#endif
        }

#if SINGLEFILE
        private static string ExtractNative()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ACTLogsUploader", "native");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, Native);
            var bytes = ReadEmbedded(Prefix + Native);
            if (bytes != null && (!File.Exists(dest) || new FileInfo(dest).Length != bytes.Length))
                File.WriteAllBytes(dest, bytes);
            return dir;
        }

        private static byte[] ReadEmbedded(string name)
        {
            using (var st = typeof(Bootstrap).Assembly.GetManifestResourceStream(name))
            {
                if (st == null) return null;
                var b = new byte[st.Length];
                int off = 0, r;
                while ((r = st.Read(b, off, b.Length - off)) > 0) off += r;
                return b;
            }
        }
#endif

        private static string GetPluginDirectory()
        {
            var assembly = typeof(Bootstrap).Assembly;
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location))
                return Path.GetDirectoryName(location);

            // Some ACT builds load plugin bytes directly, leaving Location and CodeBase empty.
            // Check the standard dedicated plugin folder relative to ACT's base/current folder.
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var currentDir = Environment.CurrentDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "Plugins", "ACTLogsUploader"),
                baseDir,
                Path.Combine(currentDir, "Plugins", "ACTLogsUploader"),
                currentDir,
            };
            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "ACTLogsUploader.dll")) &&
                    File.Exists(Path.Combine(candidate, "ClearScript.Core.dll")))
                    return candidate;
            }
            return null;
        }
    }
}
