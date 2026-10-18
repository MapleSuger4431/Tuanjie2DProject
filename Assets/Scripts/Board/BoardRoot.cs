using System.Collections.Generic;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// 棋盘根：整盘运行时数据的聚合节点，也是效果做跨格联动的**唯一**查询入口。
    /// 挂在 <see cref="BoardGridView" /> 同物体上。
    /// <para>
    /// 两种刷新：落盘 / 关卡加载 / 重开 / 尺寸变化走 <see cref="Refresh" /> 全量重建；
    /// 运行期每步只改状态位（<see cref="Consume" />），不做全量重建。
    /// </para>
    /// </summary>
    public sealed class BoardRoot : MonoViewController<BoardContext>
    {
        /// <summary>一格的运行时快照（数据层 + 视图层 + 效果消耗位）。</summary>
        public struct CellRuntime
        {
            public Vector2Int coord;
            public TileType type;
            public TileEffect effect;
            public bool consumed;
            public BoardTile view;
        }

        [SerializeField, Tooltip("为空时自动取同物体上的 BoardGridView")]
        BoardGridView gridView;

        [Header("关卡配置")]
        [SerializeField, Tooltip("本关 AI 出生时的元素属性：火人走不了冰格、冰人走不了火格（属性反转效果可中途翻转，重开复位到本值）")]
        ElementState initialElement = ElementState.Fire;

        BoardModel _board;
        CellRuntime[] _cells;
        int _width;
        int _height;
        int _lastEnsureFrame = -1;

        /// <summary>场景内单例：<see cref="BoardRunModel" /> 结算效果时从这里取。</summary>
        public static BoardRoot Instance { get; private set; }

        /// <summary>本关 AI 出生元素属性（关卡在 Inspector 里配置；<see cref="BoardRunModel" /> 在开局 / 重开时读取）。</summary>
        public ElementState InitialElement => initialElement;

        public int Width => _width;
        public int Height => _height;

        public bool IsReady => _cells != null && _width > 0 && _height > 0 && _cells.Length == _width * _height;

        // ---------- 生命周期 ----------

        void Start()
        {
            // ⚠️ 不要在 Awake 里赋值：MonoViewController 基类有自己的生命周期钩子
            Instance = this;

            if (gridView == null) gridView = GetComponent<BoardGridView>();
            _board = this.GetModel<BoardModel>();

            Refresh();

            if (_board != null)
            {
                _board.BoardCovered.AddListener(Refresh)
                    .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
                _board.BoardResized.AddListener(Refresh)
                    .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>从数据层拉整盘 + 从格子视图拉关卡预置效果；调用后顺带刷新效果图标。</summary>
        public void Refresh()
        {
            if (gridView == null) gridView = GetComponent<BoardGridView>();
            if (gridView == null) return;

            if (_board == null) _board = this.GetModel<BoardModel>();

            _width = gridView.Width;
            _height = gridView.Height;

            var count = _width * _height;
            if (_cells == null || _cells.Length != count) _cells = new CellRuntime[count];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var coord = new Vector2Int(x, y);
                    var index = y * _width + x;
                    var view = gridView.TileAt(coord);

                    var effect = _board != null ? _board.EffectAt(coord) : null;

                    // 数据层没写效果时回落到关卡预置（BoardTile 上拖的资产）
                    if (effect == null && view != null) effect = view.PresetEffect;

                    // 钉死的格子：无视数据层，强制用预置效果（关卡设计者不想被方块盖掉）
                    if (view != null && view.PinEffect) effect = view.PresetEffect;

                    _cells[index] = new CellRuntime
                    {
                        coord = coord,
                        type = _board != null ? _board.TypeAt(coord) : (view != null ? view.PresetType : TileType.Void),
                        effect = effect,
                        // 消耗状态从数据层读，不在这里维护：全盘扫描（传送门找最近）需要在数据层遍历
                        consumed = _board != null && _board.IsEffectConsumed(coord),
                        view = view,
                    };

                    if (view != null) view.ApplyEffectVisual(effect, _cells[index].consumed, _cells[index].type);
                }
            }
        }

        /// <summary>
        /// 按需补刷：棋盘视图的格子可能比本组件晚建好（Start 顺序不定），首次查询时才拉得到 view。
        /// 同帧最多刷一次，避免每次查询都全量重建。
        /// </summary>
        public void EnsureReady()
        {
            if (IsReady && !HasNullViews()) return;
            if (_lastEnsureFrame == Time.frameCount) return;

            _lastEnsureFrame = Time.frameCount;
            Refresh();
        }

        bool HasNullViews()
        {
            if (_cells == null) return true;

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].view == null) return true;
            }

            return false;
        }

        /// <summary>清空全部效果的运行期状态（重开本关时调用）；转发到数据层。</summary>
        public void ResetEffectStates()
        {
            _board?.ResetEffectStates();
            Refresh();
        }

        // ---------- 效果可用的查询（只读为主） ----------

        public bool InBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;

        public TileType TypeAt(Vector2Int cell)
        {
            EnsureReady();
            return InBounds(cell) ? _cells[Index(cell)].type : TileType.Void;
        }

        public TileEffect EffectAt(Vector2Int cell)
        {
            EnsureReady();
            return InBounds(cell) ? _cells[Index(cell)].effect : null;
        }

        public bool IsConsumed(Vector2Int cell)
        {
            EnsureReady();
            return InBounds(cell) && _cells[Index(cell)].consumed;
        }

        /// <summary>标记该格效果已消耗（传送门用过即失效）；实际写在数据层。</summary>
        public void Consume(Vector2Int cell)
        {
            EnsureReady();
            if (!InBounds(cell)) return;

            _board?.ConsumeEffect(cell);
            _cells[Index(cell)].consumed = true;
            _cells[Index(cell)].view?.ApplyEffectVisual(_cells[Index(cell)].effect, true, _cells[Index(cell)].type);
        }

        /// <summary>
        /// 找离 <paramref name="from" /> 最近的另一扇未消耗传送门；无配对返回 false（此时该门等同普通路格）。
        /// 并列时按 (y, x) 坐标序取唯一解，保证结果稳定。
        /// </summary>
        public bool TryFindNearestPortal(Vector2Int from, out Vector2Int target)
        {
            target = default;
            EnsureReady();
            if (!IsReady) return false;

            var found = false;
            var best = default(Vector2Int);
            var bestDistance = int.MaxValue;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var index = y * _width + x;
                    var cell = _cells[index];

                    if (cell.coord == from) continue;
                    if (cell.effect == null || !cell.effect.IsPortal) continue;
                    if (cell.consumed) continue;

                    var distance = Mathf.Abs(cell.coord.x - from.x) + Mathf.Abs(cell.coord.y - from.y);
                    if (found && !IsBetter(distance, cell.coord, bestDistance, best)) continue;

                    found = true;
                    best = cell.coord;
                    bestDistance = distance;
                }
            }

            target = best;
            return found;
        }

        /// <summary>距离更近；距离相同时按 (y, x) 升序取第一个，保证并列解唯一。</summary>
        static bool IsBetter(int distance, Vector2Int candidate, int bestDistance, Vector2Int best)
        {
            if (distance != bestDistance) return distance < bestDistance;
            if (candidate.y != best.y) return candidate.y < best.y;
            return candidate.x < best.x;
        }

        /// <summary>列出全盘带效果的格子（调试与未来 UI 提示用）。</summary>
        public List<Vector2Int> CellsWithEffect()
        {
            var result = new List<Vector2Int>();
            EnsureReady();
            if (!IsReady) return result;

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].effect != null) result.Add(_cells[i].coord);
            }

            return result;
        }

        public Vector3 CellToWorld(Vector2Int cell) =>
            gridView != null ? gridView.transform.TransformPoint(gridView.CellToLocal(cell)) : Vector3.zero;

        public BoardTile ViewAt(Vector2Int cell)
        {
            EnsureReady();
            return InBounds(cell) ? _cells[Index(cell)].view : null;
        }

        int Index(Vector2Int cell) => cell.y * _width + cell.x;
    }
}
