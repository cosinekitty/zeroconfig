using System;
using System.Collections.Generic;
using System.Linq;
using Heijden.DNS;

namespace CosineKitty.ZeroConfigWatcher
{
    public class Publisher : IDisposable
    {
        private readonly Dictionary<string, PublishedService> table = new Dictionary<string, PublishedService>();

        public void Dispose()
        {
            UnpublishAll();
        }

        public bool Publish(
            IPublishClient client,
            string name,
            string serviceType,
            int port,
            Dictionary<string, string> txtRecord)
        {
            // Unpublish any obsolete version of this service name.
            Unpublish(name);

            var service = new PublishedService
            {
                Client = client,
                Name = name,
                ServiceType = serviceType,
                TxtRecord = txtRecord,
            };

            lock (table)
            {
                table[name] = service;
            }

            return true;
        }

        public void Unpublish(string name)
        {
            PublishedService service;

            lock (table)
            {
                if (table.TryGetValue(name, out service))
                    table.Remove(name);
            }

            if (service != null)
                ExpireNow(service);
        }

        public void UnpublishAll()
        {
            PublishedService[] all;
            lock (table)
            {
                all = table.Values.ToArray();
                table.Clear();
            }

            foreach (PublishedService service in all)
                ExpireNow(service);
        }

        private void Advertise(PublishedService service)
        {
            // 
        }

        private void ExpireNow(PublishedService service)
        {
            // Broadcast a notification but with an expiration time of 0 seconds.
        }
    }
}
