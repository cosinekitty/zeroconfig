using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
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

        static void HexDump(byte[] data)
        {
            Console.WriteLine("       0  1  2  3  4  5  6  7    8  9  a  b  c  d  e  f");
            Console.WriteLine("      -- -- -- -- -- -- -- --   -- -- -- -- -- -- -- --");
            for (int row = 0; row < data.Length; row += 0x10)
            {
                Console.Write("{0} ", row.ToString("x4"));
                for (int col = 0; col < 0x10; ++col)
                {
                    int ofs = row + col;
                    if (col == 8)
                        Console.Write("  ");
                    if (ofs < data.Length)
                        Console.Write(" {0}", data[ofs].ToString("x2"));
                    else
                        Console.Write("   ");
                }

                Console.Write("  ");

                for (int col = 0; col < 0x10; ++col)
                {
                    int ofs = row + col;
                    if (ofs >= data.Length)
                        break;
                    if (data[ofs] >= 0x20 && data[ofs] <= 0x7f)
                        Console.Write("{0}", (char)data[ofs]);
                    else
                        Console.Write(".");
                }

                Console.WriteLine();
            }
        }

        static byte[] ParseHexDump(string hexdump)
        {
            // Parses hex dump from CasaTunes function RecordReader.cs ! LogPacket(string caller).
            // This is not the same format used by the HexDump() in this file.
            // 0000: 00 00 84 00 00 00 00 05 00 00 00 00 0e 5f 73 75   ............._su
            var list = new List<byte>();
            string[] lines = hexdump.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                int col_index = line.IndexOf(": ");
                if (col_index < 0)
                    break;
                string s = line.Substring(col_index + 2);
                int sep_index = s.IndexOf("   ");
                if (sep_index < 0)
                    break;
                s = s.Substring(0, sep_index);
                string[] token = s.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string t in token)
                {
                    byte x = byte.Parse(t, NumberStyles.HexNumber);
                    list.Add(x);
                }
            }
            return list.ToArray();
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
            new Test("Weird_NSEC", Weird_NSEC),
            new Test("ReadWrite_PTR", ReadWrite_PTR),
            new Test("ReadWrite_SRV", ReadWrite_SRV),
            new Test("ReadWrite_TXT", ReadWrite_TXT),
            new Test("MessageRoundTrip", MessageRoundTrip),
            new Test("Claim", Claim),
            new Test("Announce", Announce),
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
            var reader = new RecordReader(data, true);
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

        static int TestHexDump(int id, string hexdump)
        {
            byte[] data = ParseHexDump(hexdump);
            var message = new Message(data, false);
            foreach (RR rr in message.Answers)
                Console.WriteLine("TestHexDump({0}): rr = {1}", id, rr);
            return 0;
        }

        static int Weird_NSEC()
        {
            int rc = 0;
            int id = 0;

            if (rc == 0) rc = TestHexDump(++id, @"
0000: 00 00 84 00 00 00 00 05 00 00 00 00 0e 5f 73 75   ............._su
0010: 65 73 38 30 30 64 65 76 69 63 65 04 5f 74 63 70   es800device._tcp
0020: 05 6c 6f 63 61 6c 00 00 0c 00 01 00 00 11 94 00   .local..........
0030: 32 2f 61 31 31 33 64 41 72 63 61 6d 2d 31 66 63   2/a113dArcam-1fc
0040: 31 34 65 37 30 2d 39 36 65 32 2d 34 65 31 36 2d   14e70-96e2-4e16-
0050: 62 62 63 38 2d 61 39 62 62 30 33 33 32 36 33 36   bbc8-a9bb0332636
0060: 63 c0 0c 0c 73 64 70 35 35 2d 35 38 38 61 37 36   c...sdp55-588a76
0070: c0 20 00 2f 80 01 00 00 00 78 00 08 c1 34 00 04   . ./.....x...4..
0080: 40 00 00 08 c0 31 00 2f 80 01 00 00 11 94 00 09   @....1./........
0090: c0 31 00 05 00 00 80 00 40 c0 31 00 21 80 01 00   .1......@.1.!...
00a0: 00 00 78 00 08 00 00 00 00 00 50 c0 63 c0 31 00   ..x.......P.c.1.
00b0: 10 80 01 00 00 11 94 00 b3 20 6e 61 6d 65 3d 4a   ......... name=J
00c0: 42 4c 20 53 79 6e 74 68 65 73 69 73 20 53 44 50   BL Synthesis SDP
00d0: 2d 35 35 2d 35 38 38 61 37 36 2b 73 65 72 69 61   -55-588a76+seria
00e0: 6c 3d 63 61 64 35 38 36 64 64 2d 36 62 36 32 2d   l=cad586dd-6b62-
00f0: 34 62 64 37 2d 39 31 36 38 2d 33 35 32 35 64 36   4bd7-9168-3525d6
0100: 34 66 31 30 34 38 34 75 75 69 64 3d 61 31 31 33   4f10484uuid=a113
0110: 64 41 72 63 61 6d 2d 31 66 63 31 34 65 37 30 2d   dArcam-1fc14e70-
0120: 39 36 65 32 2d 34 65 31 36 2d 62 62 63 38 2d 61   96e2-4e16-bbc8-a
0130: 39 62 62 30 33 33 32 36 33 36 63 20 6d 61 6e 75   9bb0332636c manu
0140: 66 61 63 74 75 72 65 72 3d 48 61 72 6d 61 6e 20   facturer=Harman 
0150: 4c 75 78 75 72 79 20 41 75 64 69 6f 0f 69 70 3d   Luxury Audio.ip=
0160: 31 39 32 2e 31 36 38 2e 34 2e 32 30               192.168.4.20
");

            if (rc == 0) rc = TestHexDump(++id, @"
0000: 00 00 84 00 00 00 00 04 00 00 00 00 0f 6c 75 74   .............lut
0010: 72 6f 6e 2d 30 33 32 65 64 33 38 63 05 6c 6f 63   ron-032ed38c.loc
0020: 61 6c 00 00 01 80 01 00 00 00 78 00 04 c0 a8 04   al........x.....
0030: 47 0d 4c 75 74 72 6f 6e 20 53 74 61 74 75 73 07   G.Lutron Status.
0040: 5f 6c 75 74 72 6f 6e 04 5f 74 63 70 c0 1c 00 2f   _lutron._tcp.../
0050: 80 01 00 00 11 94 00 09 c0 2a 00 05 00 00 80 00   .........*......
0060: 40 c0 0c 00 2f 80 01 00 00 00 78 00 08 c0 3a 00   @.../.....x...:.
0070: 04 40 00 00 08 c0 31 00 10 80 01 00 00 11 94 00   .@....1.........
0080: 7f 19 4d 41 43 41 44 44 52 3d 62 34 3a 62 63 3a   ~.MACADDR=b4:bc:
0090: 37 63 3a 65 37 3a 31 37 3a 36 36 14 43 4f 44 45   7c:e7:17:66.CODE
00a0: 56 45 52 3d 30 35 2e 30 31 2e 30 31 61 30 30 30   VER=05.01.01a000
00b0: 11 44 45 56 43 4c 41 53 53 3d 30 38 30 39 30 33   .DEVCLASS=080903
00c0: 30 31 14 46 57 5f 53 54 41 54 55 53 3d 31 3a 4e   01.FW_STATUS=1:N
00d0: 6f 55 70 64 61 74 65 19 4e 57 5f 53 54 41 54 55   oUpdate.NW_STATU
00e0: 53 3d 49 6e 74 65 72 6e 65 74 57 6f 72 6b 69 6e   S=InternetWorkin
00f0: 67 0e 53 54 5f 53 54 41 54 55 53 3d 67 6f 6f 64   g.ST_STATUS=good
");

            if (rc == 0) rc = TestHexDump(++id, @"
0000: 00 00 84 00 00 00 00 06 00 00 00 00 10 5f 73 70   ............._sp
0010: 6f 74 69 66 79 2d 63 6f 6e 6e 65 63 74 04 5f 74   otify-connect._t
0020: 63 70 05 6c 6f 63 61 6c 00 00 0c 00 01 00 00 11   cp.local........
0030: 94 00 27 24 63 36 35 66 32 38 61 31 2d 30 66 38   ..'$c65f28a1-0f8
0040: 63 2d 35 34 66 37 2d 38 62 39 65 2d 61 34 30 66   c-54f7-8b9e-a40f
0050: 31 64 35 32 38 38 36 66 c0 0c 0c 58 30 31 30 30   1d52886f...X0100
0060: 30 38 4b 4a 45 39 58 c0 22 00 2f 80 01 00 00 00   08KJE9X.../.....
0070: 78 00 05 c2 32 00 01 40 c0 33 00 2f 80 01 00 00   x...2..@.3./....
0080: 11 94 00 09 c0 55 00 05 00 00 80 00 40 0a 52 6f   .....U......@.Ro
0090: 6b 75 20 55 6c 74 72 61 08 5f 61 69 72 70 6c 61   ku Ultra._airpla
00a0: 79 c0 1d 00 2f 80 01 00 00 11 94 00 09 c0 2b 00   y.../.........+.
00b0: 05 00 00 80 00 40 c0 33 00 21 80 01 00 00 00 78   .....@.3.!.....x
00c0: 00 08 00 00 00 00 83 59 c0 5a c0 33 00 10 80 01   .......Y.Z.3....
00d0: 00 00 11 94 00 0a 09 43 50 61 74 68 3d 2f 7a 63   .......CPath=/zc
");

            if (rc == 0) rc = TestHexDump(++id, @"
0000: 00 00 84 00 00 00 00 09 00 00 00 00 05 5f 68 74   ............._ht
0010: 74 70 04 5f 74 63 70 05 6c 6f 63 61 6c 00 00 0c   tp._tcp.local...
0020: 00 01 00 00 11 94 00 1e 1b 4a 42 4c 20 53 79 6e   .........JBL Syn
0030: 74 68 65 73 69 73 20 53 44 50 2d 35 35 2d 35 38   thesis SDP-55-58
0040: 38 61 37 36 c0 0c 0c 73 64 70 35 35 2d 35 38 38   8a76...sdp55-588
0050: 61 37 36 c0 17 00 2f 80 01 00 00 00 78 00 08 c0   a76.../.....x...
0060: d5 00 04 40 00 00 08 28 30 30 31 42 37 43 30 39   ...@...(001B7C09
0070: 38 30 41 44 40 4a 42 4c 20 53 79 6e 74 68 65 73   80AD@JBL Synthes
0080: 69 73 20 53 44 50 2d 35 35 2d 35 38 38 61 37 36   is SDP-55-588a76
0090: 05 5f 72 61 6f 70 c0 12 00 2f 80 01 00 00 11 94   ._raop.../......
00a0: 00 09 c0 8b 00 05 00 00 80 00 40 1b 4a 42 4c 20   ..........@.JBL 
00b0: 53 79 6e 74 68 65 73 69 73 20 53 44 50 2d 35 35   Synthesis SDP-55
00c0: 2d 35 38 38 61 37 36 08 5f 61 69 72 70 6c 61 79   -588a76._airplay
00d0: c0 12 00 2f 80 01 00 00 11 94 00 09 c0 5b 00 05   .../.........[..
00e0: 00 00 80 00 40 c0 28 00 2f 80 01 00 00 11 94 00   ....@.(./.......
00f0: 09 c0 28 00 05 00 00 80 00 40 c0 ab 00 21 80 01   ..(......@...!..
0100: 00 00 00 78 00 08 00 00 00 00 1b 58 c0 46 c0 ab   ...x.......X.F..
0110: 00 10 80 01 00 00 11 94 01 91 05 61 63 6c 3d 30   ...........acl=0
0120: 1a 64 65 76 69 63 65 69 64 3d 30 30 3a 31 42 3a   .deviceid=00:1B:
0130: 37 43 3a 30 39 3a 38 30 3a 41 44 1b 66 65 61 74   7C:09:80:AD.feat
0140: 75 72 65 73 3d 30 78 34 34 35 46 38 41 30 30 2c   ures=0x445F8A00,
0150: 30 78 31 43 33 34 30 07 72 73 66 3d 30 78 30 1b   0x1C340.rsf=0x0.
0160: 66 76 3d 70 32 30 2e 30 2e 31 30 30 2e 31 33 30   fv=p20.0.100.130
0170: 34 2e 30 78 62 39 39 32 63 65 31 09 66 6c 61 67   4.0xb992ce1.flag
0180: 73 3d 30 78 34 1a 6d 6f 64 65 6c 3d 4a 42 4c 20   s=0x4.model=JBL 
0190: 53 79 6e 74 68 65 73 69 73 20 53 44 50 2d 35 35   Synthesis SDP-55
01a0: 20 6d 61 6e 75 66 61 63 74 75 72 65 72 3d 48 61    manufacturer=Ha
01b0: 72 6d 61 6e 20 4c 75 78 75 72 79 20 41 75 64 69   rman Luxury Audi
01c0: 6f 31 73 65 72 69 61 6c 4e 75 6d 62 65 72 3d 63   o1serialNumber=c
01d0: 61 64 35 38 36 64 64 2d 36 62 36 32 2d 34 62 64   ad586dd-6b62-4bd
01e0: 37 2d 39 31 36 38 2d 33 35 32 35 64 36 34 66 31   7-9168-3525d64f1
01f0: 30 34 38 0d 70 72 6f 74 6f 76 65 72 73 3d 31 2e   048.protovers=1.
0200: 31 0d 73 72 63 76 65 72 73 3d 33 36 36 2e 30 27   1.srcvers=366.0'
0210: 70 69 3d 35 64 33 65 31 32 30 38 2d 64 35 31 31   pi=5d3e1208-d511
0220: 2d 34 33 35 64 2d 38 37 66 64 2d 31 34 32 66 31   -435d-87fd-142f1
0230: 62 33 34 66 31 32 62 28 67 69 64 3d 35 64 33 65   b34f12b(gid=5d3e
0240: 31 32 30 38 2d 64 35 31 31 2d 34 33 35 64 2d 38   1208-d511-435d-8
0250: 37 66 64 2d 31 34 32 66 31 62 33 34 66 31 32 62   7fd-142f1b34f12b
0260: 06 67 63 67 6c 3d 30 43 70 6b 3d 62 61 34 34 33   .gcgl=0Cpk=ba443
0270: 33 62 32 31 33 65 35 65 35 33 64 66 37 38 32 39   3b213e5e53df7829
0280: 39 34 37 63 32 38 64 62 62 31 36 64 66 38 34 35   947c28dbb16df845
0290: 61 36 63 64 32 38 63 63 38 35 31 31 39 35 37 38   a6cd28cc85119578
02a0: 39 36 32 33 35 63 66 30 39 30 62 c0 28 00 21 80   96235cf090b.(.!.
02b0: 01 00 00 00 78 00 08 00 00 00 00 00 50 c0 46 c0   ....x.......P.F.
02c0: 28 00 10 80 01 00 00 11 94 00 01 00               (...........
");

            if (rc == 0) rc = TestHexDump(++id, @"
0000: 00 00 84 00 00 00 00 09 00 00 00 00 12 48 6f 6d   .............Hom
0010: 65 20 54 68 65 61 74 65 72 20 28 37 34 31 29 0c   e Theater (741).
0020: 5f 64 65 76 69 63 65 2d 69 6e 66 6f 04 5f 74 63   _device-info._tc
0030: 70 05 6c 6f 63 61 6c 00 00 10 00 01 00 00 11 94   p.local.........
0040: 00 0e 0d 6d 6f 64 65 6c 3d 4a 31 30 35 61 41 50   ...model=J105aAP
0050: 24 43 44 44 42 39 34 38 35 2d 39 34 46 39 2d 35   $CDDB9485-94F9-5
0060: 33 38 34 2d 39 37 37 31 2d 46 45 31 46 36 43 44   384-9771-FE1F6CD
0070: 38 33 44 42 38 08 5f 68 6f 6d 65 6b 69 74 c0 2c   83DB8._homekit.,
0080: 00 2f 80 01 00 00 11 94 00 09 c1 35 00 05 00 00   ./.........5....
0090: 80 00 40 1f 36 43 34 41 38 35 35 37 30 41 36 41   ..@.6C4A85570A6A
00a0: 40 48 6f 6d 65 20 54 68 65 61 74 65 72 20 28 37   @Home Theater (7
00b0: 34 31 29 05 5f 72 61 6f 70 c0 2c 00 2f 80 01 00   41)._raop.,./...
00c0: 00 11 94 00 09 c0 fe 00 05 00 00 80 00 40 12 48   .............@.H
00d0: 6f 6d 65 20 54 68 65 61 74 65 72 20 28 37 34 31   ome Theater (741
00e0: 29 08 5f 61 69 72 70 6c 61 79 c0 2c 00 2f 80 01   )._airplay.,./..
00f0: 00 00 11 94 00 09 c0 d7 00 05 00 00 80 00 40 12   ..............@.
0100: 48 6f 6d 65 20 54 68 65 61 74 65 72 20 28 37 34   Home Theater (74
0110: 31 29 0f 5f 63 6f 6d 70 61 6e 69 6f 6e 2d 6c 69   1)._companion-li
0120: 6e 6b c0 2c 00 2f 80 01 00 00 11 94 00 09 c0 73   nk.,./.........s
0130: 00 05 00 00 80 00 40 20 37 30 2d 33 35 2d 36 30   ......@ 70-35-60
0140: 2d 36 33 2e 31 20 48 6f 6d 65 20 54 68 65 61 74   -63.1 Home Theat
0150: 65 72 20 28 37 34 31 29 0c 5f 73 6c 65 65 70 2d   er (741)._sleep-
0160: 70 72 6f 78 79 04 5f 75 64 70 c0 31 00 2f 80 01   proxy._udp.1./..
0170: 00 00 11 94 00 09 c0 2f 00 05 00 00 80 00 40 c0   ......./......@.
0180: 93 00 21 80 01 00 00 00 78 00 19 00 00 00 00 1b   ..!.....x.......
0190: 58 10 48 6f 6d 65 2d 54 68 65 61 74 65 72 2d 37   X.Home-Theater-7
01a0: 34 31 c0 31 c0 93 00 10 80 01 00 00 11 94 00 c4   41.1............
01b0: 0a 63 6e 3d 30 2c 31 2c 32 2c 33 07 64 61 3d 74   .cn=0,1,2,3.da=t
01c0: 72 75 65 08 65 74 3d 30 2c 33 2c 35 18 66 74 3d   rue.et=0,3,5.ft=
01d0: 30 78 34 41 37 46 44 46 44 35 2c 30 78 42 43 31   0x4A7FDFD5,0xBC1
01e0: 35 37 46 44 45 0a 73 66 3d 30 78 31 38 32 34 34   57FDE.sf=0x18244
01f0: 08 6d 64 3d 30 2c 31 2c 32 0d 61 6d 3d 41 70 70   .md=0,1,2.am=App
0200: 6c 65 54 56 36 2c 32 43 70 6b 3d 31 66 36 33 32   leTV6,2Cpk=1f632
0210: 39 66 62 33 64 38 62 35 37 65 66 61 61 32 38 38   9fb3d8b57efaa288
0220: 31 35 39 31 36 34 65 34 65 64 37 66 33 66 34 63   159164e4ed7f3f4c
0230: 61 31 65 37 38 66 39 66 61 31 62 66 65 35 66 39   a1e78f9fa1bfe5f9
0240: 39 64 34 35 63 38 66 62 61 62 31 06 74 70 3d 55   9d45c8fbab1.tp=U
0250: 44 50 08 76 6e 3d 36 35 35 33 37 0b 76 73 3d 36   DP.vn=65537.vs=6
0260: 30 30 2e 38 2e 34 31 07 6f 76 3d 31 35 2e 32 04   00.8.41.ov=15.2.
0270: 76 76 3d 32 c0 b3 00 0c 00 01 00 00 11 94 00 02   vv=2............
0280: c0 93                                             ..
");
            return rc;
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

        static int MessageRoundTrip()
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

            // Deserialize the packet into a Message object.
            var original = new Message(packet, true);
            if (0 != CheckLivingRoomAirPlaySpeaker(original))
                return 1;

            // Serialize back to binary data and compare.
            var writer = new RecordWriter();
            original.Write(writer);

            var copyData = writer.GetData();
            // I can't just compare copyData[] to packet[], because original
            // data does not follow the spec for suppressing domain name configuration.
            // So I re-deserialize it and verify it the same way as the original.
            var copy = new Message(copyData, true);
            if (0 != CheckLivingRoomAirPlaySpeaker(copy))
                return 1;

            return 0;
        }

        static int CheckLivingRoomAirPlaySpeaker(Message message)
        {
            if (message.Questions.Count != 0)
                return Fail($"Expected 0 questions but found {message.Questions.Count}");

            if (message.Answers.Count != 1)
                return Fail($"Expected 1 answer but found {message.Answers.Count}");

            if (message.Authorities.Count != 0)
                return Fail($"Expected 0 authorities but found {message.Authorities.Count}");

            if (message.Additionals.Count != 5)
                return Fail($"Expected 5 additionals but found {message.Additionals.Count}");

            const string FullName = "745E1C22FAFD@Living Room._raop._tcp.local.";
            const string ShortName = "Living-Room.local.";

            // Answer 0

            RR r = message.Answers[0];
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
            r = message.Additionals[0];
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
            r = message.Additionals[1];
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
            r = message.Additionals[2];
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
            r = message.Additionals[3];
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
            r = message.Additionals[4];
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

        static PublishedService MakeTestService()
        {
            return new PublishedService
            {
                Client = null,
                LongName = "745E1C2300FF@Office",
                ShortName = "Office",
                ServiceType = "_raop._tcp.local.",
                Port = 1234,
                TxtRecord = new Dictionary<string, string>
                {
                    {"txtvers", "1" },
                    {"ch", "2" },
                    {"cn", "0,1" },
                    {"et", "0,4" },
                    {"sv", "false" },
                    {"da", "true" },
                    {"sr", "44100" },
                    {"ss", "16" },
                    {"pw", "false" },
                    {"vn", "65537" },
                    {"tp", "UDP" },
                    {"vs", "103.2" },
                    {"am", "XW-SMA4" },
                    {"fv", "s1051.1000.0" },
                },
            };
        }

        static int Claim()
        {
            var pub = MakeTestService();
            IPAddress addr = IPAddress.Parse("192.168.1.23");
            Message claim = Publisher.MakeClaimPacket(pub, addr);

            // For now, just hex dump the claim packet so I can inspect it manually.
            var writer = new RecordWriter();
            claim.Write(writer);
            byte[] data = writer.GetData();

            Console.WriteLine();
            Console.WriteLine("Claim packet:");
            HexDump(data);
            Console.WriteLine();

            return 0;
        }

        static int Announce()
        {
            var pub = MakeTestService();
            IPAddress addr = IPAddress.Parse("192.168.1.23");
            Message announce = Publisher.MakeAnnouncePacket(pub, addr);

            // For now, just hex dump the claim packet so I can inspect it manually.
            var writer = new RecordWriter();
            announce.Write(writer);
            byte[] data = writer.GetData();

            Console.WriteLine();
            Console.WriteLine("Announce packet:");
            HexDump(data);
            Console.WriteLine();

            return 0;
        }
    }
}
