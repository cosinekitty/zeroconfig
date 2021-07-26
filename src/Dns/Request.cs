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

		public byte[] Data
		{
			get
			{
				var data = new List<byte>();
				header.QDCOUNT = (ushort)questions.Count;
				data.AddRange(header.Data);
				foreach (var q in questions)
					data.AddRange(q.Data);
				return data.ToArray();
			}
		}
	}
}
