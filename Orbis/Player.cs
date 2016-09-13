using System.Linq;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using SharpXNA;
using SharpXNA.Collision;
using SharpXNA.Input;
using static SharpXNA.Textures;
using Microsoft.Xna.Framework.Graphics;

namespace Orbis
{
    using Packet = Network.Packet;
    using Packets = Multiplayer.Packets;

    public class Player : Entity
    {
        private static Player Self => Game.Self;
        private static Player[] Players => Game.Players;
        private static World World => Game.World;
        private static float LineThickness { get { return Game.LineThickness; } set { Game.LineThickness = value; } }
        
        /// <summary>
        /// The NetConnection this player is connecting from.
        /// </summary>
        public NetConnection Connection;
        /// <summary>
        /// The friendly name of the player.
        /// </summary>
        public string Name;
        private Vector2 _lastWorldPosition;
        /// <summary>
        /// The player slot that this player occupies in the current server.
        /// </summary>
        public byte Slot;
        
        public byte Jumps;
        public float MovementSpeed { get; private set; }
        private Inventory _inventory;
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

        public void Load()
        {
            IsAffectedByGravity = true;
            Hitbox = Polygon.CreateRectangle(new Vector2((PlayerTexture.Width - 2), (PlayerTexture.Height - 2)));
            _inventory = new Inventory(Inventory.PlayerInvSize);
        }

        public new void Update(GameTime time)
        {
            base.Update(time);
            if (_lastWorldPosition != WorldPosition)
            {
                if (WorldPosition.X > _lastWorldPosition.X) Direction = 1;
                else if (WorldPosition.X < _lastWorldPosition.X) Direction = -1;
                _lastWorldPosition = WorldPosition;
            }
        }
        public void SelfUpdate(GameTime time)
        {
            if (IsOnGround) Jumps = 0;
            if (World.Tiles[TileX, TileY + 2].Solid || (MovementSpeed == 0)) MovementSpeed = World.Tiles[TileX, TileY + 2].MovementSpeed;
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
        public void DrawInventory(Vector2 position, int itemsPerRow, float scale = 1)
        {
            for (var i = 0; i < Inventory.PlayerInvSize; i++)
            {
                int x = (i%itemsPerRow), y = (i/itemsPerRow);
                var invSlot = Textures.Load("Inventory Slot.png");
                var pos = new Vector2((position.X + (x*(invSlot.Width * scale)) + x), (position.Y + (y*(invSlot.Height * scale)) + y));
                Screen.Draw(invSlot, pos);
                if (GetItem(i) != null)
                {
                    if (GetItem(i).Tile.HasValue)
                    {
                        Screen.Draw("Tiles.png", new Vector2((pos.X + ((invSlot.Width * scale)/2f)), (pos.Y + ((invSlot.Height * scale)/2f))), Tile.Source((int) GetItem(i).Tile.Value,
                            (GetItem(i).Style.HasValue ? GetItem(i).Style.Value : (byte) 0)), new Origin(4), new Vector2(4 * scale));
                    }
                    if (GetItem(i).Stack > 1) Screen.DrawString(GetItem(i).Stack.ToString(), Font.Load("calibri 30"), new Vector2((pos.X + ((invSlot.Width-4)*scale)), (pos.Y + ((invSlot.Height-4)*scale))), (Color.White * .75f), (Color.Black * .75f),
                        new Origin(1, true), new Vector2(.5f * scale));
                }
            }
        }

        public int AddItem(Item item)
        {
            if (Network.IsServer) new Packet((byte)Packets.PlayerAddItem, Slot, item.Key, item.Stack).Send(Connection);
            else if (Network.IsClient && (this == Self)) new Packet((byte)Packets.PlayerAddItem, item.Key, item.Stack).Send();
            return _inventory.Add(item);
        }
        public void SetItem(int slot, Item item, bool sync = true)
        {
            if (sync)
                if (Network.IsServer) new Packet((byte) Packets.PlayerSetItem, Slot, slot, item.Key, item.Stack).Send(Connection);
                else if (Network.IsClient && (this == Self)) new Packet((byte) Packets.PlayerSetItem, slot, item.Key, item.Stack).Send();
            _inventory.Set(slot, item);
        }
        public void RemoveItem(int slot)
        {
            if (Network.IsServer) new Packet((byte)Packets.PlayerRemoveItem, Slot, slot).Send(Connection);
            else if (Network.IsClient && (this == Self)) new Packet((byte)Packets.PlayerRemoveItem, slot).Send();
            _inventory.Remove(slot);
        }
        public void RemoveItem(Item item)
        {
            if (Network.IsServer) new Packet((byte)Packets.PlayerRemoveItem, Slot, item.Key, item.Stack).Send(Connection);
            else if (Network.IsClient && (this == Self)) new Packet((byte)Packets.PlayerRemoveItem, item.Key, item.Stack).Send();
            _inventory.Remove(item);
        }
        public bool HasItem(Item item) => _inventory.Has(item);
        public Item GetItem(int slot) => _inventory.Get(slot);
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