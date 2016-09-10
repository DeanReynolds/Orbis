using Microsoft.Xna.Framework;
using SharpXNA;

namespace Orbis
{
    public struct Tile
    {
        public Tiles Fore { get { return (Tiles)ForeID; } set { ForeID = (byte)value; } }
        public Tiles Back { get { return (Tiles)BackID; } set { BackID = (byte)value; } }
        public enum Tiles { Air, Black, Grass, Dirt, Stone, Log, Leaves, Torch }
        public byte ForeID, BackID;

        public ushort Light;

        public bool Empty => !((ForeID > 0) || (BackID > 0));
        public bool DrawBack => Fore.Matches(Tiles.Air, Tiles.Leaves);
        public bool Solid => Fore.Matches(Tiles.Black, Tiles.Grass, Tiles.Dirt, Tiles.Stone);
        public bool BackOnly => ((ForeID == 0) && (BackID > 0));
        public bool HasFore => (ForeID > 0);
        public bool NoFore => (ForeID == 0);
        public ushort LightGenerated => (ushort)((Fore == Tiles.Torch) ? 275 : 0);
        public float MovementSpeed => (100);
        public float MovementResistance => (!Solid ? 120 : 960);
        public ushort ForeLightDim => (ushort)((Fore == Tiles.Black) ? ushort.MaxValue : (Fore == Tiles.Leaves) ? 12 : 25);
        public ushort BackLightDim => (ushort)((Back == Tiles.Leaves) ? 3 : 6);

        public const int TextureSize = 8, TilesetWidth = 16;
        public static Rectangle Source(int tileID) { return new Rectangle((((tileID - 1) % TilesetWidth) * TextureSize), (((tileID - 1) / TilesetWidth) * TextureSize), TextureSize, TextureSize); }
    }
}