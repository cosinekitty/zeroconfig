using System.Collections.Generic;

namespace Heijden.DNS
{
    public abstract class Record
    {
        /// <summary>
        /// The Resource Record this RDATA record belongs to
        /// </summary>
        public RR RR;

        public abstract void Write(RecordWriter rw);
        public abstract Type RecordType();
    }
}
