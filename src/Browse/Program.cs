using System;
using CosineKitty.ZeroConfigWatcher;
using Heijden.DNS;

namespace Browse
{
    class ConsoleLogger : IDebugLogger
    {
        public void Log(string message)
        {
            Console.WriteLine("# {0}", message);
        }
    }

    class Program
    {
        const string UsageText = @"
USAGE:  Browse _service_type._protocol

For example, to search for AirPlay speakers:

    Browse _raop_.tcp

";

        static int Main(string[] args)
        {
            Browser.Logger = new ConsoleLogger();

            if (args.Length == 1)
            {
                string serviceType = args[0];
                using (var monitor = new TrafficMonitor())
                {
                    using (var browser = new Browser(monitor))
                    {
                        monitor.Start();
                        Console.WriteLine("Listening for traffic on {0} adapter(s).", monitor.ListeningAdapterCount);
                        Console.WriteLine();
                        while (true)
                        {
                            Console.WriteLine("Enter command (q = quit, b = browse, r 'Service Name' = resolve)");
                            Console.Write("> ");
                            Console.Out.Flush();
                            string command = Console.ReadLine();
                            if (command == null)
                                break;

                            command = command.Trim();
                            if (command == "q")
                                break;

                            if (command == "b")
                            {
                                ServiceBrowseResult[] browseList = browser.Browse(serviceType);
                                foreach (ServiceBrowseResult result in browseList)
                                    Console.WriteLine(result);
                                continue;
                            }

                            if (command.StartsWith("r"))
                            {
                                Console.WriteLine("Resolve command not yet implemented.");
                                continue;
                            }

                            Console.WriteLine("ERROR: Unknown command.");
                        }
                    }
                }
                return 0;
            }

            Console.WriteLine(UsageText);
            return 1;
        }
    }
}
