using System;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// 关卡秒表（运行期时间唯一真源）：从进入关卡（铺路）起算，到抵达终点或中途停下为止，越短越好。
    /// 三态：Idle 未开始 / Running 计时中 / Finished 已结束；每帧推进由 <see cref="BoardTimeTicker" /> 喂 deltaTime。
    /// 暂停只停本表、不写 Time.timeScale——暂停菜单（RB-20）走 timeScale=0 时本表随之停走，也可显式调 Pause/Resume。
    /// 铺路阶段就在走表：点「铺路完成」转 <see cref="Pause" />（挑方向偏好不计时），点 Play 转 <see cref="Resume" /> 接着走。
    /// </summary>
    [Serializable]
    public sealed class BoardTimeModel : AbstractModel
    {
        public enum StopwatchState
        {
            /// <summary>未开始：秒表归零待命（重开/切关卡的过渡态）。</summary>
            Idle,
            /// <summary>计时中：铺路、AI 行走都在计；被显式暂停时不增长。</summary>
            Running,
            /// <summary>已结束：冻结最终用时，供结算展示。</summary>
            Finished
        }

        [SerializeField] ObservableValue<StopwatchState> state = new ObservableValue<StopwatchState>(StopwatchState.Idle);
        [SerializeField] ObservableValue<float> elapsed = new ObservableValue<float>(0f);
        [SerializeField] ObservableValue<bool> paused = new ObservableValue<bool>(false);

        public ObservableValue<StopwatchState> State => state;

        /// <summary>已用时（秒），仅在 Running 且未暂停时增长。</summary>
        public ObservableValue<float> Elapsed => elapsed;

        /// <summary>暂停中（铺路完成到点 Play 之间）：表在走但时间不计。</summary>
        public ObservableValue<bool> Paused => paused;

        public bool IsRunning => state.Value == StopwatchState.Running;
        public bool IsPaused => paused.Value;

        /// <summary>开始计时（按下 Play 时调用）：归零后进入计时中；已结束的表不再启动，保留最终成绩。</summary>
        public void StartTiming()
        {
            if (state.Value == StopwatchState.Finished) return;

            paused.Value = false;
            elapsed.Value = 0f;
            state.Value = StopwatchState.Running;
        }

        /// <summary>结束计时（抵达终点 / 失败停下时调用）：冻结当前用时。</summary>
        public void StopTiming()
        {
            if (!IsRunning) return;

            paused.Value = false;
            state.Value = StopwatchState.Finished;
        }

        public void Pause()
        {
            if (IsRunning) paused.Value = true;
        }

        public void Resume() => paused.Value = false;

        /// <summary>每帧推进；未在计时中或已暂停时忽略。</summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning || paused.Value || deltaTime <= 0f) return;

            elapsed.Value += deltaTime;
        }

        public void Reset()
        {
            paused.Value = false;
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
