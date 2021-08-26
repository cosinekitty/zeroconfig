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

            Advertise(service);

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
            // When someone sends a question PTR for [_raop._tcp.local.],
            // We should respond with something like this:
            //
            // AnswerRR: name=[_raop._tcp.local.] type=PTR class=IN TTL=4500
            // 745E1C22FAFD@Living Room._raop._tcp.local.
            //
            // AdditionalRR: name=[745E1C22FAFD@Living Room._raop._tcp.local.] type=SRV class=32769 TTL=120
            // 0 0 1024 Living-Room.local.
            //
            // AdditionalRR: name=[745E1C22FAFD@Living Room._raop._tcp.local.] type=TXT class=32769 TTL=4500
            // TXT "txtvers=1"
            // TXT "ch=2"
            // TXT "cn=0,1"
            // TXT "et=0,4"
            // TXT "sv=false"
            // TXT "da=true"
            // TXT "sr=44100"
            // TXT "ss=16"
            // TXT "pw=false"
            // TXT "vn=65537"
            // TXT "tp=UDP"
            // TXT "vs=103.2"
            // TXT "am=XW-SMA4"
            // TXT "fv=s1010.1000.0"
            //
            // AdditionalRR: name=[Living-Room.local.] type=A class=32769 TTL=120
            // 192.168.1.2
            //
            // AdditionalRR: name=[745E1C22FAFD@Living Room._raop._tcp.local.] type=NSEC class=32769 TTL=120
            // NSEC 745E1C22FAFD@Living Room._raop._tcp.local. [NSAPPTR, A6]
            //
            // AdditionalRR: name=[Living-Room.local.] type=NSEC class=32769 TTL=120
            // NSEC Living-Room.local. [SOA]

        }

        private void ExpireNow(PublishedService service)
        {
            // Broadcast a notification but with an expiration time of 0 seconds.
        }
    }
}
