# 注册团结引擎 SmartMerge 合并驱动（每台机器只需运行一次）
# 作用：让 git 在合并 .unity/.scene/.prefab/.asset/.meta 冲突时，调用 TuanjieYAMLMerge 按对象智能合并，
#       而不是按文本行合并（后者对场景/Prefab 几乎无法人工解决）。
# 用法：右键本文件 → 使用 PowerShell 运行。

$ErrorActionPreference = 'Stop'

$hubEditor = 'C:\Program Files\Tuanjie\Hub\Editor'
$found = @()

if (Test-Path $hubEditor) {
    $found += Get-ChildItem $hubEditor -Directory |
              ForEach-Object { Join-Path $_.FullName 'Editor\Data\Tools\TuanjieYAMLMerge.exe' } |
              Where-Object { Test-Path $_ }
}

$merge = $found | Select-Object -Last 1

if (-not $merge) {
    Write-Error "未找到 TuanjieYAMLMerge.exe，请确认团结引擎已安装（默认查找路径：$hubEditor）"
    exit 1
}

$merge = $merge -replace '\\', '/'
# 注意：Windows PowerShell 5.1 向原生 exe(git) 传参时不会转义参数内层的引号，
# 必须手写成 \" 才能让 git 收到完整路径；写成普通 " 会被空格截断（例如只剩 "C:/Program"）。
$driver = '\"' + $merge + '\" merge -p %O %A %B %A'

git config --global merge.unityyamlmerge.name 'Tuanjie SmartMerge'
git config --global merge.unityyamlmerge.driver $driver
git config --global merge.unityyamlmerge.recursive binary

Write-Host "已配置 SmartMerge 驱动: $merge"
Write-Host "当前 driver 配置: " -NoNewline
git config --global --get merge.unityyamlmerge.driver
