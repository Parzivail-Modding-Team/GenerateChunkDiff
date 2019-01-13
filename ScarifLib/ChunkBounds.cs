using System;
using Substrate;

namespace ScarifLib
{
    public class ChunkBounds
    {
        public int MinX { get; set; }
        public int MaxX { get; set; }

        public int MinY { get; set; }
        public int MaxY { get; set; }

        public int MinZ { get; set; }
        public int MaxZ { get; set; }

        public bool BoundsExist { get; }

        public ChunkBounds(string boundsStr)
        {
            BoundsExist = false;
            if (boundsStr == null) return;

            try
            {
                var split = boundsStr.Split(':');
                MinX = int.Parse(split[0]);
                MinY = int.Parse(split[1]);
                MinZ = int.Parse(split[2]);
                MaxX = int.Parse(split[3]);
                MaxY = int.Parse(split[4]);
                MaxZ = int.Parse(split[5]);
                BoundsExist = true;
            }
            catch (Exception)
            {
                BoundsExist = false;
            }
        }

        public bool Contains(int x, int y, int z)
        {
            if (!BoundsExist)
                return true;
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;
        }

        public bool CoarseContains(ChunkRef chunk)
        {
            if (!BoundsExist)
                return true;
            return chunk.X * 16 + 16 >= MinX && 
                   chunk.X * 16 <= MaxX &&
                   chunk.Z * 16 + 16 >= MinZ && 
                   chunk.Z * 16 <= MaxZ;
        }
    }
}