using UnityEngine;
using Runestone.AesirModules;

namespace GameRuntimeTest
{
    /// <summary>
    /// 场景唯一的流程驱动：装配 UI（注册预制体 + 显示 HUD）、集中处理流程副作用
    /// （timeScale / 暂停窗口）、推进倒计时、响应重开。其它脚本不写 Time.timeScale。
    /// </summary>
    public sealed class GameRuntimeTestDriver : MonoBehaviour
    {
        [SerializeField] GameObject hudPanelPrefab;
        [SerializeField] GameObject pauseWindowPrefab;
        [SerializeField] float roundSeconds = 60f;

        RuntimeFlowModel _flow;
        RuntimeScoreModel _score;

        void Start()
        {
            UIModule.RegisterPrefab<RuntimeHudPanel>(hudPanelPrefab);
            UIModule.RegisterWindowPrefab<RuntimePauseWindow>(pauseWindowPrefab);
            UIModule.Show<RuntimeHudPanel>();

            _flow = GameRuntimeTestContext.Instance.GetModel<RuntimeFlowModel>();
            _score = GameRuntimeTestContext.Instance.GetModel<RuntimeScoreModel>();
            _flow.State.AddListenerAndInvoke(OnStateChanged);
            _flow.TimeLeft.Value = roundSeconds;
            _flow.Enter(RuntimeFlowState.Playing);
        }

        void Update()
        {
            // 暂停 / 重开是全场景唯一"不判态也要读"的输入，集中放在这里
            if (RuntimeTestInput.PausePressed)
            {
                if (_flow.IsPlaying) _flow.Enter(RuntimeFlowState.Paused);
                else if (_flow.IsPaused) _flow.Enter(RuntimeFlowState.Playing);
            }

            if (RuntimeTestInput.RestartPressed && !_flow.IsPlaying)
                Restart();

            if (!_flow.IsPlaying) return;

            if (RuntimeTestInput.FirePressed)
                _score.Score.Value += 1;

            var left = _flow.TimeLeft.Value - Time.deltaTime;
            _flow.TimeLeft.Value = left > 0f ? left : 0f;
            if (left <= 0f)
                _flow.Enter(RuntimeFlowState.GameOver);
        }

        void OnDestroy()
        {
            if (_flow != null)
                _flow.State.RemoveListener(OnStateChanged);
        }

        /// <summary>重开：Context 是纯 C# 单例不随场景销毁，必须先 Dispose 再重载场景。</summary>
        public static void Restart()
        {
            GameRuntimeTestContext.Instance.Dispose();
            SceneModule.ReloadScene(onFailed: () => Debug.LogError("[GameRuntimeTest] 重开失败"));
        }

        void OnStateChanged(RuntimeFlowState state)
        {
            Time.timeScale = state == RuntimeFlowState.Paused ? 0f : 1f;

            switch (state)
            {
                case RuntimeFlowState.Paused:
                    UIModule.Open<RuntimePauseWindow>();
                    break;
                case RuntimeFlowState.Playing:
                    UIModule.Close<RuntimePauseWindow>();
                    break;
                case RuntimeFlowState.GameOver:
                    UIModule.Close<RuntimePauseWindow>();
                    break;
            }
        }
    }
}
