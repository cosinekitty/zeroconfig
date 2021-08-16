using System;

#region Rfc info
/*
2.2 AAAA data format

   A 128 bit IPv6 address is encoded in the data portion of an AAAA
   resource record in network byte order (high-order byte first).
 */
#endregion

namespace Heijden.DNS
{
    public class RecordAAAA : Record
	{
        public UInt16[] data = new UInt16[8];
        public string Address;

		public RecordAAAA(RecordReader rr)
		{
            for (int i = 0; i < 8; ++i)
                data[i] = rr.ReadUInt16();

            Address = string.Format("{0:x}:{1:x}:{2:x}:{3:x}:{4:x}:{5:x}:{6:x}:{7:x}",
                data[0],
                data[1],
                data[2],
                data[3],
                data[4],
                data[5],
                data[6],
                data[7]);
		}

		public override string ToString()
		{
			return Address;
		}

        public override void Write(RecordWriter rw)
        {
            for (int i = 0; i < 8; ++i)
                rw.WriteUint16(data[i]);
        }
	}
}
