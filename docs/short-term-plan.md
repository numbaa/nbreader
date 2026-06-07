# NbReader 短期计划（Phase 1 收尾 → Phase 2 启动）

> 覆盖范围：补齐 Phase 1 遗留项 → 启动 Phase 2 增强阅读体验。
> 
> **当前进度：第 1-3 周已完成。** 第 4 周（书架/图书馆）待开始。

---

## 当前功能总览

| 功能 | 快捷键 | 状态 |
|------|--------|------|
| 打开 CBZ / CBR / 图片文件夹 | — | ✅ |
| 演示图片（Debug 专属） | — | ✅ |
| 适应模式（Uniform/FillWidth/FillHeight/Original） | `F` | ✅ |
| 阅读模式（单页/双页/滚动） | `M` | ✅ |
| 阅读方向（L→R / R→L） | `D` | ✅ |
| 缩放 + 平移 | Ctrl+滚轮 / 中键拖拽 | ✅ |
| 全屏 | `F11` | ✅ |
| 拖放文件 | — | ✅ |
| 错误处理 | — | ✅ |
| 滚动模式按需加载 | 自动 | ✅ |

**测试：64 个单元测试全部通过。**

---

## 第 1 周：Phase 1 收尾 — CBR 支持

### Day 1-2：CbrFileSource 实现 ✅

- [x] 实现 `CbrFileSource : IFileSource`
  - 基于 SharpCompress 的 RAR 解压（`SharpCompress.Archives.Rar.RarArchive`）
  - 内存中提取条目，过滤图片文件
  - 按条目名排序
- [x] 扩展 `FileSourceFactory` 路由：`.cbr` → `CbrFileSource`
- [x] 文件对话框添加 `.cbr` 过滤器

### Day 3：CBR 单元测试 ✅

- [x] 损坏 CBR → 抛出异常（SharpCompress InvalidFormatException）
- [x] 文件不存在 → FileNotFoundException
- [x] 工厂路由 `.cbr` → 正确创建 `CbrFileSource`
- [x] 通过伪造 .cbr 文件验证构造函数管道畅通
- [x] 注意：有效 RAR 的正向测试需要真实 .cbr 文件（SharpCompress 不支持 RAR 写入）

### Day 4-5：补充测试 & 打磨

- [ ] 加载遮罩层端到端验证（大文件场景）
- [ ] 修复 test-checklist 中搁置的检查项
- [ ] 更新 `test-checklist.md`：CBR 相关用例

---

## 第 2 周：图片适应模式 ✅

### Day 1-3：FitMode 实现

- [x] 在 `ReaderViewModel` 中添加 `FitMode` 枚举与属性
  - `Uniform`（等比缩放以适应视口）
  - `FillWidth`（适应宽度）
  - `FillHeight`（适应高度）
  - `Original`（原始尺寸 1:1）
  - `FitModeText` 计算属性（中文标签）
- [x] `ReaderView.ApplyFitMode()` → 根据视口计算目标缩放
- [x] 新图片加载时自动应用当前适应模式
- [x] `CycleFitModeCommand` → F 键循环切换

### Day 4-5：适应模式完善

- [x] 切换模式时缩放平滑过渡（视口中心锚点不变）
- [x] 状态栏显示当前适应模式（`FitModeText`）
- [x] 单元测试：FitMode 默认值、CycleFitMode 循环顺序、FitModeText 标签
- [x] 测试通过：57 个（新增 4 个 FitMode 测试）

---

## 第 3 周：阅读模式 ✅

### Day 1-3：单页/双页/滚动模式

- [x] `ReadingMode` 枚举：`SinglePage` / `DualPage` / `Scroll`
- [x] `ReadingDirection` 枚举：`LeftToRight` / `RightToLeft`
- [x] `DualPage` 实现：
  - 左右两个 `Image` 控件，封面页 (0) 单独居中，后续对开页 (1,2) (3,4)...
  - 翻页步长自适应：封面→(1,2) 步长 1，后续 ±2
  - 切换到双页时页码自动对齐
- [x] `Scroll` 实现：
  - 首屏加载 5 页，后续按需加载（滚到距末尾 1 屏时 +2 页）
  - `StackPanel` + 外层 `ScrollViewer` 滚动
  - 实时跟踪可视页码，状态栏 `第 3 页  [7/97]`
  - `ScrollBitmapsAppended` 事件驱动增量追加 Image
- [x] `ReloadPagesForCurrentModeAsync` 统一加载入口

### Day 4-5：阅读方向 & 打磨

- [x] `L→R` 默认（← 上页、→ 下页）
- [x] `R→L` 反转（← 下页、→ 上页），滚动模式下自动隐藏方向提示
- [x] 状态栏显示：模式、方向、适应模式、缩放、页码
- [x] 快捷键 `M` 循环模式、`D` 切换方向、`F` 循环适应模式
- [x] 修复：滚动模式下按 D + 翻页键崩溃（位图生命周期）
- [x] 修复：AXAML 中 `FitModeText` 重复绑定
- [x] 单元测试：+7 个（共 64 个）

---

## 第 4 周：书架/图书馆（v1.0 启动）

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

### Day 5：设置 & 主题

- [ ] 设置页面骨架（`SettingsViewModel` + `SettingsView`）
- [ ] 深色/浅色主题切换（Avalonia FluentTheme）
- [ ] 持久化用户偏好（JSON 文件）

---

## 关键设计决策

| 议题 | 方案 | 状态 |
|------|------|------|
| CBR 解压 | SharpCompress RarReader（已内置） | ⏳ |
| 双页模式 | 双 Image 控件 + ViewModel 协调 | ⏳ |
| 滚动模式 | StackPanel 懒加载（虚拟化） | ⏳ |
| 封面缓存 | 缩略图文件缓存到 `%APPDATA%/NbReader/thumbnails/` | ⏳ |
| 设置存储 | `System.Text.Json` → `%APPDATA%/NbReader/settings.json` | ⏳ |
| 主题 | Avalonia FluentTheme + 自定义 ResourceDictionary | ⏳ |

---

## 验收标准（v1.0）

- [ ] 能打开 CBR 文件并正常浏览
- [ ] 支持适应宽度/高度/原始大小模式，一键切换
- [ ] 支持单页/双页/滚动三种阅读布局
- [ ] 支持 L→R / R→L 阅读方向切换
- [ ] 书架扫描目录，显示封面网格
- [ ] 阅读进度自动保存和恢复
- [ ] 深色/浅色主题切换
- [ ] 所有单元测试通过，覆盖率 > 60%
- [ ] 端到端测试清单更新并通过
