# Phase 1 进度记录（本地导入闭环）

## 1. 使用说明

1. 本文件用于持续记录 Phase 1 完成进度。
2. 每次开发结束前必须更新：当前状态、下一步、风险/阻塞。
3. 状态约定：`todo`、`doing`、`done`、`blocked`。

## 2. 当前阶段概览

- 当前里程碑：Phase 1 收尾
- 总体状态：done
- 最近更新：2026-04-18

## 3. 里程碑清单

### M1：导入任务骨架与路径识别

- [x] 定义 ImportTask 最小字段与状态迁移（2026-04-18）
- [x] 定义 ImportTaskEvent 最小字段与事件记录点（2026-04-18）
- [x] 实现路径标准化与 normalized locator（2026-04-18）
- [x] 输入类型识别（zip / 单目录 / 系列目录）（2026-04-18）
- [x] 导入入口编排服务（创建任务 -> scanning）（2026-04-18）

### M2：结构分析与 ImportPlan

- [x] 图片文件筛选规则（2026-04-18）
- [x] 自然排序规则统一（2026-04-18）
- [x] 系列名与卷号候选提取（2026-04-18）
- [x] ImportPlan 模型与构建器（2026-04-18）
- [x] warning / conflict 生成（2026-04-18）

### M3：用户确认与冲突处理

- [x] awaiting_confirmation 触发条件（2026-04-18）
- [x] 用户确认覆盖规则（2026-04-18）
- [x] 确认结果回写 ImportPlan（2026-04-18）
- [x] 确认事件持久化（2026-04-18）

### M4：事务写库与幂等

- [x] Source、Series、Volume、Page 写库链路（2026-04-18）
- [x] importing 阶段事务提交与回滚（2026-04-18）
- [x] 重复导入幂等策略（2026-04-18）
- [x] 导入结果摘要记录（2026-04-18）

### M5：异常路径与稳定性收口

- [x] 错误码与错误信息映射（2026-04-18）
- [x] 关键日志字段与阶段耗时（2026-04-18）
- [x] 回归样本集与回归清单（2026-04-18）
- [x] Phase 1 验收场景全量回归（2026-04-18）

## 4. 测试清单（复杂/高变动功能必测）

- [x] 路径标准化与幂等键：单元测试（2026-04-18）
- [x] 图片筛选与自然排序：单元测试（2026-04-18）
- [x] 结构识别与 ImportPlan：单元测试（2026-04-18）
- [x] 冲突检测与 awaiting_confirmation：单元测试（2026-04-18）
- [x] importing 事务行为：集成测试（SQLite 临时库）（2026-04-18）
- [x] 4.4 验收场景：场景测试（2026-04-18）

## 5. 验证记录

### 2026-04-18

- 完成项：Phase 1 计划拆解与进度落盘框架建立。
- 验证结果：文档已建立，尚未进入代码实现。
- 发现问题：无。

### 2026-04-18（第 2 次）

- 完成项：M1 全量代码骨架（ImportTask、ImportTaskEvent、PathNormalizer、InputTypeDetector、ImportOrchestrator）与 SQLite 任务存储。
- 完成项：新增测试项目并补齐路径标准化、输入类型识别、Orchestrator 去重与事件流测试。
- 验证结果：`dotnet test NbReader.sln` 通过（6/6）；`dotnet build NbReader.sln -c Debug` 通过。
- 发现问题：无。

### 2026-04-18（第 3 次）

- 完成项：M2 结构分析主链路（目录/zip 图片筛选、统一自然排序、ImportPlan 构建、系列名与卷号候选、warning/conflict、requires_confirmation）。
- 完成项：ImportOrchestrator 接入 analyzing 阶段并输出 ImportPlan。
- 完成项：新增 M2 测试（图片枚举排序、ImportPlan 冲突/混合目录判断、orchestrator analyzing 事件）。
- 验证结果：`dotnet test NbReader.sln` 通过（11/11）；`dotnet build NbReader.sln -c Debug` 通过。
- 发现问题：无。

### 2026-04-18（第 4 次）

- 完成项：M3 状态流落地（Analyze 后按条件进入 `awaiting_confirmation`；确认后进入 `importing`）。
- 完成项：用户确认覆盖模型落地（系列名覆盖、卷显示名/卷号覆盖、跳过重复卷、忽略 warning）。
- 完成项：新增确认流程测试与冲突场景测试，并修复 1 个可空性编译警告（CS8631）。
- 验证结果：`dotnet test NbReader.sln` 通过（15/15）；`dotnet build NbReader.sln -c Debug` 通过，且无新增警告。
- 发现问题：无。

### 2026-04-18（第 5 次）

- 完成项：M4 导入写库服务（Source/Series/Volume/Page 事务写入、幂等 upsert、结果摘要）。
- 完成项：M4 集成测试（重复导入幂等、事务回滚、未确认计划拦截）。
- 完成项：M5 错误码消息映射与 importing 阶段关键日志字段（task_id/stage/elapsed_ms/error_code）。
- 完成项：新增回归样本与清单文档 `import-regression-checklist.md`。
- 验证结果：`dotnet test NbReader.sln` 通过（18/18）；`dotnet build NbReader.sln -c Debug` 通过（0 warning）。
- 发现问题：暂无功能阻塞，尚需完成 4.4 验收场景全量场景测试。

### 2026-04-18（第 6 次）

- 完成项：新增 `Phase1AcceptanceScenarioTests`，覆盖 4.4 的 5 条验收场景。
- 完成项：修复混合目录识别（含子目录目录优先判定为 `SeriesDirectory`），确保可进入 `awaiting_confirmation`。
- 验证结果：`dotnet test NbReader.sln` 通过（23/23）；`dotnet build NbReader.sln -c Debug` 通过（0 warning）。
- 发现问题：Phase 1 目标范围内暂无阻塞。

## 6. 下一步

1. 进入 Phase 2，开始书架与阅读闭环的最小骨架实现。
2. 在导入链路增加基于真实样本库的离线回归任务（不落盘隐私路径）。
3. 将 Phase 1 场景测试纳入后续 CI 流程。

## 7. 风险与阻塞

- 风险：现有数据库模型尚未包含 Series 和导入任务表，M4 之前需要先完成 schema 演进。
- 风险：目录识别规则在真实资源上可能频繁调整，需要先固定样本集。
- 阻塞：暂无。