using System;
using System.Collections.Generic;

namespace CosineKitty.ZeroConfigWatcher
{
    public class ServiceBrowseResult
    {
        public readonly string Name;
        public readonly string ServiceType;

        public ServiceBrowseResult(string name, string serviceType)
        {
            Name = name;
            ServiceType = serviceType;
        }
    }
}
