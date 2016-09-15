using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpXNA;
using SharpXNA.Input;
using SharpXNA.Plugins;
using Buffer = SharpXNA.Plugins.Buffer;
using Screen = SharpXNA.Screen;
using String = SharpXNA.Plugins.String;

namespace Orbis
{
    public class Game : Microsoft.Xna.Framework.Game
    {
        /// <summary>
        ///     This stores all possible states of the game.
        /// </summary>
        public enum Frames { Menu, Connecting, LoadGame, Game }
        /// <summary>
        ///     The current state of the game.
        /// </summary>
        public static Frames Frame = Frames.Menu;

        /// <summary>
        ///     The local player.
        /// </summary>
        public static Player Self;

        /// <summary>
        ///     An array of the other players on the server.
        /// </summary>
        public static Player[] Players;

        #region Menu/Connecting Variables
        public enum MenuStates { UsernameEntry, HostConnect, IPEntry }
        public static bool Quit = false;
        public static MenuStates MenuState = MenuStates.UsernameEntry;
        public static double BlinkTimer;
        public static Vector2 CamPos;
        public static Vector2? CamWaypoint;
        public static float CamNorm = Globe.Pick(-1, 1);
        #endregion
        #region Game Variables
        public const int LightingUpdateBuffer = 16;
        public const float CameraZoom = 2f, ZoomRate = .1f, CursorOpacitySpeed = 1.2f, CursorOpacityMin = .25f, CursorOpacityMax = .75f, DebugTextScale = .25f;

        public static Dictionary<string, Item> Items;
        public static int MouseTileX, MouseTileY;

        public static Texture2D TilesTexture, LightPixel, LightTile, PlayerTexture, TileSelectionTexture;
        
        public static ushort Light = 285;
        public static World MenuWorld, World;

        public static BlendState Multiply = new BlendState { AlphaSourceBlend = Blend.DestinationAlpha, AlphaDestinationBlend = Blend.Zero, AlphaBlendFunction = BlendFunction.Add, ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero, ColorBlendFunction = BlendFunction.Add };

        public static RenderTarget2D Lighting;
        public static Thread LightingThread;
        public static Camera MenuCamera, Camera;
        private static OrderedDictionary _bufferedStrings;
        public static sbyte CursorOpacitySpeedDir = (sbyte) Globe.Pick(-1, 1);
        public static string LoadingText;
        public static float LineThickness = 2, CursorOpacity = Globe.Random(CursorOpacityMin, CursorOpacityMax), LoadingPercentage;
        public static bool GenStarted, GenDone;

        public static int CamTilesMinX, CamTilesMinY, CamTilesMaxX, CamTilesMaxY, LightTilesMinX, LightTilesMinY, LightTilesMaxX, LightTilesMaxY;
        public static float ScrWidth, ScrHeight;
        public static ushort? MenuMusicChannel;
        #endregion
        
        public Game()
        {
            Globe.GraphicsDeviceManager = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        public static ulong Version { get { return Globe.Version; } set { Globe.Version = value; } }
        public static float Speed { get { return Globe.Speed; } set { Globe.Speed = value; } }
        public static Vector2 Scale => new Vector2(Screen.BackBufferWidth/1920f, Screen.BackBufferHeight/1080f);

        protected override void LoadContent()
        {
            #region Globalization
            Screen.Batch = new Batch(GraphicsDevice);
            Globe.Form = (Form) Control.FromHandle(Window.Handle);
            Globe.GameWindow = Window;
            Globe.ContentManager = Content;
            Globe.GraphicsDevice = GraphicsDevice;
            Globe.Viewport = GraphicsDevice.Viewport;
            Globe.GraphicsAdapter = GraphicsDevice.Adapter;
            #endregion
            #region Initialization
            Sound.Initialize(256);
            Textures.RootDirectory = "Textures";
            Sound.RootDirectory = "Sounds";
            Font.RootDirectory = "Fonts";
            Performance.UpdateFPS = new Buffer(20);
            Performance.DrawFPS = new Buffer(20);
            Network.OnConnectionApproval += Multiplayer.OnConnectionApproval;
            Network.OnStatusChanged += Multiplayer.OnStatusChanged;
            Network.OnData += Multiplayer.OnData;
            #endregion
            Screen.Set(1920, 1080, true);
            IsFixedTimeStep = false;
            Screen.Expand(true);
            IsMouseVisible = true;
            // If the user has already given their Username, send them straight to the Host/Connect screen.
            if (!Settings.Get("Name").IsNullOrEmpty()) MenuState = MenuStates.HostConnect;
            PlayerTexture = Textures.Load("test_char.png");
            Items = LoadItems();
            MenuCamera = new Camera() {Zoom=CameraZoom};
            UpdateResCamStuff(MenuCamera);
            MenuWorld = World.Generate(1200, 400);
            CamPos = new Vector2((MenuWorld.Spawn.X * Tile.Size), (MenuWorld.Spawn.Y * Tile.Size));
            MenuCamera.Position = new Vector2((int)Math.Round(CamPos.X), (int)Math.Round(CamPos.Y));
            UpdateCamBounds(MenuWorld, MenuCamera);
            LoadGameTextures();
            Sound.CompileOggs();
            MenuMusicChannel = Sound.PlayRaw("Glacier.wav");
        }
        public static void LoadGameTextures()
        {
            TilesTexture = Textures.Load("Tiles.png");
            LightPixel = Textures.Load("LightPixel.png");
            LightTile = Textures.Load("LightTile.png");
            TileSelectionTexture = Textures.Load("Selection.png");
        }
        public static Dictionary<string, Item> LoadItems()
        {
            var items = new Dictionary<string, Item>{
                {"Dirt", new Item("Dirt") {MaxStack = 100, Tile = Tile.Tiles.Dirt}},
                {"Stone", new Item("Stone") {MaxStack = 100, Tile = Tile.Tiles.Stone}},
                {"Torch", new Item("Torch") {MaxStack = 100, Tile = Tile.Tiles.Torch}}};
            return items;
        }
        public static void UpdateMenuWorld(GameTime time)
        {
            if (!CamWaypoint.HasValue)
            {
                var nextSurface = new Point((((int)(MenuCamera.Position.X / Tile.Size)) + ((CamNorm > 0) ? 1 : -1)), (int)(MenuCamera.Position.Y / Tile.Size));
                for (var y = 1; y < MenuWorld.Height - 1; y++)
                    if (MenuWorld.Tiles[nextSurface.X, y].Solid)
                    {
                        nextSurface.Y = (y - 1);
                        break;
                    }
                var tileHalved = (Tile.Size / 2f);
                CamWaypoint = (new Vector2(((nextSurface.X * Tile.Size) + tileHalved), ((nextSurface.Y * Tile.Size) + tileHalved)));
            }
            else
            {
                Globe.Move(ref CamPos, CamWaypoint.Value, (Math.Abs(CamNorm * 32) * (float)(time.ElapsedGameTime.TotalSeconds * 4)));
                const float camNormVel = .8f;
                MenuCamera.Position = new Vector2((int)Math.Round(CamPos.X), (int)Math.Round(CamPos.Y));
                UpdateCamBounds(MenuWorld, MenuCamera);
                if (Vector2.Distance(CamPos, CamWaypoint.Value) <= 4) CamWaypoint = null;
                if (CamPos.X >= (((MenuWorld.Width * Tile.Size) - (Screen.BackBufferWidth / 2f)) - (Tile.Size * 8))) { CamNorm = MathHelper.Max(-1, (CamNorm - (camNormVel * (float)time.ElapsedGameTime.TotalSeconds))); }
                else if (CamPos.X <= ((Screen.BackBufferWidth / 2f) + (Tile.Size * 9))) { CamNorm = MathHelper.Min(1, (CamNorm + (camNormVel * (float)time.ElapsedGameTime.TotalSeconds))); }
            }
        }
        protected override void Update(GameTime time)
        {
            Performance.UpdateFPS.Record(1/time.ElapsedGameTime.TotalSeconds);
            Timers.Update(time);
            Globe.IsActive = IsActive;
            Mouse.Update();
            Keyboard.Update(time);
            XboxPad.Update(time);
            if (XboxPad.Pressed(XboxPad.Buttons.Back) || Keyboard.Pressed(Keyboard.Keys.Escape) || Quit) Exit();
            if (Keyboard.Pressed(Keyboard.Keys.F3)) Profiler.Enabled = !Profiler.Enabled;
            Profiler.Start("Frame Update");
            #region Menu/Connecting
            if (Frame == Frames.Menu)
            {
                UpdateMenuWorld(time);
                if (MenuState == MenuStates.UsernameEntry) 
                {
                    if (IsActive)
                    {
                        BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                        if (BlinkTimer <= 0) BlinkTimer += .6;
                        var name = Settings.Get("Name").AcceptInput(String.InputFlags.NoLeadingSpaces | String.InputFlags.NoRepeatingSpaces, 20);
                        Settings.Set("Name", name);
                        if (Keyboard.Pressed(Keyboard.Keys.Enter) && !name.IsNullOrEmpty()) MenuState = MenuStates.HostConnect;
                    }
                }
                else if (MenuState == MenuStates.HostConnect)
                {
                    if (Mouse.Press(Mouse.Buttons.Left))
                    {
                        Vector2 scale = Scale*.75f, size = Font.Load("calibri 50").MeasureString("Host")*scale;
                        var button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f), (int) (Screen.BackBufferHeight/2f - size.Y), (int) size.X, (int) size.Y);
                        if (new Rectangle(Mouse.X, Mouse.Y, 1, 1).Intersects(button))
                        {
                            Multiplayer.CreateLobby(Settings.Get("Name"));
                            if (MenuMusicChannel.HasValue) { Sound.Terminate(MenuMusicChannel.Value); MenuMusicChannel = null; }
                            GenStarted = false;
                            Frame = Frames.LoadGame;
                        }
                        scale = Scale*.75f;
                        size = Font.Load("calibri 50").MeasureString("Connect")*scale;
                        button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f), (int) (Screen.BackBufferHeight/2f + size.Y*.25f), (int) size.X, (int) size.Y);
                        if (new Rectangle(Mouse.X, Mouse.Y, 1, 1).Intersects(button)) MenuState = MenuStates.IPEntry;
                    }
                }
                else if (MenuState == MenuStates.IPEntry)
                {
                    if (IsActive)
                    {
                        BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                        if (BlinkTimer <= 0) BlinkTimer += .6;
                        var ip =
                            Settings.Get("IP").AcceptInput(
                                String.InputFlags.NoLeadingPeriods | String.InputFlags.NoLetters | String.InputFlags.NoSpecalCharacters | String.InputFlags.NoSpaces | String.InputFlags.AllowPeriods | String.InputFlags.NoRepeatingPeriods |
                                String.InputFlags.AllowColons | String.InputFlags.NoRepeatingColons | String.InputFlags.NoLeadingPeriods, 21);
                        Settings.Set("IP", ip);
                        if (Keyboard.Pressed(Keyboard.Keys.Enter) && !ip.IsNullOrEmpty())
                        {
                            Network.Connect(Settings.Get("IP").Split(':')[0], Settings.Get("IP").Contains(":") ? Convert.ToInt32(Settings.Get("IP").Split(':')[1]) : Multiplayer.Port, new Network.Packet(null, Settings.Get("Name")));
                            if (MenuMusicChannel.HasValue) { Sound.Terminate(MenuMusicChannel.Value); MenuMusicChannel = null; }
                            Frame = Frames.Connecting;
                        }
                        else if (Keyboard.Pressed(Keyboard.Keys.Tab)) MenuState = MenuStates.HostConnect;
                    }
                }
                Network.Update();
            }
            else if (Frame == Frames.Connecting)
            {
                UpdateMenuWorld(time);
                BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                if (BlinkTimer <= 0) BlinkTimer += 1;
                Network.Update();
            }
            #endregion
            #region LoadGame/Game
            else if (Frame == Frames.LoadGame)
            {
                UpdateMenuWorld(time);
                BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                if (BlinkTimer <= 0) BlinkTimer += 1;
                if (Network.IsNullOrServer)
                {
                    if (!GenStarted) { LoadingText = null; GenDone = false; var thread = new Thread(() => { World = World.Generate(8400, 2400); }) {IsBackground = true}; thread.Start(); GenStarted = true; }
                    if (GenDone)
                    {
                        InitializeGame();
                        Self.Spawn(World.Spawn);
                        UpdateCamPos();
                        UpdateCamBounds(World, Camera);
                        Frame = Frames.Game;
                    }
                }
                Network.Update();
            }
            else if (Frame == Frames.Game)
            {
                MouseTileX = (int) Math.Floor(Mouse.CameraPosition.X/Tile.Size);
                MouseTileY = (int) Math.Floor(Mouse.CameraPosition.Y/Tile.Size);
                CursorOpacity = MathHelper.Clamp(CursorOpacity + CursorOpacitySpeed*(float) time.ElapsedGameTime.TotalSeconds*CursorOpacitySpeedDir, CursorOpacityMin, CursorOpacityMax);
                if (CursorOpacity.Matches(CursorOpacityMin, CursorOpacityMax)) CursorOpacitySpeedDir *= -1;
                Self.SelfUpdate(time);
                foreach (var t in Players.Where(t => t != null)) t.Update(time);
                if (Settings.IsDebugMode)
                {
                    if (Mouse.ScrolledUp())
                    {
                        Camera.Zoom = MathHelper.Min(8, (float) Math.Round(Camera.Zoom + ZoomRate, 2));
                        InitializeLighting();
                        UpdateResCamStuff(Camera);
                    }
                    if (Mouse.ScrolledDown())
                    {
                        Camera.Zoom = MathHelper.Max(.5f, (float) Math.Round(Camera.Zoom - ZoomRate, 2));
                        InitializeLighting();
                        UpdateResCamStuff(Camera);
                    }
                    if (Keyboard.Pressed(Keyboard.Keys.D1)) Self.AddItem(Items["Dirt"].Clone(Keyboard.HoldingShift() ? 30 : 3));
                    if (Keyboard.Pressed(Keyboard.Keys.D2)) Self.AddItem(Items["Stone"].Clone(Keyboard.HoldingShift() ? 30 : 3));
                }
                UpdateCamPos();
                UpdateCamBounds(World, Camera);
                for (var i = (_bufferedStrings.Count - 1); i >= 0; i--)
                {
                    var bString = (_bufferedStrings[i] as BufferedString);
                    bString.CalculateRectangle(Font.Load("calibri 50"), new Vector2(Self.WorldPosition.X, (Self.WorldPosition.Y - BufferedString.PlayerYOffset)));
                    if (i < (_bufferedStrings.Count - 1))
                    {
                        if (bString.Rectangle.Intersects((_bufferedStrings[i + 1] as BufferedString).Rectangle)) bString.Offset -= (20*(float) time.ElapsedGameTime.TotalSeconds);
                        else bString.Offset += (20*(float) time.ElapsedGameTime.TotalSeconds);
                    }
                    else bString.Offset = MathHelper.Min(0, (bString.Offset + (20 * (float)time.ElapsedGameTime.TotalSeconds)));
                    bString.Life -= (float) time.ElapsedGameTime.TotalSeconds;
                    if (bString.Life <= 0) { _bufferedStrings.RemoveAt(i); i--; }
                }
                Network.Update();
            }
            #endregion
            Profiler.Stop("Frame Update");
            Textures.Dispose();
            Sound.AutoTerminate();
            base.Update(time);
        }

        public static void DrawMenuWorld()
        {
            Screen.Setup(SamplerState.PointClamp, MenuCamera.View());
            for (var x = CamTilesMinX; x <= CamTilesMaxX; x++)
                for (var y = CamTilesMinY; y <= CamTilesMaxY; y++)
                {
                    var pos = new Vector2(x * Tile.Size, y * Tile.Size);
                    if ((MenuWorld.Tiles[x, y].BackID != 0) && MenuWorld.Tiles[x, y].DrawBack) Screen.Draw(TilesTexture, pos, Tile.Source(MenuWorld.Tiles[x, y].BackID, MenuWorld.Tiles[x, y].BackStyle), Color.DarkGray, SpriteEffects.None, .75f);
                    if (MenuWorld.Tiles[x, y].ForeID != 0)
                    {
                        Screen.Draw(TilesTexture, pos, Tile.Source(MenuWorld.Tiles[x, y].ForeID, MenuWorld.Tiles[x, y].ForeStyle), SpriteEffects.None, .25f);
                        if (MenuWorld.Tiles[x, y].HasBorder) Screen.Draw(TilesTexture, pos, Tile.Border(MenuWorld.GenerateStyle(x, y)), SpriteEffects.None, .2f);
                    }
                }
            Screen.Cease();
        }
        protected override void Draw(GameTime time)
        {
            Performance.DrawFPS.Record(1/time.ElapsedGameTime.TotalSeconds);
            GraphicsDevice.Clear(Color.Black);
            Profiler.Start("Frame Draw");
            #region Menu/Connecting
            if (Frame == Frames.Menu)
            {
                GraphicsDevice.Clear(Color.CornflowerBlue);
                DrawMenuWorld();
                Screen.Setup(SpriteSortMode.Deferred, SamplerState.PointClamp);
                Screen.DrawString("Developed by Dcrew", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight - Screen.BackBufferHeight/8f), Color.Gray*.5f, Textures.Origin.Center, Scale*.5f);
                if (MenuState == MenuStates.UsernameEntry)
                {
                    Screen.DrawString("Enter your name!", Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 35*Scale.Y), Color.Gray*.75f, new Textures.Origin(.5f, 1, true), Scale*.75f);
                    Screen.DrawString(Settings.Get("Name") + ((BlinkTimer <= .3f) && IsActive ? "|" : string.Empty), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 30*Scale.Y), Color.Black*.75f,
                        new Textures.Origin(.5f, 0, true), Scale*.75f);
                    Screen.DrawString("Press 'enter' to proceed!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 35*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                }
                else if (MenuState == MenuStates.HostConnect)
                {
                    var font = Font.Load("calibri 30");
                    var scale = Scale*.5f;
                    var size = font.MeasureString("Welcome, ")*scale;
                    Screen.DrawString("Welcome, ", font, new Vector2(Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Settings.Get("Name") + "!").X*scale.X/2f, Screen.BackBufferHeight/2f - size.Y*6), Color.Gray*.75f, null, 0,
                        new Textures.Origin(0, .5f, true), scale);
                    Screen.DrawString(Settings.Get("Name"), font,
                        new Vector2(Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Settings.Get("Name") + "!").X*scale.X/2f + font.MeasureString("Welcome, ").X*scale.X, Screen.BackBufferHeight/2f - size.Y*6), Color.Green*.75f,
                        new Textures.Origin(0, .5f, true), scale);
                    Screen.DrawString("!", font,
                        new Vector2(Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Settings.Get("Name") + "!").X*scale.X/2f + font.MeasureString("Welcome, " + Settings.Get("Name")).X*scale.X,
                            Screen.BackBufferHeight/2f - size.Y*6), Color.Gray*.75f, new Textures.Origin(0, .5f, true), scale);
                    var mouse = new Rectangle(Mouse.X, Mouse.Y, 1, 1);
                    font = Font.Load("calibri 50");
                    scale = Scale*.75f;
                    size = font.MeasureString("Host")*scale;
                    var button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f), (int) (Screen.BackBufferHeight/2f - size.Y), (int) size.X, (int) size.Y);
                    var color = Color.Silver;
                    if (mouse.Intersects(button))
                    {
                        scale += new Vector2(.35f);
                        color = Color.White;
                    }
                    Screen.DrawString("Host", font, new Vector2(button.X + button.Width/2f, button.Y + button.Height/2f), color, Color.Black*.5f, Textures.Origin.Center, scale);
                    scale = Scale*.75f;
                    size = font.MeasureString("Connect")*scale;
                    button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f), (int) (Screen.BackBufferHeight/2f + size.Y*.25f), (int) size.X, (int) size.Y);
                    color = Color.Silver;
                    if (mouse.Intersects(button))
                    {
                        scale += new Vector2(.35f);
                        color = Color.White;
                    }
                    Screen.DrawString("Connect", font, new Vector2(button.X + button.Width/2f, button.Y + button.Height/2f), color, Color.Black*.5f, Textures.Origin.Center, scale);
                }
                else if (MenuState == MenuStates.IPEntry)
                {
                    Screen.DrawString("Server IP:", Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 35*Scale.Y), Color.Gray*.75f, new Textures.Origin(.5f, 1, true), Scale*.75f);
                    Screen.DrawString(Settings.Get("IP") + ((BlinkTimer <= .3f) && IsActive ? "|" : string.Empty), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 30*Scale.Y), Color.Black*.75f,
                        new Textures.Origin(.5f, 0, true), Scale*.75f);
                    Screen.DrawString("Press 'enter' to proceed!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 35*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                    Screen.DrawString("Press 'tab' to go back!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 50*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                }
            }
            else if (Frame == Frames.Connecting)
            {
                GraphicsDevice.Clear(Color.CornflowerBlue);
                DrawMenuWorld();
                Screen.Setup();
                Screen.DrawString("Connecting to " + Settings.Get("IP") + new string('.', 4 - (int) Math.Ceiling(BlinkTimer*4)), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f), Color.White,
                    Textures.Origin.Center, Scale*.5f);
                Screen.Cease();
            }
            #endregion
            #region LoadGame/Game
            else if (Frame == Frames.LoadGame)
            {
                GraphicsDevice.Clear(Color.CornflowerBlue);
                DrawMenuWorld();
                Screen.Setup();
                if (!string.IsNullOrEmpty(LoadingText)) Screen.DrawString((LoadingText + new string('.', 4 - (int) Math.Ceiling(BlinkTimer*4)) + " " + Math.Round(LoadingPercentage) + "%"), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f), Color.White, Textures.Origin.Center, Scale);
                Screen.Cease();
            }
            else if (Frame == Frames.Game)
            {
                DrawLighting();
                GraphicsDevice.Clear(Color.CornflowerBlue);
                Screen.Setup(SamplerState.PointClamp, Camera.View());
                for (var x = CamTilesMinX; x <= CamTilesMaxX; x++)
                    for (var y = CamTilesMinY; y <= CamTilesMaxY; y++)
                        //if ((World.Tiles[x, y].Light > 0) || (World.Tiles[x, y].Fore == Tile.Tiles.Black))
                        {
                            var pos = new Vector2(x*Tile.Size, y*Tile.Size);
                            if ((World.Tiles[x, y].BackID != 0) && World.Tiles[x, y].DrawBack) Screen.Draw(TilesTexture, pos, Tile.Source(World.Tiles[x, y].BackID, World.Tiles[x, y].BackStyle), Color.DarkGray, SpriteEffects.None, .75f);
                            if (World.Tiles[x, y].ForeID != 0)
                            {
                                Screen.Draw(TilesTexture, pos, Tile.Source(World.Tiles[x, y].ForeID, World.Tiles[x, y].ForeStyle), SpriteEffects.None, .25f);
                                if (World.Tiles[x, y].HasBorder) Screen.Draw(TilesTexture, pos, Tile.Border(World.GenerateStyle(x, y)), SpriteEffects.None, .2f);
                            }
                            //Screen.DrawString(Tiles[x, y].Light.ToString(), Font.Load("Consolas"), new Vector2((rect.X + (Tile.Size / 2)), (rect.Y + (Tile.Size / 2))), Color.White, Textures.Origin.Center, new Vector2(.01f * Camera.Zoom));
                            //Screen.Draw(LightTile, rect, new Color(255, 255, 255, (255 - Tiles[x, y].Light)));
                        }
                foreach (var player in Players.Where(player => player != null)) player.Draw();
                Screen.Draw(TileSelectionTexture, new Rectangle(MouseTileX*Tile.Size, MouseTileY*Tile.Size, Tile.Size, Tile.Size), Color.White*CursorOpacity);
                Screen.Cease();
                var invSlot = Textures.Load("Inventory Slot.png");
                Screen.Setup();
                const int itemsPerRow = 7;
                var invScale = 1f; var invPos = new Vector2((Screen.BackBufferWidth - (((invSlot.Width * invScale) * itemsPerRow) + (itemsPerRow - 1)) - 5), 5);
                Self.DrawInventory(invPos, itemsPerRow, invScale);
                if (Settings.IsDebugMode)
                {
                    var rows = (int)Math.Ceiling(Inventory.PlayerInvSize / (float)itemsPerRow);
                    invPos.Y += (rows*(invSlot.Height*invScale)) + 5;
                    invScale = .5f;
                    invPos.X = (Screen.BackBufferWidth - (((invSlot.Width*invScale)*itemsPerRow) + (itemsPerRow - 1)) - 5);
                    foreach (var t in Players.Where(player => !player.Matches(null, Self)))
                    {
                        t.DrawInventory(invPos, itemsPerRow, invScale);
                        invPos.Y += (rows*(invSlot.Height*invScale)) + 10;
                    }
                }
                Screen.Cease();
                Screen.Setup(SpriteSortMode.Deferred, Multiply, Camera.View());
                Screen.Draw(Lighting, new Rectangle(CamTilesMinX * Tile.Size, CamTilesMinY * Tile.Size, Lighting.Width * Tile.Size, Lighting.Height * Tile.Size));
                Screen.Cease();
                if (Settings.IsDebugMode)
                {
                    Screen.Setup();
                    Screen.DrawString(("Zoom: " + Camera.Zoom + " - Direction: " + Self.Direction + " - Inputs: " + Self.LastInput), Font.Load("Consolas"), new Vector2(2), Color.White, Color.Black, new Vector2(DebugTextScale));
                    Screen.DrawString(("CamTiles: " + CamTilesMinX + "," + CamTilesMinY + " - " + CamTilesMaxX + "," + CamTilesMaxY), Font.Load("Consolas"), new Vector2(2, (2 + ((DebugTextScale*100)*1))), Color.White, Color.Black,
                        new Vector2(DebugTextScale));
                    Screen.DrawString(("LightTiles: " + LightTilesMinX + "," + LightTilesMinY + " - " + LightTilesMaxX + "," + LightTilesMaxY), Font.Load("Consolas"), new Vector2(2, (2 + ((DebugTextScale*100)*2))), Color.White, Color.Black,
                        new Vector2(DebugTextScale));
                    Screen.DrawString(("IsFalling: " + Self.IsFalling + " - IsOnGround: " + Self.IsOnGround), Font.Load("Consolas"), new Vector2(2, (2 + ((DebugTextScale*100)*3))), Color.White, Color.Black, new Vector2(DebugTextScale));
                    Screen.DrawString(("TilePos: " + Self.TileX + "," + Self.TileY + " - MoveSpeed: " + Self.MovementSpeed + " - MoveResistance: " + Self.MovementResistance +
                        " - Velocity: " + Math.Round(Self.Velocity.X, 1) + "," + Math.Round(Self.Velocity.Y, 1)), Font.Load("Consolas"), new Vector2(2, (2 + ((DebugTextScale*100)*4))), Color.White, Color.Black,
                        new Vector2(DebugTextScale));
                    Screen.Cease();
                }
                if (_bufferedStrings.Count > 0)
                {
                    Screen.Setup(Camera.View());
                    foreach (var v in _bufferedStrings.Values)
                    {
                        var bString = (v as BufferedString);
                        Screen.DrawString(bString.Text, Font.Load("calibri 50"), new Vector2(Self.WorldPosition.X, (Self.WorldPosition.Y - BufferedString.PlayerYOffset + bString.Offset)), (Color.White * MathHelper.Clamp((float)bString.Life, 0, 1)),
                            Textures.Origin.Center, new Vector2(BufferedString.Scale));
                    }
                    Screen.Cease();
                }
            }
            #endregion
            Profiler.Stop("Frame Draw");
            if (Profiler.Enabled) Profiler.Draw(430);
            // Just in case.
            if (Screen.IsSetup) Screen.Cease();
            base.Draw(time);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            Multiplayer.QuitLobby();
            base.OnExiting(sender, args);
        }

        public static ushort AboveLight(int x, int y)
        {
            y--;
            if (y < 0) return 0;
            return World.Tiles[x, y].Empty ? Light : World.Tiles[x, y].Light;
        }
        public static ushort BelowLight(int x, int y)
        {
            y++;
            if (y >= World.Tiles.GetLength(1)) return 0;
            return World.Tiles[x, y].Empty ? Light : World.Tiles[x, y].Light;
        }
        public static ushort LeftLight(int x, int y)
        {
            x--;
            if (x < 0) return 0;
            return World.Tiles[x, y].Empty ? Light : World.Tiles[x, y].Light;
        }
        public static ushort RightLight(int x, int y)
        {
            x++;
            if (x >= World.Tiles.GetLength(0)) return 0;
            return World.Tiles[x, y].Empty ? Light : World.Tiles[x, y].Light;
        }
        public static void UpdateResCamStuff(Camera camera)
        {
            ScrWidth = Screen.BackBufferWidth/2f/ camera.Zoom;
            ScrHeight = Screen.BackBufferHeight/2f/ camera.Zoom;
            //LineThickness = (1/Camera.Zoom);
        }
        public static void UpdateCamPos()
        {
            Camera.Position = new Vector2(MathHelper.Clamp(Self.WorldPosition.X, ScrWidth + Tile.Size, World.Tiles.GetLength(0)*Tile.Size - ScrWidth - Tile.Size),
                MathHelper.Clamp(Self.WorldPosition.Y, ScrHeight + Tile.Size, World.Tiles.GetLength(1)*Tile.Size - ScrHeight - Tile.Size));
        }
        public static void UpdateCamBounds(World world, Camera camera)
        {
            CamTilesMinX = (int) Math.Max(0, Math.Floor((camera.X - ScrWidth)/Tile.Size - 1));
            CamTilesMinY = (int) Math.Max(0, Math.Floor((camera.Y - ScrHeight)/Tile.Size - 1));
            CamTilesMaxX = (int) Math.Min(world.Tiles.GetLength(0) - 1, Math.Ceiling((camera.X + ScrWidth)/Tile.Size));
            CamTilesMaxY = (int) Math.Min(world.Tiles.GetLength(1) - 1, Math.Ceiling((camera.Y + ScrHeight)/Tile.Size));
            LightTilesMinX = Math.Max(0, CamTilesMinX - LightingUpdateBuffer);
            LightTilesMinY = Math.Max(0, CamTilesMinY - LightingUpdateBuffer);
            LightTilesMaxX = Math.Min(world.Tiles.GetLength(0) - 1, CamTilesMaxX + LightingUpdateBuffer);
            LightTilesMaxY = Math.Min(world.Tiles.GetLength(1) - 1, CamTilesMaxY + LightingUpdateBuffer);
        }
        public static void InitializeGame() { Camera = new Camera { Zoom = CameraZoom }; UpdateResCamStuff(Camera); InitializeLighting(); InitializeLightingThread(); LightingThread.Start(); LoadGameTextures(); _bufferedStrings = new OrderedDictionary(); }
        public static void InitializeLighting() { Lighting = new RenderTarget2D(Globe.GraphicsDevice, (int) Math.Ceiling(Screen.BackBufferWidth/Camera.Zoom/Tile.Size + 2), (int) Math.Ceiling(Screen.BackBufferHeight/Camera.Zoom/Tile.Size + 2)); }
        public static void InitializeLightingThread() { LightingThread = new Thread(() => { while (true) { UpdateLighting(); Thread.Sleep(50); } }) { Name = "Lighting", IsBackground = true }; }
        public static void UpdateLighting()
        {
            Profiler.Start("Update Lighting");
            for (var x = LightTilesMinX; x <= LightTilesMaxX; x++)
                for (var y = LightTilesMinY; y <= LightTilesMaxY; y++)
                {
                    ushort aboveLight = AboveLight(x, y), belowLight = BelowLight(x, y), leftLight = LeftLight(x, y), rightLight = RightLight(x, y), max = Math.Max(aboveLight, Math.Max(belowLight, Math.Max(leftLight, rightLight)));
                    World.Tiles[x, y].Light = (ushort) Math.Max(World.Tiles[x, y].Empty ? Light : World.Tiles[x, y].LightGenerated, Math.Max(0, max - (World.Tiles[x, y].BackOnly ? World.Tiles[x, y].BackLightDim : World.Tiles[x, y].ForeLightDim)));
                }
            Profiler.Stop("Update Lighting");
        }
        public static void DrawLighting()
        {
            Profiler.Start("Draw Lighting");
            int j = 0, k = 0;
            Globe.GraphicsDevice.SetRenderTarget(Lighting);
            Globe.GraphicsDevice.Clear(Color.White);
            Screen.Setup();
            for (var x = CamTilesMinX; x <= CamTilesMaxX; x++)
            {
                for (var y = CamTilesMinY; y <= CamTilesMaxY; y++)
                {
                    if (World.Tiles[x, y].Light < byte.MaxValue) Screen.Draw(LightPixel, new Rectangle(j, k, 1, 1), new Color(255, 255, 255, 255 - Math.Min((ushort) 255, World.Tiles[x, y].Light)));
                    k++;
                }
                j++;
                k = 0;
            }
            Screen.Cease();
            Globe.GraphicsDevice.SetRenderTarget(null);
            Profiler.Stop("Draw Lighting");
        }

        /// <summary>
        /// Adds the details of the given item to a buffer to be drawn to the screen.
        /// </summary>
        /// <param name="item">The string to draw.</param>
        public static void QueueItemPickupText(Item item)
        {
            var key = ("Picked up " + item.Key);
            if (_bufferedStrings.Contains(key))
            {
                var bString = (_bufferedStrings[key] as BufferedString);
                var numStr = bString.Text.Split('(')[1];
                var count = Convert.ToInt32(numStr.Substring(0, (numStr.Length - 1)));
                bString.Text = (item.Key + " (" + (count + item.Stack) + ")");
                bString.Life = BufferedString.StartingLife;
                return;
            }
            _bufferedStrings.Add(key, new BufferedString(item.Key + " (" + item.Stack + ")"));
        }
    }
}