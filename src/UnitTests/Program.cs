using System;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace CosineKitty.ZeroConfigWatcher.UnitTests
{
    class Program
    {
        static bool Verbose;

        static void Debug(string text)
        {
            if (Verbose)
                Console.WriteLine(text);
        }

        static int Main(string[] args)
        {
            // Force use of "." for the decimal mark, regardless of local culture settings.
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            if (args.Length > 0 && args[0] == "-v")
            {
                Verbose = true;
                args = args.Skip(1).ToArray();
            }

            if (args.Length == 1)
            {
                string name = args[0];
                if (name == "all")
                {
                    Console.WriteLine("zctest: BEGIN");
                    foreach (Test t in UnitTests)
                        if (0 != t.Run())
                            return 1;
                    Console.WriteLine("zctest: SUCCESS");
                    return 0;
                }

                foreach (Test t in UnitTests)
                    if (t.Name == name)
                        return t.Run();
            }

            Console.WriteLine("zctest: Invalid command line parameters.");
            return 1;
        }

        struct Test
        {
            public readonly string Name;
            private readonly Func<int> TestFunc;

            public Test(string name, Func<int> testFunc)
            {
                this.Name = name;
                this.TestFunc = testFunc;
            }

            public int Run()
            {
                int rc = TestFunc();
                string result = (rc == 0) ? "PASS" : "FAIL";
                Console.WriteLine($"zctest: {result} {Name}");
                return rc;
            }
        }

        static Test[] UnitTests = new Test[]
        {
            new Test("DoNothing", DoNothing),
        };

        static int DoNothing()
        {
            return 0;
        }
    }
}
