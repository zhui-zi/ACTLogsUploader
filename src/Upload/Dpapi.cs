using System;
using System.Security.Cryptography;
using System.Text;

namespace ACTLogsUploader.Upload
{
    // DPAPI (current user) encryption for the stored password.
    internal static class Dpapi
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ACTLogsUploader.v1");

        public static byte[] Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return null;
            try
            {
                return ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            }
            catch (Exception ex)
            {
                Logging.PluginLog.Warn($"Password encrypt failed: {ex.Message}");
                return null;
            }
        }

        public static string Decrypt(byte[] blob)
        {
            if (blob == null || blob.Length == 0) return null;
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(blob, Entropy, DataProtectionScope.CurrentUser));
            }
            catch (Exception ex)
            {
                Logging.PluginLog.Warn($"Password decrypt failed: {ex.Message}");
                return null;
            }
        }
    }
}
