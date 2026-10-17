using System;
using System.IO;
using UnityEngine;

namespace SaveTest
{
    /// <summary>
    /// 存档落盘：JsonUtility 序列化 + 先写临时文件再替换（避免写一半中断导致存档损坏）。
    /// </summary>
    public static class SaveStorage
    {
        public static string DefaultPath =>
            Path.Combine(Application.persistentDataPath, "SaveTest", "save.json");

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
                Debug.LogError($"[SaveTest] 存档解析失败：{e.Message}");
                return null;
            }
        }

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
