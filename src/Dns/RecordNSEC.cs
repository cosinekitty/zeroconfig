using System;
using System.Linq;

namespace Heijden.DNS
{
    public class RecordNSEC : Record
    {
        public byte[] RDATA;

        public RecordNSEC(RecordReader rr)
        {
            // re-read length
            ushort RDLENGTH = rr.ReadUInt16(-2);

            RDATA = rr.ReadBytes(RDLENGTH);
        }

        public override void Write(RecordWriter rw)
        {
            if (RDATA != null)
                for (int i = 0; i < RDATA.Length; ++i)
                    rw.WriteByte(RDATA[i]);
        }

        public override string ToString()
        {
            if (RDATA == null)
                return "RDATA = null";
            return "RDATA = [" + string.Join(" ", RDATA.Select(b => b.ToString("x2"))) + "]";
        }
    }
}
