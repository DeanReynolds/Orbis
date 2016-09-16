using System;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpXNA;

namespace Orbis
{
    public class World
    {
        private static string LoadingText { get { return Game.LoadingText; } set { Game.LoadingText = value; } }
        private static float LoadingPercentage { get { return Game.LoadingPercentage; } set { Game.LoadingPercentage = value; } }

        private const float _defaultZoom = 2;
        private static Color _skyColor;
        private static readonly Texture2D _blackPixel;
        public static readonly Texture2D _tilesTexture;

        public Tile[,] Tiles;
        public int Width => Tiles.GetLength(0);
        public int Height => Tiles.GetLength(1);
        public bool InBounds(int x, int y) { return !((x < 0) || (y < 0) || (x >= Tiles.GetLength(0)) || (y >= Tiles.GetLength(1))); }
        public Point Spawn;
        public ushort AmbientLight = 285;

        internal static readonly BlendState _multiplyBlend;
        internal RenderTarget2D _lightMap;
        private readonly Thread _lightThread;
        private ManualResetEvent _lightEvent = new ManualResetEvent(true);
        private const int _lightBuffer = 32, _lightPulseFrequencyMs = 20;
        private bool _light = true;
        public bool Light
        {
            get { return _light; }
            set
            {
                if (value) _lightEvent.Set();
                else _lightEvent.Reset();
                _light = value;
            }
        }

        static World()
        {
            _skyColor = new Color(143, 196, 255);
            _multiplyBlend = new BlendState
            {
                AlphaSourceBlend = Blend.DestinationAlpha,
                AlphaDestinationBlend = Blend.Zero,
                AlphaBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.DestinationColor,
                ColorDestinationBlend = Blend.Zero,
                ColorBlendFunction = BlendFunction.Add
            };
            _tilesTexture = Textures.Load("Tiles.png");
            _blackPixel = new Texture2D(Globe.GraphicsDevice, 1, 1);
            _blackPixel.SetData(new Color[1] { Color.Black });
        }
        public World(int width, int height)
        {
            Tiles = new Tile[width, height]; _camera = new Camera(); Zoom = _defaultZoom;
            _lightThread = new Thread(() => { while (true) { _lightEvent.WaitOne(Timeout.Infinite); UpdateLight(); Thread.Sleep(_lightPulseFrequencyMs); } }) { Name = "Lighting", IsBackground = true };
            _lightThread.Start();
        }
        public World(int width, int height, float zoom)
        {
            Tiles = new Tile[width, height]; _camera = new Camera(); Zoom = zoom;
            _lightThread = new Thread(() => { while (true) { _lightEvent.WaitOne(Timeout.Infinite); UpdateLight(); Thread.Sleep(_lightPulseFrequencyMs); } }) { Name = "Lighting", IsBackground = true };
            _lightThread.Start();
        }

        private readonly Camera _camera;
        public float X
        {
            get { return _camera.X; }
            set
            {
                value = MathHelper.Clamp(value, _scrSpanWidth + Tile.Size, Tiles.GetLength(0) * Tile.Size - _scrSpanWidth - Tile.Size);
                _camera.X = value;
                UpdateBounds();
            }
        }
        public float Y
        {
            get { return _camera.Y; }
            set
            {
                value = MathHelper.Clamp(value, _scrSpanHeight + Tile.Size, Tiles.GetLength(1) * Tile.Size - _scrSpanHeight - Tile.Size);
                _camera.Y = value;
                UpdateBounds();
            }
        }
        public Vector2 Position
        {
            get { return _camera.Position; }
            set
            {
                value.X = MathHelper.Clamp(value.X, _scrSpanWidth + Tile.Size, Tiles.GetLength(0) * Tile.Size - _scrSpanWidth - Tile.Size);
                value.Y = MathHelper.Clamp(value.Y, _scrSpanHeight + Tile.Size, Tiles.GetLength(1) * Tile.Size - _scrSpanHeight - Tile.Size);
                _camera.Position = value;
                UpdateBounds();
            }
        }
        public float Zoom
        {
            get { return _camera.Zoom; }
            set
            {
                _scrSpanWidth = ((Screen.BackBufferWidth/2f)/value);
                _scrSpanHeight = ((Screen.BackBufferHeight/2f)/value);
                _lightMap = new RenderTarget2D(Globe.GraphicsDevice, (int)Math.Ceiling(Screen.BackBufferWidth / value / Tile.Size + 2), (int)Math.Ceiling(Screen.BackBufferHeight / value / Tile.Size + 2));
                _camera.Zoom = value;
            }
        }
        public Matrix Matrix => _camera.View();

        private float _scrSpanWidth, _scrSpanHeight;
        private int _tilesMinX, _tilesMinY, _tilesMaxX, _tilesMaxY, _lightMinX = 1, _lightMinY = 1, _lightMaxX, _lightMaxY;
        private void UpdateBounds()
        {
            _tilesMinX = (int)Math.Max(0, Math.Floor((_camera.X - _scrSpanWidth) / Tile.Size - 1));
            _tilesMinY = (int)Math.Max(0, Math.Floor((_camera.Y - _scrSpanHeight) / Tile.Size - 1));
            _tilesMaxX = (int)Math.Min(Tiles.GetLength(0) - 1, Math.Ceiling((_camera.X + _scrSpanWidth) / Tile.Size));
            _tilesMaxY = (int)Math.Min(Tiles.GetLength(1) - 1, Math.Ceiling((_camera.Y + _scrSpanHeight) / Tile.Size));
            _lightMinX = Math.Max(1, _tilesMinX - _lightBuffer);
            _lightMinY = Math.Max(1, _tilesMinY - _lightBuffer);
            _lightMaxX = Math.Min(Tiles.GetLength(0) - 2, _tilesMaxX + _lightBuffer);
            _lightMaxY = Math.Min(Tiles.GetLength(1) - 2, _tilesMaxY + _lightBuffer);
        }
        private ushort LightAbove(int x, int y) { y--; var t = Tiles[x, y]; return t.Empty ? AmbientLight : t.Light; }
        private ushort LightBelow(int x, int y) { y++; var t = Tiles[x, y]; return t.Empty ? AmbientLight : t.Light; }
        private ushort LightLeft(int x, int y) { x--; var t = Tiles[x, y]; return t.Empty ? AmbientLight : t.Light; }
        private ushort LightRight(int x, int y) { x++; var t = Tiles[x, y]; return t.Empty ? AmbientLight : t.Light; }
        private void UpdateLight()
        {
            Profiler.Start("Update Lighting");
            for (var x = _lightMinX; x <= _lightMaxX; x++)
                for (var y = _lightMinY; y <= _lightMaxY; y++)
                {
                    var t = Tiles[x, y];
                    ushort aboveLight = LightAbove(x, y), belowLight = LightBelow(x, y), leftLight = LightLeft(x, y), rightLight = LightRight(x, y), max = Math.Max(aboveLight, Math.Max(belowLight, Math.Max(leftLight, rightLight)));
                    Tiles[x, y].Light = (ushort)Math.Max(t.Empty ? AmbientLight : t.LightGenerated, Math.Max(0, max - (t.BackOnly ? t.BackLightDim : t.ForeLightDim)));
                }
            Profiler.Stop("Update Lighting");
        }
        public void Draw()
        {
            if (_light)
            {
                CreateLightMap();
                Globe.GraphicsDevice.Clear(_skyColor);
                Screen.Setup(SpriteSortMode.BackToFront, SamplerState.PointClamp, _camera.View());
                for (var x = _tilesMinX; x <= _tilesMaxX; x++)
                    for (var y = _tilesMinY; y <= _tilesMaxY; y++)
                    {
                        var t = Tiles[x, y];
                        if (Tiles[x, y].Light <= 0) continue;
                        var pos = new Vector2(x*Tile.Size, y*Tile.Size);
                        if ((t.BackID != 0) && t.DrawBack) Screen.Draw(_tilesTexture, pos, Tile.Source(t.BackID, t.BackStyle), Color.DarkGray, SpriteEffects.None, .75f);
                        if (t.ForeID == 0) continue;
                        Screen.Draw(_tilesTexture, pos, Tile.Source(t.ForeID, t.ForeStyle), SpriteEffects.None, .25f);
                        if (t.HasBorder) Screen.Draw(_tilesTexture, pos, Tile.Border(GenerateStyle(x, y)), SpriteEffects.None, .2f);
                    }
            }
            else
            {
                Globe.GraphicsDevice.Clear(_skyColor);
                Screen.Setup(SpriteSortMode.BackToFront, SamplerState.PointClamp, _camera.View());
                for (var x = _tilesMinX; x <= _tilesMaxX; x++)
                    for (var y = _tilesMinY; y <= _tilesMaxY; y++)
                    {
                        var t = Tiles[x, y];
                        var pos = new Vector2(x * Tile.Size, y * Tile.Size);
                        if ((t.BackID != 0) && t.DrawBack) Screen.Draw(_tilesTexture, pos, Tile.Source(t.BackID, t.BackStyle), Color.DarkGray, SpriteEffects.None, .75f);
                        if (t.ForeID == 0) continue;
                        Screen.Draw(_tilesTexture, pos, Tile.Source(t.ForeID, t.ForeStyle), SpriteEffects.None, .25f);
                        if (t.HasBorder) Screen.Draw(_tilesTexture, pos, Tile.Border(GenerateStyle(x, y)), SpriteEffects.None, .2f);
                    }
            }
        }
        public void DrawLightMap()
        {
            if (_light)
            {
                Screen.Setup(World._multiplyBlend, _camera.View());
                Screen.Draw(_lightMap, new Rectangle(_tilesMinX * Tile.Size, _tilesMinY * Tile.Size, _lightMap.Width * Tile.Size, _lightMap.Height * Tile.Size), SpriteEffects.None, .1f);
                Screen.Cease();
            }
        }
        private void CreateLightMap()
        {
            Profiler.Start("Draw Lighting");
            int j = 0, k = 0;
            Globe.GraphicsDevice.SetRenderTarget(_lightMap);
            Globe.GraphicsDevice.Clear(Color.White);
            Screen.Setup();
            for (var x = _tilesMinX; x <= _tilesMaxX; x++)
            {
                for (var y = _tilesMinY; y <= _tilesMaxY; y++)
                {
                    var t = Tiles[x, y];
                    if (t.Light < byte.MaxValue) Screen.Draw(_blackPixel, new Rectangle(j, k, 1, 1), new Color(255, 255, 255, 255 - Math.Min((ushort)255, t.Light)));
                    k++;
                }
                j++;
                k = 0;
            }
            Screen.Cease();
            Globe.GraphicsDevice.SetRenderTarget(null);
            Profiler.Stop("Draw Lighting");
        }

        public static World Generate(int width, int height)
        {
            var world = new World(width, height);
            int minSurface = height/4 - height/10, maxSurface = height/4 + height/10, surface = Globe.Random(minSurface, maxSurface), surfaceLength = 0, treeSpace = width,
                underground = (surface + Globe.Random(14, 15)), jumpNorm = Globe.Pick(-1, 1), nextJump = -1;
            for (var x = 0; x < width; x++) world.Tiles[x, 0].Fore = world.Tiles[x, height - 1].Fore = Tile.Types.Black;
            for (var y = 0; y < height; y++) world.Tiles[0, y].Fore = world.Tiles[width - 1, y].Fore = Tile.Types.Black;
            var caves = new List<Cave>();
            LoadingText = "Generating Terrain";
            for (var x = 1; x < width - 1; x++)
            {
                if (x == width / 2) world.Spawn = new Point(x, surface - 3);
                for (var y = surface; y < height - 1; y++)
                {
                    if (y == surface)
                    {
                        world.Tiles[x, y].Fore = world.Tiles[x, y].Back = Tile.Types.Dirt;
                        world.Tiles[x, y].ForeStyle = 1;
                        if ((treeSpace > 1) && Globe.Chance(20))
                        {
                            world.GenerateTree(x, (y - 1));
                            treeSpace = -1;
                        }
                    }
                    else if (y < underground) { world.Tiles[x, y].Fore = world.Tiles[x, y].Back = Tile.Types.Dirt; }
                    else
                    {
                        world.Tiles[x, y].Fore = world.Tiles[x, y].Back = Tile.Types.Stone;
                        if (Globe.Chance(1, (height - y))) caves.Add(new Cave(x, y, Globe.Random(60, 180), Globe.Random(3, 4)));
                    }
                    LoadingPercentage = ((((y + 1) + (x * (world.Height - 2))) / (float)((world.Width - 2) * (world.Height - 2))) * 100);
                }
                treeSpace++;
                surfaceLength++;
                if (nextJump > 0) nextJump--;
                if ((nextJump >= 0) && (nextJump < 15) && (jumpNorm == 1)) underground += Globe.Pick(0, 1, 1, 2);
                else if (underground < (surface + 15)) underground += Globe.Pick(0, 1, 1, 2);
                else if (underground > (surface + 15)) underground -= Globe.Pick(0, 1, 1, 2);
                else if (Globe.Chance(30)) underground += Globe.Pick(-1, 0, 0, 0, 1);
                if (Globe.Chance(30))
                {
                    var dif = Globe.Random(-1, 1);
                    if (surfaceLength > 1)
                    {
                        if (nextJump <= 0)
                        {
                            dif = (Globe.Random(10, 20)*jumpNorm);
                            if (dif != 0) { caves.Add(new Cave(x, (surface + (dif/2)), Globe.Random(85, 195), Globe.Random(2, 4), (sbyte) ((jumpNorm > 0) ? -1 : 1))); }
                            nextJump = Globe.Random(40, 340); jumpNorm = Globe.Pick(-1, 1);
                        }
                    }
                    else if (dif == 1) { if ((x > 0) && !world.Tiles[x - 1, surface].Fore.Matches(Tile.Types.Dirt)) dif = Globe.Pick(-1, 0); }
                    else if (dif == -1) { if ((x > 0) && world.Tiles[x - 1, surface].Fore.Matches(Tile.Types.Dirt)) dif = Globe.Pick(0, 1); }
                    if (dif != 0)
                    {
                        surface += dif;
                        if (surface < minSurface) surface = minSurface;
                        if (surface > maxSurface) surface = maxSurface;
                        surfaceLength = 0;
                    }
                }
            }
            LoadingText = "Generating Caves";
            for (var i = 0; i < caves.Count; i++) { world.GenerateCave(caves[i].X, caves[i].Y, caves[i].Steps, caves[i].Size, caves[i].Dir); LoadingPercentage = (((i + 1)/(float) caves.Count)*100); }
            Game.GenDone = true;
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
                        Tiles[x, k].Back = Tile.Types.Log;
                        Tiles[x, k].Fore = Tile.Types.Leaves;
                        if (leavesStyle == 1)
                        {
                            var width = x + 2;
                            for (var j = x - 2; j <= width; j++)
                                if ((j >= 0) && (j < Tiles.GetLength(0)))
                                {
                                    if (Tiles[j, k].Fore == Tile.Types.Log) Tiles[j, k].Back = Tile.Types.Log;
                                    Tiles[j, k].Fore = Tile.Types.Leaves;
                                }
                            width = x + 1;
                            k--;
                            if (k >= 0)
                                for (var j = x - 1; j <= width; j++)
                                    if ((j >= 0) && (j < Tiles.GetLength(0)))
                                    {
                                        if (Tiles[j, k].Fore == Tile.Types.Log) Tiles[j, k].Back = Tile.Types.Log;
                                        Tiles[j, k].Fore = Tile.Types.Leaves;
                                    }
                            k--;
                            if (k >= 0) Tiles[x, k].Fore = Tile.Types.Leaves;
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
                                        if (Tiles[j, l].Fore == Tile.Types.Log) Tiles[j, l].Back = Tile.Types.Log;
                                        Tiles[j, l].Fore = Tile.Types.Leaves;
                                    }
                        }
                    }
                    else
                    {
                        Tiles[x, k].Fore = Tile.Types.Log;
                        if ((leavesStyle == 1) && (k < y) && (k > height + 1))
                        {
                            bool left = (x > 0) && (Tiles[x - 1, k + 1].Fore != Tile.Types.Leaves) && Globe.Chance(20), right = (x < Tiles.GetLength(0) - 1) && (Tiles[x + 1, k + 1].Fore != Tile.Types.Leaves) && Globe.Chance(20);
                            if (left && (Tiles[x - 1, k].ForeID == 0)) Tiles[x - 1, k].Fore = Tile.Types.Leaves;
                            if (right && (Tiles[x + 1, k].ForeID == 0)) Tiles[x + 1, k].Fore = Tile.Types.Leaves;
                        }
                    }
                }
        }
        public struct Cave
        {
            public int X, Y, Steps, Size;
            public sbyte Dir;

            public Cave(int x, int y, int steps, int size, sbyte dir = 0)
            {
                X = x;
                Y = y;
                Steps = steps;
                Size = size;
                Dir = dir;
            }
        }
        public void GenerateCave(int x, int y, int steps, int size, sbyte dir)
        {
            int j = x, k = y;
            var dir2 = Globe.Pick(-1, 1);
            for (var i = 0; i < steps; i++)
            {
                var size2 = (size + Globe.Random(-1, 1));
                for (var l = -size2; l < size2; l++)
                {
                    if ((j + l) < 1) continue;
                    if ((j + l) >= (Width - 1)) break;
                    for (var m = -size2; m < size2; m++)
                    {
                        if ((k + m) < 1) break;
                        if ((k + m) >= (Height - 1)) break;
                        Tiles[(j + l), (k + m)].ForeID = 0;
                    }
                }
                if (Globe.Chance(60))
                {
                    j += ((dir == 0) ? dir2 : dir);
                    k += Globe.Random(1);
                }
                if (Globe.Chance(1, 200)) dir2 = Globe.Pick(-1, 1);
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