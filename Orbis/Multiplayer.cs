using Lidgren.Network;
using SharpXNA;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Orbis
{
    using Packet = Network.Packet;
    using Frames = Game.Frames;

    public static class Multiplayer
    {
        public enum Packets { Connection, Disconnection, Initial, PlayerInput, PlayerData, WorldData, TileData, FinalData, RectangleOfTiles, RowOfTiles, ColumnOfTiles,
            PlayerAddItem, PlayerSetItem, PlayerSetInv, PlayerRemoveItem, PlayerDropItem }

        public const int ChunkWidth = 10, ChunkHeight = 10, ChunkBufferWidth = 16, ChunkBufferHeight = 12, Port = 27000;

        private static Frames Frame { get { return Game.Frame; } set { Game.Frame = value; } }
        private static Player Self{ get { return Game.Self; } set { Game.Self = value; } }
        private static Player[] Players { get { return Game.Players; } set { Game.Players = value; } }
        private static Dictionary<string, Item> Items => Game.Items;
        private static World World { get { return Game.World; } set { Game.World = value; } }
        private static string LoadingText { get { return Game.LoadingText; } set { Game.LoadingText = value; } }
        private static float LoadingPercentage { get { return Game.LoadingPercentage; } set { Game.LoadingPercentage = value; } }

        public static void CreateLobby(string playerName)
        {
            Players = new Player[256];
            Self = Player.Add(new Player(playerName));
            Network.StartHosting(Port, Players.Length);
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
                var connector = Player.Add(new Player(message.ReadString()) {Connection = message.SenderConnection,IsVelocityLocked = true});
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
                LoadingText = null;
                Frame = Frames.LoadGame;
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
                        if (!t.Matches(null, sender))
                        {
                            playerData.Add(true, t.Name, t.LinearPosition, t.Velocity, (byte)t.LastInput);
                            for (var i = 0; i < Inventory.PlayerInvSize; i++)
                            {
                                if (t.GetItem(i) != null) playerData.Add(true, t.GetItem(i).Key, t.GetItem(i).Stack);
                                else playerData.Add(false);
                            }
                        }
                        else playerData.Add(false);
                    playerData.SendTo(message.SenderConnection);
                    var finalData = new Packet((byte) Packets.FinalData);
                    finalData.SendTo(message.SenderConnection);
                }
                else
                    for (var i = 0; i < Players.Length; i++)
                        if (message.ReadBoolean())
                        {
                            Players[i] = Player.Set((byte) i, new Player(message.ReadString()) {LinearPosition = message.ReadVector2(), Velocity = message.ReadVector2(), LastInput = (Player.Inputs) message.ReadByte()});
                            if (Players[i].LastInput.HasFlag(Player.Inputs.DirLeft)) Players[i].Direction = -1;
                            else if (Players[i].LastInput.HasFlag(Player.Inputs.DirRight)) Players[i].Direction = 1;
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
                    new Packet((byte) Packets.WorldData, World.Width, World.Height, World.Spawn).SendTo(message.SenderConnection);
                    //for (var x = -ChunkBufferWidth; x < ChunkBufferWidth; x++)
                    //    for (var y = -ChunkBufferHeight; y < ChunkBufferHeight; y++)
                    //    {
                    //        var tileData = new Packet((byte)Packets.RectangleOfTiles);
                    //        WriteRectangleOfTiles(ref World.Tiles, ref tileData, (World.Spawn.X+(x*ChunkWidth)), (World.Spawn.Y+(y*ChunkHeight)), ChunkWidth, ChunkHeight);
                    //        tileData.SendTo(message.SenderConnection);
                    //    }
                    const int widthOfTilesToSync = 64;
                    var thread = new Thread(() =>
                    {
                        var x = 1;
                        for (var y = 1; y < (World.Height - 1); y++, x = 1)
                            while (x < (World.Width - 1))
                            {
                                var tileData = new Packet((byte) Packets.TileData);
                                x = WriteRowOfTiles(ref tileData, World, x, y, widthOfTilesToSync);
                                tileData.SendTo(message.SenderConnection);
                            }
                    }) {IsBackground = true};
                    thread.Start();
                    var sender = Player.Get(message.SenderConnection);
                    sender.Spawn(World.Spawn);
                    sender.LastTileX = World.Spawn.X;
                    sender.LastTileY = World.Spawn.Y;
                    sender.IsVelocityLocked = false;
                }
                else
                {
                    LoadingText = "Requesting Tile Data"; LoadingPercentage = ReadRowOfTiles(ref message, World);
                    if (LoadingPercentage >= 100)
                    {
                        var invData = new Packet((byte)Packets.PlayerData);
                        for (var i = 0; i < Inventory.PlayerInvSize; i++)
                        {
                            if (Self.GetItem(i) != null) invData.Add(true, Self.GetItem(i).Key, Self.GetItem(i).Stack);
                            else invData.Add(false);
                        }
                        invData.Send();
                    }
                }
            }
            else if (packet == Packets.FinalData)
            {
                Game.InitializeGame();
                Self.Spawn(World.Spawn);
                Game.UpdateCamPos();
                Game.UpdateCamBounds(World, Game.Camera);
                Frame = Frames.Game;
            }
            #endregion
            #region Rectangle/Column/Row OfTiles
            //else if (packet == Packets.RectangleOfTiles) { ReadRectangleOfTiles(ref message, ref World.Tiles); }
            //else if (packet == Packets.ColumnOfTiles) { ReadColumnOfTiles(ref message, ref World.Tiles); }
            else if (packet == Packets.RowOfTiles) { ReadRowOfTiles(ref message, World); }
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
            #region PlayerInput
            else if (packet == Packets.PlayerInput)
            {
                if (Network.IsServer)
                {
                    var sender = Player.Get(message.SenderConnection);
                    if (sender == null) return;
                    sender.LinearPosition = message.ReadVector2();
                    sender.Velocity = message.ReadVector2();
                    sender.LastInput = (Player.Inputs)message.ReadByte();
                    if (sender.LastInput.HasFlag(Player.Inputs.DirLeft)) sender.Direction = -1;
                    else if (sender.LastInput.HasFlag(Player.Inputs.DirRight)) sender.Direction = 1;
                    new Packet((byte) packet, sender.Slot, sender.LinearPosition, sender.Velocity, (byte) sender.LastInput).Send(message.SenderConnection);
                }
                else
                {
                    var sender = Players[message.ReadByte()];
                    if (sender == null) return;
                    sender.LinearPosition = message.ReadVector2();
                    sender.Velocity = message.ReadVector2();
                    sender.LastInput = (Player.Inputs)message.ReadByte();
                    if (sender.LastInput.HasFlag(Player.Inputs.DirLeft)) sender.Direction = -1;
                    else if (sender.LastInput.HasFlag(Player.Inputs.DirRight)) sender.Direction = 1;
                }
            }
            #endregion
            else { throw new ArgumentOutOfRangeException(nameof(packet), packet, null); }
        }

        public static int WriteRowOfTiles(ref Packet data, World world, int x, int y, ushort width)
        {
            data.Add((ushort) x, (ushort) y, width);
            var j = x;
            for (var t = 0; t < width; t++)
            {
                if (j >= (world.Width - 1)) break;
                ushort rle = 0;
                for (var k = (j + 1); k < (world.Width - 1); k++)
                {
                    if (!world.Tiles[k, y].CanRLE(world.Tiles[j, y])) break;
                    rle++;
                }
                data.Add(world.Tiles[j, y].ForeID, world.Tiles[j, y].BackID, world.Tiles[j, y].ForeStyle, rle);
                j += (rle + 1);
            }
            return j;
        }
        public static float ReadRowOfTiles(ref NetIncomingMessage data, World world)
        {
            int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), j = x;
            for (var t = 0; t < width; t++)
            {
                if (j >= (world.Width - 1)) break;
                world.Tiles[j, y].ForeID = data.ReadByte();
                world.Tiles[j, y].BackID = data.ReadByte();
                world.Tiles[j, y].ForeStyle = data.ReadByte();
                var rle = data.ReadUInt16();
                for (var k = 0; k <= rle; k++) world.Tiles[j, y].CopyTileTo(ref world.Tiles[(j + k), y]);
                j += (rle + 1);
            }
            return ((((j + 1) + (y*(World.Width-2)))/(float) ((World.Width - 2)*(World.Height-1)))*100);
        }

        //public static void WriteRectangleOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int width, int height)
        //{
        //    data.Add((ushort) x, (ushort) y, (ushort) width, (ushort) height);
        //    int endX = (x + width), endY = (y + height);
        //    for (var j = x; j < endX; j++) for (var k = y; k < endY; k++) if (World.InBounds(j, k)) data.Add(tiles[j, k].ForeID, tiles[j, k].BackID, tiles[j, k].ForeStyle);
        //}
        //public static void ReadRectangleOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        //{
        //    int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), height = data.ReadUInt16(), endX = (x + width), endY = (y + height);
        //    for (var j = x; j < endX; j++)
        //        for (var k = y; k < endY; k++)
        //            if (World.InBounds(j, k))
        //            {
        //                tiles[j, k].ForeID = data.ReadByte();
        //                tiles[j, k].BackID = data.ReadByte();
        //                tiles[j, k].ForeStyle = data.ReadByte();
        //            }
        //}
        //public static void WriteRowOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int width)
        //{
        //    data.Add((ushort) x, (ushort) y, (ushort) width);
        //    var endX = (x + width);
        //    for (var j = x; j < endX; j++) data.Add(tiles[j, y].ForeID, tiles[j, y].BackID, tiles[j, y].ForeStyle);
        //}
        //public static void ReadRowOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        //{
        //    int x = data.ReadUInt16(), y = data.ReadUInt16(), width = data.ReadUInt16(), endX = (x + width);
        //    for (var j = x; j < endX; j++)
        //    {
        //        tiles[j, y].ForeID = data.ReadByte();
        //        tiles[j, y].BackID = data.ReadByte();
        //        tiles[j, y].ForeStyle = data.ReadByte();
        //    }
        //}
        //public static void WriteColumnOfTiles(ref Tile[,] tiles, ref Packet data, int x, int y, int height)
        //{
        //    data.Add((ushort) x, (ushort) y, (ushort) height);
        //    var endY = (y + height);
        //    for (var k = y; k < endY; k++) data.Add(tiles[x, k].ForeID, tiles[x, k].BackID, tiles[x, k].ForeStyle);
        //}
        //public static void ReadColumnOfTiles(ref NetIncomingMessage data, ref Tile[,] tiles)
        //{
        //    int x = data.ReadUInt16(), y = data.ReadUInt16(), height = data.ReadUInt16(), endY = (y + height);
        //    for (var k = y; k < endY; k++)
        //    {
        //        tiles[x, k].ForeID = data.ReadByte();
        //        tiles[x, k].BackID = data.ReadByte();
        //        tiles[x, k].ForeStyle = data.ReadByte();
        //    }
        //}
    }
}