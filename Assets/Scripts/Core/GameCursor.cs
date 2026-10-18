using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 全局自定义鼠标光标：进 Play 即应用，不依赖任何场景接线（引擎级全局状态，跨场景常驻）。
    /// 贴图须以 Texture Type: Cursor + Read/Write Enabled 导入（Resources 下走 Resources.Load）。
    /// </summary>
    public static class GameCursor
    {
        /// <summary>Resources 下的光标贴图路径（不含扩展名）。</summary>
        public const string CursorTexturePath = "UI/Cursor_Gem";

        /// <summary>显示缩放：系统光标的显示尺寸 = 贴图像素尺寸，原图 55×64 偏大，默认缩半。</summary>
        public const float CursorScale = 0.5f;

        /// <summary>点击热点（箭头尖端），自贴图左上角起算的像素坐标，55×64 贴图实测尖端为 (1,0)。</summary>
        public static readonly Vector2 Hotspot = new Vector2(1f, 0f);

        private static Texture2D _scaled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            Texture2D texture = Resources.Load<Texture2D>(CursorTexturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[GameCursor] 未找到光标贴图 Resources/{CursorTexturePath}，沿用系统默认光标");
                return;
            }

            // 缓存缩放结果：非域重载反复进 Play 时不重复生成
            if (_scaled == null)
                _scaled = ScaleTexture(texture, CursorScale);

            // Auto：优先硬件光标（无跟随延迟），平台不支持该尺寸时引擎自动回退软件渲染。
            Cursor.SetCursor(_scaled, Hotspot * CursorScale, CursorMode.Auto);
        }

        /// <summary>双线性缩放贴图（光标贴图 isReadable，直接 Get/SetPixels；不走 RenderTexture，避免颜色空间往返误差）。</summary>
        private static Texture2D ScaleTexture(Texture2D source, float scale)
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            Color[] src = source.GetPixels();
            Color[] dst = new Color[w * h];
            float sx = source.width / (float)w;
            float sy = source.height / (float)h;
            for (int y = 0; y < h; y++)
            {
                // 目标像素中心映射回源图的连续坐标后做双线性插值
                float fy = (y + 0.5f) * sy - 0.5f;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, source.height - 1);
                int y1 = Mathf.Min(y0 + 1, source.height - 1);
                float ty = Mathf.Clamp01(fy - y0);
                int row0 = y0 * source.width;
                int row1 = y1 * source.width;
                for (int x = 0; x < w; x++)
                {
                    float fx = (x + 0.5f) * sx - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, source.width - 1);
                    int x1 = Mathf.Min(x0 + 1, source.width - 1);
                    float tx = Mathf.Clamp01(fx - x0);
                    dst[y * w + x] = Color.Lerp(
                        Color.Lerp(src[row0 + x0], src[row0 + x1], tx),
                        Color.Lerp(src[row1 + x0], src[row1 + x1], tx),
                        ty);
                }
            }

            var result = new Texture2D(w, h, TextureFormat.ARGB32, false);
            result.SetPixels(dst);
            result.Apply();
            return result;
        }
    }
}
