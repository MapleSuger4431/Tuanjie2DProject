using System.IO;
using Game.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Game.Editor
{
    /// <summary>
    /// 成就库生成器：建（或重建）Assets/Resources/Achievement/ 下的成就定义资产与配置库，
    /// 并把成就名 / 描述与图鉴固定文案写进 GameText 表（zh-CN / en）。
    /// <para>
    /// 新增成就 = 在 <see cref="Definitions" /> 里加一条（含图标 PNG）+ 表里补键；
    /// 判定与 UI 都不用改代码。重跑即覆盖产物。
    /// </para>
    /// </summary>
    public static class AchievementLibraryBuilder
    {
        const string Dir = "Assets/Resources/Achievement";
        const string LibraryPath = Dir + "/AchievementLibrary.asset";
        const string IconDir = "Assets/Art/Achievement";

        const string TableZh = "Assets/Localization/Tables/GameText_zh-CN.asset";
        const string TableEn = "Assets/Localization/Tables/GameText_en.asset";

        /// <summary>成就定义表：顺序即图鉴展示顺序。</summary>
        static readonly (string file, string id, string titleKey, string descKey, string icon, string eventKey,
            int threshold, bool hidden)[] Definitions =
        {
            ("Ach_Steps30", "steps_30", "AchSteps30Name", "AchSteps30Desc", "Ach_Footsteps.png",
                AchievementEvents.StepsInLevel, 30, false),
            ("Ach_FirstClear", "first_clear", "AchFirstClearName", "AchFirstClearDesc", "Ach_Banner.png",
                AchievementEvents.LevelCleared, 1, false),
            ("Ach_ElementSwitch", "element_switch", "AchElementSwitchName", "AchElementSwitchDesc", "Ach_TwinMask.png",
                AchievementEvents.ElementSwitched, 1, false),
            ("Ach_Clear3", "clear_3", "AchClear3Name", "AchClear3Desc", "Ach_Stars.png",
                AchievementEvents.TotalClears, 3, false),
        };

        [MenuItem("Tools/成就/重建成就库")]
        public static void Rebuild()
        {
            SyncTexts();
            ConfigureIcons();
            Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[成就] 成就库已重建：{LibraryPath}（{Definitions.Length} 条）");
        }

        static void Build()
        {
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

            var assets = new System.Collections.Generic.List<GameAchievement>();
            foreach (var def in Definitions)
            {
                var path = $"{Dir}/{def.file}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<GameAchievement>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<GameAchievement>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                var so = new SerializedObject(asset);
                so.FindProperty("id").stringValue = def.id;
                so.FindProperty("titleKey").stringValue = def.titleKey;
                so.FindProperty("descKey").stringValue = def.descKey;
                so.FindProperty("eventKey").stringValue = def.eventKey;
                so.FindProperty("threshold").intValue = def.threshold;
                so.FindProperty("compare").enumValueIndex = (int)AchievementCompare.GreaterOrEqual;
                so.FindProperty("hidden").boolValue = def.hidden;

                var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{def.icon}");
                if (icon == null) Debug.LogError($"[成就] 缺少图标 {IconDir}/{def.icon}");
                so.FindProperty("icon").objectReferenceValue = icon;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                assets.Add(asset);
            }

            var library = AssetDatabase.LoadAssetAtPath<AchievementLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AchievementLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var librarySo = new SerializedObject(library);
            var items = librarySo.FindProperty("items");
            items.arraySize = assets.Count;
            for (var i = 0; i < assets.Count; i++)
                items.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];

            librarySo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);

            Debug.Log($"[成就] 库条目 {assets.Count} 条，图标检查：{string.Join(", ", IconReport())}");
        }

        static string[] IconReport()
        {
            var report = new System.Collections.Generic.List<string>();
            foreach (var def in Definitions)
            {
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{def.icon}");
                report.Add($"{def.id}={icon != null}");
            }

            return report.ToArray();
        }

        /// <summary>四枚成就图标统一配成透明 Sprite（无 mipmap、256 上限）。</summary>
        static void ConfigureIcons()
        {
            foreach (var def in Definitions)
            {
                var path = $"{IconDir}/{def.icon}";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[成就] 拿不到图标导入器：{path}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
        }

        // ---------- 文案（GameText 表） ----------

        /// <summary>写成就与图鉴文案到 zh-CN / en 两张表（图鉴面板生成器会复用）。</summary>
        public static void SyncTexts()
        {
            Write(TableZh, new[]
            {
                ("MenuAchievements", "成就图鉴"),
                ("AchievementTitle", "成就图鉴"),
                ("AchievementCompleted", "完成度：{0}/{1}"),
                ("AchievementLocked", "未解锁"),
                ("AchievementUnlockedAt", "{0} 解锁"),
                ("AchievementHidden", "???"),
                ("AchievementClose", "关闭"),
                ("AchSteps30Name", "三十步行者"),
                ("AchSteps30Desc", "单关内让女巫走满 30 步"),
                ("AchFirstClearName", "初战告捷"),
                ("AchFirstClearDesc", "通关任意一关"),
                ("AchElementSwitchName", "双面魔女"),
                ("AchElementSwitchDesc", "在一局内触发元素属性反转"),
                ("AchClear3Name", "渐入佳境"),
                ("AchClear3Desc", "累计通关 3 次"),
            });

            Write(TableEn, new[]
            {
                ("MenuAchievements", "Achievements"),
                ("AchievementTitle", "Achievements"),
                ("AchievementCompleted", "Completed: {0}/{1}"),
                ("AchievementLocked", "Locked"),
                ("AchievementUnlockedAt", "Unlocked {0}"),
                ("AchievementHidden", "???"),
                ("AchievementClose", "Close"),
                ("AchSteps30Name", "Thirty-Step Walker"),
                ("AchSteps30Desc", "Walk 30 steps in a single level"),
                ("AchFirstClearName", "First Clear"),
                ("AchFirstClearDesc", "Clear any level"),
                ("AchElementSwitchName", "Two-Faced Witch"),
                ("AchElementSwitchDesc", "Trigger an element switch in one run"),
                ("AchClear3Name", "Getting the Hang of It"),
                ("AchClear3Desc", "Clear levels 3 times in total"),
            });
        }

        static void Write(string tablePath, (string key, string value)[] entries)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);
            if (table == null)
            {
                Debug.LogError($"[成就] 未找到字符串表 {tablePath}，成就文案未写入");
                return;
            }

            foreach (var entry in entries) table.AddEntry(entry.key, entry.value);

            EditorUtility.SetDirty(table);

            // 键名（m_Key）存在 Shared Data 资产里：只标脏 StringTable 时新键不落盘
            var shared = table.SharedData;
            if (shared != null) EditorUtility.SetDirty(shared);
        }
    }
}
