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

                // See if we can exploit compression by searching for the same byte pattern
                // earlier in this same message.
                int position = FindMatchingByteSequence(data);
                if (position < 0 || position > 0x3fff)
                {
                    // No match, or position is not representable, so emit entire label sequence here.
                    buffer.Add(length);
                    for (int i = 0; i < data.Length; ++i)
                        buffer.Add(data[i]);
                }
                else
                {
                    // Save space by emitting compressed data here.
                    buffer.Add((byte)(0xc0 | (position >> 8)));
                    buffer.Add((byte)(position));
                }
            }
            // Terminate the label list with a 0-length byte.
            buffer.Add(0);
        }

        private int FindMatchingByteSequence(byte[] data)
        {
            if (data.Length < 3)
                return -1;  // cannot make this data any smaller by referring to earlier strings

            // Search the first 0x3fff bytes for [length, data...] matching bytes.
            int length = Math.Min(0x3fff, buffer.Count);
            for (int position = 0; position + data.Length + 1 <= length; ++position)
            {
                if (buffer[position] == data.Length)
                {
                    bool match = true;
                    for (int index = 0; match && index < data.Length; ++index)
                        if (buffer[position + 1 + index] != data[index])
                            match = false;
                    if (match)
                        return position;
                }
            }
            return -1;  // no match found
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
