# 团结项目 · Assets/Art 资源同步（U 盘 <-> 工程）
#
# 为什么要有这个脚本：资源同步靠手工拷贝时，最容易犯两个错 ——
#   ① 漏拷 .meta（后果：所有人 Prefab 引用断链，极难补救）
#   ② 开着引擎拷贝（后果：文件被锁、拷到半成品）
# 这个脚本会把这两条变成强制检查。
#
# 用法（在工程目录下）：
#   推送到 U 盘：  .\tools\sync-art.ps1 -Direction Push -UsbRoot "F:\2DProject_资产"
#   从 U 盘拉取：  .\tools\sync-art.ps1 -Direction Pull -UsbRoot "F:\2DProject_资产"
#   （也可以右键本文件 → 使用 PowerShell 运行，然后按提示输入）
#
# ★ 本文件必须保持 UTF-8 with BOM 保存：Windows PowerShell 5.1 读无 BOM 的 UTF-8
#   会按 GBK 解码，中文注释变乱码并导致语法错误。

param(
    [ValidateSet('Push', 'Pull')][string]$Direction,
    [string]$UsbRoot
)

$ErrorActionPreference = 'Stop'

# 工程根 = tools 的上一级
if (-not $PSScriptRoot) { $PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path }
$ProjectRoot = Split-Path -Parent $PSScriptRoot

# 没给参数就交互式问
if (-not $Direction) {
    Write-Host "同步方向： 1 = 推送到 U 盘（工程 -> U 盘）   2 = 从 U 盘拉取（U 盘 -> 工程）" -ForegroundColor Cyan
    $sel = Read-Host "输入 1 或 2"
    if ($sel -eq '1') { $Direction = 'Push' } elseif ($sel -eq '2') { $Direction = 'Pull' } else { Write-Host "[X] 无效输入" -ForegroundColor Red; exit 1 }
}
if (-not $UsbRoot) {
    Write-Host "例：F:\2DProject_资产" -ForegroundColor Cyan
    $UsbRoot = Read-Host "输入 U 盘上的资源目录（含 Art 文件夹的那一层）"
}
$UsbRoot = $UsbRoot.Trim('"', ' ')

# ---------- 检查 1：引擎必须关闭 ----------
$proc = Get-Process -Name Tuanjie -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "[X] 团结引擎正在运行，先把它关掉再同步（否则文件会被锁、可能拷到半成品）。" -ForegroundColor Red
    $proc | ForEach-Object { Write-Host ("     运行中的进程 PID: " + $_.Id) }
    exit 1
}
Write-Host "[OK] 引擎未运行" -ForegroundColor Green

# ---------- 定位源与目标 ----------
$artRel      = 'Assets\Art'
$artMetaRel  = 'Assets\Art.meta'

if ($Direction -eq 'Push') {
    $srcArt  = Join-Path $ProjectRoot $artRel
    $srcMeta = Join-Path $ProjectRoot $artMetaRel
    $dstArt  = Join-Path $UsbRoot 'Art'
    $dstMeta = Join-Path $UsbRoot 'Art.meta'
} else {
    $srcArt  = Join-Path $UsbRoot 'Art'
    $srcMeta = Join-Path $UsbRoot 'Art.meta'
    $dstArt  = Join-Path $ProjectRoot $artRel
    $dstMeta = Join-Path $ProjectRoot $artMetaRel
}

if (-not (Test-Path $srcArt)) { Write-Host ("[X] 源目录不存在: " + $srcArt) -ForegroundColor Red; exit 1 }

# ---------- 检查 2：每个资源都必须有配对的 .meta ----------
$resourceExt = 'png|jpg|jpeg|psd|tga|tif|bmp|exr|hdr|gif|wav|mp3|ogg|aif|aiff|flac|m4a|ttf|otf|mp4|mov|webm'
$res = @(Get-ChildItem $srcArt -Recurse -File -ErrorAction SilentlyContinue |
         Where-Object { $_.Extension -match ('^\.(' + $resourceExt + ')$') })
$missing = @()
foreach ($f in $res) {
    if (-not (Test-Path ($f.FullName + '.meta'))) { $missing += $f.FullName.Replace($srcArt, 'Art') }
}
if ($missing.Count -gt 0) {
    Write-Host "[X] 有资源文件缺少配对的 .meta（不能同步，否则别人引用会断）：" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host ("     " + $_) }
    Write-Host "    处理：在引擎里选中该文件让它生成 .meta，或从 U 盘/对方那台把缺失的 .meta 补过来。" -ForegroundColor Yellow
    exit 1
}
Write-Host ("[OK] .meta 配对检查通过（资源文件 " + $res.Count + " 个）") -ForegroundColor Green

# ---------- 同步 ----------
Write-Host ""
Write-Host ("同步方向: " + $Direction + "   源: " + $srcArt + "   ->   目标: " + $dstArt) -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $dstArt | Out-Null
Copy-Item -LiteralPath $srcArt -Destination (Split-Path -Parent $dstArt) -Recurse -Force
if (Test-Path $srcMeta) { Copy-Item -LiteralPath $srcMeta -Destination (Split-Path -Parent $dstMeta) -Force }
if (Test-Path $dstMeta) { Write-Host "[OK] Art.meta 已同步" -ForegroundColor Green }
else { Write-Host "[!] 目标处没有 Art.meta（引擎下次打开会自己生成，不影响使用）" -ForegroundColor Yellow }

# ---------- 报告 ----------
$files = @(Get-ChildItem $dstArt -Recurse -File -Force -ErrorAction SilentlyContinue)
$sum = ($files | Measure-Object Length -Sum).Sum
Write-Host ""
Write-Host ("同步完成：目标下共 " + $files.Count + " 个文件，合计 " + ('{0:N1} KB' -f ($sum / 1KB))) -ForegroundColor Green
$files | Sort-Object FullName | Select-Object -First 20 | ForEach-Object { Write-Host ("    " + $_.FullName.Replace((Split-Path -Parent $dstArt), '')) }
if ($files.Count -gt 20) { Write-Host ("    ...（其余 " + ($files.Count - 20) + " 个省略）") }

Write-Host ""
Write-Host "别忘了：另一边（拿到资源的人）也要先关引擎再打开工程。" -ForegroundColor Cyan
