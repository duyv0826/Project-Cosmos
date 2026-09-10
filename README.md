# Project-Cosmos

> **占位名**：若游戏确定正式名称，更新本标题、仓库名与 `Docs/` 内标题并全仓库替换即可。

基于 **Unity** 开发的原创游戏项目。当前处于 **设计 / 原型阶段**。

## 一句话简介

（待填写）用一句话说明这是个什么游戏、玩法核心、目标平台。

## 仓库结构

```
Project-Cosmos/
├── README.md          # 项目主页（本文件）
├── Docs/              # 设计文档（含评审留档）
│   ├── GameDesign/    # 玩法设计：GDD、关卡、系统
│   ├── Art/           # 美术风格、角色/场景设定
│   ├── Tech/          # 技术方案：架构、管线、构建
│   └── Meta/          # 项目管理：里程碑、会议纪要、复盘
├── Assets/            # Unity 资源（由 Unity Hub 打开工程后生成/维护）
│   ├── Scenes/        # 场景
│   ├── Scripts/       # C# 脚本
│   ├── Prefabs/       # 预制体
│   ├── Art/           # 美术资源
│   └── Audio/         # 音频
├── Packages/          # Unity 包依赖（由 Unity 生成）
├── ProjectSettings/   # Unity 工程设置（由 Unity 生成）
└── .gitignore         # Unity 专用忽略规则
```

## 如何用 Unity 打开

1. 打开 **Unity Hub**。
2. Add → Add project from disk → 指向本 `Project-Cosmos` 文件夹。
3. 选择匹配的 Unity 版本 → 打开。
   > `Assets/`、`Packages/`、`ProjectSettings/` 会由 Unity 首次打开时生成，无需手动创建。

## 当前进度

- [ ] 设计文档初稿
- [ ] 首个可玩原型
- [ ] 核心玩法验证

## 文档索引

| 文档 | 位置 | 状态 |
|------|------|------|
| 游戏设计文档 GDD | `Docs/GameDesign/GDD.md` | 待填写 |
| 玩法 / 核心循环 | `Docs/GameDesign/Gameplay.md` | 待填写 |
| 技术方案 | `Docs/Tech/Architecture.md` | 待填写 |
| 里程碑规划 | `Docs/Meta/Milestones.md` | 待填写 |