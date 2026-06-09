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
| `Author` | string? | 作者/画师 | "eco heeky" |
| `Description` | string? | 简介 | |

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
| `Category` | string? | 分类 | "manga" |
| `AddedDate` | datetime | 加入书架时间 | |
| `LastReadDate` | datetime? | 最后阅读时间 | |
| `IsBookmarked` | bool | 是否收藏（在书架中可见） | true |
| `IsDownloaded` | bool | 是否已下载文件到本地 | true |
| `LocalPath` | string? | 本地文件路径（下载后填充） | `D:\NbReader\downloads\655539.cbz` |
| `SeriesId` | int? | 所属系列 | |
| `VolumeNumber` | int? | 卷/话序号 | |

### 2.3 收藏 vs 下载 —— 两个独立维度

| 状态 | IsBookmarked | IsDownloaded | 含义 |
|------|:-----------:|:------------:|------|
| 仅浏览 | ❌ | ❌ | 在线看过，未加入书架 |
| **已收藏** | ✅ | ❌ | 在书架中，在线阅读 |
| **已下载** | ✅ | ✅ | 在书架中，本地离线读 |
| 仅缓存 | ❌ | ✅ | （罕见）下载了但没加入书架 |

**用户故事**：
1. 在线浏览 → 看上某本 → 点「加入书架」→ `IsBookmarked=true`，封面出现在书架，点击后在线阅读
2. 书架上某本 → 点「下载」→ 后台下载 + 打包 CBZ → `IsDownloaded=true`，下次打开秒读
3. 直接点「下载并加入书架」→ 两个标记同时置 true

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

### 2.6 Series — 系列

同一部漫画的多卷/多话，按 `VolumeNumber` 聚合。

| 属性 | 说明 |
|------|------|
| `Title` | 系列名（如 "One Piece"） |
| `Author` | 作者 |
| `Resources` | 该系列下所有 Resource，按 VolumeNumber 排序 |

**Series 与 ComicWork 的区别**：
- `ComicWork`：**同一本**漫画的多个版本（如中文版 vs 日文版）
- `Series`：**不同本**漫画的前后关系（如第 1 卷 → 第 2 卷 → 第 3 卷）

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
│   ├── 按 Series 分组
│   ├── 排序（最近阅读 / 最近添加 / 标题 / 页数）
│   ├── 筛选（标签 / 作者 / 语言 / 分类 / 来源）
│   ├── 全文搜索
│   ├── 封面 + 标题 + 进度条
│   └── 右键菜单（读/删/编/导出）
│
├── 🏠 本地管理
│   ├── 扫描监控目录
│   ├── 手动添加文件/文件夹
│   ├── 拖放导入
│   └── 自动检测新增/删除
│
├── 🌐 在线浏览（Phase 3）
│   ├── 浏览在线源（翻页、排序）
│   ├── 漫画详情页（封面、标签、页数、简介）
│   ├── 在线阅读（流式加载）
│   ├── 下载到本地书架
│   └── 多源搜索
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

## 4. 书架 UI 设计

### 4.1 布局

```
┌─────────────────────────────────────────────────────┐
│  🔍 搜索...        排序: 最近阅读 ▼  视图: ▦ ☰     │
│  筛选: [中文 ×] [manga ×] [eco heeky] [+]          │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐         │
│  │          │  │          │  │          │         │
│  │  封面    │  │  封面    │  │  封面    │         │
│  │          │  │          │  │          │         │
│  │ 标题     │  │ 标题     │  │ 标题     │   网格  │
│  │ ████░░ 42%│ │ ██████ 85%│ │ ░░░░░░ 0%│         │
│  └──────────┘  └──────────┘  └──────────┘         │
│                                                     │
├─────────────────────────────────────────────────────┤
│  共 120 本  │  ◀ 1  2  3  ...  12  ▶              │
└─────────────────────────────────────────────────────┘
```

### 4.2 漫画卡片

每张卡片显示：
- **封面缩略图**（宽高比自适应）
- **标题**（单行，超出省略）
- **进度条**（底部细条，已读部分高亮）
- **右下角角标**：语言国旗 / 来源图标
- 悬停时显示完整标题 + 作者 + 页数 tooltip

### 4.3 筛选栏

标签以 Chip 形式展示，点击切换。已激活的标签高亮，点击 × 取消。

```
筛选: [中文] [×]  [manga] [×]  [eco heeky] [×]  [+ 添加筛选]
```

`+ 添加筛选` 点击后弹出下拉面板，按类型分组：
```
Tags: big breasts (5)  nakadashi (3)  ...
Artists: eco heeky (1)  ...
Languages: chinese (80)  japanese (30)  english (10)
Categories: manga (50)  doujinshi (40)  artist cg (30)
```

括号内数字表示"在当前结果中有多少本"。

---

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
| `tags[type=category]` | Category | 取第一个 |
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

1. 批量下载所有页面图片 → 打包为 CBZ
2. 元数据写入 SQLite（`ComicBook` 表）
3. 封面缓存到本地
4. `IsDownloaded = true`，`SourceType = Local`
5. 书架中即刻可见，后续完全离线阅读

---

## 6. SQLite 数据模型（更新版）

基于 `ComicWork` + `ComicResource` 双层模型：

```sql
-- 抽象作品（同一本漫画，跨源/跨版本）
CREATE TABLE comic_works (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    canonical_title TEXT    NOT NULL,
    author          TEXT
);
CREATE INDEX idx_work_title  ON comic_works(canonical_title);
CREATE INDEX idx_work_author ON comic_works(author);

-- 具体资源（书架的直接条目）
CREATE TABLE comic_resources (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    work_id         INTEGER,              -- FK → comic_works.id，可为空
    title           TEXT    NOT NULL,
    source_type     TEXT    NOT NULL,      -- 'local' | 'nhentai' | 'ehentai' | ...
    source_id       TEXT    NOT NULL,      -- 来源内唯一标识
    source_url      TEXT,                  -- 在线地址
    cover_path      TEXT,
    page_count      INTEGER NOT NULL DEFAULT 0,
    language        TEXT,
    category        TEXT,
    added_date      TEXT    NOT NULL,
    last_read_date  TEXT,
    is_bookmarked   INTEGER NOT NULL DEFAULT 0,
    is_downloaded   INTEGER NOT NULL DEFAULT 0,
    local_path      TEXT,                  -- 下载后的本地文件路径
    series_id       INTEGER,              -- FK → series.id
    volume_number   INTEGER,
    FOREIGN KEY (work_id)   REFERENCES comic_works(id),
    FOREIGN KEY (series_id) REFERENCES series(id),
    UNIQUE(source_type, source_id)
);
CREATE INDEX idx_resource_work   ON comic_resources(work_id);
CREATE INDEX idx_resource_series ON comic_resources(series_id);
CREATE INDEX idx_resource_added  ON comic_resources(added_date);
CREATE INDEX idx_resource_read   ON comic_resources(last_read_date);
CREATE INDEX idx_resource_bookmarked ON comic_resources(is_bookmarked);

-- 系列（多卷/多话）
CREATE TABLE series (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    title       TEXT    NOT NULL,
    author      TEXT,
    description TEXT
);

-- 标签（全局去重，英文规范名）
CREATE TABLE tags (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    name         TEXT    NOT NULL UNIQUE,   -- 英文规范名，如 "big breasts"
    type         TEXT    NOT NULL DEFAULT 'general',
    needs_review INTEGER NOT NULL DEFAULT 0 -- 是否待人工确认
);
CREATE INDEX idx_tag_type ON tags(type);

-- 标签/作者多语言别名
CREATE TABLE entity_aliases (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    tag_id      INTEGER NOT NULL,           -- FK → tags.id
    alias       TEXT    NOT NULL,           -- 别名，如 "巨乳"
    language    TEXT,                       -- 'zh' | 'ja' | 'en' | ...
    source_type TEXT,                       -- 限定来源，NULL = 通用
    FOREIGN KEY (tag_id) REFERENCES tags(id),
    UNIQUE(tag_id, alias)
);
CREATE INDEX idx_alias_tag  ON entity_aliases(tag_id);
CREATE INDEX idx_alias_name ON entity_aliases(alias);

-- 资源-标签 多对多
CREATE TABLE resource_tags (
    resource_id INTEGER NOT NULL,
    tag_id      INTEGER NOT NULL,
    PRIMARY KEY (resource_id, tag_id),
    FOREIGN KEY (resource_id) REFERENCES comic_resources(id),
    FOREIGN KEY (tag_id)      REFERENCES tags(id)
);

-- 阅读进度（关联资源）
CREATE TABLE reading_progress (
    resource_id     INTEGER PRIMARY KEY,
    current_page    INTEGER NOT NULL DEFAULT 0,
    last_read_time  TEXT    NOT NULL,
    FOREIGN KEY (resource_id) REFERENCES comic_resources(id)
);

-- 用户设置
CREATE TABLE settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

### 6.1 与初版的关键差异

| 变更 | 原因 |
|------|------|
| `comic_works` + `comic_resources` 双层 | 区分"是哪本漫画"和"是哪个版本" |
| `is_bookmarked` 独立于 `is_downloaded` | 收藏≠下载，用户可以只收藏不下载 |
| `source_url` | 保留在线地址，方便"去原网站看看" |
| `local_path` | 下载后的本地路径，与 `source_id` 分离 |
| `resource_tags` 替代 `comic_tags` | 标签关联到 Resource，同一 Work 的不同 Resource 可以有不同标签（如语言标签） |

---

## 7. 书架与在线源的交互流程

### 7.1 用户故事：浏览 nhentai → 收藏 → 下载 → 书架阅读

```
1. 用户切换到「在线浏览」视图
2. 选择 nhentai 源 → 看到中文漫画列表（分页）
3. 翻页浏览，点击感兴趣的漫画 → 进入详情页
4. 详情页显示封面、标签、页数、简介
5a. 用户点击「加入书架」→ is_bookmarked=true，封面出现在书架
    → 下次从书架点击 → 在线阅读（流式）
5b. 用户点击「下载」→ 后台下载 + 打包 CBZ → is_downloaded=true
    → 下次打开秒读，离线可用
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
| P5.1 | SQLite 持久化层（按照更新后的 Schema） |
| P5.2 | `LibraryViewModel` + `LibraryView`（书架 UI） |
| P5.3 | 本地扫描入库（`LibraryScanner`） |
| P5.4 | 阅读进度自动保存/恢复 |
| P5.5 | 基础筛选（语言、分类） |

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
| P7.1 | 标签系统完善（中文映射、标签计数） |
| P7.2 | Series 分组管理 |
| P7.3 | 更多在线源（插件化） |
| P7.4 | 元数据编辑 |
| P7.5 | 深色/浅色主题 |

---

## 11. 关键设计决策记录

| 决策 | 结论 | 理由 |
|------|------|------|
| 本地和在线漫画是否统一表？ | ✅ 统一 `comic_resources` | 书架体验一致 |
| 收藏和下载是否独立？ | ✅ `is_bookmarked` ≠ `is_downloaded` | 用户可只收藏（在线读），以后再下载 |
| 同一本漫画的不同版本如何关联？ | ✅ `comic_works` 聚合 | nhentai + e-hentai + 本地 CBZ → 同一 Work |
| 同一网站重复上传如何防重？ | `UNIQUE(source_type, source_id)` + Work 推测 | 不入库重复资源；Work 层提示合并 |
| **多语言标签/作者如何处理？** | **Canonical(英文) + Alias(多语言映射)** | 英文覆盖面最广；alias 表支持反向查找和中文显示 |
| 未匹配的标签/作者怎么处理？ | 以原始名创建 canonical，标记 `needs_review` | 不阻塞导入流程，后续人工/社区完善 |
| 去重靠自动还是手动？ | 自动推测 + 手动确认 | 自动过于激进会误伤；手动是最终仲裁 |
| 进度关联什么？ | 关联 `resource_id` | 不同版本进度独立 |
| 在线阅读和本地阅读是否共用 ReaderView？ | ✅ 共用 | `IFileSource` 多态 |
| 系列是否强制？ | ❌ 可选 | 大量同人本是单本 |
