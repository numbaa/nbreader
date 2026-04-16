# 数据库实体与字段设计

## 1. 文档目标

本文档用于把设计稿中的概念模型落到可实现的数据库结构，目标如下：

1. 支持本地漫画库管理，覆盖 zip 和图片文件夹两类来源
2. 支持系列、卷、作者、年份、tag、阅读进度等核心能力
3. 为后续 URL 导入、在线元数据匹配、同步服务预留扩展点
4. 保证首版实现足够简单，不因为过度抽象拖慢开发

---

## 2. 设计原则

### 2.1 数据库是元数据真相来源

文件系统只保存原始资源，系列归类、卷号、作者、标签、进度、导入状态都应以数据库为准。

### 2.2 概念模型和首版表结构分离

设计稿中的 Source Item、Book/Volume、Series、LibraryItem 是产品层概念。首版数据库不需要把所有概念都硬拆成独立表，应该优先满足查询、导入、阅读三条主链路。

### 2.3 首版优先 Volume 作为书架原子单元

首版书架中的最小可阅读实体定义为 Volume。Series 作为聚合层，Source 作为物理来源层。

这意味着：

1. 一个系列可包含多个卷
2. 一个卷原则上对应一个主来源
3. 一个来源可以是文件夹、zip 或远程缓存资源
4. Page 始终归属某个卷，不直接归属系列

### 2.4 规范化和可检索性优先于“纯展示文本”

支持作者、类型、年份、tag 检索，要求数据库中同时存储：

1. 展示字段
2. 规范化字段
3. 必要的索引字段
4. 来源字段

---

## 3. 表结构总览

### 3.1 MVP 必需表

1. Series
2. Volume
3. Source
4. Page
5. Person
6. SeriesPerson
7. Tag
8. SeriesTag
9. VolumeTag
10. ReadingProgress
11. ReadingHistory
12. Asset
13. ImportTask
14. ImportTaskEvent

### 3.2 进阶阶段建议表

1. MetadataMatchCandidate
2. DuplicateGroup
3. Chapter
4. RemoteProviderAccount
5. SyncState

### 3.3 不建议首版落地的抽象

1. 单独的 LibraryItem 表
2. 过度通用的 EntityAttribute KV 表
3. 把缩略图直接存进 SQLite Blob

原因很简单：首版真正需要的是稳定查询和稳定导入，而不是高度抽象。

---

## 4. 枚举定义

### 4.1 Source.kind

1. folder
2. zip
3. remote
4. cache

说明：

1. folder 表示本地图片目录
2. zip 表示本地压缩包
3. remote 表示远程来源逻辑记录
4. cache 表示从远程导入后落到本地缓存的资源实体

首版可只用 folder 和 zip，remote 与 cache 先预留。

### 4.2 Series.status

1. ongoing
2. completed
3. hiatus
4. unknown

### 4.3 Person.role

1. author
2. artist
3. original_story
4. translator
5. group

### 4.4 Tag.category

1. genre
2. source
3. content_rating
4. user
5. system

### 4.5 ImportTask.status

1. pending
2. scanning
3. analyzing
4. awaiting_confirmation
5. importing
6. post_processing
7. completed
8. failed
9. canceled

### 4.6 ImportTask.kind

1. local_path
2. local_batch
3. remote_url
3. rescan

---

## 5. 核心表设计

### 5.1 Series

用途：系列聚合实体，对应用户书架中的“作品”。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| title | TEXT | 是 | 系列显示标题 |
| title_sort | TEXT | 是 | 排序标题，去除空格、特殊前缀 |
| title_pinyin | TEXT | 否 | 拼音索引字段 |
| alt_titles_json | TEXT | 否 | 别名 JSON 数组 |
| description | TEXT | 否 | 简介 |
| status | TEXT | 是 | ongoing/completed/hiatus/unknown |
| year | INTEGER | 否 | 首发年份或作品年份 |
| cover_asset_id | INTEGER FK | 否 | 封面资产 |
| metadata_source | TEXT | 否 | 元数据来源，如 manual/anilist/bangumi |
| rating | REAL | 否 | 可选用户评分 |
| note | TEXT | 否 | 用户备注 |
| created_at | TEXT | 是 | ISO 8601 时间 |
| updated_at | TEXT | 是 | ISO 8601 时间 |
| deleted_at | TEXT | 否 | 软删除时间 |

索引建议：

1. unique index on title_sort
2. index on year
3. index on status
4. index on updated_at
5. FTS 虚表同步 title、alt_titles_json、description

说明：

1. 不建议首版把作者字段直接塞进 Series 表，因为后续会扩展画师、译者、汉化组
2. title_sort 和 title_pinyin 是检索体验关键字段

### 5.2 Volume

用途：可直接阅读的卷册实体，是首版书架的最小阅读单位。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| series_id | INTEGER FK | 否 | 所属系列，可为空 |
| source_id | INTEGER FK | 是 | 主来源 |
| volume_number | REAL | 否 | 卷号，支持 0.5、1.5 |
| volume_label | TEXT | 否 | 原始卷号文本，如 第01卷、Vol.1 |
| subtitle | TEXT | 否 | 副标题 |
| display_name | TEXT | 是 | 最终显示名 |
| sort_key | TEXT | 是 | 统一排序键 |
| page_count | INTEGER | 是 | 总页数 |
| release_date | TEXT | 否 | 发行或导入判断日期 |
| reading_direction_override | TEXT | 否 | rtl/ltr/null |
| cover_asset_id | INTEGER FK | 否 | 卷封面 |
| import_task_id | INTEGER FK | 否 | 由哪个导入任务创建 |
| scan_status | TEXT | 是 | ready/warning/broken |
| warning_flags_json | TEXT | 否 | 例如 missing_cover、duplicate_pages |
| created_at | TEXT | 是 | 创建时间 |
| updated_at | TEXT | 是 | 更新时间 |
| deleted_at | TEXT | 否 | 软删除 |

约束建议：

1. foreign key series_id references Series(id)
2. foreign key source_id references Source(id)
3. check page_count >= 0
4. unique(series_id, sort_key) 可选，但要允许用户手动修复冲突，所以建议先做普通索引而非强唯一

索引建议：

1. index on series_id
2. index on source_id
3. index on volume_number
4. index on sort_key
5. index on updated_at
6. index on scan_status

说明：

1. volume_number 用于数值排序
2. volume_label 保留原始文本，便于回显和调试
3. sort_key 应该在导入阶段生成，不能每次查询时临时计算

### 5.3 Source

用途：记录物理来源或远程来源，是导入、重扫、去重的关键表。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| kind | TEXT | 是 | folder/zip/remote/cache |
| path_or_url | TEXT | 是 | 本地路径或远程 URL |
| normalized_locator | TEXT | 是 | 规范化后的路径或 URL |
| fingerprint | TEXT | 否 | 文件指纹或内容指纹 |
| file_size | INTEGER | 否 | 本地资源总大小 |
| provider_name | TEXT | 否 | 远程来源 provider |
| provider_item_id | TEXT | 否 | provider 内作品 ID |
| encoding_hint | TEXT | 否 | zip 文件名编码提示 |
| last_scanned_at | TEXT | 否 | 最近扫描时间 |
| scan_status | TEXT | 是 | pending/ready/missing/broken |
| error_code | TEXT | 否 | 最近错误码 |
| error_message | TEXT | 否 | 最近错误信息 |
| created_at | TEXT | 是 | 创建时间 |
| updated_at | TEXT | 是 | 更新时间 |

约束建议：

1. unique(kind, normalized_locator)
2. index on fingerprint
3. index on provider_name, provider_item_id
4. index on scan_status

说明：

1. normalized_locator 用于幂等导入
2. fingerprint 用于内容去重
3. 本地文件删除后，Source 不应立即删表记录，而应标记 missing

### 5.4 Page

用途：卷内页面索引，支持阅读器顺序浏览、预加载和故障检测。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| volume_id | INTEGER FK | 是 | 所属卷 |
| source_id | INTEGER FK | 是 | 来源 |
| page_index | INTEGER | 是 | 从 0 开始的页序 |
| inner_path | TEXT | 是 | 文件夹下相对路径或 zip 内路径 |
| display_name | TEXT | 否 | 原始文件名 |
| width | INTEGER | 否 | 图像宽 |
| height | INTEGER | 否 | 图像高 |
| file_size | INTEGER | 否 | 页面大小 |
| checksum | TEXT | 否 | 页面摘要，用于重复页检测 |
| mime_type | TEXT | 否 | image/jpeg 等 |
| is_cover_candidate | INTEGER | 是 | 0/1 |
| is_deleted | INTEGER | 是 | 0/1，逻辑过滤无效页 |
| created_at | TEXT | 是 | 创建时间 |

约束建议：

1. unique(volume_id, page_index)
2. index on source_id
3. index on checksum
4. index on is_cover_candidate

说明：

1. 首版无需把页面内容缓存进数据库
2. is_deleted 用于屏蔽广告页、说明页、损坏页，不必直接删记录

### 5.5 Person

用途：作者、画师、译者、汉化组等人物或组织实体。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| name | TEXT | 是 | 展示名称 |
| normalized_name | TEXT | 是 | 规范化名称 |
| name_pinyin | TEXT | 否 | 拼音索引 |
| alias_json | TEXT | 否 | 别名数组 |
| created_at | TEXT | 是 | 创建时间 |
| updated_at | TEXT | 是 | 更新时间 |

约束建议：

1. unique(normalized_name)
2. index on name_pinyin

### 5.6 SeriesPerson

用途：定义 Series 与 Person 的多对多关系及角色。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| series_id | INTEGER FK | 是 | 系列 |
| person_id | INTEGER FK | 是 | 人物 |
| role | TEXT | 是 | author/artist/translator/group |
| sort_order | INTEGER | 是 | 展示顺序 |
| source | TEXT | 否 | manual/imported/matched |

约束建议：

1. unique(series_id, person_id, role)
2. index on person_id, role

说明：

首版只做 SeriesPerson 即可。VolumePerson 可以后续再加，除非你明确要支持单卷不同译者。

### 5.7 Tag

用途：标签主表。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| name | TEXT | 是 | 标签名 |
| normalized_name | TEXT | 是 | 规范化名称 |
| category | TEXT | 是 | genre/source/content_rating/user/system |
| color | TEXT | 否 | 展示色 |
| created_at | TEXT | 是 | 创建时间 |
| updated_at | TEXT | 是 | 更新时间 |

约束建议：

1. unique(category, normalized_name)
2. index on category

### 5.8 SeriesTag

用途：系列级标签关系。

字段建议：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| series_id | INTEGER FK | 是 | 系列 |
| tag_id | INTEGER FK | 是 | 标签 |
| source | TEXT | 否 | manual/imported/matched |

约束建议：

1. unique(series_id, tag_id)

### 5.9 VolumeTag

用途：卷级标签关系，存放来源标签、卷特有标签。

字段建议：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| volume_id | INTEGER FK | 是 | 卷 |
| tag_id | INTEGER FK | 是 | 标签 |
| source | TEXT | 否 | manual/imported/system |

约束建议：

1. unique(volume_id, tag_id)

### 5.10 ReadingProgress

用途：每卷的当前阅读进度，是高频读写表。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| volume_id | INTEGER PK/FK | 是 | 卷 |
| current_page | INTEGER | 是 | 当前页 |
| max_page_reached | INTEGER | 是 | 历史到达最远页 |
| completed | INTEGER | 是 | 0/1 |
| completed_at | TEXT | 否 | 完成时间 |
| last_read_at | TEXT | 否 | 最近阅读时间 |
| reading_mode | TEXT | 否 | single/double/scroll |
| reading_direction | TEXT | 否 | rtl/ltr |
| zoom_mode | TEXT | 否 | fit_width/fit_height/original |
| updated_at | TEXT | 是 | 更新时间 |

约束建议：

1. check current_page >= 0
2. index on last_read_at
3. index on completed

说明：

ReadingProgress 只保留当前态，不存完整历史。

### 5.11 ReadingHistory

用途：存储阅读行为事件，用于最近阅读、跨设备同步、行为统计。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| volume_id | INTEGER FK | 是 | 卷 |
| page_from | INTEGER | 否 | 起始页 |
| page_to | INTEGER | 否 | 结束页 |
| event_type | TEXT | 是 | open/close/jump/complete |
| duration_ms | INTEGER | 否 | 阅读时长 |
| created_at | TEXT | 是 | 事件时间 |

索引建议：

1. index on volume_id, created_at desc
2. index on event_type

### 5.12 Asset

用途：管理封面图、缩略图、导出缓存等本地资产。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| asset_type | TEXT | 是 | cover/thumb/preview |
| storage_path | TEXT | 是 | 相对缓存路径 |
| width | INTEGER | 否 | 宽 |
| height | INTEGER | 否 | 高 |
| file_size | INTEGER | 否 | 大小 |
| checksum | TEXT | 否 | 摘要 |
| created_at | TEXT | 是 | 创建时间 |

约束建议：

1. unique(storage_path)
2. index on asset_type

说明：

缩略图建议以文件形式存储，Asset 只存路径和元信息。

### 5.13 ImportTask

用途：导入任务主表，承接状态机和恢复机制。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| kind | TEXT | 是 | local_path/local_batch/remote_url/rescan |
| source_locator | TEXT | 是 | 路径或 URL |
| normalized_locator | TEXT | 是 | 规范化定位符 |
| status | TEXT | 是 | pending...completed/failed/canceled |
| current_stage | TEXT | 否 | scanning/analyzing/... |
| retry_count | INTEGER | 是 | 已重试次数 |
| conflict_count | INTEGER | 是 | 冲突数 |
| error_code | TEXT | 否 | 最终错误码 |
| error_message | TEXT | 否 | 错误信息 |
| payload_json | TEXT | 否 | 输入参数 |
| result_json | TEXT | 否 | 导入结果摘要 |
| created_at | TEXT | 是 | 创建时间 |
| updated_at | TEXT | 是 | 更新时间 |
| finished_at | TEXT | 否 | 完成时间 |

约束建议：

1. index on status
2. index on current_stage
3. index on normalized_locator
4. index on created_at desc

### 5.14 ImportTaskEvent

用途：记录导入过程事件，用于日志、诊断和恢复。

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| id | INTEGER PK | 是 | 主键 |
| import_task_id | INTEGER FK | 是 | 导入任务 |
| stage | TEXT | 是 | 对应阶段 |
| level | TEXT | 是 | info/warn/error |
| event_code | TEXT | 是 | 事件码 |
| message | TEXT | 是 | 消息 |
| detail_json | TEXT | 否 | 额外细节 |
| created_at | TEXT | 是 | 时间 |

索引建议：

1. index on import_task_id, created_at
2. index on level
3. index on event_code

---

## 6. 首版推荐索引策略

### 6.1 结构化检索

1. Series.year
2. Series.status
3. Person.normalized_name
4. Tag.category
5. ReadingProgress.completed
6. ReadingProgress.last_read_at

### 6.2 模糊搜索

建议使用 SQLite FTS5 建立以下逻辑搜索文档：

1. series title
2. alt titles
3. person names
4. tag names
5. description

不建议把所有字段都塞进一个超大 FTS 表。应按搜索用途构造聚合文档，避免更新成本过高。

### 6.3 拼音搜索

SQLite FTS 不负责把中文转拼音。建议在写入或更新时同步生成：

1. title_pinyin
2. person.name_pinyin
3. 需要时为 tag 生成 normalized_name

---

## 7. 关键约束与业务规则

### 7.1 系列与卷

1. Volume 可以暂时不属于任何 Series，用于“未整理”视图
2. Series 删除时默认不级联删除 Volume，只清空其 series_id 或进入回收站逻辑
3. Volume 的 sort_key 必须在导入或修正卷号时重算

### 7.2 来源与幂等导入

1. Source.kind + normalized_locator 必须唯一
2. 同一路径重复导入时，应尝试复用旧 Source
3. 文件消失时标记 missing，不自动删除卷和进度

### 7.3 页面顺序

1. Page.page_index 必须连续
2. inner_path 仅表示来源内部顺序，不一定等于展示顺序
3. 扫描阶段应先把自然排序算好，再写 Page.page_index

### 7.4 标签与人物关系

1. 人物和标签都应记录 source 字段，区分手工编辑与自动导入
2. 自动匹配产生的关系必须允许用户覆盖

### 7.5 阅读进度

1. ReadingProgress.current_page 永远指向上次关闭时的稳定页
2. completed 由业务规则驱动，不应仅根据 current_page == page_count - 1 粗暴判断
3. 完成状态应支持人工取消

---

## 8. 首版不做什么

以下内容不建议首版落库：

1. 页面级批注和书签系统
2. 完整远程账号体系
3. 复杂推荐算法数据表
4. 图片内容特征索引
5. 多来源合卷映射

原因是这些能力不会直接决定 MVP 是否可用，但会显著增加维护复杂度。

---

## 9. 建议的建表顺序

1. Source
2. Series
3. Volume
4. Page
5. Person
6. SeriesPerson
7. Tag
8. SeriesTag
9. VolumeTag
10. ReadingProgress
11. ReadingHistory
12. Asset
13. ImportTask
14. ImportTaskEvent

这个顺序便于先打通“导入 -> 书架 -> 阅读 -> 搜索”的闭环。

---

## 10. 与实现层的对应关系

建议在代码层按以下模块组织：

1. Catalog
2. Import
3. Reader
4. Metadata
5. Search
6. Infrastructure

其中数据库仓储建议最少包含：

1. SeriesRepository
2. VolumeRepository
3. SourceRepository
4. ReadingRepository
5. ImportTaskRepository
6. SearchRepository

---

## 11. 最终建议

如果只追求“首版能跑”，很容易把作者、标签、进度、路径这些字段全塞到一两张表里。但那样做会很快卡死在系列整理、检索扩展和 URL 导入上。

更稳的做法是：

1. 用 Series + Volume + Source + Page 构成主骨架
2. 用 Person + Tag 解决检索和元数据扩展
3. 用 ReadingProgress + ReadingHistory 解决阅读态和最近阅读
4. 用 ImportTask + ImportTaskEvent 支撑导入状态机和错误恢复

这样既不会过度设计，也足以支撑你当前设计稿里的核心能力。
