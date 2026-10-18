using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Board
{
    /// <summary>
    /// 下一块预告的渲染器：网格恒为 4×4 的十六宫格，方块按包围盒居中落在宫格里刷新——
    /// 换块时只改这 16 格的贴图与颜色，不重建网格，所以方框大小不会随形状变。
    /// 数据只来自 PieceModel.Next，本脚本不自己推算下一块。
    /// </summary>
    public sealed class NextPieceView : MonoBehaviour
    {
        /// <summary>十六宫格固定边长（4×4）。形状库保证包围盒不超过这个值。</summary>
        public const int GridSize = 4;

        [SerializeField] RectTransform gridRoot;

        [Tooltip("没配到格子美术时的兜底纯色块")]
        [SerializeField] Sprite cellSprite;

        [Tooltip("索引与 TileType 数值对齐：0 空 / 1 路 / 2 火 / 3 冰 / 4 山地")]
        [SerializeField] Sprite[] tileSprites = new Sprite[5];

        [SerializeField] float cellSize = 46f;
        [SerializeField] float spacing = 6f;
        [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.10f);

        readonly List<Image> _cells = new List<Image>();

        /// <summary>每格叠一个箭头图标（转向效果）；与 <see cref="_cells" /> 同下标，该格无箭头时停用。</summary>
        readonly List<Image> _arrows = new List<Image>();

        /// <summary>按方块实例重画十六宫格；方块为空时留一整框空格。</summary>
        public void Render(TilePiece piece)
        {
            if (gridRoot == null) return;

            EnsureGrid();

            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].sprite = cellSprite;
                _cells[i].color = emptyColor;
                if (i < _arrows.Count) _arrows[i].gameObject.SetActive(false);
            }

            var cells = piece != null ? piece.Cells : null;
            if (cells == null || cells.Length == 0) return;

            var cols = 0;
            var rows = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                cols = Mathf.Max(cols, cells[i].x + 1);
                rows = Mathf.Max(rows, cells[i].y + 1);
            }

            if (cols > GridSize || rows > GridSize)
                Debug.LogWarning($"[NextPieceView] 形状包围盒 {cols}×{rows} 超过十六宫格，预览会被裁掉：{piece}");

            // 形状在十六宫格里居中：左右各留一半空列，上下各留一半空行
            var offsetX = (GridSize - cols) / 2;
            var offsetY = (GridSize - rows) / 2;

            for (int i = 0; i < cells.Length; i++)
            {
                // 形状 y 向上为正，网格行自上而下排列，故行号要翻转
                var col = offsetX + cells[i].x;
                var row = offsetY + (rows - 1 - cells[i].y);
                if (col < 0 || col >= GridSize || row < 0 || row >= GridSize) continue;

                var index = row * GridSize + col;
                if (index < 0 || index >= _cells.Count) continue;

                var type = piece.TypeAt(i);
                var image = _cells[index];
                image.sprite = SpriteFor(type);
                image.color = HasArt(type) ? Color.white : TileTypeInfo.Color(type);

                // 转向箭头跟着预览一起显示：玩家在落块前就能看出哪一格会让 AI 拐弯；
                // 空地/山地这类 AI 踏不进的格子不放提示（与棋盘格上的显示规则一致）
                var effect = piece.EffectAt(i);
                var icon = effect != null && TileTypeInfo.IsHintAllowed(type) ? effect.BoardIcon : null;
                if (index < _arrows.Count)
                {
                    var arrow = _arrows[index];
                    arrow.gameObject.SetActive(icon != null);
                    if (icon != null)
                    {
                        arrow.sprite = icon;
                        arrow.color = Color.white;
                    }
                }
            }
        }

        Sprite SpriteFor(TileType type)
        {
            if (HasArt(type)) return tileSprites[(int)type];
            return cellSprite;
        }

        bool HasArt(TileType type)
        {
            if (tileSprites == null) return false;

            int index = (int)type;
            return index >= 0 && index < tileSprites.Length && tileSprites[index] != null;
        }

        /// <summary>十六宫格只建一次；之后每次换块都复用这 16 个格子。</summary>
        void EnsureGrid()
        {
            if (_cells.Count == GridSize * GridSize && _cells.Count > 0 && _cells[0] != null) return;

            Clear();

            var layout = gridRoot.GetComponent<GridLayoutGroup>();
            if (layout == null) layout = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cellSize, cellSize);
            layout.spacing = new Vector2(spacing, spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = GridSize;
            layout.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < GridSize * GridSize; i++)
            {
                var go = new GameObject("Cell");
                go.transform.SetParent(gridRoot, false);

                var image = go.AddComponent<Image>();
                image.sprite = cellSprite;
                image.color = emptyColor;
                image.raycastTarget = false;

                _cells.Add(image);

                // 必须显式带 RectTransform：new GameObject() 只有 Transform，
                // 而这里要在挂 Image 之前先摆好拉伸锚点（等 AddComponent<Image> 才转换就晚了一步）
                var arrowObject = new GameObject("Arrow", typeof(RectTransform));
                arrowObject.transform.SetParent(image.transform, false);

                var arrowRect = (RectTransform)arrowObject.transform;
                arrowRect.anchorMin = Vector2.zero;
                arrowRect.anchorMax = Vector2.one;
                arrowRect.offsetMin = Vector2.zero;
                arrowRect.offsetMax = Vector2.zero;

                var arrowImage = arrowObject.AddComponent<Image>();
                arrowImage.raycastTarget = false;
                arrowImage.preserveAspect = true;
                arrowObject.SetActive(false);

                _arrows.Add(arrowImage);
            }
        }

        void Clear()
        {
            for (int i = gridRoot != null ? gridRoot.childCount - 1 : -1; i >= 0; i--)
                Destroy(gridRoot.GetChild(i).gameObject);

            _cells.Clear();
            _arrows.Clear();
        }
    }
}
