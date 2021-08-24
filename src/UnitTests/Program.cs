﻿using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Heijden.DNS;

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
            new Test("ReadWrite_A", ReadWrite_A),
        };

        static int ReadWrite_A()
        {
            const string DomainName = "phony.example.com.";
            const uint TimeToLive = 3600;

            // Create an "A" record.
            var rec = new RecordA(new byte[] {192, 168, 1, 123});
            var packet = new RR(DomainName, Heijden.DNS.Type.A, Class.IN, TimeToLive, rec);

            // Serialize the "A" record as binary data.
            var writer = new RecordWriter();
            packet.Write(writer);
            byte[] data = writer.GetData();

            // Parse the binary data back as a packet.
            var reader = new RecordReader(data);
            var copy = new RR(reader);

            if (copy.NAME != DomainName)
            {
                Console.WriteLine($"ERROR(ReadWrite_A): copy.NAME [{copy.NAME}] does not match DomainName [{DomainName}].");
                return 1;
            }

            if (copy.Type != Heijden.DNS.Type.A)
            {
                Console.WriteLine($"ERROR(ReadWrite_A): copy.Type has incorrect value {copy.Type}.");
                return 1;
            }

            if (copy.Class != Class.IN)
            {
                Console.WriteLine($"ERROR(ReadWrite_A): copy.Class has incorrect value {copy.Class}.");
                return 1;
            }

            if (copy.TTL != TimeToLive)
            {
                Console.WriteLine($"ERROR(ReadWrite_A): copy.TTL has incorrect value {copy.TTL}.");
                return 1;
            }

            if (copy.RECORD == null)
            {
                Console.WriteLine($"ERROR(ReadWrite_A): copy.RECORD is null.");
                return 1;
            }

            // Verify the parsed packet matches the original packet in every detail.
            if (copy.RECORD is RecordA cr)
            {
                string ip1 = rec.ToString();
                string ip2 = cr.ToString();
                if (ip1 != ip2)
                {
                    Console.WriteLine($"ERROR(ReadWrite_A): ip1={ip1} does not match ip2={ip2}");
                    return 1;
                }
                return 0;
            }

            Console.WriteLine($"ERROR(ReadWrite_A): Reconstituted record is of incorrect type {copy.RECORD.GetType()}");
            return 1;
        }
    }
}
