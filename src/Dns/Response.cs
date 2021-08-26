using System;
using System.IO;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Heijden.DNS
{
    public class Response
    {
        public List<Question> Questions;
        public List<RR> Answers;
        public List<RR> Authorities;
        public List<RR> Additionals;
        public Header header;
        public int MessageSize;
        public DateTime TimeStamp;

        public Response()
        {
            Questions = new List<Question>();
            Answers = new List<RR>();
            Authorities = new List<RR>();
            Additionals = new List<RR>();
            MessageSize = 0;
            TimeStamp = DateTime.Now;
            header = new Header();
        }

        public Response(byte[] data)
        {
            TimeStamp = DateTime.Now;
            MessageSize = data.Length;
            RecordReader rr = new RecordReader(data);

            Questions = new List<Question>();
            Answers = new List<RR>();
            Authorities = new List<RR>();
            Additionals = new List<RR>();

            header = new Header(rr);

            for (int i = 0; i < header.QDCOUNT; i++)
                Questions.Add(new Question(rr));

            for (int i = 0; i < header.ANCOUNT; i++)
                Answers.Add(new RR(rr));

            for (int i = 0; i < header.NSCOUNT; i++)
                Authorities.Add(new RR(rr));

            for (int i = 0; i < header.ARCOUNT; i++)
                Additionals.Add(new RR(rr));
        }
    }
}
