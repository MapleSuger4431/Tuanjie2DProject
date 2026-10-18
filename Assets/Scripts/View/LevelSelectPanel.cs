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

        [Tooltip("节点水晶图（索引 = 关卡号-1，由生成器路径表回填）")]
        [SerializeField]
        Sprite[] crystalSprites;

        [Tooltip("节点编号徽章（索引 = 关卡号-1，由生成器路径表回填）")]
        [SerializeField]
        Sprite[] badgeSprites;

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
            var crystalImage = node.Find("Crystal")?.GetComponent<Image>();
            var badgeImage = node.Find("Badge")?.GetComponent<Image>();
            ApplyNodeIcon(crystalImage, crystalSprites, def.levelId);
            ApplyNodeIcon(badgeImage, badgeSprites, def.levelId);

            var view = new LevelNodeView(crystalImage, badgeImage, button, def);

            if (button != null && _model.IsUnlocked(def.levelId))
            {
                var captured = def;
                button.onClick.AddListener(() => OpenDetail(captured));
            }

            return view;
        }

        /// <summary>按关卡号取节点图标（索引 = 关卡号-1）；未配置/越界时隐藏该图，避免占位白块。</summary>
        static void ApplyNodeIcon(Image image, Sprite[] sprites, int levelId)
        {
            if (image == null) return;

            var index = levelId - 1;
            var sprite = sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
            image.sprite = sprite;
            if (sprite == null) image.gameObject.SetActive(false);
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

        /// <summary>
        /// 单个关卡节点的视图句柄：节点是「水晶 + 徽章」图标按钮（无文字），
        /// 解锁态原色、未解锁整体压灰且不可点；星级/用时记录在详情面板里看。
        /// </summary>
        sealed class LevelNodeView
        {
            static readonly Color UnlockedColor = Color.white;                   // 有专属美术走原色
            static readonly Color LockedColor = new Color(0.40f, 0.40f, 0.44f); // 未解锁：整体压灰

            readonly Image _crystal;
            readonly Image _badge;
            readonly Button _button;
            readonly LevelDef _def;

            internal LevelNodeView(Image crystal, Image badge, Button button, LevelDef def)
            {
                _crystal = crystal;
                _badge = badge;
                _button = button;
                _def = def;
            }

            internal void Refresh(LevelSelectModel model)
            {
                var unlocked = model.IsUnlocked(_def.levelId);
                if (_crystal != null) _crystal.color = unlocked ? UnlockedColor : LockedColor;
                if (_badge != null) _badge.color = unlocked ? UnlockedColor : LockedColor;
                if (_button != null) _button.interactable = unlocked;
            }
        }
    }
}
