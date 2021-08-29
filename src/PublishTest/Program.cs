using System;
using System.Collections.Generic;
using CosineKitty.ZeroConfigWatcher;

namespace PublishTest
{
    class Program
    {
        static int Main(string[] args)
        {
            int rc = 1;
            var service = new FakeService();
            using (var monitor = new TrafficMonitor())
            {
                monitor.Start();
                using (var publisher = new Publisher(monitor))
                {
                    var pub = new PublishedService
                    {
                        Client = service,
                        LongName = "012345678@Walter White",
                        ShortName = "heisenberg",
                        ServiceType = "_fakeservice._tcp.local.",
                        Port = 9456,
                        TxtRecord = new Dictionary<string, string>
                        {
                            { "screwdriver", "phillips" },
                            { "size", "13.7" },
                            { "color", "orange" },
                        },
                    };

                    if (publisher.Publish(pub))
                    {
                        Console.WriteLine("Publish succeeded. Press ENTER to unpublish and exit.");
                        Console.ReadLine();
                        rc = 0;
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Publish failed.");
                    }
                }
            }
            return rc;
        }
    }
}
