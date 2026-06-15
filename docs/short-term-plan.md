# NbReader 短期计划（Phase 2 — 书架与持久化）

> 产品设计：[`product-design.md`](product-design.md) | 长期计划：[`long-term-plan.md`](long-term-plan.md)
> Phase 0-1 已交付并归档。

---

## 当前功能总览

| 功能 | 状态 |
|------|:----:|
| CBZ / CBR / 图片文件夹打开 | ✅ |
| 适应模式（Uniform / FillWidth / FillHeight / Original） | ✅ |
| 阅读模式（单页 / 双页 / 滚动） | ✅ |
| 阅读方向（L→R / R→L）+ 双页自动交换 | ✅ |
| 工具栏按钮（方向/模式/适应） | ✅ |
| 全屏、缩放、平移、拖放 | ✅ |
| 错误处理 | ✅ |
| SQLite 持久化（14 张表，完整 CRUD） | ✅ |
| ComicInfo.xml 解析/生成 | ✅ |
| 左侧导航栏（书架 / 历史） | ✅ |
| 书架网格视图（封面+标题+进度条） | ✅ |
| 书架列表视图 | ✅ |
| Category 侧栏 + 新建分类 | ✅ |
| 排序（最近阅读/最近添加/标题/页数） | ✅ |
| 标题搜索 | ✅ |
| 阅读历史视图（按日期分组 + 点击续读） | ✅ |
| 漫画右键菜单（阅读/从本分类移除/移至分类/从书架移除） | ✅ |
| 打开文件自动入库 | ✅ |
| 📖 正在阅读 导航按钮 | ✅ |
| 窗口标题自适应（书架/历史/阅读器） | ✅ |
| 工具栏按钮按视图显示/隐藏 | ✅ |

---

## 第 1 周：持久化层 + 数据模型

### Day 1-2：SQLite 基础设施

- [x] 添加 `Microsoft.Data.Sqlite` 包到 `NbReader.Core`
- [x] 实现 `SqliteStorageService : IStorageService, IDisposable`
  - 启动时自动建表（完整 Schema：`comic_works` / `comic_resources`（含 `volume_number` + `chapter_number` + `file_hash`）/ `series` / `tags` / `entity_aliases` / `categories` / `resource_tags` / `resource_categories` / `reading_progress` / `read_history` / `monitored_directories` / `comic_sources` / `downloads` / `settings`）
  - 所有写操作参数化 SQL
- [x] DI 注册为单例，`Data Source={AppData}/NbReader/nbreader.db`

### Day 3：IStorageService 接口实现 + ComicInfo.xml 解析

- [x] 实现 `ComicInfoXmlParser : IComicInfoParser`
  - 从 CBZ 内解析 `ComicInfo.xml` → `ComicInfoData`
  - 支持 Page Type 标注（`FrontCover` / `Deleted` 等）
  - 生成 ComicInfo.xml（下载打包时使用）
- [x] 设置读写（`GetSetting` / `SetSetting`）
- [x] 漫画 CRUD（`AddResource` / `UpdateResource` / `GetLibrary` / `SearchLibrary`）
- [x] 进度读写（`GetProgress` / `SaveProgress`）
- [x] 历史追加（`AddHistory` / `GetHistory`）
- [x] 分类管理（`GetCategories` / `CreateCategory` / `DeleteCategory` / `ReorderCategories`）
- [x] 分类与漫画关联（`AddToCategory` / `RemoveFromCategory`）

### Day 4-5：单元测试

- [x] `SqliteStorageServiceTests` — 使用 `Data Source=:memory:` 覆盖所有 CRUD 操作
- [x] 建表幂等、参数化 SQL 防注入、并发安全

---

## 第 2 周：书架 UI

### Day 1-3：LibraryView 骨架 + 导航重构

- [x] `MainWindowViewModel`：导航系统（NavTarget / SelectedNav / 回调）
- [x] `MainWindow.axaml`：三栏布局（左侧导航栏 + 内容区 + 状态栏）
- [x] `LibraryViewModel`：加载漫画列表、Category 列表、筛选/排序逻辑
- [x] `LibraryView.axaml`：左侧 Category 栏 + 右侧网格视图
- [x] `HistoryViewModel` + `HistoryView.axaml`：按日期分组历史列表
- [x] 漫画卡片组件（封面占位 + 标题 + 进度条）
- [x] 网格/列表视图切换（`▦` / `☰`）
- [x] 排序（最近阅读 / 最近添加 / 标题 / 页数）
- [x] 搜索框 + 搜索 / 清除按钮
- [x] 导航快捷键（Ctrl+N 书架 / Ctrl+H 历史）
- [x] 窗口标题随视图切换（书架/历史/阅读器）
- [x] 工具栏阅读按钮仅在阅读器视图显示
- [x] 修复 ListBox → ItemsControl（消除分类列表双击蓝框）

### Day 4：Category 交互

- [x] 左侧栏：点击分类筛选、右键重命名/删除/排序
- [x] 双击漫画卡片 → 打开阅读器
- [x] 漫画卡片右键菜单（阅读 / 从本分类移除 / 移至分类 / 从书架移除）
- [ ] 拖拽卡片到分类名 → 归类（待 Phase 2 后续）
- [x] 「➕ 新建分类」

### Day 5：筛选与搜索

- [ ] 筛选栏 Chip 组件（标签/语言/分类，点击切换，× 取消）
- [ ] 「+ 添加筛选」下拉面板（按类型分组，显示计数）
- [x] 标题搜索（标题模糊匹配）

---

## 第 3 周：阅读进度 + 历史 + 集成

### Day 1-2：阅读进度集成

- [x] `ReaderViewModel` 引入 `IStorageService`
- [x] 打开漫画时：`GetProgress(resourceId)` → 有记录则跳转
- [x] 关闭漫画时：`SaveProgress(resourceId, page, total)`
- [x] 设置变更即时持久化（方向/适应模式）
- [x] 📂 打开文件 → 自动入库到书架
- [x] ReaderView 常驻不销毁（可见性切换，解决首页灰色问题）

### Day 3：阅读历史

- [x] 打开漫画时自动追加 `read_history` 记录
- [x] `HistoryViewModel` + `HistoryView`：按日期分组列表
- [x] 点击续读、悬停删除单条、清除全部

### Day 4：本地扫描入库

- [ ] `LibraryScanner`：扫描监控目录 → 发现 CBZ/CBR/文件夹
- [ ] 导入时按优先级提取元数据：ComicInfo.xml > 文件名推测
- [ ] 自动提取封面（ComicInfo `FrontCover` 标注 > 第一页）→ 缓存到 `%APPDATA%/NbReader/covers/`
- [ ] 计算 `file_hash`（CBZ/CBR: SHA256 前 1MB；文件夹: SHA256(文件名+大小)）
- [ ] 新建 `ComicResource` + 关联 Category "未分类"
- [ ] 重复文件跳过（`UNIQUE(source_type, source_id)` + hash 辅助判断）

### Day 5：端到端验证 + 测试

- [ ] 手动测试：扫描目录 → 书架展示 → 打开阅读 → 关闭 → 进度恢复
- [ ] 手动测试：Category 创建/归类/筛选
- [ ] 手动测试：阅读历史记录与清除
- [ ] 更新 `test-checklist.md`

---

## 关键设计决策

| 议题 | 方案 | 参考 |
|------|------|------|
| 持久化后端 | SQLite 单文件 (`nbreader.db`) | [product-design.md §6](product-design.md) |
| 数据模型 | ComicWork + ComicResource 双层 | [product-design.md §2](product-design.md) |
| 封面缓存 | `%APPDATA%/NbReader/covers/{resource_id}.webp` | — |
| 标签中文映射 | `entity_aliases` 表，后续导入 EhTagTranslation | [product-design.md §2.7](product-design.md) |
| 进度保存策略 | 翻页不写库，关闭漫画时一次性写入 | 避免 UI 线程阻塞 |

---

## 验收标准

- [ ] SQLite 建表正确，`SqliteStorageService` 单元测试全部通过
- [ ] 书架能展示本地漫画，封面 + 标题 + 进度条正确
- [ ] Category 可创建/重命名/删除/排序，卡片可拖拽归类
- [ ] 阅读进度自动保存，重新打开漫画恢复位置
- [ ] 阅读历史按日期分组展示，点击可续读
- [ ] 筛选（标签/语言/分类）正常工作
- [ ] 所有单元测试通过，覆盖率 > 60%
