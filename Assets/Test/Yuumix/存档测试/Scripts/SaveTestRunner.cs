using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SaveTest
{
    /// <summary>
    /// 存档流程自检（Play Mode 下直接调用 SaveTestRunner.RunRoundTrip() 即可）：
    /// 造数据 → 按模块顺序存档 → 模拟重开 → 按模块顺序读档 → 断言
    /// 「存档字段被恢复 / 未存档的运行时状态不被存档污染」。
    /// </summary>
    public static class SaveTestRunner
    {
        /// <summary>按模块顺序装配存档流程（以后新增模块：这里 Register 一行）。</summary>
        public static SaveDirector CreateDirector()
        {
            var context = SaveTestContext.Instance;
            return new SaveDirector()
                .Register(new PlaySaveStep(context.GetModel<PlayModel>()))
                .Register(new SettingsSaveStep(context.GetModel<SettingsModel>()));
        }

        public static string RunRoundTrip(string path = null)
        {
            path = string.IsNullOrEmpty(path) ? SaveStorage.DefaultPath : path;
            var log = new StringBuilder();
            var allOk = true;

            SaveStorage.Delete(path);
            ResetContext(); // 清掉上次运行残留（Context 是纯 C# 单例，不随场景卸载销毁）

            // 1) 造一批数据：既有需要存档的，也有纯运行时状态
            var context = SaveTestContext.Instance;
            var play = context.GetModel<PlayModel>();
            var settings = context.GetModel<SettingsModel>();

            play.BeginSession("S-1");
            play.PlayerName.Value = "Hero";
            play.Level.Value = 7;
            play.AddScore(1234);
            play.ClearLevel(3);
            play.ClearLevel(5);
            play.IsPaused = true; // 不存档的临时状态
            settings.BgmVolume.Value = 0.35f;
            settings.SfxVolume.Value = 0.8f;
            settings.Language.Value = "en-US";

            // 2) 按注册顺序逐模块存档：PlayModel → PlayData、SettingsModel → SettingsData
            CreateDirector().Save(path);
            log.AppendLine("—— 存档文件内容 ——");
            log.AppendLine(File.ReadAllText(path));

            // 3) 模拟退出重开：释放 Context，Model 回到默认值；并把存档字段改成干扰值
            ResetContext();
            context = SaveTestContext.Instance;
            play = context.GetModel<PlayModel>();
            settings = context.GetModel<SettingsModel>();

            play.BeginSession("S-2"); // 新会话：会话级状态不属于存档
            play.PlayerName.Value = "Nobody";
            play.Level.Value = 1;
            play.Score.Value = 0;
            settings.BgmVolume.Value = 1f;
            settings.Language.Value = "zh-CN";

            // 4) 按注册顺序逐模块读档：PlayData → PlayModel、SettingsData → SettingsModel
            var loaded = CreateDirector().Load(path);
            allOk &= Expect(log, "读到存档文件", true, loaded);

            // 5) 断言：进了存档的字段被恢复
            allOk &= Expect(log, "play.playerName", "Hero", play.PlayerName.Value);
            allOk &= Expect(log, "play.level", 7, play.Level.Value);
            allOk &= Expect(log, "play.score", 1234, play.Score.Value);
            allOk &= Expect(log, "play.clearedLevels", "3,5", string.Join(",", play.ClearedLevels));
            allOk &= Expect(log, "settings.bgmVolume", 0.35f, settings.BgmVolume.Value);
            allOk &= Expect(log, "settings.sfxVolume", 0.8f, settings.SfxVolume.Value);
            allOk &= Expect(log, "settings.language", "en-US", settings.Language.Value);

            // 6) 断言：没进存档的字段保持本次会话的值（未被存档覆盖）
            allOk &= Expect(log, "play.sessionId（不存档）", "S-2", play.SessionId);
            allOk &= Expect(log, "play.combo（不存档）", 0, play.Combo);
            allOk &= Expect(log, "play.isPaused（不存档）", false, play.IsPaused);

            log.Insert(0, $"[SaveTest] 存档往返自检：{(allOk ? "PASS" : "FAIL")}\n");
            return log.ToString();
        }

        static void ResetContext()
        {
            try
            {
                SaveTestContext.Instance.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveTest] 重置 Context 失败，改用现有实例：{e.Message}");
            }
        }

        static bool Expect(StringBuilder log, string name, object expected, object actual)
        {
            var ok = Equals(expected == null ? null : expected.ToString(), actual == null ? null : actual.ToString());
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}：期望 {expected} / 实际 {actual}");
            return ok;
        }
    }
}
