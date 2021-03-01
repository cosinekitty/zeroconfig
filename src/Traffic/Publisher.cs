using System;
using System.Collections.Generic;

namespace CosineKitty.ZeroConfigWatcher
{
    public class Publisher : IDisposable
    {
        private IPublishClient client;

        public void Start(IPublishClient client)
        {
            this.client = client;
        }

        public void Dispose()
        {
        }

        public bool Publish(string serviceType, string name, int port, Dictionary<string, string> txtRecord)
        {
            return false;       // failure : not yet implemented
        }
    }
}
