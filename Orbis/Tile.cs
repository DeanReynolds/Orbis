using Microsoft.Xna.Framework;
using SharpXNA;

namespace Orbis
{
    public struct Tile
    {
        public Tiles Fore { get { return (Tiles)ForeID; } set { ForeID = (byte)value; } }
        public Tiles Back { get { return (Tiles)BackID; } set { BackID = (byte)value; } }
        public enum Tiles { Empty, Dirt, Stone }
        public byte ForeID, BackID, Light;

        public bool Empty { get { return !((ForeID > 0) || (BackID > 0)); } }
        public bool DrawBack { get { return Fore.Matches(Tiles.Empty); } }
        public bool Solid { get { return Fore.Matches(Tiles.Dirt); } }
        public bool BackOnly { get { return ((ForeID == 0) && (BackID > 0)); } }

        public const int TextureSize = 8, TilesetWidth = 16;
        public static Rectangle Source(int tileID) { return new Rectangle((((tileID - 1) % TilesetWidth) * TextureSize), (((tileID - 1) / TilesetWidth) * TextureSize), TextureSize, TextureSize); }
    }
}