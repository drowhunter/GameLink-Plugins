using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;

using Timer = System.Timers.Timer;
namespace GT7Plugin
{
    public class UDPListener
    {
        public delegate void PacketReceived(object sender, byte[] buffer);
        private int heartBeatPort = 33739;

        private UdpClient udpClient;
        private IPEndPoint remoteEndPoint;

        
        private CancellationTokenSource cancellationTokenSource;

        public event PacketReceived OnPacketReceived;

        private byte[] _heartbeatBytes;

        Timer hbTimer;

        private SimInterfacePacketType _packetType;

        private SimulatorInterfaceCryptorGT7 _cryptor;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <param name="packetType"></param>
        /// <param name="sharedMode">Enable shared UDP port listening (allows multiple apps on same port). Default: true</param>
        /// <param name="gameAddress">Heartbeat destination. Broadcast for a PlayStation on the LAN, loopback/host IP for RyGT7 on this PC.</param>
        public UDPListener(SimInterfacePacketType packetType, IPAddress gameAddress, int port = 33740, bool sharedMode = true)
        {
            _packetType = packetType;

            _heartbeatBytes = (packetType switch
            {
                SimInterfacePacketType.PacketType1 => "A"u8,
                SimInterfacePacketType.PacketType2 => "B"u8,
                SimInterfacePacketType.PacketType3 => "~"u8,
                _ => "A"u8, // We should default to "~".
            }).ToArray();

            _cryptor = new (packetType);

            cancellationTokenSource = new CancellationTokenSource();

            if (sharedMode)
            {
                // Create socket with shared mode to allow multiple listeners on the same UDP port
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // Allow multiple listeners on the same UDP port
                // On Windows, ReuseAddress enables port sharing for UDP
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // Bind to the port
                socket.Bind(new IPEndPoint(IPAddress.Any, port));

                // Create UdpClient with the configured socket
                udpClient = new UdpClient();
                udpClient.Client.Dispose();
                udpClient.Client = socket;
            }
            else
            {
                // Standard mode - exclusive port binding
                udpClient = new UdpClient(port);
            }

            DisableIcmpConnectionReset(udpClient.Client);

            if (port == 33740) {
                // only send heartbeats if we are listening on the default port,
                // otherwise the data is coming from localhost which is NOT the playstation.
                remoteEndPoint = new IPEndPoint(gameAddress ?? IPAddress.Broadcast, heartBeatPort);

                hbTimer = new Timer(TimeSpan.FromSeconds(1));
                hbTimer.Elapsed += SendHeartbeat;
                hbTimer.Enabled = true;
            }

            _ = Receive(cancellationTokenSource.Token);
        }

        private async Task Receive(CancellationToken cancelToken)
        {
            // This loop is fire-and-forget: any exception that escapes it stops telemetry for good
            // with no error shown, so only cancellation or disposal may end it.
            while (!cancelToken.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await udpClient.ReceiveAsync(cancelToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    // e.g. ICMP "port unreachable" from a heartbeat sent while the game was not running.
                    continue;
                }

                if (result.Buffer.Length != GetExpectedPacketSize())
                    continue; // game latched onto a different packet type, or a stray packet

                try
                {
                    _cryptor.Decrypt(result.Buffer);
                    OnPacketReceived?.Invoke(this, result.Buffer);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GT7Plugin: packet handler failed: {ex}");
                }
            }
        }

        /// <summary>
        /// On Windows an ICMP "port unreachable" for a UDP send (heartbeat while the game is down)
        /// is reported as WSAECONNRESET on the next receive. Turn that reporting off.
        /// </summary>
        private static void DisableIcmpConnectionReset(Socket socket)
        {
            if (!OperatingSystem.IsWindows())
                return;

            const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);
            try
            {
                socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (SocketException)
            {
                // Not supported on this stack; the receive loop also tolerates the error.
            }
        }

        public void Stop()
        {
            hbTimer?.Stop();
            cancellationTokenSource.Cancel();

            udpClient.Dispose();
            udpClient = null;
        }

        private void SendHeartbeat(object? sender, ElapsedEventArgs e)
        {
            try
            {
                udpClient?.Send(_heartbeatBytes, remoteEndPoint);
            }
            catch (SocketException)
            {
                // Game not up yet / host unreachable - keep retrying on the next tick.
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// TODO: Game might send a packet of 0x94 if not using 'A' type heartbeat?
        /// Might need checking. Might also be exclusive to GT7 >= 1.42
        /// </summary>
        /// <returns></returns>
        private uint GetExpectedPacketSize()
        {
            return _packetType switch
            {
                SimInterfacePacketType.PacketType1 => 0x128,
                SimInterfacePacketType.PacketType2 => 0x13C,
                SimInterfacePacketType.PacketType3 => 0x158,
                _ => 0x128,
            };
        }
    }
}
