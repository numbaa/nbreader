# NbReader 产品设计文档

> **角色声明**：本文档从资深漫画爱好者 + 产品经理的双重视角出发，定义 NbReader 的完整产品形态。
> 技术实现细节见 [`architecture.md`](architecture.md)。

---

## 1. 产品愿景

**"一个阅读器，所有漫画。"**

无论是硬盘里的 CBZ/CBR 收藏、本地文件夹、还是 nhentai 等在线源——用户只需打开 NbReader，就能浏览、搜索、阅读和管理所有漫画。书架是统一的，体验是一致的。

---

## 2. 核心概念模型

漫画世界的抽象，用 6 个概念说清楚：

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ ComicSource  │     │  ComicWork   │     │     Tag      │
│  (数据来源)   │     │  (抽象作品)   │     │    (标签)    │
│  local/      │     │  同一本漫画   │     │   可多选     │
│  nhentai/... │     │  的不同资源   │     │   跨源通用   │
└──────┬───────┘     └──────┬───────┘     └──────┬───────┘
       │                    │                    │
       ▼                    ▼                    ▼
┌─────────────────────────────────────────────────────────┐
│                   ComicResource                        │
│                 (一份具体资源)                           │
│  ┌──────────────────────────────────────────────────┐  │
│  │  来自某个 ComicSource 的某个具体实例：              │  │
│  │  · nhentai #655539（中文汉化，174 页）              │  │
│  │  · e-hentai #98765（英文，170 页）                 │  │
│  │  · D:\Comics\EroBiiky.cbz（自己下载的本地文件）     │  │
│  │  → 它们指向 ◆同一本◆ 漫画，但是 ◆不同◆ 的资源      │  │
│  └──────────────────────────────────────────────────┘  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐             │
│  │  Page 1  │  │  Page 2  │  │  Page N  │             │
│  └──────────┘  └──────────┘  └──────────┘             │
└─────────────────────────────────────────────────────────┘
       │
       ▼
┌──────────────┐     ┌──────────────┐
│ReadingProgress│     │    Series    │
│  (阅读进度)    │     │   (系列)     │
│  关联 Resource │     │  可选聚合    │
└──────────────┘     └──────────────┘
```

### 2.1 ComicWork — 抽象作品（"这本漫画"）

`ComicWork` 回答的问题是：**"这是哪本漫画？"**——不管它来自哪个网站、哪种语言、哪个版本。

| 属性 | 类型 | 说明 | 示例 |
|------|------|------|------|
| `Id` | int | 自增主键 | 1 |
| `CanonicalTitle` | string | 规范化标题（去作者/团体前缀） | "Ero Biiky - You're my Favorite" |
| `Description` | string? | 简介 | |

> **作者不存储在 Work 上**。作者统一走 `tags` 表（`type='artist'`），通过 Resource 关联的 artist tag 推算 Work 的作者。避免 ComicWork 和 tags 双重维护不一致。

**关键**：`ComicWork` 是可选层。大多数漫画不需要手动创建 Work——系统会在下载或收藏时**自动推测**（同标题+同作者→同一 Work），用户也可以手动合并/拆分。

### 2.2 ComicResource — 一份具体资源（"这本漫画的某个版本"）

`ComicResource` 是阅读的**最小原子单位**，也是书架的**直接条目**。用户打开的就是一份 Resource。

| 属性 | 类型 | 说明 | 示例 |
|------|------|------|------|
| `Id` | int | 自增主键 | 1 |
| `WorkId` | int? | 所属抽象作品（可为空） | 1 |
| `Title` | string | 该资源的完整标题（含版本信息） | "[Eco Heeky] Ero Biiky [Chinese] [DL版]" |
| `SourceType` | string | 来源类型 | `local` / `nhentai` / `ehentai` / ... |
| `SourceId` | string | 在来源中的唯一标识 | `"655539"` |
| `SourceUrl` | string? | 在线地址（可重新访问） | `https://nhentai.net/g/655539/` |
| `CoverPath` | string? | 封面图本地缓存路径 | |
| `PageCount` | int | 总页数 | 174 |
| `Language` | string? | 语言 | "chinese" |
| `ContentType` | string? | 内容类型（原 Category，改名避免与用户分类混淆） | "manga" |
| `AddedDate` | datetime | 加入书架时间 | |
| `LastReadDate` | datetime? | 最后阅读时间 | |
| `IsBookmarked` | bool | 是否收藏（在书架中可见） | true |
| `IsDownloaded` | bool | 是否已下载文件到本地 | true |
| `LocalPath` | string? | 本地文件路径（下载后填充） | `D:\NbReader\downloads\655539.cbz` |
| `SeriesId` | int? | 所属系列 | |
| `VolumeNumber` | int? | 单行本卷号（如第 3 卷），与 `ChapterNumber` 互不排斥 | `3` |
| `ChapterNumber` | int? | 连载话号（如第 42 话），与 `VolumeNumber` 互不排斥 | `42` |
| `FileHash` | string? | 文件指纹（用于去重和路径迁移重定位），在线源未下载时为 null | `"a1b2c3d4..."` |

> **`SourceType` 和 `SourceId` 不可变**：这两个字段标识资源的**原始来源**。下载到本地后 `source_type` 仍为 `nhentai`（而非改为 `local`），`source_id` 仍为 `655539`。`is_downloaded` 和 `local_path` 独立记录本地状态。详见 [§2.3](#23-收藏-vs-下载--两个独立维度)。

> **本地漫画的数据库表示**：对于用户直接导入的本地 CBZ/CBR/图片文件夹，`source_type = 'local'`，`source_id` = 文件/目录的绝对路径，`local_path` 与 `source_id` 相同（文件本身就是来源），`is_downloaded = 1`，`is_bookmarked = 1`（导入即加入书架）。与在线源下载后的条目不同——在线源的 `source_id` 是 gallery ID 而非本地路径。详见 [§2.5.1](#251-文件指纹与路径迁移)。

### 2.3 收藏 vs 下载 —— 两个独立维度

| 状态 | IsBookmarked | IsDownloaded | 含义 |
|------|:-----------:|:------------:|------|
| 仅浏览 | — | — | 在线看过，未加入书架（**无 DB 条目**，数据来自 API） |
| **已收藏** | ✅ | ❌ | 在书架中，在线阅读 |
| **已下载** | ✅ | ✅ | 在书架中，本地离线读 |
| 仅缓存 | ❌ | ✅ | （罕见）下载了但没加入书架 |

> **关键澄清**："仅浏览"阶段不创建 `comic_resources` 记录。只有用户执行「加入书架」或「下载」操作时，才写入数据库。因此对于所有已入库条目，`is_bookmarked` 恒为 `1`（入库 = 加入书架）。该字段保留用于未来可能出现的"取消收藏但保留本地文件"场景，当前阶段可视为冗余。

**用户故事**：
1. 在线浏览 → 看上某本 → 点「加入书架」→ 创建 DB 条目，`is_bookmarked=1`，封面出现在书架，点击后在线阅读
2. 书架上某本 → 点「下载」→ 后台下载 + 打包 CBZ → **同一条目** `is_downloaded=1`, `local_path` 填充，`source_type`/`source_id` **不变**
3. 直接点「下载并加入书架」→ 创建 DB 条目，两个标记同时置 1

**同一 ComicWork 的多个 Resource 在书架上如何展示？**

书架默认展示 `ComicResource` 列表。如果多个 Resource 属于同一 Work，有两种视图：

- **展开视图**：每份 Resource 独立一行，标注来源和语言
  ```
  Ero Biiky                          [nhentai] 中文 174页 ████░░ 42%
  Ero Biiky                          [本地]    中文 174页 ████░░ 42%
  Ero Biiky                          [e-hentai] 英文 170页 ░░░░░░ 0%
  ```

- **折叠视图**（默认）：同一 Work 折叠为一张卡片，点击展开选择具体版本
  ```
  Ero Biiky  (3 个版本)              ████░░ 42%
  ```

### 2.4 去重策略

**问题**：同一本漫画在 nhentai 上有多个上传（不同 ID），不同网站也有同一本——怎么识别？

| 层级 | 策略 | 准确度 |
|------|------|--------|
| **自动推断** | 规范化标题 + 作者相同 → 自动归入同一 Work | 中（同人本标题格式混乱） |
| **来源提供** | nhentai 详情页有 `related` 关联推荐，可作为线索 | 高 |
| **用户手动** | 右键 →「合并到同一作品」/「从作品中拆分」 | 100% |

自动推断算法（保守策略——宁可漏掉，不可误合并）：
1. 提取 `DisplayTitle`（去作者前缀、去发布团体后缀）
2. 提取 `Author`（从 tags 中取 artist 类型）
3. DisplayTitle + Author **完全一致** → 同一 Work
4. 不同语言不算同一 Work（中文版和日文版是不同资源，但属于同一 Work）

### 2.5 为什么不用 SourceId 去重？

同一网站可能有同一本漫画的**不同上传**（nhentai 上同一个本子可能有两个 gallery ID）。所以 `(source_type, source_id)` 只能保证"同一来源的同一资源不重复入库"，但不能保证"同一本漫画不重复"。

实际约束：
- `UNIQUE(source_type, source_id)` — 防止重复导入同一资源
- `WorkId` — 关联同一漫画的不同资源（软约束，允许为空）

### 2.5.1 文件指纹与路径迁移

**问题**：本地文件或下载后的漫画，用户可能会移动/重命名路径。`source_id` 和 `local_path` 依赖路径，路径一变就无法打开。

#### 文件指纹（`file_hash`）

`file_hash` 是内容指纹，不随路径变化，用于去重和重定位：

| 来源类型 | 指纹算法 | 计算时机 | 用途 |
|----------|----------|----------|------|
| CBZ / CBR | SHA256（文件前 1MB） | 导入/下载时 | 去重 + 重定位 |
| 图片文件夹 | SHA256（`文件名|大小` 排序拼接） | 导入时 | 去重 + 重定位 |
| 在线源（未下载） | `null` | — | 不适用 |

> **为什么只哈希前 1MB？** 漫画 CBZ 可达数百 MB，全量哈希太慢。前 1MB 碰撞概率极低，足以区分文件。文件夹不读文件内容，只读目录元数据（文件名+大小排序后拼接），速度极快。

#### 文件丢失检测

打开漫画时文件不存在 → 书架卡片显示灰色遮罩 + ⚠️ 图标：

```
┌──────┐
│ ⚠️   │
│ 封面  │  ← 灰色半透明遮罩
│      │
│ 标题  │
│ ⚠️ 丢失│  ← 进度条位置显示警告文字
└──────┘
```

#### 路径修复：三管齐下

```
文件路径变化
    │
    ├── 用户打开漫画 → "文件不存在" → 提示「重新定位...」
    │
    ├── 书架卡片显示 ⚠️ 丢失状态
    │     └── 右键 → 「重新定位...」→ 文件选择对话框 → 更新 local_path
    │         （若 source_type='local'，同步更新 source_id）
    │
    └── 启动扫描 → 在监控目录中找到相同 hash 的文件
          → 静默更新路径（无需用户干预）
```

**与 `UNIQUE(source_type, source_id)` 约束的协调**：对于 `source_type='local'` 的条目，路径迁移需要同时更新 `source_id`。迁移流程为：先检查新路径是否与已有条目冲突 → 更新 `source_id` + `local_path`。在线源条目只更新 `local_path`，`source_id` 不变。

### 2.6 Series — 系列

同一部漫画的多卷/多话，通过 `Series` 聚合。

| 属性 | 说明 |
|------|------|
| `Title` | 系列名（如 "One Piece"、"鬼灭之刃"） |
| `Description` | 简介 |
| `Resources` | 该系列下所有 Resource，按卷号 → 话号排序 |

> **作者不存储在 Series 上**，统一走 tags 表。

**Series 与 ComicWork 的区别**：
- `ComicWork`：**同一本**漫画的多个版本（如中文版 vs 日文版）
- `Series`：**不同本**漫画的前后关系（如第 1 卷 → 第 2 卷 → 第 3 卷）

**卷与话的区分**：漫画发布有"单行本（卷）"和"连载（话）"两种形态，两个字段互不排斥：

```sql
-- comic_resources 上的两个独立字段
volume_number   INTEGER,  -- 单行本卷号（如 1, 2, 3），null = 不适用
chapter_number  INTEGER,  -- 连载话号（如 1, 2, 3），null = 不适用
```

| 场景 | volume_number | chapter_number | 示例 |
|------|:---:|:---:|------|
| 单行本第 3 卷的 CBZ | `3` | `null` | "鬼灭之刃 Vol.3" |
| 连载第 42 话（在线源） | `null` | `42` | "鬼灭之刃 第42话" |
| 第 25 话属于第 3 卷 | `3` | `25` | "鬼灭之刃 Vol.3 第25话" |
| 同人志/单本 | `null` | `null` | "Ero Biiky" |

**Series 内排序**：

```sql
ORDER BY 
    COALESCE(volume_number, 99999),
    COALESCE(chapter_number, 99999)
```

无卷号的排最后，无话号的排最后。若希望卷和连载分开排列，可改为 `ORDER BY volume_number, chapter_number`（所有卷排在连载前面）。

**设计原则**：
- Series 是**可选**的。很多同人本就是单本，不强制归属系列
- 用户可以将多本漫画手动归入一个系列
- 书架按 Series 分组展示，单本则直接展示

### 2.7 多语言与跨源实体映射

**问题**：同一个标签/作者/标题，在不同网站可能是中文、英文、日文——但它们指向同一个东西。

```
nhentai:    "big breasts"     "eco heeky"     "Ero Biiky"
中国站点:    "巨乳"             "えこひいき"     "えろびいき"
日本站点:    "大きな胸"         "eco heeky"     "えろびいき"
              ↑ 同一标签         ↑ 同一作者       ↑ 同一漫画
```

#### 设计策略：Canonical + Alias 双层

```
┌─────────────────────┐
│   CanonicalEntity   │  ← 全局唯一实体（一人/一标签/一作品）
│   id, canonical_name│
└─────────┬───────────┘
          │ 1:N
          ▼
┌─────────────────────┐
│     EntityAlias     │  ← 各语言/各来源的叫法
│   entity_id         │
│   alias             │     "big breasts" (en) / "巨乳" (zh) / "大きな胸" (ja)
│   language          │
│   source_type       │     (可选) 限定来源，NULL = 通用
└─────────────────────┘
```

#### 2.7.1 标签（Tag）多语言

以 **英文为规范名**（nhentai 标准，覆盖面最广）。中文名作为显示映射。

| 场景 | 处理方式 |
|------|----------|
| **从 nhentai 导入** | 英文标签直接命中 canonical → 完成 |
| **从中文站点导入** | 中文标签如"巨乳" → 反向查 alias 表 → 命中 canonical `big breasts` → 完成 |
| **映射表未覆盖** | 以导入时的名称创建新 canonical，标记 `needs_review` |
| **UI 展示** | 用户界面为中文 → 优先显示 alias 中的中文名；无映射时显示英文原名 |

**预置映射**：系统内置 ~200 条常用标签的中文映射（手动维护一个 JSON 文件或直接写入 alias 表）。后续用户/社区可以扩展。

```
英文 canonical       中文显示名       日文显示名
─────────────────────────────────────────────
big breasts          巨乳             大きな胸
nakadashi            中出し           中出し
ahegao               阿嘿颜           アヘ顔
netorare             寝取られ         NTR
ffm threesome        3P (女男女)      FFM 3P
...
```

#### 2.7.2 作者（Author）多语言

作者是类型为 `artist` 的 tag，复用同一套 Canonical + Alias 机制。

```
canonical: "eco heeky"
  ├── alias: "えこひいき" (ja)
  └── alias: "eco heeky" (en, romaji)
```

对于作者，规范名优先用 **romaji（罗马音）**，因为跨语言搜索时最稳定。

#### 2.7.3 标题（Title）多语言

`ComicResource.title` 始终保留**来源原始标题**，不做翻译。`ComicWork.canonical_title` 取第一个导入的 Resource 的标题，用户可以手动编辑。

去重时标题比较策略：
1. 提取"裸标题"（去掉 `[作者]` `[团体]` `[语言]` 等前缀后缀）
2. 转小写、去空格、去特殊符号
3. 裸标题完全相同 → 推测为同一 Work
4. 不同语言的标题（如 romaji vs 日文）→ 仅靠标题无法自动匹配，需 alias 映射辅助

**标题 alias 表（可选，后续阶段）**：

```
canonical_title: "Ero Biiky - You're my Favorite"
  ├── alias: "えろびいき" (ja)
  └── alias: "에로 비이키" (ko)
```

#### 2.7.4 实现取舍

| 维度 | v1.0 策略 | 后续增强 |
|------|-----------|----------|
| 标签映射 | 预置 ~200 条中文映射，其余显示英文 | 社区贡献映射表、自动翻译建议 |
| 作者映射 | 按来源原始名存储，手动合并 | 自动推测（同画廊的 artist tag 可作为线索） |
| 标题去重 | 规范化比较 + 手动合并 | 标题 alias 表辅助跨语言匹配 |
| 未匹配实体 | 正常入库，标记 `needs_review` | 后台定期提示用户确认/合并 |

**外部参考**：[EhTagTranslation](https://github.com/EhTagTranslation/Database) 社区维护了 E-Hentai 标签的多语言翻译数据库，可作为 alias 表的初始数据来源。

### 2.8 Category — 用户分类

Tag 描述漫画**是什么**（`big breasts`、`manga`、`eco heeky`），Category 描述用户**怎么看待**这本漫画（`正在追`、`已读完`、`待下载`）。

```
Tag（标签）         → 漫画的客观属性，跨用户一致
Category（分类）    → 用户的主观分组，每人不同
Series（系列）      → 漫画之间的出版关系
```

| 属性 | 说明 |
|------|------|
| `Name` | 分类名（如"正在追""已读完""待整理"） |
| `SortOrder` | 排序权重（用户可拖拽排序） |
| `Resources` | 该分类下的漫画列表 |

一个 Resource 可以属于**多个** Category（比如既在"正在追"又在"收藏夹"）——Category 本质是用户打的私人标签，多分类比单选更灵活。

**书架左侧栏**（Mihon 风格）：

```
┌────────────────────┐
│ 📁 全部 (120)      │
│ 📁 正在追 (15)     │
│ 📁 已读完 (80)     │
│ 📁 待下载 (10)     │
│ 📁 收藏夹 (15)     │
│ ──────────────     │
│ ➕ 新建分类        │
└────────────────────┘
```

### 2.9 阅读历史

每次打开一本漫画阅读，自动记录一条历史。书架之外的独立视图，按时间倒序展示最近读过的漫画。

| 属性 | 说明 |
|------|------|
| `ResourceId` | 阅读的漫画资源 |
| `LastPageIndex` | 最后阅读的页码 |
| `ReadDate` | 阅读时间 |

与 `ReadingProgress` 的区别：Progress 是每本漫画的**最新**进度（用于断点续读），History 是**所有**阅读记录的时间线（用于回溯"我昨天看了什么"）。

### 2.3 Tag — 标签

跨源、跨漫画的通用标签系统。

| 属性 | 说明 |
|------|------|
| `Name` | 规范名（英文，如 `big breasts`） |
| `Type` | 标签类型：`general` / `artist` / `character` / `parody` / `language` / `category` |
| `Aliases` | 多语言显示名（如 `巨乳`、`大きな胸`） |

> 多语言标签/作者/标题的完整处理方案见 [2.7 多语言与跨源实体映射](#27-多语言与跨源实体映射)。

### 2.4 ComicSource — 漫画源

| 源 | 类型 | 说明 |
|----|------|------|
| `local-folder` | 本地 | 用户硬盘上的 CBZ/CBR/图片文件夹 |
| `local-download` | 本地 | 从在线源下载到本地的漫画 |
| `nhentai` | 在线 | nhentai.net |
| *(未来可扩展)* | | e-hentai、pixiv 等 |

---

## 3. 功能地图

```
NbReader
├── 📖 阅读器（已实现 ✅）
│   ├── 图片渲染
│   ├── 缩放 / 平移
│   ├── 翻页（← → 键盘 / 工具栏按钮）
│   ├── 适应模式（Uniform / FillWidth / FillHeight / Original）
│   ├── 阅读模式（单页 / 双页 / 滚动）
│   ├── 阅读方向（L→R / R→L）
│   └── 全屏（F11）
│
├── 📚 书架 ⬅ 当前阶段
│   ├── 网格 / 列表双视图
│   ├── 按 Category 分组（左侧栏）
│   ├── 按 Series 分组
│   ├── 排序（最近阅读 / 最近添加 / 标题 / 页数）
│   ├── 筛选（标签 / 作者 / 语言 / 分类 / 来源）
│   ├── 全文搜索
│   ├── 封面 + 标题 + 进度条
│   └── 右键菜单（读/删/编/导出）
│
├── 🕐 阅读历史
│   ├── 按时间倒序展示最近读过的漫画
│   ├── 点击继续阅读（断点续读）
│   └── 可清除单条或全部历史
│
├── 🏠 本地管理
│   ├── 扫描监控目录
│   ├── 手动添加文件/文件夹
│   ├── 拖放导入
│   └── 自动检测新增/删除
│
├── 📥 下载管理器（后续展开）
│   ├── 下载队列（排队、暂停、恢复、取消）
│   ├── 手动调整优先级
│   └── 下载进度通知
│
├── 🌐 在线浏览（Phase 6）
│   ├── 浏览在线源（翻页、排序）
│   ├── 漫画详情页（封面、标签、页数、简介）
│   ├── 在线阅读（流式加载）
│   ├── 下载到本地书架
│   └── 多源搜索
│
├── 🔌 多源插件（Phase 7+）
│   ├── IComicSource 插件接口
│   ├── 内置源：nhentai
│   ├── 社区可贡献新源
│   └── 源启用/禁用管理
│
├── ⚙️ 设置
│   ├── 默认阅读方向 / 适应模式
│   ├── 深色 / 浅色主题
│   ├── 书架监控目录配置
│   ├── 在线源启用/禁用
│   └── 缓存管理
│
└── 📊 阅读进度（自动）
    ├── 断点续读
    ├── 进度百分比
    └── 书架进度显示
```

---

## 4. UI 与交互设计

> NbReader 是桌面应用（Avalonia UI），交互模式遵循桌面惯例：侧栏导航、右键菜单、键盘快捷键、拖放操作、可缩放面板。

---

### 4.1 全局布局：三段式

```
┌──────────────────────────────────────────────────────────────┐
│  📂 打开  📁 文件夹  │  L→R  单页  适应页面  │  NbReader    │  ← 工具栏
├────────┬─────────────────────────────────────────────────────┤
│ 导航栏  │                                                    │
│        │                  内容区                              │
│ 📚 书架 │              (视图按需切换)                         │
│ 🕐 历史 │                                                    │
│ 🌐 在线 │                                                    │
│ ⚙️ 设置 │                                                    │
│        │                                                    │
├────────┴─────────────────────────────────────────────────────┤
│  状态信息                                         缩放: 100% │  ← 状态栏
└──────────────────────────────────────────────────────────────┘
```

**工具栏**（顶部）：左侧为文件操作（打开/文件夹/演示），中间为阅读控制（方向/模式/适应），右侧为标题。

**导航栏**（左侧）：图标 + 文字，点击切换内容区视图。当前视图高亮。宽度可拖拽调整（160px~300px），可折叠为仅图标（60px）。

**内容区**（中央）：根据导航选择展示不同视图——书架、历史、在线浏览、设置、阅读器。

**状态栏**（底部）：阅读时显示页码/模式/方向/缩放；非阅读时显示就绪状态。

---

### 4.2 书架视图（LibraryView）

核心交互界面，用户最常停留的页面。

```
┌──────────────────────────────────────────────────────────────┐
│  🔍 搜索...                     排序: 最近阅读 ▼    ▦  ☰    │
│  筛选: [中文 ×] [manga ×] [eco heeky] [+]                   │
├────────┬─────────────────────────────────────────────────────┤
│ 📁 全部 │  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐          │
│   120  │  │      │  │      │  │      │  │      │          │
│ 📁 追  │  │ 封面  │  │ 封面  │  │ 封面  │  │ 封面  │          │
│    15  │  │      │  │      │  │      │  │      │          │
│ 📁 完  │  │ 标题  │  │ 标题  │  │ 标题  │  │ 标题  │  网格   │
│    80  │  │ ██░░  │  │ ████  │  │ ░░░░  │  │ ████  │          │
│ 📁 待  │  └──────┘  └──────┘  └──────┘  └──────┘          │
│    10  │                                                    │
│        │  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐          │
│ ────── │  │ ...   │  │ ...   │  │ ...   │  │ ...   │          │
│ ➕ 新建 │  └──────┘  └──────┘  └──────┘  └──────┘          │
│        │                                                    │
│        ├────────────────────────────────────────────────────┤
│        │  共 120 本              ◀ 1  2  3  ...  12  ▶     │
└────────┴────────────────────────────────────────────────────┘
```

**左侧 Category 栏**：

| 交互 | 行为 |
|------|------|
| 点击分类名 | 右侧筛选为该分类的漫画 |
| 右键分类 | 弹出菜单：重命名 / 删除 / 上移 / 下移 |
| 拖拽分类 | 调整排序 |
| 「全部」始终置顶，不可删除 |
| 「➕ 新建」在最底部 |

**漫画卡片**：

| 元素 | 说明 |
|------|------|
| 封面 | 宽高比自适应，未加载时灰色占位 |
| 标题 | 单行省略，悬停 tooltip 显示完整标题 + 作者 + 页数 |
| 进度条 | 底部 4px 细条，已读部分主题色，未读部分透明。文件丢失时显示 ⚠️ 警告 |
| 角标 | 右下角：语言国旗（🇨🇳/🇯🇵/🇬🇧）+ 来源小图标（💻本地/🌐在线/⬇已下载） |
| 文件丢失 | 封面覆盖灰色半透明遮罩 + ⚠️ 图标，标题旁提示"文件丢失" |
| 右键菜单 | 阅读 / 下载 / 重新定位...（仅丢失时）/ 编辑元数据 / 移至分类 → / 从书架移除 / 合并到作品... |
| 双击 | 打开阅读 |
| 拖拽 | 拖到左侧分类名上 → 加入该分类 |

**排序选项**：最近阅读 / 最近添加 / 标题 A-Z / 页数 / 进度

**视图切换**：`▦` 网格视图（默认）/ `☰` 列表视图（显示更多元数据列）

---

### 4.3 阅读历史视图（HistoryView）

```
┌──────────────────────────────────────────────────────────────┐
│  🕐 阅读历史                                  清除全部历史    │
├────────┬─────────────────────────────────────────────────────┤
│        │                                                     │
│        │  今天                                               │
│        │  ┌──────────────────────────────────────────────┐  │
│        │  │ [封面]  Ero Biiky - You're my Favorite       │  │
│        │  │ 80x120  第 42 页 / 共 174 页  ·  2 小时前    │  │
│        │  └──────────────────────────────────────────────┘  │
│        │  ┌──────────────────────────────────────────────┐  │
│        │  │ [封面]  One Piece Vol.1                       │  │
│        │  │ 80x120  第 15 页 / 共 200 页  ·  5 小时前    │  │
│        │  └──────────────────────────────────────────────┘  │
│        │                                                     │
│        │  昨天                                               │
│        │  ┌──────────────────────────────────────────────┐  │
│        │  │ [封面]  ...                                   │  │
│        │  └──────────────────────────────────────────────┘  │
│        │                                                     │
└────────┴─────────────────────────────────────────────────────┘
```

| 交互 | 行为 |
|------|------|
| 点击条目 | 打开漫画，断点续读 |
| 悬停条目右侧 | 出现 ✕ 按钮 → 删除单条历史 |
| 「清除全部历史」 | 确认对话框 → 清空 |
| 同一漫画多次阅读 | 合并为一条，显示最新时间和页码（不重复出现） |

---

### 4.4 在线浏览视图（SourceBrowserView）

```
┌──────────────────────────────────────────────────────────────┐
│  🌐 在线浏览    [nhentai ▼]  搜索...         排序: 最新 ▼    │
├────────┬─────────────────────────────────────────────────────┤
│        │                                                     │
│        │  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐           │
│        │  │ 封面  │  │ 封面  │  │ 封面  │  │ 封面  │           │
│        │  │      │  │      │  │      │  │      │           │
│        │  │ 标题  │  │ 标题  │  │ 标题  │  │ 标题  │           │
│        │  │ 🇨🇳174页│ │ 🇯🇵200页│ │ 🇨🇳 88页│ │ 🇬🇧150页│           │
│        │  └──────┘  └──────┘  └──────┘  └──────┘           │
│        │                                                     │
│        │  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐           │
│        │  │ ...   │  │ ...   │  │ ...   │  │ ...   │           │
│        │  └──────┘  └──────┘  └──────┘  └──────┘           │
│        │                                                     │
│        ├─────────────────────────────────────────────────────┤
│        │            ◀ 1  2  3  ...  6061  ▶                │
└────────┴─────────────────────────────────────────────────────┘

         ┌──── 点击漫画 ────┐
         ▼                  ▼
┌─────────────────┐  ┌─────────────────┐
│  漫画详情页       │  │  阅读器（和本地   │
│  封面 + 标签 +    │  │  完全相同的体验） │
│  页数 + 简介      │  │                 │
│                 │  │                 │
│  [加入书架]      │  └─────────────────┘
│  [立即下载]      │
│  [在线阅读]      │
└─────────────────┘
```

**卡片与书架卡片的区别**：封面右下角显示页数（如 `174页`），语言角标。无进度条（尚未加入书架）。

**详情页**：点击漫画卡片后，弹出或内嵌详情面板，展示大封面 + 全部标签 + 页数 + 上传时间 + 简介。三个操作按钮：「加入书架」（收藏但不下载）、「立即下载」（下载+加入书架）、「在线阅读」（直接打开阅读器流式读）。

---

### 4.5 设置视图（SettingsView）

```
┌──────────────────────────────────────────────────────────────┐
│  ⚙️ 设置                                                    │
├────────┬─────────────────────────────────────────────────────┤
│ 阅读    │                                                     │
│ 书架    │  默认阅读方向    ○ L→R    ● R→L                    │
│ 在线源  │  默认适应模式    ○ Uniform  ○ FillWidth             │
│ 外观    │                  ○ FillHeight  ● Original           │
│ 关于    │  默认阅读模式    ● 单页  ○ 双页  ○ 滚动             │
│        │                                                     │
└────────┴─────────────────────────────────────────────────────┘
```

左侧为设置分类，右侧为具体设置项。设置改动即时生效并自动持久化，无需「保存」按钮。

---

### 4.6 全局交互规范

#### 键盘快捷键

| 快捷键 | 作用 | 上下文 |
|--------|------|--------|
| `←` `→` | 翻页（方向取决于阅读方向设置） | 阅读器 |
| `PgUp` `PgDn` | 翻页（不受方向影响） | 阅读器 |
| `Space` | 下一页 | 阅读器 |
| `F` | 循环适应模式 | 阅读器 |
| `M` | 循环阅读模式 | 阅读器 |
| `D` | 切换阅读方向 | 阅读器 |
| `F11` | 全屏 | 全局 |
| `Ctrl+滚轮` | 缩放 | 阅读器 |
| `Ctrl+0` | 重置缩放 | 阅读器 |
| `Ctrl+N` | 切换到书架 | 全局 |
| `Ctrl+H` | 切换到历史 | 全局 |
| `Ctrl+B` | 切换到在线浏览 | 全局 |
| `Ctrl+,` | 打开设置 | 全局 |
| `Esc` | 退出全屏 / 关闭弹窗 | 全局 |

#### 拖放支持

| 拖放源 | 目标 | 行为 |
|--------|------|------|
| 文件管理器中的 CBZ/CBR/文件夹 | 窗口任意位置 | 打开漫画 |
| 书架上的漫画卡片 | 左侧 Category 名 | 加入该分类 |
| 书架上的漫画卡片 | 左侧「➕ 新建」 | 创建新分类并加入 |

#### 右键菜单

书架卡片右键：
```
┌──────────────────┐
│  📖 阅读          │
│  ⬇ 下载           │
│  ─────────────── │
│  🔍 重新定位...    │  ← 仅文件丢失时显示
│  ✏️ 编辑元数据     │
│  📁 移至分类  →    │  ← 展开子菜单列出所有分类
│  🔗 合并到作品...  │
│  ─────────────── │
│  🗑 从书架移除     │
└──────────────────┘
```

#### 通知

下载完成、导入完成等操作通过短暂 Toast 提示（右下角弹出，3 秒自动消失），不打断当前操作。

---

### 4.7 视图切换流程

```
                    ┌─────────────┐
           ┌───────│   阅读器     │◄────────┐
           │       │ (ReaderView) │         │
           │       └─────────────┘         │
           │         打开漫画               │ 从书架/历史点击
           │                               │
    ┌──────┴──────┐                 ┌──────┴──────┐
    │   书架       │                 │   历史       │
    │ (LibraryView)│                 │(HistoryView) │
    └──────┬──────┘                 └─────────────┘
           │
           │ 导航栏切换
           ▼
    ┌──────────────┐
    │   在线浏览     │
    │(SourceBrowser)│
    │              │
    │  点击漫画     │──→ 详情页 ──→ 阅读器（在线阅读）
    │              │──→ 下载 ──→ 加入书架 ──→ 阅读器（本地）
    └──────────────┘
```

阅读器始终是全屏内容区（覆盖导航栏），退出阅读（Esc 或关闭漫画）后回到之前的视图。

## 5. nhentai 在线源设计

### 5.1 数据映射

nhentai 的 API 返回结构和我们的 ComicBook 模型天然契合：

| nhentai 字段 | ComicBook 字段 | 说明 |
|-------------|---------------|------|
| `id` | `SourceId` | "655539" |
| `title.pretty` | `Title` | 格式化标题 |
| `tags[type=tag]` | Tags | 转换为通用标签 |
| `tags[type=artist]` | Artists | 作为标签存储 |
| `tags[type=language]` | Language | 取第一个 |
| `tags[type=category]` | ContentType | 取第一个 |
| `num_pages` | `PageCount` | |
| `images.cover` | `CoverPath` | 下载缓存 |
| `images.pages` | Page 列表 | 每页的图片 URL |

### 5.2 浏览流程

```
在线浏览页                       漫画详情页
┌─────────────────┐             ┌─────────────────┐
│  搜索 / 分类浏览  │  ──点击──▶  │  封面 + 标签 +    │
│  (翻页列表)      │             │  页数 + 简介     │
│                 │             │                 │
│  ┌────┐┌────┐   │             │  [开始阅读]      │
│  │封面││封面│   │             │  [下载到书架]    │
│  └────┘└────┘   │             └────────┬────────┘
│  ┌────┐┌────┐   │                      │
│  │封面││封面│   │             ┌────────▼────────┐
│  └────┘└────┘   │             │  阅读器（和本地   │
│                 │             │  完全相同的体验） │
│  ◀ 1 2 3 ... ▶ │             └─────────────────┘
└─────────────────┘
```

### 5.3 在线阅读 vs 本地阅读

在线漫画使用**相同的 ReaderView**。区别仅在于 `IFileSource` 的实现：

| 来源 | IFileSource 实现 | 图片来源 |
|------|-----------------|---------|
| 本地 CBZ | `CbzFileSource` | ZIP 内提取 |
| 本地 CBR | `CbrFileSource` | RAR 内提取 |
| 本地文件夹 | `DirectoryFileSource` | 磁盘文件 |
| **nhentai 在线** | `NhentaiFileSource` | **HTTP 流式下载** |

**流式加载策略**：
- 当前页 + 预加载后 3 页，其余按需加载
- 已加载的页面缓存到临时目录
- 下载到书架后，所有页面写入本地 CBZ，后续按本地漫画处理

### 5.4 下载到书架

用户在线浏览 → 看上一本漫画 → 点「下载到书架」：

**若已加入书架**（已有 `comic_resources` 条目）：
1. 批量下载所有页面图片 → 打包为 CBZ，写入 `local_path`
2. 封面缓存到本地，更新 `cover_path`
3. 设置 `is_downloaded = 1`
4. `source_type` / `source_id` **保持不变**（仍为 `nhentai` / `655539`），保留来源追溯能力
5. 后续打开直接读本地 CBZ，离线可用

**若首次下载**（尚无条目）：
1. 同上打包
2. 创建 `comic_resources` 条目，`is_bookmarked = 1`, `is_downloaded = 1`
3. 元数据（标签、语言等）一并写入

---

## 6. SQLite 数据模型

> **实现时直接使用。** 所有表使用 `IF NOT EXISTS`，首次启动自动建表。

```sql
-- ═══════════════════════════════════════════════════════════
-- 初始化（SqliteStorageService 构造函数中执行）
-- ═══════════════════════════════════════════════════════════
PRAGMA foreign_keys = ON;
PRAGMA journal_mode  = WAL;
PRAGMA busy_timeout  = 5000;

-- ═══════════════════════════════════════════════════════════
-- 抽象作品（同一本漫画，跨源/跨版本）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS comic_works (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    canonical_title TEXT    NOT NULL,
    description     TEXT
);
CREATE INDEX IF NOT EXISTS idx_work_title ON comic_works(canonical_title);

-- ═══════════════════════════════════════════════════════════
-- 具体资源（书架的直接条目，阅读的原子单位）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS comic_resources (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    work_id         INTEGER,              -- FK → comic_works.id，可为空
    title           TEXT    NOT NULL,      -- 完整标题（含版本信息）
    source_type     TEXT    NOT NULL,      -- 'local' | 'nhentai' | 'ehentai' | ...
    source_id       TEXT    NOT NULL,      -- 来源内唯一标识
    source_url      TEXT,                  -- 在线地址
    cover_path      TEXT,                  -- 封面图本地缓存路径
    page_count      INTEGER NOT NULL DEFAULT 0,
    language        TEXT,                  -- 'chinese' | 'japanese' | 'english' | ...
    content_type    TEXT,                  -- 'manga' | 'doujinshi' | 'artist cg' | ...
    added_date      TEXT    NOT NULL,      -- ISO 8601
    last_read_date  TEXT,                  -- ISO 8601
    is_bookmarked   INTEGER NOT NULL DEFAULT 0,
    is_downloaded   INTEGER NOT NULL DEFAULT 0,
    local_path      TEXT,                  -- 下载后的本地文件路径
    series_id       INTEGER,              -- FK → series.id
    volume_number   INTEGER,              -- 单行本卷号，null 表示不适用
    chapter_number  INTEGER,              -- 连载话号，null 表示不适用
    file_hash       TEXT,                  -- 文件指纹（CBZ/CBR: SHA256 前 1MB；文件夹: SHA256(文件名|大小)；在线未下载: null）
    FOREIGN KEY (work_id)   REFERENCES comic_works(id),
    FOREIGN KEY (series_id) REFERENCES series(id),
    UNIQUE(source_type, source_id)
);
CREATE INDEX IF NOT EXISTS idx_resource_work       ON comic_resources(work_id);
CREATE INDEX IF NOT EXISTS idx_resource_series     ON comic_resources(series_id);
CREATE INDEX IF NOT EXISTS idx_resource_added      ON comic_resources(added_date);
CREATE INDEX IF NOT EXISTS idx_resource_read       ON comic_resources(last_read_date);
CREATE INDEX IF NOT EXISTS idx_resource_bookmarked ON comic_resources(is_bookmarked);
CREATE INDEX IF NOT EXISTS idx_resource_language   ON comic_resources(language);
CREATE INDEX IF NOT EXISTS idx_resource_content    ON comic_resources(content_type);

-- ═══════════════════════════════════════════════════════════
-- 系列（多卷/多话聚合）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS series (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    title       TEXT    NOT NULL,
    description TEXT
);
CREATE INDEX IF NOT EXISTS idx_series_title ON series(title);

-- ═══════════════════════════════════════════════════════════
-- 标签（全局去重，英文规范名）
-- 作者也是 tag（type='artist'），不单独建表
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS tags (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    name         TEXT    NOT NULL UNIQUE,   -- 英文规范名，如 "big breasts"
    type         TEXT    NOT NULL DEFAULT 'general',
    needs_review INTEGER NOT NULL DEFAULT 0 -- 是否待人工确认
);
CREATE INDEX IF NOT EXISTS idx_tag_type   ON tags(type);
CREATE INDEX IF NOT EXISTS idx_tag_review ON tags(needs_review);

-- ═══════════════════════════════════════════════════════════
-- 标签/作者多语言别名
-- 导入时可反向查找：SELECT tag_id FROM entity_aliases WHERE alias = '巨乳'
-- 展示时可正向查找：SELECT alias FROM entity_aliases WHERE tag_id = 1 AND language = 'zh'
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS entity_aliases (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    tag_id      INTEGER NOT NULL,
    alias       TEXT    NOT NULL,
    language    TEXT,                       -- 'zh' | 'ja' | 'en' | ...
    source_type TEXT,                       -- 限定来源，NULL = 通用
    FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE,
    UNIQUE(tag_id, alias)
);
CREATE INDEX IF NOT EXISTS idx_alias_tag  ON entity_aliases(tag_id);
CREATE INDEX IF NOT EXISTS idx_alias_name ON entity_aliases(alias);

-- ═══════════════════════════════════════════════════════════
-- 资源-标签 多对多
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS resource_tags (
    resource_id INTEGER NOT NULL,
    tag_id      INTEGER NOT NULL,
    PRIMARY KEY (resource_id, tag_id),
    FOREIGN KEY (resource_id) REFERENCES comic_resources(id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id)      REFERENCES tags(id)             ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_rt_tag ON resource_tags(tag_id);

-- ═══════════════════════════════════════════════════════════
-- 用户分类（Category）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS categories (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    name       TEXT    NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS resource_categories (
    resource_id INTEGER NOT NULL,
    category_id INTEGER NOT NULL,
    PRIMARY KEY (resource_id, category_id),
    FOREIGN KEY (resource_id) REFERENCES comic_resources(id) ON DELETE CASCADE,
    FOREIGN KEY (category_id) REFERENCES categories(id)      ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_rc_cat ON resource_categories(category_id);

-- ═══════════════════════════════════════════════════════════
-- 阅读进度（每资源仅最新一条，用于断点续读）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS reading_progress (
    resource_id     INTEGER PRIMARY KEY,
    current_page    INTEGER NOT NULL DEFAULT 0,
    last_read_time  TEXT    NOT NULL,       -- ISO 8601
    FOREIGN KEY (resource_id) REFERENCES comic_resources(id) ON DELETE CASCADE
);

-- ═══════════════════════════════════════════════════════════
-- 阅读历史（每次打开追加一条，保留全部时间线）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS read_history (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    resource_id     INTEGER NOT NULL,
    last_page_index INTEGER NOT NULL DEFAULT 0,
    read_date       TEXT    NOT NULL,       -- ISO 8601
    FOREIGN KEY (resource_id) REFERENCES comic_resources(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_history_resource ON read_history(resource_id);
CREATE INDEX IF NOT EXISTS idx_history_date     ON read_history(read_date DESC);

-- ═══════════════════════════════════════════════════════════
-- 监控目录（本地管理 — 扫描 CBZ/CBR/图片文件夹的来源）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS monitored_directories (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    path    TEXT    NOT NULL UNIQUE,
    enabled INTEGER NOT NULL DEFAULT 1
);

-- ═══════════════════════════════════════════════════════════
-- 在线源配置（哪些源启用、哪些禁用）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS comic_sources (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    source_type TEXT    NOT NULL UNIQUE,    -- 'nhentai' | 'ehentai' | ...
    enabled     INTEGER NOT NULL DEFAULT 0,
    config_json TEXT                        -- 源特定配置（JSON）
);

-- ═══════════════════════════════════════════════════════════
-- 下载任务（Phase 6+ 使用，表先建好避免后续迁移）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS downloads (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    resource_id   INTEGER,                  -- 关联的漫画资源（可为空，下载完成后填充）
    source_type   TEXT    NOT NULL,
    source_id     TEXT    NOT NULL,
    source_url    TEXT    NOT NULL,
    status        TEXT    NOT NULL DEFAULT 'queued',  -- queued | downloading | paused | done | failed
    progress      INTEGER NOT NULL DEFAULT 0,         -- 0-100
    priority      INTEGER NOT NULL DEFAULT 0,
    created_date  TEXT    NOT NULL,         -- ISO 8601
    FOREIGN KEY (resource_id) REFERENCES comic_resources(id)
);
CREATE INDEX IF NOT EXISTS idx_dl_status ON downloads(status);

-- ═══════════════════════════════════════════════════════════
-- 用户设置（key-value）
-- ═══════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

### 6.1 Schema 设计说明

| 表 | 用途 | Phase |
|----|------|:-----:|
| `comic_works` | 抽象作品，跨源/跨版本聚合 | 2 |
| `comic_resources` | 具体资源，书架直接条目 | 2 |
| `series` | 多卷/多话系列 | 2 |
| `tags` | 标签/作者全局去重（英文规范名） | 2 |
| `entity_aliases` | 标签/作者多语言别名 | 2 |
| `resource_tags` | 资源-标签多对多 | 2 |
| `categories` | 用户自定义分类 | 2 |
| `resource_categories` | 资源-分类多对多 | 2 |
| `reading_progress` | 断点续读 | 2 |
| `read_history` | 阅读时间线 | 2 |
| `monitored_directories` | 本地监控目录列表 | 2 |
| `comic_sources` | 在线源启用/禁用配置 | 2（表） / 6（逻辑） |
| `downloads` | 下载任务队列 | 2（表） / 6（逻辑） |
| `settings` | 用户偏好 key-value | 2 |

### 6.2 关键修正

| 修正 | 原因 |
|------|------|
| `comic_resources.category` → `content_type` | 避免与 `categories` 表（用户分类）命名冲突 |
| `comic_works.author` 移除 | 作者统一走 `tags`（`type='artist'`）+ `resource_tags`，避免双重维护 |
| `series.author` 移除 | 同上，系列作者通过关联 Resource 的 artist tag 推算 |
| 外键添加 `ON DELETE CASCADE` | 删除 Resource 时自动清理关联的标签/分类/进度/历史 |
| 补充 `PRAGMA`（foreign_keys / WAL / busy_timeout） | 外键强制 + 读写并发性能 + 锁等待超时 |
| 补充缺失索引 | `resource_tags(tag_id)` / `resource_categories(category_id)` / `tags(needs_review)` / `comic_resources(language)` / `comic_resources(content_type)` / `series(title)` / `downloads(status)` |
| 新增 `monitored_directories` | 本地管理功能需要持久化监控目录列表 |
| 新增 `comic_sources` | 在线源管理需要持久化启用/禁用状态 |
| 新增 `downloads` | 下载管理器需要任务队列，表先建好避免后续迁移 |
| `volume_number` 拆为 `volume_number` + `chapter_number` | 卷（单行本）和话（连载）是两种发布形态，原来的单一字段无法区分；两个字段互不排斥、可同时有值 |
| 明确 `source_type` / `source_id` 不可变 | 下载到本地后不覆盖原始来源信息，保留追溯能力；`is_downloaded` + `local_path` 独立记录本地状态 |
| 明确「仅浏览」不创建 DB 条目 | 在线浏览/详情页/在线阅读均不入库，只有「加入书架」或「下载」操作才写 `comic_resources` |
| 新增 `file_hash` | CBZ/CBR 取前 1MB 文件内容的 SHA256，图片文件夹取排序文件名+大小的 SHA256；用于去重和路径迁移重定位 |

## 7. 书架与在线源的交互流程

### 7.1 用户故事：浏览 nhentai → 收藏 → 下载 → 书架阅读

```
1. 用户切换到「在线浏览」视图
2. 选择 nhentai 源 → 看到中文漫画列表（分页）
3. 翻页浏览，点击感兴趣的漫画 → 进入详情页
4. 详情页显示封面、标签、页数、简介
5a. 用户点击「加入书架」→ 创建 comic_resources 条目，is_bookmarked=1
    → 封面出现在书架，点击后在线阅读（流式）
5b. 用户点击「下载」→ 后台下载 + 打包 CBZ → 同一条目 is_downloaded=1, local_path 填充
    → source_type 仍为 nhentai（来源可追溯），下次打开秒读本地 CBZ
6. 如果该漫画在 nhentai 和 e-hentai 都有：
    → 系统自动推测为同一 Work，书架折叠显示"Ero Biiky（2个版本）"
    → 用户可手动确认合并或拆分
```

### 7.2 用户故事：管理本地收藏

```
1. 用户设置监控目录（如 D:\Comics\）
2. NbReader 启动时扫描该目录，自动发现新增 CBZ/CBR/文件夹
3. 提取封面 → 入库 → 书架可见
4. 用户按标签/语言/分类筛选
5. 点击漫画 → ReaderView 打开 → 断点续读
6. 读完 → 进度自动保存 → 书架显示 "100%"
```

---

## 8. 架构分层（更新）

```
┌──────────────────────────────────────────────────────────┐
│                   UI Layer (NbReader)                    │
│  ┌──────────┐ ┌───────────┐ ┌──────────┐ ┌───────────┐ │
│  │ReaderView│ │LibraryView│ │SourceBrow│ │SettingsVw │ │
│  │(阅读器)   │ │(书架)     │ │serView   │ │(设置)     │ │
│  └────┬─────┘ └─────┬─────┘ │(在线浏览) │ └─────┬─────┘ │
│       │             │       └─────┬─────┘       │       │
│  ┌────┴─────────────┴─────────────┴─────────────┴────┐  │
│  │              ViewModels (MVVM)                     │  │
│  │  ReaderVM │ LibraryVM │ SourceBrowserVM │ SettVM  │  │
│  └────────────────────┬───────────────────────────────┘  │
├───────────────────────┼──────────────────────────────────┤
│            Core Layer (NbReader.Core)                    │
│  ┌────────────────────┴───────────────────────────────┐  │
│  │                   Abstractions                      │  │
│  │  IFileSource │ IStorageService │ IComicSource      │  │
│  │  IImageLoader│                 │ (在线源接口)       │  │
│  └────────────────────┬───────────────────────────────┘  │
│  ┌────────────────────┴───────────────────────────────┐  │
│  │                   Services                          │  │
│  │  FileSourceFactory  │ SqliteStorageService          │  │
│  │  SkiaImageLoader    │ NhentaiSource (实现IComicSrc) │  │
│  │  LibraryScanner     │ DownloadService               │  │
│  └────────────────────┬───────────────────────────────┘  │
│  ┌────────────────────┴───────────────────────────────┐  │
│  │                   Models                            │  │
│  │  ComicBook │ Series │ Tag │ ReadingProgress         │  │
│  │  ComicInfo │ PageInfo│                               │  │
│  └─────────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────┤
│               Infrastructure                             │
│  Avalonia │ SkiaSharp │ SharpCompress │ SQLite │ HTTP   │
└──────────────────────────────────────────────────────────┘
```

---

## 9. 新增核心接口

```csharp
// ── 在线漫画源接口 ──────────────────────────────

public interface IComicSource
{
    /// <summary>源名称</summary>
    string Name { get; }

    /// <summary>获取漫画列表（分页）</summary>
    Task<BrowseResult> BrowseAsync(BrowseQuery query);

    /// <summary>获取漫画详情（含所有标签、页数）</summary>
    Task<ComicDetail> GetDetailAsync(string sourceId);

    /// <summary>获取指定页的图片流（在线阅读用）</summary>
    Task<Stream> GetPageStreamAsync(string sourceId, int pageIndex);

    /// <summary>下载整本漫画的所有页面</summary>
    Task<byte[][]> DownloadPagesAsync(string sourceId, IProgress<int>? progress = null);
}

public class BrowseQuery
{
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SortBy { get; set; }  // "date" | "popular"
    public Dictionary<string, string> Filters { get; set; } = new();
}

public class BrowseResult
{
    public List<ComicBook> Items { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
}

public class ComicDetail
{
    public ComicBook Book { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public string? Description { get; set; }
}
```

---

## 10. 实施路线图

### 当前阶段：Phase 5 — 书架基础 + 持久化

| 步骤 | 内容 |
|------|------|
| P5.1 | SQLite 持久化层（完整 Schema，含 categories + history） |
| P5.2 | `LibraryViewModel` + `LibraryView`（书架 UI + 左侧 Category 栏） |
| P5.3 | 本地扫描入库（`LibraryScanner`） |
| P5.4 | 阅读进度自动保存/恢复 + 阅读历史记录 |
| P5.5 | 基础筛选（语言、分类、Category） |

### 下一阶段：Phase 6 — 在线源

| 步骤 | 内容 |
|------|------|
| P6.1 | `IComicSource` 接口 + `NhentaiSource` 实现 |
| P6.2 | `SourceBrowserView`（在线浏览列表 + 翻页） |
| P6.3 | 漫画详情页 |
| P6.4 | 在线阅读（流式加载） |
| P6.5 | 下载到书架（批量下载 + 打包 CBZ） |

### 远期：Phase 7+ — 打磨

| 步骤 | 内容 |
|------|------|
| P7.1 | 下载管理器（队列、优先级、通知） |
| P7.2 | 多源插件架构（IComicSource 社区扩展） |
| P7.3 | 标签系统完善（EhTagTranslation 导入 + 中文映射） |
| P7.4 | Series 分组管理 |
| P7.5 | 元数据编辑 |
| P7.6 | 深色/浅色主题 |

---

## 11. 关键设计决策记录

| 决策 | 结论 | 理由 |
|------|------|------|
| 本地和在线漫画是否统一表？ | ✅ 统一 `comic_resources` | 书架体验一致 |
| 收藏和下载是否独立？ | ✅ `is_bookmarked` ≠ `is_downloaded` | 用户可只收藏（在线读），以后再下载 |
| 同一本漫画的不同版本如何关联？ | ✅ `comic_works` 聚合 | nhentai + e-hentai + 本地 CBZ → 同一 Work |
| **用户分类 vs 标签怎么区分？** | **Tag = 客观属性，Category = 主观分组** | Tag 跨漫画通用；Category 是用户私人书架管理 |
| 一个 Resource 能属于多个 Category 吗？ | ✅ 多对多 | 同一本可以既在"正在追"又在"收藏夹" |
| 同一网站重复上传如何防重？ | `UNIQUE(source_type, source_id)` + Work 推测 | 不入库重复资源；Work 层提示合并 |
| 多语言标签/作者如何处理？ | Canonical(英文) + Alias(多语言映射) | 英文覆盖面最广；可导入 EhTagTranslation 数据 |
| 未匹配的标签/作者怎么处理？ | 以原始名创建 canonical，标记 `needs_review` | 不阻塞导入流程 |
| 去重靠自动还是手动？ | 自动推测 + 手动确认 | 自动过于激进会误伤 |
| 进度关联什么？ | 关联 `resource_id` | 不同版本进度独立 |
| 阅读历史 vs 阅读进度？ | History = 全部时间线，Progress = 最新状态 | History 找回"昨天看了什么"，Progress 用于断点续读 |
| 在线阅读和本地阅读是否共用 ReaderView？ | ✅ 共用 | `IFileSource` 多态 |
| 多源支持架构？ | `IComicSource` 插件接口 | 内置 nhentai，社区可扩展更多源 |
| 系列是否强制？ | ❌ 可选 | 大量同人本是单本 |
| 卷和话如何区分？ | `volume_number` + `chapter_number` 两个独立可空字段 | 单行本（卷）和连载（话）是不同发布形态，可同时有值（如第 3 卷第 25 话） |
| 下载后是新建条目还是更新？ | ✅ 原地更新同一条目 | 保留 `source_type`/`source_id` 不变（来源追溯），仅更新 `is_downloaded` + `local_path` |
| 在线浏览阶段是否入库？ | ❌ 不入库 | 浏览/详情/在线阅读数据来自 API，只有用户主动「加入书架」或「下载」才创建 DB 记录 |
| 文件路径迁移如何处理？ | hash 重定位 + UI 手动定位 + 扫描自动修复 | 本地文件可能被用户移动；哈希不随路径变化，可在监控目录中自动匹配 |
| 文件哈希算法如何选？ | CBZ/CBR: SHA256(前 1MB)；文件夹: SHA256(文件名+大小排序) | 全量哈希太慢；前 1MB 碰撞概率极低；文件夹用元数据指纹避免读取所有图片 |
