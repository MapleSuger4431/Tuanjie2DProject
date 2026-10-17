using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 屏幕布局真源：用 UI 白板占位上下与右侧，剩下的左侧正方形就是棋盘的独立操作空间。
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

        [SerializeField] float paddingLeft = 100f;
        [SerializeField] float reserveTop = 100f;
        [SerializeField] float reserveBottom = 100f;

        [Tooltip("棋盘可用区右侧至少保留的宽度：参与「最大正方形」计算，实际面板只会更宽")]
        [SerializeField] float reserveRight = 572f;

        [Tooltip("棋盘视口相对可用区最大正方形的每边内缩像素（1920 下 60 → 边长 880 缩到 760）")]
        [SerializeField] float boardInset = 60f;

        [Tooltip("棋盘视口右缘到右侧面板左缘的间距")]
        [SerializeField] float boardToPanelGap = 0f;

        /// <summary>设计分辨率到当前屏幕的等比缩放系数。</summary>
        public float UiScale { get; private set; } = 1f;

        /// <summary>棋盘操作空间（当前屏幕像素，左下原点）：可用矩形内最大的**正方形**，靠左垂直居中。</summary>
        public Rect OperationRect { get; private set; }

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
            float right = reserveRight * UiScale;
            float top = reserveTop * UiScale;

            float availableWidth = Mathf.Max(Screen.width - left - right, 1f);
            float availableHeight = Mathf.Max(Screen.height - bottom - top, 1f);

            // 棋盘是正方形：先取可用区最大正方形，再整体内缩 boardInset，
            // 缩出来的四周不再空着——外框贴着缩后的视口，右侧那条空白直接归 RightPanel。
            float inset = Mathf.Max(boardInset, 0f) * UiScale;
            float side = Mathf.Max(Mathf.Min(availableWidth, availableHeight) - inset * 2f, 1f);
            OperationRect = new Rect(left, bottom + (availableHeight - side) * 0.5f, side, side);

            ApplyTopBar(topBar, reserveTop);
            ApplyBottomBar(bottomBar, reserveBottom);
            ApplyRightPanel(rightPanel, OperationRect.xMax, boardToPanelGap * UiScale, reserveTop, reserveBottom);
            ApplyFrame(boardFrame, OperationRect);
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
        /// 右侧面板不再是固定宽度：左缘贴着棋盘视口右缘（再加一点间距），一路铺到屏幕右边，
        /// 于是棋盘缩小腾出来的那条空白直接归 UI，不会被空着。
        /// 左缘按屏幕像素归一化成 anchor，与棋盘视口共用同一套坐标，缩放屏幕上也不会错位。
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
