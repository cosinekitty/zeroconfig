using System;
using System.Collections.Generic;
using System.Diagnostics;
using Heijden.DNS;

namespace CosineKitty.ZeroConfigWatcher
{
    public class Browser : IDisposable
    {
        private readonly TrafficMonitor monitor;
        private readonly Dictionary<string, ServiceCollection> serviceRoot = new ();

        public static IDebugLogger Logger;

        public static void Log(string message)
        {
            if (Logger != null)
                Logger.Log(message);
        }

        public Browser(TrafficMonitor monitor)
        {
            this.monitor = monitor;
            monitor.OnReceive += OnPacket;
        }

        public void Dispose()
        {
            monitor.OnReceive -= OnPacket;
        }

        public ServiceBrowseResult[] Browse(string serviceType)
        {
            var list = new List<ServiceBrowseResult>();
            return list.ToArray();
        }

        private static string FirstToken(string text)
        {
            // text = "iTunes_Ctrl_DF6D11C544851FEC._dacp._tcp.local."
            // return "iTunes_Ctrl_DF6D11C544851FEC"
            if (text != null)
            {
                int firstPeriodIndex = text.IndexOf('.');
                if (firstPeriodIndex > 0)
                    return text.Substring(0, firstPeriodIndex);
            }
            return null;
        }

        private static string RemainingText(string text)
        {
            // text = "iTunes_Ctrl_DF6D11C544851FEC._dacp._tcp.local."
            // return "_dacp._tcp.local."
            if (text != null)
            {
                int firstPeriodIndex = text.IndexOf('.');
                if (firstPeriodIndex > 0)
                    return text.Substring(firstPeriodIndex + 1);
            }
            return null;
        }

        private void OnPacket(object sender, Packet packet)
        {
            var response = new Response(packet.Data);

            /*
            foreach (Question q in response.Questions)
            {
            }
            */

            foreach (AnswerRR a in response.Answers)
            {
                if (a.Class == Heijden.DNS.Class.IN && a.RECORD is RecordPTR ptr)
                {
                    string serviceType = a.NAME;
                    string name = FirstToken(ptr.PTRDNAME);
                    if (name != null && serviceType != null)
                    {
                        Browser.Log($"OnPacket: name=[{name}], serviceType=[{serviceType}]");
                    }
                }
            }

            foreach (AuthorityRR a in response.Authorities)
            {
                if (a.Class == Heijden.DNS.Class.IN)
                {
                    if (a.Type == Heijden.DNS.Type.SRV && a.RECORD is RecordSRV srv)
                    {
                        // FIXFIXFIX: handle name conflicts discovered by existing "defenders" with the same name.
                        string name = FirstToken(a.NAME);
                        string serviceType = RemainingText(a.NAME);
                        if (name != null && serviceType != null)
                        {
                            lock (serviceRoot)
                            {
                                if (serviceRoot.TryGetValue(serviceType, out ServiceCollection collection))
                                {
                                    if (!collection.ServiceTable.TryGetValue(name, out ServiceInfo info))
                                        collection.ServiceTable.Add(name, info = new ServiceInfo());

                                    info.UpdateSrv(srv);
                                }
                            }
                        }
                    }
                }
            }

            /*

            foreach (AdditionalRR a in response.Additionals)
            {
            }

            */
        }
    }

    internal class ServiceCollection
    {
        public readonly Dictionary<string, ServiceInfo> ServiceTable = new();
    }

    internal class ServiceInfo
    {
        public ServiceFact<RecordSRV> srv;

        public void UpdateSrv(RecordSRV record)
        {
            if (srv == null || record.PRIORITY < srv.Record.PRIORITY)
            {
                srv = new ServiceFact<RecordSRV>(record);
                Browser.Log($"UpdateSrv: record = {record}");
            }
        }
    }

    internal class ServiceFact<RecordType> where RecordType : Record
    {
        public Stopwatch Elapsed = Stopwatch.StartNew();
        public RecordType Record;

        public ServiceFact(RecordType record)
        {
            Record = record;
        }
    }
}
