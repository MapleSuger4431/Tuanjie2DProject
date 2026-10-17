using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 屏幕布局真源：右侧面板按屏宽比例占位，左边剩下的整块（外框 + 棋盘）恒为 1 - rightPanelRatio。
    /// 外框贴着左侧可用区的最大正方形铺满，棋盘视口取外框的透明内区，两者同心——棋盘落在框正中。
    /// 所有尺寸以 designResolution（1920×1080）为单位书写，按 UiScale 等比缩放到当前屏幕。
    /// UI 已接入 Aesir UIRoot：画布与三块白板由 BoardRuntimePanel 显示时注册进来
    /// （场景序列化引用仅作回退），缩放系数直接取 UI 根画布的 scaleFactor，保证 UI 与棋盘视口同一真源。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class BoardScreenLayout : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform topBar;
        [SerializeField] RectTransform bottomBar;
        [SerializeField] RectTransform rightPanel;

        [Tooltip("包裹棋盘的装饰外框；RectTransform 完全由本布局按视口矩形驱动")]
        [SerializeField] RectTransform boardFrame;

        [Tooltip("所有留白与占位尺寸的基准分辨率")]
        [SerializeField] Vector2 designResolution = new Vector2(1920f, 1080f);

        [SerializeField] float paddingLeft = 40f;
        [SerializeField] float reserveTop = 56f;
        [SerializeField] float reserveBottom = 48f;

        [Tooltip("右侧操作面板占用的屏宽比例；左侧剩下的整块（外框 + 棋盘）就是 1 - 该比例")]
        [SerializeField] float rightPanelRatio = 0.4f;

        [Tooltip("外框相对可用区最大正方形的每边内缩像素")]
        [SerializeField] float boardInset = 0f;

        [Tooltip("外框贴图的透明内区占整幅图的比例：1024 的图内框 908 → 0.8867。" +
                 "棋盘视口按它从外框内缩，于是棋盘正好落在外框正中，装饰带围在外圈而不是压住最外圈格子")]
        [SerializeField] float frameInnerRatio = 0.8867f;

        [Tooltip("棋盘视口右缘到右侧面板左缘的间距")]
        [SerializeField] float boardToPanelGap = 0f;

        /// <summary>设计分辨率到当前屏幕的等比缩放系数。</summary>
        public float UiScale { get; private set; } = 1f;

        /// <summary>棋盘操作空间（当前屏幕像素，左下原点）：外框内的透明区域，与外框同心。</summary>
        public Rect OperationRect { get; private set; }

        /// <summary>外框矩形（当前屏幕像素，左下原点）：可用区内最大的正方形，把棋盘整块围在正中。</summary>
        public Rect FrameRect { get; private set; }

        void LateUpdate() => Recompute();

        /// <summary>由 BoardRuntimePanel 在显示时注册 UI 根画布（UIRoot 的层画布）。</summary>
        public void RegisterCanvas(Canvas uiCanvas) => canvas = uiCanvas;

        /// <summary>由 BoardRuntimePanel 在显示时注册三块占位白板。</summary>
        public void RegisterBars(RectTransform top, RectTransform bottom, RectTransform right)
        {
            topBar = top;
            bottomBar = bottom;
            rightPanel = right;
        }

        /// <summary>由 BoardRuntimePanel 在显示时注册棋盘外框（位置由本布局驱动）。</summary>
        public void RegisterFrame(RectTransform frame) => boardFrame = frame;

        /// <summary>按当前屏幕尺寸重算缩放系数、占位白板尺寸与操作空间矩形。</summary>
        public void Recompute()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;

            // 缩放系数一律跟 UI 根画布走（CanvasScaler 已算好），棋盘视口才不会与 UI 白板错位
            UiScale = canvas != null
                ? canvas.scaleFactor
                : Mathf.Min(Screen.width / designResolution.x, Screen.height / designResolution.y);

            float left = paddingLeft * UiScale;
            float bottom = reserveBottom * UiScale;
            float top = reserveTop * UiScale;

            // 右侧面板按屏宽比例占位：左边剩下的整块（外框 + 棋盘）恒为 1 - rightPanelRatio
            float boardAreaRight = Mathf.Clamp(
                Screen.width * (1f - Mathf.Clamp01(rightPanelRatio)), left + 1f, Screen.width);

            float areaWidth = Mathf.Max(boardAreaRight - left, 1f);
            float areaHeight = Mathf.Max(Screen.height - bottom - top, 1f);
            var areaCenter = new Vector2(left + areaWidth * 0.5f, bottom + areaHeight * 0.5f);

            // 外框：可用区内最大的正方形，居中在左侧区域里
            float inset = Mathf.Max(boardInset, 0f) * UiScale;
            float frameSide = Mathf.Max(Mathf.Min(areaWidth, areaHeight) - inset * 2f, 1f);
            FrameRect = new Rect(
                areaCenter.x - frameSide * 0.5f, areaCenter.y - frameSide * 0.5f, frameSide, frameSide);

            // 棋盘视口 = 外框的透明内区，与外框同心：棋盘落在框正中，装饰带围在最外圈
            float side = Mathf.Max(frameSide * Mathf.Clamp01(frameInnerRatio), 1f);
            OperationRect = new Rect(areaCenter.x - side * 0.5f, areaCenter.y - side * 0.5f, side, side);

            ApplyTopBar(topBar, reserveTop);
            ApplyBottomBar(bottomBar, reserveBottom);
            ApplyRightPanel(rightPanel, boardAreaRight, boardToPanelGap * UiScale, reserveTop, reserveBottom);
            ApplyFrame(boardFrame, FrameRect);
        }

        static void ApplyTopBar(RectTransform bar, float height)
        {
            if (bar == null) return;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, height);
        }

        static void ApplyBottomBar(RectTransform bar, float height)
        {
            if (bar == null) return;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, height);
        }

        /// <summary>
        /// 右侧面板左缘按 rightPanelRatio 归一化成 anchor，与外框共用同一套屏幕像素坐标，
        /// 缩放屏幕上也不会错位；左边剩下的整块就是外框 + 棋盘。
        /// </summary>
        static void ApplyRightPanel(RectTransform panel, float boardRightPixel, float gapPixel, float top, float bottom)
        {
            if (panel == null) return;

            float left = Mathf.Clamp01((boardRightPixel + Mathf.Max(gapPixel, 0f)) / Mathf.Max(Screen.width, 1f));
            panel.anchorMin = new Vector2(left, 0f);
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(0f, bottom);
            panel.offsetMax = new Vector2(0f, -top);
        }

        /// <summary>
        /// 棋盘外框：精确铺满视口矩形。anchor 由屏幕像素归一化而来，
        /// 与 BoardCameraFitter 的 pixelRect 同一坐标系，外框恰好把整块棋盘围住。
        /// </summary>
        static void ApplyFrame(RectTransform frame, Rect rect)
        {
            if (frame == null) return;

            float w = Mathf.Max(Screen.width, 1f);
            float h = Mathf.Max(Screen.height, 1f);
            frame.anchorMin = new Vector2(rect.xMin / w, rect.yMin / h);
            frame.anchorMax = new Vector2(rect.xMax / w, rect.yMax / h);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;
        }
    }
}
