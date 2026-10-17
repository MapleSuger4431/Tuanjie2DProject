using System;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 一个待放置的方块实例 = 形状 + 每一格各自随机出来的状态 + 每一格各自的效果（转向箭头）。
    /// 形状模板 <see cref="TileShape" /> 是全局共享的、不能存随机结果，所以随机出来的逐格内容挂在这层实例上。
    /// 下标与 <see cref="TileShape.Cells" /> 一一对齐。
    /// </summary>
    public sealed class TilePiece
    {
        readonly TileShape _shape;
        readonly TileType[] _cellTypes;
        readonly TileEffect[] _cellEffects;

        public TilePiece(TileShape shape, TileType[] cellTypes) : this(shape, cellTypes, null)
        {
        }

        public TilePiece(TileShape shape, TileType[] cellTypes, TileEffect[] cellEffects)
        {
            _shape = shape;

            var count = shape != null && shape.Cells != null ? shape.Cells.Length : 0;

            _cellTypes = new TileType[count];
            if (cellTypes != null && cellTypes.Length == count)
            {
                Array.Copy(cellTypes, _cellTypes, count);
            }
            else
            {
                for (int i = 0; i < count; i++) _cellTypes[i] = TileType.Land;
            }

            _cellEffects = new TileEffect[count];
            if (cellEffects != null)
                Array.Copy(cellEffects, _cellEffects, Mathf.Min(cellEffects.Length, count));
        }

        public TileShape Shape => _shape;

        public Vector2Int[] Cells => _shape != null ? _shape.Cells : Array.Empty<Vector2Int>();

        public int CellCount => _cellTypes.Length;

        /// <summary>第 index 格的状态（与 <see cref="Cells" /> 同下标）。</summary>
        public TileType TypeAt(int index) =>
            index >= 0 && index < _cellTypes.Length ? _cellTypes[index] : TileType.Land;

        /// <summary>第 index 格的效果（与 <see cref="Cells" /> 同下标）；该格没有效果时为 null。</summary>
        public TileEffect EffectAt(int index) =>
            index >= 0 && index < _cellEffects.Length ? _cellEffects[index] : null;

        /// <summary>包围盒尺寸（格）：x = 宽，y = 高；空块按 1×1 兜底。</summary>
        public Vector2Int Bounds
        {
            get
            {
                var cells = Cells;
                if (cells.Length == 0) return Vector2Int.one;

                var width = 0;
                var height = 0;
                for (var i = 0; i < cells.Length; i++)
                {
                    width = Mathf.Max(width, cells[i].x + 1);
                    height = Mathf.Max(height, cells[i].y + 1);
                }

                return new Vector2Int(width, height);
            }
        }

        /// <summary>
        /// 顺时针旋转 90° 产出**新实例**（形状库里的原件不受影响）：格偏移、每格状态、每格效果一起转，
        /// 三者始终保持同下标对齐——分开转会让「哪一格是火 / 哪一格带箭头」错位。
        /// </summary>
        public TilePiece RotateCW()
        {
            var cells = Cells;
            var count = cells.Length;
            if (count == 0 || _shape == null) return this;

            var maxX = 0;
            for (var i = 0; i < count; i++) maxX = Mathf.Max(maxX, cells[i].x);

            // 顺时针 90°（y 向上）：(x, y) → (y, maxX - x)，再平移回包围盒左上角
            var rotated = new Vector2Int[count];
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            for (var i = 0; i < count; i++)
            {
                rotated[i] = new Vector2Int(cells[i].y, maxX - cells[i].x);
                minX = Mathf.Min(minX, rotated[i].x);
                minY = Mathf.Min(minY, rotated[i].y);
            }

            for (var i = 0; i < count; i++)
                rotated[i] = new Vector2Int(rotated[i].x - minX, rotated[i].y - minY);

            // 与形状库同一口径：先行后列排序，让同一朝向只有一种表达
            var order = new int[count];
            for (var i = 0; i < count; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => rotated[a].y != rotated[b].y
                ? rotated[a].y.CompareTo(rotated[b].y)
                : rotated[a].x.CompareTo(rotated[b].x));

            var orderedCells = new Vector2Int[count];
            var orderedTypes = new TileType[count];
            var orderedEffects = new TileEffect[count];
            for (var i = 0; i < count; i++)
            {
                orderedCells[i] = rotated[order[i]];
                orderedTypes[i] = _cellTypes[order[i]];
                orderedEffects[i] = _cellEffects[order[i]];
            }

            return new TilePiece(_shape.WithCells(orderedCells), orderedTypes, orderedEffects);
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(_shape != null ? _shape.ShapeName : "?").Append('[');
            for (int i = 0; i < _cellTypes.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(TileTypeInfo.DisplayName(_cellTypes[i]));

                var effect = _cellEffects[i];
                if (effect != null) sb.Append('+').Append(effect.DisplayName);
            }

            return sb.Append(']').ToString();
        }
    }
}
