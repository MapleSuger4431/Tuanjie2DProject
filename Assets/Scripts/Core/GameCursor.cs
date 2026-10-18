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

        /// <summary>点击热点（箭头尖端），自贴图左上角起算的像素坐标，55×64 贴图实测尖端为 (1,0)。</summary>
        public static readonly Vector2 Hotspot = new Vector2(1f, 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            Texture2D texture = Resources.Load<Texture2D>(CursorTexturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[GameCursor] 未找到光标贴图 Resources/{CursorTexturePath}，沿用系统默认光标");
                return;
            }

            // Auto：优先硬件光标（无跟随延迟），平台不支持该尺寸时引擎自动回退软件渲染。
            Cursor.SetCursor(texture, Hotspot, CursorMode.Auto);
        }
    }
}
