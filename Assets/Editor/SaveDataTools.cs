using Game.Save;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 存档数据工具：编辑器阶段一键删除当前环境的存档（编辑器 = 项目根 TestSaves/save.json）。
    /// <para>
    /// 走 <see cref="SaveStorage" /> 的无路径 API（与运行时同一套介质分流逻辑），不自己拼路径；
    /// 删除前弹确认框显示位置与当前状态。Play 模式下删除时提醒：内存中的模型数据不会被清除，
    /// 下一次自动存档（设置 / 通关 / 成就变更即落盘）会立刻把进度重新写回。
    /// </para>
    /// </summary>
    public static class SaveDataTools
    {
        [MenuItem("Tools/存档/删除当前存档")]
        public static void DeleteCurrentSave()
        {
            string location = SaveStorage.DefaultPath;
            bool existed = SaveStorage.DefaultExists();
            bool playing = Application.isPlaying;

            var message = $"存档位置：{location}\n当前状态：{(existed ? "存在" : "不存在")}";
            if (playing)
                message += "\n\n当前处于 Play 模式：删除文件不会清掉内存中的进度，" +
                           "任何一次自动存档都会把它重新写回。建议停止 Play 后再删除。";
            if (!existed)
                message += "\n\n当前没有存档，执行无效果。";

            if (!EditorUtility.DisplayDialog("删除当前存档", message, "删除", "取消")) return;

            SaveStorage.DeleteDefault();

            Debug.Log($"[工具] 已删除当前存档：{location}（删除前存在={existed}，删除后存在={SaveStorage.DefaultExists()}）");
        }
    }
}
