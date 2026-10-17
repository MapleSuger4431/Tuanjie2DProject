# Region Semantic Enrichment

根据面板用途、区域名称、描述和父子关系补全缺失语义。不得修改 `rect`、`parentId` 或用户显式填写的字段。

仅输出建议字段：

- `componentType`

优先使用画板标准值：`custom`、`container`、`background`、`Button`、`Label`、`Image`、`TextField`、`ListView`、`ScrollView`、`grid`、`Slider`、`ProgressBar`、`Toggle`。无法确定时保留 `custom`，不要创建近义拼写。
