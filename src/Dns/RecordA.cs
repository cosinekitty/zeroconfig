using System;
using System.Net;
/*
3.4.1. A RDATA format

+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
|                    ADDRESS                    |
+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

where:

ADDRESS         A 32 bit Internet address.

Hosts that have multiple Internet addresses will have multiple A
records.
*
*/

namespace Heijden.DNS
{
    public class RecordA : Record
	{
        public IPAddress Address;

		public RecordA(RecordReader rr)
		{
            var data = new byte[4];
            data[0] = rr.ReadByte();
            data[1] = rr.ReadByte();
            data[2] = rr.ReadByte();
            data[3] = rr.ReadByte();
            Address = new IPAddress(data);
		}

		public override string ToString()
		{
			return Address.ToString();
		}
	}
}
