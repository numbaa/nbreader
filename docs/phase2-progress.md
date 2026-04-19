# Phase 2 进度记录（书架与阅读闭环）

## 1. 使用说明

1. 本文件用于持续记录 Phase 2 完成进度。
2. 每次开发结束前必须更新：当前状态、下一步、风险/阻塞。
3. 状态约定：`todo`、`doing`、`done`、`blocked`。

## 2. 当前阶段概览

- 当前里程碑：M5 已完成
- 总体状态：done
- 最近更新：2026-04-20

## 3. 里程碑清单

### M1：主界面导航框架与系列聚合查询

- [x] 建立主界面导航结构（2026-04-18）
- [x] 实现系列聚合查询模型（2026-04-18）
- [x] 系列卡片列表最小交互（2026-04-18）

### M2：系列详情、卷打开与统一页源抽象

- [x] 实现系列详情页与卷列表（2026-04-18）
- [x] 打通卷打开进入阅读器链路（2026-04-18）
- [x] 建立统一页源抽象第一版（2026-04-18）

### M3：单页阅读、ReaderState 与基础预加载

- [x] 实现单页阅读基础能力（2026-04-18）
- [x] 定义 ReaderState 与状态迁移（2026-04-18）
- [x] 实现邻近页预加载与释放策略（2026-04-18）

### M4：双页模式、方向切换与配对规则

- [x] 实现双页模式与模式切换（2026-04-18）
- [x] 实现阅读方向切换与映射（2026-04-18）
- [x] 实现封面单页与双页配对规则（2026-04-18）

### M5：进度持久化、最近阅读与继续阅读

- [x] 实现 ReadingProgress 持久化（2026-04-18）
- [x] 实现进度写回策略（事件触发 + 节流）（2026-04-18）
- [x] 实现最近阅读与继续阅读视图（2026-04-18）
- [x] 实现卷尾进入下一卷逻辑（2026-04-18）

## 4. 测试清单（复杂/高变动功能必测）

- [x] 系列聚合查询与排序规则：单元测试（2026-04-20）
- [x] 统一页源一致性：单元测试（2026-04-20）
- [x] 双页配对与阅读方向：单元测试（2026-04-18）
- [x] ReaderState 状态迁移与切卷：集成测试（2026-04-20）
- [x] ReadingProgress 写回节流与恢复：集成测试（2026-04-18）
- [x] 5.4 验收场景：场景测试（2026-04-20）

## 5. 验证记录

### 2026-04-18

- 完成项：Phase 2 计划已建立，进入 M1。
- 验证结果：文档与进度框架已落盘。
- 发现问题：暂无。

### 2026-04-18（第 2 次）

- 完成项：实现 M1 导航框架（Catalog/Import/Reader/Search/Metadata/Infrastructure）与右侧内容区切换。
- 完成项：新增系列聚合查询服务 `SeriesQueryService`，按最近更新时间降序读取系列卡片数据。
- 完成项：主窗口接入系列卡片列表加载与选择反馈，形成系列浏览最小交互闭环。
- 验证结果：`dotnet build NbReader.sln -c Debug` 通过（0 warning）；`dotnet test tests/NbReader.Import.Tests/NbReader.Import.Tests.csproj` 通过（23/23）。
- 发现问题：无阻塞；后续需在 M2 补齐系列详情与卷打开链路。

### 2026-04-18（第 3 次）

- 完成项：新增卷查询服务 `VolumeQueryService`，支持按系列读取卷列表并聚合页数。
- 完成项：Catalog 视图升级为“系列卡片 + 系列详情（卷列表）”双栏布局。
- 完成项：打通“系列 -> 卷 -> 打开卷”入口，进入 Reader 占位视图并带出当前卷摘要。
- 验证结果：`dotnet build NbReader.sln -c Debug` 通过（0 warning）；`dotnet test tests/NbReader.Import.Tests/NbReader.Import.Tests.csproj` 通过（23/23）。
- 发现问题：无阻塞；统一页源抽象已在下一次迭代完成。

### 2026-04-18（第 4 次）

- 完成项：新增统一页源读取类 `UnifiedVolumePageSource`，支持目录卷与 zip 卷统一读取页面流。
- 完成项：新增卷读取上下文查询（`VolumeReaderContext`），打通 `source_path + page_locator` 的首屏读取链路。
- 完成项：Reader 入口升级为真实首屏渲染，打开卷后可显示第一页图像。
- 验证结果：`dotnet build NbReader.sln -c Debug` 通过（0 warning）；`dotnet test tests/NbReader.Import.Tests/NbReader.Import.Tests.csproj` 通过（23/23）。
- 发现问题：暂无阻塞；下一步进入 M3（单页阅读、ReaderState、基础预加载）。

### 2026-04-18（第 5 次）

- 完成项：实现 ReaderState（`ReaderLifecycle + ReaderStateMachine`）并接入阅读状态迁移（VolumeReady -> PageLoading -> PageReady / Error）。
- 完成项：Reader 区域升级为单页阅读交互，支持上一页/下一页、页码与状态展示。
- 完成项：实现邻近页预加载窗口（半径 1）与缓存释放策略，避免缓存无限增长。
- 完成项：新增 `ReaderStateMachineTests`，覆盖状态迁移与预加载窗口策略。
- 验证结果：`dotnet build NbReader.sln -c Debug` 通过（0 warning）；`dotnet test tests/NbReader.Import.Tests/NbReader.Import.Tests.csproj` 通过（27/27）。
- 发现问题：暂无阻塞；下一步进入 M4（双页模式、方向切换与配对规则）。

### 2026-04-18（第 6 次）

- 完成项：Reader 区域支持单双页模式切换，新增双页渲染布局与模式状态展示。
- 完成项：支持阅读方向（从左到右 / 从右到左）切换，并将上一页/下一页导航映射到对应方向。
- 完成项：新增 `ReaderSpreadRules`，固化封面单页 + 后续双页配对规则，并接入阅读入口初始页计算。
- 完成项：新增 `ReaderSpreadRulesTests`，覆盖双页配对、方向左右页映射与 RTL 初始定位规则。
- 验证结果：`dotnet build NbReader.sln -c Debug` 通过（0 warning）；`dotnet test tests/NbReader.Import.Tests/NbReader.Import.Tests.csproj` 通过（32/32）。
- 发现问题：暂无阻塞；下一步进入 M5（进度持久化、最近阅读与继续阅读）。

### 2026-04-18（第 7 次）

- 完成项：新增 `reading_progress` 表与索引，接入 `ReadingProgressService`，支持进度读写与最近阅读查询。
- 完成项：Reader 读写策略升级为“事件触发 + 节流 + 退出强制落盘”，并恢复阅读模式/方向与页码。
- 完成项：主界面新增继续阅读与最近阅读入口，支持从记录恢复打开；卷尾自动尝试打开同系列下一卷。
- 完成项：新增 `ReadingProgressIntegrationTests`，覆盖进度持久化、最大阅读页保持、最近阅读排序与下一卷查询。
- 验证结果：`dotnet build NbReader.sln -c Debug` 通过（0 warning）；`dotnet test tests/NbReader.Import.Tests/NbReader.Import.Tests.csproj` 通过（35/35）。
- 发现问题：暂无阻塞；M5 已完成，后续进入测试清单收口与 5.4 验收场景。

### 2026-04-20

- 完成项：新增 `SeriesQueryServiceTests`，覆盖系列聚合数量统计、最近更新时间排序与 limit 生效规则。
- 完成项：新增 `UnifiedVolumePageSourceTests`，覆盖绝对路径、目录相对路径、zip 条目（含大小写回退）与缺失页返回空流场景。
- 验证结果：`dotnet test tests/NbReader.Import.Tests/NbReader.Import.Tests.csproj` 通过（41/41）。
- 发现问题：暂无阻塞；测试清单剩余 ReaderState 切卷集成测试与 5.4 验收场景。

### 2026-04-20（第 2 次）

- 完成项：新增 `ReaderStateIntegrationTests`，覆盖打开卷后的状态迁移（VolumeReady -> PageLoading -> PageReady）与卷尾切到下一卷链路。
- 完成项：新增 `Phase2AcceptanceScenarioTests`，覆盖 5.4 的 5 条验收场景（进入卷并翻页、关闭恢复页码、双页方向切换、卷尾进入下一卷、最近阅读与继续阅读可用）。
- 验证结果：`dotnet test tests/NbReader.Import.Tests/NbReader.Import.Tests.csproj` 通过（48/48）。
- 发现问题：暂无阻塞；Phase 2 测试清单已全部完成。

## 6. 下一步

1. 根据真实数据样本复核双页配对与进度恢复边界行为。
2. 准备进入下一阶段里程碑规划与任务拆分。

## 7. 风险与阻塞

- 风险：双页配对规则可能在真实资源中出现边界差异，需要尽早固化样本集。
- 风险：大图预加载窗口如果过大，可能触发内存抖动。
- 阻塞：暂无。
