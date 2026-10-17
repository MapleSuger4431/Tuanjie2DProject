using UnityEngine;
using UnityEngine.UI;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;

namespace GameRuntimeTest
{
    /// <summary>常驻 HUD：显示得分 / 倒计时 / 流程状态与操作提示，提供暂停按钮。</summary>
    public class RuntimeHudPanel : AesirBasePanelViewController<GameRuntimeTestContext>
    {
        [SerializeField] Text scoreText;
        [SerializeField] Text timeText;
        [SerializeField] Text stateText;
        [SerializeField] Text hintText;
        [SerializeField] Button pauseButton;

        RuntimeFlowModel _flow;
        RuntimeScoreModel _score;

        protected override void OnInit()
        {
            RuntimeTestFonts.Apply(transform);
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);

            _flow = this.GetModel<RuntimeFlowModel>();
            _score = this.GetModel<RuntimeScoreModel>();

            _score.Score.AddListenerAndInvoke(OnScoreChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _flow.TimeLeft.AddListenerAndInvoke(OnTimeLeftChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _flow.State.AddListenerAndInvoke(OnStateChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);

            if (hintText != null)
                hintText.text = "WASD/方向键 移动　空格 得分　Esc 暂停";
        }

        void OnEnable()
        {
            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        void OnDisable()
        {
            if (pauseButton != null)
                pauseButton.onClick.RemoveListener(OnPauseButtonClicked);
        }

        void OnPauseButtonClicked()
        {
            if (_flow != null && _flow.IsPlaying)
                _flow.Enter(RuntimeFlowState.Paused);
        }

        void OnScoreChanged(int score)
        {
            if (scoreText != null)
                scoreText.text = $"得分：{score}";
        }

        void OnTimeLeftChanged(float timeLeft)
        {
            if (timeText != null)
                timeText.text = $"时间：{timeLeft:0.0}";
        }

        void OnStateChanged(RuntimeFlowState state)
        {
            if (stateText == null) return;
            switch (state)
            {
                case RuntimeFlowState.Playing:
                    stateText.text = string.Empty;
                    break;
                case RuntimeFlowState.Paused:
                    stateText.text = "已暂停";
                    break;
                case RuntimeFlowState.GameOver:
                    stateText.text = "游戏结束　按 R 重开";
                    break;
            }
        }
    }
}
