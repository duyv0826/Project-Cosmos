using System.Collections.Generic;

namespace Cosmos.Core
{
    /// <summary>
    /// 房间几何：墙、两个开关、闩锁门、光点。纯数据，无副作用。
    /// 关卡几何必须服从 M1 约束：A 的四邻只有 B 是通路，逼出唯一解。
    /// </summary>
    public sealed class ShadowRoom
    {
        public readonly int Width;
        public readonly int Height;

        private readonly bool[] _walls; // index = y * Width + x

        public GridPos Start { get; }
        public GridPos PlayerSwitch { get; }
        public GridPos ShadowSwitch { get; }
        public GridPos Door { get; }
        public GridPos Light { get; }

        public ShadowRoom(
            int width,
            int height,
            IEnumerable<GridPos> walls,
            GridPos start,
            GridPos playerSwitch,
            GridPos shadowSwitch,
            GridPos door,
            GridPos light)
        {
            Width = width;
            Height = height;
            _walls = new bool[width * height];

            foreach (var w in walls)
            {
                if (InBounds(w)) _walls[w.Y * width + w.X] = true;
            }

            Start = start;
            PlayerSwitch = playerSwitch;
            ShadowSwitch = shadowSwitch;
            Door = door;
            Light = light;
        }

        public bool InBounds(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

        public bool IsWall(GridPos p) => InBounds(p) && _walls[p.Y * Width + p.X];

        /// <summary>
        /// 里程碑 1「影子之室」，4×4 单屏。
        /// <code>
        ///       x0  x1  x2  x3
        /// y3    #   #   L   #
        /// y2    #   #   D   #
        /// y1    .   .   B   A
        /// y0    P   .   .   #
        /// </code>
        /// </summary>
        public static ShadowRoom Milestone1()
        {
            var walls = new List<GridPos>
            {
                new GridPos(0, 3), new GridPos(1, 3), new GridPos(3, 3),
                new GridPos(0, 2), new GridPos(1, 2), new GridPos(3, 2),
                new GridPos(3, 0),
            };

            return new ShadowRoom(
                width: 4,
                height: 4,
                walls: walls,
                start: new GridPos(0, 0),
                playerSwitch: new GridPos(3, 1),
                shadowSwitch: new GridPos(2, 1),
                door: new GridPos(2, 2),
                light: new GridPos(2, 3));
        }
    }
}
