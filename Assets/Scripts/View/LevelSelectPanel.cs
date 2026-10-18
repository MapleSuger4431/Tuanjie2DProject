using System.Collections.Generic;
using Game.Core;
using Game.Model;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 选关面板（Panel 形态，根不带 Canvas，挂框架 Normal 层）：大地图 + 关卡节点。
    /// <para>
    /// 左上「返回」回主场景、右上「设置」打开设置面板；点已解锁节点打开 LevelDetailPanel。
    /// 静态结构由 Assets/Editor/LevelSelectUIBuilder 产出（改结构改生成器），
    /// 节点由 <see cref="LevelSelectModel" /> 关卡表驱动运行时生成，
    /// 节点位置由 NodesRoot 下的空对象决定（用户在编辑器里自由增删/拖动，Hierarchy 顺序 = 关卡顺序）。
    /// </para>
    /// <para>
    /// 全部文本经 <see cref="Game.Core.Localization" /> 取值，语言切换时整体刷新。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class LevelSelectPanel : AesirBasePanelViewController<GameContext>
    {
        [Tooltip("节点容器（每个直接子物体 = 一个关卡位，Hierarchy 顺序 = 关卡顺序）")]
        [SerializeField]
        RectTransform nodesRoot;

        [Tooltip("左上返回按钮")]
        [SerializeField]
        Button backButton;

        [Tooltip("右上设置按钮")]
        [SerializeField]
        Button settingsButton;

        [Tooltip("关卡节点模板")]
        [SerializeField]
        GameObject levelNodePrefab;

        [Tooltip("标题文本")]
        [SerializeField]
        TMP_Text titleLabel;

        [Tooltip("返回按钮文本")]
        [SerializeField]
        TMP_Text backButtonLabel;

        [Tooltip("设置按钮文本")]
        [SerializeField]
        TMP_Text settingsButtonLabel;

        LevelSelectModel _model;
        readonly List<LevelNodeView> _nodes = new List<LevelNodeView>();

        protected override void OnInit()
        {
            // 生命周期：克隆 → OnInit → Start —— Model 缓存与按钮绑定必须在 OnInit 完成
            _model = this.GetModel<LevelSelectModel>();

            // 语言已由 SettingsService 在启动时从存档应用到 Unity Localization（取文本前已选中正确语言）

            Localization.OnLanguageChanged += ApplyLocalization;

            backButton.onClick.AddListener(OnBackClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);

            // 先建节点再刷文本：节点文本由 ApplyLocalization 统一写入
            BuildNodes();
            ApplyLocalization();
        }

        protected override void OnDestroy()
        {
            Localization.OnLanguageChanged -= ApplyLocalization;
            base.OnDestroy();
        }

        /// <summary>左上返回：回主场景（主菜单）。</summary>
        void OnBackClicked()
        {
            Debug.Log($"[LevelSelect] 返回主场景：{ScenePaths.MainMenu}");
            SceneTransition.LoadSingle(ScenePaths.MainMenu,
                onFailed: () => Debug.LogError($"[LevelSelect] 返回主场景失败：{ScenePaths.MainMenu}"));
        }

        /// <summary>右上设置：打开设置面板（Popup 层）。</summary>
        void OnSettingsClicked()
        {
            UIModule.Show<MenuSettingsPanel>(UIPaths.MenuSettingsPanel);
        }

        void BuildNodes()
        {
            if (nodesRoot == null)
            {
                Debug.LogError("[LevelSelect] nodesRoot 未回填，请重跑 菜单 Tools/选关/重建选关 UI 预制体");
                return;
            }

            if (levelNodePrefab == null)
            {
                Debug.LogError("[LevelSelect] levelNodePrefab 未回填，请重跑 菜单 Tools/选关/重建选关 UI 预制体");
                return;
            }

            // 节点集合 = NodesRoot 的全部直接子物体（按 Hierarchy 顺序 = 关卡顺序；用户在编辑器里
            // 自由增删空对象/拖动位置，关卡数不固定）。子物体比 Levels 表多时自动出占位关卡
            // （sceneName 空，进入按钮预检只打 Warning）。
            var anchors = new List<RectTransform>();
            foreach (Transform c in nodesRoot)
                anchors.Add((RectTransform)c);
            anchors.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));

            foreach (var a in anchors)
            {
                var levelId = a.GetSiblingIndex() + 1;
                var def = _model.GetDef(levelId) ?? new LevelDef { levelId = levelId, sceneName = "" };
                _nodes.Add(CreateNode(def, a));
            }
        }

        LevelNodeView CreateNode(LevelDef def, RectTransform anchor)
        {
            var nodeGo = Instantiate(levelNodePrefab, anchor);
            nodeGo.name = "Node_" + def.levelId;
            var node = (RectTransform)nodeGo.transform;
            // 锚点模式：节点居中锚定于空对象（位置=空对象位置，用户拖空对象即拖节点）
            node.anchorMin = node.anchorMax = new Vector2(0.5f, 0.5f);
            node.anchoredPosition = Vector2.zero;

            var button = node.GetComponent<Button>();
            // 通用节点形态（2026-10-18）：Badge=框框图（生成器固定 badge_01_no_number，全关卡共用）、
            // Badge/LevNum=框内文本——关卡号直接打字写进去，加关卡零素材成本
            var frameImage = node.Find("Badge")?.GetComponent<Image>();
            var numText = node.Find("Badge/LevNum")?.GetComponent<TMP_Text>();
            if (numText != null) numText.text = def.levelId.ToString();

            var view = new LevelNodeView(frameImage, numText, button, def);

            // 无条件注册点击监听：未解锁由 Refresh 置 interactable=false 挡住点击。
            // 若只在「创建时已解锁」的节点上注册，将来面板存续期间动态解锁时 Refresh 只改外观不补监听，
            // 节点会「看着解锁但点了没反应」（2026-10-18 接口审查发现，已修）。
            if (button != null)
            {
                var captured = def;
                button.onClick.AddListener(() => OpenDetail(captured));
            }

            return view;
        }

        void OpenDetail(LevelDef def)
        {
            // 全项目统一面板形态：详情弹窗也是 Panel，走 UIModule.Show（不用窗口的 Open/Close 接口）。
            // payload 显式 (object) 强转，避免命中只带 path 的重载家族把 payload 当资源路径吞掉
            UIModule.Show<LevelDetailPanel>((object)def, UIPaths.LevelDetailPanel);
        }

        /// <summary>按当前语言刷新全部文本（标题 / 按钮 / 节点状态）。</summary>
        void ApplyLocalization()
        {
            // 2026-10-18 用户定稿：Title 暂时为空（背景图横幅自带「关卡总览」，两行文字会重叠）——
            // 本地化键 LevelSelectTitle 仍保留「选择关卡」，恢复标题时取消下一行注释即可。
            // if (titleLabel != null) titleLabel.text = Localization.Get(Localization.Key.LevelSelectTitle);
            if (backButtonLabel != null) backButtonLabel.text = Localization.Get(Localization.Key.LevelSelectBack);
            if (settingsButtonLabel != null) settingsButtonLabel.text = Localization.Get(Localization.Key.SettingsTitle);

            foreach (var node in _nodes) node.Refresh(_model);
        }

        /// <summary>
        /// 单个关卡节点的视图句柄：节点是「通用框框图 + 框内关卡号文本」，
        /// 解锁态原色、未解锁框与文本整体压灰且不可点；星级/用时记录在详情面板里看。
        /// </summary>
        sealed class LevelNodeView
        {
            static readonly Color UnlockedColor = Color.white;                   // 有专属美术走原色
            static readonly Color LockedColor = new Color(0.40f, 0.40f, 0.44f); // 未解锁：整体压灰

            readonly Image _frame;   // 框框图（所有关卡共用）
            readonly TMP_Text _num;  // 框内关卡号文本
            readonly Button _button;
            readonly LevelDef _def;

            internal LevelNodeView(Image frame, TMP_Text num, Button button, LevelDef def)
            {
                _frame = frame;
                _num = num;
                _button = button;
                _def = def;
            }

            internal void Refresh(LevelSelectModel model)
            {
                var unlocked = model.IsUnlocked(_def.levelId);
                if (_frame != null) _frame.color = unlocked ? UnlockedColor : LockedColor;
                if (_num != null) _num.color = unlocked ? UnlockedColor : LockedColor;
                if (_button != null) _button.interactable = unlocked;
            }
        }
    }
}
