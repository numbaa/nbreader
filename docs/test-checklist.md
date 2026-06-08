# NbReader 端到端测试清单（Phase 5 — 书架/图书馆 → 设置）

> 当前阶段：书架/图书馆 → 阅读进度 → 设置 & 主题。
> 已归档清单：[Phase 0→1](test-checklist-archive-phase0-1.md) | [Phase 1→4](test-checklist-archive-phase1-4.md)
> 全量回归请用 [`test-checklist-full.md`](test-checklist-full.md)。

---

## 1. 书架/图书馆

- [ ] 切换到书架视图 → 显示欢迎/空状态
- [ ] 添加监控目录 → 自动扫描 CBZ/CBR/图片文件夹
- [ ] 网格视图显示封面缩略图 + 标题 + 页数
- [ ] 列表视图可选
- [ ] 点击书目 → 打开阅读器并跳转到该书
- [ ] 上次阅读进度自动恢复

---

## 2. 阅读进度

- [ ] 打开漫画，翻到第 N 页，关闭窗口
- [ ] 重新打开同一漫画 → 自动恢复到第 N 页
- [ ] 进度信息持久化到磁盘（重启后仍有效）
- [ ] 在书架上显示进度百分比/页码

---

## 3. 设置 & 主题

- [ ] 打开设置页面（齿轮图标/菜单）
- [ ] 切换深色/浅色主题 → 即时生效
- [ ] 设置项持久化（重启后保持）
- [ ] 默认阅读方向可配置
- [ ] 默认适应模式可配置

---

## 4. 回归验证（Phase 0→4 核心功能）

> 详细清单见 [`test-checklist-archive-phase0-1.md`](test-checklist-archive-phase0-1.md) 和 [`test-checklist-archive-phase1-4.md`](test-checklist-archive-phase1-4.md)，此处仅验证关键路径。

- [ ] CBZ/CBR/图片文件夹打开、翻页、缩放正常
- [ ] 适应模式循环切换正常（Uniform/FillWidth/FillHeight/Original）
- [ ] 单页/双页/滚动模式切换正常
- [ ] 阅读方向切换正常（L→R / R→L）
- [ ] 全屏 F11 正常
- [ ] 拖放文件正常
- [ ] 错误处理正常（损坏文件、不支持的格式）

---

## 测试结果汇总

| 日期 | 通过项 | 失败项 | 备注 |
|------|--------|--------|------|
|      | /4     |        |      |
