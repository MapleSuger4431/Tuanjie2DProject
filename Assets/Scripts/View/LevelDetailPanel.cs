using System;
using Game.Core;
using Game.Model;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 关卡详情弹窗（**面板形态**：RectTransform 根、无自带 Canvas，挂 UIRoot 的 Popup 层 Canvas 之下）。
    /// <para>
    /// 全项目统一只用 Panel：不使用 UIModule 的 Open/Close 窗口接口
    /// （窗口直挂 UIRoot 根节点、绕开分层 Canvas，UIRoot 跨场景常驻时清理与层级都不可控）。
    /// 打开走 <c>UIModule.Show&lt;LevelDetailPanel&gt;((object)def, UIPaths.LevelDetailPanel)</c>，
    /// 关闭走 <see cref="AesirBasePanel.HideSelf" />。
    /// </para>
    /// <para>
    /// 模态与关闭双通道由面板自身的全屏 <c>Mask</c> 子物体承接（Image 拦截射线 + Button 收点击），
    /// 面板形态没有窗口专属的蒙版调度。结构由 Assets/Editor/LevelSelectUIBuilder 产出。
    /// </para>
    /// 流程：点地图节点打开 → 显示最佳记录（无记录显示「暂无记录」）→ 确认进入 → 切场景。
    /// </summary>
    [AddComponentMenu("")]
    public class LevelDetailPanel : AesirBasePanelViewController<GameContext>
    {
        [Tooltip("标题文本")]
        [SerializeField]
        TMP_Text titleLabel;

        [Tooltip("关卡预览图（当前布局未启用：2026-10-18 星级行占了原预览区；恢复时在 LevelSelectUIBuilder.BuildLevelDetailPanel 重建节点并回填）")]
        [SerializeField]
        Image previewImage;

        [Tooltip("星级图标，3 个（标题与记录文本之间水平排布）；点亮=白色原色、未获得=暗灰，由 ShowStars 控制")]
        [SerializeField]
        Image[] starImages;

        // ⚠️ 两个记录标签（RecordLabel/LastLabel）的数量与形态待定：用户 2026-10-18 手动调整过两者在预制体上的
        // 布局并表示"后续确定信息后再调整"——改动这两个标签前先与用户确认保留几个；布局参数以预制体手调版为准，
        // 重跑 LevelSelectUIBuilder 会覆盖手调，勿随意重跑。
        [Tooltip("最短用时文本")]
        [SerializeField]
        TMP_Text recordLabel;

        [Tooltip("上次用时文本（未通关时留空）——该标签是否保留待定（见上方注释）")]
        [SerializeField]
        TMP_Text lastLabel;

        [Tooltip("进入按钮")]
        [SerializeField]
        Button enterButton;

        [Tooltip("取消按钮")]
        [SerializeField]
        Button cancelButton;

        [Tooltip("进入按钮文本")]
        [SerializeField]
        TMP_Text enterButtonLabel;

        [Tooltip("取消按钮文本")]
        [SerializeField]
        TMP_Text cancelButtonLabel;

        LevelSelectModel _model;
        LevelDef _current;

        protected override void OnInit()
        {
            // 生命周期：克隆 → OnInit → OnShow(payload)，早于 Start——Model 缓存与按钮绑定必须在 OnInit 完成
            _model = this.GetModel<LevelSelectModel>();
            enterButton.onClick.AddListener(OnEnterClicked);
            cancelButton.onClick.AddListener(HideSelf);

            // 蒙版点击关闭：面板没有 AesirBaseWindow.OnMaskClicked 回调，由 Mask 子物体的 Button 直接承接
            var maskTransform = transform.Find("Mask");
            if (maskTransform != null)
            {
                var maskButton = maskTransform.GetComponent<Button>();
                if (maskButton != null)
                {
                    maskButton.onClick.AddListener(HideSelf);
                }
            }

            Localization.OnLanguageChanged += ApplyLocalization;
            ApplyLocalization();
        }

        protected override void OnDestroy()
        {
            Localization.OnLanguageChanged -= ApplyLocalization;
            base.OnDestroy();
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

            titleLabel.text = Localization.Get(Localization.Key.LevelName, _current.levelId);

            // 成绩显示走下面两个公开接口（搜索关键字：ShowStars / ShowRecord）
            ShowRecord(_current);
            ShowStars(_model.GetBestStars(_current.levelId));
        }

        // ============================================================
        //  关卡成绩接口（外部绑定/查询入口）
        //  数据源：LevelSelectModel → LevelProgressModel（随存档持久化），
        //  每次打开都读最新值——刚通关回选关再点开这关，看到的就是刷新后的成绩。
        //  当前星级口径（用户拍板）：通关 = 1 星；3 个星位留给后续星级规则扩展。
        // ============================================================

        /// <summary>暗灰星（未获得）。</summary>
        static readonly Color DimStarColor = new Color(0.40f, 0.40f, 0.44f);

        /// <summary>
        /// 刷新星级显示：第 count 个之前的星点亮（白色原色），其余暗灰。
        /// OnShow 自动调用（数据源 LevelSelectModel.GetBestStars）；外部（测试/结算页等）也可直接调用。
        /// </summary>
        public void ShowStars(int starCount)
        {
            if (starImages == null || starImages.Length == 0) return;
            var lit = Mathf.Clamp(starCount, 0, starImages.Length);
            for (var i = 0; i < starImages.Length; i++)
                starImages[i].color = i < lit ? Color.white : DimStarColor;
        }

        /// <summary>
        /// 读取指定关卡的存档记录并打印到弹窗：最短用时 + 上次用时（未通关显示「暂无记录」、上次留空）。
        /// OnShow 自动调用；外部也可直接调用来刷新。
        /// </summary>
        public void ShowRecord(LevelDef def)
        {
            var cleared = _model.HasRecord(def.levelId);

            recordLabel.text = cleared
                ? Localization.Get(Localization.Key.LevelBestTime, FormatTime(_model.GetBestTime(def.levelId)))
                : Localization.Get(Localization.Key.LevelNoRecord);

            if (lastLabel != null)
                lastLabel.text = cleared
                    ? Localization.Get(Localization.Key.LevelLastTime, FormatTime(_model.GetLastTime(def.levelId)))
                    : string.Empty;
        }

        protected override void OnHide()
        {
            _current = null;
            base.OnHide();
        }

        void ApplyLocalization()
        {
            if (enterButtonLabel != null) enterButtonLabel.text = Localization.Get(Localization.Key.LevelDetailEnter);
            if (cancelButtonLabel != null) cancelButtonLabel.text = Localization.Get(Localization.Key.LevelDetailCancel);

            // 已打开时切换语言：标题与记录随语言重刷
            if (_current != null) OnShow(_current);
        }

        void OnEnterClicked()
        {
            if (_current == null) return;
            var path = "Assets/Scenes/" + _current.sceneName + ".unity";
            // 预检：未登记 Build Settings 就不触发加载（避免框架报 Error 噪音）；关卡场景就位后自动变为真实切换
            if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
            {
                Debug.LogWarning($"[LevelDetail] 场景 \"{path}\" 未登记 Build Settings，暂不切换（占位阶段预期行为）");
                HideSelf();
                return;
            }

            // 走 SceneTransition：先收起选关界面与本弹窗再加载，避免 UIRoot 常驻导致旧界面叠进关卡
            SceneTransition.LoadSingle(path,
                onFailed: () => Debug.LogWarning($"[LevelDetail] 场景 \"{path}\" 切换失败"));
        }

        /// <summary>用时文本：mm:ss.d，与关卡内秒表（BoardTimeModel.FormatElapsed）同款口径。</summary>
        static string FormatTime(float seconds)
        {
            if (seconds < 0f) return "--:--.-";

            var t = TimeSpan.FromSeconds(seconds);
            return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}";
        }
    }
}
