using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.View;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;

/// <summary>
/// 选关面板（Panel 形态，根不带 Canvas，挂框架 Normal 层）：
/// 大地图 + 关卡节点。点已解锁节点 → 打开 LevelDetailWindow（Window 模态弹窗）。
/// 静态结构（背景/标题/节点容器）由生成器 UiPrefabBuilder 产出；
/// 节点数量由 Model 关卡表驱动、运行时生成——改关卡数无需重跑生成器。
/// </summary>
public class LevelSelectPanel : AesirBasePanelViewController<GameContext>
{
    [SerializeField] private RectTransform nodesRoot;   // 节点容器（生成器回填）
    [SerializeField] private Button backButton;         // 左上：返回主菜单（生成器回填）
    [SerializeField] private Button settingsButton;     // 右上：设置（生成器回填）
    [SerializeField] private GameObject levelNodePrefab; // 节点模板（生成器回填，美术改节点视觉只动它）

    private LevelSelectModel _model;

    protected override void OnInit()
    {
        // 按钮绑定放 OnInit（早于 Start，对齐框架生命周期）
        backButton.onClick.AddListener(OnBackClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
    }

    private void Start()
    {
        _model = this.GetModel<LevelSelectModel>();
        BuildNodes();
    }

    private void OnBackClicked()
    {
        // 返回主菜单：切到队友已入库的主菜单场景（已登记 Build Settings）
        // 切走前隐藏本面板——UIRoot 跨场景常驻，不隐藏会把选关面板带到主菜单场景
        UIModule.Hide<LevelSelectPanel>();
        SceneModule.LoadSceneSingle(ScenePaths.MainMenu,
            onFailed: () =>
            {
                UIModule.Show<LevelSelectPanel>(path: TestTFUIPaths.LevelSelectPanel); // 切换失败恢复面板
                Debug.LogWarning($"[LevelSelect] 切换开始菜单场景 \"{ScenePaths.MainMenu}\" 失败");
            });
    }

    private void OnSettingsClicked()
    {
        // 设置面板（队友已入库）：Popup 层面板，蒙版 / × 双通道关闭由面板自身处理
        UIModule.Show<MenuSettingsPanel>(path: UIPaths.MenuSettingsPanel);
    }

    private void BuildNodes()
    {
        if (nodesRoot == null)
        {
            Debug.LogError("[LevelSelect] nodesRoot 未回填，请重跑 UiPrefabBuilder.RebuildAll()");
            return;
        }
        if (levelNodePrefab == null)
        {
            Debug.LogError("[LevelSelect] levelNodePrefab 未回填，请重跑 UiPrefabBuilder.RebuildAll()");
            return;
        }

        foreach (var def in _model.Levels)
            CreateNode(def);
    }

    private void CreateNode(LevelDef def)
    {
        var unlocked = _model.IsUnlocked(def.levelId);

        // 实例化节点模板（视觉结构在 LevelNode.prefab，美术后续直接改 prefab）
        var nodeGo = Instantiate(levelNodePrefab, nodesRoot);
        nodeGo.name = "Node_" + def.levelId;
        var node = (RectTransform)nodeGo.transform;
        // 锚点定位：anchor 即归一化位置——不依赖布局更新时序
        node.anchorMin = node.anchorMax = def.nodePos;
        node.anchoredPosition = Vector2.zero;

        // 按数据设置底色 / 文本 / 交互
        node.GetComponent<Image>().color = unlocked
            ? new Color(0.24f, 0.46f, 0.62f)
            : new Color(0.30f, 0.30f, 0.33f);

        var button = node.GetComponent<Button>();
        button.interactable = unlocked;

        node.Find("Name").GetComponent<TMP_Text>().text = def.displayName;
        string starsText;
        if (!unlocked) starsText = "LOCKED";
        else if (!_model.HasRecord(def.levelId)) starsText = "---";
        else starsText = new string('★', _model.GetBestStars(def.levelId)).PadRight(3, '☆');
        node.Find("Stars").GetComponent<TMP_Text>().text = starsText;

        if (unlocked)
        {
            var captured = def;
            button.onClick.AddListener(() => OpenDetail(captured));
        }
    }

    private void OpenDetail(LevelDef def)
    {
        UIModule.Open<LevelDetailWindow>(def, path: TestTFUIPaths.LevelDetailWindow);
    }
}
