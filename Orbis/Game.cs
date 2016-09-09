using System;
using System.Linq;
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
    using System.Threading;
    using Packet = Network.Packet;
    using Packets = Multiplayer.Packets;

    public class Game : Microsoft.Xna.Framework.Game
    {
        public enum Frames { Menu, Connecting, LoadGame, Game }
        public static Frames Frame = Frames.Menu;

        public static Player Self;
        public static Player[] Players;
        public static bool Quit = false;

        public Game() { Globe.GraphicsDeviceManager = new GraphicsDeviceManager(this); Content.RootDirectory = "Content"; }

        public static ulong Version { get { return Globe.Version; } set { Globe.Version = value; } }
        public static float Speed { get { return Globe.Speed; } set { Globe.Speed = value; } }

        public static Vector2 Scale => new Vector2(Screen.BackBufferWidth/1920f, Screen.BackBufferHeight/1080f);
        
        #region Menu/Connecting Variables
        public enum MenuStates { UsernameEntry, HostConnect, IPEntry }
        public static MenuStates MenuState = MenuStates.UsernameEntry;
        public static double BlinkTimer;
        #endregion
        #region Game Variables
        public const int TileSize = 8, ChunkWidth = 160, ChunkHeight = 120, LightingUpdateBuffer = 16;
        public static Tile[,] Tiles;
        public static Texture2D TilesTexture, LightPixel, LightTile, PlayerTexture, TileSelectionTexture;
        public static Point Spawn;
        public static ushort Light = 285;
        public static BlendState Multiply = new BlendState { AlphaSourceBlend = Blend.DestinationAlpha, AlphaDestinationBlend = Blend.Zero, AlphaBlendFunction = BlendFunction.Add, ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero, ColorBlendFunction = BlendFunction.Add };
        public static RenderTarget2D Lighting;
        public static Thread LightingThread;
        public static Camera Camera;
        public const float CameraZoom = 2f, ZoomRate = .05f, CursorOpacitySpeed = .02f, CursorOpacityMin = .25f, CursorOpacityMax = .75f;
        public static sbyte CursorOpacitySpeedDir = (sbyte)Globe.Pick(-1, 1);
        public static float LineThickness = 1, CursorOpacity = Globe.Random(CursorOpacityMin, CursorOpacityMax);
        public static int MouseTileX, MouseTileY;
        #endregion

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
            Performance.UpdateFPS.Record(1 / time.ElapsedGameTime.TotalSeconds);
            Mouse.Update();
            Keyboard.Update(time);
            XboxPad.Update(time);
            Timers.Update(time);
            Globe.IsActive = IsActive;
            if (XboxPad.Pressed(XboxPad.Buttons.Back) || Keyboard.Pressed(Keyboard.Keys.Escape) || Quit) Exit();
            if (Keyboard.Pressed(Keyboard.Keys.F3)) Profiler.Enabled = !Profiler.Enabled;
            Profiler.Start("Frame Update");
            #region Menu/Connecting
            if (Frame == Frames.Menu)
            {
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
                            Vector2 scale = Scale * .75f, size = Font.Load("calibri 50").MeasureString("Host") * scale;
                            var button = new Rectangle((int)(Screen.BackBufferWidth / 2f - size.X / 2f),
                                (int)(Screen.BackBufferHeight / 2f - size.Y), (int)size.X, (int)size.Y);
                            if (new Rectangle(Mouse.X, Mouse.Y, 1, 1).Intersects(button))
                            {
                                Multiplayer.CreateLobby(Settings.Get("Name"));
                                Frame = Frames.LoadGame;
                            }
                            scale = Scale * .75f;
                            size = Font.Load("calibri 50").MeasureString("Connect") * scale;
                            button = new Rectangle((int)(Screen.BackBufferWidth / 2f - size.X / 2f),
                                (int)(Screen.BackBufferHeight / 2f + size.Y * .25f), (int)size.X, (int)size.Y);
                            if (new Rectangle(Mouse.X, Mouse.Y, 1, 1).Intersects(button)) MenuState = MenuStates.IPEntry;
                        }
                        break;
                    case MenuStates.IPEntry:
                        if (IsActive)
                        {
                            BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                            if (BlinkTimer <= 0) BlinkTimer += .6;
                            var ip = Settings.Get("IP").AcceptInput(
                                    String.InputFlags.NoLeadingPeriods | String.InputFlags.NoLetters |
                                    String.InputFlags.NoSpecalCharacters | String.InputFlags.NoSpaces |
                                    String.InputFlags.AllowPeriods |
                                    String.InputFlags.NoRepeatingPeriods | String.InputFlags.AllowColons |
                                    String.InputFlags.NoRepeatingColons | String.InputFlags.NoLeadingPeriods, 21);
                            Settings.Set("IP", ip);
                            if (Keyboard.Pressed(Keyboard.Keys.Enter) && !ip.IsNullOrEmpty())
                            {
                                Network.Connect(Settings.Get("IP").Split(':')[0],
                                    Settings.Get("IP").Contains(":") ? Convert.ToInt32(Settings.Get("IP").Split(':')[1]) : 6121,
                                    new Packet(null, Settings.Get("Name")));
                                Frame = Frames.Connecting;
                            }
                            else if (Keyboard.Pressed(Keyboard.Keys.Tab)) MenuState = MenuStates.HostConnect;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                Network.Update();
            }
            else if (Frame == Frames.Connecting)
            {
                BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                if (BlinkTimer <= 0) BlinkTimer += 1;
                Network.Update();
            }
            #endregion
            #region LoadGame/Game
            else if (Frame == Frames.LoadGame)
            {
                BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                if (BlinkTimer <= 0) BlinkTimer += 1;
                if (Network.IsNullOrServer)
                {
                    // Create camera.
                    Camera = new Camera {Zoom = CameraZoom};
                    UpdateResCamStuff();
                    LineThickness = (1 / Camera.Zoom);
                    Tiles = Generation.Generate(8400, 2400, out Spawn);
                    Self.Position = new Vector2((Spawn.X*TileSize), (Spawn.Y*TileSize));
                    Self.UpdateTilePos(); Self.UpdateLastTilePos();
                    Camera.Position = Self.Position;
                    UpdateCamTilesPos();
                    Lighting = new RenderTarget2D(Globe.GraphicsDevice, (int)Math.Ceiling((Screen.BackBufferWidth / Camera.Zoom) / TileSize + 1), (int)Math.Ceiling((Screen.BackBufferHeight / Camera.Zoom) / TileSize + 1));
                    LightingThread = new Thread(() => { while (true) { UpdateLighting(); Thread.Sleep(100); } }) { Name = "Lighting", IsBackground = true };
                    LightingThread.Start();
                    LoadGameTextures();
                    Frame = Frames.Game;
                }
                Network.Update();
            }
            else if (Frame == Frames.Game)
            {
                MouseTileX = (int)Math.Floor(Mouse.CameraPosition.X / TileSize);
                MouseTileY = (int)Math.Floor(Mouse.CameraPosition.Y / TileSize);
                CursorOpacity = MathHelper.Clamp((CursorOpacity + (CursorOpacitySpeed * CursorOpacitySpeedDir)), CursorOpacityMin, CursorOpacityMax);
                if (CursorOpacity.Matches(CursorOpacityMin, CursorOpacityMax)) CursorOpacitySpeedDir *= -1;
                Self.SelfUpdate(time);
                foreach (var t in Players.Where(t => t != null)) t.Update(time);
                if (Timers.Tick("posSync") && Network.IsServer)
                    foreach (var player in Players)
                        if (player?.Connection != null)
                        {
                            var packet = new Packet((byte) Packets.Position);
                            foreach (var other in Players.Where(other => !other.Matches(null, player)))
                                packet.Add(other.Slot, other.Position);
                            packet.SendTo(player.Connection, NetDeliveryMethod.UnreliableSequenced, 1);
                        }
                // I need the Zooming to test multiplayer tile syncing
                if (Mouse.ScrolledUp()) { Camera.Zoom = MathHelper.Min(4, (float)Math.Round((Camera.Zoom + ZoomRate), 2)); Lighting = new RenderTarget2D(Globe.GraphicsDevice, (int)Math.Ceiling((Screen.BackBufferWidth / Camera.Zoom) / TileSize + 1), (int)Math.Ceiling((Screen.BackBufferHeight / Camera.Zoom) / TileSize + 1)); UpdateResCamStuff(); UpdateCamTilesPos(); }
                if (Mouse.ScrolledDown()) { Camera.Zoom = MathHelper.Max(.25f, (float)Math.Round((Camera.Zoom - ZoomRate), 2)); Lighting = new RenderTarget2D(Globe.GraphicsDevice, (int)Math.Ceiling((Screen.BackBufferWidth / Camera.Zoom) / TileSize + 1), (int)Math.Ceiling((Screen.BackBufferHeight / Camera.Zoom) / TileSize + 1)); UpdateResCamStuff(); UpdateCamTilesPos(); }
                Network.Update();
            }
            #endregion
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
            #region Menu/Connecting
            if (Frame == Frames.Menu)
            {
                GraphicsDevice.Clear(Color.WhiteSmoke);
                Screen.Setup(SpriteSortMode.Deferred, SamplerState.PointClamp);
                Screen.DrawString("Developed by Dcrew", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight - Screen.BackBufferHeight/8f), Color.Gray*.5f, Textures.Origin.Center, Scale*.5f);
                switch (MenuState)
                {
                    case MenuStates.UsernameEntry:
                        Screen.DrawString("Enter your name!", Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 35*Scale.Y), Color.Gray*.75f, new Textures.Origin(.5f, 1, true), Scale*.75f);
                        Screen.DrawString(Settings.Get("Name") + ((BlinkTimer <= .3f) && IsActive ? "|" : string.Empty), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 30*Scale.Y), Color.Black*.75f, new Textures.Origin(.5f, 0, true), Scale*.75f);
                        Screen.DrawString("Press 'enter' to proceed!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 35*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                        break;
                    case MenuStates.HostConnect:
                        var font = Font.Load("calibri 30");
                        var scale = Scale*.5f;
                        var size = font.MeasureString("Welcome, ")*scale;
                        Screen.DrawString("Welcome, ", font, new Vector2(Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Settings.Get("Name") + "!").X*scale.X/2f, Screen.BackBufferHeight/2f - size.Y*6), Color.Gray*.75f, null, 0, new Textures.Origin(0, .5f, true), scale);
                        Screen.DrawString(Settings.Get("Name"), font, new Vector2(Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Settings.Get("Name") + "!").X*scale.X/2f + font.MeasureString("Welcome, ").X*scale.X, Screen.BackBufferHeight/2f - size.Y*6), Color.Green*.75f, new Textures.Origin(0, .5f, true), scale);
                        Screen.DrawString("!", font, new Vector2(Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Settings.Get("Name") + "!").X*scale.X/2f + font.MeasureString("Welcome, " + Settings.Get("Name")).X*scale.X, Screen.BackBufferHeight/2f - size.Y*6), Color.Gray*.75f, new Textures.Origin(0, .5f, true), scale);
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
                        Screen.DrawString(Settings.Get("IP") + ((BlinkTimer <= .3f) && IsActive ? "|" : string.Empty), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 30*Scale.Y), Color.Black*.75f, new Textures.Origin(.5f, 0, true), Scale*.75f);
                        Screen.DrawString("Press 'enter' to proceed!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 35*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                        Screen.DrawString("Press 'tab' to go back!", Font.Load("calibri 30"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 50*Scale.Y), Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else if (Frame == Frames.Connecting)
            {
                Screen.Setup();
                Screen.DrawString("Connecting to " + Settings.Get("IP") + new string('.', 4 - (int) Math.Ceiling(BlinkTimer*4)), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f), Color.White, Textures.Origin.Center, Scale*.5f);
                Screen.Cease();
            }
            #endregion
            #region LoadGame/Game
            else if (Frame == Frames.LoadGame)
            {
                Screen.Setup();
                Screen.DrawString("Loading" + new string('.', 4 - (int) Math.Ceiling(BlinkTimer*4)), Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f), Color.White, Textures.Origin.Center, Scale*.5f);
                Screen.Cease();
            }
            else if (Frame == Frames.Game)
            {
                DrawLighting();
                GraphicsDevice.Clear(Color.CornflowerBlue);
                Screen.Setup(SamplerState.PointClamp, Camera.View(Camera.Samplers.Point));
                for (var x = CamTilesMinX; x < CamTilesMaxX; x++)
                    for (var y = CamTilesMinY; y < CamTilesMaxY; y++)
                        if (InBounds(x, y) && (Tiles[x, y].Light > 0)/*Draw tile only if not in complete darkness*/)
                        {
                            var rect = new Rectangle(x*TileSize, y*TileSize, TileSize, TileSize);
                            if ((Tiles[x, y].BackID != 0) && Tiles[x, y].DrawBack) Screen.Draw(TilesTexture, rect, Tile.Source(Tiles[x, y].BackID), Color.DarkGray);
                            if (Tiles[x, y].ForeID != 0) Screen.Draw(TilesTexture, rect, Tile.Source(Tiles[x, y].ForeID));
                            //Screen.DrawString(Tiles[x, y].Light.ToString(), Font.Load("Consolas"), new Vector2((rect.X + (TileSize / 2)), (rect.Y + (TileSize / 2))), Color.White, Textures.Origin.Center, new Vector2(.01f * Camera.Zoom));
                            //Screen.Draw(LightTile, rect, new Color(255, 255, 255, (255 - Tiles[x, y].Light)));
                        }
                foreach (var player in Players.Where(player => player != null)) player.Draw();
                Screen.Draw(TileSelectionTexture, new Rectangle((MouseTileX * TileSize), (MouseTileY * TileSize), TileSize, TileSize), (Color.White * CursorOpacity));
                Screen.Cease();
                Screen.Setup(SpriteSortMode.Deferred, Multiply, Camera.View(Camera.Samplers.Point));
                Screen.Draw(Lighting, new Rectangle((CamTilesMinX * TileSize), (CamTilesMinY * TileSize), (Lighting.Width * TileSize), (Lighting.Height * TileSize)));
                Screen.Cease();
                Screen.Setup();
                Screen.DrawString(("Zoom: " + Camera.Zoom), Font.Load("Consolas"), new Vector2(2), Color.White, Color.Black, new Vector2(.35f));
                Screen.Cease();
            }
            #endregion
            Profiler.Stop("Frame Draw");
            if (Profiler.Enabled) Profiler.Draw(430);
            // Just in case.
            if (Screen.IsSetup) Screen.Cease();
            base.Draw(time);
        }

        protected override void OnExiting(object sender, EventArgs args) { Multiplayer.QuitLobby(); base.OnExiting(sender, args); }

        public static bool InBounds(int x, int y) { return InBounds(ref Tiles, x, y); }
        public static bool InBounds(ref Tile[,] tiles, int x, int y) { return !((x < 0) || (y < 0) || (x >= tiles.GetLength(0)) || (y >= tiles.GetLength(1))); }
        public static ushort AboveLight(int x, int y) { y--; if (y < 0) return 0; return Tiles[x, y].Empty ? Light : Tiles[x, y].Light; }
        public static ushort BelowLight(int x, int y) { y++; if (y >= Tiles.GetLength(1)) return 0; return Tiles[x, y].Empty ? Light : Tiles[x, y].Light; }
        public static ushort LeftLight(int x, int y) { x--; if (x < 0) return 0; return Tiles[x, y].Empty ? Light : Tiles[x, y].Light; }
        public static ushort RightLight(int x, int y) { x++; if (x >= Tiles.GetLength(0)) return 0; return Tiles[x, y].Empty ? Light : Tiles[x, y].Light; }
        public static int CamTilesMinX, CamTilesMinY, CamTilesMaxX, CamTilesMaxY;
        public static void UpdateCamTilesPos()
        {
            CamTilesMinX = (int)Math.Floor((Camera.X - ScrWidthTiles) / TileSize);
            CamTilesMinY = (int)Math.Floor((Camera.Y - ScrHeightTiles) / TileSize);
            CamTilesMaxX = (int)Math.Ceiling((Camera.X + ScrWidthTiles) / TileSize);
            CamTilesMaxY = (int)Math.Ceiling((Camera.Y + ScrHeightTiles) / TileSize);
        }
        public static float ScrWidthTiles, ScrHeightTiles;
        public static void UpdateResCamStuff() { ScrWidthTiles = ((Screen.BackBufferWidth / 2f) / Camera.Zoom); ScrHeightTiles = ((Screen.BackBufferHeight / 2f) / Camera.Zoom); }
        public static void UpdateLighting()
        {
            Profiler.Start("Update Lighting");
            int xMax = (CamTilesMaxX + LightingUpdateBuffer), yMax = (CamTilesMaxY + LightingUpdateBuffer);
            for (var x = (CamTilesMinX - LightingUpdateBuffer); x <= xMax; x++)
                for (var y = (CamTilesMinY - LightingUpdateBuffer); y <= yMax; y++)
                    if (InBounds(x, y))
                    {
                        ushort aboveLight = AboveLight(x, y), belowLight = BelowLight(x, y), leftLight = LeftLight(x, y), rightLight = RightLight(x, y), max = Math.Max(aboveLight, Math.Max(belowLight, Math.Max(leftLight, rightLight)));
                        Tiles[x, y].Light = (ushort)Math.Max((Tiles[x, y].Empty ? Light : Tiles[x, y].LightGenerated), (max - (Tiles[x, y].BackOnly ? Tiles[x, y].BackLightDim : Tiles[x, y].ForeLightDim)));
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
                    if (InBounds(x, y)) Screen.Draw(LightPixel, new Rectangle(j, k, 1, 1), new Color(255, 255, 255, (255 - Math.Min((ushort)255, Tiles[x, y].Light))));
                    else Screen.Draw(LightPixel, new Rectangle(j, k, 1, 1));
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