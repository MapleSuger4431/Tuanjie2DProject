using System;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// 运行阶段与 AI 状态：铺路 → 运行 → 结算的三态机 + AI 的当前格、来路格与胜负结果。
    /// AI 单步决策**只看当前格的四邻 1 格**，不做任何全局寻路（无 BFS）：
    /// 候选 = 四邻中「在界内 且（可行走块 或 终点）且 不是来路格」，按 <see cref="AiPreferenceModel" /> 的顺序取第一个。
    /// 起点不可作普通路径格（AI 只在那里出生），终点由本类特判「进入即胜利」。
    /// </summary>
    [Serializable]
    public sealed class BoardRunModel : AbstractModel
    {
        /// <summary>关卡阶段。</summary>
        public enum Phase
        {
            /// <summary>铺路阶段：可放方块，可改方向优先级，用时照算。</summary>
            Building,

            /// <summary>铺路完成：锁定放置、秒表暂停，只留方向优先级可调，等玩家点 Play。</summary>
            Ready,

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
        [SerializeField] ObservableValue<int> stepCount = new ObservableValue<int>(0);

        /// <summary>上一步所在的格（禁止 180° 掉头的判据）；初始等于起点，表示「还没有来路」。</summary>
        Vector2Int _previousCell;

        MoveDirection? _lastDirection;

        BoardModel _board;
        AiPreferenceModel _preference;
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

        public bool IsBuilding => phase.Value == Phase.Building;
        public bool IsReady => phase.Value == Phase.Ready;
        public bool IsRunning => phase.Value == Phase.Running;

        /// <summary>本步选用的方向；尚未移动时为 null（UI 高亮用）。</summary>
        public MoveDirection? LastDirection => _lastDirection;

        protected override void OnInitialize()
        {
            _board = this.GetModel<BoardModel>();
            _preference = this.GetModel<AiPreferenceModel>();
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

            var start = _board != null ? _board.StartCell : Vector2Int.zero;
            _previousCell = start;
            aiCell.Value = start;
            _preference?.SetLocked(false);
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
        /// 铺路完成：锁定放置并暂停秒表，方向优先级仍可调（挑偏好的这段时间不计入用时），等玩家点 Play。
        /// 一旦完成不回退——要改摆法走「重新开始本关」。
        /// </summary>
        public bool CompleteBuilding()
        {
            if (_board == null || phase.Value != Phase.Building) return false;

            phase.Value = Phase.Ready;
            _time?.Pause();

            return true;
        }

        /// <summary>解除秒表暂停：铺路阶段的时间已计入，这里接着走而不是重新起表。</summary>
        void ResumeStopwatch()
        {
            if (_time == null) return;

            if (_time.IsRunning) _time.Resume();
            else RestartStopwatch();
        }

        /// <summary>点击 Play：进入运行阶段，锁定放置与偏好顺序，AI 从起点出发，秒表解除暂停继续计。</summary>
        public bool Play()
        {
            if (_board == null || phase.Value != Phase.Ready) return false;

            var start = _board.StartCell;
            result.Value = RunResult.None;
            stepCount.Value = 0;
            _lastDirection = null;
            _previousCell = start;
            aiCell.Value = start;
            phase.Value = Phase.Running;

            _preference?.SetLocked(true);
            ResumeStopwatch();

            AiMoved.Invoke();

            // 起点紧贴终点（极小棋盘）等边界情况：出发即在终点上
            if (start == _board.GoalCell) Finish(RunResult.Win);

            return true;
        }

        /// <summary>
        /// 走一步：只看四邻 1 格。无候选（死路 / 只能掉头）则原地停下判负；踏入终点即判胜。
        /// 非运行阶段调用无效，便于驱动方无脑每帧调用。
        /// </summary>
        public void Step()
        {
            if (!IsRunning || _board == null || _preference == null) return;

            var from = aiCell.Value;

            var order = _preference.Order;
            for (var i = 0; i < order.Count; i++)
            {
                var direction = order[i];
                var target = from + MoveDirectionInfo.Offset(direction);

                // 禁止 180° 掉头：来路格永远不作候选
                if (target == _previousCell) continue;
                if (!CanEnter(target)) continue;

                _previousCell = from;
                _lastDirection = direction;
                aiCell.Value = target;
                stepCount.Value += 1;
                AiMoved.Invoke();

                if (target == _board.GoalCell) Finish(RunResult.Win);

                return;
            }

            Finish(RunResult.Lose);
        }

        /// <summary>AI 能否进入该格：界内，且是可行走块（路/火/冰）或终点。起点与山地一律不可进入。</summary>
        bool CanEnter(Vector2Int cell)
        {
            if (!_board.InBounds(cell)) return false;
            if (cell == _board.GoalCell) return true;

            return TileTypeInfo.IsPassable(_board.TypeAt(cell));
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
