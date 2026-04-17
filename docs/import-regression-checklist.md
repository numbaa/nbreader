# Import 回归样本与清单（Phase 1）

## 1. 样本集

1. 单卷 zip（连续页号）
2. 单卷目录（自然排序页号）
3. 系列目录（规范卷命名）
4. 根目录混合图片和子目录（应进入 awaiting_confirmation）
5. 重复卷号系列目录（应产生 conflict）
6. 无有效图片目录（应返回明确错误）

## 2. 回归清单

1. 重复导入同一输入：不产生重复 Source/Series/Volume，Page 行数稳定。
2. analyzing 阶段遇到 mixed_directory_layout：进入 awaiting_confirmation。
3. 用户确认覆盖系列名/卷号后：ImportPlan 更新并进入 importing。
4. importing 事务中途失败：Source/Series/Volume/Page 全部回滚。
5. 导入完成日志：包含 task_id、stage、elapsed_ms、volumes、pages。
6. 导入失败日志：包含 task_id、stage、elapsed_ms、error_code 和异常详情。

## 3. 执行建议

1. 每次修改导入规则后，至少执行第 2 节全部回归项。
2. 每次修复导入缺陷，新增对应自动化测试并记录到 Phase 1 进度文件。
3. 发布前在三平台各跑一次第 2 节回归清单。
