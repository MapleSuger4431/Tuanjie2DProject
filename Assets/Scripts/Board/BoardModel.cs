using System;
using System.Collections.Generic;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
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

        /// <summary>最小棋盘（10×10）的方块上限。</summary>
        public const int MinPieces = 30;

        /// <summary>最大棋盘（20×20）的方块上限。</summary>
        public const int MaxPieces = 50;

        [SerializeField] int width = MinSize;
        [SerializeField] int height = MinSize;

        [SerializeField] ObservableValue<int> coveredCount = new ObservableValue<int>(0);

        // 起终点按关卡配置（关卡画笔 → 棋盘视图 → 本层下发）；非法值在访问器里回落角位，旧场景零迁移
        [SerializeField] Vector2Int startCell = Vector2Int.zero;
        [SerializeField] Vector2Int goalCell = new Vector2Int(-1, -1);

        TileType[] _tiles;

        /// <summary>关卡预设快照（<see cref="ApplyPreset" /> 时留存），供「重新开始本关」还原初始地形。</summary>
        TileType[] _presetTiles;

        /// <summary>效果层：与 <see cref="_tiles" /> 平行（行主序）。效果不改变通行性，只是叠在格子上的一层。</summary>
        TileEffect[] _effects;

        /// <summary>效果层的关卡预设快照，重开时还原。</summary>
        TileEffect[] _presetEffects;

        /// <summary>
        /// 效果的运行期状态（传送门已消耗）。只存在数据层——
        /// ScriptableObject 资产全局共享，写进去会跨格跨关跨 Play 污染且退出不还原。
        /// </summary>
        bool[] _consumed;

        public int Width => width;
        public int Height => height;

        /// <summary>本关方块上限：随棋盘边长线性插值（10×10→30 块，20×20→50 块，每 +1 边长 +2）。</summary>
        public int PieceLimit =>
            MinPieces + (Mathf.Max(width, height) - MinSize) * (MaxPieces - MinPieces) / (MaxSize - MinSize);

        /// <summary>起点：关卡可配（<see cref="SetStartGoal" />）；未配置或越界回落左下角。</summary>
        public Vector2Int StartCell => InBounds(startCell) ? startCell : Vector2Int.zero;

        /// <summary>终点：关卡可配（<see cref="SetStartGoal" />）；未配置、越界或与起点重合回落右上角。</summary>
        public Vector2Int GoalCell =>
            InBounds(goalCell) && goalCell != StartCell ? goalCell : new Vector2Int(width - 1, height - 1);

        /// <summary>配置起点/终点（关卡画笔与生成工具入口）；越界或重合的配置整组忽略。</summary>
        public void SetStartGoal(Vector2Int start, Vector2Int goal)
        {
            if (!InBounds(start) || !InBounds(goal) || start == goal)
            {
                Debug.LogWarning($"[BoardModel] 忽略非法的起点/终点配置：{start} / {goal}（须在棋盘内且不重合）");
                return;
            }

            if (start == StartCell && goal == GoalCell) return;

            startCell = start;
            goalCell = goal;
            TerminalsChanged.Invoke();
        }

        /// <summary>已发生的覆盖次数（每个方块算 1 次，非格数）。</summary>
        public ObservableValue<int> CoveredCount => coveredCount;

        /// <summary>覆盖写入成功后触发（一次性瞬时事件）→ 驱动 View 刷新渲染。</summary>
        public MiniEvent BoardCovered { get; } = new MiniEvent();

        /// <summary>尺寸变化后触发（先于数据清空）→ 驱动 View 按新尺寸重建格子。</summary>
        public MiniEvent BoardResized { get; } = new MiniEvent();

        /// <summary>起点 / 终点配置变化后触发 → 运行模型把 AI 放回新起点（画笔改起终点时由配置侧调用）。</summary>
        public MiniEvent TerminalsChanged { get; } = new MiniEvent();

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
            _effects = new TileEffect[width * height];
            coveredCount.Value = 0;
            BoardCovered.Invoke();
        }

        /// <summary>
        /// 还原到关卡预设：清空全部已铺方块、地形与效果层回到设计态，已用方块数归零（「重新开始本关」用）。
        /// 还没应用过预设时退化为整盘空。效果的运行期状态（传送门消耗）一并复位。
        /// </summary>
        public void RestorePreset()
        {
            if (_tiles == null || _tiles.Length != width * height) _tiles = new TileType[width * height];

            if (_presetTiles != null && _presetTiles.Length == _tiles.Length)
                Array.Copy(_presetTiles, _tiles, _tiles.Length);
            else
                Array.Clear(_tiles, 0, _tiles.Length);

            if (_effects == null || _effects.Length != width * height) _effects = new TileEffect[width * height];

            if (_presetEffects != null && _presetEffects.Length == _effects.Length)
                Array.Copy(_presetEffects, _effects, _effects.Length);
            else
                Array.Clear(_effects, 0, _effects.Length);

            ResetEffectStates();
            coveredCount.Value = 0;
            BoardCovered.Invoke();
        }

        /// <summary>用关卡预设初始化状态层（只含通行状态），并留存快照供重开还原。越界的预设项忽略。</summary>
        public void ApplyPreset(IEnumerable<KeyValuePair<Vector2Int, TileType>> preset) =>
            ApplyPreset(preset, null);

        /// <summary>
        /// 用关卡预设初始化两层：通行状态 <paramref name="preset" /> + 效果层 <paramref name="effects" />，并留存两份快照。
        /// 越界的预设项忽略；效果可为空（多数关卡没有预置效果）。
        /// </summary>
        public void ApplyPreset(
            IEnumerable<KeyValuePair<Vector2Int, TileType>> preset,
            IEnumerable<KeyValuePair<Vector2Int, TileEffect>> effects)
        {
            Reset();

            if (preset != null)
            {
                foreach (var entry in preset)
                {
                    if (!InBounds(entry.Key)) continue;
                    _tiles[Index(entry.Key)] = entry.Value;
                }
            }

            if (effects != null)
            {
                foreach (var entry in effects)
                {
                    if (!InBounds(entry.Key)) continue;
                    _effects[Index(entry.Key)] = entry.Value;
                }
            }

            _presetTiles = (TileType[])_tiles.Clone();
            _presetEffects = (TileEffect[])_effects.Clone();
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

        /// <summary>指定写入状态（起终点不是可铺设状态，按 Land 兜底；山地允许被方块铺出）。</summary>
        public bool TryCover(Vector2Int anchor, TileShape shape, TileType fill)
        {
            fill = NormalizeFill(fill);

            if (!CanCover(anchor, shape)) return false;

            EnsureArrays();

            for (int i = 0; i < shape.Cells.Length; i++)
            {
                var index = Index(anchor + shape.Cells[i]);
                _tiles[index] = fill;
                // 不带效果的方块铺过来会清掉该格原有的效果（钉死的格子由 BoardRoot 在读取时改回预置值）
                _effects[index] = null;
            }

            coveredCount.Value += 1;
            BoardCovered.Invoke();
            return true;
        }

        /// <summary>方块实例版合法性判定。</summary>
        public bool CanCover(Vector2Int anchor, TilePiece piece) => CanCover(anchor, piece?.Shape);

        /// <summary>
        /// 按方块实例逐格写入各自随机出来的状态（块内可以同时出现空 / 路 / 火 / 冰 / 山地）。
        /// 整块要么全写、要么不写。
        /// </summary>
        public bool TryCover(Vector2Int anchor, TilePiece piece)
        {
            var shape = piece != null ? piece.Shape : null;
            if (shape == null || shape.Cells == null || shape.Cells.Length == 0) return false;
            if (!CanCover(anchor, shape)) return false;

            EnsureArrays();

            var cells = shape.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                var index = Index(anchor + cells[i]);
                _tiles[index] = NormalizeFill(piece.TypeAt(i));
                _effects[index] = piece.EffectAt(i);
            }

            coveredCount.Value += 1;
            BoardCovered.Invoke();
            return true;
        }

        /// <summary>起终点只能由棋盘固定写入，方块若抽到就回落成路面；其余状态（含山地）原样落盘。</summary>
        static TileType NormalizeFill(TileType fill) =>
            fill == TileType.Start || fill == TileType.Goal ? TileType.Land : fill;

        // ---------- 效果层（与状态层平行，不改变通行性） ----------

        /// <summary>该格的效果；没有则 null（多数格子没有效果）。</summary>
        public TileEffect EffectAt(Vector2Int cell) =>
            _effects != null && InBounds(cell) ? _effects[Index(cell)] : null;

        /// <summary>调试与关卡画笔的写入口。</summary>
        public void SetEffect(Vector2Int cell, TileEffect effect)
        {
            EnsureArrays();
            if (!InBounds(cell)) return;

            _effects[Index(cell)] = effect;
            BoardCovered.Invoke();
        }

        /// <summary>该格的效果是否已经消耗掉（传送门用过）。</summary>
        public bool IsEffectConsumed(Vector2Int cell) =>
            _consumed != null && InBounds(cell) && _consumed[Index(cell)];

        /// <summary>标记该格效果已消耗。</summary>
        public void ConsumeEffect(Vector2Int cell)
        {
            EnsureArrays();
            if (!InBounds(cell)) return;

            _consumed[Index(cell)] = true;
        }

        /// <summary>清空全盘效果的运行期状态（重开本关时调用）。</summary>
        public void ResetEffectStates()
        {
            if (_consumed == null || _consumed.Length != width * height) _consumed = new bool[width * height];
            else Array.Clear(_consumed, 0, _consumed.Length);
        }

        /// <summary>保证状态层与效果层数组已按当前尺寸建好（尺寸变化后补齐）。</summary>
        void EnsureArrays()
        {
            var count = width * height;
            if (_tiles == null || _tiles.Length != count) _tiles = new TileType[count];
            if (_effects == null || _effects.Length != count) _effects = new TileEffect[count];
            if (_consumed == null || _consumed.Length != count) _consumed = new bool[count];
        }

        int Index(Vector2Int cell) => cell.y * width + cell.x;
    }
}
