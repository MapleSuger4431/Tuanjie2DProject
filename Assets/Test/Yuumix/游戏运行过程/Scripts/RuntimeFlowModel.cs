using System;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace GameRuntimeTest
{
    public enum RuntimeFlowState
    {
        Playing,
        Paused,
        GameOver
    }

    /// <summary>流程状态与回合倒计时（运行时数据唯一真源）。</summary>
    [Serializable]
    public sealed class RuntimeFlowModel : AbstractModel
    {
        [SerializeField] ObservableValue<RuntimeFlowState> state =
            new ObservableValue<RuntimeFlowState>(RuntimeFlowState.Playing);

        [SerializeField] ObservableValue<float> timeLeft = new ObservableValue<float>(60f);

        public ObservableValue<RuntimeFlowState> State => state;
        public ObservableValue<float> TimeLeft => timeLeft;

        public bool IsPlaying => state.Value == RuntimeFlowState.Playing;
        public bool IsPaused => state.Value == RuntimeFlowState.Paused;

        /// <summary>流程写入约定入口：经它写入可让 Driver 的副作用（timeScale/暂停窗口）集中响应。</summary>
        public void Enter(RuntimeFlowState next) => state.Value = next;
    }
}
