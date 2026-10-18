using System;
using System.IO;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 存档落盘：JsonUtility 序列化。存储介质按运行环境分流——两边介质不同，天然隔离，
    /// 避免编辑器测试档经共享的 persistentDataPath 泄漏进打包版（同公司名/产品名时两者路径相同）：
    /// <list type="bullet">
    /// <item>编辑器（测试）→ 项目根 <c>TestSaves/</c>（与 Assets 同级，随项目走，不进 Git）。</item>
    /// <item>打包版 → PlayerPrefs（macOS 为 plist、Windows 为注册表，玩家机器上不落可见文件）。</item>
    /// </list>
    /// 文件分支沿用「先写临时文件再替换」，避免写一半中断导致存档损坏。
    /// </summary>
    public static class SaveStorage
    {
        const string PlayerPrefsKey = "Game.Save.SaveGame";

        /// <summary>测试注入点：非空时强制走对应介质（仅测试脚本使用，游戏代码不要碰）。</summary>
        internal static bool? usePlayerPrefsOverride;

        /// <summary>当前是否走 PlayerPrefs 介质。</summary>
        public static bool UsePlayerPrefs => usePlayerPrefsOverride ?? !Application.isEditor;

        /// <summary>当前存档位置（诊断/日志用；打包版无文件路径，返回键名）。</summary>
        public static string DefaultPath => UsePlayerPrefs
            ? $"PlayerPrefs[{PlayerPrefsKey}]"
            : Path.Combine(ProjectRoot, "TestSaves", "save.json");

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>当前环境是否已有存档。</summary>
        public static bool DefaultExists() => UsePlayerPrefs
            ? PlayerPrefs.HasKey(PlayerPrefsKey)
            : File.Exists(DefaultPath);

        public static void WriteDefault(SaveGame root)
        {
            if (UsePlayerPrefs)
            {
                PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(root, true));
                PlayerPrefs.Save();
                return;
            }

            Write(DefaultPath, root);
        }

        public static SaveGame ReadDefault()
        {
            if (!UsePlayerPrefs) return Read(DefaultPath);

            var json = PlayerPrefs.GetString(PlayerPrefsKey, null);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonUtility.FromJson<SaveGame>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 存档解析失败：{e.Message}");
                return null;
            }
        }

        public static void DeleteDefault()
        {
            if (UsePlayerPrefs)
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
                PlayerPrefs.Save();
                return;
            }

            Delete(DefaultPath);
        }

        // —— 文件介质原语（编辑器 TestSaves 分支在用）——

        public static void Write(string path, SaveGame root)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(root, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static SaveGame Read(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                return JsonUtility.FromJson<SaveGame>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 存档解析失败：{e.Message}");
                return null;
            }
        }

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
