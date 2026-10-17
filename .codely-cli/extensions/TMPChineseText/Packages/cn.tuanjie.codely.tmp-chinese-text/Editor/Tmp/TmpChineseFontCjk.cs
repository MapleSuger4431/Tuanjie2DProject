using UnityEngine;
#if TMP_PRESENT
using TMPro;
#endif

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontCjk
    {
        public const char Probe = '中';

        public static bool FontHasCjk(Font font)
        {
            return font != null && font.HasCharacter(Probe);
        }

#if TMP_PRESENT
        public static bool FontAssetHasCjk(TMP_FontAsset font)
        {
            if (font == null)
                return false;
            if (font.HasCharacter(Probe, false, false))
                return true;
            return font.atlasPopulationMode != AtlasPopulationMode.Static
                   && FontHasCjk(font.sourceFontFile);
        }
#endif
    }
}
