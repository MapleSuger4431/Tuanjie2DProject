using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.View;
using Runestone.AesirModules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单资产生成器（重跑 = 覆盖产物，禁止在产物上手改结构；要改就改这里再重建）：
/// 1. 从封面设计图与 UI 切片总览图生成 Assets/Art/MainMenu/ 下的封面背景（程序化抠除品牌区与烘焙按钮）、
///    按钮底图与品牌切片（棋盘距离键控抠图），主菜单资产不引用沙盒 Assets/Test
/// 2. 生成 Assets/Resources/UIPrefabs/MainMenuPanel.prefab、MenuHelpPanel.prefab 与 MenuSettingsPanel.prefab
///    （按 Assets/Arts/封面设计图/封面全局.png 复刻：AI 抠图成品 封面背景.png 铺底（装饰边框与全部 UI 已抠除），品牌区切片徽章/标题/EDGE GRID/分隔线/标语分层，
///    四按钮右下锚点 x=-155、y=502/379/257/134（开始=横幅底图、说明/设置=牌匾底图、退出=胶囊底图，均无字键控抠图成品 + Text 标签；继续游戏已并入开始游戏），
///    操作说明弹窗 760×560 + 全屏蒙版，设置面板 860×680）
/// 3. 生成 Assets/Scenes/MainMenu.scene（主相机 + MainMenuBoot，Additive 方式不扰动当前打开场景）
/// 4. 把 MainMenu.scene 登记进 Build Settings（Bootstrap 保持 index 0）
/// </summary>
public static class MainMenuBuilder
{
    /// <summary>封面设计图源路径（沙盒 Arts 目录）。</summary>
    const string CoverSource = "Assets/Arts/封面设计图/封面全局.png";

    /// <summary>封面背景在主工程的落位（原设计图抠除右列品牌区与烘焙按钮后的成品，程序化插值旧方案，现仅留档）。</summary>
    const string CoverDest = "Assets/Art/MainMenu/CoverFull.png";

    /// <summary>主菜单实际使用的背景：AI 局部重绘成品（装饰边框与全部 UI 抠除、星空补全并羽化接缝的纯背景）。</summary>
    const string MenuBackground = "Assets/Arts/封面设计图/封面背景.png";

    /// <summary>开始按钮底图：键控抠图成品（透明底、无字无图标、无星徽），九宫格翅膀段已配。</summary>
    const string BtnStartPath = "Assets/Arts/UI组件/按钮底图_开始_无字.png";

    /// <summary>次级按钮底图（牌匾造型，继续/玩法说明/设置共用），九宫格圆角段已配。</summary>
    const string BtnPlaquePath = "Assets/Arts/UI组件/按钮底图_玩法说明_无字.png";

    /// <summary>退出按钮底图（胶囊造型，底部烘焙投影已根除），九宫格圆角段已配。</summary>
    const string BtnQuitPath = "Assets/Arts/UI组件/按钮底图_退出_无字.png";

    /// <summary>弹窗/设置面板背景：操作说明.png（深紫华丽金框+顶部浅色牌匾，2048×2048 方图带透明边距，Simple 全幅）。</summary>
    const string PanelBgPath = "Assets/Arts/UI组件/操作说明.png";

    /// <summary>UI 切片总览图源路径（原 16bit RGB 无 alpha；2026-10-18 起背景已整图键控为透明、子精灵即取即用，EnsureMenuArt 的棋盘键控切片随之停用）。</summary>
    const string SheetSource = "Assets/Arts/UI组件/imageb52c1134c728.png";

    /// <summary>按钮底图落位目录。</summary>
    const string ButtonsFolder = "Assets/Art/MainMenu/buttons";

    /// <summary>品牌区抠图切片落位目录。</summary>
    const string PiecesFolder = "Assets/Art/MainMenu/pieces";

    /// <summary>
    /// 按钮底图在总览图上的搜索区（图像坐标 top-left origin 的 x1,y1,x2,y2 + 九宫格边框）。
    /// 实际裁切框由棋盘距离键控在搜索区内自动紧致化。
    /// </summary>
    static readonly (string name, int x1, int y1, int x2, int y2, Vector4 border)[] ButtonSliceDefs =
    {
        ("button_start_base", 612, 208, 952, 326, new Vector4(66f, 30f, 66f, 30f)),
        ("button_secondary_base", 612, 468, 948, 568, new Vector4(40f, 20f, 40f, 20f)),
    };

    /// <summary>品牌区切片在总览图上的搜索区（徽章/标题/EDGE GRID/分隔线/标语/两枚按钮图标）。</summary>
    static readonly (string name, int x1, int y1, int x2, int y2)[] PieceSliceDefs =
    {
        ("emblem", 135, 25, 450, 141),
        ("title", 70, 142, 510, 258),
        ("edgegrid", 95, 259, 485, 292),
        ("divider", 95, 295, 485, 315),
        ("slogan", 85, 365, 490, 428),
        ("icon_book", 855, 360, 950, 440),
        ("icon_exit", 855, 595, 950, 678),
    };

    static string PanelPrefabPath => "Assets/Resources/" + UIPaths.MainMenuPanel + ".prefab";

    static string HelpPrefabPath => "Assets/Resources/" + UIPaths.MenuHelpPanel + ".prefab";

    static string SettingsPrefabPath => "Assets/Resources/" + UIPaths.MenuSettingsPanel + ".prefab";

    [MenuItem("Tools/主菜单/重建主菜单资产")]
    public static void RebuildAll()
    {
        EnsureMenuArt();
        BuildMainMenuPanel();
        BuildMenuHelpPanel();
        BuildMenuSettingsPanel();
        BuildMainMenuScene();
        AssetDatabase.SaveAssets();
        SelfCheck();
    }

    /// <summary>
    /// 生成菜单美术（幂等：存在即跳过；改定义后删除产物文件重跑即可全量重切）：
    /// 1. 封面背景：原设计图解码 → 程序化抠掉右列品牌区与三枚烘焙按钮（行插值填充+羽化）→ CoverFull.png
    /// 2. 按钮底图与品牌切片：按棋盘距离键控从总览图紧致裁切（泛洪保内部不透明 + 距离渐变羽化发光边缘）
    /// </summary>
    public static void EnsureMenuArt()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(CoverDest) == null)
        {
            var cover = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            cover.LoadImage(File.ReadAllBytes(CoverSource));
            // 右列品牌区（徽章+标题+EDGE GRID+分隔线+标语）与三枚烘焙按钮的覆盖范围，图像坐标
            EraseRegion(cover, 660, 20, 1022, 552, 14, 8, 14, 14);
            File.WriteAllBytes(CoverDest, cover.EncodeToPNG());
            AssetDatabase.ImportAsset(CoverDest);
            SetSpriteImport(CoverDest, Vector4.zero);
            Debug.Log($"[MainMenuBuilder] 已从原设计图生成抠除品牌区的封面背景 {CoverDest}");
        }

        Directory.CreateDirectory(ButtonsFolder);
        Directory.CreateDirectory(PiecesFolder);
        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        sheet.LoadImage(File.ReadAllBytes(SheetSource));
        if (sheet.GetPixel(0, 0).a < 0.05f)
        {
            Debug.LogWarning("[MainMenuBuilder] 总览图已是透明键控版（背景已抠为透明），棋盘键控切片停用；已生成的按钮底图/品牌切片产物保留，若需重切请先从 git 恢复原版总览图");
            return;
        }

        foreach (var def in ButtonSliceDefs)
        {
            var outPath = ButtonsFolder + "/" + def.name + ".png";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(outPath) != null)
            {
                continue;
            }

            CutAndSavePiece(sheet, def.name, def.x1, def.y1, def.x2, def.y2, outPath, def.border);
        }

        foreach (var def in PieceSliceDefs)
        {
            var outPath = PiecesFolder + "/" + def.name + ".png";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(outPath) != null)
            {
                continue;
            }

            CutAndSavePiece(sheet, def.name, def.x1, def.y1, def.x2, def.y2, outPath, Vector4.zero);
        }

        Debug.Log("[MainMenuBuilder] 菜单美术就绪（封面 + 按钮底图 + 品牌切片）");
    }

    /// <summary>Sprite 导入设置（无 mipmap、透明、可选九宫格边框）。</summary>
    static void SetSpriteImport(string path, Vector4 border)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    /// <summary>总览图 20px 灰/黑棋盘格的预测色（图像坐标：行+列索引之和为奇 → 灰 85，偶 → 黑）。</summary>
    static Color CheckerColor(int xImg, int yImg)
    {
        var gray = ((xImg / 20) + (yImg / 20)) % 2 == 1;
        return gray ? new Color(85f / 255f, 85f / 255f, 85f / 255f) : Color.black;
    }

    static float CheckerDistance(Color c, int xImg, int yImg)
    {
        var pred = CheckerColor(xImg, yImg);
        return Mathf.Max(Mathf.Abs(c.r - pred.r), Mathf.Max(Mathf.Abs(c.g - pred.g), Mathf.Abs(c.b - pred.b)));
    }

    /// <summary>
    /// 从总览图紧致裁切一个切片并落盘：
    /// 前景判定=与棋盘预测色最大通道距离&gt;0.08；裁切框=前景紧致包围盒+4px；
    /// alpha = max(边界泛洪背景羽化, 距棋盘色的平滑渐变)——既保住深色按钮内部不透明，又让发光边缘自然渐隐。
    /// </summary>
    static void CutAndSavePiece(Texture2D sheet, string name, int x1, int y1, int x2, int y2, string outPath, Vector4 border)
    {
        int W = sheet.width, H = sheet.height;
        int sx = x1, sy = H - y2, sw = x2 - x1, sh = y2 - y1;
        if (sx < 0 || sy < 0 || sx + sw > W || sy + sh > H)
        {
            Debug.LogError($"[MainMenuBuilder] 切片 {name} 搜索区越界 ({sx},{sy},{sw},{sh})");
            return;
        }

        var src = sheet.GetPixels(sx, sy, sw, sh);

        // 紧致包围盒（前景=与棋盘预测色距离够大）
        int minX = sw, minY = sh, maxX = -1, maxY = -1;
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                if (CheckerDistance(src[y * sw + x], x1 + x, H - 1 - (sy + y)) <= 0.08f)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

        if (maxX < 0)
        {
            Debug.LogError($"[MainMenuBuilder] 切片 {name} 搜索区内无前景像素");
            return;
        }

        if (minX == 0 || minY == 0 || maxX == sw - 1 || maxY == sh - 1)
        {
            Debug.LogWarning($"[MainMenuBuilder] 切片 {name} 紧致框触及搜索区边界，若成品被裁断请扩大搜索区");
        }

        const int pad = 4;
        int cx = Mathf.Max(0, minX - pad), cy = Mathf.Max(0, minY - pad);
        int cw = Mathf.Min(sw - cx, maxX - minX + 1 + 2 * pad);
        int ch = Mathf.Min(sh - cy, maxY - minY + 1 + 2 * pad);

        var crop = new Color[cw * ch];
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                crop[y * cw + x] = src[(cy + y) * sw + (cx + x)];

        // 边界泛洪：从裁切框四边出发，只经过与棋盘高度相似的像素（0.06 内），把外部背景连通域标出
        var bg = new bool[cw * ch];
        var stack = new Stack<int>();
        void Seed(int x, int y)
        {
            int i = y * cw + x;
            if (bg[i] || CheckerDistance(crop[i], x1 + cx + x, H - 1 - (sy + cy + y)) >= 0.06f)
            {
                return;
            }

            bg[i] = true;
            stack.Push(i);
        }

        for (int x = 0; x < cw; x++)
        {
            Seed(x, 0);
            Seed(x, ch - 1);
        }

        for (int y = 0; y < ch; y++)
        {
            Seed(0, y);
            Seed(cw - 1, y);
        }

        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % cw, y = i / cw;
            if (x > 0) Seed(x - 1, y);
            if (x < cw - 1) Seed(x + 1, y);
            if (y > 0) Seed(x, y - 1);
            if (y < ch - 1) Seed(x, y + 1);
        }

        // 泛洪 alpha（背景 0 / 前景 1）做两次 3x3 盒滤波羽化
        var alpha = new float[cw * ch];
        for (int i = 0; i < alpha.Length; i++)
        {
            alpha[i] = bg[i] ? 0f : 1f;
        }

        for (var pass = 0; pass < 2; pass++)
        {
            var next = new float[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    float sum = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = Mathf.Clamp(x + dx, 0, cw - 1), ny = Mathf.Clamp(y + dy, 0, ch - 1);
                            sum += alpha[ny * cw + nx];
                        }

                    next[y * cw + x] = sum / 9f;
                }

            alpha = next;
        }

        // 与距离键控渐变取 max：边缘发光按对比度平滑渐隐，内部不透明区域不受影响
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                int i = y * cw + x;
                float d = CheckerDistance(crop[i], x1 + cx + x, H - 1 - (sy + cy + y));
                float key = Mathf.Clamp01((d - 0.05f) / 0.25f);
                crop[i].a = Mathf.Max(alpha[i], key);
            }

        // 裁切框边界 6px 渐隐：搜索区若切到相邻元素的辉光或自身被截断的辉光，避免出现硬边
        const int edgeFade = 6;
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                int i = y * cw + x;
                float edgeDist = Mathf.Min(Mathf.Min(x, cw - 1 - x), Mathf.Min(y, ch - 1 - y)) + 1f;
                crop[i].a *= Mathf.Clamp01(edgeDist / edgeFade);
            }

        var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        tex.SetPixels(crop);
        tex.Apply();
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(outPath);
        SetSpriteImport(outPath, border);
        Debug.Log($"[MainMenuBuilder] 已裁切切片 {outPath} ({cw}x{ch})");
    }

    /// <summary>
    /// 抠除设计图一个矩形区域（图像坐标）：每行取左右外侧采样做横向插值填充，
    /// 四边按 feather 像素羽化过渡，避免生硬补丁边缘。
    /// </summary>
    static void EraseRegion(Texture2D cover, int x1, int y1, int x2, int y2, int featherLeft, int featherRight, int featherTop, int featherBottom)
    {
        int W = cover.width, H = cover.height;
        int ux = x1, uy = H - y2, uw = x2 - x1, uh = y2 - y1;
        var px = cover.GetPixels(ux, uy, uw, uh);

        for (int row = 0; row < uh; row++)
        {
            var left = SampleBand(cover, Mathf.Max(0, x1 - 8), x1 - 1, uy + row);
            var right = SampleBand(cover, x2 + 1, Mathf.Min(W - 1, x2 + 8), uy + row);
            for (int col = 0; col < uw; col++)
            {
                var fill = Color.Lerp(left, right, uw > 1 ? (float)col / (uw - 1) : 0f);
                float wx = Mathf.Min((col + 0.5f) / featherLeft, (uw - col - 0.5f) / featherRight);
                float wy = Mathf.Min((uh - row - 0.5f) / featherTop, (row + 0.5f) / featherBottom);
                float blend = Mathf.Clamp01(Mathf.Min(wx, wy));
                int i = row * uw + col;
                px[i] = Color.Lerp(px[i], fill, blend);
            }
        }

        cover.SetPixels(ux, uy, uw, uh, px);
        cover.Apply();
    }

    static Color SampleBand(Texture2D tex, int xFrom, int xTo, int unityY)
    {
        var sum = Color.clear;
        int n = 0;
        for (int x = xFrom; x <= xTo; x++)
        {
            if (x < 0 || x >= tex.width || unityY < 0 || unityY >= tex.height)
            {
                continue;
            }

            sum += tex.GetPixel(x, unityY);
            n++;
        }

        return n > 0 ? sum / n : new Color(0.05f, 0.05f, 0.09f, 1f);
    }

    /// <summary>生成主菜单面板预制体（MainMenuPanel：封面背景 + 品牌区切片分层 + 右侧四按钮，设置在玩法说明与退出之间）。</summary>
    public static void BuildMainMenuPanel()
    {
        var root = new GameObject("MainMenuPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root);

        // Background：AI 抠图成品全屏铺底（封面背景.png：装饰边框、品牌区与按钮全部抠除，星空补全且接缝已羽化）
        var bgRt = CreateRect(root, "Background");
        Stretch(bgRt);
        var bgImage = bgRt.gameObject.AddComponent<Image>();
        bgImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuBackground);
        bgImage.raycastTarget = false;

        // Branding：设计图右列品牌区抠图切片（徽章→标题→EDGE GRID→分隔线→标语），居中锚点按设计比例摆放
        var brandingRt = CreateRect(root, "Branding");
        Stretch(brandingRt);
        CreatePieceImage(brandingRt, "Emblem", "emblem", new Vector2(593f, 380f), new Vector2(441f, 160f));
        CreatePieceImage(brandingRt, "Title", "title", new Vector2(540f, 266f), new Vector2(589f, 137f));
        CreatePieceImage(brandingRt, "EdgeGrid", "edgegrid", new Vector2(576f, 163f), new Vector2(503f, 34f));
        CreatePieceImage(brandingRt, "Divider", "divider", new Vector2(611f, 126f), new Vector2(367f, 10f));
        CreatePieceImage(brandingRt, "Slogan", "slogan", new Vector2(567f, 69f), new Vector2(510f, 53f));

        // Buttons 容器 + 四按钮（右缘与设计图按钮列对齐 x=-155；无字键控抠图底图 + Text 标签；锚点右下，Y 正值向上）
        var startSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BtnStartPath);
        var plaqueSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BtnPlaquePath);
        var quitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BtnQuitPath);
        var bookIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PiecesFolder + "/icon_book.png");
        var exitIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PiecesFolder + "/icon_exit.png");
        var buttonsRt = CreateRect(root, "Buttons");
        Stretch(buttonsRt);
        var newGame = CreateButton(buttonsRt, "BtnNewGame", "开始游戏", new Vector2(-155f, 502f), startSprite, new Vector2(459f, 98f), null);
        var help = CreateButton(buttonsRt, "BtnHelp", "玩法说明", new Vector2(-155f, 379f), plaqueSprite, new Vector2(441f, 70f), bookIcon);
        var settings = CreateButton(buttonsRt, "BtnSettings", "设置", new Vector2(-155f, 257f), plaqueSprite, new Vector2(441f, 70f), null);
        var quit = CreateButton(buttonsRt, "BtnQuit", "退出游戏", new Vector2(-155f, 134f), quitSprite, new Vector2(441f, 70f), exitIcon);

        var panel = root.gameObject.AddComponent<MainMenuPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("layer").intValue = (int)UILayer.Normal;
        so.FindProperty("destroyOnHide").boolValue = true;
        so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
        so.FindProperty("newGameButton").objectReferenceValue = newGame;
        so.FindProperty("helpButton").objectReferenceValue = help;
        so.FindProperty("settingsButton").objectReferenceValue = settings;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root.gameObject, PanelPrefabPath);
    }

    /// <summary>生成操作说明弹窗预制体（MenuHelpPanel：全屏蒙版 + 950×950 操作说明.png 背景 + 顶部牌匾标题 + 浅色正文 + × 关闭）。</summary>
    public static void BuildMenuHelpPanel()
    {
        var root = new GameObject("MenuHelpPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root);

        // Mask：全屏半透明蒙版，点击即关闭（Image 拦截射线 + Button 承接点击）
        var maskRt = CreateRect(root, "Mask");
        Stretch(maskRt);
        var maskImage = maskRt.gameObject.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, 0.55f);
        var maskButton = maskRt.gameObject.AddComponent<Button>();
        maskButton.targetGraphic = maskImage;

        // Content：内容卡
        var contentRt = CreateRect(root, "Content");
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(950f, 950f);

        var cardBgRt = CreateRect(contentRt, "Background");
        Stretch(cardBgRt);
        var cardBg = cardBgRt.gameObject.AddComponent<Image>();
        cardBg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelBgPath);
        cardBg.raycastTarget = false;

        var titleRt = CreateRect(contentRt, "Title");
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 257f);
        titleRt.sizeDelta = new Vector2(520f, 64f);
        var title = titleRt.gameObject.AddComponent<Text>();
        title.text = "操作说明";
        title.fontSize = 40;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color32(62, 32, 100, 255);
        title.font = GetLegacyFont();

        // BtnClose：右上角 ×
        var closeRt = CreateRect(contentRt, "BtnClose");
        closeRt.anchorMin = new Vector2(0.5f, 0.5f);
        closeRt.anchorMax = new Vector2(0.5f, 0.5f);
        closeRt.anchoredPosition = new Vector2(290f, 135f);
        closeRt.sizeDelta = new Vector2(52f, 52f);
        var closeImage = closeRt.gameObject.AddComponent<Image>();
        closeImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        closeImage.type = Image.Type.Sliced;
        var closeButton = closeRt.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;

        var closeTextRt = CreateRect(closeRt, "Text");
        Stretch(closeTextRt);
        var closeText = closeTextRt.gameObject.AddComponent<Text>();
        closeText.text = "×";
        closeText.fontSize = 30;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = new Color32(50, 50, 50, 255);
        closeText.font = GetLegacyFont();

        // Body：操作说明正文
        var bodyRt = CreateRect(contentRt, "Body");
        bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRt.anchoredPosition = new Vector2(0f, 8f);
        bodyRt.sizeDelta = new Vector2(620f, 290f);
        var body = bodyRt.gameObject.AddComponent<Text>();
        body.text = "鼠标移动：预览拼块落点\n鼠标左键：放置当前拼块\nR：旋转拼块\nZ：撤销上一次铺设\nSpace：出发，AI 开始寻路\n\n铺得越快、路线越巧，通关评价越高。";
        body.fontSize = 30;
        body.alignment = TextAnchor.UpperLeft;
        body.color = new Color32(238, 235, 228, 255);
        body.font = GetLegacyFont();

        var panel = root.gameObject.AddComponent<MenuHelpPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("layer").intValue = (int)UILayer.Popup;
        so.FindProperty("destroyOnHide").boolValue = true;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("contentText").objectReferenceValue = body;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root.gameObject, HelpPrefabPath);
    }

    /// <summary>生成设置面板预制体（MenuSettingsPanel：全屏蒙版 + 1050×1050 操作说明.png 背景：分辨率档位 / 音效音乐滑条 / 中英文语言）。</summary>
    public static void BuildMenuSettingsPanel()
    {
        var root = new GameObject("MenuSettingsPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root);

        // Mask：全屏半透明蒙版，点击即关闭
        var maskRt = CreateRect(root, "Mask");
        Stretch(maskRt);
        var maskImage = maskRt.gameObject.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, 0.55f);
        maskRt.gameObject.AddComponent<Button>();

        // Content：内容卡
        var contentRt = CreateRect(root, "Content");
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(1050f, 1050f);

        var cardBgRt = CreateRect(contentRt, "Background");
        Stretch(cardBgRt);
        var cardBg = cardBgRt.gameObject.AddComponent<Image>();
        cardBg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelBgPath);
        cardBg.raycastTarget = false;

        var title = MakeText(contentRt, "Title", "设置", new Vector2(0f, 284f), new Vector2(440f, 60f), 42, TextAnchor.MiddleCenter);
        var closeButton = MakeCenterButton(contentRt, "BtnClose", "×", new Vector2(330f, 145f), new Vector2(52f, 52f), 30, false);

        // 分辨率行：◀ 档位值 ▶
        var resRow = MakeRow(contentRt, "ResolutionRow", 145f);
        var resRowLabel = MakeText(resRow, "RowLabel", "分辨率", new Vector2(-230f, 0f), new Vector2(250f, 48f), 32, TextAnchor.MiddleRight, new Color32(235, 235, 245, 255));
        var resPrev = MakeCenterButton(resRow, "BtnPrev", "<", new Vector2(-70f, 0f), new Vector2(52f, 48f), 30, false);
        var resValue = MakeText(resRow, "Value", "1920 × 1080", new Vector2(60f, 0f), new Vector2(240f, 48f), 30, TextAnchor.MiddleCenter, new Color32(235, 235, 245, 255));
        var resNext = MakeCenterButton(resRow, "BtnNext", ">", new Vector2(195f, 0f), new Vector2(52f, 48f), 30, false);

        // 音效行（滑条 x=115，与行标签保持间隙不压字）
        var sfxRow = MakeRow(contentRt, "SfxRow", 55f);
        var sfxLabel = MakeText(sfxRow, "RowLabel", "音效", new Vector2(-230f, 0f), new Vector2(250f, 48f), 32, TextAnchor.MiddleRight, new Color32(235, 235, 245, 255));
        var sfxSlider = MakeVolumeSlider(sfxRow);

        // 音乐行
        var musicRow = MakeRow(contentRt, "MusicRow", -35f);
        var musicLabel = MakeText(musicRow, "RowLabel", "音乐", new Vector2(-230f, 0f), new Vector2(250f, 48f), 32, TextAnchor.MiddleRight, new Color32(235, 235, 245, 255));
        var musicSlider = MakeVolumeSlider(musicRow);

        // 语言行（Transition.None，选中色由 MenuSettingsPanel 运行时写 Image.color）
        var langRow = MakeRow(contentRt, "LanguageRow", -125f);
        var langLabel = MakeText(langRow, "RowLabel", "语言", new Vector2(-230f, 0f), new Vector2(250f, 48f), 32, TextAnchor.MiddleRight, new Color32(235, 235, 245, 255));
        var chineseButton = MakeCenterButton(langRow, "BtnChinese", "中文", new Vector2(-10f, 0f), new Vector2(160f, 52f), 30, true);
        var englishButton = MakeCenterButton(langRow, "BtnEnglish", "English", new Vector2(190f, 0f), new Vector2(160f, 52f), 26, true);

        var panel = root.gameObject.AddComponent<MenuSettingsPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("layer").intValue = (int)UILayer.Popup;
        so.FindProperty("destroyOnHide").boolValue = true;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("resolutionRowLabel").objectReferenceValue = resRowLabel;
        so.FindProperty("resolutionValueText").objectReferenceValue = resValue;
        so.FindProperty("sfxLabel").objectReferenceValue = sfxLabel;
        so.FindProperty("musicLabel").objectReferenceValue = musicLabel;
        so.FindProperty("languageLabel").objectReferenceValue = langLabel;
        so.FindProperty("resolutionPrevButton").objectReferenceValue = resPrev;
        so.FindProperty("resolutionNextButton").objectReferenceValue = resNext;
        so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        so.FindProperty("chineseButton").objectReferenceValue = chineseButton;
        so.FindProperty("englishButton").objectReferenceValue = englishButton;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root.gameObject, SettingsPrefabPath);
    }

    /// <summary>
    /// 生成主菜单场景（主相机 + MainMenuBoot）。
    /// Additive 新建场景后再还原激活场景，全程不关闭、不改动用户当前打开的场景。
    /// </summary>
    public static void BuildMainMenuScene()
    {
        var original = EditorSceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        try
        {
            // 显式激活新场景，保证后续 new GameObject 落在新场景内
            EditorSceneManager.SetActiveScene(scene);

            var cam = Camera.main;
            if (cam == null)
            {
                throw new System.InvalidOperationException("新建场景缺少主相机（DefaultGameObjects 应自带 Main Camera）");
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.14f, 0.15f, 0.18f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            // 主相机排除 UI(5) 与 TransparentFX(1)：UI 只由 UIRoot 的 UICamera 渲染，避免重复渲染加深蒙版
            cam.cullingMask = ~((1 << 5) | (1 << 1));
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var boot = new GameObject("MainMenuBoot");
            boot.AddComponent<MainMenuBoot>();

            EditorSceneManager.SaveScene(scene, ScenePaths.MainMenu);
        }
        finally
        {
            if (EditorSceneManager.GetActiveScene() == scene && original.IsValid())
            {
                EditorSceneManager.SetActiveScene(original);
            }

            EditorSceneManager.CloseScene(scene, true);
        }

        RegisterInBuildSettings();
        Debug.Log($"[MainMenuBuilder] 已生成 {ScenePaths.MainMenu} 并登记 Build Settings");
    }

    /// <summary>把主菜单场景登记进 Build Settings（Bootstrap 保持 index 0，MainMenu 追加）。</summary>
    static void RegisterInBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == ScenePaths.MainMenu))
        {
            return;
        }

        list.Add(new EditorBuildSettingsScene(ScenePaths.MainMenu, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>自检：产物根组件类型与关键接线逐项断言，静默失败在此变成显式错误。</summary>
    static void SelfCheck()
    {
        var failures = new List<string>();

        var panelPrefab = AssetDatabase.LoadAssetAtPath<MainMenuPanel>(PanelPrefabPath);
        if (panelPrefab == null)
        {
            failures.Add($"{PanelPrefabPath} 根组件不是 MainMenuPanel 或加载失败");
        }
        else
        {
            var so = new SerializedObject(panelPrefab);
            foreach (var field in new[] { "backgroundImage", "newGameButton", "helpButton", "settingsButton", "quitButton" })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                {
                    failures.Add($"{PanelPrefabPath} 字段 {field} 未接线");
                }
            }

            if (so.FindProperty("layer").intValue != (int)UILayer.Normal)
            {
                failures.Add($"{PanelPrefabPath} layer 应为 Normal");
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(MenuBackground) == null)
            {
                failures.Add($"{MenuBackground} 主菜单背景缺失（AI 抠图成品，勿删除或改名）");
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(BtnStartPath) == null ||
                AssetDatabase.LoadAssetAtPath<Sprite>(BtnPlaquePath) == null ||
                AssetDatabase.LoadAssetAtPath<Sprite>(BtnQuitPath) == null)
            {
                failures.Add("主菜单按钮底图（无字键控抠图成品）缺失，勿删除或改名");
            }

            foreach (var piece in new[] { "emblem", "title", "edgegrid", "divider", "slogan", "icon_book", "icon_exit" })
            {
                if (AssetDatabase.LoadAssetAtPath<Sprite>(PiecesFolder + "/" + piece + ".png") == null)
                {
                    failures.Add($"品牌切片 {piece}.png 缺失（EnsureMenuArt 应先执行）");
                }
            }
        }

        var helpPrefab = AssetDatabase.LoadAssetAtPath<MenuHelpPanel>(HelpPrefabPath);
        if (helpPrefab == null)
        {
            failures.Add($"{HelpPrefabPath} 根组件不是 MenuHelpPanel 或加载失败");
        }
        else
        {
            var so = new SerializedObject(helpPrefab);
            foreach (var field in new[] { "titleText", "contentText", "closeButton" })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                {
                    failures.Add($"{HelpPrefabPath} 字段 {field} 未接线");
                }
            }

            if (so.FindProperty("layer").intValue != (int)UILayer.Popup)
            {
                failures.Add($"{HelpPrefabPath} layer 应为 Popup");
            }
        }

        var settingsPrefab = AssetDatabase.LoadAssetAtPath<MenuSettingsPanel>(SettingsPrefabPath);
        if (settingsPrefab == null)
        {
            failures.Add($"{SettingsPrefabPath} 根组件不是 MenuSettingsPanel 或加载失败");
        }
        else
        {
            var so = new SerializedObject(settingsPrefab);
            foreach (var field in new[]
                     {
                         "titleText", "resolutionRowLabel", "resolutionValueText", "sfxLabel", "musicLabel", "languageLabel",
                         "resolutionPrevButton", "resolutionNextButton", "sfxSlider", "musicSlider",
                         "chineseButton", "englishButton", "closeButton"
                     })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                {
                    failures.Add($"{SettingsPrefabPath} 字段 {field} 未接线");
                }
            }

            if (so.FindProperty("layer").intValue != (int)UILayer.Popup)
            {
                failures.Add($"{SettingsPrefabPath} layer 应为 Popup");
            }
        }

        if (!System.Array.Exists(EditorBuildSettings.scenes, s => s.path == ScenePaths.MainMenu))
        {
            failures.Add($"Build Settings 未登记 {ScenePaths.MainMenu}");
        }

        if (EditorBuildSettings.scenes.Length == 0 || EditorBuildSettings.scenes[0].path != "Assets/Scenes/Bootstrap.unity")
        {
            failures.Add("Build Settings index 0 不是 Bootstrap.unity");
        }

        if (failures.Count > 0)
        {
            Debug.LogError("[MainMenuBuilder] 自检失败：\n" + string.Join("\n", failures));
            return;
        }

        Debug.Log("[MainMenuBuilder] 主菜单资产重建完成，自检通过（MainMenuPanel / MenuHelpPanel / MenuSettingsPanel / MainMenu.scene / Build Settings）");
    }

    static void SavePrefab(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[MainMenuBuilder] 已生成 {path}");
    }

    static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>品牌区切片图：按目标框内接（保持切片纵横比），居中锚点，不拦截射线。</summary>
    static void CreatePieceImage(Transform parent, string objName, string pieceName, Vector2 center, Vector2 targetSize)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PiecesFolder + "/" + pieceName + ".png");
        if (sprite == null)
        {
            Debug.LogError($"[MainMenuBuilder] 缺少切片 {pieceName}.png，无法布置品牌区");
            return;
        }

        var rt = CreateRect(parent, objName);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        var scale = Mathf.Min(targetSize.x / sprite.rect.width, targetSize.y / sprite.rect.height);
        rt.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
    }

    /// <summary>主菜单按钮（设计图底图 + 白色居中文字 + 可选左侧图标），右下锚点，尺寸由调用方指定。</summary>
    static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Sprite baseSprite, Vector2 size, Sprite iconSprite)
    {
        var rt = CreateRect(parent, name);
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = baseSprite;
        image.type = Image.Type.Sliced;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        if (iconSprite != null)
        {
            var iconRt = CreateRect(rt, "Icon");
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(26f, 0f);
            iconRt.sizeDelta = new Vector2(46f, 46f);
            var iconImage = iconRt.gameObject.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }

        var labelRt = CreateRect(rt, "Text");
        Stretch(labelRt);
        var text = labelRt.gameObject.AddComponent<Text>();
        text.text = label;
        text.fontSize = 32;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = GetLegacyFont();
        return button;
    }

    /// <summary>设置面板行容器：居中锚点，760×64。</summary>
    static RectTransform MakeRow(Transform parent, string name, float y)
    {
        var rt = CreateRect(parent, name);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(760f, 64f);
        return rt;
    }

    /// <summary>设置面板文本：居中锚点，深灰，不拦截射线。</summary>
    static Text MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color32? color = null)
    {
        var rt = CreateRect(parent, name);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color ?? new Color32(50, 50, 50, 255);
        t.raycastTarget = false;
        t.font = GetLegacyFont();
        return t;
    }

    /// <summary>设置面板居中锚点按钮（transitionNone 时取消 ColorTint，由 Image.color 承载选中色）。</summary>
    static Button MakeCenterButton(Transform parent, string name, string labelText, Vector2 pos, Vector2 size, int fontSize, bool transitionNone)
    {
        var rt = CreateRect(parent, name);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (transitionNone)
        {
            button.transition = Selectable.Transition.None;
        }

        var labelRt = CreateRect(rt, "Text");
        Stretch(labelRt);
        var label = labelRt.gameObject.AddComponent<Text>();
        label.text = labelText;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color32(50, 50, 50, 255);
        label.font = GetLegacyFont();
        return button;
    }

    /// <summary>音量滑条：居中锚点 x=115、420×28，LeftToRight 0~1。</summary>
    static Slider MakeVolumeSlider(Transform row)
    {
        var sliderRt = CreateRect(row, "Slider");
        sliderRt.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRt.anchoredPosition = new Vector2(115f, 0f);
        sliderRt.sizeDelta = new Vector2(420f, 28f);

        var bgRt = CreateRect(sliderRt, "Background");
        Stretch(bgRt);
        var bg = bgRt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.78f, 0.78f, 0.80f, 1f);

        var fillAreaRt = CreateRect(sliderRt, "Fill Area");
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = new Vector2(6f, 0f);
        fillAreaRt.offsetMax = new Vector2(-6f, 0f);
        var fillRt = CreateRect(fillAreaRt, "Fill");
        Stretch(fillRt);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.color = new Color(0.45f, 0.65f, 0.95f, 1f);
        fill.raycastTarget = false;

        var handleAreaRt = CreateRect(sliderRt, "Handle Slide Area");
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(6f, -14f);
        handleAreaRt.offsetMax = new Vector2(-6f, 14f);
        var handleRt = CreateRect(handleAreaRt, "Handle");
        handleRt.sizeDelta = new Vector2(26f, 40f);
        var handleImage = handleRt.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        var slider = sliderRt.gameObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImage;
        return slider;
    }

    static Font GetLegacyFont()
    {
        try
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (System.Exception)
        {
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
