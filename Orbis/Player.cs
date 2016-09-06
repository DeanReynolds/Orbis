using System.Linq;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpXNA;
using SharpXNA.Collision;
using SharpXNA.Input;
using static SharpXNA.Textures;

namespace Orbis
{
    public class Player
    {
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
        public Vector2 Position;
        /// <summary>
        /// The player slot that this player occupies in the current server.
        /// </summary>
        public byte Slot;
        public Vector2 Speed = new Vector2(250, 250);
        private static readonly Texture2D _texture = Globe.ContentManager.Load<Texture2D>("test_char");

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

        public bool Collides
        {
            get
            {
                UpdateHitbox();
                /*for (int x = (int)(Position.X / Tile.Width - 1); x <= (Position.X / Tile.Width + 1); x++)
                    for (int y = (int)(Position.Y / Tile.Height - 1); y <= (Position.Y / Tile.Height + 1); y++)
                        if (World.InBounds(x, y) && (World.Tiles[x, y].Fore > 0) && Tiles.Fore[World.Tiles[x, y].Fore].Solid)
                        {
                            Polygon Hitbox = Polygon.CreateRectangleWithCross(new Vector2(Tile.Width, Tile.Height), Vector2.Zero);
                            Hitbox.Position = new Vector2(((x * Tile.Width) + (Tile.Width / 2f)), ((y * Tile.Height) + (Tile.Height / 2f)));
                            if (this.Hitbox.Intersects(Hitbox)) return true;
                        }*/
                return false;
            }
        }

        public void Load()
        {
            // The player is 16x22 pixels.
            Hitbox = Polygon.CreateEllipse(new Vector2(16, 22), 16);
        }

        public void Update(GameTime time)
        {
            if (this == Self)
            {
                if (Globe.IsActive)
                {
                    if (Keyboard.Holding(Keyboard.Keys.W))
                    {
                        Move(new Vector2(0, -(float) (Speed.Y*time.ElapsedGameTime.TotalSeconds)));
                    }
                    if (Keyboard.Holding(Keyboard.Keys.S))
                    {
                        Move(new Vector2(0, (float) (Speed.Y*time.ElapsedGameTime.TotalSeconds)));
                    }
                    if (Keyboard.Holding(Keyboard.Keys.A))
                    {
                        Move(new Vector2(-(float) (Speed.X*time.ElapsedGameTime.TotalSeconds), 0));
                    }
                    if (Keyboard.Holding(Keyboard.Keys.D))
                    {
                        Move(new Vector2((float) (Speed.X*time.ElapsedGameTime.TotalSeconds), 0));
                    }
                }
                if (Timers.Tick("posSync") && Network.IsClient)
                    new Network.Packet((byte) Multiplayer.Packets.Position, Position).Send(
                        NetDeliveryMethod.UnreliableSequenced, 1);
            }
        }

        public void Draw()
        {
            Screen.Draw(_texture, Position, Origin.Center);
            Hitbox.Draw(Color.Red*.2f, .5f);
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
                    while (!Collides) Position.Y += offset.Y < 0 ? -specific : specific;
                    while (Collides) Position.Y -= offset.Y < 0 ? -specific : specific;
                }
            }
        }

        public void UpdateHitbox()
        {
            Hitbox.Position = Position;
        }

        public float VolumeFromDistance(Vector2 position, float fade, float max)
        {
            return Position.VolumeFromDistance(position, fade, max);
        }

        public static Player Get(NetConnection connection)
        {
            return Players.FirstOrDefault(t => (t != null) && (t.Connection == connection));
        }

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