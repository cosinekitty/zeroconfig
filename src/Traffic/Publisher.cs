using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
            Response response = MakeAnnouncePacket(service, serverIpAddress);
            trafficMonitor.Broadcast(response);
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
            // Question: name=[745E1C2300FF@Office._raop._tcp.local.] type=ANY class=IN
            // Question: name=[Office.local.] type=ANY class=IN
            //
            // RR: name=[745E1C2300FF@Office._raop._tcp.local.] type=SRV class=IN TTL=120
            // 0 0 1024 Office.local.
            //
            // RR: name=[Office.local.] type=A class=IN TTL=120
            // 192.168.1.7

            var response = new Response();

            string fqLongName = service.LongName + service.ServiceType;     // "745E1C22FAFD@Living Room._raop._tcp.local."
            string localShortName = LocalQualify(service.ShortName);        // "Living-Room.local."

            response.Questions.Add(new Question(fqLongName, QType.ANY, QClass.IN));
            response.Questions.Add(new Question(localShortName, QType.ANY, QClass.IN));

            var srv = new RecordSRV(0, 0, service.Port, service.ShortName);
            response.Authorities.Add(new RR(fqLongName, ShortTimeToLive, srv));

            var arec = new RecordA(serverIpAddress.GetAddressBytes());
            response.Authorities.Add(new RR(localShortName, ShortTimeToLive, arec));

            return response;
        }

        public static Response MakeAnnouncePacket(PublishedService service, IPAddress serverIpAddress)
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

            var response = new Response();
            string fqLongName = service.LongName + service.ServiceType;     // "745E1C22FAFD@Living Room._raop._tcp.local."
            string localShortName = LocalQualify(service.ShortName);        // "Living-Room.local."

            var ptr = new RecordPTR(fqLongName);
            response.Answers.Add(new RR(service.ServiceType, LongTimeToLive, ptr));

            var srv = new RecordSRV(0, 0, service.Port, service.ShortName);
            response.Additionals.Add(new RR(fqLongName, ShortTimeToLive, srv));

            var txt = new RecordTXT(service.TxtRecord);
            response.Additionals.Add(new RR(fqLongName, LongTimeToLive, txt));

            var arec = new RecordA(serverIpAddress.GetAddressBytes());
            response.Additionals.Add(new RR(localShortName, ShortTimeToLive, arec));

            var nsec1 = new RecordNSEC(fqLongName, Heijden.DNS.Type.NSAPPTR, Heijden.DNS.Type.A6);
            response.Additionals.Add(new RR(fqLongName, ShortTimeToLive, nsec1));

            var nsec2 = new RecordNSEC(service.ShortName, Heijden.DNS.Type.SOA);
            response.Additionals.Add(new RR(localShortName, ShortTimeToLive, nsec2));

            return response;
        }
    }
}
