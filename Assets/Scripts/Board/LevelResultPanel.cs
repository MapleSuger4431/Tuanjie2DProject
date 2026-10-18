using System.Collections;
using Game.Core;
using Game.View;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Board
{
    /// <summary>
    /// 关卡结算面板基类（**Panel 形态**：RectTransform 根、无自带 Canvas，挂 UIRoot 的 Popup 层）。
    /// 胜负共用「重玩此关 / 回到选关界面」两个出口按钮与全屏蒙版，差异只在标题与成绩区，
    /// 故公共行为集中在此：按钮绑定、语言刷新、出口跳转。
    /// <para>
    /// 打开走 <c>UIModule.Show&lt;T&gt;(path: UIPaths.Xxx)</c>（由 <see cref="BoardRuntimePanel" /> 在
    /// <see cref="BoardRunModel.RunFinished" /> 时按胜负二选一拉起），关闭走 <see cref="AesirBasePanel.HideSelf" />。
    /// 蒙版只用 Image 拦截射线（不加 Button）：结算必须二选一，点空白不关闭。
    /// </para>
    /// </summary>
    public abstract class LevelResultPanel : AesirBasePanelViewController<BoardContext>
    {
        [Tooltip("结算标题（通关成功 / 通关失败）")]
        [SerializeField]
        TMP_Text titleLabel;

        [Tooltip("成绩行：步数")]
        [SerializeField]
        TMP_Text stepsLabel;

        [Tooltip("成绩行：已用方块 / 上限")]
        [SerializeField]
        TMP_Text piecesLabel;

        [Tooltip("重玩此关按钮")]
        [SerializeField]
        Button retryButton;

        [Tooltip("重玩此关按钮文本")]
        [SerializeField]
        TMP_Text retryButtonLabel;

        [Tooltip("回到选关界面按钮")]
        [SerializeField]
        Button backToSelectButton;

        [Tooltip("回到选关界面按钮文本")]
        [SerializeField]
        TMP_Text backToSelectButtonLabel;

        [Header("弹出动画")]
        [Tooltip("羊皮纸卡片：弹入动画的缩放目标")]
        [SerializeField]
        RectTransform cardRoot;

        [Tooltip("卡片的 CanvasGroup：弹入时整卡淡入")]
        [SerializeField]
        CanvasGroup cardGroup;

        [Tooltip("弹入时长（秒）")]
        [SerializeField, Min(0.01f)]
        float popInDuration = 0.2f;

        [Tooltip("弹入起始缩放（相对 1）")]
        [SerializeField, Range(0.5f, 1f)]
        float popInFromScale = 0.85f;

        Coroutine _popIn;

        /// <summary>本面板的标题本地化键（子类给出）。</summary>
        protected abstract Localization.Key TitleKey { get; }

        protected BoardRunModel Run { get; private set; }

        /// <summary>命名刻意避开 <c>Time</c>：那是 UnityEngine.Time 的静态类名，成员同名会遮蔽它。</summary>
        protected BoardTimeModel Timer { get; private set; }

        /// <summary>已用方块数与上限来自棋盘模型。</summary>
        protected BoardModel Board { get; private set; }

        protected override void OnInit()
        {
            Run = this.GetModel<BoardRunModel>();
            Timer = this.GetModel<BoardTimeModel>();
            Board = this.GetModel<BoardModel>();

            if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
            if (backToSelectButton != null) backToSelectButton.onClick.AddListener(OnBackToSelectClicked);

            // 语言切换即时刷新（打开中切语言也要跟着变）
            Localization.OnLanguageChanged += ApplyLocalization;
            ApplyLocalization();
        }

        protected override void OnDestroy()
        {
            Localization.OnLanguageChanged -= ApplyLocalization;
            base.OnDestroy();
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);

            // 成绩区在每次打开时取最新值（同一实例复用时秒表可能已重置）
            ApplyResultText();

            PlayPopIn();
        }

        /// <summary>按当前语言刷新标题、按钮与成绩区；子类覆写<see cref="ApplyResultText" />补自己的成绩行。</summary>
        protected virtual void ApplyLocalization()
        {
            if (titleLabel != null) titleLabel.text = Localization.Get(TitleKey);
            if (retryButtonLabel != null) retryButtonLabel.text = Localization.Get(Localization.Key.LevelResultRetry);
            if (backToSelectButtonLabel != null)
                backToSelectButtonLabel.text = Localization.Get(Localization.Key.LevelResultBackToSelect);

            ApplyResultText();
        }

        /// <summary>
        /// 刷新成绩区两行（步数 / 已用方块）：胜负面板共用，差异只在标题与用时行。
        /// 每次打开/切语言都重新取当前值——同一实例复用时这些值可能已被重开归零。
        /// 用时不在基类：失败页不记时间，只有 <see cref="LevelClearPanel" /> 覆写补这一行。
        /// </summary>
        protected virtual void ApplyResultText()
        {
            if (Run == null) return;

            if (stepsLabel != null)
                stepsLabel.text = Localization.Get(Localization.Key.LevelResultSteps, Run.StepCount.Value);

            if (piecesLabel != null && Board != null)
                piecesLabel.text = Localization.Get(Localization.Key.LevelResultPieces,
                    Board.CoveredCount.Value, Board.PieceLimit);
        }

        /// <summary>重玩此关：完整重开（清盘 + 重抽 + 计时归零）后收起结算面板。</summary>
        void OnRetryClicked()
        {
            Run?.RestartLevel();
            HideSelf();
        }

        /// <summary>回到选关界面：带加载过场（先盖住结算面板再加载）。</summary>
        void OnBackToSelectClicked()
        {
            LoadingPanel.Begin(ScenePaths.LevelSelect);
        }

        /// <summary>
        /// 轻量弹入（用户拍板）：卡片从 <see cref="popInFromScale" /> 缩放 + 全透明弹到原位，
        /// back ease-out 结尾轻微过冲再回落；走未缩放时间，暂停 / 慢放时节奏与真实秒数一致。
        /// 星星逐颗点亮演出与此并行（星星在卡片内部，跟着卡片一起缩放）。
        /// </summary>
        void PlayPopIn()
        {
            if (cardRoot == null) return;

            if (_popIn != null) StopCoroutine(_popIn);
            _popIn = StartCoroutine(PopIn());
        }

        IEnumerator PopIn()
        {
            var start = Vector3.one * popInFromScale;
            cardRoot.localScale = start;
            if (cardGroup != null) cardGroup.alpha = 0f;

            // back ease-out 标准系数：t=0 起于 0、t=1 归 1、中途过冲约 10%
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            var time = 0f;
            while (time < popInDuration)
            {
                time += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(time / popInDuration);
                var eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                cardRoot.localScale = Vector3.LerpUnclamped(start, Vector3.one, eased);
                if (cardGroup != null) cardGroup.alpha = t;
                yield return null;
            }

            cardRoot.localScale = Vector3.one;
            if (cardGroup != null) cardGroup.alpha = 1f;
            _popIn = null;
        }
    }
}
