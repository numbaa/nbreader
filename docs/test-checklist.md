# NbReader 端到端测试清单（Phase 2 — 书架与持久化）

> 产品设计：[`product-design.md`](product-design.md) | 短期计划：[`short-term-plan.md`](short-term-plan.md)
> 已归档清单：[Phase 0→1](test-checklist-archive-phase0-1.md) | [Phase 1→4 旧](test-checklist-archive-phase1-4.md)
> 全量回归请用 [`test-checklist-full.md`](test-checklist-full.md)。

---

## 1. 持久化层

- [x] 应用首次启动 → 自动创建 `nbreader.db`，含完整 Schema（14 张表，含 `comic_sources` / `downloads` / `monitored_directories`）
- [x] `comic_resources` 表含 `volume_number` + `chapter_number`（双字段）和 `file_hash`
- [x] 关闭并重新打开 → 数据库不重建，数据保留
- [x] 设置变更（方向/适应模式）→ 重启后保持

---

## 2. 书架视图

### 2.1 基础展示
- [x] 导航栏点击「📚 书架」→ 显示书架视图
- [x] 空书架 → 显示空状态引导（"添加监控目录或拖放漫画文件"）
- [x] 网格视图：封面缩略图 + 标题 + 进度条
- [x] 列表视图：切换到 ☰ 显示标题、页数、进度

### 2.2 Category
- [x] 左侧栏显示默认分类（全部）
- [x] 「➕ 新建分类」→ 输入名称 → 分类出现在列表中
- [x] 右键分类 → 重命名 / 删除 / 上移 / 下移
- [x] 点击分类名 → 右侧筛选为该分类的漫画
- [x] 拖拽漫画卡片到分类名 → 漫画加入该分类

### 2.3 排序与筛选
- [x] 排序切换：最近阅读 / 最近添加 / 标题 A-Z / 页数
- [x] 点击 Chip × → 取消该筛选
- [x] 「+ 添加筛选」→ 弹出下拉面板，可按语言/类型筛选

### 2.4 漫画卡片交互
- [x] 双击卡片 → 打开阅读器
- [x] 右键卡片 → 菜单：阅读 / 从本分类移除 / 移至分类 → / 从书架移除

---

## 3. 阅读进度

- [x] 打开漫画，翻到第 N 页，关闭窗口
- [x] 重新打开同一漫画 → 自动恢复到第 N 页
- [x] 进度持久化到 SQLite（重启后仍有效）
- [x] 书架上卡片显示进度百分比/进度条

---

## 4. 阅读历史

- [x] 打开漫画阅读 → 历史中自动出现一条记录
- [x] 导航栏点击「🕐 历史」→ 显示历史视图
- [x] 历史按日期分组（今天/昨天/更早）
- [x] 同一天内按时间倒序排列
- [x] 悬停条目右侧 → ✕ 删除单条
- [x] 「清除全部历史」→ 确认后清空
- [x] 点击历史条目 → 打开漫画，断点续读

---

## 5. 本地扫描

- [x] 设置监控目录（如 `D:\Comics\`）
- [x] 启动或手动触发扫描 → 自动发现 CBZ/CBR/图片文件夹
- [x] 新发现的漫画自动入库，归入"未分类"
- [x] 封面自动提取并缓存（后端已实现，前端封面显示待修复）
- [x] 重复文件不重复入库（`UNIQUE(source_type, source_id)` + `file_hash` 校验）
- [x] CBZ 内含 `ComicInfo.xml` → 优先解析填充 Title/Series/Tags 等字段
- [x] `ComicInfo.xml` 标注 `Page Type="FrontCover"` → 提取该页为书架封面
- [ ] `ComicInfo.xml` 标注 `Page Type="Deleted"` → 阅读器中跳过该页
- [x] 无 ComicInfo.xml → 回退文件名推测元数据 + 第一页为封面

---

## 6. 回归验证（Phase 0→1 核心功能）

- [ ] CBZ/CBR/图片文件夹打开、翻页、缩放正常
- [ ] 适应模式循环切换正常（Uniform/FillWidth/FillHeight/Original）
- [ ] 单页/双页/滚动模式切换正常
- [ ] 阅读方向切换正常（L→R / R→L）+ 双页模式自动交换
- [ ] 全屏 F11 正常
- [ ] 拖放文件正常
- [ ] 错误处理正常（损坏文件、不支持的格式）

---

## 7. 元数据与文件指纹

- [ ] 导入本地 CBZ → `file_hash` 正确计算（SHA256 前 1MB）
- [ ] 导入图片文件夹 → `file_hash` 为文件名+大小排序 SHA256
- [ ] 在线源未下载 → `file_hash` 为 null
- [ ] ComicInfo.xml 字段映射正确（Title/Series/Number/Genre/Writer/LanguageISO/Manga）
- [ ] 下载打包 CBZ → 内含自动生成的 ComicInfo.xml

---

## 测试结果汇总

| 日期 | 通过项 | 失败项 | 备注 |
|------|--------|--------|------|
|      | /7     |        |      |
