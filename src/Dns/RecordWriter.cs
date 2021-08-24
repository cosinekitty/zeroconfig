using System;
using System.Collections.Generic;
using System.Text;

namespace Heijden.DNS
{
    public class RecordWriter
    {
        private List<byte> buffer = new List<byte>();

        public int Length
        {
            get { return buffer.Count; }
        }

        public byte[] GetData()
        {
            return buffer.ToArray();
        }

        public void WriteByte(byte x)
        {
            buffer.Add(x);
        }

        public void WriteUint16(UInt16 x)
        {
            buffer.Add((byte)(x >> 8));
            buffer.Add((byte)(x));
        }

        public void WriteUint32(UInt32 x)
        {
            buffer.Add((byte)(x >> 24));
            buffer.Add((byte)(x >> 16));
            buffer.Add((byte)(x >> 8));
            buffer.Add((byte)(x));
        }

        public void WriteDomainName(string s)
        {
            // Split the domain name into a series of labels that are delimited by ".".
            string[] list = s.Split(new char[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string label in list)
            {
                byte[] data = Encoding.UTF8.GetBytes(label);
                if (data.Length > 63)
                    throw new Exception($"Label [{label}] is longer than 63 bytes.");
                byte length = (byte)data.Length;
                // FIXFIXFIX: exploit name compression when the label exists earlier in this packet.
                // length |= 0xc0, etc...
                buffer.Add(length);
                for (int i = 0; i < data.Length; ++i)
                    buffer.Add(data[i]);
            }
            // Terminate the label list with a 0-length byte.
            buffer.Add(0);
        }

        public void WriteString(string s)
        {
            byte[] data = Encoding.UTF8.GetBytes(s);

            if (data.Length >= 0xc0)
                throw new ArgumentException($"String is too long to encode: [{s}]");

            buffer.Add((byte)data.Length);
            for (int i = 0; i < data.Length; ++i)
                buffer.Add(data[i]);
        }

        public void WriteRecord(Record rec)
        {
            // Reserve 2 bytes for the record length at the front.
            int front = buffer.Count;
            buffer.Add(0);
            buffer.Add(0);

            // Serialize the record into the buffer.
            rec.Write(this);

            // Calculate the number of bytes serialized.
            int length = (buffer.Count - front) - 2;
            if (length > 0xffff)
                throw new ArgumentException($"Record is too long: {length} bytes");

            // Store the length at the front.
            buffer[front] = (byte)(length >> 8);
            buffer[front+1] = (byte)(length);
        }
    }
}
