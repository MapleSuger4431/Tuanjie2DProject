using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;

/// <summary>
/// 关卡详情弹窗（Window 模态形态：根自带 Canvas，直挂 UIRoot，蒙版挡输入）。
/// 流程：点地图节点打开 → 第一行显示最佳记录（无记录显示 NO RECORD）→ 确认进入 → 切场景。
/// 结构约定：Mask 子节点（半透明遮罩 + Button，生成器按名回填 closeOnMaskClick=true）+ Content 内容区。
/// </summary>
public class LevelDetailWindow : AesirBaseWindowViewController<GameContext>
{
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Image previewImage;   // 关卡预览图（Title 与 Record 之间，占位；后续接入按关卡换图）
    [SerializeField] private TMP_Text recordLabel;
    [SerializeField] private Button enterButton;
    [SerializeField] private Button cancelButton;

    private LevelSelectModel _model;
    private LevelDef _current;

    protected override void OnInit()
    {
        // 生命周期：克隆 → OnInit → OnShow(payload)，早于 Start——Model 缓存与按钮绑定必须在 OnInit 完成
        _model = this.GetModel<LevelSelectModel>();
        enterButton.onClick.AddListener(OnEnterClicked);
        cancelButton.onClick.AddListener(CloseSelf);
    }

    protected override void OnShow(object payload)
    {
        base.OnShow(payload);   // 基类默认行为 = SetActive(true)，不可丢
        _current = payload as LevelDef;
        if (_current == null)
        {
            Debug.LogError("[LevelDetail] OnShow 收到的 payload 不是 LevelDef");
            return;
        }
        titleLabel.text = _current.displayName;
        // 第一行：先弹最好的记录；没有就显示无（NO RECORD，中文字体就位后切"无"）
        recordLabel.text = _model.HasRecord(_current.levelId)
            ? "BEST: " + new string('★', _model.GetBestStars(_current.levelId))
              + "   TIME: " + FormatTime(_model.GetBestTime(_current.levelId))
            : "NO RECORD";
    }

    private void OnEnterClicked()
    {
        if (_current == null) return;
        var path = "Assets/Scenes/" + _current.sceneName + ".unity";
        // 预检：未登记 Build Settings 就不触发加载（避免框架报 Error 噪音）；关卡场景就位后自动变为真实切换
        if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
        {
            Debug.LogWarning($"[LevelDetail] 场景 \"{path}\" 未登记 Build Settings，暂不切换（占位阶段预期行为）");
            CloseSelf();
            return;
        }
        SceneModule.LoadSceneSingle(path, onFailed: () =>
        {
            Debug.LogWarning($"[LevelDetail] 场景 \"{path}\" 切换失败");
            CloseSelf();
        });
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) return "--:--";
        var t = System.TimeSpan.FromSeconds(seconds);
        return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    }
}
