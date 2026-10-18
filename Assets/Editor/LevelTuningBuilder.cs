using System.Collections.Generic;
using Game.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// 五关难度曲线的唯一真源（菜单 Tools/关卡/重建五关难度配置，重跑即覆盖产物）。
    /// 设计口径（用户 2026-10-18 拍板）：
    /// <list type="bullet">
    /// <item>地图：10×10 → 15×15 → 15×15 → 20×20 → 20×20；</item>
    /// <item>三星时限：30/40/50/60/70 秒，每关 +10 秒，超时**不判负**只是拿不到第三颗星；</item>
    /// <item>血色深渊（<see cref="TileType.Mountain" />）：场地预置 0/3/3/3/5 个，方块可刷出 0/0/1/3/3 个（每块最多 1 格）；</item>
    /// <item>金币：1/1/2/2/2 枚，第二颗星要求全部捡到；</item>
    /// <item>属性反转牌：第 4 关才引入（第 1~3 关概率 0，第 4/5 关 0.35 / 0.3）。</item>
    /// </list>
    /// 本脚本只写配置资产与关卡场景的地形 / 金币，不改玩法代码。
    /// </summary>
    public static class LevelTuningBuilder
    {
        const string SceneFolder = "Assets/Scenes/";
        const string TemplateScene = "Assets/Scenes/Level_02.unity";

        const string PoolConfigPath = "Assets/Resources/Board/BoardTilePoolConfig.asset";
        const string StarConfigPath = "Assets/Resources/Board/LevelStarConfig.asset";
        const string CoinSpritePath = "Assets/Art/Board/Items/Item_Coin.png";

        /// <summary>关卡自带驱动器的根名：清理残留时必须保住它。</summary>
        const string BoardRuntimeRootName = "[BoardRuntime]";

        /// <summary>单关配置。</summary>
        sealed class LevelSpec
        {
            public string sceneName;
            public int size;
            public float timeStarSeconds;
            public int maxAbyssPerPiece = 1;
            public int maxAbyssPerRun;
            public float invertChance;
            public Vector2Int[] abyss = { };
            public Vector2Int[] coins = { };

            [Tooltip("是否重建地形：第一关已由他人在格子上钉了传送门效果，重建会把效果层冲掉，故默认跳过")]
            public bool rebuildTerrain = true;
        }

        /// <summary>
        /// 五关配置表。深渊位置避开起点（左下）与终点（右上），彼此也不贴在一起；
        /// 金币不与深渊重合（重合就永远捡不到）。
        /// </summary>
        static readonly LevelSpec[] Levels =
        {
            new LevelSpec
            {
                sceneName = "Level_01", size = 10, timeStarSeconds = 30,
                maxAbyssPerRun = 0, invertChance = 0f,
                abyss = new Vector2Int[] { },
                coins = new[] { new Vector2Int(3, 6) },
                rebuildTerrain = false,
            },
            new LevelSpec
            {
                sceneName = "Level_02", size = 15, timeStarSeconds = 40,
                maxAbyssPerRun = 0, invertChance = 0f,
                abyss = new[] { new Vector2Int(4, 3), new Vector2Int(10, 7), new Vector2Int(7, 12) },
                coins = new[] { new Vector2Int(7, 7) },
            },
            new LevelSpec
            {
                sceneName = "Level_03", size = 15, timeStarSeconds = 50,
                maxAbyssPerRun = 1, invertChance = 0f,
                abyss = new[] { new Vector2Int(3, 5), new Vector2Int(9, 9), new Vector2Int(12, 3) },
                coins = new[] { new Vector2Int(5, 4), new Vector2Int(11, 10) },
            },
            new LevelSpec
            {
                sceneName = "Level_04", size = 20, timeStarSeconds = 60,
                maxAbyssPerRun = 3, invertChance = 0.35f,
                abyss = new[] { new Vector2Int(5, 5), new Vector2Int(13, 8), new Vector2Int(8, 15) },
                coins = new[] { new Vector2Int(6, 6), new Vector2Int(14, 13) },
            },
            new LevelSpec
            {
                sceneName = "Level_05", size = 20, timeStarSeconds = 70,
                maxAbyssPerRun = 3, invertChance = 0.3f,
                abyss = new[]
                {
                    new Vector2Int(4, 4), new Vector2Int(9, 12), new Vector2Int(15, 6),
                    new Vector2Int(12, 17), new Vector2Int(17, 11),
                },
                coins = new[] { new Vector2Int(5, 9), new Vector2Int(12, 4) },
            },
        };

        [MenuItem("Tools/关卡/重建五关难度配置")]
        public static void Build()
        {
            TunePoolConfig();
            TuneStarConfig();

            foreach (var spec in Levels) BuildScene(spec);

            AssetDatabase.SaveAssets();
            Debug.Log("[LevelTuning] 五关难度配置已重建（地形 / 金币 / 随机表 / 三星时限）");
        }

        // ---------- 配置资产 ----------

        /// <summary>随机表：血色深渊刷新上限与反转牌概率（逐关覆盖，types 权重不在这里改）。</summary>
        static void TunePoolConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BoardTilePoolConfig>(PoolConfigPath);
            if (config == null)
            {
                Debug.LogError($"[LevelTuning] 找不到 {PoolConfigPath}");
                return;
            }

            var so = new SerializedObject(config);
            var levels = so.FindProperty("levels");

            foreach (var spec in Levels)
            {
                var index = IndexOfScene(levels, spec.sceneName);
                if (index < 0)
                {
                    levels.InsertArrayElementAtIndex(levels.arraySize);
                    index = levels.arraySize - 1;
                }

                var entry = levels.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("sceneName").stringValue = spec.sceneName;
                entry.FindPropertyRelative("maxAbyssPerPiece").intValue = spec.maxAbyssPerPiece;
                entry.FindPropertyRelative("maxAbyssPerRun").intValue = spec.maxAbyssPerRun;
                entry.FindPropertyRelative("invertChance").floatValue = spec.invertChance;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        /// <summary>三星时限：30 / 40 / 50 / 60 / 70 秒。</summary>
        static void TuneStarConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelStarConfig>(StarConfigPath);
            if (config == null)
            {
                Debug.LogError($"[LevelTuning] 找不到 {StarConfigPath}");
                return;
            }

            var so = new SerializedObject(config);
            so.FindProperty("defaultTimeStarSeconds").floatValue = Levels[0].timeStarSeconds;

            var levels = so.FindProperty("levels");
            levels.ClearArray();

            for (var i = 0; i < Levels.Length; i++)
            {
                levels.InsertArrayElementAtIndex(i);

                var entry = levels.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sceneName").stringValue = Levels[i].sceneName;
                entry.FindPropertyRelative("timeStarSeconds").floatValue = Levels[i].timeStarSeconds;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        static int IndexOfScene(SerializedProperty array, string sceneName)
        {
            for (var i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                var name = entry.FindPropertyRelative("sceneName");
                if (name != null && name.stringValue == sceneName) return i;
            }

            return -1;
        }

        // ---------- 关卡场景 ----------

        static void BuildScene(LevelSpec spec)
        {
            var path = SceneFolder + spec.sceneName + ".unity";
            var existed = System.IO.File.Exists(path);

            // 已存在的关卡就地改，避免把别人配的效果层冲掉；没有的关卡从第二关复制结构另存
            var scene = existed
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.OpenScene(TemplateScene, OpenSceneMode.Single);

            RemoveRuntimeLeftovers(scene);

            var board = GameObject.Find("Board");
            if (board == null)
            {
                Debug.LogError($"[LevelTuning] {spec.sceneName} 里找不到 Board 物体，跳过");
                return;
            }

            if (spec.rebuildTerrain) ApplyTerrain(board, spec);
            else Debug.Log($"[LevelTuning] {spec.sceneName} 跳过地形重建（保留格子上的预置效果）");

            ApplyCoins(board, spec);

            // 自检：驱动器必须还在，否则关卡进得去却没有任何 UI，且不报错
            if (GameObject.Find(BoardRuntimeRootName) == null)
                Debug.LogError($"[LevelTuning] {spec.sceneName} 缺少驱动器根 {BoardRuntimeRootName}，请检查清理逻辑");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path, false);
            RegisterBuildSettings(path);

            Debug.Log($"[LevelTuning] {spec.sceneName}（{(existed ? "已存在·更新" : "新建")}）{spec.size}×{spec.size}，" +
                      $"预置血色深渊 {spec.abyss.Length} 个，金币 {spec.coins.Length} 枚，三星时限 {spec.timeStarSeconds}s");
        }

        /// <summary>地形：整盘清空后只写预置的血色深渊，其余交给玩家铺。</summary>
        static void ApplyTerrain(GameObject board, LevelSpec spec)
        {
            var painter = board.GetComponent<BoardLevelPainter>();
            if (painter != null)
            {
                painter.Resize(spec.size);
                painter.ClearStates();

                foreach (var cell in spec.abyss) painter.SetState(cell.x, cell.y, (int)TileType.Mountain);

                painter.ApplyToTiles();
            }

            var grid = board.GetComponent<BoardGridView>();
            grid?.ApplyTileSprites();
        }

        /// <summary>金币：按配置的枚数重建（多枚关卡每枚一个子物体）。</summary>
        static void ApplyCoins(GameObject board, LevelSpec spec)
        {
            // 先清掉旧的（含上一版留下的单枚 Coin），再按当前配置重建，保证枚数严格等于配置
            for (var i = board.transform.childCount - 1; i >= 0; i--)
            {
                var child = board.transform.GetChild(i);
                if (child != null && child.name.StartsWith("Coin")) Object.DestroyImmediate(child.gameObject);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoinSpritePath);
            if (sprite == null) Debug.LogWarning($"[LevelTuning] 找不到金币图 {CoinSpritePath}");

            for (var i = 0; i < spec.coins.Length; i++)
            {
                var go = new GameObject("Coin_" + (i + 1));
                go.transform.SetParent(board.transform, false);

                var coin = go.AddComponent<BoardCoin>();
                var so = new SerializedObject(coin);

                so.FindProperty("cell").vector2IntValue = spec.coins[i];
                if (sprite != null) so.FindProperty("coinSprite").objectReferenceValue = sprite;

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>删掉误存进场景的运行时根（UIRoot / 框架模块 / 面板克隆），否则新关会带着别人的 UI 进场。</summary>
        static void RemoveRuntimeLeftovers(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == null) continue;

                var name = go.name;

                // [BoardRuntime] 是关卡自己的驱动器（BoardScreenLayout + BoardRuntimeDriver），必须留下——
                // 它名字也带方括号，误删会让关卡进不去且没有任何报错
                if (name == BoardRuntimeRootName) continue;

                var leftover = name.StartsWith("[") || name.Contains("(Clone)");
                if (!leftover) continue;

                Object.DestroyImmediate(go);
                Debug.Log($"[LevelTuning] 移除残留根：{name}");
            }
        }

        static void RegisterBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var scene in scenes)
                if (scene != null && scene.path == path) return;

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
