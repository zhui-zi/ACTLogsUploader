using System.Collections.Generic;

namespace ACTLogsUploader.Config
{
    public enum Language { English, Chinese }

    // UI string table. T(key) returns the current language's text; missing keys return the key.
    public static class Loc
    {
        public static Language Current = Language.Chinese;

        private static readonly Dictionary<string, string[]> M = new Dictionary<string, string[]>
        {
            // labels
            ["lbl.language"] = new[] { "Language", "语言" },
            ["lbl.target"] = new[] { "Target", "目标" },
            ["lbl.email"] = new[] { "Email", "邮箱" },
            ["lbl.password"] = new[] { "Password", "密码" },
            ["lbl.region"] = new[] { "Region", "区域" },
            ["lbl.visibility"] = new[] { "Visibility", "可见性" },
            ["lbl.uploadTo"] = new[] { "Upload to", "上传到" },
            ["lbl.logFolder"] = new[] { "Log folder", "日志文件夹" },
            ["lbl.description"] = new[] { "Description", "备注" },
            ["lbl.log"] = new[] { "Log", "日志" },
            // buttons
            ["btn.save"] = new[] { "Save", "保存" },
            ["btn.login"] = new[] { "Login", "登录" },
            ["btn.uploadLatest"] = new[] { "Upload latest log", "上传最新日志" },
            ["btn.uploadFile"] = new[] { "Upload file...", "上传指定文件..." },
            ["btn.startLive"] = new[] { "Start live", "开始实时" },
            ["btn.stopLive"] = new[] { "Stop live", "停止实时" },
            // checkboxes
            ["chk.remember"] = new[] { "Remember credentials (password stored DPAPI-encrypted)", "记住账号密码（密码经 DPAPI 加密存储）" },
            ["chk.uploadPrev"] = new[] { "Include existing fights in the log when uploading / going live", "上传/实时时包含日志中已有的战斗" },
            // combo items
            ["target.global"] = new[] { "Global (fflogs.com)", "国际服 (fflogs.com)" },
            ["target.china"] = new[] { "China 国服 (cn.fflogs.com)", "国服 (cn.fflogs.com)" },
            ["region.cn"] = new[] { "CN (国服)", "国服 (CN)" },
            ["vis.public"] = new[] { "Public", "公开" },
            ["vis.private"] = new[] { "Private", "私密" },
            ["vis.unlisted"] = new[] { "Unlisted", "不公开" },
            ["guild.personal"] = new[] { "Personal Logs", "个人日志" },
            // status messages
            ["st.ready"] = new[] { "Ready. Target={0}. Not logged in.", "就绪。目标={0}。未登录。" },
            ["st.initFailed"] = new[] { "Init failed: {0}", "初始化失败：{0}" },
            ["st.unloaded"] = new[] { "Unloaded.", "已卸载。" },
            ["st.enterCreds"] = new[] { "Enter email and password first.", "请先填写邮箱和密码。" },
            ["st.loggingIn"] = new[] { "Logging in...", "正在登录..." },
            ["st.loggedInAs"] = new[] { "Logged in as {0} ({1}).", "已登录：{0}（{1}）。" },
            ["st.loginFailed"] = new[] { "Login failed - see log.", "登录失败，见下方日志。" },
            ["st.noLogFolder"] = new[] { "No FFXIVLogs folder found.", "未找到 FFXIVLogs 文件夹。" },
            ["st.logFileNotFound"] = new[] { "Log file not found.", "未找到日志文件。" },
            ["st.uploading"] = new[] { "Uploading {0}...", "正在上传 {0}..." },
            ["st.uploaded"] = new[] { "Uploaded: {0}", "已上传：{0}" },
            ["st.uploadFailed"] = new[] { "Upload failed: {0}", "上传失败：{0}" },
            ["st.liveStarted"] = new[] { "Live logging started.", "实时上传已开始。" },
            ["st.liveStopping"] = new[] { "Live logging stopping...", "实时上传停止中..." },
            ["st.notLoggedIn"] = new[] { "Not logged in - click Login first.", "未登录，请先点击登录。" },
            ["st.settingsSaved"] = new[] { "Settings saved.", "设置已保存。" },
            ["st.error"] = new[] { "Error: {0}", "出错：{0}" },
        };

        public static string T(string key, params object[] args)
        {
            var text = M.TryGetValue(key, out var v)
                ? (Current == Language.Chinese ? v[1] : v[0])
                : key;
            return args != null && args.Length > 0 ? string.Format(text, args) : text;
        }
    }
}
