using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace CosineKitty.ZeroConfigWatcher
{
    public class TrafficMonitor : IDisposable
    {
        private readonly object mutex = new object();
        private Thread queueWorkerThread;
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private bool closed;
        private UdpClient udpClient4;   // IPv4 client
        private readonly Queue<Packet> inQueue = new();
        // FIXFIXFIX: add IPv6 client support.

        public event TrafficEventHandler OnReceive;

        public void Start()
        {
            lock (mutex)
            {
                if (closed)
                    throw new Exception("Cannot restart a TrafficMonitor after it has been disposed.");

                if (udpClient4 != null)
                    throw new Exception("TrafficMonitor has already been started.");

                queueWorkerThread = new Thread(QueueWorkerThread)
                {
                    IsBackground = true,
                    Name = "ZeroConfig TrafficMonitor queue worker",
                };
                queueWorkerThread.Start();

                udpClient4 = MakeClient();
                udpClient4.BeginReceive(ReceiveCallback, null);
            }
        }

        private UdpClient MakeClient()
        {
            // https://stackoverflow.com/questions/2192548/specifying-what-network-interface-an-udp-multicast-should-go-to-in-net
            // https://windowsasusual.blogspot.com/2013/01/socket-option-multicast-interface.html

            var udpClient = new UdpClient();
            Socket socket = udpClient.Client;

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in nics)
            {
                if (!adapter.SupportsMulticast)
                    continue; // multicast is meaningless for this type of connection

                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                if (OperationalStatus.Up != adapter.OperationalStatus)
                    continue; // this adapter is off or not connected

                IPv4InterfaceProperties p = adapter.GetIPProperties().GetIPv4Properties();
                if (null == p)
                    continue; // IPv4 is not configured on this adapter

                int index;
                try
                {
                    index = p.Index;
                }
                catch (Exception)
                {
                    continue;   // skip adapters without indexes
                }

                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, (int)IPAddress.HostToNetworkOrder(index));
                udpClient.ExclusiveAddressUse = false;
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                var localEp = new IPEndPoint(IPAddress.Any, 5353);
                socket.Bind(localEp);
                var multicastAddress = IPAddress.Parse("224.0.0.251");
                var multOpt = new MulticastOption(multicastAddress, index);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, multOpt);
                return udpClient;
            }
            throw new Exception("Could not find suitable adapter for multicast.");
        }

        public void Dispose()
        {
            lock (mutex)
            {
                if (closed)
                    return;     // ignore redundant calls

                closed = true;
                if (udpClient4 != null)
                {
                    udpClient4.Dispose();
                    udpClient4 = null;
                }
            }
            signal.Set();   // wake up worker thread so it notices we are closing; it then exits immediately.
            queueWorkerThread.Join();
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            lock (mutex)
            {
                if (closed)
                    return;

                IPEndPoint remoteEndPoint = null;
                byte[] data = udpClient4.EndReceive(result, ref remoteEndPoint);
                var packet = new Packet { Data = data, RemoteEndPoint = remoteEndPoint };
                inQueue.Enqueue(packet);
                udpClient4.BeginReceive(ReceiveCallback, null);
            }
            signal.Set();
        }

        private void QueueWorkerThread()
        {
            while (true)
            {
                signal.WaitOne();
                while (true)
                {
                    Packet packet;
                    lock (mutex)
                    {
                        if (closed)
                            return;

                        if (inQueue.Count == 0)
                            break;

                        packet = inQueue.Dequeue();
                    }

                    if (OnReceive != null)
                        OnReceive(this, packet);
                }
            }
        }
    }
}
