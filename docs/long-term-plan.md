# NbReader 长期计划

> 产品设计详见 [`product-design.md`](product-design.md)。本文档追踪里程碑和整体进度。

---

## 愿景

打造一款跨平台（Windows / macOS / Linux）桌面漫画阅读器，统一管理本地收藏与在线漫画，提供沉浸式阅读体验。

---

## 里程碑路线图

```
┌─────────────┐    ┌─────────────────┐    ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  Phase 0    │───▶│  Phase 1        │───▶│  Phase 2         │───▶│  Phase 3         │───▶│  Phase 4         │
│  项目初始化  │    │  核心阅读体验     │    │  书架与持久化     │    │  在线漫画源       │    │  打磨与生态      │
│  (v0.1) ✅  │    │  (v0.5) ✅      │    │  (v1.0) ⬅ 当前   │    │  (v1.5)          │    │  (v2.0)          │
└─────────────┘    └─────────────────┘    └──────────────────┘    └──────────────────┘    └──────────────────┘
```

---

## Phase 0 — 项目初始化（v0.1）✅

| 任务 | 状态 |
|------|:----:|
| Avalonia MVVM 项目结构搭建 | ✅ |
| 引入 Avalonia UI、CommunityToolkit.Mvvm、xUnit | ✅ |
| 基础窗口（工具栏 + 内容区 + 状态栏） | ✅ |
| 单元测试基础设施 | ✅ |

---

## Phase 1 — 核心阅读体验（v0.5）✅

| 功能 | 状态 |
|------|:----:|
| CBZ / CBR / 图片文件夹 打开与渲染 | ✅ |
| 缩放（Ctrl+滚轮）、平移（中键拖拽） | ✅ |
| 翻页（← → PgUp PgDn Space） | ✅ |
| 适应模式（Uniform / FillWidth / FillHeight / Original） | ✅ |
| 阅读模式（单页 / 双页 / 滚动） | ✅ |
| 阅读方向（L→R / R→L） | ✅ |
| 全屏（F11） | ✅ |
| 拖放文件打开 | ✅ |
| 错误处理（损坏文件、不支持格式） | ✅ |
| 工具栏按钮（模式/方向/适应） | ✅ |
| 双页模式 R→L 交换 + 落单页对齐 | ✅ |

---

## Phase 2 — 书架与持久化（v1.0）⬅ 当前阶段

> 设计依据：[product-design.md §2 核心概念模型](product-design.md)、[§6 SQLite Schema](product-design.md)

| 功能 | 优先级 | 状态 |
|------|:------:|:----:|
| SQLite 持久化层（ComicWork + ComicResource + Tags + Categories + History + ComicInfo.xml） | P0 | ✅ |
| `LibraryView`（书架网格/列表 + 左侧 Category 栏） | P0 | ✅ |
| 导航系统（书架/历史/阅读器切换 + 快捷键） | P0 | ✅ |
| `HistoryView`（阅读历史按时间分组） | P1 | ✅ |
| 本地目录扫描与入库（`LibraryScanner`） | P0 | ✅ |
| 阅读进度自动保存与断点续读 | P0 | ✅ |
| Category 管理（新建/重命名/排序/删除/归类） | P1 | ✅ |
| 书架标签/语言/分类筛选 | P1 | ✅ |
| 书架搜索 | P2 | ✅ |
| 右键菜单（阅读/从本分类移除/移至分类/从书架移除） | P2 | ✅ |
| 打开文件自动入库 + 阅读历史自动记录 | P0 | ✅ |
| 本地漫画元数据编辑 | P2 | — |

### 验收标准 Phase 2

- [x] 能扫描目录、导入漫画、在书架中浏览
- [x] 关闭后再打开，阅读进度自动恢复
- [x] 可按 Category 分组、按语言/内容类型筛选
- [x] 阅读历史完整记录，点击续读
- [x] 所有单元测试通过（163 个），覆盖率 > 60%

---

## Phase 3 — 在线漫画源（v1.5）

> 设计依据：[product-design.md §5 nhentai 在线源设计](product-design.md)

| 功能 | 优先级 |
|------|:------:|
| `IComicSource` 接口 | P0 |
| `NhentaiSource` 实现（浏览/搜索/详情/阅读） | P0 |
| `SourceBrowserView`（在线浏览翻页列表） | P0 |
| 漫画详情页（封面 + 标签 + 操作按钮） | P0 |
| 在线阅读（NhentaiFileSource 流式加载） | P1 |
| 下载到书架（批量下载 + 打包 CBZ） | P1 |
| 内置 ~200 条标签中文映射（EhTagTranslation 数据导入） | P2 |
| Canonical + Alias 多语言标签系统 | P2 |

---

## Phase 4 — 打磨与生态（v2.0）

| 功能 | 优先级 |
|------|:------:|
| 深色 / 浅色主题 | P1 |
| 下载管理器（队列/暂停/优先级/通知） | P1 |
| 多源插件架构（IComicSource 社区扩展） | P2 |
| 设置页面（默认方向/适应模式/监控目录） | P1 |
| 备份与恢复（JSON 导出/导入） | P2 |
| ComicWork 手动合并/拆分 UI | P2 |
| Series 分组管理 | P2 |
| 数据备份恢复 | P2 |

---

## 非功能性目标

| 维度 | 目标 |
|------|------|
| 测试覆盖率 | > 60%（Phase 2）→ > 70%（Phase 4） |
| 启动时间 | < 2 秒 |
| 翻页延迟 | < 100ms（预加载后） |
| 内存 | 100 页漫画 < 500MB |
| 安装包 | < 100MB |

---

## 已完成汇总

**Phase 0-1 已交付**，64 个单元测试全部通过。归档材料：
- 测试清单：[`test-checklist-archive-phase0-1.md`](test-checklist-archive-phase0-1.md)、[`test-checklist-archive-phase1-4.md`](test-checklist-archive-phase1-4.md)
- 计划：[`short-term-plan-archive-phase0-1.md`](short-term-plan-archive-phase0-1.md)


