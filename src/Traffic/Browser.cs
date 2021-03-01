using System;

namespace CosineKitty.ZeroConfigWatcher
{
    public class Browser : IDisposable
    {
        public void Dispose()
        {
        }

        public void SubscribeTo(TrafficMonitor monitor)
        {
            monitor.OnReceive += OnPacket;
        }

        private void OnPacket(object sender, Packet packet)
        {
        }
    }
}
