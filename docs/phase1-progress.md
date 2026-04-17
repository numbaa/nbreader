# Phase 1 进度记录（本地导入闭环）

## 1. 使用说明

1. 本文件用于持续记录 Phase 1 完成进度。
2. 每次开发结束前必须更新：当前状态、下一步、风险/阻塞。
3. 状态约定：`todo`、`doing`、`done`、`blocked`。

## 2. 当前阶段概览

- 当前里程碑：M2（结构分析与 ImportPlan）
- 总体状态：doing
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

- [ ] awaiting_confirmation 触发条件
- [ ] 用户确认覆盖规则
- [ ] 确认结果回写 ImportPlan
- [ ] 确认事件持久化

### M4：事务写库与幂等

- [ ] Source、Series、Volume、Page 写库链路
- [ ] importing 阶段事务提交与回滚
- [ ] 重复导入幂等策略
- [ ] 导入结果摘要记录

### M5：异常路径与稳定性收口

- [ ] 错误码与错误信息映射
- [ ] 关键日志字段与阶段耗时
- [ ] 回归样本集与回归清单
- [ ] Phase 1 验收场景全量回归

## 4. 测试清单（复杂/高变动功能必测）

- [x] 路径标准化与幂等键：单元测试（2026-04-18）
- [x] 图片筛选与自然排序：单元测试（2026-04-18）
- [x] 结构识别与 ImportPlan：单元测试（2026-04-18）
- [ ] 冲突检测与 awaiting_confirmation：单元测试
- [ ] importing 事务行为：集成测试（SQLite 临时库）
- [ ] 4.4 验收场景：场景测试

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

## 6. 下一步

1. 开始 M3：实现 awaiting_confirmation 触发条件与状态切换。
2. 实现用户确认覆盖模型（系列名、卷号、重复策略）并回写 ImportPlan。
3. 补充冲突检测与 awaiting_confirmation 的专项单元测试。

## 7. 风险与阻塞

- 风险：现有数据库模型尚未包含 Series 和导入任务表，M4 之前需要先完成 schema 演进。
- 风险：目录识别规则在真实资源上可能频繁调整，需要先固定样本集。
- 阻塞：暂无。
