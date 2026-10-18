using Game.Board;
using Runestone.AesirModules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>棋盘运行时面板生成器的底层工具与字段回填。</summary>
    public static partial class BoardRuntimePanelBuilder
    {
        /// <summary>右侧面板中的一行状态文本（已用 / 用时 / AI 状态），浅色居中。</summary>
        static Text BuildStatusRow(RectTransform right, string name, float top, int fontSize)
        {
            var row = NewRect(name, right);
            SetAnchors(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -top), new Vector2(0f, RowHeight));
            return AddTextComponent(row, fontSize, LightText, TextAnchor.MiddleCenter);
        }

        /// <summary>出发按钮：440×84 主菜单同款横幅底图（九宫格）+ 白字。</summary>
        static (Button button, Text label) BuildPlayButton(RectTransform right)
        {
            var rect = NewRect("PlayButton", right);
            SetAnchors(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -PlayTop), new Vector2(440f, 84f));
            var button = AddButton(rect, PlaySpritePath);
            var label = AddText(rect, "Label", 40, Color.white, TextAnchor.MiddleCenter, stretch: true);
            return (button, label);
        }

        /// <summary>底部操作提示行：浅色文字铺满底部占位条。</summary>
        static Text BuildHintText(RectTransform bottomBar)
        {
            var rect = NewRect("HintText", bottomBar);
            AnchorOffsets(rect, Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, 0f));
            return AddTextComponent(rect, 34, LightText, TextAnchor.MiddleCenter);
        }

        /// <summary>把全部节点引用回填进 BoardRuntimePanel 的序列化字段（层固定 Normal）。</summary>
        static void WirePanel(
            BoardRuntimePanel panel, GameObject root,
            RectTransform topBar, RectTransform bottomBar, RectTransform right, NextPieceView nextPieceView,
            Text placedCount, Text timeText, Text goalText, Text aiState, Text hintText,
            Button playButton, Text playLabel, Text statusTitle, Text statusName, Text statusWalk, Text nextTitle,
            Image elementIcon,
            Button back, Text backLabel, Button restart, Text restartLabel)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("topBar").objectReferenceValue = topBar;
            so.FindProperty("bottomBar").objectReferenceValue = bottomBar;
            so.FindProperty("rightPanel").objectReferenceValue = right;
            so.FindProperty("nextPieceView").objectReferenceValue = nextPieceView;
            so.FindProperty("placedCountText").objectReferenceValue = placedCount;
            so.FindProperty("timeText").objectReferenceValue = timeText;
            so.FindProperty("goalText").objectReferenceValue = goalText;
            so.FindProperty("hintText").objectReferenceValue = hintText;
            so.FindProperty("boardFrame").objectReferenceValue = null;
            so.FindProperty("boardFrameSprite").objectReferenceValue = LoadSprite(BoardFramePath);
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("playButtonLabel").objectReferenceValue = playLabel;
            so.FindProperty("aiStateText").objectReferenceValue = aiState;
            so.FindProperty("statusTitleText").objectReferenceValue = statusTitle;
            so.FindProperty("statusNameText").objectReferenceValue = statusName;
            so.FindProperty("statusWalkText").objectReferenceValue = statusWalk;
            so.FindProperty("nextTitleText").objectReferenceValue = nextTitle;

            so.FindProperty("elementIcon").objectReferenceValue = elementIcon;
            so.FindProperty("elementIconIce").objectReferenceValue = LoadSprite(IceIconPath);
            so.FindProperty("elementIconFire").objectReferenceValue = LoadSprite(FireIconPath);

            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("backButtonLabel").objectReferenceValue = backLabel;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("restartButtonLabel").objectReferenceValue = restartLabel;
            so.FindProperty("layer").enumValueIndex = (int)UILayer.Normal;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 低层工具 ----------

        static RectTransform NewRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        /// <summary>按 offsetMin / offsetMax 定位（跨列拉伸的行内嵌套元素用，语义比 anchoredPosition 直白）。</summary>
        static void AnchorOffsets(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void Stretch(RectTransform rect) =>
            SetAnchors(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        static Button AddButton(RectTransform rect, string spritePath)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(spritePath);
            image.type = Image.Type.Sliced;
            return rect.gameObject.AddComponent<Button>();
        }

        /// <summary>带定位选项的 Text：stretch=铺满父级；leftInset>0=左侧让出固定宽度（星芒图标位）。</summary>
        static Text AddText(RectTransform parent, string name, int fontSize, Color color, TextAnchor anchor,
            bool stretch = false, float leftInset = 0f)
        {
            var rect = NewRect(name, parent);
            if (stretch) Stretch(rect);
            else if (leftInset > 0f)
                AnchorOffsets(rect, Vector2.zero, Vector2.one, new Vector2(leftInset, 0f), Vector2.zero);

            return AddTextComponent(rect, fontSize, color, anchor);
        }

        static Text AddTextComponent(RectTransform rect, int fontSize, Color color, TextAnchor anchor)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogError($"[BoardRuntimePanelBuilder] 未找到素材 {path}");
            return sprite;
        }
    }
}
