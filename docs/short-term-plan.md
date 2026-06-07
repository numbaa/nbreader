# NbReader 短期计划（Phase 1 收尾 → Phase 2 启动）

> 覆盖范围：补齐 Phase 1 遗留项 → 启动 Phase 2 增强阅读体验。预计 4-6 周。

---

## 第 1 周：Phase 1 收尾 — CBR 支持

### Day 1-2：CbrFileSource 实现

- [ ] 实现 `CbrFileSource : IFileSource`
  - 基于 SharpCompress 的 RAR 解压（`SharpCompress.Readers.RarReader`）
  - 内存中提取条目，过滤图片文件
  - 按条目名排序
  - 复用与 `CbzFileSource` 相似的逻辑结构（可提取公共基类）
- [ ] 扩展 `FileSourceFactory` 路由：`.cbr` → `CbrFileSource`
- [ ] 文件对话框添加 `.cbr` 过滤器

### Day 3：CBR 单元测试

- [ ] 正常 CBR（含多张图片）→ 正确解析页码、排序
- [ ] 空 CBR（无图片条目）→ PageCount = 0
- [ ] 损坏 CBR → 抛出明确异常
- [ ] 嵌套目录 CBR → 递归收集图片
- [ ] FileSourceFactory 路由 `.cbr` → 正确创建 `CbrFileSource`

### Day 4-5：补充测试 & 打磨

- [ ] 加载遮罩层端到端验证（大文件场景）
- [ ] 修复 test-checklist 中搁置的检查项
- [ ] 更新 `test-checklist.md`：CBR 相关用例

---

## 第 2 周：图片适应模式

### Day 1-3：FitMode 实现

- [ ] 在 `ReaderViewModel` 中添加 `FitMode` 属性
  - `Uniform`（当前，等比缩放以适应视口）
  - `FillWidth`（适应宽度，高度可能超出）
  - `FillHeight`（适应高度，宽度可能超出）
  - `Original`（原始尺寸，1:1 像素）
- [ ] `ReaderView` 响应 `FitMode` 变化，动态调整 `Stretch` 和 `Image` 尺寸
- [ ] 适配模式切换按钮/快捷键（如 `F` 键循环切换）

### Day 4-5：适应模式完善

- [ ] 切换模式时保持缩放锚点（视觉中心不跳动）
- [ ] 状态栏显示当前适应模式
- [ ] 单元测试：FitMode 切换逻辑、边界条件

---

## 第 3 周：阅读模式

### Day 1-3：单页/双页/滚动模式

- [ ] 在 `ReaderViewModel` 中添加 `ReadingMode` 属性
  - `SinglePage`（当前，一次显示一页）
  - `DualPage`（左右两页并排，横屏优化）
  - `Scroll`（连续垂直滚动，无缝阅读）
- [ ] `DualPage` 实现：
  - 左右两个 `Image` 控件，分别显示第 N 页和第 N+1 页
  - 第一页（封面）单独居中
  - 翻页 +2 / -2
- [ ] `Scroll` 实现：
  - 所有页面垂直排列，使用 `StackPanel` + `ScrollViewer`
  - 懒加载：只解码可见区域的图片
  - 滚动位置即阅读进度

### Day 4-5：阅读方向

- [ ] 添加 `ReadingDirection` 属性：`LeftToRight` / `RightToLeft`
- [ ] 影响翻页逻辑（← / → 的含义互换）
- [ ] 双页模式下左右页顺序反转
- [ ] 工具栏添加模式切换按钮

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
