using System.Collections.Generic;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Board
{
    /// <summary>
    /// 关卡内右下角的成就 / 消息小窗（**Panel 形态**：根只有 RectTransform，无自带 Canvas，挂 UIRoot 的 Top 层）。
    /// <para>
    /// 常驻打开（由 <see cref="BoardRuntimeDriver" /> 随关卡拉起、由 <c>MainMenuBoot</c> 随主菜单拉起），不做 Show/Hide 反复开关——
    /// <c>destroyOnHide</c> 默认 true，反复开关会销毁重建；这里只切右下角那张卡片的显隐与透明度。
    /// 切场景由 <see cref="Game.Core.SceneTransition" /> 的 sceneLoaded 钩子统一收掉，本类不写清理代码。
    /// </para>
    /// <para>
    /// 两条硬约束（04 篇 AC-04）：
    /// ① **不阻断操作**——整条链路 raycastTarget=false，不可点击、不抢输入焦点，鼠标照常放置方块；
    /// ② **排队播放**——播放中来的消息入队，当前条播完立刻播下一条，不叠加（队列满丢弃最旧的）。
    /// </para>
    /// 结构由 <c>Assets/Editor/LevelTipPanelBuilder.cs</c> 重建，改 UI 改生成器。
    /// 挂 <see cref="GameContext" />（不是关卡专属 Context）：成就通知在关卡内解锁，但主菜单 / 选关也要能弹。
    /// </para>
    /// </summary>
    public sealed class LevelTipPanel : AesirBasePanelViewController<GameContext>
    {
        /// <summary>播放阶段：空闲 → 淡入 → 停留 → 淡出 →（队列还有就回到淡入）。</summary>
        enum Stage
        {
            Idle,
            FadeIn,
            Hold,
            FadeOut,
        }

        [Header("结构")]
        [Tooltip("右下角那张做旧便签卡片（本面板唯一的可见节点）")]
        [SerializeField] RectTransform tipCard;

        [Tooltip("卡片的 CanvasGroup：淡入淡出只改它的 alpha")]
        [SerializeField] CanvasGroup cardGroup;

        [Tooltip("成就徽章图标")]
        [SerializeField] Image icon;

        [Tooltip("标题文本（如「成就解锁」）")]
        [SerializeField] TMP_Text titleText;

        [Tooltip("正文文本（成就名 / 数值）")]
        [SerializeField] TMP_Text messageText;

        [Header("素材")]
        [Tooltip("徽章贴图；未回填时用它补上（卡片底图由生成器直接挂在 Image 上）")]
        [SerializeField] Sprite iconSprite;

        [Header("节奏")]
        [Tooltip("淡入 / 淡出各自时长（秒）")]
        [SerializeField, Min(0.01f)] float fadeDuration = 0.25f;

        [Tooltip("完全显示后的停留时长（秒）；淡入 + 停留 + 淡出 = 5 秒")]
        [SerializeField, Min(0f)] float holdDuration = 4.5f;

        [Tooltip("排队上限：超出的丢弃最旧的一条")]
        [SerializeField, Min(1)] int maxQueued = 5;

        readonly Queue<BoardTipMessage> _queue = new Queue<BoardTipMessage>();

        Stage _stage = Stage.Idle;
        float _elapsed;
        BoardTipModel _tips;

        protected override void OnInit()
        {
            if (icon != null)
            {
                if (icon.sprite == null && iconSprite != null) icon.sprite = iconSprite;
                icon.raycastTarget = false;
            }

            // 面板整体不可点击：任何 Graphic 参与射线都会挡住棋盘放置（与 EnsureBoardFrame 同一个坑）
            foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            SetCardActive(false);
            SetAlpha(0f);
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);

            if (_tips == null)
            {
                _tips = this.GetModel<BoardTipModel>();
                if (_tips != null)
                    _tips.Message.AddListener(OnMessage).RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            }
        }

        void OnMessage(BoardTipMessage message)
        {
            if (message == null) return;

            while (_queue.Count >= maxQueued) _queue.Dequeue();

            _queue.Enqueue(message);
            if (_stage == Stage.Idle) PlayNext();
        }

        void Update()
        {
            if (_stage == Stage.Idle) return;

            // 用未缩放时间：暂停菜单 / 慢放时节奏与真实秒数一致
            _elapsed += Time.unscaledDeltaTime;

            switch (_stage)
            {
                case Stage.FadeIn:
                    SetAlpha(Mathf.Clamp01(_elapsed / fadeDuration));
                    if (_elapsed >= fadeDuration)
                    {
                        SetAlpha(1f);
                        Enter(Stage.Hold);
                    }

                    break;

                case Stage.Hold:
                    if (_elapsed >= holdDuration) Enter(Stage.FadeOut);
                    break;

                case Stage.FadeOut:
                    SetAlpha(Mathf.Clamp01(1f - _elapsed / fadeDuration));
                    if (_elapsed >= fadeDuration)
                    {
                        SetAlpha(0f);
                        SetCardActive(false);
                        Enter(Stage.Idle);
                        PlayNext();   // 队列里还有就接着播
                    }

                    break;
            }
        }

        /// <summary>取下一条消息开始播放；队列空则回到空闲。</summary>
        void PlayNext()
        {
            if (_queue.Count == 0)
            {
                _stage = Stage.Idle;
                return;
            }

            var message = _queue.Dequeue();
            if (titleText != null) titleText.text = message.Title;
            if (messageText != null) messageText.text = message.Body;

            SetCardActive(true);
            SetAlpha(0f);
            Enter(Stage.FadeIn);
        }

        void Enter(Stage stage)
        {
            _stage = stage;
            _elapsed = 0f;
        }

        void SetCardActive(bool value)
        {
            if (tipCard != null) tipCard.gameObject.SetActive(value);
        }

        void SetAlpha(float value)
        {
            if (cardGroup != null) cardGroup.alpha = value;
        }
    }
}
