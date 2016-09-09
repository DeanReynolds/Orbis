using Microsoft.Xna.Framework;
using SharpXNA;

namespace Orbis.World
{
    public static class Generation
    {
        public static Tile[,] Generate(int width, int height, out Point spawn)
        {
            var tiles = new Tile[width, height];
            int minSurface = ((height / 4) - (height / 10)), maxSurface = (height / 4) + (height / 10), surface = Globe.Random(minSurface, maxSurface),
                surfaceLength = 0, treeSpace = width;
            spawn = Point.Zero;
            for (var x = 0; x < width; x++) { tiles[x, 0].Fore = tiles[x, (height - 1)].Fore = Tile.Tiles.Black; }
            for (var y = 0; y < height; y++) { tiles[0, y].Fore = tiles[(width - 1), y].Fore = Tile.Tiles.Black; }
            for (var x = 1; x < (width - 1); x++)
            {
                if (x == width / 2) spawn = new Point(x, surface - 3);
                var underground = (surface + Globe.Random(10, 12));
                for (var y = surface; y < (height - 1); y++)
                {
                    if (y == surface)
                    {
                        tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Grass;
                        if ((treeSpace > 1) && Globe.Chance(20)) { GenerateTree(ref tiles, x, (y - 1)); treeSpace = -1; }
                    }
                    else if (y < underground) { tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Dirt; }
                    else { tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Stone; }
                }
                treeSpace++; surfaceLength++;
                if ((surfaceLength > 1) && Globe.Chance(30))
                {
                    surface += Globe.Random(-1, 1);
                    if (surface < minSurface) surface = minSurface;
                    if (surface > maxSurface) surface = maxSurface;
                    surfaceLength = 0;
                }
            }
            return tiles;
        }
        public static void GenerateTree(ref Tile[,] tiles, int x, int y)
        {
            int height = (y - Globe.Random(15, 25)), leavesStyle = Globe.Pick(1, 2);
            for (int k = y; k >= height; k--)
                if (Game.InBounds(ref tiles, x, k))
                {
                    if (tiles[x, k].ForeID > 0) break;
                    if (k == height)
                    {
                        tiles[x, k].Back = Tile.Tiles.Log;
                        tiles[x, k].Fore = Tile.Tiles.Leaves;
                        if (leavesStyle == 1)
                        {
                            int width = (x + 2);
                            for (int j = (x - 2); j <= width; j++)
                                if ((j >= 0) && (j < tiles.GetLength(0)))
                                {
                                    if (tiles[j, k].Fore == Tile.Tiles.Log) { tiles[j, k].Back = Tile.Tiles.Log; tiles[j, k].Fore = Tile.Tiles.Leaves; }
                                    else if (tiles[j, k].ForeID == 0) tiles[j, k].Fore = Tile.Tiles.Leaves;
                                }
                            width = (x + 1); k--;
                            if (k >= 0)
                            {
                                for (int j = (x - 1); j <= width; j++)
                                    if ((j >= 0) && (j < tiles.GetLength(0)))
                                    {
                                        if (tiles[j, k].Fore == Tile.Tiles.Log) { tiles[j, k].Back = Tile.Tiles.Log; tiles[j, k].Fore = Tile.Tiles.Leaves; }
                                        else if (tiles[j, k].ForeID == 0) tiles[j, k].Fore = Tile.Tiles.Leaves;
                                    }
                            }
                            k--;
                            if (k >= 0)
                            {
                                if (tiles[x, k].Fore == Tile.Tiles.Log) { tiles[x, k].Back = Tile.Tiles.Log; tiles[x, k].Fore = Tile.Tiles.Leaves; }
                                else if (tiles[x, k].ForeID == 0) tiles[x, k].Fore = Tile.Tiles.Leaves;
                            }
                        }
                        else if (leavesStyle == 2)
                        {
                            int width = (x + 1); height--; int treeHeight = (y - height), leavesCut = Globe.Random(2, (treeHeight / 2));
                            for (int l = (y - leavesCut); l >= height; l--)
                                for (int j = (x - 1); j <= width; j++)
                                    if ((j >= 0) && (j < tiles.GetLength(0)))
                                    {
                                        if (tiles[j, l].Fore == Tile.Tiles.Log) { tiles[j, l].Back = Tile.Tiles.Log; tiles[j, l].Fore = Tile.Tiles.Leaves; }
                                        else if (tiles[j, l].ForeID == 0) tiles[j, l].Fore = Tile.Tiles.Leaves;
                                    }
                        }
                    }
                    else
                    {
                        tiles[x, k].Fore = Tile.Tiles.Log;
                        if ((leavesStyle == 1) && (k < y))
                        {
                            bool left = ((x > 0) && Globe.Chance(20)), right = ((x < (tiles.GetLength(0) - 1)) && Globe.Chance(20));
                            if (left && (tiles[(x - 1), k].ForeID == 0)) tiles[(x - 1), k].Fore = Tile.Tiles.Leaves;
                            if (right && (tiles[(x + 1), k].ForeID == 0)) tiles[(x + 1), k].Fore = Tile.Tiles.Leaves;
                        }
                    }
                }
        }
    }
}
