using Game.Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>棋盘运行时面板生成器的各区块构建：状态卡 / 下一块 / 状态行 / 出发按钮 / 右上角出口按钮。</summary>
    public static partial class BoardRuntimePanelBuilder
    {
        /// <summary>上下占位条：平铺深紫（无 sprite，纯色 Image），位置真源仍是 BoardScreenLayout。</summary>
        static RectTransform BuildBar(string name, Transform parent, Vector2 anchor, Vector2 pivot)
        {
            var bar = NewRect(name, parent);
            SetAnchors(bar, anchor, new Vector2(1f, anchor.y), pivot, Vector2.zero, new Vector2(0f, 100f));
            var image = bar.gameObject.AddComponent<Image>();
            image.sprite = null;
            image.color = BarColor;
            return bar;
        }

        /// <summary>区块星芒标题行：✦ 星芒图标 + 粉色标题文本（文本留空，运行时本地化填充）。返回标题文本。</summary>
        static Text BuildHeader(RectTransform right, string name, float top)
        {
            var row = NewRect(name, right);
            SetAnchors(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -top), new Vector2(0f, HeaderHeight));

            var star = NewRect("StarIcon", row);
            SetAnchors(star, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(26f, 0f), new Vector2(40f, 40f));
            var starImage = star.gameObject.AddComponent<Image>();
            starImage.sprite = LoadSprite(StarPath);
            starImage.preserveAspect = true;
            starImage.raycastTarget = false;

            return AddText(row, "Label", 34, HeaderPink, TextAnchor.MiddleLeft, leftInset: 58f);
        }

        /// <summary>右上角「返回选关 / 重新开始」：紫系九宫格图案按钮 + 白字，直接建进预制体。</summary>
        static (Button back, Text backLabel, Button restart, Text restartLabel) BuildTopRightButtons(RectTransform topBar)
        {
            var backRect = NewRect("BackToLevelSelect", topBar);
            SetAnchors(backRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -TopButtonsTop), new Vector2(220f, 72f));
            var back = AddButton(backRect, BackSpritePath);
            var backLabel = AddText(backRect, "Text", 30, Color.white, TextAnchor.MiddleCenter, stretch: true);

            // 与返回按钮同一条：右缘往左让开 220 + 12px 间距（与运行时 EnsureRestartButton 的算法一致）
            var restartRect = NewRect("RestartLevel", topBar);
            SetAnchors(restartRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-248f, -TopButtonsTop), new Vector2(200f, 72f));
            var restart = AddButton(restartRect, RestartSpritePath);
            var restartLabel = AddText(restartRect, "Text", 30, Color.white, TextAnchor.MiddleCenter, stretch: true);

            return (back, backLabel, restart, restartLabel);
        }

        /// <summary>
        /// 「✦角色状态」区块：星芒标题 + 圆角状态卡（左头像，右侧角色名 / 当前元素图标 / 可走地形行）。
        /// 元素图标只有一张，运行时按 AI 属性在 冰(ice.png) / 火(Element_Fire) 之间切换；文本留空由本地化填充。
        /// </summary>
        static (Text title, Text name, Text dir, Text walk, Image elementIcon) BuildStatusSection(RectTransform right)
        {
            var title = BuildHeader(right, "StatusHeader", StatusCardTop - 58f);

            var card = NewRect("StatusCard", right);
            SetAnchors(card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -StatusCardTop), new Vector2(-40f, StatusCardHeight));
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = UiSpritePainter.EnsureCardSprite();
            cardImage.type = Image.Type.Sliced;
            cardImage.color = CardTint;

            var avatar = NewRect("Avatar", card);
            SetAnchors(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(108f, 0f), new Vector2(156f, 156f));
            var avatarImage = avatar.gameObject.AddComponent<Image>();
            avatarImage.sprite = LoadSprite(AvatarPath);
            avatarImage.preserveAspect = true;
            avatarImage.raycastTarget = false;

            var name = AddText(card, "NameLabel", 38, Color.white, TextAnchor.MiddleLeft);
            AnchorOffsets(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(212f, -56f), new Vector2(-16f, -12f));

            // 出发朝向行：角色名与可走行之间（真源 BoardRoot.InitialDirection，按关配置）
            var dir = AddText(card, "DirLabel", 28, LightText, TextAnchor.MiddleLeft);
            AnchorOffsets(dir.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(212f, -110f), new Vector2(-16f, -62f));

            // 当前元素图标：贴卡片右缘垂直居中，运行时按元素属性换 sprite（见 BoardRuntimePanel.RefreshElementIcon）
            var elementIcon = NewRect("ElementIcon", card);
            SetAnchors(elementIcon, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(104f, 104f));
            var elementImage = elementIcon.gameObject.AddComponent<Image>();
            elementImage.sprite = LoadSprite(FireIconPath);
            elementImage.preserveAspect = true;
            elementImage.raycastTarget = false;

            var walk = AddText(card, "WalkLabel", 30, LightText, TextAnchor.MiddleLeft);
            AnchorOffsets(walk.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(212f, -164f), new Vector2(-16f, -116f));

            return (title, name, dir, walk, elementImage);
        }

        /// <summary>「✦下一块」区块：星芒标题 + 深色圆角底衬 + 十六宫格预览（NextPieceView 结构保持原样）。</summary>
        static (Text title, NextPieceView view) BuildNextSection(RectTransform right, Sprite cardSprite)
        {
            var title = BuildHeader(right, "NextHeader", NextHeaderTop);

            var next = NewRect("NextPiece", right);
            SetAnchors(next, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -NextAreaTop), new Vector2(0f, NextAreaHeight));

            var gridCard = NewRect("GridCard", next);
            SetAnchors(gridCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(240f, 240f));
            var gridCardImage = gridCard.gameObject.AddComponent<Image>();
            gridCardImage.sprite = cardSprite;
            gridCardImage.type = Image.Type.Sliced;
            gridCardImage.color = GridCardTint;

            var grid = NewRect("Grid", next);
            SetAnchors(grid, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-64f, -64f));

            var view = next.gameObject.AddComponent<NextPieceView>();
            var so = new SerializedObject(view);
            so.FindProperty("gridRoot").objectReferenceValue = grid;
            so.FindProperty("cellSprite").objectReferenceValue = null;
            var sprites = so.FindProperty("tileSprites");
            sprites.arraySize = 5;
            for (var i = 0; i < 5; i++)
                sprites.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite($"{TilesDir}/Tile_{(TileType)i}.png");
            so.FindProperty("cellSize").floatValue = 40f;
            so.FindProperty("spacing").floatValue = 8f;
            so.FindProperty("emptyColor").colorValue = new Color(1f, 1f, 1f, 0.07f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return (title, view);
        }
    }
}
