using System.Collections;
using Game.Core;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 加载过场面板（Top 层、全屏、拦截全部输入）：主菜单 → 选关切换时展示的过场动画。
    /// <para>
    /// 由 <see cref="Begin" /> 发起：先盖住当前画面，再走 <see cref="SceneTransition.LoadSingle" /> 加载目标场景；
    /// 本面板实现 <see cref="IPersistThroughSceneLoad" />，收面板时被豁免、跨场景存活，
    /// 待「场景加载完成 + 最短展示时长」后整卡淡出并自毁（destroyOnHide）。
    /// </para>
    /// <para>
    /// 动画：旋转宝石环 = 18 帧序列图按帧率换帧（Ring_01..18）；四角星自转；英文条透明度呼吸。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class LoadingPanel : AesirBasePanelViewController<GameContext>, IPersistThroughSceneLoad
    {
        [Tooltip("整卡淡出用 CanvasGroup（挂在根上）")]
        [SerializeField]
        CanvasGroup canvasGroup;

        [Tooltip("全屏背景图（必须拦截射线——加载期间挡住一切点击）")]
        [SerializeField]
        Image backgroundImage;

        [Tooltip("旋转宝石环 Image（按帧序列换图）")]
        [SerializeField]
        Image ringImage;

        [Tooltip("宝石环帧序列（Ring_01..Ring_18 顺序接线）")]
        [SerializeField]
        Sprite[] ringFrames = new Sprite[0];

        [Tooltip("宝石环播放帧率（张/秒）")]
        [SerializeField, Range(4f, 30f)]
        float ringFrameRate = 16f;

        [Tooltip("四角星装饰（持续自转）")]
        [SerializeField]
        RectTransform starRect;

        [Tooltip("英文文字条（透明度呼吸）")]
        [SerializeField]
        Image textEnImage;

        [Tooltip("最短展示时长（秒）——过场至少亮这么久再淡出，避免一闪而过")]
        [SerializeField, Range(0f, 3f)]
        float minDurationSeconds = 1.2f;

        [Tooltip("淡出时长（秒）")]
        [SerializeField, Range(0.1f, 1f)]
        float fadeSeconds = 0.35f;

        string targetScenePath;
        bool sceneLoadDone;
        float elapsed;
        float ringTimer;
        int frameIndex;

        /// <summary>发起一次带加载过场的场景切换：当前画面立刻被加载面板盖住，随后开始加载目标场景。</summary>
        /// <param name="scenePath">目标场景完整资产路径（ScenePaths 常量）。</param>
        public static void Begin(string scenePath)
        {
            UIModule.Show<LoadingPanel>((object)scenePath, UIPaths.LoadingPanel);
        }

        protected override void OnInit()
        {
            // 静态 Text 的默认字体对中文字形不可靠，运行时统一挂系统中文动态字体
            GameFonts.Apply(transform);
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);
            targetScenePath = payload as string;
            sceneLoadDone = false;
            elapsed = 0f;
            ringTimer = 0f;
            frameIndex = 0;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (ringImage != null && ringFrames != null && ringFrames.Length > 0 && ringFrames[0] != null)
            {
                ringImage.sprite = ringFrames[0];
            }

            StartCoroutine(RunSequence());
        }

        /// <summary>
        /// 过场时序：等一帧让加载画面先渲染 → 走 SceneTransition 加载（收面板时本面板被豁免）→
        /// 「最短展示时长 + 加载完成」双条件满足 → 再等一帧让目标场景首个面板亮出来 → 淡出 → 自毁。
        /// </summary>
        IEnumerator RunSequence()
        {
            yield return null;

            SceneTransition.LoadSingle(targetScenePath,
                onCompleted: () => sceneLoadDone = true,
                onFailed: () =>
                {
                    Debug.LogError($"[LoadingPanel] 场景切换失败：{targetScenePath}，撤下加载画面留在当前场景");
                    HideSelf();
                });

            while (elapsed < minDurationSeconds || !sceneLoadDone)
            {
                yield return null;
            }

            // 目标场景的引导脚本（Start 里 Show 首个面板）在 sceneLoaded 之后执行，多等一帧再淡出
            yield return null;

            var fade = 0f;
            while (fade < fadeSeconds)
            {
                fade += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, fade / fadeSeconds);
                }

                yield return null;
            }

            HideSelf();
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            elapsed += dt;

            // 旋转宝石环：按帧率换帧（unscaled——即使未来挂暂停菜单也不冻住过场）
            if (ringImage != null && ringFrames != null && ringFrames.Length > 0)
            {
                ringTimer += dt;
                var interval = 1f / Mathf.Max(1f, ringFrameRate);
                while (ringTimer >= interval)
                {
                    ringTimer -= interval;
                    frameIndex = (frameIndex + 1) % ringFrames.Length;
                }

                var frame = ringFrames[frameIndex];
                if (frame != null && ringImage.sprite != frame)
                {
                    ringImage.sprite = frame;
                }
            }

            // 四角星自转
            if (starRect != null)
            {
                starRect.Rotate(0f, 0f, -80f * dt);
            }

            // 英文条呼吸（中文条保持常亮）
            if (textEnImage != null)
            {
                var alpha = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(elapsed * 3.2f));
                var color = textEnImage.color;
                color.a = alpha;
                textEnImage.color = color;
            }
        }
    }
}
