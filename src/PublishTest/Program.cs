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
            using (var publisher = new Publisher())
            {
                var txtRecord = new Dictionary<string, string>
                {
                    { "screwdriver", "phillips" },
                    { "size", "13.7" },
                    { "color", "orange" },
                };

                if (publisher.Publish(service, "heisenberg", "_fakeservice._tcp", 9456, txtRecord))
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
            return rc;
        }
    }
}
