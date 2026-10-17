using UnityEngine;
using UnityEngine.UI;

namespace Sunset.Test.UI
{
    /// <summary>
    /// 跨平台中文动态字体工具（仿 SampleUIFonts）：探测系统字体列表生成动态字体
    /// 并应用到子树全部 Text。场景/预制体内静态保存的 Text 默认字体对中文字形不可靠，
    /// 统一在运行时挂字体保证中文正常显示。
    /// </summary>
    public static class SandboxUIFonts
    {
        static Font cached;

        /// <summary>为指定根物体子树内全部 Text 应用系统中文动态字体。</summary>
        public static void Apply(Transform root)
        {
            if (cached == null)
            {
                cached = CreateChineseFont();
            }

            var texts = root.GetComponentsInChildren<Text>(true);
            foreach (var text in texts)
            {
                text.font = cached;
            }
        }

        /// <summary>跨平台候选探测顺序 Windows → macOS → Linux，全部不可用时回退内置字体。</summary>
        static Font CreateChineseFont()
        {
            var candidates = new[]
                { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "WenQuanYi Zen Hei" };
            var installedFonts = Font.GetOSInstalledFontNames();
            foreach (var candidate in candidates)
            {
                foreach (var installedName in installedFonts)
                {
                    if (installedName == candidate)
                    {
                        return Font.CreateDynamicFontFromOSFont(installedName, 22);
                    }
                }
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
