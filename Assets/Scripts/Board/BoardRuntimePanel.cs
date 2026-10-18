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
    /// 上下深紫占位条 + 右侧操作面板（角色状态卡 / 下一块预告 / 已用与用时 / Play 出发按钮），结构由
    /// <c>Assets/Editor/BoardRuntimePanelBuilder.cs</c> 重建。全部数据来自 Model 的 ObservableValue。
    /// AI 没有方向偏好可调——它出生朝固定方向一路直行，转向全靠铺在格子上的箭头。
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

        [Header("角色状态卡（元素属性 + 可走地形说明）")]
        [Tooltip("「角色状态」区块标题文本；运行时按本地化表刷新")]
        [SerializeField] Text statusTitleText;

        [Tooltip("角色名文本；运行时按本地化表刷新")]
        [SerializeField] Text statusNameText;

        [Tooltip("可走地形行：可走：陆地 / 火 / 冰（由 TileTypeInfo.IsPassable 动态拼出）")]
        [SerializeField] Text statusWalkText;

        [Tooltip("「下一块」区块标题文本")]
        [SerializeField] Text nextTitleText;

        [Tooltip("元素图例的名称标签，顺序固定 陆地 / 火 / 冰（与生成器里三组小图标对齐）")]
        [SerializeField] Text[] elementLegendLabels;

        [Header("棋盘外框")]
        [Tooltip("包裹棋盘的装饰外框；未回填时现场创建一个。位置由 BoardScreenLayout 按视口矩形驱动")]
        [SerializeField] Image boardFrame;

        [Tooltip("外框贴图（1024×1024 的镂空边框，中间透明）")]
        [SerializeField] Sprite boardFrameSprite;

        /// <summary>现场创建外框时用的节点名。</summary>
        const string BoardFrameName = "BoardFrame";

        [Header("运行控制")]
        [SerializeField] Button playButton;
        [SerializeField] Text playButtonLabel;
        [SerializeField] Text aiStateText;
        [Header("现场创建按钮的配色")]
        [SerializeField] Color buttonColor = new Color(0.98f, 0.98f, 1f, 1f);
        [SerializeField] Color buttonTextColor = new Color(0.13f, 0.12f, 0.18f, 1f);

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

        PieceModel _piece;
        BoardModel _board;
        BoardTimeModel _time;
        BoardRunModel _run;

        /// <summary>结算事件是否已经挂过：同一面板实例被反复 Show 时避免重复订阅导致弹出多个结算面板。</summary>
        bool _resultHooked;

        protected override void OnInit()
        {
            GameFonts.Apply(transform);
            StretchToScreen();

            EnsureBoardFrame();

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

            EnsureHintText();

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

        /// <summary>
        /// 棋盘外框：一张中间镂空的边框图，由 BoardScreenLayout 铺满棋盘视口矩形，把整块棋盘围起来。
        /// 两个必须：raycastTarget 关掉（它盖在棋盘上方，参与射线就会把放置与平移全挡掉），
        /// 层级压到最底（右侧操作区与上下白板都在它之上，外框不会盖住它们）。
        /// </summary>
        void EnsureBoardFrame()
        {
            if (boardFrame == null)
            {
                var existing = transform.Find(BoardFrameName);
                if (existing != null) boardFrame = existing.GetComponent<Image>();
            }

            if (boardFrame == null)
            {
                var frameObject = new GameObject(BoardFrameName, typeof(RectTransform));
                var rect = frameObject.transform as RectTransform;
                rect.SetParent(transform, false);
                rect.localScale = Vector3.one;
                boardFrame = frameObject.AddComponent<Image>();
                rect.SetAsFirstSibling();
            }

            boardFrame.raycastTarget = false;
            boardFrame.preserveAspect = true;
            boardFrame.color = Color.white;
            if (boardFrame.sprite == null && boardFrameSprite != null) boardFrame.sprite = boardFrameSprite;
        }

        /// <summary>
        /// 操作提示行挂在**底部白板**里（下半部分那条空白），显示放置 / 旋转 / 缩放 / 平移。
        /// 预制体没回填时在 BottomBar 下现场创建一条；已有节点则只补字体，不覆写它自己的位置。
        /// </summary>
        void EnsureHintText()
        {
            var parent = bottomBar != null ? bottomBar : rightPanel;
            if (parent == null) return;

            const string name = "HintText";
            var existing = parent.Find(name);
            if (hintText == null)
            {
                hintText = existing != null
                    ? existing.GetComponent<Text>()
                    : CreateText(name, parent, 34, buttonTextColor, TextAnchor.MiddleCenter);
            }

            if (hintText == null) return;

            if (existing == null)
            {
                // 现场创建的才铺满底部白板：预制体里配好的位置不在这里动
                var rect = hintText.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(-48f, 0f);
            }

            GameFonts.Apply(hintText.transform);
        }

        void ApplyLocalization()
        {
            if (backButtonLabel != null)
                backButtonLabel.text = Localization.Get(Localization.Key.LevelBackToSelect);
            if (restartButtonLabel != null)
                restartButtonLabel.text = Localization.Get(Localization.Key.LevelRestart);
            if (hintText != null)
                hintText.text = Localization.Get(Localization.Key.LevelBoardHint);
            if (playButtonLabel != null)
                playButtonLabel.text = Localization.Get(Localization.Key.LevelPlay);
            if (nextTitleText != null)
                nextTitleText.text = Localization.Get(Localization.Key.LevelNextPiece);
            if (statusTitleText != null)
                statusTitleText.text = Localization.Get(Localization.Key.LevelStatusCharacter);
            if (statusNameText != null)
                statusNameText.text = Localization.Get(Localization.Key.LevelCharacterName);

            if (statusWalkText != null)
                statusWalkText.text = Localization.Get(Localization.Key.LevelWalkable, WalkableTerrainNames(CurrentElement()));

            if (elementLegendLabels != null && elementLegendLabels.Length >= 3)
            {
                elementLegendLabels[0].text = Localization.Get(Localization.Key.TileNameLand);
                elementLegendLabels[1].text = Localization.Get(Localization.Key.TileNameFire);
                elementLegendLabels[2].text = Localization.Get(Localization.Key.TileNameIce);
            }
        }

        /// <summary>
        /// 角色状态卡的元素三元组：类型与本地化键配对，能否可走以 <see cref="ElementStateInfo.CanWalkOn" /> 为真源，
        /// 属性反转 / 重开时「可走：…」行自动跟随。
        /// </summary>
        static readonly (TileType type, Localization.Key key)[] WalkableElements =
        {
            (TileType.Land, Localization.Key.TileNameLand),
            (TileType.Fire, Localization.Key.TileNameFire),
            (TileType.Ice, Localization.Key.TileNameIce),
        };

        /// <summary>AI 当前元素属性；面板初始化早于 Model 挂接时回落火人。</summary>
        ElementState CurrentElement()
        {
            var run = _run != null ? _run : this.GetModel<BoardRunModel>();
            return run != null ? run.Element.Value : ElementState.Fire;
        }

        /// <summary>拼「可走」列表：陆地恒可走，火格需火属性、冰格需冰属性（06 篇 RB-09 的 HUD 呈现）。</summary>
        string WalkableTerrainNames(ElementState element)
        {
            var names = new List<string>(WalkableElements.Length);
            foreach (var entry in WalkableElements)
            {
                if (!ElementStateInfo.CanWalkOn(element, entry.type)) continue;

                names.Add(Localization.Get(entry.key));
            }

            return string.Join(" / ", names);
        }

        /// <summary>元素属性变化（出生 / 反转 / 重开）：刷新「可走」行与 AI 状态行里的属性标识。</summary>
        void OnElementChanged()
        {
            ApplyLocalization();
            OnRunStateChanged();
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
                layout.RegisterFrame(boardFrame != null ? (RectTransform)boardFrame.transform : null);
            }

            _piece = this.GetModel<PieceModel>();
            _board = this.GetModel<BoardModel>();
            _time = this.GetModel<BoardTimeModel>();
            _run = this.GetModel<BoardRunModel>();

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
            _run.Element.AddListenerAndInvoke(_ => OnElementChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            // 只挂一次：本面板实例在同一关卡内可能被反复 Show，重复订阅会让一次结算弹出多个面板
            if (!_resultHooked)
            {
                _resultHooked = true;
                _run.RunFinished.AddListener(OnRunFinished)
                    .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            }

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

            // 流程是「铺路 → 点 Play 出发」：铺路阶段 Play 即可点（方向偏好已删，无需铺路完成暂停）。
            // 结算后按钮收起（不再变「重来」），重开一律走右上角「重新开始」与结算面板。
            if (playButton != null)
            {
                playButton.gameObject.SetActive(!settled);
                playButton.interactable = phase == BoardRunModel.Phase.Building;
            }

            // Play 标签不随阶段变（出发文案归 ApplyLocalization 管），这里只管可用性

            if (aiStateText == null) return;

            var steps = _run.StepCount.Value;
            var elementText = $"{ElementStateInfo.DisplayName(_run.Element.Value)}属性";
            switch (phase)
            {
                case BoardRunModel.Phase.Building:
                    aiStateText.text = $"AI 待命 · {elementText} · 起点（出发朝向 {MoveDirectionInfo.DisplayName(_run.InitialDirection)}）";
                    break;
                case BoardRunModel.Phase.Running:
                    aiStateText.text = $"AI 行进中 · 第 {steps} 步 · {elementText}";
                    break;
                default:
                    var win = _run.Result.Value == BoardRunModel.RunResult.Win;
                    aiStateText.text = win
                        ? $"抵达终点 · 共 {steps} 步"
                        : $"无路可走 · 第 {steps} 步停下";
                    break;
            }
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

            var text = CreateText("Text", rect, 28, buttonTextColor, TextAnchor.MiddleCenter);
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

    }
}
