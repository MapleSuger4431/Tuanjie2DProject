using Game.Board;
using Game.Core;
using Runestone.AesirModules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 棋盘运行时面板生成器：重建 Assets/Resources/UIPrefabs/BoardRuntimePanel.prefab。
    /// 皮肤 = 深紫 fantasy（按用户参考图）：上下深紫占位条 + 右侧圆角深紫面板，内容从上到下
    /// 「✦角色状态卡（头像 + 当前元素图标 + 可走地形）→ ✦下一块（十六宫格预览）→ 已用 / 用时 / AI 状态 → 出发按钮」，
    /// 右上角直接建「返回选关 / 重新开始」两枚紫系图案按钮。
    /// 面板文本节点一律留空——文案真源在 GameText 表，运行时经 <see cref="Game.Core.Localization" /> 刷新。
    /// 重跑即覆盖产物；要改结构改这里，禁止手改预制体。
    /// </summary>
    public static partial class BoardRuntimePanelBuilder
    {
        const string PrefabPath = "Assets/Resources/UIPrefabs/BoardRuntimePanel.prefab";

        // ---- 复用素材 ----
        public const string BoardFramePath = "Assets/Art/Board/BoardFrame.png";
        public const string AvatarPath = "Assets/GameUISprite/头像.png";
        public const string StarPath = "Assets/GameUISprite/星星.png";
        public const string PlaySpritePath = "Assets/Arts/UI组件/按钮底图_开始_无字.png";
        public const string BackSpritePath = "Assets/Arts/UI组件/按钮底图_玩法说明_无字.png";
        public const string RestartSpritePath = "Assets/Arts/UI组件/按钮底图_退出_无字.png";
        public const string TilesDir = "Assets/Art/Board/Tiles";
        public const string IceIconPath = "Assets/GameUISprite/ice.png";
        public const string FireIconPath = "Assets/Art/Board/Element_Fire.png";

        // ---- 配色（深紫 fantasy）----
        public static readonly Color BarColor = new Color(0.078f, 0.047f, 0.137f, 1f);
        public static readonly Color HeaderPink = new Color(1f, 0.42f, 0.85f, 1f);
        public static readonly Color LightText = new Color(0.79f, 0.77f, 0.90f, 1f);
        public static readonly Color CardTint = new Color(0.216f, 0.145f, 0.330f, 1f);
        public static readonly Color GridCardTint = new Color(0.157f, 0.102f, 0.255f, 1f);

        // ---- 布局（RightPanel 本地坐标，1920×1080 下面板 768×976，自上而下累计）----
        public const float HeaderHeight = 52f;
        public const float RowHeight = 44f;
        // 状态卡顶部让开右上角两枚按钮（高 72 + 上边距 14 = 底缘 86），再留 18px 间距
        public const float StatusCardTop = 104f;
        public const float StatusCardHeight = 204f;
        public const float NextHeaderTop = 316f;
        public const float NextAreaTop = 376f;
        public const float NextAreaHeight = 280f;
        public const float PlacedRowTop = 664f;
        public const float TimeRowTop = 710f;
        public const float GoalRowTop = 756f;
        public const float AiStateRowTop = 802f;
        // 出发按钮上下都留呼吸空间：距 AI 状态行 20px、距面板底 26px
        public const float PlayTop = 866f;

        [MenuItem("Tools/关卡/重建棋盘运行时面板")]
        public static void RebuildAll()
        {
            BoardTextBuilder.Sync();   // 一站式：面板结构与文案键一起刷新
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[关卡] 棋盘运行时面板已重建：" + PrefabPath);
        }

        static void BuildPrefab()
        {
            var panelSprite = UiSpritePainter.EnsurePanelSprite();
            var cardSprite = UiSpritePainter.EnsureCardSprite();

            var root = new GameObject("BoardRuntimePanel", typeof(RectTransform));
            var panel = root.AddComponent<BoardRuntimePanel>();
            Stretch((RectTransform)root.transform);

            var topBar = BuildBar("TopBar", root.transform, new Vector2(0f, 1f), new Vector2(0.5f, 1f));
            var bottomBar = BuildBar("BottomBar", root.transform, Vector2.zero, new Vector2(0.5f, 0f));

            var right = NewRect("RightPanel", root.transform);
            SetAnchors(right, new Vector2(0.6f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            var rightImage = right.gameObject.AddComponent<Image>();
            rightImage.sprite = panelSprite;
            rightImage.type = Image.Type.Sliced;

            var (back, backLabel, restart, restartLabel) = BuildTopRightButtons(topBar);
            var (statusTitle, statusName, statusWalk, elementIcon) = BuildStatusSection(right);
            var (nextTitle, nextPieceView) = BuildNextSection(right, cardSprite);
            var placedCount = BuildStatusRow(right, "PlacedCountText", PlacedRowTop, 32);
            var timeText = BuildStatusRow(right, "TimeText", TimeRowTop, 32);
            var goalText = BuildStatusRow(right, "GoalText", GoalRowTop, 28);
            var aiState = BuildStatusRow(right, "AiStateText", AiStateRowTop, 30);
            var (playButton, playLabel) = BuildPlayButton(right);
            var hintText = BuildHintText(bottomBar);

            WirePanel(panel, root, topBar, bottomBar, right, nextPieceView,
                placedCount, timeText, goalText, aiState, hintText,
                playButton, playLabel, statusTitle, statusName, statusWalk, nextTitle, elementIcon,
                back, backLabel, restart, restartLabel);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
