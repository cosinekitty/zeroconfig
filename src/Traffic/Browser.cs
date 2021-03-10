using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using Heijden.DNS;

namespace CosineKitty.ZeroConfigWatcher
{
    public class Browser : IDisposable
    {
        private readonly TrafficMonitor monitor;
        private readonly Dictionary<string, ServiceCollection> serviceRoot = new();
        private System.Timers.Timer expirationTimer;

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

            expirationTimer = new System.Timers.Timer();
            expirationTimer.Interval = 1000.0;
            expirationTimer.Elapsed += OnExpirationTimer;
            expirationTimer.AutoReset = true;
            expirationTimer.Enabled = true;
        }

        private void OnExpirationTimer(object sender, ElapsedEventArgs e)
        {
            lock (serviceRoot)
            {
                foreach (var pair in serviceRoot)
                {
                    string serviceType = pair.Key;
                    ServiceCollection collection = pair.Value;

                    string[] obsoleteNames = collection.ServiceTable
                        .Where(kv => kv.Value.IsExpired())
                        .Select(kv => kv.Key)
                        .ToArray();

                    foreach (string name in obsoleteNames)
                    {
                        Browser.Log($"OnExpirationTimer: deleting [{name}] from [{serviceType}]");
                        collection.ServiceTable.Remove(name);
                    }
                }
            }
        }

        public void Dispose()
        {
            expirationTimer.Enabled = false;
            monitor.OnReceive -= OnPacket;
        }

        public ServiceBrowseResult[] Browse(string serviceType)
        {
            if (!serviceType.EndsWith(".local."))
                serviceType += ".local.";

            // FIXFIXFIX: active browsing: send out packets to query for this service type, if needed.

            var list = new List<ServiceBrowseResult>();
            lock (serviceRoot)
            {
                if (serviceRoot.TryGetValue(serviceType, out ServiceCollection collection))
                {
                    foreach (var kv in collection.ServiceTable)
                    {
                        string name = kv.Key;
                        ServiceInfo info = kv.Value;
                        if (info.ptr != null)
                            list.Add(new ServiceBrowseResult(name, serviceType));
                    }
                }
            }
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
                if (a.RECORD is RecordPTR ptr)
                {
                    string serviceType = a.NAME;
                    string name = FirstToken(ptr.PTRDNAME);
                    if (name != null && serviceType != null)
                    {
                        Browser.Log($"OnPacket: serviceType=[{serviceType}], name=[{name}]");
                        lock (serviceRoot)
                        {
                            ServiceCollection collection = LazyCreateServiceType(serviceType);
                            ServiceInfo info = collection.LazyCreate(name);
                            info.UpdatePtr(ptr);
                        }
                    }
                }
            }

            /*

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


            foreach (AdditionalRR a in response.Additionals)
            {
            }

            */
        }

        private ServiceCollection LazyCreateServiceType(string serviceType)
        {
            if (!serviceRoot.TryGetValue(serviceType, out ServiceCollection collection))
                serviceRoot.Add(serviceType, collection = new ServiceCollection());

            return collection;
        }
    }

    internal class ServiceCollection
    {
        public readonly Dictionary<string, ServiceInfo> ServiceTable = new();

        public ServiceInfo LazyCreate(string name)
        {
            if (!ServiceTable.TryGetValue(name, out ServiceInfo info))
                ServiceTable.Add(name, info = new ServiceInfo());

            return info;
        }
    }

    internal class ServiceInfo
    {
        public ServiceFact<RecordSRV> srv;
        public ServiceFact<RecordPTR> ptr;

        public void UpdateSrv(RecordSRV record)
        {
            if (srv == null || record.PRIORITY < srv.Record.PRIORITY)
            {
                srv = new ServiceFact<RecordSRV>(record);
                Browser.Log($"UpdateSrv: record = {record}");
            }
        }

        public void UpdatePtr(RecordPTR record)
        {
            ptr = new ServiceFact<RecordPTR>(record);
        }

        public bool IsExpired()
        {
            return (ptr != null) && (ptr.RemainingLifeInSeconds() < 0.0);
        }
    }

    internal class ServiceFact<RecordType> where RecordType : Record
    {
        public Stopwatch Elapsed = Stopwatch.StartNew();
        public RecordType Record;

        public ServiceFact(RecordType record)
        {
            if (record == null)
                throw new ArgumentException("Null record not allowed", nameof(record));
            Record = record;
        }

        public double RemainingLifeInSeconds()
        {
            return (double)Record.RR.TTL - Elapsed.Elapsed.TotalSeconds;
        }
    }
}
