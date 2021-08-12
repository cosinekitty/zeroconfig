using System;
using System.Collections.Generic;

namespace CosineKitty.ZeroConfigWatcher
{
    public class Publisher : IDisposable
    {
        public void Dispose()
        {
        }

        public bool Publish(
            IPublishClient client,
            string name,
            string serviceType,
            int port,
            Dictionary<string, string> txtRecord)
        {
            return false;       // failure : not yet implemented
        }

        public void Unpublish(string name)
        {

        }
    }
}
