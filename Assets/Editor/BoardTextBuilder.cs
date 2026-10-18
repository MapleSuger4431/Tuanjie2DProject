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
                ("LevelPlay", "出发"),
                ("LevelStarGoal", "★ 第三星：{0} 内通关并捡到金币"),
                ("LevelNextPiece", "下一块"),
                ("LevelStatusCharacter", "角色状态"),
                ("LevelCharacterName", "女巫"),
                ("LevelWalkable", "可走：{0}"),
                ("LevelSpawnDir", "出发朝向：{0}"),
                ("TileNameLand", "陆地"),
                ("TileNameFire", "火"),
                ("TileNameIce", "冰"),
            });

            Write(TableEn, new[]
            {
                ("LevelBoardHint", "Left click to place　R to rotate 90° clockwise　Scroll to zoom　Right drag to pan"),
                ("LevelPlay", "Play"),
                ("LevelStarGoal", "★ 3rd star: clear + coin within {0}"),
                ("LevelNextPiece", "Next Piece"),
                ("LevelStatusCharacter", "Character"),
                ("LevelCharacterName", "Witch"),
                ("LevelWalkable", "Walkable: {0}"),
                ("LevelSpawnDir", "Spawn facing: {0}"),
                ("TileNameLand", "Land"),
                ("TileNameFire", "Fire"),
                ("TileNameIce", "Ice"),
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
