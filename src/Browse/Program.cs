using System;
using System.IO;
using System.Net;
using CosineKitty.ZeroConfigWatcher;
using Heijden.DNS;

namespace Browse
{
    class FileLogger : IDebugLogger, IDisposable
    {
        private StreamWriter output;

        public FileLogger(string logFileName)
        {
            output = File.CreateText(logFileName);
            Console.WriteLine("Logging to file: {0}", logFileName);
        }

        public void Dispose()
        {
            if (output != null)
            {
                output.Dispose();
                output = null;
            }
        }

        public void Log(string message)
        {
            if (output != null)
            {
                lock (output)
                {
                    DateTime now = DateTime.Now;
                    string stamp = $"{now.Year:0000}-{now.Month:00}-{now.Day:00} {now.Hour:00}:{now.Minute:00}:{now.Second:00}.{now.Millisecond:000}";
                    output.WriteLine("{0}  {1}", stamp, message);
                    output.Flush();
                }
            }
        }
    }

    class Program
    {
        static void Print(string message)
        {
            Browser.Log("# " + message);
            Console.WriteLine(message);
        }

        static int Main(string[] args)
        {
            using var logger = new FileLogger("browser.log");
            Browser.Logger = logger;
            using var monitor = new TrafficMonitor();
            using var browser = new Browser(monitor);
            var browseList = new ServiceBrowseResult[0];
            monitor.Start();
            Print($"Listening for traffic on {monitor.ListeningAdapterCount} adapter(s).");
            Print("");
            while (true)
            {
                Print("Enter command (q = quit, s = service types, b _type._protocol = browse, r <index> = resolve)");
                Console.Write("> ");
                Console.Out.Flush();
                string command = Console.ReadLine();
                if (command == null)
                    break;

                Browser.Log($"> {command}");

                command = command.Trim();
                if (command == "q")
                    break;

                if (command == "s")
                {
                    foreach (string st in browser.ServiceTypeList())
                        Print(st);

                    continue;
                }

                if (command.StartsWith("b"))
                {
                    string serviceType = command.Substring(1).Trim();
                    browseList = browser.Browse(serviceType);
                    for (int index = 0; index < browseList.Length; ++index)
                        Print($"{index}. {browseList[index]}");
                    continue;
                }

                if (command.StartsWith("r"))
                {
                    if (browseList.Length == 0)
                    {
                        Print("You must browse and receive at least one result before you can resolve.");
                    }
                    else
                    {
                        string indexText = command.Substring(1).Trim();
                        if (int.TryParse(indexText, out int index) && (index >= 0) && (index < browseList.Length))
                        {
                            ServiceResolveResult result = browser.Resolve(browseList[index], 5);
                            if (result == null)
                            {
                                Print("RESOLVE FALIURE.");
                            }
                            else
                            {
                                Print($"name = {result.Name}");
                                Print($"host = {result.HostName}");
                                foreach (IPEndPoint ep in result.IpEndpointList)
                                    Print($"endpoint = {ep}");
                                foreach (var kv in result.TxtRecord)
                                    Print($"TXT \"{kv.Key}={kv.Value}\"");
                                Print("");
                            }
                        }
                        else
                        {
                            Print($"ERROR: Invalid browse index: must be 0..{browseList.Length-1}");
                        }
                    }
                    continue;
                }

                Print("ERROR: Unknown command.");
            }
            return 0;
        }
    }
}
