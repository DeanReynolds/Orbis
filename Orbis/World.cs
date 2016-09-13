using Microsoft.Xna.Framework;
using SharpXNA;

namespace Orbis
{
    public class World
    {
        public Tile[,] Tiles;
        public int Width => Tiles.GetLength(0);
        public int Height => Tiles.GetLength(1);
        public bool InBounds(int x, int y) { return !((x < 0) || (y < 0) || (x >= Tiles.GetLength(0)) || (y >= Tiles.GetLength(1))); }
        public Point Spawn;

        public World(int width, int height) { Tiles = new Tile[width, height]; }

        public static World Generate(int width, int height)
        {
            var world = new World(width, height);
            int minSurface = height / 4 - height / 10, maxSurface = height / 4 + height / 10, surface = Globe.Random(minSurface, maxSurface), surfaceLength = 0, treeSpace = width;
            for (var x = 0; x < width; x++) world.Tiles[x, 0].Fore = world.Tiles[x, height - 1].Fore = Tile.Tiles.Black;
            for (var y = 0; y < height; y++) world.Tiles[0, y].Fore = world.Tiles[width - 1, y].Fore = Tile.Tiles.Black;
            for (var x = 1; x < width - 1; x++)
            {
                if (x == width / 2) world.Spawn = new Point(x, surface - 3);
                var underground = surface + Globe.Random(10, 12);
                for (var y = surface; y < height - 1; y++)
                    if (y == surface)
                    {
                        world.Tiles[x, y].Fore = world.Tiles[x, y].Back = Tile.Tiles.Dirt;
                        world.Tiles[x, y].ForeStyle = 1;
                        if ((treeSpace > 1) && Globe.Chance(20))
                        {
                            world.GenerateTree(x, (y - 1));
                            treeSpace = -1;
                        }
                    }
                    else if (y < underground) { world.Tiles[x, y].Fore = world.Tiles[x, y].Back = Tile.Tiles.Dirt; }
                    else
                    { world.Tiles[x, y].Fore = world.Tiles[x, y].Back = Tile.Tiles.Stone; }
                treeSpace++;
                surfaceLength++;
                if (Globe.Chance(30))
                {
                    var dif = Globe.Random(-1, 1);
                    if (surfaceLength == 1)
                        if (dif == 1)
                        {
                            if ((x > 0) && !world.Tiles[x - 1, surface].Fore.Matches(Tile.Tiles.Dirt)) dif = Globe.Pick(-1, 0);
                        }
                        else if (dif == -1)
                        {
                            if ((x > 0) && world.Tiles[x - 1, surface].Fore.Matches(Tile.Tiles.Dirt)) dif = Globe.Pick(0, 1);
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
            return world;
        }

        public void GenerateTree(int x, int y)
        {
            int height = y - Globe.Random(15, 25), leavesStyle = Globe.Pick(1, 2);
            for (var k = y; k >= height; k--)
                if (k >= 0)
                {
                    if (Tiles[x, k].ForeID > 0) break;
                    if (k == height)
                    {
                        Tiles[x, k].Back = Tile.Tiles.Log;
                        Tiles[x, k].Fore = Tile.Tiles.Leaves;
                        if (leavesStyle == 1)
                        {
                            var width = x + 2;
                            for (var j = x - 2; j <= width; j++)
                                if ((j >= 0) && (j < Tiles.GetLength(0)))
                                {
                                    if (Tiles[j, k].Fore == Tile.Tiles.Log) Tiles[j, k].Back = Tile.Tiles.Log;
                                    Tiles[j, k].Fore = Tile.Tiles.Leaves;
                                }
                            width = x + 1;
                            k--;
                            if (k >= 0)
                                for (var j = x - 1; j <= width; j++)
                                    if ((j >= 0) && (j < Tiles.GetLength(0)))
                                    {
                                        if (Tiles[j, k].Fore == Tile.Tiles.Log) Tiles[j, k].Back = Tile.Tiles.Log;
                                        Tiles[j, k].Fore = Tile.Tiles.Leaves;
                                    }
                            k--;
                            if (k >= 0) Tiles[x, k].Fore = Tile.Tiles.Leaves;
                        }
                        else if (leavesStyle == 2)
                        {
                            var width = x + 1;
                            height--;
                            int treeHeight = y - height, leavesCut = Globe.Random(2, treeHeight / 2);
                            for (var l = y - leavesCut; l >= height; l--)
                                for (var j = x - 1; j <= width; j++)
                                    if ((j >= 0) && (j < Tiles.GetLength(0)))
                                    {
                                        if (Tiles[j, l].Fore == Tile.Tiles.Log) Tiles[j, l].Back = Tile.Tiles.Log;
                                        Tiles[j, l].Fore = Tile.Tiles.Leaves;
                                    }
                        }
                    }
                    else
                    {
                        Tiles[x, k].Fore = Tile.Tiles.Log;
                        if ((leavesStyle == 1) && (k < y) && (k > height + 1))
                        {
                            bool left = (x > 0) && (Tiles[x - 1, k + 1].Fore != Tile.Tiles.Leaves) && Globe.Chance(20), right = (x < Tiles.GetLength(0) - 1) && (Tiles[x + 1, k + 1].Fore != Tile.Tiles.Leaves) && Globe.Chance(20);
                            if (left && (Tiles[x - 1, k].ForeID == 0)) Tiles[x - 1, k].Fore = Tile.Tiles.Leaves;
                            if (right && (Tiles[x + 1, k].ForeID == 0)) Tiles[x + 1, k].Fore = Tile.Tiles.Leaves;
                        }
                    }
                }
        }
        public byte GenerateStyle(int x, int y)
        {
            if ((x <= 0) || (y <= 0) || (x >= Tiles.GetLength(0) - 1) || (y >= (Tiles.GetLength(1) - 1))) return 0;
            var tile = Tiles[x, y];
            byte style = 0;
            if (Tiles[x, y - 1].BorderJoins(tile)) style++;
            if (Tiles[x + 1, y].BorderJoins(tile)) style += 2;
            if (Tiles[x, y + 1].BorderJoins(tile)) style += 4;
            if (Tiles[x - 1, y].BorderJoins(tile)) style += 8;
            if (Tiles[x + 1, y - 1].BorderJoins(tile))
                if (style == 0) { style = 16; }
                else if (style == 4) { style = 17; }
                else if (style == 8) { style = 18; }
                else if (style == 12) { style = 19; }
            if (Tiles[x + 1, y + 1].BorderJoins(tile))
                if (style == 0) { style = 20; }
                else if (style == 1) { style = 21; }
                else if (style == 8) { style = 22; }
                else if (style == 9) { style = 23; }
                else if (style == 16) { style = 32; }
                else if (style == 18) { style = 33; }
            if (Tiles[x - 1, y + 1].BorderJoins(tile))
                if (style == 0) { style = 24; }
                else if (style == 1) { style = 25; }
                else if (style == 2) { style = 26; }
                else if (style == 3) { style = 27; }
                else if (style == 16) { style = 34; }
                else if (style == 20) { style = 42; }
                else if (style == 21) { style = 44; }
                else if (style == 32) { style = 35; }
            if (Tiles[x - 1, y - 1].BorderJoins(tile))
                if (style == 0) { style = 28; }
                else if (style == 2) { style = 29; }
                else if (style == 4) { style = 30; }
                else if (style == 6) { style = 31; }
                else if (style == 16) { style = 37; }
                else if (style == 17) { style = 38; }
                else if (style == 20) { style = 40; }
                else if (style == 24) { style = 39; }
                else if (style == 26) { style = 41; }
                else if (style == 35) { style = 43; }
                else if (style == 42) { style = 36; }
            return style;
        }
    }
}