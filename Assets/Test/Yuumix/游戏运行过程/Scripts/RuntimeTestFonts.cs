using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameRuntimeTest
{
    /// <summary>
    /// 跨平台中文动态字体。动态 Font 不是可序列化资产，预制体里只放内置字体占位，
    /// 运行时由面板 OnInit 对子树统一 Apply。
    /// </summary>
    public static class RuntimeTestFonts
    {
        static readonly string[] Candidates =
        {
            "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "WenQuanYi Zen Hei", "Arial"
        };

        static Font s_Font;

        public static Font Get()
        {
            if (s_Font != null) return s_Font;

            var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
            foreach (var name in Candidates)
            {
                if (!installed.Contains(name)) continue;
                s_Font = Font.CreateDynamicFontFromOSFont(name, 16);
                break;
            }

            if (s_Font == null)
                s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return s_Font;
        }

        public static void Apply(Transform root)
        {
            var font = Get();
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                text.font = font;
        }
    }
}
