using System.Collections.Generic;

namespace Heijden.DNS
{
    public class Request
    {
        public Header header;
        readonly List<Question> questions;

        public Request()
        {
            header = new Header
            {
                OPCODE = OPCode.Query,
                QDCOUNT = 0
            };

            questions = new List<Question>();
        }

        public void AddQuestion(Question question)
        {
            questions.Add(question);
        }

        public void Write(RecordWriter writer)
        {
            header.QDCOUNT = (ushort)questions.Count;

            header.Write(writer);
            foreach (Question q in questions)
                q.Write(writer);
        }
    }
}
