using System;
using System.Net;

namespace CosineKitty.ZeroConfigWatcher
{
    public delegate void TrafficEventHandler(object sender, Packet e);

    public class Packet
    {
        public DateTime UtcArrival;
        public IPEndPoint RemoteEndPoint;
        public byte[] Data;
    }
}