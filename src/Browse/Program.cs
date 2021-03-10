using System;
using System.Net;
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
                        var browseList = new ServiceBrowseResult[0];
                        monitor.Start();
                        Console.WriteLine("Listening for traffic on {0} adapter(s).", monitor.ListeningAdapterCount);
                        Console.WriteLine();
                        while (true)
                        {
                            Console.WriteLine("Enter command (q = quit, b = browse, r <index> = resolve)");
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
                                browseList = browser.Browse(serviceType);
                                foreach (ServiceBrowseResult result in browseList)
                                    Console.WriteLine(result);
                                continue;
                            }

                            if (command.StartsWith("r"))
                            {
                                if (browseList.Length == 0)
                                {
                                    Console.WriteLine("You must browse and receive at least one result before you can resolve.");
                                }
                                else
                                {
                                    string indexText = command.Substring(1).Trim();
                                    if (int.TryParse(indexText, out int index) && (index >= 0) && (index < browseList.Length))
                                    {
                                        ServiceResolveResult result = browser.Resolve(browseList[index], 5);
                                        if (result == null)
                                        {
                                            Console.WriteLine("RESOLVE FALIURE.");
                                        }
                                        else
                                        {
                                            Console.WriteLine("name = {0}", result.Name);
                                            Console.WriteLine("host = {0}", result.HostName);
                                            foreach (IPEndPoint ep in result.IpEndpointList)
                                                Console.WriteLine("endpoint = {0}", ep);
                                            foreach (var kv in result.TxtRecord)
                                                Console.WriteLine("TXT \"{0}={1}\"", kv.Key, kv.Value);
                                            Console.WriteLine();
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("ERROR: Invalid browse index: must be 0..{0}", browseList.Length-1);
                                    }
                                }
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
