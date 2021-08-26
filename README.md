# zeroconfig

This is a collection of C# class libraries and executables for browsing,
resolving, and publishing services on a Multicast Domain Name Service (MDNS).

# Browse

An interactive tool that provides a command prompt
for interacting with zeroconfig operations: enumerating
available service types, browsing services, resolving
services, and publishing/unpublishing services.

# Watcher

`Watcher` is a C# .NET 5 diagnostic tool for monitoring MDNS ZeroConfig
service discovery traffic on the local network.

You can think of the `Watcher` program as similar to a stripped-down
[Wireshark](https://www.wireshark.org/) that runs as a .NET Core console app.
It works equally well in any platform that supports .NET Core, including Windows,
Linux, and MacOS.

The `Watcher` program opens the MDNS standard port 5353 on the multicast address 224.0.0.251.
Then it passively monitors and displays all multicast DNS traffic on your local network,
including a hex dump of each packet, along with interpretation of the packet structure.

## Quick Start

```
cd src/Watcher
dotnet run
```

This will display all traffic to the console, as well as logging to the file `watcher.log` in the current directory.
Press the ENTER key at any time to stop logging and quit the program.

## Example Output

Here is the output of a packet from a Google Chromecast advertising itself on my home network:

```
=========================================================================
2021-03-01T01:30:11.1981096Z : packet from 192.168.1.7

       0  1  2  3  4  5  6  7    8  9  a  b  c  d  e  f
      -- -- -- -- -- -- -- --   -- -- -- -- -- -- -- --
0000  00 00 84 00 00 00 00 01   00 00 00 03 0b 5f 67 6f  ............._go
0010  6f 67 6c 65 63 61 73 74   04 5f 74 63 70 05 6c 6f  oglecast._tcp.lo
0020  63 61 6c 00 00 0c 00 01   00 00 00 78 00 2e 2b 43  cal........x..+C
0030  68 72 6f 6d 65 63 61 73   74 2d 33 37 35 38 61 63  hromecast-3758ac
0040  61 65 34 34 32 61 34 37   62 64 63 64 31 36 64 63  ae442a47bdcd16dc
0050  65 30 62 38 38 61 63 64   37 32 c0 0c c0 2e 00 10  e0b88acd72......
0060  80 01 00 00 11 94 00 bb   23 69 64 3d 33 37 35 38  ........#id=3758
0070  61 63 61 65 34 34 32 61   34 37 62 64 63 64 31 36  acae442a47bdcd16
0080  64 63 65 30 62 38 38 61   63 64 37 32 23 63 64 3d  dce0b88acd72#cd=
0090  35 37 34 35 36 33 33 33   43 44 32 32 30 39 41 38  57456333CD2209A8
00a0  33 43 43 34 33 42 30 34   44 33 35 33 32 36 45 41  3CC43B04D35326EA
00b0  13 72 6d 3d 45 42 43 31   44 35 41 32 35 31 30 45  .rm=EBC1D5A2510E
00c0  43 39 43 37 05 76 65 3d   30 35 0d 6d 64 3d 43 68  C9C7.ve=05.md=Ch
00d0  72 6f 6d 65 63 61 73 74   12 69 63 3d 2f 73 65 74  romecast.ic=/set
00e0  75 70 2f 69 63 6f 6e 2e   70 6e 67 11 66 6e 3d 4c  up/icon.png.fn=L
00f0  69 76 69 6e 67 20 52 6f   6f 6d 20 54 56 07 63 61  iving Room TV.ca
0100  3d 34 31 30 31 04 73 74   3d 30 0f 62 73 3d 46 41  =4101.st=0.bs=FA
0110  38 46 43 41 35 30 42 36   43 34 04 6e 66 3d 31 03  8FCA50B6C4.nf=1.
0120  72 73 3d c0 2e 00 21 80   01 00 00 00 78 00 2d 00  rs=...!.....x.-.
0130  00 00 00 1f 49 24 33 37   35 38 61 63 61 65 2d 34  ....I$3758acae-4
0140  34 32 61 2d 34 37 62 64   2d 63 64 31 36 2d 64 63  42a-47bd-cd16-dc
0150  65 30 62 38 38 61 63 64   37 32 c0 1d c1 35 00 01  e0b88acd72...5..
0160  80 01 00 00 00 78 00 04   c0 a8 01 07              .....x......

AnswerRR: name=[_googlecast._tcp.local.] type=PTR class=IN TTL=120
Chromecast-3758acae442a47bdcd16dce0b88acd72._googlecast._tcp.local.

AdditionalRR: name=[Chromecast-3758acae442a47bdcd16dce0b88acd72._googlecast._tcp.local.] type=TXT class=32769 TTL=4500
TXT "id=3758acae442a47bdcd16dce0b88acd72"
TXT "cd=57456333CD2209A83CC43B04D35326EA"
TXT "rm=EBC1D5A2510EC9C7"
TXT "ve=05"
TXT "md=Chromecast"
TXT "ic=/setup/icon.png"
TXT "fn=Living Room TV"
TXT "ca=4101"
TXT "st=0"
TXT "bs=FA8FCA50B6C4"
TXT "nf=1"
TXT "rs="


AdditionalRR: name=[Chromecast-3758acae442a47bdcd16dce0b88acd72._googlecast._tcp.local.] type=SRV class=32769 TTL=120
0 0 8009 3758acae-442a-47bd-cd16-dce0b88acd72.local.

AdditionalRR: name=[3758acae-442a-47bd-cd16-dce0b88acd72.local.] type=A class=32769 TTL=120
192.168.1.7

```

## References

[RFC 1035 : Domain Names - Implementation And Specification](https://datatracker.ietf.org/doc/html/rfc1035)

[RFC 4034 : Resource Records for the DNS Security Extensions](https://datatracker.ietf.org/doc/html/rfc4034)

[RFC 6762 : Multicast DNS](https://tools.ietf.org/html/rfc6762)
Describes the evolution of traditional unicast domain name service
to a link-local multicast DNS service.

[RFC 6763 : DNS-Based Service Discovery](https://tools.ietf.org/html/rfc6763)
Provides detail about how MDNS is used for browsing and
resolving services on a local network.

## Credits

Code for parsing DNS packets derives from the
[DNS.NET Resolver project](https://www.codeproject.com/Articles/23673/DNS-NET-Resolver-C)
by Alphons van der Heijden.

The DNS code also includes modifications made for Claire Novotny's
[Zeroconf](https://github.com/novotnyllc/Zeroconf) Bonjour/MDNS discovery project.
