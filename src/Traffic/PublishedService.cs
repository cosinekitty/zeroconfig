using System;
using System.Collections.Generic;

namespace CosineKitty.ZeroConfigWatcher
{
    internal class PublishedService
    {
        public IPublishClient Client;
        public string Name;
        public string ServiceType;
        public Dictionary<string, string> TxtRecord;
    }
}
