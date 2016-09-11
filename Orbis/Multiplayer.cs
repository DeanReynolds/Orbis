using Lidgren.Network;
using SharpXNA;
using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orbis.World;

namespace Orbis
{
    using Packet = Network.Packet;
    using Frames = Game.Frames;

    public static class Multiplayer
    {
        public enum Packets { Connection, Disconnection, Initial, Position, PlayerData, TileData, RectangleOfTiles, RowOfTiles, ColumnOfTiles }

        private const int ChunkWidth = Game.ChunkWidth, ChunkHeight = Game.ChunkHeight, ChunkSyncSize = 3;

        private static Frames Frame { get { return Game.Frame; } set { Game.Frame = value; } }
        private static Player Self{ get { return Game.Self; } set { Game.Self = value; } }
        private static Player[] Players { get { return Game.Players; } set { Game.Players = value; } }
        private static Point Spawn { get { return Game.Spawn; } set { Game.Spawn = value; } }
        private static RenderTarget2D Lighting { get { return Game.Lighting; } set { Game.Lighting = value; } }
        private static Thread LightingThread { get { return Game.LightingThread; } set { Game.LightingThread = value; } }
        private static Camera Camera { get { return Game.Camera; } set { Game.Camera = value; } }
        private static float LineThickness { get { return Game.LineThickness; } set { Game.LineThickness = value; } }

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
            Frame = Frames.Menu;
        }

        public static void OnConnectionApproval(NetIncomingMessage message)
        {
            var clientVersion = message.ReadUInt64();
            if (clientVersion == Globe.Version)
            {
                var connector = Player.Add(new Player(message.ReadString()) {Connection = message.SenderConnection});
                if (connector != null)
                {
                    var data = new Packet((byte) Packets.Initial, (byte)(Players.Length - 1), connector.Slot);
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
            switch (packet)
            {
                case Packets.Connection:
                    Player.Set(message.ReadByte(), new Player(message.ReadString()));
                    break;
                case Packets.Disconnection:
                    var disconnector = Network.IsServer ? Player.Get(message.SenderConnection) : Network.IsClient ? Players[message.ReadByte()] : null;
                    if (disconnector != null) Player.Remove(disconnector);
                    if (Network.IsServer) new Packet((byte)packet, disconnector?.Slot).Send(message.SenderConnection);
                    break;
                case Packets.Initial:
                    Players = new Player[message.ReadByte() + 1];
                    Self = Player.Set(message.ReadByte(), new Player(Settings.Get("Name")));
                    Timers.Add("posSync", 1 / 20d);
                    Camera = new Camera() { Zoom = Game.CameraZoom }; Game.UpdateResCamStuff(); LineThickness = (1 / Camera.Zoom);
                    Frame = Frames.LoadGame;
                    new Packet((byte)Packets.PlayerData).Send();
                    new Packet((byte)Packets.TileData).Send();
                    break;
                case Packets.PlayerData:
                    if (Network.IsServer)
                    {
                        var sender = Player.Get(message.SenderConnection);
                        var data = new Packet((byte)packet);
                        foreach (var t in Players)
                        {
                            if (!t.Matches(null, sender)) data.Add(true, t.Name);
                            else data.Add(false);
                        }
                        data.SendTo(message.SenderConnection);
                    }
                    else for (var i = 0; i < Players.Length; i++) if (message.ReadBoolean()) Players[i] = Player.Set((byte)i, new Player(message.ReadString()));
                    break;
                case Packets.TileData:
                    if (Network.IsServer)
                    {
                        var data = new Packet((byte)packet, (ushort)Game.Tiles.GetLength(0), (ushort)Game.Tiles.GetLength(1));
                        WriteRectangleOfTiles(ref Game.Tiles, ref data, (Spawn.X - (ChunkWidth / 2)), (Spawn.Y - (ChunkHeight / 2)), ChunkWidth, ChunkHeight);
                        data.Add((ushort)Spawn.X, (ushort)Spawn.Y);
                        var sender = Player.Get(message.SenderConnection);
                        sender.Spawn(Spawn); sender.LastTileX = Spawn.X; sender.LastTileY = Spawn.Y;
                        data.SendTo(message.SenderConnection);
                    }
                    else
                    {
                        Game.Tiles = new Tile[message.ReadUInt16(), message.ReadUInt16()];
                        ReadRectangleOfTiles(ref message, ref Game.Tiles);
                        Spawn = new Point(message.ReadUInt16(), message.ReadUInt16());
                        Self.Spawn(Spawn); Camera.Position = Self.WorldPosition;
                        Game.UpdateCamPos(); Game.UpdateCamBounds(); Game.InitializeLighting();
                        LightingThread = new Thread(() => { while (true) { Game.UpdateLighting(); Thread.Sleep(100); } }) { Name = "Lighting", IsBackground = true };
                        LightingThread.Start();
                        Game.LoadGameTextures();
                        Frame = Frames.Game;
                    }
                    break;
                case Packets.RectangleOfTiles:
                    ReadRectangleOfTiles(ref message, ref Game.Tiles);
                    break;
                case Packets.ColumnOfTiles:
                    ReadColumnOfTiles(ref message, ref Game.Tiles);
                    break;
                case Packets.RowOfTiles:
                    ReadRowOfTiles(ref message, ref Game.Tiles);
                    break;
                case Packets.Position:
                    if (Network.IsServer)
                    {
                        var sender = Player.Get(message.SenderConnection);
                        if (sender != null)
                        {
                            sender.LinearPosition = message.ReadVector2();
                            sender.Velocity = message.ReadVector2();
                            sender.UpdateTilePos();
                            //while (sender.TileX < sender.LastTileX) { var data = new Packet((byte)Packets.ColumnOfTiles); WriteColumnOfTiles(ref Game.Tiles, ref data, (sender.LastTileX - (ChunkWidth / 2) - 1), (sender.LastTileY - (ChunkHeight / 2)), ChunkHeight); sender.LastTileX--; data.SendTo(sender.Connection); }
                            //while (sender.TileX > sender.LastTileX) { var data = new Packet((byte)Packets.ColumnOfTiles); WriteColumnOfTiles(ref Game.Tiles, ref data, (sender.LastTileX + ((ChunkWidth / 2))), (sender.LastTileY - (ChunkHeight / 2)), ChunkHeight); sender.LastTileX++; data.SendTo(sender.Connection); }
                            //while (sender.TileY < sender.LastTileY) { var data = new Packet((byte)Packets.RowOfTiles); WriteRowOfTiles(ref Game.Tiles, ref data, (sender.LastTileX - (ChunkWidth / 2)), (sender.LastTileY - (ChunkHeight / 2) - 1), ChunkWidth); sender.LastTileY--; data.SendTo(sender.Connection); }
                            //while (sender.TileY > sender.LastTileY) { var data = new Packet((byte)Packets.RowOfTiles); WriteRowOfTiles(ref Game.Tiles, ref data, (sender.LastTileX - (ChunkWidth / 2)), (sender.LastTileY + ((ChunkHeight / 2))), ChunkWidth); sender.LastTileY++; data.SendTo(sender.Connection); }
                            const int chunkSyncSizeMinus = (ChunkSyncSize - 1);
                            while (sender.TileX < (sender.LastTileX - chunkSyncSizeMinus)) { var data = new Packet((byte)Packets.RectangleOfTiles); WriteRectangleOfTiles(ref Game.Tiles, ref data, (sender.LastTileX - (ChunkWidth / 2) - ChunkSyncSize), (sender.LastTileY - (ChunkHeight / 2)), ChunkSyncSize, ChunkHeight); sender.LastTileX -= ChunkSyncSize; data.SendTo(sender.Connection); }
                            while (sender.TileX > (sender.LastTileX + chunkSyncSizeMinus)) { var data = new Packet((byte)Packets.RectangleOfTiles); WriteRectangleOfTiles(ref Game.Tiles, ref data, (sender.LastTileX + (ChunkWidth / 2) + chunkSyncSizeMinus), (sender.LastTileY - (ChunkHeight / 2)), ChunkSyncSize, ChunkHeight); sender.LastTileX += ChunkSyncSize; data.SendTo(sender.Connection); }
                            while (sender.TileY < (sender.LastTileY - chunkSyncSizeMinus)) { var data = new Packet((byte)Packets.RectangleOfTiles); WriteRectangleOfTiles(ref Game.Tiles, ref data, (sender.LastTileX - (ChunkWidth / 2)), (sender.LastTileY - (ChunkHeight / 2) - ChunkSyncSize), ChunkWidth, ChunkSyncSize); sender.LastTileY -= ChunkSyncSize; data.SendTo(sender.Connection); }
                            while (sender.TileY > (sender.LastTileY + chunkSyncSizeMinus)) { var data = new Packet((byte)Packets.RectangleOfTiles); WriteRectangleOfTiles(ref Game.Tiles, ref data, (sender.LastTileX - (ChunkWidth / 2)), (sender.LastTileY - (ChunkHeight / 2) + chunkSyncSizeMinus), ChunkWidth, ChunkSyncSize); sender.LastTileY += ChunkSyncSize; data.SendTo(sender.Connection); }
                        }
                    }
                    else
                    {
                        var count = (message.LengthBytes - 1) / 17;
                        for (var i = 0; i < count; i++)
                        {
                            var sender = Players[message.ReadByte()];
                            if (sender != null)
                            {
                                sender.LinearPosition = message.ReadVector2();
                                sender.Velocity = message.ReadVector2();
                            }
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(packet), packet, null);
            }
        }

        public static void WriteRectangleOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int width, int height)
        {
            data.Add((ushort) x, (ushort) y, (ushort) width, (ushort) height);
            int endX = (x + width), endY = (y + height);
            for (var j = x; j < endX; j++) for (var k = y; k < endY; k++) if (Game.InBounds(j, k)) data.Add(tiles[j, k].ForeID, tiles[j, k].BackID, tiles[j, k].Style);
        }

        public static void ReadRectangleOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), height = data.ReadUInt16(), endX = (x + width), endY = (y + height);
            for (var j = x; j < endX; j++)
                for (var k = y; k < endY; k++)
                    if (Game.InBounds(j, k))
                    {
                        tiles[j, k].ForeID = data.ReadByte();
                        tiles[j, k].BackID = data.ReadByte();
                        tiles[j, k].Style = data.ReadByte();
                    }
        }

        public static void WriteRowOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int width)
        {
            data.Add((ushort) x, (ushort) y, (ushort) width);
            var endX = (x + width);
            for (var j = x; j < endX; j++) data.Add(tiles[j, y].ForeID, tiles[j, y].BackID, tiles[j, y].Style);
        }

        public static void ReadRowOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), endX = (x + width);
            for (var j = x; j < endX; j++)
            {
                tiles[j, y].ForeID = data.ReadByte();
                tiles[j, y].BackID = data.ReadByte();
                tiles[j, y].Style = data.ReadByte();
            }
        }

        public static void WriteColumnOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int height)
        {
            data.Add((ushort) x, (ushort) y, (ushort) height);
            var endY = (y + height);
            for (var k = y; k < endY; k++) data.Add(tiles[x, k].ForeID, tiles[x, k].BackID, tiles[x, k].Style);
        }

        public static void ReadColumnOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), height = data.ReadUInt16(), endY = (y + height);
            for (var k = y; k < endY; k++)
            {
                tiles[x, k].ForeID = data.ReadByte();
                tiles[x, k].BackID = data.ReadByte();
                tiles[x, k].Style = data.ReadByte();
            }
        }
    }
}