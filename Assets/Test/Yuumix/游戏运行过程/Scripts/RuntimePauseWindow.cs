using UnityEngine;
using UnityEngine.UI;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;

namespace GameRuntimeTest
{
    /// <summary>暂停窗口（Window 形态，根自带 Canvas）：继续 / 重开。</summary>
    public class RuntimePauseWindow : AesirBaseWindowViewController<GameRuntimeTestContext>
    {
        [SerializeField] Text titleText;
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;

        RuntimeFlowModel _flow;

        protected override void OnInit()
        {
            RuntimeTestFonts.Apply(transform);
            if (titleText != null)
                titleText.text = "暂停";
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);
            _flow = this.GetModel<RuntimeFlowModel>();
        }

        void OnEnable()
        {
            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);
        }

        void OnDisable()
        {
            if (resumeButton != null)
                resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (restartButton != null)
                restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        void OnResumeClicked()
        {
            if (_flow != null && _flow.IsPaused)
                _flow.Enter(RuntimeFlowState.Playing);
        }

        void OnRestartClicked() => GameRuntimeTestDriver.Restart();
    }
}
