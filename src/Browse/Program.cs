using System;
using CosineKitty.ZeroConfigWatcher;

namespace Browse
{
    class Program
    {
        const string UsageText = @"
USAGE:  Browse _service_type._protocol

For example, to search for AirPlay speakers:

    Browse _raop_.tcp

";

        static int Main(string[] args)
        {
            if (args.Length == 1)
            {
                string serviceType = args[0];
                using (var browser = new Browser())
                {
                    using (var monitor = new TrafficMonitor())
                    {
                        browser.SubscribeTo(monitor);
                        monitor.Start();
                        Console.WriteLine("Listening for traffic on {0} adapter(s). Press ENTER to quit.", monitor.ListeningAdapterCount);
                        Console.WriteLine();
                        Console.ReadLine();
                    }
                }
                return 0;
            }

            Console.WriteLine(UsageText);
            return 1;
        }
    }
}
