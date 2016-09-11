using Microsoft.Xna.Framework;
using SharpXNA;

namespace Orbis.World
{
    public struct Tile
    {
        public Tiles Fore { get { return (Tiles)ForeID; } set { ForeID = (byte)value; } }
        public Tiles Back { get { return (Tiles)BackID; } set { BackID = (byte)value; } }
        public enum Tiles { Air, Black, Dirt, Stone, Log, Leaves, Torch }
        public byte ForeID, BackID;

        public const int Size = 8;

        public byte Style;
        public ushort Light;

        public bool Empty => !((ForeID > 0) || (BackID > 0));
        public bool DrawBack => Fore.Matches(Tiles.Air, Tiles.Leaves);
        public bool Solid => Fore.Matches(Tiles.Black, Tiles.Dirt, Tiles.Stone);
        public bool BackOnly => ((ForeID == 0) && (BackID > 0));
        public bool HasFore => (ForeID > 0);
        public bool NoFore => (ForeID == 0);
        public ushort LightGenerated => (ushort)((Fore == Tiles.Torch) ? 275 : 0);
        public float MovementSpeed => (100);
        public float MovementResistance => (!Solid ? 120 : 960);
        public ushort ForeLightDim => (ushort)((Fore == Tiles.Black) ? ushort.MaxValue : (Fore == Tiles.Leaves) ? 12 : 25);
        public ushort BackLightDim => (ushort)((Back == Tiles.Leaves) ? 3 : 6);
        public bool HasBorder => !Fore.Matches(Tiles.Log);
        public bool BorderJoins(Tile tile) { return tile.ForeID != ForeID; }
        public bool EitherForeIs(Tile tile, params Tiles[] types) { return Fore.Matches(types) && tile.Fore.Matches(types); }

        public const int TextureSize = 8, TilesetHeight = 32;
        public static Rectangle Source(int tileID, byte style) { return new Rectangle((((4 + (tileID / TilesetHeight) * 4) + (style % 4)) * TextureSize), (((tileID - 1) % TilesetHeight) * TextureSize), TextureSize, TextureSize); }
        public static Rectangle Border(byte style) { return new Rectangle(((style % 4) * TextureSize), ((style / 4) * TextureSize), TextureSize, TextureSize); }
    }
}