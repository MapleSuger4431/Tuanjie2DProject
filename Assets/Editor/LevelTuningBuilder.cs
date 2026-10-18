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
    /// <item>初始路网：每关一套**固定**路网（改玩法不改路，方便刷分刷时间），可通行格（路/火/冰）≥30%，
    /// 用户 2026-10-18 拍板「不能一进关全是深渊」；路网含少量火/冰格。</item>
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
        const string PortalAssetPath = "Assets/Resources/Board/Effects/Portal.asset";

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

            [Tooltip("起点格；null = 不改（保留画笔现状，默认左下角 (0,0)）")]
            public Vector2Int? startCell;

            [Tooltip("终点格；null = 不改（保留画笔现状，默认右上角）")]
            public Vector2Int? goalCell;

            [Tooltip("预置传送门，两两成对：每两个坐标构成一对互传端点（踩到任一端传到另一端，两端同时消耗）。" +
                     "第 4 关起引入（用户 2026-10-18 拍板：第 4/5 关各设计两个传送门格子）")]
            public Vector2Int[] portals = { };

            [Tooltip("固定初始路网，每关一套（用户 2026-10-18 拍板：进关不能满屏深渊，可通行格须 ≥30%，" +
                     "且路网固定不变，方便刷分刷时间）。第一行 = 最上面一行（y 最大），" +
                     "字符：. 深渊 / L 路 / F 火 / I 冰 / M 血色深渊（与 abyss 填同一批坐标即可）")]
            public string[] road = { };

            [Tooltip("是否重建地形：第一关地形已定稿默认跳过（预置效果改由 portals 配置统一管理，重建地形也不会冲掉它）")]
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
                road = new[]
                {
                    "....LLLLL.", "..LLL...L.", "..L.....L.", "...LLL.L..", ".LL.L.....",
                    ".L..L..FF.", ".L.LL.....", ".L....II..", ".LLL......", ".LLLLL....",
                },
            },
            new LevelSpec
            {
                sceneName = "Level_02", size = 15, timeStarSeconds = 40,
                maxAbyssPerRun = 0, invertChance = 0f,
                abyss = new[] { new Vector2Int(4, 3), new Vector2Int(10, 7), new Vector2Int(7, 12) },
                coins = new[] { new Vector2Int(7, 7) },
                road = new[]
                {
                    "............LL.", "...IILLLL.LLLL.", ".......M.......", ".............L.", "..LILL.LLLLLL..",
                    "..L...LL.......", "..L............", ".LLLL.LLLLMLLL.", ".....LL..L.....", ".........L.LL..",
                    ".LLL.LLFFL.LLL.", ".L..M..........", ".L.LL..........", ".LLFFLL.LLLLL..", ".LL............",
                },
            },
            new LevelSpec
            {
                sceneName = "Level_03", size = 15, timeStarSeconds = 50,
                maxAbyssPerRun = 1, invertChance = 0f,
                abyss = new[] { new Vector2Int(3, 5), new Vector2Int(9, 9), new Vector2Int(12, 3) },
                coins = new[] { new Vector2Int(5, 4), new Vector2Int(11, 10) },
                road = new[]
                {
                    "...........LLL.", ".LLLL...LLLIIL.", "....L.L......LL", "......L.LLLLLL.", "...LLLL....L...",
                    "..........L....", ".LLL..LLLIL.L.L", "..L....L....L.L", "..L....L....L.L", "...MLLLF..LLLL.",
                    ".....L.........", ".LLLLF...LLLM..", ".L.....LLL.....", ".L.............", ".LFF...........",
                },
            },
            new LevelSpec
            {
                sceneName = "Level_04", size = 20, timeStarSeconds = 60,
                maxAbyssPerRun = 3, invertChance = 0.35f,
                abyss = new[] { new Vector2Int(5, 5), new Vector2Int(13, 8), new Vector2Int(8, 15) },
                coins = new[] { new Vector2Int(6, 6), new Vector2Int(14, 13) },
                // 入口距起点 (0,0) 曼哈顿 5 格（起点右侧竖链 (2,1)→(2,3) 第 5 步即踩到），
                // 出口挪到右上区远离两枚金币（距 (6,6) 12 格、(14,13) 11 格）——用户 2026-10-18 拍板
                portals = new[] { new Vector2Int(2, 3), new Vector2Int(16, 4) },
                road = new[]
                {
                    "....LLLLLLLLLLLLLLL.", "..............L.....", "...........L..L.....", "..LLLLLL.LILLLL.LFL.",
                    ".........L..........", ".........L..........", ".LLLLII.LLLLLLL.LLLL", "...L.............L..",
                    ".................L..",                     "..LLLLLLL.LILLLL.LLL", ".....L..........L...", ".....L..............",
                    ".LLLLL.LLLLLL.LLLLF.", "......L......L......", "............L.......", "..LLLLL.LLFLLL.LLLLL",
                    "..L.....L...........", "..L.................", ".LLFFLLL.LLLLLL.LLL.", ".LL.................",
                },
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
                portals = new[] { new Vector2Int(3, 2), new Vector2Int(13, 16) },
                road = new[]
                {
                    "...LLLL.LLLL.LLLL.L.", ".............L......", ".........L...L......", "..LLL.LLLL.LLLI.FLLL",
                    ".....L..............", ".....L.........L....", ".LLL.LLLL.LIIL.LLLL.", "..L.........L...L...",
                    "............L...L...", "..LLL.LLLL.LLLL.LLLF", ".L...L..L...........", ".L......L.........L.",
                    ".LLI.LLLL.LLLL.LLLL.", "..............L....L", "..............L....L", "..LL.LFLL.LLLL.LLLLL",
                    "...L...L..L.........", "...L......L.........", ".LFFL.LLLL.LLLL.LL..", ".LL.................",
                },
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

            // 第一关格子上有钉死的传送门效果，所以原本整关跳过地形重建；路网也要写进去，
            // 而 ApplyTerrain 只改通行状态、不碰效果层（已实测传送门能保住），故配了路网就照写
            if (spec.rebuildTerrain || (spec.road != null && spec.road.Length > 0)) ApplyTerrain(board, spec);
            else Debug.Log($"[LevelTuning] {spec.sceneName} 跳过地形重建（保留格子上的预置效果）");

            // 传送门放在地形之后写入：即使地形重建顺带重建了格子，效果也落在最终格子上
            ApplyPortals(board, spec);

            ApplyCoins(board, spec);

            // 起终点：只在该关显式配置时写入（null = 保留画笔现状，默认左下/右上）
            ApplyTerminals(board, spec);

            // 自检：驱动器必须还在，否则关卡进得去却没有任何 UI，且不报错
            if (GameObject.Find(BoardRuntimeRootName) == null)
                Debug.LogError($"[LevelTuning] {spec.sceneName} 缺少驱动器根 {BoardRuntimeRootName}，请检查清理逻辑");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path, false);
            RegisterBuildSettings(path);

            Debug.Log($"[LevelTuning] {spec.sceneName}（{(existed ? "已存在·更新" : "新建")}）{spec.size}×{spec.size}，" +
                      $"预置血色深渊 {spec.abyss.Length} 个，金币 {spec.coins.Length} 枚，" +
                      $"预置传送门 {spec.portals.Length} 格，三星时限 {spec.timeStarSeconds}s");
        }

        /// <summary>
        /// 地形：先按 <see cref="LevelSpec.road" /> 铺出该关的固定初始路网，再把预置血色深渊压在最上层，
        /// 剩下没写到的格子就是深渊。只改格子的通行状态与贴图，不动格子上的效果层（传送门等）——
        /// 效果资产挂在 BoardTile.effect 上，ApplyToTiles 不会重写它，只有被迫重建格子时才会丢。
        /// </summary>
        static void ApplyTerrain(GameObject board, LevelSpec spec)
        {
            var painter = board.GetComponent<BoardLevelPainter>();
            if (painter == null) return;

            painter.Resize(spec.size);
            painter.ClearStates();

            if (spec.road != null && spec.road.Length == spec.size)
            {
                for (var i = 0; i < spec.size; i++)
                {
                    var y = spec.size - 1 - i;
                    var row = spec.road[i];

                    for (var x = 0; x < spec.size && x < row.Length; x++)
                        painter.SetState(x, y, StateOfTerrain(row[x]));
                }
            }
            else
            {
                Debug.LogWarning($"[LevelTuning] {spec.sceneName} 没配 {spec.size} 行路网（实际 {spec.road?.Length ?? 0} 行），只写预置血色深渊");
            }

            // 预置血色深渊优先级最高，压在路网之上
            foreach (var cell in spec.abyss) painter.SetState(cell.x, cell.y, (int)TileType.Mountain);

            painter.ApplyToTiles();

            var grid = board.GetComponent<BoardGridView>();
            grid?.ApplyTileSprites();

            CheckTerrain(grid, spec);
        }

        /// <summary>路网字符 → 格子状态：. 深渊 / L 路 / F 火 / I 冰 / M 血色深渊。</summary>
        static int StateOfTerrain(char c)
        {
            switch (c)
            {
                case 'L': return (int)TileType.Land;
                case 'F': return (int)TileType.Fire;
                case 'I': return (int)TileType.Ice;
                case 'M': return (int)TileType.Mountain;
                default: return (int)TileType.Void;
            }
        }

        /// <summary>
        /// 自检：可通行格（路 / 火 / 冰）必须 ≥30%——用户 2026-10-18 拍板「进关不能满屏深渊」；
        /// 金币也不能落在深渊或血色深渊上，否则永远捡不到。
        /// 注意：编辑模式下 BoardGridView.TileAt 恒为 null（格子表要等 Start），只能遍历子物体比对坐标。
        /// </summary>
        static void CheckTerrain(BoardGridView grid, LevelSpec spec)
        {
            if (grid == null) return;

            var total = 0;
            var walkable = 0;
            var presets = new Dictionary<Vector2Int, TileType>();

            foreach (var tile in grid.GetComponentsInChildren<BoardTile>(true))
            {
                total++;

                if (tile.PresetType == TileType.Land || tile.PresetType == TileType.Fire || tile.PresetType == TileType.Ice)
                    walkable++;

                if (!presets.ContainsKey(tile.Cell)) presets.Add(tile.Cell, tile.PresetType);
            }

            if (total == 0) return;

            var pct = walkable * 100f / total;
            var line = $"[LevelTuning] {spec.sceneName} 可通行 {walkable}/{total} = {pct:F1}%";
            if (pct < 30f) Debug.LogError(line + "，低于 30% 下限，请补路网");
            else Debug.Log(line);

            foreach (var coin in spec.coins)
            {
                var type = presets.TryGetValue(coin, out var t) ? t : TileType.Void;
                if (type != TileType.Land && type != TileType.Fire && type != TileType.Ice)
                    Debug.LogWarning($"[LevelTuning] {spec.sceneName} 金币 ({coin.x},{coin.y}) 落在 {type} 上，玩家走不过去");
            }
        }

        /// <summary>起终点：只在该关显式配置时写入画笔并重新应用到格子（null = 保留画笔现状，默认左下/右上）。</summary>
        static void ApplyTerminals(GameObject board, LevelSpec spec)
        {
            if (!spec.startCell.HasValue && !spec.goalCell.HasValue) return;

            var painter = board.GetComponent<BoardLevelPainter>();
            if (painter == null) return;

            if (spec.startCell.HasValue && !painter.SetStartCell(spec.startCell.Value.x, spec.startCell.Value.y))
                Debug.LogError($"[LevelTuning] {spec.sceneName} 起点非法：{spec.startCell.Value}（须在棋盘内且不与终点重合）");

            if (spec.goalCell.HasValue && !painter.SetGoalCell(spec.goalCell.Value.x, spec.goalCell.Value.y))
                Debug.LogError($"[LevelTuning] {spec.sceneName} 终点非法：{spec.goalCell.Value}（须在棋盘内且不与起点重合）");

            // 起终点写在格子预设上，改完要重新应用才会落盘
            painter.ApplyToTiles();
        }

        /// <summary>
        /// 预置传送门：两两成对写入 <see cref="BoardTile.effect" /> 并钉死（pinEffect，方块覆盖不掉——
        /// 发牌侧每块都带箭头效果，不钉死会被数据层效果遮蔽）。先清掉本生成器此前写入的旧传送门再重建，重跑幂等；
        /// 两端都要求落在可通行的预置路格上，且不与金币 / 血色深渊 / 起终点重合（违规只告警不阻断，方便先跑起来看）。
        /// </summary>
        static void ApplyPortals(GameObject board, LevelSpec spec)
        {
            var grid = board.GetComponent<BoardGridView>();
            if (grid == null) return;

            var byCell = new Dictionary<Vector2Int, BoardTile>();
            foreach (var tile in grid.GetComponentsInChildren<BoardTile>(true))
                if (!byCell.ContainsKey(tile.Cell)) byCell.Add(tile.Cell, tile);

            TileEffect portal = null;
            if (spec.portals != null && spec.portals.Length > 0)
            {
                portal = AssetDatabase.LoadAssetAtPath<TileEffect>(PortalAssetPath);
                if (portal == null) Debug.LogError($"[LevelTuning] 找不到传送门资产 {PortalAssetPath}");
            }

            // 清场：全项目预置传送门只有本生成器一个来源，按类型清不会误伤别人配的效果
            foreach (var tile in byCell.Values)
            {
                if (!(tile.PresetEffect is PortalEffect)) continue;

                tile.SetEffect(null);
                tile.SetPinned(false);
                tile.ApplyEffectVisual(null, false, tile.PresetType);
                EditorUtility.SetDirty(tile);
            }

            var written = 0;
            for (var i = 0; portal != null && i + 1 < spec.portals.Length; i += 2)
            {
                var pair = new[] { spec.portals[i], spec.portals[i + 1] };
                foreach (var cell in pair)
                {
                    if (!byCell.TryGetValue(cell, out var tile))
                    {
                        Debug.LogError($"[LevelTuning] {spec.sceneName} 传送门 ({cell.x},{cell.y}) 超出棋盘，跳过");
                        continue;
                    }

                    if (!TileTypeInfo.IsPassable(tile.PresetType))
                        Debug.LogWarning($"[LevelTuning] {spec.sceneName} 传送门 ({cell.x},{cell.y}) 落在 {tile.PresetType} 上，" +
                                         "AI 踏不进去（图标会在格子铺成可走后出现）");

                    foreach (var coin in spec.coins)
                        if (coin == cell)
                            Debug.LogWarning($"[LevelTuning] {spec.sceneName} 传送门 ({cell.x},{cell.y}) 与金币重合，踩上去会被传走捡不到");

                    foreach (var abyss in spec.abyss)
                        if (abyss == cell)
                            Debug.LogWarning($"[LevelTuning] {spec.sceneName} 传送门 ({cell.x},{cell.y}) 与血色深渊重合");

                    tile.SetEffect(portal);
                    tile.SetPinned(true);
                    tile.ApplyEffectVisual(portal, false, tile.PresetType);
                    EditorUtility.SetDirty(tile);
                    written++;
                }
            }

            if (written > 0) Debug.Log($"[LevelTuning] {spec.sceneName} 写入预置传送门 {written} 格（{written / 2} 对）");
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
