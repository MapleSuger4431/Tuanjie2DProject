using System.Collections.Generic;
using Game.Core;
using Game.Model;
using Game.Service;
using Runestone.AesirModules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 成就图鉴面板（**Panel 形态**：根只有 RectTransform，无自带 Canvas，挂 UIRoot 的 Popup 层）。
    /// 主菜单「成就图鉴」按钮打开，列出成就配置库里的全部条目：图标 + 名称 + 描述 + 状态
    /// （已解锁显示解锁日期，未解锁显示「未解锁」或计数进度，隐藏成就解锁前显示 ???）。
    /// <para>
    /// 数据全部来自 <see cref="AchievementService" />（定义）与 <see cref="AchievementModel" />（状态）；
    /// 行节点**只创建一次后复用**（每次打开只改文本与图标）——反复 Destroy 重建会因销毁延迟把列表越点越长。
    /// 结构由 <c>Assets/Editor/AchievementPanelBuilder.cs</c> 重建，改 UI 改生成器。
    /// </para>
    /// </summary>
    public sealed class AchievementPanel : AesirBasePanelViewController<GameContext>
    {
        [Header("结构")]
        [Tooltip("图鉴标题")]
        [SerializeField] TMP_Text titleText;

        [Tooltip("完成度：已解锁 / 总数")]
        [SerializeField] TMP_Text completedText;

        [Tooltip("成就行容器（VerticalLayoutGroup）")]
        [SerializeField] RectTransform listRoot;

        [Tooltip("行模板：运行时按成就条数克隆，模板自身保持停用")]
        [SerializeField] GameObject rowTemplate;

        [Tooltip("关闭按钮")]
        [SerializeField] Button closeButton;

        [Tooltip("遮罩上的点击关闭按钮（点面板外空白关闭）")]
        [SerializeField] Button maskButton;

        [Tooltip("关闭按钮文本")]
        [SerializeField] TMP_Text closeButtonLabel;

        [Header("配色")]
        [Tooltip("已解锁行的名称颜色（亮紫粉）")]
        [SerializeField] Color unlockedColor = new Color(1f, 0.42f, 0.85f, 1f);

        [Tooltip("未解锁行的名称颜色（灰紫）")]
        [SerializeField] Color lockedColor = new Color(0.45f, 0.43f, 0.55f, 1f);

        [Tooltip("未解锁图标的置灰染色")]
        [SerializeField] Color lockedIconColor = new Color(0.30f, 0.29f, 0.36f, 0.75f);

        /// <summary>一行的四个节点（克隆后缓存，避免每次查找）。</summary>
        sealed class Row
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text Name;
            public TMP_Text Desc;
            public TMP_Text Status;
        }

        readonly List<Row> _rows = new List<Row>();

        AchievementService _achievements;

        protected override void OnInit()
        {
            if (closeButton != null) closeButton.onClick.AddListener(HideSelf);
            if (maskButton != null) maskButton.onClick.AddListener(HideSelf);

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
            base.OnShow(payload);

            // 每次打开都重新取状态：解锁可能发生在上次打开之后
            if (_achievements == null) _achievements = GameContext.Instance.GetService<AchievementService>();
            ApplyLocalization();
            RefreshRows();
        }

        void ApplyLocalization()
        {
            if (titleText != null) titleText.text = Localization.Get("AchievementTitle");
            if (closeButtonLabel != null) closeButtonLabel.text = Localization.Get("AchievementClose");

            if (completedText != null && _achievements != null)
                completedText.text = Localization.Get("AchievementCompleted", _achievements.UnlockedCount,
                    _achievements.TotalCount);

            if (_rows.Count > 0) RefreshRows();
        }

        /// <summary>按成就条数准备行（缺的克隆、多的停用），再逐行填内容。</summary>
        void RefreshRows()
        {
            if (_achievements == null || listRoot == null || rowTemplate == null) return;

            var items = _achievements.Achievements;
            while (_rows.Count < items.Count)
            {
                var clone = Instantiate(rowTemplate, listRoot, false);
                clone.SetActive(true);
                _rows.Add(new Row
                {
                    Root = clone,
                    Icon = FindImage(clone, "Icon"),
                    Name = FindText(clone, "NameText"),
                    Desc = FindText(clone, "DescText"),
                    Status = FindText(clone, "StatusText"),
                });
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var visible = i < items.Count;
                if (row.Root != null) row.Root.SetActive(visible);
                if (!visible) continue;

                FillRow(row, items[i]);
            }

            if (completedText != null)
                completedText.text = Localization.Get("AchievementCompleted", _achievements.UnlockedCount,
                    _achievements.TotalCount);
        }

        void FillRow(Row row, GameAchievement achievement)
        {
            if (achievement == null) return;

            var unlocked = _achievements.IsUnlocked(achievement.Id);
            var hidden = achievement.Hidden && !unlocked;

            if (row.Icon != null)
            {
                row.Icon.sprite = achievement.Icon;
                row.Icon.color = unlocked ? Color.white : lockedIconColor;
            }

            if (row.Name != null)
            {
                row.Name.text = hidden ? Localization.Get("AchievementHidden") : Localization.Get(achievement.TitleKey);
                row.Name.color = unlocked ? unlockedColor : lockedColor;
            }

            if (row.Desc != null)
                row.Desc.text = hidden ? Localization.Get("AchievementHidden") : Localization.Get(achievement.DescKey);

            if (row.Status != null) row.Status.text = StatusText(achievement, unlocked);
        }

        /// <summary>状态行：已解锁显示日期；未解锁显示进度（计数型 x/阈值）或「未解锁」。</summary>
        string StatusText(GameAchievement achievement, bool unlocked)
        {
            if (unlocked)
            {
                var utc = _achievements.GetUnlockTime(achievement.Id);
                var date = ParseDate(utc);
                return date == null ? string.Empty : Localization.Get("AchievementUnlockedAt", date);
            }

            var progress = _achievements.GetProgress(achievement.Id);
            return progress > 0 && achievement.Threshold > 1
                ? $"{progress}/{achievement.Threshold}"
                : Localization.Get("AchievementLocked");
        }

        /// <summary>ISO 8601 UTC → 本地日期（yyyy-MM-dd）；解析失败返回 null（不显示日期行）。</summary>
        static string ParseDate(string utc)
        {
            if (string.IsNullOrEmpty(utc)) return null;

            return System.DateTime.TryParse(utc, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed.ToLocalTime().ToString("yyyy-MM-dd")
                : null;
        }

        static Image FindImage(GameObject root, string name)
        {
            var child = root.transform.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        static TMP_Text FindText(GameObject root, string name)
        {
            var child = root.transform.Find(name);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }
    }
}
