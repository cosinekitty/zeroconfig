using System;
using System.IO;
using System.Net;
using CosineKitty.ZeroConfigWatcher;

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
        const string HelpText = @"

q
    Quit this program.

h
    Print this help screen.

S
    Broadcast a request to ask for all available service types.

s
    List service types discovered so far.

B _type._protocol
    Broadcast a request to browse for the given protocol.
    For example, 'B _raop._tcp' asks for all AirPlay speakers
    on the local network to announce their presence.

b _type._protocol
    Display services discovered so far that belong to the specified protocol.
    For example, 'b _raop._tcp' displays all AirPlay speakers
    that have announced their presence since this program started running,
    whether we asked for it (via the 'B' command), or they did so for
    some other reason (self-update or some other program asked).

R <index>
    Broadcast a request to resolve the service with the given index in the
    list printed by the 'b' command.

r <index>
    Display latest resolution of the service with the given index in the list
    printed by the 'b' command.

";

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
                Print("Enter command (q = quit, h = help)");
                Console.Write("> ");
                Console.Out.Flush();
                string command = Console.ReadLine();
                if (command == null)
                    break;

                Browser.Log($"> {command}");

                command = command.Trim();
                if (command == "q")
                    break;

                if (command == "h")
                {
                    Console.WriteLine(HelpText);
                    continue;
                }

                if (command == "S")
                {
                    browser.RequestServiceTypes();
                    continue;
                }

                if (command == "s")
                {
                    foreach (string st in browser.ServiceTypeList())
                        Print(st);
                    continue;
                }

                if (command.StartsWith("B"))
                {
                    string serviceType = command.Substring(1).Trim();
                    browser.RequestBrowse(serviceType);
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

                if (command.StartsWith("r") || command.StartsWith("R"))
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
                            if (command.StartsWith("r"))
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
                            else // "R"
                            {
                                // !!! RequestResolve
                                Print("RequestResolve not implemented. (Do we need it?)");
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
