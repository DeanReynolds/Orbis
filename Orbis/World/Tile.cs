using Microsoft.Xna.Framework;
using SharpXNA;

namespace Orbis
{
    public struct Tile
    {
        public Tiles Fore { get { return (Tiles)ForeID; } set { ForeID = (byte)value; } }
        public Tiles Back { get { return (Tiles)BackID; } set { BackID = (byte)value; } }
        public enum Tiles { Air, Dirt, Stone }
        public byte ForeID, BackID;

        public ushort Light;

        public bool Empty => !((ForeID > 0) || (BackID > 0));
        public bool DrawBack => Fore.Matches(Tiles.Air);
        public bool Solid => Fore.Matches(Tiles.Dirt);
        public bool BackOnly => (ForeID == 0) && (BackID > 0);

        public const int TextureSize = 8, TilesetWidth = 16;
        public static Rectangle Source(int tileID) { return new Rectangle((((tileID - 1) % TilesetWidth) * TextureSize), (((tileID - 1) / TilesetWidth) * TextureSize), TextureSize, TextureSize); }
    }
}