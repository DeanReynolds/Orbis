using Lidgren.Network;
using SharpXNA;
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orbis
{
    using Packet = Network.Packet;
    using Frames = Game.Frames;

    public static class Multiplayer
    {
        public enum Packets { Connection, Disconnection, Initial, Position, PlayerData, WorldData, TileData, FinalData, RectangleOfTiles, RowOfTiles, ColumnOfTiles,
            PlayerAddItem, PlayerSetItem, PlayerSetInv, PlayerRemoveItem, PlayerDropItem }

        private const int ChunkWidth = 10, ChunkHeight = 10, ChunkBufferWidth = 16, ChunkBufferHeight = 12;

        private static Frames Frame { get { return Game.Frame; } set { Game.Frame = value; } }
        private static Player Self{ get { return Game.Self; } set { Game.Self = value; } }
        private static Player[] Players { get { return Game.Players; } set { Game.Players = value; } }
        private static Dictionary<string, Item> Items => Game.Items;
        private static World World { get { return Game.World; } set { Game.World = value; } }
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
            Frame = Frames.Menu;
        }

        public static void OnConnectionApproval(NetIncomingMessage message)
        {
            var clientVersion = message.ReadUInt64();
            if (clientVersion == Globe.Version)
            {
                var connector = Player.Add(new Player(message.ReadString()) {Connection = message.SenderConnection,IsVelocitLocked = true});
                if (connector != null)
                {
                    var data = new Packet((byte) Packets.Initial, (byte)(Players.Length - 1), connector.Slot);
                    message.SenderConnection.Approve(data.Construct());
                    new Packet((byte)Packets.Connection, connector.Slot, connector.Name).Send(message.SenderConnection);
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
            if (packet == Packets.Connection) { Player.Set(message.ReadByte(), new Player(message.ReadString())); }
            else if (packet == Packets.Disconnection)
            {
                var disconnector = Network.IsServer ? Player.Get(message.SenderConnection) : Network.IsClient ? Players[message.ReadByte()] : null;
                if (disconnector != null) Player.Remove(disconnector);
                if (Network.IsServer) new Packet((byte) packet, disconnector?.Slot).Send(message.SenderConnection);
            }
            #endregion
            #region Initial/PlayerSetInv/PlayerData/TileData
            else if (packet == Packets.Initial)
            {
                Players = new Player[message.ReadByte() + 1];
                Self = Player.Set(message.ReadByte(), new Player(Settings.Get("Name")));
                Timers.Add("posSync", 1/20d);
                Camera = new Camera() {Zoom = Game.CameraZoom};
                Game.UpdateResCamStuff();
                Frame = Frames.LoadGame;
                var invData = new Packet((byte) Packets.PlayerData);
                for (var i = 0; i < Inventory.PlayerInvSize; i++)
                {
                    if (Self.GetItem(i) != null) invData.Add(true, Self.GetItem(i).Key, Self.GetItem(i).Stack);
                    else invData.Add(false);
                }
                invData.Send();
                new Packet((byte) Packets.TileData).Send();
            }
            else if (packet == Packets.PlayerData)
            {
                if (Network.IsServer)
                {
                    var sender = Player.Get(message.SenderConnection);
                    var invData = new Packet((byte) Packets.PlayerSetInv, sender.Slot);
                    for (var i = 0; i < Inventory.PlayerInvSize; i++)
                        if (message.ReadBoolean())
                        {
                            sender.SetItem(i, Items[message.ReadString()].Clone(message.ReadInt32()), false);
                            invData.Add(true, sender.GetItem(i).Key, sender.GetItem(i).Stack);
                        }
                        else invData.Add(false);
                    invData.Send(message.SenderConnection);
                    var playerData = new Packet((byte) packet);
                    foreach (var t in Players)
                    {
                        if (!t.Matches(null, sender))
                        {
                            playerData.Add(true, t.Name);
                            for (var i = 0; i < Inventory.PlayerInvSize; i++)
                            {
                                if (t.GetItem(i) != null) playerData.Add(true, t.GetItem(i).Key, t.GetItem(i).Stack);
                                else playerData.Add(false);
                            }
                        }
                        else playerData.Add(false);
                    }
                    playerData.SendTo(message.SenderConnection);
                }
                else
                    for (var i = 0; i < Players.Length; i++)
                        if (message.ReadBoolean())
                        {
                            Players[i] = Player.Set((byte) i, new Player(message.ReadString())); 
                            for (var j = 0; j < Inventory.PlayerInvSize; j++) if (message.ReadBoolean()) Players[i].SetItem(j, Items[message.ReadString()].Clone(message.ReadInt32()), false);
                        }
            }
            else if (packet == Packets.PlayerSetInv)
            {
                var sender = Network.IsServer ? Player.Get(message.SenderConnection) : Network.IsClient ? Players[message.ReadByte()] : null;
                if (sender == null) return;
                for (var i = 0; i < Inventory.PlayerInvSize; i++) if (message.ReadBoolean()) sender.SetItem(i, Items[message.ReadString()].Clone(message.ReadInt32()));
            }
            else if (packet == Packets.WorldData) { World = new World(message.ReadInt32(), message.ReadInt32()) {Spawn = message.ReadPoint()}; }
            else if (packet == Packets.TileData)
            {
                if (Network.IsServer)
                {
                    new Packet((byte)Packets.WorldData, World.Width, World.Height, World.Spawn).SendTo(message.SenderConnection);
                    for (var x = -ChunkBufferWidth; x < ChunkBufferWidth; x++)
                        for (var y = -ChunkBufferHeight; y < ChunkBufferHeight; y++)
                        {
                            var tileData = new Packet((byte)Packets.RectangleOfTiles);
                            WriteRectangleOfTiles(ref World.Tiles, ref tileData, (World.Spawn.X+(x*ChunkWidth)), (World.Spawn.Y+(y*ChunkHeight)), ChunkWidth, ChunkHeight);
                            tileData.SendTo(message.SenderConnection);
                        }
                    var sender = Player.Get(message.SenderConnection);
                    sender.Spawn(World.Spawn);
                    sender.LastTileX = World.Spawn.X;
                    sender.LastTileY = World.Spawn.Y;
                    sender.IsVelocitLocked = false;
                    var finalData = new Packet((byte) Packets.FinalData);
                    finalData.SendTo(message.SenderConnection);
                }
            }
            else if (packet == Packets.FinalData)
            {
                Self.Spawn(World.Spawn);
                Game.UpdateCamPos();
                Game.UpdateCamBounds();
                Game.InitializeLightingThread();
                Game.InitializeLighting();
                LightingThread.Start();
                Game.LoadGameTextures();
                Frame = Frames.Game;
            }
            #endregion
            #region Rectangle/Column/Row OfTiles
            else if (packet == Packets.RectangleOfTiles) { ReadRectangleOfTiles(ref message, ref World.Tiles); }
            else if (packet == Packets.ColumnOfTiles) { ReadColumnOfTiles(ref message, ref World.Tiles); }
            else if (packet == Packets.RowOfTiles) { ReadRowOfTiles(ref message, ref World.Tiles); }
            #endregion
            #region Player Add/Set/Remove Item
            else if (packet == Packets.PlayerAddItem)
            {
                var sender = Network.IsServer ? Player.Get(message.SenderConnection) : Network.IsClient ? Players[message.ReadByte()] : null;
                sender?.AddItem(Items[message.ReadString()].Clone(message.ReadInt32()));
            }
            else if (packet == Packets.PlayerSetItem)
            {
                var sender = Network.IsServer ? Player.Get(message.SenderConnection) : Network.IsClient ? Players[message.ReadByte()] : null;
                sender?.SetItem(message.ReadInt32(), Items[message.ReadString()].Clone(message.ReadInt32()));
            }
            else if (packet == Packets.PlayerRemoveItem)
            {
                var sender = Network.IsServer ? Player.Get(message.SenderConnection) : Network.IsClient ? Players[message.ReadByte()] : null;
                if (sender == null) return;
                if (message.LengthBytes == 5) sender.RemoveItem(message.ReadInt32());
                else sender.RemoveItem(Items[message.ReadString()].Clone(message.ReadInt32()));
            }
            #endregion
            #region Position
            else if (packet == Packets.Position)
            {
                if (Network.IsServer)
                {
                    var sender = Player.Get(message.SenderConnection);
                    if (sender == null) return;
                    sender.LinearPosition = message.ReadVector2();
                    sender.Velocity = message.ReadVector2();
                    const int chunkWidthBuffered = (ChunkWidth*ChunkBufferWidth);
                    const int chunkHeightBuffered = (ChunkHeight*ChunkBufferHeight);
                    while (sender.TileX < (sender.LastTileX - ChunkWidth))
                    {
                        var data = new Packet((byte)Packets.RectangleOfTiles);
                        WriteRectangleOfTiles(ref World.Tiles, ref data, (sender.LastTileX - chunkWidthBuffered - ChunkWidth), (sender.LastTileY - chunkHeightBuffered), ChunkWidth, (chunkHeightBuffered * 2));
                        sender.LastTileX -= ChunkWidth;
                        data.SendTo(sender.Connection);
                    }
                    while (sender.TileX > (sender.LastTileX + ChunkWidth))
                    {
                        var data = new Packet((byte)Packets.RectangleOfTiles);
                        WriteRectangleOfTiles(ref World.Tiles, ref data, (sender.LastTileX + chunkWidthBuffered), (sender.LastTileY - chunkHeightBuffered), ChunkWidth, (chunkHeightBuffered * 2));
                        sender.LastTileX += ChunkWidth;
                        data.SendTo(sender.Connection);
                    }
                    while (sender.TileY < (sender.LastTileY - ChunkHeight))
                    {
                        var data = new Packet((byte)Packets.RectangleOfTiles);
                        WriteRectangleOfTiles(ref World.Tiles, ref data, (sender.LastTileX - chunkWidthBuffered), (sender.LastTileY - chunkHeightBuffered - ChunkHeight), (chunkWidthBuffered * 2), ChunkHeight);
                        sender.LastTileY -= ChunkHeight;
                        data.SendTo(sender.Connection);
                    }
                    while (sender.TileY > (sender.LastTileY + ChunkHeight))
                    {
                        var data = new Packet((byte)Packets.RectangleOfTiles);
                        WriteRectangleOfTiles(ref World.Tiles, ref data, (sender.LastTileX - chunkWidthBuffered), (sender.LastTileY + chunkHeightBuffered), (chunkWidthBuffered * 2), ChunkHeight);
                        sender.LastTileY += ChunkHeight;
                        data.SendTo(sender.Connection);
                    }
                }
                else
                {
                    var count = (message.LengthBytes - 1)/17;
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
            }
            #endregion
            else { throw new ArgumentOutOfRangeException(nameof(packet), packet, null); }
        }

        public static void WriteRectangleOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int width, int height)
        {
            data.Add((ushort) x, (ushort) y, (ushort) width, (ushort) height);
            int endX = (x + width), endY = (y + height);
            for (var j = x; j < endX; j++) for (var k = y; k < endY; k++) if (World.InBounds(j, k)) data.Add(tiles[j, k].ForeID, tiles[j, k].BackID, tiles[j, k].ForeStyle);
        }
        public static void ReadRectangleOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), height = data.ReadUInt16(), endX = (x + width), endY = (y + height);
            for (var j = x; j < endX; j++)
                for (var k = y; k < endY; k++)
                    if (World.InBounds(j, k))
                    {
                        tiles[j, k].ForeID = data.ReadByte();
                        tiles[j, k].BackID = data.ReadByte();
                        tiles[j, k].ForeStyle = data.ReadByte();
                    }
        }
        public static void WriteRowOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int width)
        {
            data.Add((ushort) x, (ushort) y, (ushort) width);
            var endX = (x + width);
            for (var j = x; j < endX; j++) data.Add(tiles[j, y].ForeID, tiles[j, y].BackID, tiles[j, y].ForeStyle);
        }
        public static void ReadRowOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), endX = (x + width);
            for (var j = x; j < endX; j++)
            {
                tiles[j, y].ForeID = data.ReadByte();
                tiles[j, y].BackID = data.ReadByte();
                tiles[j, y].ForeStyle = data.ReadByte();
            }
        }
        public static void WriteColumnOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int height)
        {
            data.Add((ushort) x, (ushort) y, (ushort) height);
            var endY = (y + height);
            for (var k = y; k < endY; k++) data.Add(tiles[x, k].ForeID, tiles[x, k].BackID, tiles[x, k].ForeStyle);
        }
        public static void ReadColumnOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), height = data.ReadUInt16(), endY = (y + height);
            for (var k = y; k < endY; k++)
            {
                tiles[x, k].ForeID = data.ReadByte();
                tiles[x, k].BackID = data.ReadByte();
                tiles[x, k].ForeStyle = data.ReadByte();
            }
        }
    }
}