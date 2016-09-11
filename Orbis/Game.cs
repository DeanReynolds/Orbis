using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orbis.World;
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
        public enum Frames
        {
            Menu,
            Connecting,
            LoadGame,
            Game
        }
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
        public enum MenuStates
        {
            UsernameEntry,
            HostConnect,
            IPEntry
        }
        public static bool Quit = false;
        public static MenuStates MenuState = MenuStates.UsernameEntry;
        public static double BlinkTimer;
        #endregion
        #region Game Variables
        public const int ChunkWidth = 160, ChunkHeight = 120, LightingUpdateBuffer = 16;


        public const float CameraZoom = 2f, ZoomRate = .1f, CursorOpacitySpeed = 1.2f, CursorOpacityMin = .25f, CursorOpacityMax = .75f;


        public static int MouseTileX, MouseTileY;
        public static Tile[,] Tiles;

        public static Texture2D TilesTexture, LightPixel, LightTile, PlayerTexture, TileSelectionTexture;

        public static Point Spawn;
        public static ushort Light = 285;

        public static BlendState Multiply = new BlendState
        {
            AlphaSourceBlend = Blend.DestinationAlpha, AlphaDestinationBlend = Blend.Zero, AlphaBlendFunction = BlendFunction.Add, ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero,
            ColorBlendFunction = BlendFunction.Add
        };

        public static RenderTarget2D Lighting;
        public static Thread LightingThread;
        public static Camera Camera;
        public static sbyte CursorOpacitySpeedDir = (sbyte) Globe.Pick(-1, 1);

        public static float LineThickness = 1, CursorOpacity = Globe.Random(CursorOpacityMin, CursorOpacityMax);

        public static int CamTilesMinX, CamTilesMinY, CamTilesMaxX, CamTilesMaxY, LightTilesMinX, LightTilesMinY, LightTilesMaxX, LightTilesMaxY;

        public static float ScrWidth, ScrHeight;
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
            Sound.RootDirectory = "Sound";
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
        }
        public static void LoadGameTextures()
        {
            TilesTexture = Textures.Load("Tiles.png");
            LightPixel = Textures.Load("LightPixel.png");
            LightTile = Textures.Load("LightTile.png");
            TileSelectionTexture = Textures.Load("Selection.png");
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
            switch (Frame)
            {
                    #region Menu/Connecting
                case Frames.Menu:
                    switch (MenuState)
                    {
                        case MenuStates.UsernameEntry:
                            if (IsActive)
                            {
                                BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                                if (BlinkTimer <= 0) BlinkTimer += .6;
                                var name = Settings.Get("Name").AcceptInput(String.InputFlags.NoLeadingSpaces | String.InputFlags.NoRepeatingSpaces, 20);
                                Settings.Set("Name", name);
                                if (Keyboard.Pressed(Keyboard.Keys.Enter) && !name.IsNullOrEmpty()) MenuState = MenuStates.HostConnect;
                            }
                            break;
                        case MenuStates.HostConnect:
                            if (Mouse.Press(Mouse.Buttons.Left))
                            {
                                Vector2 scale = Scale*.75f, size = Font.Load("calibri 50").MeasureString("Host")*scale;
                                var button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f), (int) (Screen.BackBufferHeight/2f - size.Y), (int) size.X, (int) size.Y);
                                if (new Rectangle(Mouse.X, Mouse.Y, 1, 1).Intersects(button))
                                {
                                    Multiplayer.CreateLobby(Settings.Get("Name"));
                                    Frame = Frames.LoadGame;
                                }
                                scale = Scale*.75f;
                                size = Font.Load("calibri 50").MeasureString("Connect")*scale;
                                button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f), (int) (Screen.BackBufferHeight/2f + size.Y*.25f), (int) size.X, (int) size.Y);
                                if (new Rectangle(Mouse.X, Mouse.Y, 1, 1).Intersects(button)) MenuState = MenuStates.IPEntry;
                            }
                            break;
                        case MenuStates.IPEntry:
                            if (IsActive)
                            {
                                BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                                if (BlinkTimer <= 0) BlinkTimer += .6;
                                var ip =
                                    Settings.Get("IP").AcceptInput(
                                        String.InputFlags.NoLeadingPeriods | String.InputFlags.NoLetters | String.InputFlags.NoSpecalCharacters | String.InputFlags.NoSpaces | String.InputFlags.AllowPeriods |
                                        String.InputFlags.NoRepeatingPeriods | String.InputFlags.AllowColons | String.InputFlags.NoRepeatingColons | String.InputFlags.NoLeadingPeriods, 21);
                                Settings.Set("IP", ip);
                                if (Keyboard.Pressed(Keyboard.Keys.Enter) && !ip.IsNullOrEmpty())
                                {
                                    Network.Connect(Settings.Get("IP").Split(':')[0], Settings.Get("IP").Contains(":") ? Convert.ToInt32(Settings.Get("IP").Split(':')[1]) : 6121, new Network.Packet(null, Settings.Get("Name")));
                                    Frame = Frames.Connecting;
                                }
                                else if (Keyboard.Pressed(Keyboard.Keys.Tab)) MenuState = MenuStates.HostConnect;
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    Network.Update();
                    break;
                case Frames.Connecting:
                    BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                    if (BlinkTimer <= 0) BlinkTimer += 1;
                    Network.Update();
                    break;
                    #endregion
                    #region LoadGame/Game
                case Frames.LoadGame:
                    BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                    if (BlinkTimer <= 0) BlinkTimer += 1;
                    if (Network.IsNullOrServer)
                    {
                        Camera = new Camera {Zoom = CameraZoom};
                        UpdateResCamStuff();
                        LineThickness = 1/Camera.Zoom;
                        Tiles = Generation.Generate(8400, 2400, out Spawn);
                        Self.Spawn(Spawn); Camera.Position = Self.WorldPosition;
                        UpdateCamPos(); UpdateCamBounds(); InitializeLighting();
                        LightingThread = new Thread(() =>
                        {
                            while (true)
                            {
                                UpdateLighting();
                                Thread.Sleep(100);
                            }
                        }) {Name = "Lighting", IsBackground = true};
                        LightingThread.Start();
                        LoadGameTextures();
                        Frame = Frames.Game;
                    }
                    Network.Update();
                    break;
                case Frames.Game:
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
                            UpdateResCamStuff();
                        }
                        if (Mouse.ScrolledDown())
                        {
                            Camera.Zoom = MathHelper.Max(.5f, (float) Math.Round(Camera.Zoom - ZoomRate, 2));
                            InitializeLighting();
                            UpdateResCamStuff();
                        }
                    }
                    UpdateCamPos();
                    UpdateCamBounds();
                    if (Network.IsServer)
                        while (Timers.Tick("posSync"))
                            foreach (var player in Players)
                                if (player?.Connection != null)
                                {
                                    var packet = new Network.Packet((byte) Multiplayer.Packets.Position);
                                    foreach (var other in
                                        Players.Where(other => !other.Matches(null, player))) packet.Add(other.Slot, other.LinearPosition, other.Velocity);
                                    packet.SendTo(player.Connection, NetDeliveryMethod.UnreliableSequenced, 1);
                                }
                    Network.Update();
                    break;
                    #endregion
            }
            Profiler.Stop("Frame Update");
            Textures.Dispose();
            Sound.AutoTerminate();
            base.Update(time);
        }
        protected override void Draw(GameTime time)
        {
            Performance.DrawFPS.Record(1/time.ElapsedGameTime.TotalSeconds);
            GraphicsDevice.Clear(Color.Black);
            Profiler.Start("Frame Draw");
            switch (Frame)
            {
                    #region Menu/Connecting
                case Frames.Menu:
                    GraphicsDevice.Clear(Color.WhiteSmoke);
                    Screen.Setup(SpriteSortMode.Deferred, SamplerState.PointClamp);
                    Screen.DrawString("Developed by Dcrew", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight - Screen.BackBufferHeight/8f), Color.Gray*.5f, Textures.Origin.Center, Scale*.5f);
                    switch (MenuState)
                    {
                        case MenuStates.UsernameEntry:
                            Screen.DrawString("Enter your name!", Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 35*Scale.Y), Color.Gray*.75f, new Textures.Origin(.5f, 1, true), Scale*.75f);
                            Screen.DrawString(Settings.Get("Name") + ((BlinkTimer <= .3f) && IsActive ? "|" : string.Empty), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 30*Scale.Y),
                                Color.Black*.75f, new Textures.Origin(.5f, 0, true), Scale*.75f);
                            Screen.DrawString("Press 'enter' to proceed!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 35*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true),
                                Scale*.5f);
                            break;
                        case MenuStates.HostConnect:
                            var font = Font.Load("calibri 30");
                            var scale = Scale*.5f;
                            var size = font.MeasureString("Welcome, ")*scale;
                            Screen.DrawString("Welcome, ", font, new Vector2(Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Settings.Get("Name") + "!").X*scale.X/2f, Screen.BackBufferHeight/2f - size.Y*6), Color.Gray*.75f,
                                null, 0, new Textures.Origin(0, .5f, true), scale);
                            Screen.DrawString(Settings.Get("Name"), font,
                                new Vector2(Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Settings.Get("Name") + "!").X*scale.X/2f + font.MeasureString("Welcome, ").X*scale.X, Screen.BackBufferHeight/2f - size.Y*6),
                                Color.Green*.75f, new Textures.Origin(0, .5f, true), scale);
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
                            break;
                        case MenuStates.IPEntry:
                            Screen.DrawString("Server IP:", Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 35*Scale.Y), Color.Gray*.75f, new Textures.Origin(.5f, 1, true), Scale*.75f);
                            Screen.DrawString(Settings.Get("IP") + ((BlinkTimer <= .3f) && IsActive ? "|" : string.Empty), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 30*Scale.Y),
                                Color.Black*.75f, new Textures.Origin(.5f, 0, true), Scale*.75f);
                            Screen.DrawString("Press 'enter' to proceed!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 35*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true),
                                Scale*.5f);
                            Screen.DrawString("Press 'tab' to go back!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 50*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true),
                                Scale*.5f);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                case Frames.Connecting:
                    Screen.Setup();
                    Screen.DrawString("Connecting to " + Settings.Get("IP") + new string('.', 4 - (int) Math.Ceiling(BlinkTimer*4)), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f), Color.White,
                        Textures.Origin.Center, Scale*.5f);
                    Screen.Cease();
                    break;
                    #endregion
                    #region LoadGame/Game
                case Frames.LoadGame:
                    Screen.Setup();
                    Screen.DrawString("Loading" + new string('.', 4 - (int) Math.Ceiling(BlinkTimer*4)), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f), Color.White, Textures.Origin.Center,
                        Scale*.5f);
                    Screen.Cease();
                    break;
                case Frames.Game:
                    DrawLighting();
                    GraphicsDevice.Clear(Color.CornflowerBlue);
                    Screen.Setup(SamplerState.PointClamp, Camera.View());
                    for (var x = CamTilesMinX; x <= CamTilesMaxX; x++)
                        for (var y = CamTilesMinY; y <= CamTilesMaxY; y++)
                            if ((Tiles[x, y].Light > 0) || (Tiles[x, y].Fore == Tile.Tiles.Black))
                            {
                                var pos = new Vector2(x*Tile.Size, y*Tile.Size);
                                if ((Tiles[x, y].BackID != 0) && Tiles[x, y].DrawBack) Screen.Draw(TilesTexture, pos, Tile.Source(Tiles[x, y].BackID, 0), Color.DarkGray, SpriteEffects.None, .75f);
                                if (Tiles[x, y].ForeID != 0)
                                {
                                    Screen.Draw(TilesTexture, pos, Tile.Source(Tiles[x, y].ForeID, Tiles[x, y].Style), SpriteEffects.None, .25f);
                                    if (Tiles[x, y].HasBorder) Screen.Draw(TilesTexture, pos, Tile.Border(Generation.GenerateStyle(ref Tiles, x, y)), SpriteEffects.None, .2f);
                                }
                                //Screen.DrawString(Tiles[x, y].Light.ToString(), Font.Load("Consolas"), new Vector2((rect.X + (Tile.Size / 2)), (rect.Y + (Tile.Size / 2))), Color.White, Textures.Origin.Center, new Vector2(.01f * Camera.Zoom));
                                //Screen.Draw(LightTile, rect, new Color(255, 255, 255, (255 - Tiles[x, y].Light)));
                            }
                    foreach (var player in Players.Where(player => player != null)) player.Draw();
                    Screen.Draw(TileSelectionTexture, new Rectangle(MouseTileX*Tile.Size, MouseTileY*Tile.Size, Tile.Size, Tile.Size), Color.White*CursorOpacity);
                    Screen.Cease();
                    Screen.Setup(SpriteSortMode.Deferred, Multiply, Camera.View(Camera.Samplers.Point));
                    Screen.Draw(Lighting, new Rectangle(CamTilesMinX*Tile.Size, CamTilesMinY*Tile.Size, Lighting.Width*Tile.Size, Lighting.Height*Tile.Size));
                    Screen.Cease();
                    Screen.Setup();
                    Screen.DrawString("Zoom: " + Camera.Zoom, Font.Load("Consolas"), new Vector2(2), Color.White, Color.Black, new Vector2(.35f));
                    //Screen.DrawString(("CamTiles: " + CamTilesMinX + "," + CamTilesMinY + " - " + CamTilesMaxX + "," + CamTilesMaxY), Font.Load("Consolas"), new Vector2(0, 37), Color.White, Color.Black, new Vector2(.35f));
                    //Screen.DrawString(("LightTiles: " + LightTilesMinX + "," + LightTilesMinY + " - " + LightTilesMaxX + "," + LightTilesMaxY), Font.Load("Consolas"), new Vector2(0, 72), Color.White, Color.Black, new Vector2(.35f));
                    Screen.Cease();
                    break;
                    #endregion
            }
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

        public static bool InBounds(int x, int y) { return InBounds(ref Tiles, x, y); }
        public static bool InBounds(ref Tile[,] tiles, int x, int y) { return !((x < 0) || (y < 0) || (x >= tiles.GetLength(0)) || (y >= tiles.GetLength(1))); }
        public static ushort AboveLight(int x, int y)
        {
            y--;
            if (y < 0) return 0;
            return Tiles[x, y].Empty ? Light : Tiles[x, y].Light;
        }
        public static ushort BelowLight(int x, int y)
        {
            y++;
            if (y >= Tiles.GetLength(1)) return 0;
            return Tiles[x, y].Empty ? Light : Tiles[x, y].Light;
        }
        public static ushort LeftLight(int x, int y)
        {
            x--;
            if (x < 0) return 0;
            return Tiles[x, y].Empty ? Light : Tiles[x, y].Light;
        }
        public static ushort RightLight(int x, int y)
        {
            x++;
            if (x >= Tiles.GetLength(0)) return 0;
            return Tiles[x, y].Empty ? Light : Tiles[x, y].Light;
        }
        public static void UpdateResCamStuff()
        {
            ScrWidth = Screen.BackBufferWidth/2f/Camera.Zoom;
            ScrHeight = Screen.BackBufferHeight/2f/Camera.Zoom;
        }
        public static void UpdateCamPos()
        {
            Camera.Position = new Vector2(MathHelper.Clamp(Self.WorldPosition.X, ScrWidth + Tile.Size, Tiles.GetLength(0)*Tile.Size - ScrWidth - Tile.Size),
                MathHelper.Clamp(Self.WorldPosition.Y, ScrHeight + Tile.Size, Tiles.GetLength(1)*Tile.Size - ScrHeight - Tile.Size));
        }
        public static void UpdateCamBounds()
        {
            CamTilesMinX = (int) Math.Max(0, Math.Floor((Camera.X - ScrWidth)/Tile.Size - 1));
            CamTilesMinY = (int) Math.Max(0, Math.Floor((Camera.Y - ScrHeight)/Tile.Size - 1));
            CamTilesMaxX = (int) Math.Min(Tiles.GetLength(0) - 1, Math.Ceiling((Camera.X + ScrWidth)/Tile.Size));
            CamTilesMaxY = (int) Math.Min(Tiles.GetLength(1) - 1, Math.Ceiling((Camera.Y + ScrHeight)/Tile.Size));
            LightTilesMinX = Math.Max(0, CamTilesMinX - LightingUpdateBuffer);
            LightTilesMinY = Math.Max(0, CamTilesMinY - LightingUpdateBuffer);
            LightTilesMaxX = Math.Min(Tiles.GetLength(0) - 1, CamTilesMaxX + LightingUpdateBuffer);
            LightTilesMaxY = Math.Min(Tiles.GetLength(1) - 1, CamTilesMaxY + LightingUpdateBuffer);
        }
        public static void InitializeLighting()
        {
            Lighting = new RenderTarget2D(Globe.GraphicsDevice, (int) Math.Ceiling(Screen.BackBufferWidth/Camera.Zoom/Tile.Size + 2), (int) Math.Ceiling(Screen.BackBufferHeight/Camera.Zoom/Tile.Size + 2));
        }
        public static void UpdateLighting()
        {
            Profiler.Start("Update Lighting");
            for (var x = LightTilesMinX; x <= LightTilesMaxX; x++)
                for (var y = LightTilesMinY; y <= LightTilesMaxY; y++)
                {
                    ushort aboveLight = AboveLight(x, y), belowLight = BelowLight(x, y), leftLight = LeftLight(x, y), rightLight = RightLight(x, y), max = Math.Max(aboveLight, Math.Max(belowLight, Math.Max(leftLight, rightLight)));
                    Tiles[x, y].Light = (ushort) Math.Max(Tiles[x, y].Empty ? Light : Tiles[x, y].LightGenerated, Math.Max(0, max - (Tiles[x, y].BackOnly ? Tiles[x, y].BackLightDim : Tiles[x, y].ForeLightDim)));
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
                    Screen.Draw(LightPixel, new Rectangle(j, k, 1, 1), new Color(255, 255, 255, 255 - Math.Min((ushort) 255, Tiles[x, y].Light)));
                    k++;
                }
                j++;
                k = 0;
            }
            Screen.Cease();
            Globe.GraphicsDevice.SetRenderTarget(null);
            Profiler.Stop("Draw Lighting");
        }
    }
}