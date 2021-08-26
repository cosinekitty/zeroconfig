using System;
using System.Collections.Generic;
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
            new Test("ReadWrite_TXT", ReadWrite_TXT),
            new Test("ResponseRoundTrip", ResponseRoundTrip),
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
            var packet = new RR(DomainName, TimeToLive, rec);

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
            var packet = new RR(DomainName, TimeToLive, rec);

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
            var packet = new RR(DomainName, TimeToLive, rec);

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
            var packet = new RR(DomainName, TimeToLive, rec);

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
            var packet = new RR(DomainName, TimeToLive, rec);

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

        static int ReadWrite_TXT()
        {
            const string DomainName = "phony.example.com.";
            const uint TimeToLive = 987;

            var txtlist = new string[]
            {
                "sr=44100",
                "ct=true",
                "fv=51.34.55.2",
                "pw=129084712151231256"
            };

            var rec = new RecordTXT(txtlist);
            var packet = new RR(DomainName, TimeToLive, rec);

            RR copy = RoundTrip(packet);
            if (0 != CheckDeserializedPacket(packet, copy))
                return 1;

            // Verify the parsed packet matches the original packet in every detail.
            if (copy.RECORD is RecordTXT cr)
            {
                Debug(cr.ToString());
                if (cr.TXT.Count != txtlist.Length)
                    return Fail($"Reconstituted TXT has {cr.TXT.Count} entries, but expected {txtlist.Length}.");
                for (int i = 0; i < txtlist.Length; ++i)
                    if (txtlist[i] != cr.TXT[i])
                        return Fail($"txtlist[{i}]='{txtlist[i]}', but cr.TXT[{i}]='{cr.TXT[i]}'.");
                return 0;
            }

            return Fail($"Reconstituted record is of incorrect type {copy.RECORD.GetType()}");
        }

        static int ResponseRoundTrip()
        {
            // An actual packet from my AirPlay speaker [Living Room].
            var packet = new byte[]
            {
                0x00, 0x00, 0x84, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x05, 0x05, 0x5f, 0x72, 0x61, // ............._ra
                0x6f, 0x70, 0x04, 0x5f, 0x74, 0x63, 0x70, 0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x00, 0x00, 0x0c, // op._tcp.local...
                0x00, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0x1b, 0x18, 0x37, 0x34, 0x35, 0x45, 0x31, 0x43, 0x32, // .........745E1C2
                0x32, 0x46, 0x41, 0x46, 0x44, 0x40, 0x4c, 0x69, 0x76, 0x69, 0x6e, 0x67, 0x20, 0x52, 0x6f, 0x6f, // 2FAFD@Living Roo
                0x6d, 0xc0, 0x0c, 0xc0, 0x28, 0x00, 0x21, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x14, 0x00, // m...(.!.....x...
                0x00, 0x00, 0x00, 0x04, 0x00, 0x0b, 0x4c, 0x69, 0x76, 0x69, 0x6e, 0x67, 0x2d, 0x52, 0x6f, 0x6f, // ......Living-Roo
                0x6d, 0xc0, 0x17, 0xc0, 0x28, 0x00, 0x10, 0x80, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0x7a, 0x09, // m...(.........z.
                0x74, 0x78, 0x74, 0x76, 0x65, 0x72, 0x73, 0x3d, 0x31, 0x04, 0x63, 0x68, 0x3d, 0x32, 0x06, 0x63, // txtvers=1.ch=2.c
                0x6e, 0x3d, 0x30, 0x2c, 0x31, 0x06, 0x65, 0x74, 0x3d, 0x30, 0x2c, 0x34, 0x08, 0x73, 0x76, 0x3d, // n=0,1.et=0,4.sv=
                0x66, 0x61, 0x6c, 0x73, 0x65, 0x07, 0x64, 0x61, 0x3d, 0x74, 0x72, 0x75, 0x65, 0x08, 0x73, 0x72, // false.da=true.sr
                0x3d, 0x34, 0x34, 0x31, 0x30, 0x30, 0x05, 0x73, 0x73, 0x3d, 0x31, 0x36, 0x08, 0x70, 0x77, 0x3d, // =44100.ss=16.pw=
                0x66, 0x61, 0x6c, 0x73, 0x65, 0x08, 0x76, 0x6e, 0x3d, 0x36, 0x35, 0x35, 0x33, 0x37, 0x06, 0x74, // false.vn=65537.t
                0x70, 0x3d, 0x55, 0x44, 0x50, 0x08, 0x76, 0x73, 0x3d, 0x31, 0x30, 0x33, 0x2e, 0x32, 0x0a, 0x61, // p=UDP.vs=103.2.a
                0x6d, 0x3d, 0x58, 0x57, 0x2d, 0x53, 0x4d, 0x41, 0x34, 0x0f, 0x66, 0x76, 0x3d, 0x73, 0x31, 0x30, // m=XW-SMA4.fv=s10
                0x31, 0x30, 0x2e, 0x31, 0x30, 0x30, 0x30, 0x2e, 0x30, 0xc0, 0x55, 0x00, 0x01, 0x80, 0x01, 0x00, // 10.1000.0.U.....
                0x00, 0x00, 0x78, 0x00, 0x04, 0xc0, 0xa8, 0x01, 0x02, 0xc0, 0x28, 0x00, 0x2f, 0x80, 0x01, 0x00, // ..x.......(./...
                0x00, 0x00, 0x78, 0x00, 0x09, 0xc0, 0x28, 0x00, 0x05, 0x00, 0x00, 0x80, 0x00, 0x40, 0xc0, 0x55, // ..x...(......@.U
                0x00, 0x2f, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x05, 0xc0, 0x55, 0x00, 0x01, 0x40        // ./.....x...U..@
            };

            // Deserialize the packet into a Response object.
            var original = new Response(packet);
            if (0 != CheckLivingRoomAirPlaySpeaker(original))
                return 1;

            // Serialize back to binary data and compare.
            var writer = new RecordWriter();
            original.Write(writer);

            var copyData = writer.GetData();
            // I can't just compare copyData[] to packet[], because original
            // data does not follow the spec for suppressing domain name configuration.
            // So I re-deserialize it and verify it the same way as the original.
            var copy = new Response(copyData);
            if (0 != CheckLivingRoomAirPlaySpeaker(copy))
                return 1;

            return 0;
        }

        static int CheckLivingRoomAirPlaySpeaker(Response response)
        {
            if (response.Questions.Count != 0)
                return Fail($"Expected 0 questions but found {response.Questions.Count}");

            if (response.Answers.Count != 1)
                return Fail($"Expected 1 answer but found {response.Answers.Count}");

            if (response.Authorities.Count != 0)
                return Fail($"Expected 0 authorities but found {response.Authorities.Count}");

            if (response.Additionals.Count != 5)
                return Fail($"Expected 5 additionals but found {response.Additionals.Count}");

            const string FullName = "745E1C22FAFD@Living Room._raop._tcp.local.";
            const string ShortName = "Living-Room.local.";

            // Answer 0

            RR r = response.Answers[0];
            if (r.NAME != "_raop._tcp.local.")
                return Fail("Incorrect Answers[0].NAME");

            if (r.RECORD is RecordPTR ptr)
            {
                if (ptr.PTRDNAME != FullName)
                    return Fail("Answers[0].PTRDNAME is incorrect.");
            }
            else
                return Fail("Answers[0] should have been PTR type.");

            // Additional 0
            r = response.Additionals[0];
            if (r.NAME != FullName)
                return Fail("Additionals[0] has incorrect name.");

            if (r.RECORD is RecordSRV srv)
            {
                string t = srv.ToString();
                if (t != "0 0 1024 Living-Room.local.")
                    return Fail("Additionals[0] has incorrect contents: " + t);
            }
            else
                return Fail("Additionals[0] should have been SRV.");

            // Additional 1
            r = response.Additionals[1];
            if (r.NAME != FullName)
                return Fail("Additionals[1] has incorrect name.");

            if (r.RECORD is RecordTXT txt)
            {
                var correct = new string[]
                {
                    "txtvers=1",
                    "ch=2",
                    "cn=0,1",
                    "et=0,4",
                    "sv=false",
                    "da=true",
                    "sr=44100",
                    "ss=16",
                    "pw=false",
                    "vn=65537",
                    "tp=UDP",
                    "vs=103.2",
                    "am=XW-SMA4",
                    "fv=s1010.1000.0"
                };
                if (txt.TXT.Count != correct.Length)
                    return Fail($"Expected {correct.Length} TXT entries, but found {txt.TXT.Count}.");

                for (int i = 0; i < correct.Length; ++i)
                    if (correct[i] != txt.TXT[i])
                        return Fail($"Expected TXT[{i}]='{correct[i]}' but found '{txt.TXT[i]}'.");
            }
            else
                return Fail("Additionals[1] should have been TXT.");

            // Additional 2
            r = response.Additionals[2];
            if (r.NAME != ShortName)
                return Fail($"Additional[2] name should have been '{ShortName}' but found '{r.NAME}'.");

            if (r.RECORD is RecordA a)
            {
                if (a.ToString() != "192.168.1.2")
                    return Fail("Incorrect IPv4 address in A record.");
            }
            else
                return Fail("Additionals[2] should have been type A.");

            // Additional 3
            r = response.Additionals[3];
            if (r.NAME != FullName)
                return Fail("Additionals[3] has incorrect name.");

            if (r.RECORD is RecordNSEC n3)
            {
                string t = n3.ToString();
                if (t != "NSEC 745E1C22FAFD@Living Room._raop._tcp.local. [NSAPPTR, A6]")
                    return Fail("Additionals[3] has incorrect content: " + t);
            }
            else
                return Fail("Additionals[3] should have been NSEC.");

            // Additional 4
            r = response.Additionals[4];
            if (r.NAME != ShortName)
                return Fail("Additionals[4] has incorrect name.");

            if (r.RECORD is RecordNSEC n4)
            {
                string t = n4.ToString();
                if (t != "NSEC Living-Room.local. [SOA]")
                    return Fail("Additionals[4] has incorrect content: " + t);
            }
            else
                return Fail("Additionals[4] should have been NSEC.");

            return 0;
        }
    }
}
