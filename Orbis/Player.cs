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

        public const float Gravity = 720, MaxYVel = 840, MaxXVel = 1200;
        public byte Jumps;
        public float MovementSpeed { get; private set; }
        public float MovementResistance { get; private set; }
        public Vector2 Scale;
        private static Texture2D PlayerTexture => Game.PlayerTexture;

        public int LastTileX, LastTileY;
        public sbyte Direction;

        /// <summary>
        /// Creates a player with only a name.
        /// </summary>
        /// <param name="name">The username of the player.</param>
        public Player(string name) : base(CollosionOptions.CollidesWithTerrain) { Name = name; Load(); }
        /// <summary>
        /// Creates a player with a slot and a name.
        /// </summary>
        /// <param name="slot">The slot number to place the player into.</param>
        /// <param name="name">The username of the player.</param>
        public Player(byte slot, string name) : base(CollosionOptions.CollidesWithTerrain) { Slot = slot; Name = name; Load(); }
        public void Load() { Scale = new Vector2((PlayerTexture.Width - 2), (PlayerTexture.Height - 2)); Hitbox = Polygon.CreateRectangle(Scale); }

        public new void Update(GameTime time)
        {
            base.Update(time);
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
            if (IsOnGround) Jumps = 0;
            if (Tiles[TileX, TileY + 2].Solid) MovementSpeed = Tiles[TileX, TileY + 2].MovementSpeed;
            if (Globe.IsActive)
            {
                if (Keyboard.Holding(Keyboard.Keys.W) && (Jumps <= 0)) { Velocity.Y = -300; Jumps++; }
                if (Keyboard.Holding(Keyboard.Keys.A)) Velocity.X = -MovementSpeed;
                if (Keyboard.Holding(Keyboard.Keys.D)) Velocity.X = MovementSpeed;
                if (Settings.IsDebugMode)
                {
                    var spd = (Keyboard.HoldingShift() ? 25 : 10);
                    if (Keyboard.Holding(Keyboard.Keys.Up)) { LinearY -= spd; Velocity.Y = 0; }
                    if (Keyboard.Holding(Keyboard.Keys.Left)) { LinearX -= spd; Velocity.X = 0; }
                    if (Keyboard.Holding(Keyboard.Keys.Right)) { LinearX += spd; Velocity.X = 0; }
                }
            }
            if (Network.IsClient) while (Timers.Tick("posSync")) new Packet((byte)Packets.Position, LinearPosition, Velocity).Send(NetDeliveryMethod.UnreliableSequenced, 1);
        }
        public void Draw()
        {
            Screen.Draw(PlayerTexture, WorldPosition, Origin.Center, ((Direction == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None), 0);
            if (Settings.IsDebugMode) Hitbox.Draw(Color.Red*.5f, LineThickness);
        }

        public void Spawn(Point spawn) { LinearPosition = new Vector2(((spawn.X * Tile.Size) + (Tile.Size / 2f)), ((spawn.Y * Tile.Size) + (Tile.Size / 2f))); }
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