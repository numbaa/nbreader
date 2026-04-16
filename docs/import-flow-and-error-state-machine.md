# 导入流程与错误处理状态机

## 1. 文档目标

本文档定义漫画导入模块的工作方式，覆盖：

1. 本地路径导入
2. 批量导入
3. URL 导入
4. 错误分类
5. 重试与恢复策略
6. 与数据库表的协作关系

导入模块的目标不是“尽量自动猜对一切”，而是：

1. 尽量自动识别
2. 在不确定时停下来让用户确认
3. 保证导入幂等
4. 出错后可诊断、可恢复、可重试

---

## 2. 设计原则

### 2.1 导入是任务，不是函数

一次导入可能持续数秒到数分钟，还会经历扫描、解析、写库、缩略图生成等多个阶段，因此必须建模为任务。

### 2.2 本地导入和 URL 导入是两条链路

两者共用同一套任务框架，但前半段状态不同：

1. 本地导入重在文件结构识别和物理路径校验
2. URL 导入重在 provider 解析、网络请求、远程资源拉取

### 2.3 允许“无法自动判断”

漫画资源的真实情况远比理想路径复杂：

1. 文件名混乱
2. 二级目录不规范
3. zip 内有噪声文件
4. 同系列多版本混杂
5. URL provider 返回章节不完整

因此系统必须有 awaiting_confirmation 状态。

### 2.4 导入必须幂等

重复拖入同一路径、同一 zip、同一 URL，不应生成重复记录。

### 2.5 错误分为可恢复和不可恢复

不是所有错误都应该直接失败退出。比如网络超时、文件被占用、缩略图生成失败，都应允许局部重试。

---

## 3. 术语

### 3.1 输入对象

1. Path Input：本地路径，可以是文件夹或 zip
2. Batch Input：多个本地路径
3. Url Input：远程作品或章节 URL

### 3.2 导入结果层级

1. Source：物理或远程来源记录
2. Volume：导入后的可阅读卷册
3. Series：聚合后的作品
4. Asset：封面和缩略图缓存

### 3.3 核心状态

1. pending
2. scanning
3. analyzing
4. awaiting_confirmation
5. importing
6. post_processing
7. completed
8. failed
9. canceled

---

## 4. 总体状态机

通用状态机如下：

```text
pending
  -> scanning
  -> analyzing
  -> awaiting_confirmation (可选)
  -> importing
  -> post_processing
  -> completed

任意阶段
  -> failed
  -> canceled
```

说明：

1. scanning 负责原始输入探测
2. analyzing 负责结构识别、元数据猜测、冲突分析
3. awaiting_confirmation 是人工纠正入口
4. importing 负责实际写入数据库和关系建立
5. post_processing 负责封面、缩略图、搜索索引、统计信息

---

## 5. 本地导入流程

### 5.1 输入范围

本地导入支持：

1. 单个 zip 文件
2. 单个图片目录
3. 系列目录
4. 多路径批量拖入

首版不建议支持：

1. rar/7z
2. 嵌套压缩包自动展开
3. 网络共享盘的实时监听导入

### 5.2 本地导入阶段定义

#### 阶段 A：任务创建

输入：路径或路径列表

动作：

1. 创建 ImportTask
2. 记录原始输入和 normalized_locator
3. 去重检查是否已有进行中的同类任务

输出：

1. 新任务
2. 或复用已存在任务

失败条件：

1. 输入为空
2. 路径非法
3. 无权限访问根路径

#### 阶段 B：扫描 scanning

动作：

1. 检查路径是否存在
2. 判断路径类型：文件、目录、批量输入
3. 过滤不支持项
4. 收集初步文件统计

输出：

1. PathDescriptor
2. BatchDescriptor

失败条件：

1. 文件不存在
2. 权限不足
3. 输入类型不支持
4. 路径过长或编码异常

#### 阶段 C：分析 analyzing

动作：

1. 判断是单卷还是系列目录
2. 识别卷命名规则
3. 提取候选系列名、卷号、作者线索
4. 扫描有效图片文件
5. 检测噪声文件和损坏文件
6. 检测与现有库的冲突

输出：

1. ImportPlan
2. ConflictReport
3. WarningList

进入 awaiting_confirmation 的条件：

1. 无法稳定识别卷号
2. 同系列同卷疑似重复
3. 根目录图片和子目录图片混用
4. 目录中包含多套系列
5. 有效图片数量不足但非零
6. 自动识别系列名置信度过低

直接失败的条件：

1. 没有任何有效图片
2. zip 无法打开
3. 所有图片均损坏
4. 输入对象明显不是漫画资源

#### 阶段 D：用户确认 awaiting_confirmation

用户应能修改：

1. 系列名
2. 卷号
3. 是否合并到已有系列
4. 是否跳过重复卷
5. 是否忽略部分输入项

用户可选动作：

1. 确认继续
2. 部分跳过后继续
3. 取消任务

说明：

如果你的目标是面向真实混乱资源库，这个阶段不是补丁，而是主流程的一部分。

#### 阶段 E：导入 importing

动作：

1. 建立或复用 Source
2. 建立或复用 Series
3. 创建或更新 Volume
4. 批量写入 Page
5. 建立人物、标签等关系
6. 初始化 ReadingProgress

要求：

1. 事务边界清晰
2. 批量写入页面时避免逐页单独提交
3. 冲突可记录、可回滚

失败条件：

1. 数据库锁冲突且重试后仍失败
2. 外键约束失败
3. 中途源文件不可读
4. 磁盘空间不足导致缓存初始化失败

#### 阶段 F：后处理 post_processing

动作：

1. 生成封面
2. 生成缩略图
3. 建立全文检索索引
4. 记录统计信息
5. 更新任务结果摘要

说明：

后处理失败不一定要把整个导入标记为 failed。建议区分：

1. 主导入成功，后处理部分失败
2. 主导入失败

首版可以把前者标记为 completed with warnings，并在任务详情中展示警告。

---

## 6. URL 导入流程

### 6.1 URL 导入前提

URL 导入必须基于 provider 抽象，不应直接在主逻辑里写死网站爬虫。

建议 provider 接口至少包含：

1. CanHandle(url)
2. ParseWork(url)
3. ListChapters(work)
4. ListPages(chapter)
5. DownloadPage(page)
6. GetRateLimitPolicy()

### 6.2 URL 导入阶段定义

#### 阶段 A：任务创建

动作：

1. 创建 remote_url 类型 ImportTask
2. 记录 URL 和 provider 候选
3. 过滤重复 URL 任务

#### 阶段 B：URL 解析 scanning

动作：

1. 匹配 provider
2. 校验 URL 格式
3. 标准化 URL

失败条件：

1. 无 provider 支持
2. URL 非法
3. provider 配置不可用

#### 阶段 C：远程分析 analyzing

动作：

1. 拉取作品元数据
2. 拉取章节列表
3. 判断 URL 指向作品还是单章节
4. 生成导入计划
5. 识别与本地库的重名或重复冲突

进入 awaiting_confirmation 的条件：

1. 用户需要选择导入章节范围
2. 作品与本地已有系列匹配不明确
3. provider 返回多候选作品
4. 远程元数据不完整

#### 阶段 D：下载与本地化 importing

首版建议采用“导入即下载到本地缓存”的模式。

动作：

1. 下载页面文件
2. 生成本地缓存结构
3. 建立 Source(kind=cache 或 remote+cache)
4. 建立 Volume / Page 记录
5. 关联 Series

失败条件：

1. 网络超时达到重试上限
2. 登录失效且无法自动恢复
3. 章节页面为空
4. 下载到一半磁盘空间不足

#### 阶段 E：后处理 post_processing

与本地导入一致，但需要额外记录：

1. provider 名称
2. 远程作品 ID
3. 远程章节 ID
4. 原始 URL

---

## 7. ImportPlan 设计

分析阶段的核心输出不是直接写数据库，而是先生成一个 ImportPlan。建议结构如下：

```text
ImportPlan
  - task_id
  - input_kind
  - series_candidates[]
  - volume_plans[]
  - conflict_report
  - warning_list
  - requires_confirmation
```

其中 VolumePlan 建议包含：

1. source locator
2. candidate series title
3. candidate volume number
4. display name
5. page file list
6. cover candidate
7. duplicate hints
8. warning flags

好处：

1. analyzing 和 importing 可以解耦
2. UI 可以展示“将要导入什么”
3. 用户确认后只需修改 ImportPlan 再继续

---

## 8. 错误分类

错误处理不要只靠异常字符串，建议统一错误码。

### 8.1 输入类错误

1. INPUT_EMPTY
2. PATH_NOT_FOUND
3. PATH_ACCESS_DENIED
4. PATH_TOO_LONG
5. UNSUPPORTED_INPUT_TYPE
6. URL_INVALID

### 8.2 结构识别类错误

1. NO_VALID_IMAGE
2. MIXED_DIRECTORY_LAYOUT
3. VOLUME_NUMBER_UNCERTAIN
4. SERIES_NAME_LOW_CONFIDENCE
5. MULTI_SERIES_IN_ONE_INPUT
6. ZIP_OPEN_FAILED
7. ZIP_FILENAME_ENCODING_UNKNOWN

### 8.3 内容类错误

1. IMAGE_DECODE_FAILED
2. PAGE_LIST_EMPTY
3. PAGE_ORDER_UNSTABLE
4. DUPLICATE_VOLUME_CONFLICT
5. SOURCE_FINGERPRINT_CONFLICT

### 8.4 系统类错误

1. DB_BUSY
2. DB_CONSTRAINT_FAILED
3. DISK_FULL
4. FILE_LOCKED
5. OUT_OF_MEMORY
6. CACHE_WRITE_FAILED

### 8.5 网络类错误

1. PROVIDER_NOT_SUPPORTED
2. PROVIDER_AUTH_REQUIRED
3. PROVIDER_RATE_LIMITED
4. NETWORK_TIMEOUT
5. NETWORK_UNREACHABLE
6. REMOTE_RESOURCE_REMOVED
7. REMOTE_RESPONSE_INVALID

---

## 9. 错误等级与处理策略

### 9.1 info

不影响导入完成，只用于记录过程。

示例：

1. 检测到 3 个噪声文件并忽略
2. 自动修正自然排序

### 9.2 warning

导入可完成，但结果可能需要用户注意。

示例：

1. 系列名置信度低
2. 部分页面损坏已跳过
3. 未生成封面，使用第一页代替

### 9.3 error

当前阶段无法继续，必须失败或等待用户处理。

示例：

1. 所有图片都无法解码
2. 数据库事务提交失败
3. provider 不支持当前 URL

---

## 10. 状态迁移规则

### 10.1 允许的迁移

1. pending -> scanning
2. scanning -> analyzing
3. analyzing -> awaiting_confirmation
4. analyzing -> importing
5. awaiting_confirmation -> importing
6. importing -> post_processing
7. post_processing -> completed
8. 任意状态 -> failed
9. pending/scanning/analyzing/awaiting_confirmation -> canceled

### 10.2 不允许的迁移

1. completed -> importing
2. failed -> importing，除非生成新任务或显式 retry
3. canceled -> importing

### 10.3 retry 规则

建议只允许以下阶段重试：

1. scanning
2. analyzing
3. importing
4. post_processing

并区分两类重试：

1. 原地重试：任务 ID 不变，retry_count 增加
2. 派生重试：复制旧任务输入，创建新任务

首版建议优先支持原地重试。

---

## 11. 幂等与去重策略

### 11.1 路径级幂等

规则：

1. 对本地路径做 normalized_locator
2. 若 Source(kind, normalized_locator) 已存在，则进入复用或重扫流程

### 11.2 内容级幂等

规则：

1. zip 可对整个文件计算指纹
2. 文件夹可对页面文件列表和部分摘要生成组合指纹
3. 指纹一致时提示用户复用已有卷

### 11.3 语义级冲突

规则：

1. 同系列同卷号已存在时提示冲突
2. 同系列同标题不同指纹时提示多版本冲突
3. 同一来源不同卷号时提示命名识别异常

### 11.4 URL 幂等

规则：

1. provider_name + provider_item_id + chapter_id 应可识别重复导入
2. URL 参数中无意义部分要在标准化阶段剔除

---

## 12. 回滚与恢复策略

### 12.1 数据库事务边界

建议把导入拆成两层事务：

1. 主事务：Series、Source、Volume、Page 写入
2. 后处理事务：Asset、索引、统计信息

这样主导入完成后，即使缩略图失败，也不会丢掉主体数据。

### 12.2 文件系统副作用

如果导入过程会写缓存文件，必须记录已创建路径，以便失败时清理未完成产物。

### 12.3 恢复原则

1. 主事务失败则回滚数据库写入
2. 后处理失败则保留主体记录并写 warning
3. missing 源文件通过 rescan 任务恢复，不直接删除书库记录

---

## 13. 任务日志设计

ImportTaskEvent 应记录以下信息：

1. stage
2. event_code
3. level
4. message
5. detail_json
6. created_at

建议关键节点都记录事件：

1. 任务创建
2. 路径扫描完成
3. 识别出系列和卷
4. 命中重复卷
5. 用户确认修改
6. 数据库导入成功
7. 缩略图生成失败
8. 任务完成

这会极大降低后续排查成本。

---

## 14. UI 表现建议

导入任务列表至少应展示：

1. 输入来源
2. 当前状态
3. 识别结果摘要
4. 冲突数
5. 警告数
6. 最终错误信息
7. 重试按钮
8. 查看详情按钮

对于 awaiting_confirmation，界面应支持：

1. 修改系列名
2. 修改卷号
3. 合并到已有系列
4. 跳过单个输入项
5. 批量应用规则

如果导入界面只显示一个旋转进度条，用户会很难信任系统。

---

## 15. 推荐的实现顺序

### 15.1 MVP 第一阶段

1. 单路径本地导入
2. zip 和文件夹识别
3. 基本 ImportTask 和 ImportTaskEvent
4. 冲突检测
5. 手动确认
6. 入库和分页写入

### 15.2 MVP 第二阶段

1. 批量导入
2. 重试机制
3. 缩略图后处理
4. 导入失败视图
5. 重扫任务

### 15.3 增强阶段

1. URL provider 抽象
2. 远程导入
3. 下载缓存策略
4. 限流和认证处理

---

## 16. 伪代码示例

```text
RunImportTask(task):
  set status = scanning
  descriptor = ScanInput(task)

  set status = analyzing
  plan = Analyze(descriptor)

  if plan.requires_confirmation:
    set status = awaiting_confirmation
    wait user decision

  set status = importing
  ExecuteImport(plan)

  set status = post_processing
  RunPostProcessing(plan)

  set status = completed
```

补充原则：

1. 每个阶段开始和结束都记录事件
2. 每次状态变更都写入 ImportTask
3. 任意异常都要转成统一错误码和可展示消息

---

## 17. 最终建议

导入模块决定这款漫画软件是否真的能服务真实用户。真正棘手的不是“能不能读图”，而是：

1. 脏路径能不能稳定识别
2. 分错类时能不能修正
3. 失败后能不能知道为什么失败
4. 重复导入时会不会把书架弄乱

所以首版导入模块最重要的不是全自动，而是：

1. 任务化
2. 幂等
3. 可确认
4. 可恢复
5. 可诊断

只要这五点成立，后续再加 URL 导入、插件系统和元数据抓取时，系统不会失控。
