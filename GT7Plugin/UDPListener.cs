using System.Diagnostics;
using System.Linq.Expressions;
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
        private int targetPort = 33739;

        private UdpClient udpClient;
        private IPEndPoint remoteEndPoint;

        private Task receiveTask;
        private CancellationTokenSource cancellationTokenSource;

        public event PacketReceived OnPacketReceived;

        Timer hbTimer;
        public UDPListener(int port = 33740)
        {
            udpClient = new UdpClient(port);
            remoteEndPoint = new IPEndPoint(IPAddress.Broadcast, targetPort);
            cancellationTokenSource = new CancellationTokenSource();
            hbTimer = new Timer(TimeSpan.FromSeconds(1));
            hbTimer.Elapsed += SendHeartbeat;
            hbTimer.Enabled = true;

            receiveTask = Receive(cancellationTokenSource.Token);
        }
        private async Task Receive(CancellationToken cancelToken)
        {
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    UdpReceiveResult res = await udpClient.ReceiveAsync(cancelToken);

                    OnPacketReceived.Invoke(this,res.Buffer);
                }
            } catch(OperationCanceledException)
            {
            }
        }

        public void Stop()
        {
            hbTimer.Stop();
            cancellationTokenSource.Cancel();

            udpClient.Dispose();
            udpClient = null;
        }
        private void SendHeartbeat(object? sender, ElapsedEventArgs e)
        {
            udpClient.Send(Encoding.UTF8.GetBytes("A"), remoteEndPoint);
        }
    }
}
