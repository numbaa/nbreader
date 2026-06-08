# NbReader 短期计划（Phase 5 — 书架/图书馆 → 设置）

> 覆盖范围：书架/图书馆 → 阅读进度 → 设置 & 主题。
> 
> **Phase 1→4 已完成并归档。** 详见 [`test-checklist-archive-phase1-4.md`](test-checklist-archive-phase1-4.md)。

---

## 当前功能总览

| 功能 | 快捷键 | 状态 |
|------|--------|------|
| 打开 CBZ / CBR / 图片文件夹 | — | ✅ |
| 演示图片（Debug 专属） | — | ✅ |
| 适应模式（Uniform/FillWidth/FillHeight/Original） | `F` / 工具栏按钮 | ✅ |
| 阅读模式（单页/双页/滚动） | `M` / 工具栏按钮 | ✅ |
| 阅读方向（L→R / R→L） | `D` / 工具栏按钮 | ✅ |
| 双页模式 R→L 自动交换左右页 | — | ✅ |
| 双页模式落单页左对齐 | — | ✅ |
| 滚动模式精确页码跟踪 | 自动 | ✅ |
| 缩放 + 平移 | Ctrl+滚轮 / 中键拖拽 | ✅ |
| 全屏 | `F11` | ✅ |
| 拖放文件 | — | ✅ |
| 错误处理 | — | ✅ |

**测试：64 个单元测试全部通过。**

---

## 第 1 周：书架/图书馆

### Day 1-2：目录扫描

- [ ] 实现 `LibraryScanner` 服务
  - 扫描指定目录，自动发现 CBZ/CBR/图片文件夹
  - 提取封面（每个源的第一页缩略图）
  - 缓存扫描结果（JSON）
- [ ] 实现 `LibraryViewModel` + `LibraryView`
  - 网格/列表视图展示发现的书目
  - 显示封面缩略图 + 标题 + 页数 + 上次阅读时间

### Day 3-4：阅读进度 & 书架集成

- [ ] 完善 `ReadingProgress` 模型（当前页码、总页数、最后阅读时间）
- [ ] 打开漫画时自动恢复上次阅读位置
- [ ] 从书架点击 → 跳转到阅读器并恢复进度

### Day 5：书架打磨

- [ ] 搜索/排序功能
- [ ] 空状态与错误状态 UI

---

## 第 2 周：设置 & 主题

### Day 1-3：设置页面

- [ ] 设置页面骨架（`SettingsViewModel` + `SettingsView`）
- [ ] 深色/浅色主题切换（Avalonia FluentTheme）
- [ ] 持久化用户偏好（JSON 文件 → `%APPDATA%/NbReader/settings.json`）
- [ ] 默认阅读方向可配置
- [ ] 默认适应模式可配置

### Day 4-5：回归测试 & 打磨

- [ ] 全量回归验证（[test-checklist-full.md](test-checklist-full.md)）
- [ ] 修复回归问题
- [ ] 更新文档

---

## 关键设计决策

| 议题 | 方案 | 状态 |
|------|------|------|
| 书架封面缓存 | 缩略图文件缓存到 `%APPDATA%/NbReader/thumbnails/` | ⏳ |
| 设置存储 | `System.Text.Json` → `%APPDATA%/NbReader/settings.json` | ⏳ |
| 主题 | Avalonia FluentTheme + 自定义 ResourceDictionary | ⏳ |
| 阅读进度存储 | JSON 文件，按文件路径索引 | ⏳ |

---

## 验收标准（v1.0）

- [ ] 书架扫描目录，显示封面网格
- [ ] 阅读进度自动保存和恢复
- [ ] 深色/浅色主题切换
- [ ] 所有单元测试通过，覆盖率 > 60%
- [ ] 端到端测试清单更新并通过
