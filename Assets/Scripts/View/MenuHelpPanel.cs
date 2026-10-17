using Game.Core;
using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 操作说明弹窗（面板形态，与 MainMenuPanel 同构：RectTransform 根、无 Canvas，
    /// 挂载于 UIRoot 的 Popup 层 Canvas 之下）。
    /// <para>
    /// 经 <c>UIModule.Show&lt;MenuHelpPanel&gt;(UIPaths.MenuHelpPanel)</c> 打开；
    /// 标题/正文由本地化接管（<see cref="Game.Core.Localization" />），语言切换时即时刷新。
    /// 支持关闭按钮与点击蒙版双通道关闭。
    /// 面板没有 UIModule 的蒙版调度（那是窗口专属），蒙版直接用面板自身的
    /// 全屏 Mask 子物体（Image 拦截射线 + Button 承接点击）实现模态与关闭。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class MenuHelpPanel : AesirBasePanel
    {
        [Tooltip("标题文本（由打开时的 payload 设置）")]
        [SerializeField]
        Text titleText;

        [Tooltip("说明正文")]
        [SerializeField]
        Text contentText;

        [Tooltip("关闭按钮")]
        [SerializeField]
        Button closeButton;

        /// <summary>说明正文（只读，便于外部验证）。</summary>
        public Text ContentText => contentText;

        protected override void OnInit()
        {
            // 静态 Text 的默认字体对中文字形不可靠，运行时统一挂系统中文动态字体
            GameFonts.Apply(transform);

            closeButton.onClick.AddListener(HideSelf);

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

            // 标题/正文由本地化接管：订阅语言切换（打开中切语言也即时刷新），初始化即应用一次
            Localization.OnLanguageChanged += ApplyLocalization;
            ApplyLocalization();
        }

        protected override void OnDestroy()
        {
            Localization.OnLanguageChanged -= ApplyLocalization;
            base.OnDestroy();
        }

        /// <summary>按当前语言刷新标题与正文。</summary>
        void ApplyLocalization()
        {
            if (titleText != null)
            {
                titleText.text = Localization.Get(Localization.Key.HelpTitle);
            }

            if (contentText != null)
            {
                contentText.text = Localization.Get(Localization.Key.HelpBody);
            }
        }
    }
}
