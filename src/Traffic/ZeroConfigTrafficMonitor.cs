using System;

namespace CosineKitty.ZeroConfigWatcher
{
    public class TrafficMonitor : IDisposable
    {
        public event TrafficEventHandler OnReceive;

        public void Start()
        {
        }

        public void Dispose()
        {
        }
    }
}
