using System;
using System.Collections.Generic;

namespace Cosmos.Core
{
    /// <summary>一次成功的移动/状态变化，用于驱动表现层。</summary>
    public enum SimEvent
    {
        Blocked,      // 撞墙或门未开，这一拍不消耗
        Step,         // 玩家走了一步，影子同步挪到上一拍的位置
        Sync,         // 玩家踩 A 且影子踩 B —— 闩锁门开启的那一瞬间
        ReachedLight, // 玩家抵达光点
        Reset,
    }

    /// <summary>
    /// 「影子同步」的权威状态。纯 C#，不引用 UnityEngine。
    ///
    /// 唯一规则：影子在第 t 拍站在玩家第 t-Lag 拍的格子上。
    /// lag=1 时即"影子永远站在你上一拍的位置"——它没有原地不动的指令，
    /// 所以"同时踩住 A 和 B"只能靠 A、B 相邻且最后一步从 B 迈入 A。
    ///
    /// 门是闩锁式的：双开关同时压住的瞬间永久开启（M1 约束 B：lag=1 下持续踩住型门无解）。
    /// 影子不会被门夹住、不会被墙阻挡——它复读的是玩家走过的合法路径（M1 约束 C）。
    /// </summary>
    public sealed class ShadowSyncSim
    {
        public const int DefaultLag = 1;

        public ShadowRoom Room { get; }
        public int Lag { get; }

        /// <summary>玩家从开局到现在的完整轨迹，索引即拍号。第 0 项为出生点。</summary>
        private readonly List<GridPos> _trail = new List<GridPos>(256);

        public GridPos PlayerPos { get; private set; }
        public GridPos ShadowPos { get; private set; }
        public int Beat { get; private set; }
        public bool DoorLatched { get; private set; }
        public bool ReachedLight { get; private set; }
        public int SyncCount { get; private set; }

        public event Action<SimEvent> Emitted;

        public ShadowSyncSim(ShadowRoom room, int lag = DefaultLag)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (lag < 0) throw new ArgumentOutOfRangeException(nameof(lag));

            Room = room;
            Lag = lag;
            Reset();
        }

        public bool IsDoorOpen => DoorLatched;

        /// <summary>目标格是否可走。门在闩锁前视为墙。</summary>
        public bool IsBlocked(GridPos p)
        {
            if (!Room.InBounds(p)) return true;
            if (Room.IsWall(p)) return true;
            if (p == Room.Door && !DoorLatched) return true;
            return false;
        }

        /// <summary>走一步。返回 false 表示这一拍没有发生（撞墙 / 门未开 / 已通关）。</summary>
        public bool TryMove(GridPos dir)
        {
            if (ReachedLight) return false;
            if (dir == GridPos.Zero) return false;

            var next = PlayerPos + dir;
            if (IsBlocked(next))
            {
                Emitted?.Invoke(SimEvent.Blocked);
                return false;
            }

            PlayerPos = next;
            _trail.Add(next);
            Beat++;
            UpdateShadow();
            Emitted?.Invoke(SimEvent.Step);

            // 双开关同时压住 → 闩锁。只在第一次触发时发事件。
            if (!DoorLatched && PlayerPos == Room.PlayerSwitch && ShadowPos == Room.ShadowSwitch)
            {
                DoorLatched = true;
                SyncCount++;
                Emitted?.Invoke(SimEvent.Sync);
            }

            if (DoorLatched && PlayerPos == Room.Light)
            {
                ReachedLight = true;
                Emitted?.Invoke(SimEvent.ReachedLight);
            }

            return true;
        }

        /// <summary>R 键：整关重置，玩家与影子回起点，门重新落锁。无惩罚、可无限重来。</summary>
        public void Reset()
        {
            _trail.Clear();
            _trail.Add(Room.Start);

            PlayerPos = Room.Start;
            ShadowPos = Room.Start;
            Beat = 0;
            DoorLatched = false;
            ReachedLight = false;
            SyncCount = 0;

            Emitted?.Invoke(SimEvent.Reset);
        }

        /// <summary>影子位置 = 玩家 Lag 拍之前的位置；开局队列未满时停在起点。</summary>
        private void UpdateShadow()
        {
            int index = _trail.Count - 1 - Lag;
            ShadowPos = index >= 0 ? _trail[index] : Room.Start;
        }

        /// <summary>按当前状态算出一句可上屏的说明，表现层与测试共用，避免两套判断。</summary>
        public string Describe()
        {
            if (ReachedLight) return "你们终于同步了一次";
            if (DoorLatched) return "门开了 —— 但它只开了一次";
            if (Beat == 0) return "它跟着你，慢了半拍";
            return "拍 " + Beat + "　你" + PlayerPos + "　它" + ShadowPos;
        }
    }
}
