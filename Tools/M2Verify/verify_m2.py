#!/usr/bin/env python3
"""M2「余温回廊」设计稿核验器（纯规则，不依赖 Unity）。

用途：把 Docs/GameDesign/LevelDesign-M2-AfterglowCorridor.md 里的五张逐拍表、
     几何断言、门参数、无绕过通路、余量算术与落锁边界，用可执行的方式复核一遍。

跑法：python verify_m2.py   （期望：通过 118 条，失败 0 条；失败时退出码 1）
"""

from __future__ import annotations

# ---------------------------------------------------------------- 关卡数据
# 与文档 §4 §5 §6 的网格图逐格一致；y 向上为正（y2 在最上）。
ROOM_A = {
    "w": 7, "h": 2,
    "cells": {
        (0, 1): "#", (1, 1): "#", (2, 1): "h1", (3, 1): "#", (4, 1): "#", (5, 1): "#", (6, 1): "#",
        (0, 0): ".", (1, 0): ".", (2, 0): ".", (3, 0): "S1", (4, 0): ".", (5, 0): "G1", (6, 0): "La",
    },
    "start": (0, 0),
    "plate": {"S1": (3, 0)},
    "door": {"G1": (5, 0)},
    "latch": {},
    "light": [(6, 0)],
    "collect": {"h1": (2, 1)},
}

ROOM_B = {
    "w": 7, "h": 3,
    "cells": {
        (0, 2): "#", (1, 2): "#", (2, 2): "#", (3, 2): "S3", (4, 2): "h2", (5, 2): "#", (6, 2): "#",
        (0, 1): "#", (1, 1): "#", (2, 1): "#", (3, 1): ".", (4, 1): "#", (5, 1): "#", (6, 1): "#",
        (0, 0): ".", (1, 0): ".", (2, 0): ".", (3, 0): ".", (4, 0): ".", (5, 0): "G2", (6, 0): "Lb",
    },
    "start": (0, 0),
    "plate": {"S3": (3, 2)},
    "door": {"G2": (5, 0)},
    "latch": {},
    "light": [(6, 0)],
    "collect": {"h2": (4, 2)},
}

ROOM_C = {
    "w": 7, "h": 3,
    "cells": {
        (0, 2): "#", (1, 2): "#", (2, 2): "#", (3, 2): "#", (4, 2): "#", (5, 2): "#", (6, 2): "Lc",
        (0, 1): "#", (1, 1): "#", (2, 1): "B2", (3, 1): "A2", (4, 1): "D2", (5, 1): ".", (6, 1): "G3",
        (0, 0): ".", (1, 0): ".", (2, 0): ".", (3, 0): "#", (4, 0): "#", (5, 0): "S4", (6, 0): "#",
    },
    "start": (0, 0),
    "plate": {"S4": (5, 0)},
    "door": {"G3": (6, 1)},
    "latch": {"B2": (2, 1), "A2": (3, 1), "D2": (4, 1)},
    "light": [(6, 2)],
    "collect": {},
}

ROOMS = {"A": ROOM_A, "B": ROOM_B, "C": ROOM_C}

DELTA = {"U": (0, 1), "S": (0, -1), "A": (-1, 0), "D": (1, 0)}  # U/D 与 W/S 等价，统一用 U/S
DIRS = ((0, 1), (0, -1), (-1, 0), (1, 0))

# 文档里的门参数
OPEN_HOLD = {"A": 4, "B": 5, "C": 4}

# ---------------------------------------------------------------- 逐拍表
# 文档三张表（输入序列），以及表里写死的期望值。
CASES = [
    {
        "name": "室A 主线",
        "room": "A",
        "inputs": "DDDDDD",
        "expect_player": [(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0)],
        "expect_shadow": [(0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0)],
        "expect_press": 4,
        "expect_player_entry": 5,
        "expect_shadow_entry": 6,
        "collect": [],
    },
    {
        "name": "室A 绕 h1 拿光尘 1",
        "room": "A",
        "inputs": "DDUSDDDD",
        "expect_player": [(1, 0), (2, 0), (2, 1), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0)],
        "expect_shadow": [(0, 0), (1, 0), (2, 0), (2, 1), (2, 0), (3, 0), (4, 0), (5, 0)],
        "expect_press": 6,
        "expect_player_entry": 7,
        "expect_shadow_entry": 8,
        "collect": ["h1"],
    },
    {
        "name": "室B 主线",
        "room": "B",
        "inputs": "DDDUUSSDDD",
        "expect_player": [(1, 0), (2, 0), (3, 0), (3, 1), (3, 2), (3, 1), (3, 0), (4, 0), (5, 0), (6, 0)],
        "expect_shadow": [(0, 0), (1, 0), (2, 0), (3, 0), (3, 1), (3, 2), (3, 1), (3, 0), (4, 0), (5, 0)],
        "expect_press": 6,
        "expect_player_entry": 9,
        "expect_shadow_entry": 10,
        "collect": [],
    },
    {
        "name": "室B 高手解（先拿 h2，窗口跟着最后一次压板）",
        "room": "B",
        "inputs": "DDDUUDASSDDD",
        "expect_player": [(1, 0), (2, 0), (3, 0), (3, 1), (3, 2), (4, 2), (3, 2), (3, 1), (3, 0), (4, 0), (5, 0), (6, 0)],
        "expect_shadow": [(0, 0), (1, 0), (2, 0), (3, 0), (3, 1), (3, 2), (4, 2), (3, 2), (3, 1), (3, 0), (4, 0), (5, 0)],
        "expect_press": 8,
        "expect_player_entry": 11,
        "expect_shadow_entry": 12,
        "collect": ["h2"],
    },
    {
        "name": "室C 主线",
        "room": "C",
        "inputs": "DDUDDDSUDU",
        "expect_player": [(1, 0), (2, 0), (2, 1), (3, 1), (4, 1), (5, 1), (5, 0), (5, 1), (6, 1), (6, 2)],
        "expect_shadow": [(0, 0), (1, 0), (2, 0), (2, 1), (3, 1), (4, 1), (5, 1), (5, 0), (5, 1), (6, 1)],
        "expect_press": 8,
        "expect_player_entry": 9,
        "expect_shadow_entry": 10,
        "collect": [],
    },
]

FAIL: list[str] = []
PASS = 0


def ok(cond: bool, msg: str) -> None:
    global PASS
    if cond:
        PASS += 1
    else:
        FAIL.append(msg)


def is_wall(room, p) -> bool:
    c = room["cells"].get(p)
    return c is None or c == "#"


def run_case(case) -> None:
    room = ROOMS[case["room"]]
    pos = room["start"]
    shadow = room["start"]  # 开局重合、队列空
    hist_p, hist_s = [pos], [shadow]
    for i, ch in enumerate(case["inputs"], start=1):
        d = DELTA[ch]
        nxt = (pos[0] + d[0], pos[1] + d[1])
        ok(not is_wall(room, nxt), f'{case["name"]} 拍{i} 撞墙：{pos} + {ch} -> {nxt}')
        pos = nxt
        shadow = hist_p[-1]  # lag = 1
        hist_p.append(pos)
        hist_s.append(shadow)

    ok(hist_p[1:] == case["expect_player"], f'{case["name"]} 玩家逐拍位置与文档不符')
    ok(hist_s[1:] == case["expect_shadow"], f'{case["name"]} 影子逐拍位置与文档不符')

    # 压板拍 = 影子位于板格的拍（末次为准）
    plate = next(iter(room["plate"].values()))
    presses = [t for t in range(1, len(hist_s)) if hist_s[t] == plate]
    ok(bool(presses), f'{case["name"]} 影子从未压板')
    ok(presses and presses[-1] == case["expect_press"],
       f'{case["name"]} 末次压板拍 {presses[-1] if presses else None} != 文档 {case["expect_press"]}')

    door = next(iter(room["door"].values()))
    t_player = next((t for t in range(1, len(hist_p)) if hist_p[t] == door), None)
    t_shadow = next((t for t in range(1, len(hist_s)) if hist_s[t] == door), None)
    ok(t_player == case["expect_player_entry"],
       f'{case["name"]} 玩家穿门拍 {t_player} != 文档 {case["expect_player_entry"]}')
    ok(t_shadow == case["expect_shadow_entry"],
       f'{case["name"]} 影子穿门拍 {t_shadow} != 文档 {case["expect_shadow_entry"]}')

    # 窗口覆盖：窗口 = 末次压板拍 .. +openHold-1
    hold = OPEN_HOLD[case["room"]]
    p = presses[-1]
    win = (p, p + hold - 1)
    ok(t_player is not None and win[0] <= t_player <= win[1],
       f'{case["name"]} 玩家穿门拍 {t_player} 不在窗口 {win}')
    ok(t_shadow is not None and win[0] <= t_shadow <= win[1],
       f'{case["name"]} 影子穿门拍 {t_shadow} 不在窗口 {win}（门会先落锁，影子被关在门外）')

    # 收集
    for tag in case["collect"]:
        cell = room["collect"][tag]
        ok(cell in hist_p, f'{case["name"]} 声称拿了 {tag}，但路径没经过 {cell}')


def check_geometry() -> None:
    """文档里靠"几何锁死唯一解"立论的四处，逐个复核邻格。"""
    def nb(room, p):
        out = set()
        for d in DIRS:
            q = (p[0] + d[0], p[1] + d[1])
            if not is_wall(room, q):
                out.add(q)
        return out

    # 室A：走廊唯一通路，主路必经 S1（S1 在 y0 上，y0 只有 h1 一个岔口）
    ok(nb(ROOM_A, (2, 1)) == {(2, 0)}, "室A h1 不是只有一个邻居")
    ok(nb(ROOM_A, (0, 0)) == {(1, 0)}, "室A 起点不是走廊端点")

    # 室B：h2 唯一邻居是 S3；S3 只有 (3,1) 一个出口
    ok(nb(ROOM_B, (4, 2)) == {(3, 2)}, "室B h2 不是只有 S3 一个邻居")
    ok(nb(ROOM_B, (3, 2)) == {(3, 1), (4, 2)}, "室B S3 邻格与文档不符")
    ok(ROOM_B["cells"].get((5, 1)) == "#", "室B (5,1) 应为墙（G2 只能从西侧进）")

    # 室C：A2 四邻唯一通路是 B2（4,1 是门、3,0/3,2 是墙）
    ok(nb(ROOM_C, (3, 1)) == {(2, 1), (4, 1)}, "室C A2 邻格与文档不符（唯一解失效）")
    ok(nb(ROOM_C, (5, 0)) == {(5, 1)}, "室C S4 不是死胡同")
    ok(nb(ROOM_C, (6, 2)) == {(6, 1)}, "室C Lc 不是只能经 G3 抵达")
    ok(nb(ROOM_C, (6, 1)) == {(5, 1), (6, 2)}, "室C G3 邻格与文档不符")


def check_door_params() -> None:
    """门参数公式：openHold >= d + 1（d+1 保影子穿门），余量 = openHold - d。"""
    for name, room in ROOMS.items():
        plate = next(iter(room["plate"].values()))
        door = next(iter(room["door"].values()))
        # BFS 求板到门的正交最短步数
        from collections import deque
        q, dist = deque([plate]), {plate: 0}
        while q:
            cur = q.popleft()
            if cur == door:
                break
            for d in DIRS:
                nxt = (cur[0] + d[0], cur[1] + d[1])
                if nxt not in dist and not is_wall(room, nxt):
                    dist[nxt] = dist[cur] + 1
                    q.append(nxt)
        d = dist.get(door)
        hold = OPEN_HOLD[name]
        print(f"  室{name}: d={d}  openHold={hold}  余量={hold - d}  影子跟进余量={hold - d - 1}")
        ok(d is not None, f"室{name} 板与门不连通")
        ok(hold >= d + 1, f"室{name} openHold={hold} 不满足 >= d+1={d + 1}")


def check_no_shortcut() -> None:
    """门未开时，该室光点是否真的不可达（含室A主路必经板）。"""
    from collections import deque
    for name, room in ROOMS.items():
        door = next(iter(room["door"].values()))
        target = room["light"][0]
        q, seen = deque([room["start"]]), {room["start"]}
        while q:
            cur = q.popleft()
            for d in DIRS:
                nxt = (cur[0] + d[0], cur[1] + d[1])
                if nxt in seen or is_wall(room, nxt):
                    continue
                if nxt == door:  # 门未开 = 不可通行
                    continue
                seen.add(nxt)
                q.append(nxt)
        ok(target not in seen, f"室{name} 门未开也能到光点 {target}（存在绕过门的通路）")



def check_slack() -> None:
    """余量与影子跟进：网格是二分图，玩家绕路只能花偶数步。

    玩家穿门拍 = 离开板拍 + d + s（s 必为偶数）
    窗口        = 影子压板拍 .. +openHold-1 = 离开板拍+1 .. 离开板拍+openHold
    => 玩家可穿门: s <= openHold - d            (标称余量)
    => 影子可跟进: s <= openHold - d - 1        (影子穿门拍 = 玩家穿门拍 + 1)
    => 有效余量 = 2 * floor((openHold-d)/2)
    """
    from collections import deque
    for name, room in ROOMS.items():
        plate = next(iter(room["plate"].values()))
        door = next(iter(room["door"].values()))
        q, dist = deque([plate]), {plate: 0}
        while q:
            cur = q.popleft()
            for dvec in DIRS:
                nxt = (cur[0] + dvec[0], cur[1] + dvec[1])
                if nxt not in dist and not is_wall(room, nxt):
                    dist[nxt] = dist[cur] + 1
                    q.append(nxt)
        d = dist[door]
        hold = OPEN_HOLD[name]
        nominal = hold - d
        effective = 2 * (nominal // 2)
        follow = hold - d - 1
        print(f"  室{name}: openHold={hold} d={d} 标称余量={nominal} 有效余量={effective} "
              f"影子跟进容错={follow} -> 影子是否恒跟得上: {'是' if follow >= 0 and effective <= follow else '否（s=2 时影子出窗）'}")


def check_stranded() -> None:
    """构造一个"压板后绕 2 步"的走法，看落锁规则会不会把影子关在门外。"""
    room = ROOM_C
    inputs = "DDUDDDSU" + "ADDU"       # 拍1-8 同主线，拍9 A 回 D2、拍10 D 回来、拍11 进门、拍12 到光点
    pos, hist_p = room["start"], [room["start"]]
    for ch in inputs:
        d = DELTA[ch]
        pos = (pos[0] + d[0], pos[1] + d[1])
        ok(not is_wall(room, pos), f"绕路用例撞墙：{pos}")
        hist_p.append(pos)
    hist_s = [room["start"]] + hist_p[:-1]
    plate, door = (5, 0), (6, 1)
    press = max(t for t in range(1, len(hist_s)) if hist_s[t] == plate)
    t_p = next(t for t in range(1, len(hist_p)) if hist_p[t] == door)
    t_s = next(t for t in range(1, len(hist_s)) if hist_s[t] == door)
    win = (press, press + OPEN_HOLD["C"] - 1)
    print(f"  室C 压板后绕 2 步: 压板拍={press} 窗口={win} 玩家穿门拍={t_p} "
          f"{'在窗内' if win[0] <= t_p <= win[1] else '出窗'} / 影子穿门拍={t_s} "
          f"{'在窗内' if win[0] <= t_s <= win[1] else '出窗 → 按落锁规则会被关在门外'}")



def main() -> None:
    print("== 逐拍表 ==")
    for case in CASES:
        before = len(FAIL)
        run_case(case)
        print(f"  {'OK  ' if len(FAIL) == before else 'FAIL'} {case['name']}（{len(case['inputs'])} 拍）")
    print("== 几何断言 ==")
    check_geometry()
    print("== 门参数 ==")
    check_door_params()
    print("== 无绕过通路 ==")
    check_no_shortcut()
    print("== 余量与影子跟进 ==")
    check_slack()
    print("== 落锁边界（门只拦玩家 vs 门等影子）==")
    check_stranded()
    print()
    print(f"通过 {PASS} 条，失败 {len(FAIL)} 条")
    if FAIL:
        for m in FAIL:
            print("  ✗", m)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
