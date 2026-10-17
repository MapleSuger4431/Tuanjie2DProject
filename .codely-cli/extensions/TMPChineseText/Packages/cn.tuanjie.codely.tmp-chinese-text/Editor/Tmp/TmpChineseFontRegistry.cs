using UnityEditor;
using UnityEngine;
#if TMP_PRESENT
using TMPro;
#endif

namespace Codely.TmpChineseFont
{
    public sealed class TmpChineseFontRegistry : ScriptableObject
    {
#if TMP_PRESENT
        public TMP_FontAsset designated;
        public TMP_FontAsset previous;
#endif
        public bool useAsDefaultFont;

        public static TmpChineseFontRegistry LoadOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TmpChineseFontRegistry>(
                TmpChineseFontPaths.RegistryAsset);
            if (existing != null)
                return existing;

            var folder = TmpChineseFontPaths.ProjectFontsFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Codely"))
                    AssetDatabase.CreateFolder("Assets", "Codely");
                AssetDatabase.CreateFolder("Assets/Codely", "Fonts");
            }

            var created = CreateInstance<TmpChineseFontRegistry>();
            AssetDatabase.CreateAsset(created, TmpChineseFontPaths.RegistryAsset);
            AssetDatabase.SaveAssets();
            return created;
        }

        public static void Save(TmpChineseFontRegistry registry)
        {
            if (registry == null)
                return;
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
        }
    }
}
