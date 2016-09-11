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
                        tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Dirt;
                        tiles[x, y].Style = 1;
                        if ((treeSpace > 1) && Globe.Chance(20)) { GenerateTree(ref tiles, x, (y - 1)); treeSpace = -1; }
                    }
                    else if (y < underground) { tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Dirt; }
                    else { tiles[x, y].Fore = tiles[x, y].Back = Tile.Tiles.Stone; }
                }
                treeSpace++; surfaceLength++;
                if (Globe.Chance(30))
                {
                    var dif = Globe.Random(-1, 1);
                    if (surfaceLength == 1)
                    {
                        if (dif == 1) { if ((x > 0) && !tiles[(x - 1), surface].Fore.Matches(Tile.Tiles.Dirt)) dif = Globe.Pick(-1, 0); }
                        else if (dif == -1) { if ((x > 0) && tiles[(x - 1), surface].Fore.Matches(Tile.Tiles.Dirt)) dif = Globe.Pick(0, 1); }
                    }
                    if (dif != 0)
                    {
                        surface += dif;
                        if (surface < minSurface) surface = minSurface;
                        if (surface > maxSurface) surface = maxSurface;
                        surfaceLength = 0;
                    }
                }
            }
            //for (var x = 1; x < (width - 1); x++) for (var y = 1; y < (height - 1); y++) GenerateStyle(ref tiles, x, y);
            return tiles;
        }
        public static void GenerateTree(ref Tile[,] tiles, int x, int y)
        {
            int height = (y - Globe.Random(15, 25)), leavesStyle = Globe.Pick(1, 2);
            for (int k = y; k >= height; k--)
                if (k >= 0)
                {
                    if (tiles[x, k].ForeID > 0) break;
                    if (k == height)
                    {
                        tiles[x, k].Back = Tile.Tiles.Log; tiles[x, k].Fore = Tile.Tiles.Leaves;
                        if (leavesStyle == 1)
                        {
                            int width = (x + 2);
                            for (int j = (x - 2); j <= width; j++)
                                if ((j >= 0) && (j < tiles.GetLength(0)))
                                {
                                    if (tiles[j, k].Fore == Tile.Tiles.Log) tiles[j, k].Back = Tile.Tiles.Log;
                                    tiles[j, k].Fore = Tile.Tiles.Leaves;
                                }
                            width = (x + 1); k--;
                            if (k >= 0)
                                for (int j = (x - 1); j <= width; j++)
                                    if ((j >= 0) && (j < tiles.GetLength(0)))
                                    {
                                        if (tiles[j, k].Fore == Tile.Tiles.Log) tiles[j, k].Back = Tile.Tiles.Log;
                                        tiles[j, k].Fore = Tile.Tiles.Leaves;
                                    }
                            k--; if (k >= 0) tiles[x, k].Fore = Tile.Tiles.Leaves;
                        }
                        else if (leavesStyle == 2)
                        {
                            int width = (x + 1); height--; int treeHeight = (y - height), leavesCut = Globe.Random(2, (treeHeight / 2));
                            for (int l = (y - leavesCut); l >= height; l--)
                                for (int j = (x - 1); j <= width; j++)
                                    if ((j >= 0) && (j < tiles.GetLength(0)))
                                    {
                                        if (tiles[j, l].Fore == Tile.Tiles.Log) tiles[j, l].Back = Tile.Tiles.Log;
                                        tiles[j, l].Fore = Tile.Tiles.Leaves;
                                    }
                        }
                    }
                    else
                    {
                        tiles[x, k].Fore = Tile.Tiles.Log;
                        if ((leavesStyle == 1) && (k < y) && (k > (height + 1)))
                        {
                            bool left = ((x > 0) && (tiles[(x - 1), (k + 1)].Fore != Tile.Tiles.Leaves) && Globe.Chance(20)),
                                right = ((x < (tiles.GetLength(0) - 1)) && (tiles[(x + 1), (k + 1)].Fore != Tile.Tiles.Leaves) && Globe.Chance(20));
                            if (left && (tiles[(x - 1), k].ForeID == 0)) tiles[(x - 1), k].Fore = Tile.Tiles.Leaves;
                            if (right && (tiles[(x + 1), k].ForeID == 0)) tiles[(x + 1), k].Fore = Tile.Tiles.Leaves;
                        }
                    }
                }
        }

        public static byte GenerateStyle(ref Tile[,] tiles, int x, int y)
        {
            var tile = tiles[x, y];
            byte style = 0;
            if (tiles[x, (y - 1)].BorderJoins(tile)) style++;
            if (tiles[(x + 1), y].BorderJoins(tile)) style += 2;
            if (tiles[x, (y + 1)].BorderJoins(tile)) style += 4;
            if (tiles[(x - 1), y].BorderJoins(tile)) style += 8;
            if (tiles[(x + 1), (y - 1)].BorderJoins(tile))
            {
                if (style == 0) style = 16;
                else if (style == 4) style = 17;
                else if (style == 8) style = 18;
                else if (style == 12) style = 19;
            }
            if (tiles[(x + 1), (y + 1)].BorderJoins(tile))
            {
                if (style == 0) style = 20;
                else if (style == 1) style = 21;
                else if (style == 8) style = 22;
                else if (style == 9) style = 23;
                else if (style == 16) style = 32;
                else if (style == 18) style = 33;
            }
            if (tiles[(x - 1), (y + 1)].BorderJoins(tile))
            {
                if (style == 0) style = 24;
                else if (style == 1) style = 25;
                else if (style == 2) style = 26;
                else if (style == 3) style = 27;
                else if (style == 16) style = 34;
                else if (style == 20) style = 42;
                else if (style == 21) style = 44;
                else if (style == 32) style = 35;
            }
            if (tiles[(x - 1), (y - 1)].BorderJoins(tile))
            {
                if (style == 0) style = 28;
                else if (style == 2) style = 29;
                else if (style == 4) style = 30;
                else if (style == 6) style = 31;
                else if (style == 16) style = 37;
                else if (style == 17) style = 38;
                else if (style == 20) style = 40;
                else if (style == 24) style = 39;
                else if (style == 26) style = 41;
                else if (style == 35) style = 43;
                else if (style == 42) style = 36;
            }
            return style;
        }
    }
}
