using System.Linq;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpXNA;
using SharpXNA.Collision;
using SharpXNA.Input;
using static SharpXNA.Textures;
using System;

namespace Orbis
{
    using Packet = Network.Packet;
    using Packets = Multiplayer.Packets;

    public class Player
    {
        private const int TileSize = Game.TileSize;

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
        public Vector2 Position, LastPosition;
        /// <summary>
        /// The player slot that this player occupies in the current server.
        /// </summary>
        public byte Slot;

        public const float Gravity = 10;
        public byte Jumps;
        public Vector2 Speed = new Vector2(250, 250), Velocity = Vector2.Zero;
        public Vector2 Scale;

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

        private static Player Self => Game.Self;
        private static Player[] Players => Game.Players;

        public void Load() { Scale = new Vector2((TileSize * 2), (TileSize * 3)); Hitbox = Polygon.CreateRectangle(Scale); }
        public bool Collides
        {
            get
            {
                UpdateHitbox();
                for (int x = (int)Math.Floor((Position.X / TileSize) - 1); x <= (int)Math.Ceiling((Position.X / TileSize) + 1); x++)
                    for (int y = (int)Math.Floor((Position.Y / TileSize) - 1); y <= (int)Math.Ceiling((Position.Y / TileSize) + 1); y++)
                        if (Game.InBounds(x, y) && Game.Tiles[x, y].Solid)
                        {
                            var hitbox = Polygon.CreateSquare(TileSize);
                            hitbox.Position = new Vector2(((x * TileSize) + (TileSize / 2f)), ((y * TileSize) + (TileSize / 2f)));
                            if (Hitbox.Intersects(hitbox)) return true;
                        }
                return false;
            }
        }

        public void Update(GameTime time)
        {
            if (this == Self)
            {
                Velocity.Y += (float)(Gravity * time.ElapsedGameTime.TotalSeconds);
                if (Globe.IsActive)
                {
                    if (Keyboard.Holding(Keyboard.Keys.W) && (Jumps <= 0)) { Velocity.Y = -5; Jumps++; Move(new Vector2(0, -10)); }
                    if (Keyboard.Holding(Keyboard.Keys.A)) Move(new Vector2(-(float)(Speed.X * time.ElapsedGameTime.TotalSeconds), 0));
                    if (Keyboard.Holding(Keyboard.Keys.D)) Move(new Vector2((float)(Speed.X * time.ElapsedGameTime.TotalSeconds), 0));
                }
                Move(Velocity);
                if (Timers.Tick("posSync") && Network.IsClient) new Packet((byte)Packets.Position, Position).Send(NetDeliveryMethod.UnreliableSequenced, 1);
            }
            if (LastPosition != Position)
            {
                if (Position.X > LastPosition.X) Direction = 1;
                else Direction = -1;
                LastPosition = Position;
            }
        }
        public void Draw()
        {
            float tileSizeHalved = (TileSize / 2f); int tileSizeDoubled = (TileSize * 2);
            Screen.Draw(Textures.Load("test_char.png"), Position, Origin.Center, new Vector2(3), ((Direction == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None), 0);
            Hitbox.Draw(Color.Red*.75f, .5f);
        }

        public void Move(Vector2 offset)
        {
            const float specific = 1;
            if ((offset.X != 0) && !Collides)
            {
                Position.X += offset.X;
                if (Collides)
                {
                    Position.X -= offset.X;
                    Velocity.X = 0;
                    while (!Collides) Position.X += offset.X < 0 ? -specific : specific;
                    while (Collides) Position.X -= offset.X < 0 ? -specific : specific;
                }
            }
            if ((offset.Y != 0) && !Collides)
            {
                Position.Y += offset.Y;
                if (Collides)
                {
                    Position.Y -= offset.Y;
                    if (offset.Y > 0) Jumps = 0;
                    Velocity.Y = 0;
                    while (!Collides) Position.Y += offset.Y < 0 ? -specific : specific;
                    while (Collides) Position.Y -= offset.Y < 0 ? -specific : specific;
                }
            }
        }

        public void UpdateHitbox() { Hitbox.Position = Position; }
        public float VolumeFromDistance(Vector2 position, float fade, float max) { return Position.VolumeFromDistance(position, fade, max); }
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