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
    /// 节点由 <see cref="LevelSelectModel" /> 关卡表驱动运行时生成。
    /// </para>
    /// <para>
    /// 全部文本经 <see cref="Game.Core.Localization" /> 取值，语言切换时整体刷新。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class LevelSelectPanel : AesirBasePanelViewController<GameContext>
    {
        [Tooltip("节点容器")]
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

            foreach (var def in _model.Levels)
            {
                _nodes.Add(CreateNode(def));
            }
        }

        LevelNodeView CreateNode(LevelDef def)
        {
            var nodeGo = Instantiate(levelNodePrefab, nodesRoot);
            nodeGo.name = "Node_" + def.levelId;
            var node = (RectTransform)nodeGo.transform;
            // 锚点定位：anchor 即归一化位置——不依赖布局更新时序
            node.anchorMin = node.anchorMax = def.nodePos;
            node.anchoredPosition = Vector2.zero;

            var button = node.GetComponent<Button>();
            var image = node.GetComponent<Image>();
            var view = new LevelNodeView(node, image, button,
                node.Find("Name")?.GetComponent<TMP_Text>(),
                node.Find("Stars")?.GetComponent<TMP_Text>(), def);

            if (button != null && _model.IsUnlocked(def.levelId))
            {
                var captured = def;
                button.onClick.AddListener(() => OpenDetail(captured));
            }

            _ = nodeGo;   // 节点视觉后续直接改 LevelNode.prefab
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
            if (titleLabel != null) titleLabel.text = Localization.Get(Localization.Key.LevelSelectTitle);
            if (backButtonLabel != null) backButtonLabel.text = Localization.Get(Localization.Key.LevelSelectBack);
            if (settingsButtonLabel != null) settingsButtonLabel.text = Localization.Get(Localization.Key.SettingsTitle);

            foreach (var node in _nodes) node.Refresh(_model);
        }

        /// <summary>单个关卡节点的视图句柄：语言切换时按解锁/记录状态重刷文本与配色。</summary>
        sealed class LevelNodeView
        {
            static readonly Color UnlockedColor = new Color(0.24f, 0.46f, 0.62f);
            static readonly Color LockedColor = new Color(0.30f, 0.30f, 0.33f);

            readonly Image _image;
            readonly Button _button;
            readonly TMP_Text _name;
            readonly TMP_Text _stars;
            readonly LevelDef _def;

            internal LevelNodeView(RectTransform root, Image image, Button button, TMP_Text name, TMP_Text stars, LevelDef def)
            {
                _image = image;
                _button = button;
                _name = name;
                _stars = stars;
                _def = def;
            }

            internal void Refresh(LevelSelectModel model)
            {
                var unlocked = model.IsUnlocked(_def.levelId);
                if (_image != null) _image.color = unlocked ? UnlockedColor : LockedColor;
                if (_button != null) _button.interactable = unlocked;
                if (_name != null) _name.text = Localization.Get(Localization.Key.LevelName, _def.levelId);

                if (_stars == null) return;
                if (!unlocked) _stars.text = Localization.Get(Localization.Key.LevelLocked);
                else if (!model.HasRecord(_def.levelId)) _stars.text = Localization.Get(Localization.Key.LevelNoStar);
                else _stars.text = new string('★', model.GetBestStars(_def.levelId)).PadRight(3, '☆');
            }
        }
    }
}
