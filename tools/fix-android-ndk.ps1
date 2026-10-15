# 团结引擎 Android NDK 检查/修复脚本（每台要打安卓包的机器跑一次）
#
# 背景：tuanjie-cli 安装 android 模块时，NDK 经常出两种问题，而且报错信息一样（都是
#       "Android NDK not found"），实际原因完全不同：
#         (1) 报安装成功、但 NDK 目录其实是空的（模块排队后没真装上）
#         (2) NDK 被装深了一层：NDK\android-ndk-rXX\，而引擎只认 NDK\ 根目录
#             （判定标志：NDK\source.properties 是否存在）
#       本脚本会检查并自动修复第 (2) 种；第 (1) 种会提示你该跑哪条命令。
#
# 用法：右键本文件 → 使用 PowerShell 运行
# ★ 本文件必须保持 UTF-8 with BOM 保存：Windows PowerShell 5.1 读无 BOM 的 UTF-8
#   会按 GBK 解码，中文注释变乱码并导致语法错误。

$ErrorActionPreference = 'Continue'

function Get-EngineRoots {
    $roots = @()
    try {
        $p = (& tuanjie.exe install-path --get 2>$null | Select-Object -First 1)
        if ($p -and (Test-Path $p)) { $roots += $p.Trim() }
    } catch { }
    foreach ($d in @('C:\Program Files\Tuanjie\Hub\Editor', 'D:\develop\Unity\TuanjieEngine')) {
        if (Test-Path $d) { $roots += $d }
    }
    $roots | Select-Object -Unique
}

$roots = Get-EngineRoots
if (-not $roots) {
    Write-Host "[X] 找不到团结引擎安装目录。" -ForegroundColor Red
    Write-Host "    请手动传入路径，例如： .\fix-android-ndk.ps1 -EngineRoot 'D:\develop\Unity\TuanjieEngine'"
    exit 1
}

$fixedCount = 0
$problemList = @()

foreach ($root in $roots) {
    foreach ($ver in (Get-ChildItem $root -Directory -ErrorAction SilentlyContinue)) {
        $androidPlayer = Join-Path $ver.FullName 'Editor\Data\PlaybackEngines\AndroidPlayer'
        if (-not (Test-Path $androidPlayer)) { continue }

        $ndk = Join-Path $androidPlayer 'NDK'
        Write-Host ""
        Write-Host ("检查引擎 {0}  ({1})" -f $ver.Name, $root) -ForegroundColor Cyan

        # 情况 A：没有 NDK 目录
        if (-not (Test-Path $ndk)) {
            Write-Host "  [X] 未安装 NDK" -ForegroundColor Yellow
            Write-Host "      → 请运行：tuanjie.exe install-modules <引擎公开版本，如 1.10.4> --module android-ndk" -ForegroundColor Yellow
            $problemList += ("{0}: 未安装 NDK" -f $ver.Name)
            continue
        }

        # 情况 B：目录存在但是空的
        $inner = @(Get-ChildItem $ndk -Force -ErrorAction SilentlyContinue)
        if ($inner.Count -eq 0) {
            Write-Host "  [X] NDK 目录是空的（说明当初安装并未真正完成）" -ForegroundColor Yellow
            Write-Host "      → 请重新运行：tuanjie.exe install-modules <引擎公开版本，如 1.10.4> --module android-ndk" -ForegroundColor Yellow
            $problemList += ("{0}: NDK 目录为空" -f $ver.Name)
            continue
        }

        # 情况 C：位置正确
        if (Test-Path (Join-Path $ndk 'source.properties')) {
            $rev = (Select-String -Path (Join-Path $ndk 'source.properties') -Pattern 'Pkg.Revision' |
                    Select-Object -First 1).Line
            Write-Host ("  [OK] NDK 位置正确  {0}" -f ($rev -replace '\s+', ' ')) -ForegroundColor Green
            continue
        }

        # 情况 D：装深了一层，自动扁平化
        $nested = $inner |
                  Where-Object { $_.PSIsContainer -and (Test-Path (Join-Path $_.FullName 'source.properties')) } |
                  Select-Object -First 1

        if (-not $nested) {
            Write-Host "  [X] 目录结构无法识别，请手动检查：" -ForegroundColor Red
            Write-Host ("      {0}" -f $ndk) -ForegroundColor Red
            $problemList += ("{0}: NDK 结构无法识别" -f $ver.Name)
            continue
        }

        Write-Host ("  [!] NDK 被装深了一层：NDK\{0}\ → 正在提高一级" -f $nested.Name) -ForegroundColor Yellow
        try {
            $tmpName = 'NDK__flatten_tmp'
            $tmpPath = Join-Path $androidPlayer $tmpName
            if (Test-Path $tmpPath) { Remove-Item -LiteralPath $tmpPath -Recurse -Force }
            Rename-Item -LiteralPath $ndk -NewName $tmpName -ErrorAction Stop
            Move-Item -LiteralPath (Join-Path $tmpPath $nested.Name) -Destination $ndk -ErrorAction Stop
            Remove-Item -LiteralPath $tmpPath -Recurse -Force -ErrorAction SilentlyContinue

            if (Test-Path (Join-Path $ndk 'source.properties')) {
                Write-Host "  [OK] 修复完成" -ForegroundColor Green
                $fixedCount++
            } else {
                Write-Host "  [X] 扁平化后仍找不到 source.properties" -ForegroundColor Red
                $problemList += ("{0}: 扁平化失败" -f $ver.Name)
            }
        } catch {
            Write-Host ("  [X] 修复失败（可能有编辑器正在运行占用了文件）：{0}" -f $_.Exception.Message) -ForegroundColor Red
            $problemList += ("{0}: 修复失败" -f $ver.Name)
        }
    }
}

Write-Host ""
if ($problemList.Count -eq 0) {
    Write-Host ("全部检查通过（本次修复 {0} 处）。" -f $fixedCount) -ForegroundColor Green
} else {
    Write-Host ("发现 {0} 处需要处理：" -f $problemList.Count) -ForegroundColor Yellow
    foreach ($item in $problemList) { Write-Host ("  - " + $item) }
}
