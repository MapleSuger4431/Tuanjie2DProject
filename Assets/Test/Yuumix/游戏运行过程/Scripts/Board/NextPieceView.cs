using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 下一块预告的渲染器：按形状包围盒生成 UGUI 格子网格，用形状色点亮对应格。
    /// 数据只来自 PieceModel.Next，本脚本不自己推算下一块。
    /// </summary>
    public sealed class NextPieceView : MonoBehaviour
    {
        [SerializeField] RectTransform gridRoot;
        [SerializeField] Sprite cellSprite;
        [SerializeField] float cellSize = 46f;
        [SerializeField] float spacing = 6f;
        [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.10f);

        readonly List<Image> _cells = new List<Image>();
        int _cols;
        int _rows;

        /// <summary>按形状重画网格；形状为空时清空。</summary>
        public void Render(TileShape shape)
        {
            if (gridRoot == null) return;

            if (shape == null || shape.Cells == null || shape.Cells.Length == 0)
            {
                Clear();
                return;
            }

            var cols = 0;
            var rows = 0;
            for (int i = 0; i < shape.Cells.Length; i++)
            {
                cols = Mathf.Max(cols, shape.Cells[i].x + 1);
                rows = Mathf.Max(rows, shape.Cells[i].y + 1);
            }

            EnsureGrid(cols, rows);

            for (int i = 0; i < _cells.Count; i++)
                _cells[i].color = emptyColor;

            for (int i = 0; i < shape.Cells.Length; i++)
            {
                // 形状 y 向上为正，网格行自上而下排列，故行号要翻转
                var row = rows - 1 - shape.Cells[i].y;
                var index = row * cols + shape.Cells[i].x;
                if (index >= 0 && index < _cells.Count)
                    _cells[index].color = shape.Color;
            }
        }

        void EnsureGrid(int cols, int rows)
        {
            if (_cols == cols && _rows == rows && _cells.Count == cols * rows) return;

            Clear();

            var layout = gridRoot.GetComponent<GridLayoutGroup>();
            if (layout == null) layout = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cellSize, cellSize);
            layout.spacing = new Vector2(spacing, spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = cols;
            layout.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < cols * rows; i++)
            {
                var go = new GameObject("Cell");
                go.transform.SetParent(gridRoot, false);

                var image = go.AddComponent<Image>();
                image.sprite = cellSprite;
                image.color = emptyColor;
                image.raycastTarget = false;

                _cells.Add(image);
            }

            _cols = cols;
            _rows = rows;
        }

        void Clear()
        {
            for (int i = gridRoot != null ? gridRoot.childCount - 1 : -1; i >= 0; i--)
                Destroy(gridRoot.GetChild(i).gameObject);

            _cells.Clear();
            _cols = 0;
            _rows = 0;
        }
    }
}
