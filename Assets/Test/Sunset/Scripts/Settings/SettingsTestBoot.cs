using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.UI;

namespace Sunset.Test.UI
{
    /// <summary>
    /// 设置按钮测试场景引导：场景内仅放本脚本与主相机（UIRoot 运行时自动创建）。
    /// <para>
    /// 运行时在左下角搭建一个"设置"测试按钮（独立 Screen Space Overlay 画布，模拟真实菜单里的设置入口），
    /// 点击经 <c>UIModule.Show&lt;SettingsPanel&gt;("UIPrefabs/SettingsPanel")</c> 从 Resources 加载并打开设置面板；
    /// 进场景时也会自动打开一次面板，便于直接测试。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class SettingsTestBoot : MonoBehaviour
    {
        void Start()
        {
            // 进场景即应用已保存的音效音量（设置持久化的启动应用）
            GameSettings.ApplySfxVolume();
            BuildTestButton();

            // 进场景自动打开一次设置面板（关闭后可用左下角按钮重新打开）
            UIModule.Show<SettingsPanel>("UIPrefabs/SettingsPanel");
            Debug.Log("[SettingsTestBoot] 设置测试就绪：面板已打开，左下角有\"设置\"按钮");
        }

        void BuildTestButton()
        {
            var canvasGo = new GameObject("TestButtonCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var btnGo = new GameObject("BtnSettings");
            btnGo.transform.SetParent(canvasGo.transform, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(160f, 70f);
            rect.sizeDelta = new Vector2(220f, 64f);

            var img = btnGo.AddComponent<Image>();
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            if (img.sprite != null)
            {
                img.type = Image.Type.Sliced;
            }

            var button = btnGo.AddComponent<Button>();

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.text = "设置";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 32;
            txt.color = new Color32(50, 50, 50, 255);
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 运行时挂中文系统字体（测试按钮独立于 UIRoot，面板的 OnInit 不会覆盖到它）
            SandboxUIFonts.Apply(canvasGo.transform);

            button.onClick.AddListener(() => UIModule.Show<SettingsPanel>("UIPrefabs/SettingsPanel"));
        }
    }
}
