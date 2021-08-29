using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Heijden.DNS;

namespace CosineKitty.ZeroConfigWatcher
{
    public class Publisher : IDisposable
    {
        private readonly Dictionary<string, PublishedService> table = new Dictionary<string, PublishedService>();
        private readonly TrafficMonitor trafficMonitor;

        private const uint LongTimeToLive = 4500;
        private const uint ShortTimeToLive = 120;

        public Publisher(TrafficMonitor monitor)
        {
            trafficMonitor = monitor;
        }

        public void Dispose()
        {
            UnpublishAll();
        }

        public bool Publish(PublishedService service)
        {
            // Unpublish any obsolete version of this service name.
            Unpublish(service.LongName);

            lock (table)
            {
                table[service.LongName] = service;
            }

            Advertise(service);

            return true;
        }

        public void Unpublish(string longName)
        {
            PublishedService service;

            lock (table)
            {
                if (table.TryGetValue(longName, out service))
                    table.Remove(longName);
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
            IPAddress serverIpAddress = TrafficMonitor.GetServerIPAddress();
            Response claim = MakeClaimPacket(service, serverIpAddress);
            Response announce = MakeAnnouncePacket(service, serverIpAddress);

            trafficMonitor.Broadcast(claim);
            Thread.Sleep(1000);
            trafficMonitor.Broadcast(announce);
            Thread.Sleep(500);
            trafficMonitor.Broadcast(announce);
            Thread.Sleep(2000);
            trafficMonitor.Broadcast(announce);
            Thread.Sleep(4000);
            trafficMonitor.Broadcast(announce);
        }

        private static string LocalQualify(string shortName)
        {
            if (!shortName.EndsWith(".local."))
                return shortName + ".local.";
            return shortName;
        }

        private void ExpireNow(PublishedService service)
        {
            // Broadcast a notification but with an expiration time of 0 seconds.
        }

        public static Response MakeClaimPacket(PublishedService service, IPAddress serverIpAddress)
        {
            var response = new Response();

            string fqLongName = service.LongName + "." + service.ServiceType;     // "745E1C22FAFD@Living Room._raop._tcp.local."
            string localShortName = LocalQualify(service.ShortName);        // "Living-Room.local."

            // Question: name=[012345678@Walter White._fakeservice._tcp.local.] type=ANY class=IN
            response.Questions.Add(new Question(fqLongName, QType.ANY, QClass.IN));

            // Question: name=[heisenberg.local.] type=ANY class=IN
            response.Questions.Add(new Question(localShortName, QType.ANY, QClass.IN));

            // RR: name=[012345678@Walter White._fakeservice._tcp.local.] type=SRV class=IN TTL=120
            // 0 0 9456 heisenberg.local.
            var srv = new RecordSRV(0, 0, service.Port, localShortName);
            response.Authorities.Add(new RR(fqLongName, ShortTimeToLive, srv));

            // RR: name=[heisenberg.local.] type=A class=IN TTL=120
            // 192.168.1.3
            var arec = new RecordA(serverIpAddress.GetAddressBytes());
            response.Authorities.Add(new RR(localShortName, ShortTimeToLive, arec));

            return response;
        }

        public static Response MakeAnnouncePacket(PublishedService service, IPAddress serverIpAddress)
        {
            var response = new Response();
            response.header.QR = true;  // this is a response, not a question
            response.header.AA = true;  // this is an authoritative answer
            string fqLongName = service.LongName + "." + service.ServiceType;     // "745E1C22FAFD@Living Room._raop._tcp.local."
            string localShortName = LocalQualify(service.ShortName);        // "Living-Room.local."

            // ANSWER[0]:
            // RR: name=[745E1C2300FF@Office._raop._tcp.local.] type=SRV class=32769 TTL=120
            // 0 0 1024 Office.local.
            var srv = new RecordSRV(0, 0, service.Port, localShortName);
            response.Answers.Add(new RR(fqLongName, ShortTimeToLive, srv) { Class = (Class)0x8001 });

            // ANSWER[1]:
            // RR: name=[745E1C2300FF@Office._raop._tcp.local.] type=TXT class=32769 TTL=4500
            // TXT "txtvers=1"
            // TXT "ch=2"
            // ...
            var txt = new RecordTXT(service.TxtRecord);
            response.Answers.Add(new RR(fqLongName, LongTimeToLive, txt) { Class = (Class)0x8001 });

            // ANSWER[2]:
            // RR: name=[_services._dns-sd._udp.local.] type=PTR class=IN TTL=4500
            //_raop._tcp.local.
            var ptr1 = new RecordPTR(service.ServiceType);
            response.Answers.Add(new RR("_services._dns-sd._udp.local.", LongTimeToLive, ptr1));

            // ANSWER[3]:
            // RR: name=[_raop._tcp.local.] type=PTR class=IN TTL=4500
            // 745E1C2300FF@Office._raop._tcp.local.
            var ptr2 = new RecordPTR(fqLongName);
            response.Answers.Add(new RR(service.ServiceType, LongTimeToLive, ptr2));

            // ANSWER[4]:
            // RR: name=[Office.local.] type=A class=32769 TTL=120
            // 192.168.1.7
            var arec = new RecordA(serverIpAddress.GetAddressBytes());
            response.Answers.Add(new RR(localShortName, ShortTimeToLive, arec) { Class = (Class)0x8001 });

            // ANSWER[5]:
            // RR: name=[7.1.168.192.in-addr.arpa.] type=PTR class=32769 TTL=120
            // Office.local.
            string arpaIpName = ArpaIpName(serverIpAddress);    // "7.1.168.192.in-addr.arpa."
            var ptr3 = new RecordPTR(localShortName);
            response.Answers.Add(new RR(arpaIpName, ShortTimeToLive, ptr3) { Class = (Class)0x8001 });

            // ADDITIONAL[0]:
            // RR: name=[745E1C2300FF@Office._raop._tcp.local.] type=NSEC class=32769 TTL=120
            // NSEC 745E1C2300FF@Office._raop._tcp.local. [NSAPPTR, A6]
            var nsec1 = new RecordNSEC(fqLongName, Heijden.DNS.Type.NSAPPTR, Heijden.DNS.Type.A6);
            response.Additionals.Add(new RR(fqLongName, ShortTimeToLive, nsec1) { Class = (Class)0x8001 });

            // ADDITIONAL[1]:
            // RR: name=[Office.local.] type=NSEC class=32769 TTL=120
            // NSEC Office.local. [SOA]
            var nsec2 = new RecordNSEC(localShortName, Heijden.DNS.Type.SOA);
            response.Additionals.Add(new RR(localShortName, ShortTimeToLive, nsec2) { Class = (Class)0x8001 });

            return response;
        }

        private static string ArpaIpName(IPAddress serverIpAddress)
        {
            // 192.168.1.7 ==> "7.1.168.192.in-addr.arpa."
            string rev = string.Join(".", serverIpAddress.GetAddressBytes().Reverse().Select(b => b.ToString()));
            return rev + ".in-addr.arpa.";
        }
    }
}
