using System;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 关卡秒表（运行期时间唯一真源）：记录「按 Play 出发 → 抵达终点或中途停下」这一段的实际用时，越短越好。
    /// 三态：Idle 未开始 / Running 计时中 / Finished 已结束；每帧推进由 <see cref="BoardTimeTicker" /> 喂 deltaTime。
    /// 暂停只停本表、不写 Time.timeScale——暂停菜单（RB-20）走 timeScale=0 时本表随之停走，也可显式调 Pause/Resume。
    /// </summary>
    [Serializable]
    public sealed class BoardTimeModel : AbstractModel
    {
        public enum StopwatchState
        {
            /// <summary>未开始：铺路阶段、尚未按下 Play。</summary>
            Idle,
            /// <summary>计时中：从出发到抵达终点或停下。</summary>
            Running,
            /// <summary>已结束：冻结最终用时，供结算展示。</summary>
            Finished
        }

        [SerializeField] ObservableValue<StopwatchState> state = new ObservableValue<StopwatchState>(StopwatchState.Idle);
        [SerializeField] ObservableValue<float> elapsed = new ObservableValue<float>(0f);

        bool _paused;

        public ObservableValue<StopwatchState> State => state;

        /// <summary>已用时（秒），仅在 Running 且未暂停时增长。</summary>
        public ObservableValue<float> Elapsed => elapsed;

        public bool IsRunning => state.Value == StopwatchState.Running;
        public bool IsPaused => _paused;

        /// <summary>开始计时（按下 Play 时调用）：归零后进入计时中；已结束的表不再启动，保留最终成绩。</summary>
        public void StartTiming()
        {
            if (state.Value == StopwatchState.Finished) return;

            _paused = false;
            elapsed.Value = 0f;
            state.Value = StopwatchState.Running;
        }

        /// <summary>结束计时（抵达终点 / 失败停下时调用）：冻结当前用时。</summary>
        public void StopTiming()
        {
            if (!IsRunning) return;

            _paused = false;
            state.Value = StopwatchState.Finished;
        }

        public void Pause()
        {
            if (IsRunning) _paused = true;
        }

        public void Resume() => _paused = false;

        /// <summary>每帧推进；未在计时中或已暂停时忽略。</summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning || _paused || deltaTime <= 0f) return;

            elapsed.Value += deltaTime;
        }

        public void Reset()
        {
            _paused = false;
            elapsed.Value = 0f;
            state.Value = StopwatchState.Idle;
        }

        /// <summary>用时文本：mm:ss.d，HUD 直接显示。</summary>
        public string FormatElapsed()
        {
            var total = elapsed.Value;
            int minutes = Mathf.FloorToInt(total / 60f);
            float seconds = total - minutes * 60f;
            return $"{minutes:00}:{seconds:00.0}";
        }
    }
}
