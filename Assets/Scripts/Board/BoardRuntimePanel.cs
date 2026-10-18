using System.Collections.Generic;
using Game.Core;
using Game.Service;
using UnityEngine;
using UnityEngine.UI;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;

namespace Game.Board
{
    /// <summary>
    /// 棋盘运行时常驻面板（挂 UIRoot 的层画布下，自身不带 Canvas）：
    /// 上下白板占位 + 右侧操作面板，面板上方预告下一块。全部数据来自 Model 的 ObservableValue。
    /// 右侧中下部是运行控制区：用时 / 状态 / Play 按钮 / 方向优先级排序列表。
    /// 优先级列表在铺路阶段可自由调序（↑↓ 或点选置顶），点 Play 后锁定为只读展示。
    /// </summary>
    public sealed class BoardRuntimePanel : AesirBasePanelViewController<BoardContext>
    {
        [SerializeField] RectTransform topBar;
        [SerializeField] RectTransform bottomBar;
        [SerializeField] RectTransform rightPanel;
        [SerializeField] NextPieceView nextPieceView;
        [SerializeField] Text placedCountText;
        [SerializeField] Text timeText;
        [SerializeField] Text hintText;

        [Header("运行控制")]
        [Tooltip("铺路完成按钮；未回填时自动在 Play 按钮左侧创建（两者并排等分原 Play 按钮的宽度）")]
        [SerializeField] Button finishBuildingButton;
        [SerializeField] Text finishBuildingButtonLabel;
        [SerializeField] Button playButton;
        [SerializeField] Text playButtonLabel;
        [SerializeField] Text aiStateText;
        [SerializeField] RectTransform priorityRoot;

        [Header("优先级行配色")]
        [SerializeField] Color rowColor = new Color(0.88f, 0.88f, 0.93f, 1f);
        [SerializeField] Color rowLockedColor = new Color(0.80f, 0.80f, 0.86f, 1f);
        [SerializeField] Color rowTextColor = new Color(0.13f, 0.12f, 0.18f, 1f);
        [SerializeField] Color buttonColor = new Color(0.98f, 0.98f, 1f, 1f);

        [Tooltip("铺路完成按钮底色（现场创建时才用；与绿色 Play 区分，避免看不出是按钮）")]
        [SerializeField] Color finishButtonColor = new Color(0.98f, 0.83f, 0.42f, 1f);

        [Header("关卡出口")]
        [Tooltip("返回选关按钮；未回填时自动在右上角创建")]
        [SerializeField] Button backButton;
        [SerializeField] Text backButtonLabel;

        [Tooltip("重新开始本关按钮；未回填时自动在返回选关按钮左侧创建")]
        [SerializeField] Button restartButton;
        [SerializeField] Text restartButtonLabel;

        /// <summary>右上角两个按钮的尺寸与间距（角锚定，不随父级拉伸）。</summary>
        const float TopBarButtonHeight = 72f;
        const float TopBarButtonGap = 12f;
        const float BackButtonWidth = 220f;
        const float RestartButtonWidth = 200f;

        /// <summary>运行控制区两个按钮并排：等分原 Play 按钮的 400 宽，整体仍居中于右侧面板。</summary>
        const float RunButtonWidth = 194f;
        const float RunButtonHeight = 80f;
        const float RunButtonGap = 12f;

        PieceModel _piece;
        BoardModel _board;
        BoardTimeModel _time;
        BoardRunModel _run;
        AiPreferenceModel _preference;

        /// <summary>结算事件是否已经挂过：同一面板实例被反复 Show 时避免重复订阅导致弹出多个结算面板。</summary>
        bool _resultHooked;

        /// <summary>优先级列表的四行，创建一次后反复复用（避免重建堆积）。</summary>
        readonly List<PriorityRow> _rows = new List<PriorityRow>();

        protected override void OnInit()
        {
            GameFonts.Apply(transform);
            StretchToScreen();

            EnsureBackButton();
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackToLevelSelectClicked);
            }

            EnsureRestartButton();
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            EnsureFinishBuildingButton();
            if (finishBuildingButton != null)
            {
                finishBuildingButton.onClick.RemoveAllListeners();
                finishBuildingButton.onClick.AddListener(OnFinishBuildingClicked);
            }

            Localization.OnLanguageChanged += ApplyLocalization;
            ApplyLocalization();
        }

        protected override void OnDestroy()
        {
            Localization.OnLanguageChanged -= ApplyLocalization;
            base.OnDestroy();
        }

        /// <summary>关卡出口：回选关场景（关卡结算面板落地后可在此前插入确认弹窗）。</summary>
        void OnBackToLevelSelectClicked()
        {
            SceneTransition.LoadSingle(ScenePaths.LevelSelect,
                onFailed: () => Debug.LogError($"[BoardRuntime] 返回选关失败：{ScenePaths.LevelSelect}"));
        }

        /// <summary>返回按钮缺失时按右侧面板底部条带现场创建（预制体没回填也能用）。</summary>
        void EnsureBackButton()
        {
            // 挂在屏幕右上角：TopBar 白板的右端（它整条横跨屏幕，右端即右上角）。
            // 不放右侧面板顶部——那里是「下一块」预览区，会压住。TopBar 缺失时退回右侧面板。
            var parent = topBar != null ? topBar : rightPanel;
            if (parent == null) return;

            const string name = "BackToLevelSelect";
            var existing = parent.Find(name);
            var positionedByCaller = existing != null;
            if (backButton == null)
            {
                backButton = existing != null
                    ? existing.GetComponent<Button>()
                    : CreateButton(name, parent, string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f));
                positionedByCaller = false;
            }

            if (backButton == null) return;

            if (!positionedByCaller)
            {
                // 角锚定 + 固定尺寸：贴右上角留边，不随父级拉伸（TopBar 高 100，按钮 72 放得下）
                var rect = (RectTransform)backButton.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(BackButtonWidth, TopBarButtonHeight);
                rect.anchoredPosition = new Vector2(-16f, -14f);
            }

            if (backButtonLabel == null) backButtonLabel = backButton.GetComponentInChildren<Text>();
            GameFonts.Apply(backButton.transform);
        }

        /// <summary>重开本关：清空已铺方块、还原地形、重抽方块、AI 回起点、计时归零（不重载场景）。</summary>
        void OnRestartClicked()
        {
            if (_run == null) return;

            _run.RestartLevel();
        }

        /// <summary>
        /// 重新开始按钮缺失时现场创建，排在返回选关按钮左侧（右上角同一条）。
        /// 必须在 <see cref="EnsureBackButton" /> 之后调用——要读返回按钮的宽度算偏移。
        /// </summary>
        void EnsureRestartButton()
        {
            var parent = topBar != null ? topBar : rightPanel;
            if (parent == null) return;

            const string name = "RestartLevel";
            var existing = parent.Find(name);
            var positionedByCaller = existing != null;
            if (restartButton == null)
            {
                restartButton = existing != null
                    ? existing.GetComponent<Button>()
                    : CreateButton(name, parent, string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f));
                positionedByCaller = false;
            }

            if (restartButton == null) return;

            if (!positionedByCaller)
            {
                // 与返回按钮同款角锚定：右边缘往左让开「返回按钮宽 + 12px 间距」，再往左展开自身宽度
                var rect = (RectTransform)restartButton.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(RestartButtonWidth, TopBarButtonHeight);

                var backWidth = backButton != null ? ((RectTransform)backButton.transform).sizeDelta.x : BackButtonWidth;
                rect.anchoredPosition = new Vector2(-(16f + backWidth + TopBarButtonGap), -14f);
            }

            if (restartButtonLabel == null) restartButtonLabel = restartButton.GetComponentInChildren<Text>();
            GameFonts.Apply(restartButton.transform);
        }

        /// <summary>铺路完成：停表并锁定放置，把方向优先级的调整权交给玩家（这段时间不计用时）。</summary>
        void OnFinishBuildingClicked() => _run?.CompleteBuilding();

        /// <summary>
        /// 铺路完成按钮缺失时现场创建：紧挨 Play 按钮左侧并排，两者等分原 Play 按钮占的那段宽度、整体仍居中。
        /// 必须在 playButton 已解析之后调用——要读它的父级与纵向位置，并把它让到右半边。
        /// </summary>
        void EnsureFinishBuildingButton()
        {
            if (playButton == null) return;

            var parent = playButton.transform.parent;
            if (parent == null) return;

            const string name = "FinishBuilding";
            var existing = parent.Find(name);
            var positionedByCaller = existing != null;
            if (finishBuildingButton == null)
            {
                finishBuildingButton = existing != null
                    ? existing.GetComponent<Button>()
                    : CreateButton(name, (RectTransform)parent, string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                positionedByCaller = false;
            }

            if (finishBuildingButton == null) return;

            if (!positionedByCaller)
            {
                var playRect = (RectTransform)playButton.transform;
                var y = playRect.anchoredPosition.y;
                var offset = (RunButtonWidth + RunButtonGap) * 0.5f;

                var rect = (RectTransform)finishBuildingButton.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(RunButtonWidth, RunButtonHeight);
                rect.anchoredPosition = new Vector2(-offset, y);

                // Play 让到右半边，与铺路完成同宽同高
                playRect.sizeDelta = new Vector2(RunButtonWidth, RunButtonHeight);
                playRect.anchoredPosition = new Vector2(offset, y);

                var image = finishBuildingButton.GetComponent<Image>();
                if (image != null) image.color = finishButtonColor;
            }

            if (finishBuildingButtonLabel == null)
                finishBuildingButtonLabel = finishBuildingButton.GetComponentInChildren<Text>();
            GameFonts.Apply(finishBuildingButton.transform);
        }

        void ApplyLocalization()
        {
            if (backButtonLabel != null)
                backButtonLabel.text = Localization.Get(Localization.Key.LevelBackToSelect);
            if (restartButtonLabel != null)
                restartButtonLabel.text = Localization.Get(Localization.Key.LevelRestart);
            if (finishBuildingButtonLabel != null)
                finishBuildingButtonLabel.text = Localization.Get(Localization.Key.LevelFinishBuilding);
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);

            // 布局真源仍在 BoardScreenLayout：把 UI 根画布与三块占位白板交给它驱动
            var layout = FindObjectOfType<BoardScreenLayout>();
            if (layout != null)
            {
                layout.RegisterCanvas(GetComponentInParent<Canvas>());
                layout.RegisterBars(topBar, bottomBar, rightPanel);
            }

            _piece = this.GetModel<PieceModel>();
            _board = this.GetModel<BoardModel>();
            _time = this.GetModel<BoardTimeModel>();
            _run = this.GetModel<BoardRunModel>();
            _preference = this.GetModel<AiPreferenceModel>();

            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayClicked);
            }

            _piece.Next.AddListenerAndInvoke(OnNextChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _board.CoveredCount.AddListenerAndInvoke(OnCoveredCountChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _time.Elapsed.AddListenerAndInvoke(_ => OnTimeChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _time.State.AddListenerAndInvoke(_ => OnTimeChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _time.Paused.AddListenerAndInvoke(_ => OnTimeChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);

            _run.CurrentPhase.AddListenerAndInvoke(_ => OnRunStateChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _run.Result.AddListenerAndInvoke(_ => OnRunStateChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _run.StepCount.AddListenerAndInvoke(_ => OnRunStateChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            // 只挂一次：本面板实例在同一关卡内可能被反复 Show，重复订阅会让一次结算弹出多个面板
            if (!_resultHooked)
            {
                _resultHooked = true;
                _run.RunFinished.AddListener(OnRunFinished)
                    .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            }

            _preference.OrderChanged.AddListener(RebuildPriorityRows)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _preference.Locked.AddListenerAndInvoke(_ => RebuildPriorityRows())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);

            if (hintText != null)
                hintText.text = "左键放置当前方块　滚轮缩放棋盘　右键平移";
        }

        void OnPlayClicked()
        {
            // 结算后不再复用本按钮做「重来」：重开入口统一走右上角「重新开始」与结算面板
            _run?.Play();
        }

        /// <summary>离开结算态（重来 / 重新开始）时收起已打开的结算面板，避免压在铺路阶段上。</summary>
        void HideResultPanels()
        {
            var clear = UIModule.Get<LevelClearPanel>();
            if (clear != null && clear.IsOpen) UIModule.Hide<LevelClearPanel>();

            var fail = UIModule.Get<LevelFailPanel>();
            if (fail != null && fail.IsOpen) UIModule.Hide<LevelFailPanel>();
        }

        /// <summary>
        /// 胜负已定：按结果拉起对应的结算面板（通关 → LevelClearPanel，失败 → LevelFailPanel）。
        /// 两者共用 LevelResultPanel 的「重玩此关 / 回到选关」出口，差异只在标题与成绩区。
        /// </summary>
        void OnRunFinished()
        {
            if (_run == null) return;

            if (_run.Result.Value == BoardRunModel.RunResult.Win)
            {
                // 通关即写记录（最短用时在此刷新并落存档），破纪录与否传给结算面板展示
                var isNewRecord = RecordClearForCurrentLevel();
                UIModule.Show<LevelClearPanel>((object)isNewRecord, UIPaths.LevelClearPanel);
                return;
            }

            UIModule.Show<LevelFailPanel>(path: UIPaths.LevelFailPanel);
        }

        /// <summary>
        /// 把本次通关写入 GameContext 侧的通关记录（BoardContext 里没有那些 Model，走服务桥接）。
        /// 用时取秒表最终值——Finish 已 StopTiming，读到的就是冻结成绩。
        /// </summary>
        bool RecordClearForCurrentLevel()
        {
            if (_time == null) return false;

            var service = GameContext.Instance.GetService<LevelProgressService>();
            return service.RecordClearForCurrentScene(_time.Elapsed.Value, _run.StepCount.Value);
        }

        void OnNextChanged(TilePiece piece)
        {
            if (nextPieceView != null) nextPieceView.Render(piece);
        }

        void OnCoveredCountChanged(int count)
        {
            if (placedCountText == null) return;

            placedCountText.text = _board != null ? $"已用：{count}/{_board.PieceLimit}" : $"已用：{count}";
        }

        /// <summary>秒表一行文本：未开始 / 计时中 / 已暂停 / 已结束共用，用时越短越好。</summary>
        void OnTimeChanged()
        {
            if (timeText == null || _time == null) return;

            string suffix;
            if (_time.State.Value == BoardTimeModel.StopwatchState.Running)
                suffix = _time.IsPaused ? "（已暂停）" : string.Empty;
            else if (_time.State.Value == BoardTimeModel.StopwatchState.Finished) suffix = "（已结束）";
            else suffix = "（未开始）";

            timeText.text = $"用时：{_time.FormatElapsed()}{suffix}";
        }

        /// <summary>阶段 / 结果 / 步数任一变化：刷新 Play 按钮与 AI 状态文本。</summary>
        void OnRunStateChanged()
        {
            if (_run == null) return;

            var phase = _run.CurrentPhase.Value;
            var settled = phase == BoardRunModel.Phase.Settled;

            // 重来 / 重新开始会回到铺路阶段：此时必须收起结算面板，否则它压在棋盘上挡住放置
            if (!settled) HideResultPanels();

            // 流程是「铺路完成 → 选偏好 → Play」：铺路阶段只有前者可点，Play 要等铺路完成才解锁。
            // 结算后两个按钮一起收起（不再变「重来」），重开一律走右上角「重新开始」与结算面板。
            if (finishBuildingButton != null)
            {
                finishBuildingButton.gameObject.SetActive(!settled);
                finishBuildingButton.interactable = phase == BoardRunModel.Phase.Building;
            }

            if (playButton != null)
            {
                playButton.gameObject.SetActive(!settled);
                playButton.interactable = phase == BoardRunModel.Phase.Ready;
            }

            if (playButtonLabel != null) playButtonLabel.text = "Play";

            if (aiStateText == null) return;

            var steps = _run.StepCount.Value;
            switch (phase)
            {
                case BoardRunModel.Phase.Building:
                    aiStateText.text = "AI 待命 · 起点";
                    break;
                case BoardRunModel.Phase.Ready:
                    aiStateText.text = "已铺路完成 · 计时暂停，调好方向后点 Play";
                    break;
                case BoardRunModel.Phase.Running:
                    aiStateText.text = $"AI 行进中 · 第 {steps} 步";
                    break;
                default:
                    var win = _run.Result.Value == BoardRunModel.RunResult.Win;
                    aiStateText.text = win
                        ? $"抵达终点 · 共 {steps} 步"
                        : $"无路可走 · 第 {steps} 步停下";
                    break;
            }
        }

        /// <summary>
        /// 按偏好顺序刷新四行排序列表；锁定时隐藏调序按钮。
        /// 行是**复用**的：MonoBehaviour.Destroy 延迟到帧末生效，若每次重建都先 Destroy 再新建，
        /// 同一帧内多次触发（顺序变更 + 锁定变更）会让旧行尚未消失、新行已加入，行数成倍堆积。
        /// </summary>
        void RebuildPriorityRows()
        {
            if (priorityRoot == null || _preference == null) return;

            EnsureRows();

            var count = _preference.Count;
            var locked = _preference.Locked.Value;

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var visible = i < count;
                row.root.gameObject.SetActive(visible);
                if (!visible) continue;

                var index = i;
                row.label.text = $"{i + 1}. {MoveDirectionInfo.DisplayName(_preference.At(i))}";
                row.background.color = locked ? rowLockedColor : rowColor;
                row.upButton.gameObject.SetActive(!locked && i > 0);
                row.downButton.gameObject.SetActive(!locked && i < count - 1);

                row.upButton.onClick.RemoveAllListeners();
                row.upButton.onClick.AddListener(() => _preference.MoveUp(index));
                row.downButton.onClick.RemoveAllListeners();
                row.downButton.onClick.AddListener(() => _preference.MoveDown(index));
            }

            GameFonts.Apply(priorityRoot);
        }

        void EnsureRows()
        {
            var total = MoveDirectionInfo.Count;

            while (_rows.Count < total)
                _rows.Add(CreateRow(_rows.Count, total));

            for (var i = _rows.Count - 1; i >= total; i--)
            {
                Destroy(_rows[i].root.gameObject);
                _rows.RemoveAt(i);
            }
        }

        PriorityRow CreateRow(int index, int total)
        {
            var rowObject = new GameObject($"Row_{index}", typeof(RectTransform));
            var rowRect = rowObject.transform as RectTransform;
            rowRect.SetParent(priorityRoot, false);
            rowRect.localScale = Vector3.one;

            // 行按序号在容器里均分高度：第 index 行占 [1-(index+1)/total, 1-index/total]
            float top = 1f - index / (float)total;
            float bottom = 1f - (index + 1f) / total;
            rowRect.anchorMin = new Vector2(0f, bottom);
            rowRect.anchorMax = new Vector2(1f, top);
            rowRect.offsetMin = new Vector2(0f, 4f);
            rowRect.offsetMax = new Vector2(0f, -4f);

            var background = rowObject.AddComponent<Image>();
            background.color = rowColor;

            var label = CreateText("Label", rowRect, 30, rowTextColor, TextAnchor.MiddleLeft);
            SetAnchors(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.52f, 1f),
                new Vector2(20f, 0f), new Vector2(0f, 0f));

            var upButton = CreateButton("Up", rowRect, "↑", new Vector2(0.56f, 0.12f), new Vector2(0.77f, 0.88f));
            var downButton = CreateButton("Down", rowRect, "↓", new Vector2(0.80f, 0.12f), new Vector2(0.99f, 0.88f));

            return new PriorityRow(rowRect, background, label, upButton, downButton);
        }

        Button CreateButton(string name, RectTransform parent, string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = buttonObject.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            SetAnchors(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

            var image = buttonObject.GetComponent<Image>();
            image.color = buttonColor;

            var text = CreateText("Text", rect, 28, rowTextColor, TextAnchor.MiddleCenter);
            SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;

            return buttonObject.GetComponent<Button>();
        }

        static Text CreateText(string name, RectTransform parent, int fontSize, Color color, TextAnchor anchor)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            var rect = textObject.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            var text = textObject.AddComponent<Text>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>面板本体铺满整屏，三块占位白板各自锚定自己的区域。</summary>
        void StretchToScreen()
        {
            var rect = transform as RectTransform;
            if (rect == null) return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        readonly struct PriorityRow
        {
            public readonly RectTransform root;
            public readonly Image background;
            public readonly Text label;
            public readonly Button upButton;
            public readonly Button downButton;

            public PriorityRow(RectTransform root, Image background, Text label, Button upButton, Button downButton)
            {
                this.root = root;
                this.background = background;
                this.label = label;
                this.upButton = upButton;
                this.downButton = downButton;
            }
        }
    }
}
