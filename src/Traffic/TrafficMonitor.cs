using System;
using System.Collections.Generic;
using System.Net;
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

                udpClient4 = new UdpClient(5353, AddressFamily.InterNetwork);
                udpClient4.JoinMulticastGroup(IPAddress.Parse("224.0.0.251"));
                udpClient4.BeginReceive(ReceiveCallback, null);
            }
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
