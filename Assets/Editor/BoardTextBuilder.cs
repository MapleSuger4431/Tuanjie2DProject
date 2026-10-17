using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Game.Editor
{
    /// <summary>
    /// 关卡内文案同步器：把底部操作提示行写进 GameText 字符串表（zh-CN / en）。
    /// <para>
    /// 文案真源在表里，界面运行时按 <see cref="Game.Core.Localization.Key" /> 取值——
    /// 改文案改这里再跑菜单，不要在预制体文本节点上手写。
    /// </para>
    /// </summary>
    public static class BoardTextBuilder
    {
        const string TableZh = "Assets/Localization/Tables/GameText_zh-CN.asset";
        const string TableEn = "Assets/Localization/Tables/GameText_en.asset";

        [MenuItem("Tools/关卡/同步关卡文案")]
        public static void Sync()
        {
            Write(TableZh, new[]
            {
                ("LevelBoardHint", "左键放置当前方块　R 键顺时针旋转 90°　滚轮缩放棋盘　右键平移"),
            });

            Write(TableEn, new[]
            {
                ("LevelBoardHint", "Left click to place　R to rotate 90° clockwise　Scroll to zoom　Right drag to pan"),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[关卡] 关卡文案已同步到 GameText 表");
        }

        static void Write(string tablePath, (string key, string value)[] entries)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);
            if (table == null)
            {
                Debug.LogError($"[BoardTextBuilder] 未找到字符串表 {tablePath}，关卡文案未写入");
                return;
            }

            foreach (var entry in entries) table.AddEntry(entry.key, entry.value);

            EditorUtility.SetDirty(table);

            // 键名（m_Key）存在 Shared Data 资产里：只标脏 StringTable 时新键不会真正落盘，
            // 域重载/重新进 Play 后运行时就报 "No translation found for '<key>'"
            var shared = table.SharedData;
            if (shared != null) EditorUtility.SetDirty(shared);
        }
    }
}
