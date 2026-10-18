using System;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// 运行阶段与 AI 状态：铺路 → 运行 → 结算的三态机 + AI 的当前格、来路格与胜负结果。
    /// AI 单步决策**没有方向偏好、也不做全局寻路（无 BFS）**：出生朝 <see cref="InitialDirection" />，
    /// 之后一路直行，只有踩到带转向箭头（<see cref="ForcedTurnEffect" />）的格子才改朝向。
    /// AI 另有元素属性（火人 / 冰人，06 篇 RB-09，出生值按关卡配置 <see cref="BoardRoot.InitialElement" />）：
    /// 火格需火属性、冰格需冰属性，属性反转效果（<see cref="ElementSwitchEffect" />）踏入即翻转。
    /// 前方不可进入即停下判负；另有步数上限兜底，防两个互指的箭头把 AI 锁成死循环。
    /// 起点不可作普通路径格（AI 只在那里出生），终点由本类特判「进入即胜利」。
    /// </summary>
    [Serializable]
    public sealed class BoardRunModel : AbstractModel
    {
        /// <summary>关卡阶段。</summary>
        public enum Phase
        {
            /// <summary>铺路阶段：可放方块，用时照算；点 Play 直接进入运行。</summary>
            Building,

            /// <summary>运行阶段：锁定放置，AI 逐步行走，秒表继续。</summary>
            Running,

            /// <summary>已结算：AI 抵达终点（胜利）或无路可走（失败）。</summary>
            Settled,
        }

        /// <summary>结算结果。</summary>
        public enum RunResult
        {
            /// <summary>尚未结算。</summary>
            None,

            /// <summary>抵达终点。</summary>
            Win,

            /// <summary>无合法下一步，原地停下。</summary>
            Lose,
        }

        [SerializeField] ObservableValue<Phase> phase = new ObservableValue<Phase>(Phase.Building);
        [SerializeField] ObservableValue<RunResult> result = new ObservableValue<RunResult>(RunResult.None);
        [SerializeField] ObservableValue<Vector2Int> aiCell = new ObservableValue<Vector2Int>(default);
        [Tooltip("AI 出生朝向：出发这一步的方向不来自箭头，之后一路直行直到踩到箭头格")]
        [SerializeField] MoveDirection initialDirection = MoveDirection.Right;

        [Tooltip("单局步数上限：互指的箭头会让 AI 绕圈，到这里直接判负（兜底，正常通关远小于此）")]
        [SerializeField, Min(1)] int maxSteps = 200;

        [SerializeField] ObservableValue<int> stepCount = new ObservableValue<int>(0);

        [SerializeField] ObservableValue<ElementState> element = new ObservableValue<ElementState>(ElementState.Fire);

        /// <summary>上一步所在的格（禁止 180° 掉头的判据）；初始等于起点，表示「还没有来路」。</summary>
        Vector2Int _previousCell;

        MoveDirection? _lastDirection;

        /// <summary>效果结算出来的强制朝向，只对下一步生效。</summary>
        MoveDirection? _forcedDirection;

        /// <summary>效果结算的复用缓冲：每步先清空再交给效果填，避免逐步分配。</summary>
        readonly TileEffectResult _effectResult = new TileEffectResult();

        BoardModel _board;
        BoardTimeModel _time;
        PieceModel _piece;

        /// <summary>AI 位置变化（含出发与重置）后触发 → 视图插值移动。</summary>
        public MiniEvent AiMoved { get; } = new MiniEvent();

        /// <summary>结算（胜或负）时触发一次 → 刷新 HUD 与后续结算面板。</summary>
        public MiniEvent RunFinished { get; } = new MiniEvent();

        public ObservableValue<Phase> CurrentPhase => phase;
        public ObservableValue<RunResult> Result => result;
        public ObservableValue<Vector2Int> AiCell => aiCell;
        public ObservableValue<int> StepCount => stepCount;

        /// <summary>AI 当前元素属性（火 / 冰）：出生按关卡配置，反转效果踏入即翻转，重开时复位。</summary>
        public ObservableValue<ElementState> Element => element;

        public bool IsBuilding => phase.Value == Phase.Building;
        public bool IsRunning => phase.Value == Phase.Running;

        /// <summary>本步选用的方向；尚未移动时为 null（UI 高亮用）。</summary>
        public MoveDirection? LastDirection => _lastDirection;

        /// <summary>AI 出生朝向（场景中可配）：出发这一步与之后没有箭头时的朝向。</summary>
        public MoveDirection InitialDirection => initialDirection;

        protected override void OnInitialize()
        {
            _board = this.GetModel<BoardModel>();
            _time = this.GetModel<BoardTimeModel>();
            _piece = this.GetModel<PieceModel>();

            // 棋盘尺寸变化（切关卡）时整局重置，避免 AI 停在旧坐标上
            _board.BoardResized.AddListener(ResetRun);

            ResetRun();
        }

        /// <summary>回到铺路阶段并把 AI 放回起点；不清理已铺的棋盘。铺路阶段照算用时，故秒表一并重新起表。</summary>
        public void ResetRun()
        {
            phase.Value = Phase.Building;
            result.Value = RunResult.None;
            stepCount.Value = 0;
            _lastDirection = null;
            _forcedDirection = null;
            element.Value = ResolveInitialElement();

            var start = _board != null ? _board.StartCell : Vector2Int.zero;
            _previousCell = start;
            aiCell.Value = start;
            RestartStopwatch();

            AiMoved.Invoke();
        }

        /// <summary>秒表重新起表（归零并进入计时）：铺路本身也计入用时。</summary>
        void RestartStopwatch()
        {
            if (_time == null) return;

            _time.Reset();
            _time.StartTiming();
        }

        /// <summary>
        /// 完整重开本关：还原关卡预设地形（清掉全部已铺方块）、重抽当前与下一块、AI 回起点、计时归零、阶段回到铺路。
        /// 顺序固定——先还原棋盘把已用数归零，再重抽方块（否则会被上限判据挡住），最后重置运行态。
        /// </summary>
        public void RestartLevel()
        {
            _board?.RestorePreset();
            _piece?.Reroll();
            ResetRun();
        }

        /// <summary>
        /// 点击 Play：铺路阶段直接进入运行阶段（方向偏好已删，铺完即可出发），锁定放置，
        /// AI 从起点按出生朝向出发。秒表从铺路起一直在走，这里无需再动表。
        /// </summary>
        public bool Play()
        {
            if (_board == null || phase.Value != Phase.Building) return false;

            var start = _board.StartCell;
            result.Value = RunResult.None;
            stepCount.Value = 0;
            _lastDirection = null;
            _forcedDirection = null;
            element.Value = ResolveInitialElement();
            _previousCell = start;
            aiCell.Value = start;
            phase.Value = Phase.Running;

            AiMoved.Invoke();

            // 起点紧贴终点（极小棋盘）等边界情况：出发即在终点上
            if (start == _board.GoalCell)
            {
                Finish(RunResult.Win);
                return true;
            }

            // 起点上若钉了箭头，出发前先结算一次（关卡设计者可以靠它给定初始朝向）
            ApplyEnterEffect(start);

            return true;
        }

        /// <summary>
        /// 走一步：朝向 = 箭头强制方向（若有） &gt; 上一步朝向 &gt; 出生朝向，只朝这个方向走一格。
        /// 前方不可进入（越界 / 空地 / 山地 / 起点）即原地停下判负；踏入终点即判胜。
        /// 非运行阶段调用无效，便于驱动方无脑每帧调用。
        /// </summary>
        public void Step()
        {
            if (!IsRunning || _board == null) return;

            var from = aiCell.Value;
            var direction = ResolveDirection();
            var target = from + MoveDirectionInfo.Offset(direction);

            if (!CanEnter(target))
            {
                Finish(RunResult.Lose);
                return;
            }

            _previousCell = from;
            _lastDirection = direction;
            aiCell.Value = target;
            stepCount.Value += 1;
            AiMoved.Invoke();

            if (target == _board.GoalCell)
            {
                Finish(RunResult.Win);
                return;
            }

            // 兜底：两个互指的箭头会把 AI 锁成死循环，到这里判负而不是把编辑器卡死
            if (stepCount.Value >= maxSteps)
            {
                Debug.LogWarning($"[BoardRunModel] 步数达到上限 {maxSteps}，疑似箭头绕圈，本局判负");
                Finish(RunResult.Lose);
                return;
            }

            // 落点格上的效果（转向箭头等）在踏入的这一步结算，结果作用于下一步
            ApplyEnterEffect(target);
        }

        /// <summary>本步朝向：效果强制的方向（只对下一步生效）优先，其次沿用上一步，都没有就用出生朝向。</summary>
        MoveDirection ResolveDirection()
        {
            if (_forcedDirection.HasValue)
            {
                var forced = _forcedDirection.Value;
                _forcedDirection = null;
                return forced;
            }

            return _lastDirection ?? initialDirection;
        }

        /// <summary>
        /// 本关出生元素属性：按关卡配置（<see cref="BoardRoot.InitialElement" />，每个关卡场景在 Inspector 里各配各的）；
        /// 棋盘根还没就绪（纯数据层直驱 / 测试环境）时回落火人。
        /// </summary>
        ElementState ResolveInitialElement()
        {
            var root = BoardRoot.Instance;
            return root != null ? root.InitialElement : ElementState.Fire;
        }

        /// <summary>
        /// 结算 AI 踏入某格的效果：效果只往 <see cref="TileEffectResult" /> 里填结果，由本类统一应用——
        /// 效果自己不许改棋盘、也不许改 AI 位置。一步最多结算一个效果，落点不再二次结算（天然防互指死循环）。
        /// </summary>
        void ApplyEnterEffect(Vector2Int cell)
        {
            if (_board == null) return;

            // 棋盘根是跨格联动的唯一入口（传送门找配对门要全盘扫描）；它还没建好时退回数据层
            var root = BoardRoot.Instance;
            var effect = root != null ? root.EffectAt(cell) : _board.EffectAt(cell);
            if (effect == null) return;

            // 已消耗的效果（传送门用过）不再触发：消耗状态存在数据层，不写进效果资产
            if (root != null && root.IsConsumed(cell)) return;

            _effectResult.Clear();
            var pawn = new AiPawnState
            {
                Cell = cell,
                PreviousCell = _previousCell,
                LastDirection = _lastDirection,
                StepCount = stepCount.Value,
                Element = element.Value,
            };

            effect.OnEnter(new TileEffectContext(root, cell, pawn, _effectResult));

            if (_effectResult.Stop)
            {
                Finish(RunResult.Lose);
                return;
            }

            // 属性反转等元素转换：立即改写 AI 属性，影响后续每一步的通行判定（含本格效果里带出的传送落点判定）
            if (_effectResult.SetElement.HasValue) element.Value = _effectResult.SetElement.Value;

            if (_effectResult.ForcedNext.HasValue) _forcedDirection = _effectResult.ForcedNext;
            if (_effectResult.ClearIncoming) _previousCell = cell;

            var destination = _effectResult.TeleportTo;
            if (!destination.HasValue) return;

            // 传送不计步数；来路按效果要求清空，否则落点会禁掉回头这一步
            var teleportTo = destination.Value;
            if (!CanEnter(teleportTo)) return;

            _previousCell = _effectResult.ClearIncoming ? teleportTo : cell;
            aiCell.Value = teleportTo;
            AiMoved.Invoke();

            if (teleportTo == _board.GoalCell) Finish(RunResult.Win);
        }

        /// <summary>
        /// AI 能否进入该格：界内，且是可行走块（路/火/冰）或终点；火格还需火属性、冰格还需冰属性（06 篇 RB-09），
        /// 陆地任意属性可走。起点与山地一律不可进入。
        /// </summary>
        bool CanEnter(Vector2Int cell)
        {
            if (!_board.InBounds(cell)) return false;
            if (cell == _board.GoalCell) return true;

            var type = _board.TypeAt(cell);
            return TileTypeInfo.IsPassable(type) && ElementStateInfo.CanWalkOn(element.Value, type);
        }

        void Finish(RunResult value)
        {
            result.Value = value;
            phase.Value = Phase.Settled;
            _time?.StopTiming();
            RunFinished.Invoke();
        }
    }
}
