using System.Linq;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using SharpXNA;
using SharpXNA.Collision;
using SharpXNA.Input;
using static SharpXNA.Textures;
using System;
using Microsoft.Xna.Framework.Graphics;
using Orbis.World;

namespace Orbis
{
    using Packet = Network.Packet;
    using Packets = Multiplayer.Packets;

    // TODO: Possibly make an abstract class Entity to base players, mobs, projectiles, and particles from?
    // It would hold common things like position info, hitbox info, and health.
    // Having health on projectiles and particles is useful because it would mean that enemy projectiles
    //  can be destroyed before they hit, and particles' health will constantly decrease as long as it
    //  is 'alive', creating a destruction timer (eg. smoke from a furnace).

    public class Player : Entity
    {
        private static Player Self => Game.Self;
        private static Player[] Players => Game.Players;
        private static Tile[,] Tiles => Game.Tiles;
        private static Camera Camera => Game.Camera;
        private static float LineThickness { get { return Game.LineThickness; } set { Game.LineThickness = value; } }
        
        /// <summary>
        /// The NetConnection this player is connecting from.
        /// </summary>
        public NetConnection Connection;
        /// <summary>
        /// The collision polygon for the player.
        /// </summary>
        public Polygon Hitbox;
        /// <summary>
        /// The friendly name of the player.
        /// </summary>
        public string Name;

        /// <summary>
        /// The location of the player in the world.
        /// </summary>
        private Vector2 LastPosition;
        /// <summary>
        /// The player slot that this player occupies in the current server.
        /// </summary>
        public byte Slot;

        public const float Gravity = 720, MaxYVel = 720, MaxXVel = 1200;
        public byte Jumps;
        public float MovementSpeed { get; private set; }
        public float MovementResistance { get; private set; }
        public Vector2 Velocity = Vector2.Zero, Scale;
        public static Texture2D PlayerTexture => Game.PlayerTexture;

        public int TileX, TileY, LastTileX, LastTileY;
        public sbyte Direction;

        /// <summary>
        /// Creates a player with only a name.
        /// </summary>
        /// <param name="Name">The username of the player.</param>
        public Player(string Name)
        {
            // Pass the name through.
            this.Name = Name;
            Load();
        }
        /// <summary>
        /// Creates a player with a slot and a name.
        /// </summary>
        /// <param name="Slot">The slot number to place the player into.</param>
        /// <param name="Name">The username of the player.</param>
        public Player(byte Slot, string Name)
        {
            // Pass the slot and name through.
            this.Slot = Slot;
            this.Name = Name;
            Load();
        }
        
        public void Load() { Scale = new Vector2(PlayerTexture.Width, PlayerTexture.Height); Hitbox = Polygon.CreateRectangle(Scale); }
        public bool Collides
        {
            get
            {
                UpdateHitbox();
                for (var x = (int)Math.Floor((LinearPosition.X / Tile.Size) - 1); x <= (int)Math.Ceiling((LinearPosition.X / Tile.Size) + 1); x++)
                    for (var y = (int)Math.Floor((LinearPosition.Y / Tile.Size) - 1); y <= (int)Math.Ceiling((LinearPosition.Y / Tile.Size) + 1); y++)
                        if (Game.InBounds(x, y) && Tiles[x, y].Solid)
                        {
                            var hitbox = Polygon.CreateSquare(Tile.Size);
                            hitbox.Position = new Vector2(((x * Tile.Size) + (Tile.Size / 2f)), ((y * Tile.Size) + (Tile.Size / 2f)));
                            if (Hitbox.Intersects(hitbox)) return true;
                        }
                return false;
            }
        }

        public void Update(GameTime time)
        {
            Move(Velocity * (float)time.ElapsedGameTime.TotalSeconds); UpdateTilePos();
            //PixelPosition = new Vector2((int) Math.Round(Position.X), (int) Math.Round(Position.Y));
            MovementResistance = Tiles[TileX, (TileY + 2)].MovementResistance;
            if (LastPosition != LinearPosition)
            {
                if (LinearPosition.X > LastPosition.X) Direction = 1;
                else if (LinearPosition.X < LastPosition.X) Direction = -1;
                LastPosition = LinearPosition;
            }
            var movementResistance = (MovementResistance * (float)time.ElapsedGameTime.TotalSeconds);
            if (Velocity.X > 0) Velocity.X = MathHelper.Clamp((Velocity.X - movementResistance), 0, MaxXVel);
            else if (Velocity.X < 0) Velocity.X = MathHelper.Clamp((Velocity.X + movementResistance), -MaxXVel, 0);
            Velocity.Y = MathHelper.Min(MaxYVel, (Velocity.Y + (Gravity * (float)time.ElapsedGameTime.TotalSeconds)));
        }
        public void SelfUpdate(GameTime time)
        {
            if (Tiles[TileX, TileY + 2].Solid) MovementSpeed = Tiles[TileX, TileY + 2].MovementSpeed;
            if (Globe.IsActive)
            {
                if (Keyboard.Holding(Keyboard.Keys.W) && (Jumps <= 0)) { Velocity.Y = -300; Jumps++; }
                if (Keyboard.Holding(Keyboard.Keys.A)) Velocity.X = -MovementSpeed;
                if (Keyboard.Holding(Keyboard.Keys.D)) Velocity.X = MovementSpeed;
                if (Settings.IsDebugMode)
                {
                    if (Keyboard.Holding(Keyboard.Keys.Left)) LinearX -= Camera.Zoom;
                    if (Keyboard.Holding(Keyboard.Keys.Right)) LinearY += Camera.Zoom;
                }
            }
            if (Network.IsClient) while (Timers.Tick("posSync")) new Packet((byte)Packets.Position, LinearPosition, Velocity).Send(NetDeliveryMethod.UnreliableSequenced, 1);
        }
        public void Draw()
        {
            Screen.Draw(PlayerTexture, WorldPosition, Origin.Center, ((Direction == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None), 0);
            if (Settings.IsDebugMode) { Hitbox.Position = WorldPosition; Hitbox.Draw(Color.Red*.5f, LineThickness); }
        }

        public void Move(Vector2 offset)
        {
            const float specific = 1;
            if ((offset.X != 0) && !Collides)
            {
                LinearX += offset.X;
                if (Collides)
                {
                    LinearX -= offset.X;
                    Velocity.X = 0;
                    while (!Collides) LinearX += offset.X < 0 ? -specific : specific;
                    while (Collides) LinearX -= offset.X < 0 ? -specific : specific;
                }
            }
            if ((offset.Y != 0) && !Collides)
            {
                LinearY += offset.Y;
                if (Collides)
                {
                    LinearY -= offset.Y;
                    if (offset.Y > 0) Jumps = 0;
                    Velocity.Y = 0;
                    while (!Collides) LinearY += offset.Y < 0 ? -specific : specific;
                    while (Collides) LinearY -= offset.Y < 0 ? -specific : specific;
                }
            }
        }

        public void Spawn(Point spawn) { LinearPosition = new Vector2(((spawn.X * Tile.Size) + (Tile.Size / 2f)), ((spawn.Y * Tile.Size) + (Tile.Size / 2f))); }
        public void UpdateHitbox() { Hitbox.Position = LinearPosition; }
        public void UpdateTilePos() { TileX = (int)(LinearPosition.X / Tile.Size); TileY = (int)(LinearPosition.Y / Tile.Size); }
        public void UpdateLastTilePos() { LastTileX = TileX; LastTileY = TileY; }
        public float VolumeFromDistance(Vector2 position, float fade, float max) { return LinearPosition.VolumeFromDistance(position, fade, max); }
        public static Player Get(NetConnection connection) { return Players.FirstOrDefault(t => (t != null) && (t.Connection == connection)); }
        public static Player Add(Player player)
        {
            for (var i = 0; i < Players.Length; i++)
                if (Players[i] == null)
                {
                    player.Slot = (byte) i;
                    Players[i] = player;
                    return player;
                }
            return null;
        }
        public static Player Set(byte slot, Player player)
        {
            if (slot < Players.Length)
            {
                player.Slot = slot;
                Players[slot] = player;
                return player;
            }
            return null;
        }
        public static bool Remove(Player player)
        {
            for (var i = 0; i < Players.Length; i++)
                if (Players[i] == player)
                {
                    Players[i] = null;
                    return true;
                }
            return false;
        }
    }
}