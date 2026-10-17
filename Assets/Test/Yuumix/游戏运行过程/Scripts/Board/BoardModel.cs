using System;
using System.Collections.Generic;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 棋盘数据层（尺寸随关卡在 10×10 ~ 20×20 间变化）。运行期唯一真源：每格的当前状态 <see cref="TileType" />。
    /// 初始状态来自场景里各格 Inspector 配的预设（<see cref="ApplyPreset" />），之后由方块落下改写。
    /// 覆盖规则：山地 / 起点 / 终点不可覆盖，其余（含已铺格）允许被后续方块再覆盖。
    /// </summary>
    [Serializable]
    public sealed class BoardModel : AbstractModel
    {
        /// <summary>起始关卡尺寸下限。</summary>
        public const int MinSize = 10;

        /// <summary>最高难度尺寸上限。</summary>
        public const int MaxSize = 20;

        [SerializeField] int width = MinSize;
        [SerializeField] int height = MinSize;

        [SerializeField] ObservableValue<int> coveredCount = new ObservableValue<int>(0);

        TileType[] _tiles;

        public int Width => width;
        public int Height => height;

        /// <summary>起点：左下角。</summary>
        public Vector2Int StartCell => Vector2Int.zero;

        /// <summary>终点：右上角。</summary>
        public Vector2Int GoalCell => new Vector2Int(width - 1, height - 1);

        /// <summary>已发生的覆盖次数（每个方块算 1 次，非格数）。</summary>
        public ObservableValue<int> CoveredCount => coveredCount;

        /// <summary>覆盖写入成功后触发（一次性瞬时事件）→ 驱动 View 刷新渲染。</summary>
        public MiniEvent BoardCovered { get; } = new MiniEvent();

        /// <summary>尺寸变化后触发（先于数据清空）→ 驱动 View 按新尺寸重建格子。</summary>
        public MiniEvent BoardResized { get; } = new MiniEvent();

        protected override void OnInitialize()
        {
            width = Mathf.Clamp(width, MinSize, MaxSize);
            height = Mathf.Clamp(height, MinSize, MaxSize);
            Reset();
        }

        /// <summary>切换关卡尺寸：先清空状态层（同步扩缩容），再让 View 按新尺寸重建格子。</summary>
        public void SetSize(int newWidth, int newHeight)
        {
            width = Mathf.Clamp(newWidth, MinSize, MaxSize);
            height = Mathf.Clamp(newHeight, MinSize, MaxSize);

            Reset();
            BoardResized.Invoke();
        }

        public void Reset()
        {
            _tiles = new TileType[width * height];
            coveredCount.Value = 0;
            BoardCovered.Invoke();
        }

        /// <summary>
        /// 用关卡预设初始化状态层（先整盘 Void 再逐格写入）。越界的预设项忽略。
        /// </summary>
        public void ApplyPreset(IEnumerable<KeyValuePair<Vector2Int, TileType>> preset)
        {
            Reset();
            if (preset == null) return;

            foreach (var entry in preset)
            {
                if (!InBounds(entry.Key)) continue;
                _tiles[Index(entry.Key)] = entry.Value;
            }

            BoardCovered.Invoke();
        }

        public bool InBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

        public TileType TypeAt(Vector2Int cell) => InBounds(cell) ? _tiles[Index(cell)] : TileType.Void;

        public bool IsPassable(Vector2Int cell) => TileTypeInfo.IsPassable(TypeAt(cell));

        /// <summary>整块合法性：块内任一格越界或落在山地/起点/终点上，整块非法。</summary>
        public bool CanCover(Vector2Int anchor, TileShape shape)
        {
            if (shape == null || shape.Cells == null) return false;

            for (int i = 0; i < shape.Cells.Length; i++)
            {
                var cell = anchor + shape.Cells[i];
                if (!InBounds(cell)) return false;
                if (!TileTypeInfo.IsCoverable(_tiles[Index(cell)])) return false;
            }

            return true;
        }

        /// <summary>把块内每格改写成方块的元素状态；非法时整块不写入。</summary>
        public bool TryCover(Vector2Int anchor, TileShape shape) =>
            TryCover(anchor, shape, shape != null ? shape.TileType : TileType.Land);

        /// <summary>指定写入状态（为空/山地/起终点时按 TileType.Land 兜底后再由规则校验）。</summary>
        public bool TryCover(Vector2Int anchor, TileShape shape, TileType fill)
        {
            if (fill == TileType.Mountain || fill == TileType.Start || fill == TileType.Goal)
                fill = TileType.Land;

            if (!CanCover(anchor, shape)) return false;

            for (int i = 0; i < shape.Cells.Length; i++)
                _tiles[Index(anchor + shape.Cells[i])] = fill;

            coveredCount.Value += 1;
            BoardCovered.Invoke();
            return true;
        }

        int Index(Vector2Int cell) => cell.y * width + cell.x;
    }
}
