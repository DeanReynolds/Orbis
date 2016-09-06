using Microsoft.Xna.Framework;
using SharpXNA;

namespace Orbis
{
    public struct Tile
    {
        public Tiles Fore { get { return (Tiles)ForeID; } set { ForeID = (byte)value; } }
        public Tiles Back { get { return (Tiles)BackID; } set { BackID = (byte)value; } }
        public enum Tiles { Empty, Dirt }
        public byte ForeID, BackID;

        public bool DrawBack { get { return Fore.Matches(Tiles.Empty); } }

        public const int TextureSize = 8, TilesetWidth = 16;
        public static Rectangle Source(int tileID) { return new Rectangle((((tileID - 1) % TilesetWidth) * TextureSize), (((tileID - 1) / TilesetWidth) * TextureSize), TextureSize, TextureSize); }
    }
}