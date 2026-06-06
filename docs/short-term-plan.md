# NbReader 短期计划（Phase 0 → Phase 1）

> 本计划覆盖从项目初始化到 MVP 本地阅读的完整流程，预计 4-6 周。

---

## 第 1 周：项目初始化（Phase 0）

### Day 1-2：项目脚手架 ✅

- [x] 使用 Avalonia UI 模板创建解决方案
  - `NbReader` — 主项目（Avalonia MVVM Application）
  - `NbReader.Core` — 核心逻辑库（类库）
  - `NbReader.Tests` — 单元测试项目（xUnit）
- [x] 配置 `.csproj` 依赖：
  - `Avalonia` 11.2.5 + `Avalonia.Desktop` 11.2.5 + `Avalonia.Fonts.Inter` 11.2.5
  - `CommunityToolkit.Mvvm` 8.4.1（MVVM 工具包）
  - `AvaloniaUI.DiagnosticsSupport` 2.2.1（开发调试）
  - `Microsoft.Extensions.DependencyInjection` 8.0.1
  - `Microsoft.Extensions.Logging.Console` 8.0.1
  - `SharpCompress` 0.39.0、`SkiaSharp` 3.116.1
- [x] 创建基础目录结构（见架构设计文档）
- [x] 配置 `.gitignore`、`Directory.Build.props`
- [x] 目标框架：`.NET 8.0`（LTS）

### Day 3-4：基础架构搭建 ✅

- [x] 实现 MVVM 基础类：
  - `ViewModelBase` — 基类（继承 `ObservableObject`）
  - `RelayCommand` / `AsyncRelayCommand`（CommunityToolkit.Mvvm 源生成）
- [x] ~~实现导航服务 `INavigationService`~~ → 改用 `ContentControl` + `DataTemplate` 按类型匹配
- [x] 创建 `MainWindow` + `MainWindowViewModel` + `ReaderViewModel`
- [x] 运行验证：窗口能正常启动

### Day 5：测试基础设施 ✅

- [x] 单元测试项目配置完成（xUnit + FluentAssertions）
- [x] 编写 `ReaderViewModel` 单元测试（6 个用例）
- [x] 确认 `dotnet test` 全部通过

---

## 第 2 周：图片渲染引擎

### Day 1-3：图片加载器 ✅

- [x] 实现 `IImageLoader` 接口 → `SkiaImageLoader`（`NbReader.Core/Services/`）
- [x] 实现 `SkiaImage`（包装 `SKBitmap`，`NbReader.Core/Services/`）
- [x] 支持格式：JPG, PNG, BMP, GIF, WebP（SkiaSharp 原生支持）
- [x] 灰度图自动转 BGRA8888
- [x] 实现 `ImageConverter`：SKBitmap → Avalonia Bitmap（`NbReader/Converters/`）
- [x] 单元测试：10 个用例（SkiaImageLoaderTests 6 + ImageConverterTests 4，含 Headless 平台初始化）

### Day 4-5：阅读器视图 ✅

- [x] 实现 `ReaderView` 用户控件（Avalonia UserControl）
  - `Stretch="Uniform"` + 修改 `Image.Width/Height` 实现真实缩放（非 RenderTransform）
  - 视口中心锚点缩放偏移补偿
  - Ctrl+鼠标滚轮缩放（Window 层 tunnel 拦截 → 改 VM.ZoomLevel → View 响应）
  - 普通滚轮由 ScrollViewer 原生滚动
  - 中键拖拽平移（ScrollViewer 隧道事件 + Pointer.Capture）
  - 键盘翻页（MainWindow tunnel KeyDown → VM 命令）
  - 键盘缩放（Ctrl+Plus/Minus/0 → VM 命令 → ApplyZoom）
- [x] `ReaderViewModel` 完整对接
  - `DisplayBitmap` 属性 → View 通过 PropertyChanged 监听自动更新
  - `ZoomLevel` 属性 → View 监听变化自动 ApplyZoom
  - `IImageLoader` 构造函数注入
  - `LoadFileSourceAsync` → 解码 → DisplayBitmap
  - `Close` 命令清理 IImage + DisplayBitmap 双资源
  - `LoadDemoImageAsync` → 内置 `DemoFileSource`（3 页内存图片，可翻页验证）
- [x] 单元测试：6 个用例（ReaderViewModel + FakeImageLoader）
- [x] 中文字体回退：App.axaml 全局 TextBlock/Button/Window 样式 + SkiaSharp 显式加载微软雅黑

---

## 第 3 周：文件格式解析

### Day 1-2：图片文件夹读取 ✅

- [x] 实现 `IFileSource` 接口
- [x] 实现 `DirectoryFileSource`
  - 扫描文件夹，收集图片文件
  - 按文件名自然排序
  - 支持子文件夹递归（可选）
- [x] 单元测试：排序逻辑、过滤非图片文件（12 个用例）

### Day 3-4：CBZ 格式支持 ✅

- [x] 引入 `SharpCompress` 处理 ZIP
- [x] 实现 `CbzFileSource`
  - 内存中解压 ZIP 条目
  - 过滤图片条目
  - 按名称排序
- [x] 处理边界情况：嵌套目录、非图片文件、损坏压缩包
- [x] 单元测试：正常 CBZ、空 CBZ、损坏 CBZ（12 个用例）

### Day 5：统一文件源 ✅

- [x] 实现 `FileSourceFactory` 工厂类
  - 根据文件扩展名自动选择 `IFileSource` 实现
  - 默认回退为目录模式
- [x] 单元测试：工厂路由逻辑（7 个用例）

---

## 第 4 周：整合 & 基础 UI

### Day 1-2：主窗口整合 ✅

- [x] 实现完整的 `MainWindow.axaml` 布局
  - 顶部工具栏（📂 打开文件 + 📁 打开文件夹 + 🎨 演示按钮）
  - 中央 `ReaderView`（通过 ViewLocator 自动匹配 ReaderViewModel）
  - 底部状态栏（页码、缩放百分比）
- [x] 实现 `MainWindowViewModel` 完整逻辑
  - `OpenFileAsync(path)` — 对接 FileSourceFactory + 文件对话框
  - `LoadDemoCommand` — 加载 3 页演示图片
  - 窗口标题绑定 `Reader.ComicName`
- [x] `OpenFileAsync` 对接实际文件对话框 + FileSourceFactory
- [x] 拖放文件到窗口打开

### Day 3：翻页功能 ✅

- [x] 实现 `GoToPrevPageCommand` / `GoToNextPageCommand`（ReaderViewModel）
- [x] 键盘快捷键（MainWindow tunnel KeyDown）：← → PgUp PgDn Space
- [x] 翻页边界处理（第一页/最后一页时命令 NOP）
- [x] 单元测试：ReaderViewModelTests 翻页边界 2 个用例

### Day 4-5：完善 & 打磨

- [x] 拖放文件到窗口打开
- [ ] 全屏模式（F11）
- [ ] 错误处理：文件不存在、格式不支持
- [ ] 加载状态提示
- [ ] 端到端集成测试
- [ ] 手动测试：多种格式漫画

---

## 关键设计决策（待确认）

| 议题 | 方案 | 状态 |
|------|------|------|
| IOC 容器 | `Microsoft.Extensions.DependencyInjection` 8.0.1 | ✅ 已集成 |
| 图片库 | SkiaSharp 3.116.1 | ✅ 已引用 |
| 压缩库 | `SharpCompress` 0.39.0（支持 RAR） | ✅ 已引用 |
| 配置存储 | JSON 文件 + `Microsoft.Extensions.Logging` | ✅ 已引用 |
| 日志 | `Microsoft.Extensions.Logging` 8.0.1 | ✅ 已引用 |
| 导航 | `ContentControl` + `DataTemplate`（无额外导航库）| ✅ 已确定 |

---

## 风险 & 缓解

| 风险 | 概率 | 缓解措施 |
|------|------|----------|
| CBZ 大文件内存溢出 | 中 | 实现流式读取 + LRU 缓存 |
| 图片解码性能瓶颈 | 低 | 后台线程解码 + 预加载 |
| Avalonia 平台兼容问题 | 中 | 只聚焦 Windows 先跑通，再扩展 |
| WebP 动画支持缺失 | 高 | Phase 1 只支持静态，后续迭代 |

---

## 验收标准（MVP v0.5）

- [x] 双击启动应用，显示空白窗口 → 目标：显示带工具栏的窗口
- [x] 能通过菜单/按钮打开 CBZ 文件
- [x] 能打开包含图片的文件夹
- [ ] 图片正确渲染，支持鼠标缩放和平移
- [ ] 能通过键盘/鼠标翻页
- [ ] 底部状态栏显示页码和文件名
- [ ] 所有单元测试通过
- [ ] 无明显崩溃或内存泄漏
