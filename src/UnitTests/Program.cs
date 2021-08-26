using System;
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
            new Test("Compression", Compression),
            new Test("ReadWrite_A", ReadWrite_A),
            new Test("ReadWrite_AAAA", ReadWrite_AAAA),
            new Test("ReadWrite_NSEC", ReadWrite_NSEC),
            new Test("ReadWrite_PTR", ReadWrite_PTR),
            new Test("ReadWrite_SRV", ReadWrite_SRV),
        };

        static int Fail(string message)
        {
            Console.WriteLine("ERROR: {0}", message);
            return 1;
        }

        static int Compression()
        {
            // Exercise domain name compression.
            // Verify that the entire common tail part of a domain name
            // is aliased as a pointer to an earlier part.
            // https://datatracker.ietf.org/doc/html/rfc1035#section-4.1.4

            var writer = new RecordWriter();
            writer.WriteUint16(0x1234);     // just filler data, so offsets are nonzero
            int firstIndex = writer.Length;
            if (firstIndex != 2)
                return Fail($"Incorrect firstIndex={firstIndex}; expected 2.");

            writer.WriteDomainNameCompressed("bunny.example.com.");
            int secondIndex = writer.Length;
            int firstNameLength = secondIndex - firstIndex;
            // The first name length should be
            // 1 + 5 "bunny"
            // 1 + 7 "example"
            // 1 + 3 "com"
            // 1 (terminator)
            // for a total of 19 bytes.
            if (firstNameLength != 19)
                return Fail($"Incorrect firstNameLength={firstNameLength}; expected 19.");

            writer.WriteDomainNameCompressed("fluffy.bunny.example.com.");
            int compressedNameLength = writer.Length - secondIndex;

            // The compressed name format should be exactly this:
            byte[] expected = new byte[]
            {
                6,          // the length of the string "fluffy"
                (byte)'f',
                (byte)'l',
                (byte)'u',
                (byte)'f',
                (byte)'f',
                (byte)'y',

                0xc0,       // pointer escape 0xc0, and high byte of pointer is 0x00
                0x02        // low byte of pointer is 0x02
            };

            if (compressedNameLength != expected.Length)
                return Fail($"Expected compressed name length to be {expected.Length} bytes, but found {compressedNameLength}.");

            // Verify each byte is as we expect.
            for (int i = 0; i < expected.Length; ++i)
            {
                int k = secondIndex + i;
                byte actual = writer.GetByte(k);
                if (expected[i] != actual)
                    return Fail($"Expected byte value 0x{expected[i]:x2} at offset {k}, but found 0x{actual:x2}.");
            }

            return 0;
        }

        static RR RoundTrip(RR packet)
        {
            // Serialize the record as binary data.
            var writer = new RecordWriter();
            packet.Write(writer);
            byte[] data = writer.GetData();

            // Parse the binary data back as a packet.
            var reader = new RecordReader(data);
            var copy = new RR(reader);
            return copy;
        }

        static int CheckDeserializedPacket(RR packet, RR copy)
        {
            if (copy.NAME != packet.NAME)
                return Fail($"copy.NAME=[{copy.NAME}] does not match packet.NAME=[{packet.NAME}].");

            if (copy.Type != packet.Type)
                return Fail($"copy.Type has incorrect value {copy.Type}.");

            if (copy.Class != packet.Class)
                return Fail($"copy.Class has incorrect value {copy.Class}.");

            if (copy.TTL != packet.TTL)
                return Fail($"copy.TTL has incorrect value {copy.TTL}.");

            if (copy.RECORD == null)
                return Fail("copy.RECORD is null.");

            return 0;
        }

        static int ReadWrite_A()
        {
            const string DomainName = "phony.example.com.";
            const uint TimeToLive = 3600;

            // Create an "A" record for the IPv4 address.
            var rec = new RecordA(new byte[] {192, 168, 1, 123});
            var packet = new RR(DomainName, Heijden.DNS.Type.A, Class.IN, TimeToLive, rec);

            RR copy = RoundTrip(packet);
            if (0 != CheckDeserializedPacket(packet, copy))
                return 1;

            // Verify the parsed packet matches the original packet in every detail.
            if (copy.RECORD is RecordA cr)
            {
                string ip1 = rec.ToString();
                string ip2 = cr.ToString();
                if (ip1 != ip2)
                    return Fail($"ip1={ip1} does not match ip2={ip2}");
                return 0;
            }

            return Fail($"Reconstituted record is of incorrect type {copy.RECORD.GetType()}");
        }

        static int ReadWrite_AAAA()
        {
            const string DomainName = "bigbadbob.example.com.";
            const uint TimeToLive = 1234;

            // Create an "AAAA" record for the IPv6 address.
            var rec = new RecordAAAA(new UInt16[] {0x1234, 0x5678, 0xabcd, 0x9876, 0x4444, 0x5555, 0x6666, 0x7777});
            var packet = new RR(DomainName, Heijden.DNS.Type.AAAA, Class.IN, TimeToLive, rec);

            RR copy = RoundTrip(packet);
            if (0 != CheckDeserializedPacket(packet, copy))
                return 1;

            // Verify the parsed packet matches the original packet in every detail.
            if (copy.RECORD is RecordAAAA cr)
            {
                string ip1 = rec.ToString();
                string ip2 = cr.ToString();
                if (ip1 != ip2)
                    return Fail($"ip1={ip1} does not match ip2={ip2}");
                return 0;
            }

            return Fail($"Reconstituted record is of incorrect type {copy.RECORD.GetType()}");
        }

        static int ReadWrite_PTR()
        {
            const string DomainName = "phony.example.com.";
            const uint TimeToLive = 3600;

            // Create a "PTR" record.
            const string PtrName = "balogna.example.com.";
            var rec = new RecordPTR(PtrName);
            var packet = new RR(DomainName, Heijden.DNS.Type.PTR, Class.IN, TimeToLive, rec);

            RR copy = RoundTrip(packet);
            if (0 != CheckDeserializedPacket(packet, copy))
                return 1;

            // Verify the parsed packet matches the original packet in every detail.
            if (copy.RECORD is RecordPTR cr)
            {
                if (cr.PTRDNAME != PtrName)
                    return Fail($"cr.PTRDNAME=[{cr.PTRDNAME}] does not match PtrName=[{PtrName}].");
                return 0;
            }

            return Fail($"Reconstituted record is of incorrect type {copy.RECORD.GetType()}");
        }

        static int ReadWrite_NSEC()
        {
            const string DomainName = "phony.example.com.";
            const uint TimeToLive = 987;

            const string NextDomainName = "zebra.example.com.";
            var rec = new RecordNSEC(NextDomainName, Heijden.DNS.Type.PTR, Heijden.DNS.Type.TXT, Heijden.DNS.Type.A);
            var packet = new RR(DomainName, Heijden.DNS.Type.NSEC, Class.IN, TimeToLive, rec);

            RR copy = RoundTrip(packet);
            if (0 != CheckDeserializedPacket(packet, copy))
                return 1;

            // Verify the parsed packet matches the original packet in every detail.
            if (copy.RECORD is RecordNSEC cr)
            {
                string origText = rec.ToString();
                string copyText = cr.ToString();
                if (origText != copyText)
                    return Fail($"origText=[{origText}] != copyText=[{copyText}].");
                return 0;
            }

            return Fail($"Reconstituted record is of incorrect type {copy.RECORD.GetType()}");
        }

        static int ReadWrite_SRV()
        {
            const string DomainName = "phony.example.com.";
            const uint TimeToLive = 987;

            var rec = new RecordSRV(12345, 9876, 8153, "totally.bogus.example.com.");
            var packet = new RR(DomainName, Heijden.DNS.Type.SRV, Class.IN, TimeToLive, rec);

            RR copy = RoundTrip(packet);
            if (0 != CheckDeserializedPacket(packet, copy))
                return 1;

            // Verify the parsed packet matches the original packet in every detail.
            if (copy.RECORD is RecordSRV cr)
            {
                string origText = rec.ToString();
                string copyText = cr.ToString();
                if (origText != copyText)
                    return Fail($"origText=[{origText}] != copyText=[{copyText}].");
                return 0;
            }

            return Fail($"Reconstituted record is of incorrect type {copy.RECORD.GetType()}");
        }
    }
}
