using System;
using System.Collections.Generic;
using Cosmos.Core;

namespace Cosmos.CoreSimCheck
{
    /// <summary>
    /// 无头验收：用纯 C# 把 Docs/GameDesign/LevelDesign-M1-ShadowRoom.md 的逐拍推演跑一遍。
    /// 不依赖 Unity、不需要美术、不需要打开编辑器。dotnet run 即可。
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static int Main()
        {
            GeometryIsForced();
            DocumentedSolution();
            DoorNeedsLatch();
            LatchSurvivesLeavingTheSwitch();
            LightEndsTheRun();
            BlockedMoveCostsNoBeat();
            ResetReturnsToStart();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("RESULT: all checks passed");
                return 0;
            }

            Console.WriteLine("RESULT: " + _failures + " check(s) failed");
            return 1;
        }

        // 1. 约束 A：A 的四邻只有 B 是通路，唯一解被几何锁死
        private static void GeometryIsForced()
        {
            var room = ShadowRoom.Milestone1();
            var sim = new ShadowSyncSim(room);

            var open = new List<GridPos>();
            foreach (var dir in new[] { GridPos.Up, GridPos.Down, GridPos.Left, GridPos.Right })
            {
                var n = room.PlayerSwitch + dir;
                if (!sim.IsBlocked(n)) open.Add(n);
            }

            Check(
                "A 只有一个可走邻格，且必须是影子开关 B",
                open.Count == 1 && open[0] == room.ShadowSwitch,
                "walkable neighbours of A = " + string.Join(" ", open));

            Check("A、B 正交相邻", Manhattan(room.PlayerSwitch, room.ShadowSwitch) == 1, "");
        }

        // 2. 逐拍推演：拍 4 必须同时压住 A 与 B
        private static void DocumentedSolution()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            var expected = new[]
            {
                "(0,0)|(0,0)",
                "(1,0)|(0,0)",
                "(2,0)|(1,0)",
                "(2,1)|(2,0)",
                "(3,1)|(2,1)",
            };

            var steps = new[] { GridPos.Right, GridPos.Right, GridPos.Up, GridPos.Right };

            Console.WriteLine("beat  player   shadow");
            Console.WriteLine(string.Format("{0,4}  {1,-8} {2,-8}", 0, sim.PlayerPos, sim.ShadowPos));
            Check("拍 0 开局重合", Got(sim) == expected[0], Got(sim));

            for (int i = 0; i < steps.Length; i++)
            {
                sim.TryMove(steps[i]);
                int beat = i + 1;
                Console.WriteLine(string.Format("{0,4}  {1,-8} {2,-8}", beat, sim.PlayerPos, sim.ShadowPos));
                Check("拍 " + beat + " 位置与文档一致", Got(sim) == expected[beat], Got(sim));
            }

            Check("拍 4 触发闩锁（玩家踩 A + 影子踩 B）", sim.DoorLatched, "latched=" + sim.DoorLatched);
            Check("影子严格滞后 1 拍", sim.ShadowPos == new GridPos(2, 1), "shadow=" + sim.ShadowPos);
        }

        // 3. 门在闩锁前必须挡住玩家（M1 约束 B）
        private static void DoorNeedsLatch()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            sim.TryMove(GridPos.Right);   // (1,0)
            sim.TryMove(GridPos.Right);   // (2,0)
            sim.TryMove(GridPos.Up);      // (2,1) = B
            bool moved = sim.TryMove(GridPos.Up); // (2,2) 门，未闩锁

            Check("未同步时门不可通行", !moved && sim.PlayerPos == new GridPos(2, 1), "player=" + sim.PlayerPos);
            Check("被挡的一拍不进入门格", !sim.DoorLatched, "");
        }

        // 4. 闩锁：玩家离开 A 之后门仍然开着
        private static void LatchSurvivesLeavingTheSwitch()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Up);
            sim.TryMove(GridPos.Right);   // 拍 4 同步

            sim.TryMove(GridPos.Left);    // 离开 A
            Check("离开 A 后门仍保持开启（闩锁）", sim.DoorLatched && sim.PlayerPos == new GridPos(2, 1), "player=" + sim.PlayerPos);
        }

        // 5. 抵达光点即结束，之后输入不再生效
        private static void LightEndsTheRun()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Up);
            sim.TryMove(GridPos.Right);   // A
            sim.TryMove(GridPos.Left);    // B
            sim.TryMove(GridPos.Up);      // 门
            sim.TryMove(GridPos.Up);      // 光点

            Check("走到光点即通关", sim.ReachedLight, "player=" + sim.PlayerPos);

            int beat = sim.Beat;
            bool after = sim.TryMove(GridPos.Down);
            Check("通关后输入被忽略", !after && sim.Beat == beat, "beat=" + sim.Beat);
        }

        // 6. 撞墙不消耗拍，影子的时刻表不被空拍污染（M1 约束 E 的反面）
        private static void BlockedMoveCostsNoBeat()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            bool left = sim.TryMove(GridPos.Left);   // 出界
            bool down = sim.TryMove(GridPos.Down);   // 出界

            Check("撞墙/出界返回 false", !left && !down, "");
            Check("撞墙不消耗拍，影子不动",
                sim.Beat == 0 && sim.PlayerPos == new GridPos(0, 0) && sim.ShadowPos == new GridPos(0, 0),
                "beat=" + sim.Beat + " shadow=" + sim.ShadowPos);
        }

        // 7. R 重来：状态完全回零
        private static void ResetReturnsToStart()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Up);
            sim.TryMove(GridPos.Right);
            sim.Reset();

            Check("重置后回到起点",
                sim.PlayerPos == new GridPos(0, 0) && sim.ShadowPos == new GridPos(0, 0) && sim.Beat == 0,
                "player=" + sim.PlayerPos + " shadow=" + sim.ShadowPos + " beat=" + sim.Beat);
            Check("重置后门重新落锁", !sim.DoorLatched && !sim.ReachedLight, "");
        }

        // ---------- 断言 ----------

        private static string Got(ShadowSyncSim sim) => sim.PlayerPos + "|" + sim.ShadowPos;

        private static int Manhattan(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private static void Check(string name, bool ok, string detail)
        {
            Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + name + (string.IsNullOrEmpty(detail) ? "" : "   [" + detail + "]"));
            if (!ok) _failures++;
        }
    }
}
