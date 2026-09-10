# 技术方案 · 影子同步（ShadowSync）

> 对应 GDD 核心机制 B「影子同步」，里程碑 1 的关键脚本。
> 目标：让**影子永远滞后半拍复读玩家输入**，并成为关卡机关（双开关等）的载体。

---

## 1. 核心思路一句话

玩家输入会被记录进一个**固定长度的动作队列**（FIFO）；影子不读"当前指令"，而是读**队列里最旧的那一条**——自然就形成了"滞后 N 拍"。影子走过的位置 = 玩家 N 拍前的位置。

---

## 2. 滞后量的实现方式（三种，选一）

| 方式 | 原理 | 适用 | 备注 |
|------|------|------|------|
| **同步位移** | 玩家每移动一格，把旧位置压进队列，影子 = 队列头 | 网格/走格制（推荐首版） | 最简单、可预测，适合验证玩法 |
| **计时延迟** | 记录玩家世界坐标和时间戳，影子回放到 `T - N秒` | 平滑连续移动 | 更顺滑但易漂移，调试成本高 |
| **录制回放** | 录制一段玩家轨迹，影子从头播放 | 固定演出/NPC | 不灵活，不适合随时解锁 |

> **首版建议**：用**同步位移 + 队列**。它完全体现"滞后半拍"的核心，且确定性强，方便调机关。等玩法验证后再考虑平滑计时版。

---

## 3. 数据结构（同步位移版）

用队列保存最近 `n` 步玩家位置，*先进先出*：

```
Player 移动一步
    → 把 Player 当前格子压入 queue 尾部
    → 若 queue 长度 > n，弹出队头
    → Shadow 移动到 queue 队头（即 n 步前的位置）
```

- `n` = 滞后步数（首版固定 `n=1`，即"半拍/一步"）。
- 影子**只复制位置，不复制朝向/状态**——这让"诱导影子望向某处"成为玩家需要思考的挑战。

---

## 4. 关键脚本（原型骨架）

**PlayerController.cs（玩家移动 + 驱动队列）**
```csharp
using UnityEngine;

public class PlayerController : MonoBehaviour {
    public float moveStep = 1f;
    private Vector2Int _pos; // 网格坐标

    void Update() {
        Vector2Int dir = ReadInput(); // 读方向键/AD，返回一格位移
        if (dir != Vector2Int.zero) {
            _pos += dir;
            transform.position = new Vector3(_pos.x, _pos.y, 0);
            ShadowSync.Instance.PushStep(_pos); // 把这一步交给影子队列
        }
    }
}
```

**ShadowSync.cs（影子滞后队列，单例）**
```csharp
using System.Collections.Generic;
using UnityEngine;

public class ShadowSync : MonoBehaviour {
    public static ShadowSync Instance;
    public int lagSteps = 1;
    public Transform shadow;
    private Queue<Vector2Int> _queue = new Queue<Vector2Int>();

    void Awake() { Instance = this; }

    public void PushStep(Vector2Int playerPos) {
        _queue.Enqueue(playerPos);
        while (_queue.Count > lagSteps) {
            _queue.Dequeue();
        }
        // 若队列未满（刚开场），影子停在起点不动
        if (_queue.Count == lagSteps) {
            shadow.position = new Vector3(_queue.Peek().x, _queue.Peek().y, 0);
        }
    }
}
```

> 说明：`lagSteps=1` 时，影子 = 你**上一步**的位置；这样"你和影子同时站上两个开关"这种机关才能成立：你移动到开关 A，影子在下一拍停在你的旧位置＝开关 B。

---

## 5. 机关设计的技术前提（双开关）

"你和影子各踩一个开关"需要两个判定：
- **玩家开关**：正常检测玩家 `BoxCollider2D` 叠在开关上。
- **影子开关**：检测**影子**的碰撞体（影子被判定感压），当影子停在开关上而玩家不在 → 触发。
- 触发条件：两个开关**同时**被踩住 → 开门。

> 注意：影子的 `Rigidbody2D` 设 `kinematic`（不被重力影响、只被代码移动），否则会飘落进地面。

---

## 6. 边界与坑（重点）

| 坑 | 说明 / 对策 |
|----|------|
| **开局队列空** | 开场影子停在起点，`PushStep` 里 `while` 只保证不超长，需另处理"队列未满时影子不动"（见骨架注释） |
| **影子穿墙** | 队列存的是"合法移动过"的位置，理论上不穿墙；但若机关让影子落到不可走格，需在 `PushStep` 前做碰撞判定 |
| **影子不准被玩家推走** | 影子 `kinematic`，物理上不与玩家碰撞体产生推挤 |
| **挂机/停滞** | 若玩家长时间不动、影子已到队头，需保证 UI/机关状态稳定，不无限累积 |
| **半拍 vs 一拍** | 首版就按"一步"做，"半拍"是抽象表达；如需真正的半拍可调成时间制的 `计时延迟`，但建议后置 |

---

## 7. 验收标准（对应里程碑 1）

- [ ] 玩家可以移动，影子严格滞后 `n=1` 步跟随
- [ ] 双开关机关：玩家踩 A + 影子停在 B → 门开启
- [ ] 通关后影子短暂与玩家"重合同步"，触发一次反馈（作为情绪节拍）
- [ ] Unity 编辑器内运行无报错

---

## 8. 相关文件

- 文档：`Docs/GameDesign/GDD.md`（机制 B）、`Docs/Meta/Milestones.md`（里程碑 1）
- 脚本：`Assets/Scripts/PlayerController.cs`、`Assets/Scripts/ShadowSync.cs`