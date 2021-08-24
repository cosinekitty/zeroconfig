using System;
using System.Collections.Generic;
using System.Text;

namespace Heijden.DNS
{
    public class RecordReader
    {
        byte[] m_Data;
        int m_Position;
        public RecordReader(byte[] data)
        {
            m_Data = data;
            m_Position = 0;
        }

        public int Position
        {
            get
            {
                return m_Position;
            }
            set
            {
                m_Position = value;
            }
        }

        public int Length
        {
            get
            {
                if (m_Data == null)
                    return 0;
                else
                    return m_Data.Length;
            }
        }

        public RecordReader(byte[] data, int Position)
        {
            m_Data = data;
            m_Position = Position;
        }


        public byte ReadByte()
        {
            if (m_Position >= m_Data.Length)
                return 0;
            else
                return m_Data[m_Position++];
        }

        public UInt16 ReadUInt16()
        {
            byte hi = ReadByte();
            byte lo = ReadByte();
            return (UInt16)((hi << 8) | lo);
        }

        public UInt16 ReadUInt16(int offset)
        {
            m_Position += offset;
            return ReadUInt16();
        }

        public UInt32 ReadUInt32()
        {
            UInt16 hi = ReadUInt16();
            UInt16 lo = ReadUInt16();
            return (UInt32)((hi << 16) | lo);
        }

        public string ReadDomainName()
        {
            // A domain name is a series of zero or more "labels".
            // Each label has a length prefix byte.
            // If the high two bits are set for the length prefix, it indicates
            // that we are to copy a susbstring from an earlier part of the same packet.
            // Otherwise, the text bytes follow the length byte.
            // After each label we append ".".

            var bytes = new List<byte>();
            int length = 0;

            // get  the length of the first label
            while ((length = ReadByte()) != 0)
            {
                // top 2 bits set denotes domain name compression and to reference elsewhere
                if ((length & 0xc0) == 0xc0)
                {
                    // work out the existing domain name, copy this pointer
                    RecordReader newRecordReader = new RecordReader(m_Data, (length & 0x3f) << 8 | ReadByte());
                    if (bytes.Count > 0)
                    {
                        return Encoding.UTF8.GetString(bytes.ToArray(), 0, bytes.Count) + newRecordReader.ReadDomainName();
                    }
                    return newRecordReader.ReadDomainName();
                }

                // if not using compression, copy a char at a time to the domain name
                while (length > 0)
                {
                    bytes.Add(ReadByte());
                    length--;
                }
                bytes.Add((byte)'.');
            }
            if (bytes.Count == 0)
                return ".";
            return Encoding.UTF8.GetString(bytes.ToArray(), 0, bytes.Count);
        }

        public string ReadString()
        {
            short length = ReadByte();
            var bytes = new List<byte>();
            for (int i=0; i<length; i++)
                bytes.Add(ReadByte());
            return Encoding.UTF8.GetString(bytes.ToArray(), 0, bytes.Count);
        }

        public byte[] ReadBytes(int intLength)
        {
            byte[] list = new byte[intLength];
            for (int intI = 0; intI < intLength; intI++)
                list[intI] = ReadByte();
            return list;
        }

        public Record ReadRecord(Type type, int Length)
        {
            switch (type)
            {
                case Type.A:
                    return new RecordA(this);
                case Type.PTR:
                    return new RecordPTR(this);
                case Type.TXT:
                    return new RecordTXT(this, Length);
                case Type.AAAA:
                    return new RecordAAAA(this);
                case Type.SRV:
                    return new RecordSRV(this);
                case Type.NSEC:
                    return new RecordNSEC(this);
                default:
                    return new RecordUnknown(this);
            }
        }
    }
}
