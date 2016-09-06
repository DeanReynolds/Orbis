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
    public class Game : Microsoft.Xna.Framework.Game
    {
        public enum Frames
        {
            Menu,
            Connecting,
            LoadGame,
            Game
        }

        public static Frames Frame = Frames.Menu;

        public static Player Self;
        public static Player[] Players;
        public static bool Quit = false;

        public Game()
        {
            Globe.GraphicsDeviceManager = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        public static ulong Version
        {
            get { return Globe.Version; }
            set { Globe.Version = value; }
        }

        public static float Speed
        {
            get { return Globe.Speed; }
            set { Globe.Speed = value; }
        }

        public static Vector2 Scale => new Vector2(Screen.BackBufferWidth/1920f, Screen.BackBufferHeight/1080f);
        
        #region Menu/Connecting Variables

        public static INI Settings;
        public static string Name, IP;
        public static byte MenuState;
        public static double BlinkTimer;

        #endregion

        #region Game Variables

        public static Matrix CamScale = Matrix.CreateScale(2f, 2f, 1f);

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
            
            Screen.Set(1920, 1080, false);
            var VirtualResolution = new Point(Screen.BackBufferWidth / 8, Screen.BackBufferHeight / 8);
            float XScale = Screen.BackBufferWidth / VirtualResolution.X;
            float YScale = Screen.BackBufferHeight / VirtualResolution.Y;
            //CamScale = Matrix.CreateScale(XScale, YScale, 1f);
            Screen.Expand(true);
            IsMouseVisible = true;
            Settings = INI.ReadFile("settings.ini");
            Name = Settings.Get("Name");
            IP = Settings.Get("IP");
            if (!Name.IsNullOrEmpty()) MenuState = 1;
        }

        protected override void Update(GameTime time)
        {
            Performance.UpdateFPS.Record(1/time.ElapsedGameTime.TotalSeconds);
            Mouse.Update();
            Keyboard.Update(time);
            XboxPad.Update(time);
            Timers.Update(time);
            Globe.IsActive = IsActive;
            if (XboxPad.Pressed(XboxPad.Buttons.Back) || Keyboard.Pressed(Keyboard.Keys.Escape) || Quit) Exit();
            if (Keyboard.Pressed(Keyboard.Keys.F3)) Profiler.Enabled = !Profiler.Enabled;
            Profiler.Start("Frame Update");

            #region Menu/Connecting Frame

            if (Frame == Frames.Menu)
            {
                if (MenuState == 0)
                {
                    if (IsActive)
                    {
                        BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                        if (BlinkTimer <= 0) BlinkTimer += .6;
                        Name = Name.AcceptInput(
                            String.InputFlags.NoLeadingSpaces | String.InputFlags.NoRepeatingSpaces, 20);
                        Settings.Set("Name", Name, true);
                        if (Keyboard.Pressed(Keyboard.Keys.Enter) && !Name.IsNullOrEmpty()) MenuState = 1;
                    }
                }
                else if (MenuState == 1)
                {
                    if (Mouse.Press(Mouse.Buttons.Left))
                    {
                        var mouse = new Rectangle(Mouse.X, Mouse.Y, 1, 1);
                        var str = "Host";
                        var font = Font.Load("calibri 50");
                        Vector2 scale = Scale*.75f, size = font.MeasureString(str)*scale;
                        var button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f),
                            (int) (Screen.BackBufferHeight/2f - size.Y), (int) size.X, (int) size.Y);
                        if (mouse.Intersects(button))
                        {
                            Multiplayer.CreateLobby(Name);
                            Frame = Frames.LoadGame;
                        }
                        str = "Connect";
                        scale = Scale*.75f;
                        size = font.MeasureString(str)*scale;
                        button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f),
                            (int) (Screen.BackBufferHeight/2f + size.Y*.25f), (int) size.X, (int) size.Y);
                        if (mouse.Intersects(button)) MenuState = 2;
                    }
                }
                else if (MenuState == 2)
                {
                    if (IsActive)
                    {
                        BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                        if (BlinkTimer <= 0) BlinkTimer += .6;
                        IP =
                            IP.AcceptInput(
                                String.InputFlags.NoLeadingPeriods | String.InputFlags.NoLetters |
                                String.InputFlags.NoSpecalCharacters | String.InputFlags.NoSpaces |
                                String.InputFlags.AllowPeriods |
                                String.InputFlags.NoRepeatingPeriods | String.InputFlags.AllowColons |
                                String.InputFlags.NoRepeatingColons | String.InputFlags.NoLeadingPeriods, 21);
                        Settings.Set("IP", IP, true);
                        if (Keyboard.Pressed(Keyboard.Keys.Enter) && !IP.IsNullOrEmpty())
                        {
                            Network.Connect(IP.Split(':')[0],
                                IP.Contains(":") ? Convert.ToInt32(IP.Split(':')[1]) : 6121,
                                new Network.Packet(null, Name));
                            Frame = Frames.Connecting;
                        }
                        else if (Keyboard.Pressed(Keyboard.Keys.Tab)) MenuState = 1;
                    }
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
                #region LoadGame/Game Frame

            else if (Frame == Frames.LoadGame)
            {
                BlinkTimer -= time.ElapsedGameTime.TotalSeconds;
                if (BlinkTimer <= 0) BlinkTimer += 1;
                if (Network.IsNullOrServer) Frame = Frames.Game;
                Network.Update();
            }
            else if (Frame == Frames.Game)
            {
                foreach (var t in Players.Where(t => t != null))
                    t.Update(time);
                if (Timers.Tick("posSync") && Network.IsServer)
                    foreach (var player in Players)
                        if (player?.Connection != null)
                        {
                            var packet = new Network.Packet((byte) Multiplayer.Packets.Position);
                            foreach (
                                var other in
                                    Players.Where(other => !other.Matches(null, player))
                                        .Where(other => (other != player) && (other != null)))
                                packet.Add(other.Slot, other.Position);
                            packet.SendTo(player.Connection, NetDeliveryMethod.UnreliableSequenced, 1);
                        }
                Network.Update();
            }

            #endregion

            Profiler.Stop("Frame Update");
            Textures.ApplyDispose();
            Sound.AutoTerminate();
            base.Update(time);
        }

        protected override void Draw(GameTime time)
        {
            Performance.DrawFPS.Record(1/time.ElapsedGameTime.TotalSeconds);
            GraphicsDevice.Clear(Color.Black);
            Profiler.Start("Frame Draw");
            
            if (Frame == Frames.Menu)
            {
                GraphicsDevice.Clear(Color.WhiteSmoke);
                Screen.Setup(SpriteSortMode.Deferred, SamplerState.PointClamp);
                Screen.DrawString("Developed by Dcrew", Font.Load("calibri 30"),
                    new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight - Screen.BackBufferHeight/8f),
                    Color.Gray*.5f, Textures.Origin.Center, Scale*.5f);
                var position = new Vector2(Screen.BackBufferWidth/8f, Screen.BackBufferHeight/2f - 40);
                if (MenuState == 0)
                {
                    Screen.DrawString("Enter your name!", Font.Load("calibri 50"),
                        new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 35*Scale.Y), Color.Gray*.75f,
                        new Textures.Origin(.5f, 1, true), Scale*.75f);
                    Screen.DrawString(Name + ((BlinkTimer <= .3f) && IsActive ? "|" : string.Empty),
                        Font.Load("calibri 50"),
                        new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 30*Scale.Y),
                        Color.Black*.75f, new Textures.Origin(.5f, 0, true), Scale*.75f);
                    Screen.DrawString("Press 'enter' to proceed!", Font.Load("calibri 30"),
                        new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 35*Scale.Y),
                        Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                }
                else if (MenuState == 1)
                {
                    var str = "Welcome, ";
                    var font = Font.Load("calibri 30");
                    var scale = Scale*.5f;
                    var size = font.MeasureString(str)*scale;
                    Screen.DrawString(str, font,
                        new Vector2(
                            Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Name + "!").X*scale.X/2f,
                            Screen.BackBufferHeight/2f - size.Y*6), Color.Gray*.75f, null, 0,
                        new Textures.Origin(0, .5f, true), scale);
                    str = Name;
                    Screen.DrawString(str, font,
                        new Vector2(
                            Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Name + "!").X*scale.X/2f +
                            font.MeasureString("Welcome, ").X*scale.X, Screen.BackBufferHeight/2f - size.Y*6),
                        Color.Green*.75f, new Textures.Origin(0, .5f, true), scale);
                    str = "!";
                    Screen.DrawString(str, font,
                        new Vector2(
                            Screen.BackBufferWidth/2f - font.MeasureString("Welcome, " + Name + "!").X*scale.X/2f +
                            font.MeasureString("Welcome, " + Name).X*scale.X, Screen.BackBufferHeight/2f - size.Y*6),
                        Color.Gray*.75f, new Textures.Origin(0, .5f, true), scale);
                    var mouse = new Rectangle(Mouse.X, Mouse.Y, 1, 1);
                    str = "Host";
                    font = Font.Load("calibri 50");
                    scale = Scale*.75f;
                    size = font.MeasureString(str)*scale;
                    var button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f),
                        (int) (Screen.BackBufferHeight/2f - size.Y), (int) size.X, (int) size.Y);
                    var color = Color.Silver;
                    if (mouse.Intersects(button))
                    {
                        scale += new Vector2(.35f);
                        color = Color.White;
                    }
                    Screen.DrawString(str, font, new Vector2(button.X + button.Width/2f, button.Y + button.Height/2f),
                        color, Color.Black*.5f, Textures.Origin.Center, scale);
                    str = "Connect";
                    scale = Scale*.75f;
                    size = font.MeasureString(str)*scale;
                    button = new Rectangle((int) (Screen.BackBufferWidth/2f - size.X/2f),
                        (int) (Screen.BackBufferHeight/2f + size.Y*.25f), (int) size.X, (int) size.Y);
                    color = Color.Silver;
                    if (mouse.Intersects(button))
                    {
                        scale += new Vector2(.35f);
                        color = Color.White;
                    }
                    Screen.DrawString(str, font, new Vector2(button.X + button.Width/2f, button.Y + button.Height/2f),
                        color, Color.Black*.5f, Textures.Origin.Center, scale);
                }
                else if (MenuState == 2)
                {
                    Screen.DrawString("Enter the IP:Port!", Font.Load("calibri 50"),
                        new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 35*Scale.Y), Color.Gray*.75f,
                        new Textures.Origin(.5f, 1, true), Scale*.75f);
                    Screen.DrawString(IP + ((BlinkTimer <= .3f) && IsActive ? "|" : string.Empty),
                        Font.Load("calibri 50"),
                        new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f - 30*Scale.Y),
                        Color.Black*.75f, new Textures.Origin(.5f, 0, true), Scale*.75f);
                    Screen.DrawString("Press 'enter' to proceed!", Font.Load("calibri 30"),
                        new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 35*Scale.Y),
                        Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                    Screen.DrawString("Press 'tab' to go back!", Font.Load("calibri 30"),
                        new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f + 50*Scale.Y),
                        Color.DimGray*.5f, new Textures.Origin(.5f, 1, true), Scale*.5f);
                }
            }
            else if (Frame == Frames.Connecting)
            {
                Screen.Setup();
                Screen.DrawString("Connecting to " + IP + new string('.', 4 - (int) Math.Ceiling(BlinkTimer*4)),
                    Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f),
                    Color.White, Textures.Origin.Center, Scale*.5f);
                Screen.Cease();
            }
            else if (Frame == Frames.LoadGame)
            {
                Screen.Setup();
                Screen.DrawString("Loading" + new string('.', 4 - (int) Math.Ceiling(BlinkTimer*4)),
                    Font.Load("calibri 50"), new Vector2(Screen.BackBufferWidth/2f, Screen.BackBufferHeight/2f),
                    Color.White, Textures.Origin.Center, Scale*.5f);
                Screen.Cease();
            }
            else if (Frame == Frames.Game)
            {
                Screen.Setup(SamplerState.PointClamp, CamScale);
                foreach (var player in Players.Where(player => player != null))
                    player.Draw();
                Screen.Cease();
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
    }
}