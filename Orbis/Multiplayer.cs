using System;
using Lidgren.Network;
using SharpXNA;

namespace Orbis
{
    public static class Multiplayer
    {
        public enum Packets
        {
            Connection,
            Disconnection,
            Initial,
            Position
        }

        private static Game.Frames Frame
        {
            get { return Game.Frame; }
            set { Game.Frame = value; }
        }

        private static Player Self
        {
            get { return Game.Self; }
            set { Game.Self = value; }
        }

        private static Player[] Players
        {
            get { return Game.Players; }
            set { Game.Players = value; }
        }

        public static void CreateLobby(string playerName)
        {
            Players = new Player[10];
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
                    var data = new Network.Packet((byte) Packets.Initial, (byte) Players.Length, connector.Slot);
                    foreach (var t in Players)
                    {
                        if (!t.Matches(null, connector))
                        {
                            data.Add(true, t.Name);
                        }
                        else
                        {
                            data.Add(false);
                        }
                        message.SenderConnection.Approve(data.Construct());
                        new Network.Packet((byte) Packets.Connection, connector.Slot, connector.Name).Send(
                            message.SenderConnection);
                    }
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

        public static void OnData(NetIncomingMessage message)
        {
            ProcessPacket((Packets) message.ReadByte(), message);
        }

        public static void ProcessPacket(Packets packet, NetIncomingMessage message)
        {
            switch (packet)
            {
                case Packets.Connection:
                    Player.Set(message.ReadByte(), new Player(message.ReadString()));
                    break;
                case Packets.Disconnection:
                    var disconnector = Network.IsServer
                        ? Player.Get(message.SenderConnection)
                        : Network.IsClient ? Players[message.ReadByte()] : null;
                    if (disconnector != null) Player.Remove(disconnector);
                    if (Network.IsServer)
                        new Network.Packet((byte) packet, disconnector?.Slot).Send(message.SenderConnection);
                    break;
                case Packets.Initial:
                    Players = new Player[message.ReadByte()];
                    Self = Player.Set(message.ReadByte(), new Player(Game.Name));
                    for (var i = 0; i < Players.Length; i++)
                        if (message.ReadBoolean())
                            Players[i] = Player.Set((byte) i, new Player(message.ReadString()));
                    Timers.Add("Positions", 1/20d);
                    Frame = Game.Frames.LoadGame;
                    break;
                case Packets.Position:
                    if (Network.IsServer)
                    {
                        var sender = Player.Get(message.SenderConnection);
                        if (sender != null)
                        {
                            sender.Position = message.ReadVector2();
                        }
                    }
                    else if (Network.IsClient)
                    {
                        var count = (message.LengthBytes - 1)/13;
                        for (var i = 0; i < count; i++)
                        {
                            var sender = Players[message.ReadByte()];
                            if (sender != null)
                            {
                                sender.Position = message.ReadVector2();
                            }
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(packet), packet, null);
            }
        }
    }
}