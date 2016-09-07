using Microsoft.Xna.Framework;
using SharpXNA;

namespace Orbis.World
{
    public static class Generation
    {
        public static Tile[,] Generate(int width, int height, out Point spawn)
        {
            var tiles = new Tile[width, height];
            int minSurface = ((height / 4) - (height / 10)), maxSurface = (height / 4) + (height / 10), surface = Globe.Random(minSurface, maxSurface), surfaceLength = 0;
            spawn = Point.Zero;
            for (var x = 0; x < width; x++)
            {
                if (x == width / 2) spawn = new Point(x, surface - 2);
                var underground = (surface + Globe.Random(10, 12));
                for (var y = surface; y < height; y++)
                {
                    if (y == surface) { tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Grass; }
                    else if (y < underground) { tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Dirt; }
                    else { tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Stone; }
                }
                surfaceLength++;
                if (surfaceLength > 1)
                {
                    if (Globe.Chance(30))
                    {
                        surface += Globe.Random(-1, 1);
                        if (surface < minSurface) surface = minSurface;
                        if (surface > maxSurface) surface = maxSurface;
                        surfaceLength = 0;
                    }
                }
            }
            return tiles;
        }
    }
}
