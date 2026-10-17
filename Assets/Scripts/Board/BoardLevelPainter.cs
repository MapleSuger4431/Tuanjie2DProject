using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 关卡配置画笔：用「一维化的二维数组」在 Inspector 上配整盘格子的初始状态，一键应用到棋盘子物体（一一对应）。
    /// Unity 不序列化 int[,]，二维数组用「扁平 int 数组 + 边长」表达，行主序：索引 = y × size + x，与 <see cref="BoardModel" /> 的 Index 规则一致。
    /// 数值 ↔ <see cref="TileType" /> 直接对齐：0 空 / 1 路 / 2 火 / 3 冰 / 4 山地；左下角固定起点、右上角固定终点，不占数组值（应用时自动写入）。
    /// 子物体逐个配的效果走 <see cref="CellEffect" />，与本数组互不影响。
    /// </summary>
    [ExecuteAlways]
    public sealed class BoardLevelPainter : MonoBehaviour
    {
        /// <summary>可循环的状态数量：0 ~ 4 共五种。</summary>
        public const int StateCount = 5;

        [Tooltip("棋盘渲染器；为空时自动取同物体上的 BoardGridView")]
        [SerializeField] BoardGridView gridView;

        [Tooltip("棋盘边长（正方形，10 ~ 20）")]
        [SerializeField] int size = BoardModel.MinSize;

        [Tooltip("格子初始状态，行主序（索引 = y × 边长 + x），取值 0~4：0 空 / 1 路 / 2 火 / 3 冰 / 4 山地")]
        [SerializeField] int[] states = new int[BoardModel.MinSize * BoardModel.MinSize];

        public int Size => size;

        /// <summary>数值 → 格子状态（越界值夹到 0~4）。</summary>
        public static TileType StateToTileType(int state) =>
            (TileType)Mathf.Clamp(state, (int)TileType.Void, (int)TileType.Mountain);

        /// <summary>格子状态 → 数值；起点/终点不在 0~4 内，回落 0（它们由棋盘自动写入）。</summary>
        public static int TileTypeToState(TileType type)
        {
            var value = (int)type;
            return value >= 0 && value < StateCount ? value : 0;
        }

        /// <summary>数值循环：左键 +1，右键 -1。</summary>
        public static int Cycle(int state, int delta)
        {
            var next = (state + delta) % StateCount;
            return next < 0 ? next + StateCount : next;
        }

        public static Color StateColor(int state) => TileTypeInfo.Color(StateToTileType(state));

        public static string StateName(int state)
        {
            switch (StateToTileType(state))
            {
                case TileType.Land: return "路";
                case TileType.Fire: return "火";
                case TileType.Ice: return "冰";
                case TileType.Mountain: return "山地";
                default: return "空";
            }
        }

        public bool InBounds(int x, int y) => x >= 0 && x < size && y >= 0 && y < size;

        /// <summary>左下角：固定起点，不参与数组循环。</summary>
        public bool IsStartCell(int x, int y) => x == 0 && y == 0;

        /// <summary>右上角：固定终点，不参与数组循环。</summary>
        public bool IsGoalCell(int x, int y) => x == size - 1 && y == size - 1;

        /// <summary>该格应用后的最终状态（含起点/终点覆盖）。</summary>
        public TileType FinalTypeAt(int x, int y)
        {
            if (IsStartCell(x, y)) return TileType.Start;
            if (IsGoalCell(x, y)) return TileType.Goal;
            return StateToTileType(StateAt(x, y));
        }

        /// <summary>读格值；数组与边长尚未同步时也不会越界（返回 0）。</summary>
        public int StateAt(int x, int y)
        {
            if (states == null || x < 0 || x >= size || y < 0 || y >= size) return 0;

            var index = y * size + x;
            return index < 0 || index >= states.Length ? 0 : Mathf.Clamp(states[index], 0, StateCount - 1);
        }

        public void SetState(int x, int y, int state)
        {
            if (x < 0 || x >= size || y < 0 || y >= size) return;

            EnsureArrayLength();
            var index = y * size + x;
            if (index < 0 || index >= states.Length) return;
            states[index] = Mathf.Clamp(state, 0, StateCount - 1);
        }

        /// <summary>点击循环：delta 传 +1 或 -1。</summary>
        public void CycleState(int x, int y, int delta) => SetState(x, y, Cycle(StateAt(x, y), delta));

        /// <summary>整盘置 0（空）。</summary>
        public void ClearStates()
        {
            EnsureArrayLength();
            for (var i = 0; i < states.Length; i++) states[i] = 0;
        }

        /// <summary>改边长并按坐标保留重叠区域（放大补 0，缩小丢外圈）。</summary>
        public void Resize(int newSize)
        {
            var target = Mathf.Clamp(newSize, BoardModel.MinSize, BoardModel.MaxSize);
            if (target == size && states != null && states.Length == size * size) return;

            var old = states;
            var oldSize = size;
            size = target;
            states = new int[size * size];

            if (old == null) return;

            var copyWidth = Mathf.Min(oldSize, size);
            for (var y = 0; y < copyWidth; y++)
            {
                for (var x = 0; x < copyWidth; x++)
                {
                    var oldIndex = y * oldSize + x;
                    if (oldIndex < 0 || oldIndex >= old.Length) continue;
                    states[y * size + x] = Mathf.Clamp(old[oldIndex], 0, StateCount - 1);
                }
            }
        }

        /// <summary>保证数组长度 = 边长²（保留已有值）。</summary>
        public void EnsureArrayLength()
        {
            size = Mathf.Clamp(size, BoardModel.MinSize, BoardModel.MaxSize);
            if (states != null && states.Length == size * size) return;

            var old = states;
            var oldSize = old != null ? (int)Mathf.Sqrt(old.Length) : 0;
            states = new int[size * size];
            if (old == null) return;

            var copyWidth = Mathf.Min(oldSize, size);
            for (var y = 0; y < copyWidth; y++)
            {
                for (var x = 0; x < copyWidth; x++)
                {
                    var oldIndex = y * oldSize + x;
                    if (oldIndex < 0 || oldIndex >= old.Length) continue;
                    states[y * size + x] = Mathf.Clamp(old[oldIndex], 0, StateCount - 1);
                }
            }
        }

        /// <summary>取棋盘渲染器（同物体自动兜底）。</summary>
        public BoardGridView ResolveGrid()
        {
            if (gridView == null) gridView = GetComponent<BoardGridView>();
            return gridView;
        }

        /// <summary>收集棋盘下全部格子（用于应用/读取）。</summary>
        public List<BoardTile> CollectTiles()
        {
            var result = new List<BoardTile>();
            var grid = ResolveGrid();
            if (grid == null) return result;

            result.AddRange(grid.GetComponentsInChildren<BoardTile>(true));
            return result;
        }

        /// <summary>
        /// 一键应用：数组尺寸与棋盘不一致或还没有格子时先按数组尺寸程序化生成，再把数组值逐格写进子物体（一一对应）。
        /// 起点（左下）与终点（右上）由棋盘自动写入，不取数组值。返回写入格数。
        /// </summary>
        public int ApplyToTiles()
        {
            var grid = ResolveGrid();
            if (grid == null)
            {
                Debug.LogWarning("[BoardLevelPainter] 同物体上没有 BoardGridView，无法应用。", this);
                return 0;
            }

            EnsureArrayLength();

            var map = BuildTileMap(grid);
            if (map.Count == 0 || grid.BoardSize != size)
            {
                grid.BuildTiles(size);
                map = BuildTileMap(grid);
            }

            var applied = 0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var coord = new Vector2Int(x, y);
                    if (!map.TryGetValue(coord, out var tile) || tile == null) continue;

                    tile.Configure(coord, FinalTypeAt(x, y));
                    applied++;
                }
            }

            return applied;
        }

        /// <summary>反向同步：把子物体上已配的预设状态读回数组（起点/终点记为 0），边长跟随棋盘。返回读取格数。</summary>
        public int CaptureFromTiles()
        {
            var grid = ResolveGrid();
            if (grid == null)
            {
                Debug.LogWarning("[BoardLevelPainter] 同物体上没有 BoardGridView，无法读取。", this);
                return 0;
            }

            var map = BuildTileMap(grid);
            if (map.Count == 0) return 0;

            size = Mathf.Clamp(grid.BoardSize, BoardModel.MinSize, BoardModel.MaxSize);
            EnsureArrayLength();

            var read = 0;
            foreach (var pair in map)
            {
                if (pair.Value == null || !InBounds(pair.Key.x, pair.Key.y)) continue;

                states[pair.Key.y * size + pair.Key.x] = TileTypeToState(pair.Value.PresetType);
                read++;
            }

            return read;
        }

        /// <summary>按当前边长整盘重建格子（会清掉子物体，含逐个配的效果）。</summary>
        public void GenerateTiles()
        {
            var grid = ResolveGrid();
            if (grid == null) return;

            grid.BuildTiles(size);
        }

        void Reset()
        {
            ResolveGrid();
            if (gridView != null) size = Mathf.Clamp(gridView.BoardSize, BoardModel.MinSize, BoardModel.MaxSize);
            EnsureArrayLength();
        }

        void OnValidate() => EnsureArrayLength();

        static Dictionary<Vector2Int, BoardTile> BuildTileMap(BoardGridView grid)
        {
            var map = new Dictionary<Vector2Int, BoardTile>();
            foreach (var tile in grid.GetComponentsInChildren<BoardTile>(true))
            {
                if (tile != null && !map.ContainsKey(tile.Cell)) map.Add(tile.Cell, tile);
            }

            return map;
        }
    }
}
