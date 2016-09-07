using Lidgren.Network;
using SharpXNA;
using Microsoft.Xna.Framework;

namespace Orbis
{
    using Packet = Network.Packet;
    using Frames = Game.Frames;
    using Microsoft.Xna.Framework.Graphics;
    using System;
    using System.Threading;

    public static class Multiplayer
    {
        public enum Packets { Connection, Disconnection, Initial, Position, PlayerData, TileData, RectangleOfTiles, RowOfTiles, ColumnOfTiles }

        private const int TileSize = Game.TileSize, ChunkWidth = Game.ChunkWidth, ChunkHeight = Game.ChunkHeight;

        private static Frames Frame { get { return Game.Frame; } set { Game.Frame = value; } }
        private static Player Self{ get { return Game.Self; } set { Game.Self = value; } }
        private static Player[] Players { get { return Game.Players; } set { Game.Players = value; } }
        private static Tile[,] Tiles { get { return Game.Tiles; } set { Game.Tiles = value; } }
        private static Point Spawn { get { return Game.Spawn; } set { Game.Spawn = value; } }
        private static RenderTarget2D Lighting { get { return Game.Lighting; } set { Game.Lighting = value; } }
        private static Thread LightingThread { get { return Game.LightingThread; } set { Game.LightingThread = value; } }
        private static Camera Camera { get { return Game.Camera; } set { Game.Camera = value; } }

        public static void CreateLobby(string playerName)
        {
            Players = new Player[256];
            Self = Player.Add(new Player(playerName));
            Network.StartHosting(6121, Players.Length);
            Timers.Add("posSync", 1/20d);
        }
        public static void QuitLobby()
        {
            Network.Shutdown("Game");
            Players = null;
            Timers.Remove("Positions");
            Frame = Game.Frames.Menu;
        }

        public static void OnConnectionApproval(NetIncomingMessage message)
        {
            var clientVersion = message.ReadUInt64();
            if (clientVersion == Globe.Version)
            {
                var connector = Player.Add(new Player(message.ReadString()) {Connection = message.SenderConnection});
                if (connector != null)
                {
                    var data = new Packet((byte) Packets.Initial, (byte) Players.Length, connector.Slot);
                    message.SenderConnection.Approve(data.Construct());
                    new Packet((byte)Packets.Connection, connector.Slot, connector.Name).Send(
                        message.SenderConnection);
                }
                else message.SenderConnection.Deny("full");
            }
            else message.SenderConnection.Deny("different version");
        }
        public static void OnStatusChanged(NetIncomingMessage message)
        {
            var state = Network.IsClient
                ? (NetConnectionStatus) message.ReadByte()
                : Network.IsServer ? message.SenderConnection.Status : NetConnectionStatus.None;
            if (state == NetConnectionStatus.Connected)
            {
                if (Network.IsClient)
                    ProcessPacket((Packets) message.SenderConnection.RemoteHailMessage.ReadByte(),
                        message.SenderConnection.RemoteHailMessage);
            }
            else if (state == NetConnectionStatus.Disconnected)
            {
                if (Network.IsClient) QuitLobby();
                else ProcessPacket(Packets.Disconnection, message);
            }
        }
        public static void OnData(NetIncomingMessage message) { ProcessPacket((Packets) message.ReadByte(), message); }
        public static void ProcessPacket(Packets packet, NetIncomingMessage message)
        {
            #region Connection/Disconnection
            if (packet == Packets.Connection) Player.Set(message.ReadByte(), new Player(message.ReadString()));
            else if (packet == Packets.Disconnection)
            {
                var disconnector = Network.IsServer ? Player.Get(message.SenderConnection) : Network.IsClient ? Players[message.ReadByte()] : null;
                if (disconnector != null) Player.Remove(disconnector);
                if (Network.IsServer) new Packet((byte)packet, disconnector?.Slot).Send(message.SenderConnection);
            }
            #endregion
            #region Initial Data
            else if (packet == Packets.Initial)
            {
                Players = new Player[message.ReadByte()];
                Self = Player.Set(message.ReadByte(), new Player(Game.Name));
                Timers.Add("posSync", 1 / 20d);
                Camera = new Camera();
                Lighting = new RenderTarget2D(Globe.GraphicsDevice, (int)Math.Ceiling(Screen.BackBufferWidth / (float)TileSize + 1), (int)Math.Ceiling(Screen.BackBufferHeight / (float)TileSize + 1));
                LightingThread = new Thread(() => { while (true) { Game.UpdateLighting(); Thread.Sleep(100); } }) { Name = "Lighting", IsBackground = true };
                Frame = Frames.LoadGame;
                new Packet((byte)Packets.PlayerData).Send();
                new Packet((byte)Packets.TileData).Send();
            }
            else if (packet == Packets.PlayerData)
            {
                if (Network.IsServer)
                {
                    Player sender = Player.Get(message.SenderConnection);
                    var data = new Packet((byte)packet);
                    foreach (var t in Players)
                    {
                        if (!t.Matches(null, sender)) data.Add(true, t.Name);
                        else data.Add(false);
                    }
                    data.SendTo(message.SenderConnection);
                }
                else for (var i = 0; i < Players.Length; i++) if (message.ReadBoolean()) Players[i] = Player.Set((byte)i, new Player(message.ReadString()));
            }
            else if (packet == Packets.TileData)
            {
                if (Network.IsServer)
                {
                    var data = new Packet((byte)packet, (ushort)Tiles.GetLength(0), (ushort)Tiles.GetLength(1));
                    WriteRectangleOfTiles(ref Game.Tiles, ref data, (Spawn.X - (ChunkWidth / 2)), (Spawn.Y - (ChunkHeight / 2)), ChunkWidth, ChunkHeight);
                    data.Add((ushort)Spawn.X, (ushort)Spawn.Y);
                    Player sender = Player.Get(message.SenderConnection);
                    sender.Position = new Vector2((Spawn.X * TileSize), (Spawn.Y * TileSize));
                    sender.TileX = sender.LastTileX = Spawn.X;
                    sender.TileY = sender.LastTileY = Spawn.Y;
                    data.SendTo(message.SenderConnection);
                }
                else
                {
                    Game.Tiles = new Tile[message.ReadUInt16(), message.ReadUInt16()];
                    ReadRectangleOfTiles(ref message, ref Game.Tiles);
                    Spawn = new Point(message.ReadUInt16(), message.ReadUInt16());
                    Self.Position = new Vector2((Spawn.X * TileSize), (Spawn.Y * TileSize));
                    Camera.Position = Self.Position;
                    LightingThread.Start();
                    Frame = Frames.Game;
                }
            }
            #endregion
            #region Row/Column Of Tiles
            else if (packet == Packets.ColumnOfTiles) ReadColumnOfTiles(ref message, ref Game.Tiles);
            else if (packet == Packets.RowOfTiles) ReadRowOfTiles(ref message, ref Game.Tiles);
            #endregion
            else if (packet == Packets.Position)
            {
                if (Network.IsServer)
                {
                    var sender = Player.Get(message.SenderConnection);
                    if (sender != null)
                    {
                        sender.Position = message.ReadVector2();
                        sender.TileX = (int)(sender.Position.X / TileSize);
                        sender.TileY = (int)(sender.Position.Y / TileSize);
                        while (sender.TileX < sender.LastTileX) { var data = new Packet((byte)Packets.ColumnOfTiles); WriteColumnOfTiles(ref Game.Tiles, ref data, (sender.TileX - (ChunkWidth / 2)), (sender.TileY - (ChunkHeight / 2)), ChunkHeight); sender.LastTileX--; data.SendTo(sender.Connection); }
                        while (sender.TileX > sender.LastTileX) { var data = new Packet((byte)Packets.ColumnOfTiles); WriteColumnOfTiles(ref Game.Tiles, ref data, (sender.TileX + ((ChunkWidth / 2) - 1)), (sender.TileY - (ChunkHeight / 2)), ChunkHeight); sender.LastTileX++; data.SendTo(sender.Connection); }
                        while (sender.TileY < sender.LastTileY) { var data = new Packet((byte)Packets.RowOfTiles); WriteRowOfTiles(ref Game.Tiles, ref data, (sender.TileX - (ChunkWidth / 2)), (sender.TileY - (ChunkHeight / 2)), ChunkWidth); sender.LastTileY--; data.SendTo(sender.Connection); }
                        while (sender.TileY > sender.LastTileY) { var data = new Packet((byte)Packets.RowOfTiles); WriteRowOfTiles(ref Game.Tiles, ref data, (sender.TileX - (ChunkWidth / 2)), (sender.TileY + ((ChunkHeight / 2) - 1)), ChunkWidth); sender.LastTileY++; data.SendTo(sender.Connection); }
                    }
                }
                else
                {
                    var count = (message.LengthBytes - 1) / 9;
                    for (var i = 0; i < count; i++)
                    {
                        var sender = Players[message.ReadByte()];
                        if (sender != null)
                        {
                            sender.Position = message.ReadVector2();
                        }
                    }
                }
            }
        }

        public static void WriteRectangleOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int width, int height)
        {
            data.Add((ushort)x, (ushort)y, (ushort)width, (ushort)height);
            int endX = (x + width), endY = (y + height);
            for (int j = x; j < endX; j++) for (int k = y; k < endY; k++) data.Add(tiles[j, k].ForeID, tiles[j, k].BackID);
        }
        public static void ReadRectangleOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), height = data.ReadUInt16(), endX = (x + width), endY = (y + height);
            for (int j = x; j < endX; j++) for (int k = y; k < endY; k++) { tiles[j, k].ForeID = data.ReadByte(); tiles[j, k].BackID = data.ReadByte(); }
        }
        public static void WriteRowOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int width)
        {
            data.Add((ushort)x, (ushort)y, (ushort)width);
            int endX = (x + width);
            for (int j = x; j < endX; j++) data.Add(tiles[j, y].ForeID, tiles[j, y].BackID);
        }
        public static void ReadRowOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), endX = (x + width);
            for (int j = x; j < endX; j++) { tiles[j, y].ForeID = data.ReadByte(); tiles[j, y].BackID = data.ReadByte(); }
        }
        public static void WriteColumnOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int height)
        {
            data.Add((ushort)x, (ushort)y, (ushort)height);
            int endY = (y + height);
            for (int k = y; k < endY; k++) data.Add(tiles[x, k].ForeID, tiles[x, k].BackID);
        }
        public static void ReadColumnOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), height = data.ReadUInt16(), endY = (y + height);
            for (int k = y; k < endY; k++) { tiles[x, k].ForeID = data.ReadByte(); tiles[x, k].BackID = data.ReadByte(); }
        }
    }
}