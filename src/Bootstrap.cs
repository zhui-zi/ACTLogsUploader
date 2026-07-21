#if SINGLEFILE
using System;
using System.IO;
using System.Reflection;

namespace ACTLogsUploader
{
    // Single-file build only. Managed deps are IL-merged in; the native V8 and ICU data can't
    // be, so they're embedded: ICU data (a managed assembly loaded by name) is resolved from the
    // embedded copy, and the native lib is extracted to a temp folder that ClearScript's
    // AuxiliarySearchPath points at. Both load at V8 init, after ACT has discovered the plugin.
    internal static class Bootstrap
    {
        private const string Prefix = "embed/";
        private const string Native = "ClearScriptV8.win-x64.dll";
        private const string IcuName = "ClearScript.V8.ICUData";

        private static bool _done;
        private static Assembly _icu;

        public static void Init()
        {
            if (_done) return;
            _done = true;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try
            {
                Microsoft.ClearScript.HostSettings.AuxiliarySearchPath = ExtractNative();
            }
            catch (Exception ex)
            {
                Logging.PluginLog.Error("V8 native setup failed", ex);
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            if (new AssemblyName(args.Name).Name != IcuName) return null;
            if (_icu != null) return _icu;
            var bytes = ReadEmbedded(Prefix + IcuName + ".dll");
            return _icu = bytes != null ? Assembly.Load(bytes) : null;
        }

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
    }
}
#endif
