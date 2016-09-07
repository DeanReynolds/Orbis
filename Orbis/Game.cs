using System;
using System.Linq;
using System.Windows.Forms;
using Lidgren.Network;
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

        public Game()
        {
            Globe.GraphicsDeviceManager = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        public static ulong Version { get { return Globe.Version; } set { Globe.Version = value; } }
        public static float Speed { get { return Globe.Speed; } set { Globe.Speed = value; } }

        public static Vector2 Scale => new Vector2(Screen.BackBufferWidth/1920f, Screen.BackBufferHeight/1080f);
        
        #region Menu/Connecting Variables
        public enum MenuStates { UsernameEntry, HostConnect, IPEntry }
        public static MenuStates MenuState = MenuStates.UsernameEntry;
        public static double BlinkTimer;
        #endregion
        #region Game Variables
        public const int TileSize = 8, ChunkWidth = 160, ChunkHeight = 120, LightingUpdateBuffer = 2;
        public static Tile[,] Tiles;
        public static Point Spawn;
        public static ushort Light = 285;
        public static BlendState Multiply = new BlendState { AlphaSourceBlend = Blend.DestinationAlpha, AlphaDestinationBlend = Blend.Zero, AlphaBlendFunction = BlendFunction.Add, ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero, ColorBlendFunction = BlendFunction.Add };
        public static RenderTarget2D Lighting;
        public static Thread LightingThread;
        public static Camera Camera;
        private const float CameraZoom = 2f;
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
            Settings.Parse();
            // If the user has already given their Username, send them straight to the Host/Connect screen.
            if (!Settings.Get("Name").IsNullOrEmpty()) MenuState = MenuStates.HostConnect;
            //Multiplayer.CreateLobby(Name);
            //Frame = Frames.LoadGame; // Start on the Load frame and skip the menu
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
            if (!string.IsNullOrEmpty(Settings.Get("Name"))) MenuState = MenuStates.HostConnect;
            if (Frame == Frames.Menu)
            {
                switch (MenuState)
                {
                    case MenuStates.UsernameEntry:
                        if (IsActive)
                        {
                            BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                            if (BlinkTimer <= 0) BlinkTimer += .6;
                            var name = "";
                            name = name.AcceptInput(
                                String.InputFlags.NoLeadingSpaces | String.InputFlags.NoRepeatingSpaces, 20);
                            if (Keyboard.Pressed(Keyboard.Keys.Enter) && !name.IsNullOrEmpty())
                            {
                                if (!string.IsNullOrEmpty(name)) Settings.Set("Name", name);
                                MenuState = MenuStates.HostConnect;
                            }
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
                            var ip = "";
                            ip = ip.AcceptInput(
                                    String.InputFlags.NoLeadingPeriods | String.InputFlags.NoLetters |
                                    String.InputFlags.NoSpecalCharacters | String.InputFlags.NoSpaces |
                                    String.InputFlags.AllowPeriods |
                                    String.InputFlags.NoRepeatingPeriods | String.InputFlags.AllowColons |
                                    String.InputFlags.NoRepeatingColons | String.InputFlags.NoLeadingPeriods, 21);
                            if (Keyboard.Pressed(Keyboard.Keys.Enter) && !ip.IsNullOrEmpty())
                            {
                                Settings.Set("IP", ip);
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
                    Camera = new Camera {Zoom = CameraZoom};
                    //Tiles = Generate(8400, 2400, out Spawn);
                    Tiles = World.Generation.Generate(2100, 600, out Spawn);
                    Lighting = new RenderTarget2D(Globe.GraphicsDevice, (int) Math.Ceiling((Screen.BackBufferWidth/Camera.Zoom)/TileSize + 1), (int) Math.Ceiling((Screen.BackBufferHeight/Camera.Zoom)/TileSize + 1));
                    LightingThread = new Thread(() =>
                    {
                        while (true)
                        {
                            UpdateLighting();
                            Thread.Sleep(100);
                        }
                    }) {Name = "Lighting", IsBackground = true};
                    LightingThread.Start();
                    Self.Position = new Vector2((Spawn.X*TileSize), (Spawn.Y*TileSize));
                    Frame = Frames.Game;
                }
                Network.Update();
            }
            else if (Frame == Frames.Game)
            {
                foreach (var t in Players.Where(t => t != null)) t.Update(time);
                Camera.Position = Self.Position;
                if (Timers.Tick("posSync") && Network.IsServer)
                    foreach (var player in Players)
                        if (player?.Connection != null)
                        {
                            var packet = new Packet((byte) Packets.Position);
                            foreach (var other in Players.Where(other => !other.Matches(null, player)))
                                packet.Add(other.Slot, other.Position);
                            packet.SendTo(player.Connection, NetDeliveryMethod.UnreliableSequenced, 1);
                        }
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
                // This is never used. Why?
                var position = new Vector2(Screen.BackBufferWidth/8f, Screen.BackBufferHeight/2f - 40);
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
                int xMax = (int) Math.Ceiling((Camera.X + (Screen.BackBufferWidth/2f)/Camera.Zoom)/TileSize), yMax = (int) Math.Ceiling((Camera.Y + ((Screen.BackBufferHeight/2f)/Camera.Zoom))/TileSize);
                for (var x = (int) Math.Floor((Camera.X - (Screen.BackBufferWidth/2f)/Camera.Zoom)/TileSize); x < xMax; x++)
                    for (var y = (int) Math.Floor((Camera.Y - (Screen.BackBufferHeight/2f)/Camera.Zoom)/TileSize); y < yMax; y++)
                        if (InBounds(x, y))
                        {
                            var rect = new Rectangle(x*TileSize, y*TileSize, TileSize, TileSize);
                            if (Tiles[x, y].ForeID != 0) Screen.Draw("Tiles.png", rect, Tile.Source(Tiles[x, y].ForeID));
                            if ((Tiles[x, y].BackID != 0) && Tiles[x, y].DrawBack) Screen.Draw("Tiles.png", rect, Tile.Source(Tiles[x, y].BackID), Color.DimGray);
                            //Screen.DrawString(Tiles[x, y].Light.ToString(), Font.Load("Consolas"), new Vector2((rect.X + 2), (rect.Y + 2)), Color.White, new Vector2(.1f));
                            Screen.Draw(Textures.Pixel(Color.Black, true), rect, new Color(255, 255, 255, (255 - Tiles[x, y].Light)));
                        }
                foreach (var player in Players.Where(player => player != null)) player.Draw();
                Screen.Cease();
                Screen.Setup(SpriteSortMode.Deferred, Multiply, Camera.View(Camera.Samplers.Point));
                Screen.Draw(Lighting, new Rectangle(
                    (int) Math.Floor((Camera.X - (Screen.BackBufferWidth/2f)/Camera.Zoom)/TileSize)*TileSize,
                    (int) Math.Floor((Camera.Y - (Screen.BackBufferHeight/2f)/Camera.Zoom)/TileSize)*TileSize,
                    Lighting.Width*TileSize,
                    Lighting.Height*TileSize));
                Screen.Cease();
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
        
        public static bool InBounds(int x, int y)
        {
            return !((x < 0) || (y < 0) || (x >= Tiles.GetLength(0)) || (y >= Tiles.GetLength(1)));
        }

        public static bool OffScreen(int x, int y)
        {
            int xMin = (int) Math.Floor((Camera.X - ((Screen.BackBufferWidth/2f)/Camera.Zoom))/TileSize - 1), xMax = (int) Math.Ceiling((Camera.X + ((Screen.BackBufferWidth/2f)/Camera.Zoom))/TileSize), yMin = (int) Math.Floor((Camera.Y - ((Screen.BackBufferHeight/2f)/Camera.Zoom))/TileSize - 1), yMax = (int) Math.Ceiling((Camera.Y + ((Screen.BackBufferHeight/2f)/Camera.Zoom))/TileSize);
            return ((x < xMin) || (y < yMin) || (x > xMax) || (y > yMax));
        }

        public static ushort AboveLight(int x, int y)
        {
            y--;
            if (OffScreen(x, y) || !InBounds(x, y)) return 0;
            return Tiles[x, y].Empty ? Light : Tiles[x, y].Light;
        }

        public static ushort BelowLight(int x, int y)
        {
            y++;
            if (OffScreen(x, y) || !InBounds(x, y)) return 0;
            return Tiles[x, y].Empty ? Light : Tiles[x, y].Light;
        }

        public static ushort LeftLight(int x, int y)
        {
            x--;
            if (OffScreen(x, y) || !InBounds(x, y)) return 0;
            return Tiles[x, y].Empty ? Light : Tiles[x, y].Light;
        }

        public static ushort RightLight(int x, int y)
        {
            x++;
            if (OffScreen(x, y) || !InBounds(x, y)) return 0;
            return Tiles[x, y].Empty ? Light : Tiles[x, y].Light;
        }

        public static void UpdateLighting()
        {
            int xMax = (int) Math.Ceiling((Camera.X + ((Screen.BackBufferWidth/2f)/Camera.Zoom))/TileSize) + LightingUpdateBuffer, yMax = ((int) Math.Ceiling((Camera.Y + ((Screen.BackBufferHeight/2f)/Camera.Zoom))/TileSize) + LightingUpdateBuffer);
            for (var x = (int) Math.Floor((Camera.X - ((Screen.BackBufferWidth/2f)/Camera.Zoom))/TileSize) - LightingUpdateBuffer; x <= xMax; x++)
                for (var y = (int) Math.Floor((Camera.Y - ((Screen.BackBufferHeight/2f)/Camera.Zoom))/TileSize) - LightingUpdateBuffer; y <= yMax; y++)
                    if (InBounds(x, y))
                    {
                        ushort aboveLight = AboveLight(x, y), belowLight = BelowLight(x, y), leftLight = LeftLight(x, y), rightLight = RightLight(x, y), max = Math.Max(aboveLight, Math.Max(belowLight, Math.Max(leftLight, rightLight)));
                        Tiles[x, y].Light = (ushort) Math.Max(0, Math.Min(255, Math.Max((Tiles[x, y].Empty ? Light : 0), (max - (Tiles[x, y].BackOnly ? 6 : 25)))));
                    }
        }

        public static void DrawLighting()
        {
            int j = 0, k = 0;
            Globe.GraphicsDevice.SetRenderTarget(Lighting);
            Globe.GraphicsDevice.Clear(Color.White);
            Screen.Setup();
            int xMax = (int) Math.Ceiling((Camera.X + ((Screen.BackBufferWidth/2f)/Camera.Zoom))/TileSize), yMax = (int) Math.Ceiling((Camera.Y + ((Screen.BackBufferHeight/2f)/Camera.Zoom))/TileSize);
            for (var x = (int) Math.Floor((Camera.X - ((Screen.BackBufferWidth/2f)/Camera.Zoom))/TileSize); x <= xMax; x++)
            {
                for (var y = (int) Math.Floor((Camera.Y - ((Screen.BackBufferHeight/2f)/Camera.Zoom))/TileSize); y <= yMax; y++)
                {
                    if (InBounds(x, y)) Screen.Draw(Textures.Pixel(Color.Black, true), new Rectangle(j, k, 1, 1), new Color(255, 255, 255, (255 - Tiles[x, y].Light)));
                    else Screen.Draw(Textures.Pixel(Color.Black, true), new Rectangle(j, k, 1, 1));
                    k++;
                }
                j++;
                k = 0;
            }
            Screen.Cease();
            Globe.GraphicsDevice.SetRenderTarget(null);
        }
    }
}