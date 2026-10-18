using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 程序化 UI 贴图绘制器：用代码画圆角矩形（可选描边、纵向渐变），落成 PNG 资产并配好 Sprite 导入设置。
    /// 目前服务于棋盘运行时面板的深紫皮肤（面板底 / 卡片底）；重跑即重画，字节确定性产出。
    /// </summary>
    public static class UiSpritePainter
    {
        public const string SpriteDir = "Assets/Art/Board/UI";
        public const string PanelSpritePath = SpriteDir + "/Panel_DarkPurple.png";
        public const string CardSpritePath = SpriteDir + "/Card_Rounded.png";
        public const string PlaqueSpritePath = SpriteDir + "/Plaque_Violet.png";

        /// <summary>深紫面板：纵向渐变填充 + 亮紫描边，圆角 24px，9-slice 边 36。</summary>
        public static readonly Color PanelTop = new Color(0.133f, 0.082f, 0.216f, 1f);
        public static readonly Color PanelBottom = new Color(0.075f, 0.047f, 0.137f, 1f);
        public static readonly Color PanelStroke = new Color(0.463f, 0.290f, 0.737f, 1f);

        /// <summary>深紫描边之外再垫一圈更暗的收边，避免描边与外界生硬相接。</summary>
        public static readonly Color PanelOuterStroke = new Color(0.271f, 0.161f, 0.447f, 1f);

        /// <summary>卡片：白色带轻渐变（供 Image.color 染色），无描边。</summary>
        public static readonly Color CardTop = new Color(1f, 1f, 1f, 1f);
        public static readonly Color CardBottom = new Color(0.82f, 0.82f, 0.86f, 1f);

        /// <summary>按钮牌匾：暗紫宝石底 + 古铜金描边（旧魔导书那套，配羊皮纸卷轴）。</summary>
        public static readonly Color PlaqueTop = new Color(0.353f, 0.180f, 0.435f, 1f);
        public static readonly Color PlaqueBottom = new Color(0.196f, 0.078f, 0.267f, 1f);
        public static readonly Color PlaqueStroke = new Color(0.78f, 0.63f, 0.33f, 1f);
        public static readonly Color PlaqueOuterStroke = new Color(0.259f, 0.125f, 0.322f, 1f);

        /// <summary>生成（或重画）两张皮肤贴图并返回加载好的 Sprite。</summary>
        public static Sprite EnsurePanelSprite() => EnsureSprite(PanelSpritePath, 128, 24f, 3f, PanelTop, PanelBottom, PanelStroke, PanelOuterStroke, new Vector4(36f, 36f, 36f, 36f));

        public static Sprite EnsureCardSprite() => EnsureSprite(CardSpritePath, 128, 20f, 0f, CardTop, CardBottom, Color.clear, Color.clear, Vector4.zero);

        /// <summary>按钮牌匾：128px、圆角 28、金边 4px，9-slice 边 40（拉伸到 330×88 仍保住圆角与描边）。</summary>
        public static Sprite EnsurePlaqueSprite() => EnsureSprite(PlaqueSpritePath, 128, 28f, 4f, PlaqueTop, PlaqueBottom, PlaqueStroke, PlaqueOuterStroke, new Vector4(40f, 40f, 40f, 40f));

        static Sprite EnsureSprite(
            string path, int size, float radius, float border,
            Color top, Color bottom, Color stroke, Color outerStroke, Vector4 sliceBorder)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            WritePng(path, size, radius, border, top, bottom, stroke, outerStroke);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path, size, sliceBorder);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// 圆角矩形光栅化：对每个像素求到圆角矩形边界的有符号距离，
        /// 内部按 y 向渐变填充，边界 <paramref name="border" /> 像素内画描边，最外圈再垫一圈更暗收边。
        /// </summary>
        static void WritePng(string path, int size, float radius, float border, Color top, Color bottom, Color stroke, Color outerStroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float inner = half - radius;
            var center = new Vector2(half - 0.5f, half - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x - center.x) - inner;
                    float qy = Mathf.Abs(y - center.y) - inner;
                    // 到边界的有符号距离：内负外正（SDF of rounded box）
                    float dist = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                                 + Mathf.Min(Mathf.Max(qx, qy), 0f);

                    var fill = Color.Lerp(bottom, top, y / (size - 1f));
                    pixels[y * size + x] = Shade(dist, fill, border, stroke, outerStroke);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static Color32 Shade(float dist, Color fill, float border, Color stroke, Color outerStroke)
        {
            // 1px 抗锯齿：dist -0.5 → 全覆盖，+0.5 → 全透明
            float coverage = Mathf.Clamp01(0.5f - dist);
            if (coverage <= 0f) return default;

            Color color;
            if (border > 0f && dist > -border) color = stroke;
            else if (border > 0f && dist > -border - 1.5f) color = outerStroke;
            else color = fill;

            return new Color(color.r, color.g, color.b, color.a * coverage);
        }

        static void ConfigureImporter(string path, int size, Vector4 sliceBorder)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[UiSpritePainter] 拿不到 {path} 的 TextureImporter");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = size;
            importer.spriteBorder = sliceBorder;
            importer.SaveAndReimport();
        }
    }
}
