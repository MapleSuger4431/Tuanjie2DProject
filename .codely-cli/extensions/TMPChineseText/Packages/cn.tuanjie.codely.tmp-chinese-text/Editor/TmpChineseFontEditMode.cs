using UnityEditor;
using UnityEngine;

namespace Codely.TmpChineseFont
{
    public static class TmpChineseFontEditMode
    {
        public static bool CanWrite(out string status)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                status = TmpChineseFontStatus.RequiresEditMode;
                return false;
            }

            status = null;
            return true;
        }
    }
}
