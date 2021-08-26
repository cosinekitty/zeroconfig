using System;
using CosineKitty.ZeroConfigWatcher;
using Heijden.DNS;

namespace Watcher
{
    class Program
    {
        static Logger logger;

        static int Main(string[] args)
        {
            using (logger = new Logger(true, "watcher.log"))
            using (var monitor = new TrafficMonitor())
            {
                monitor.OnReceive += OnTrafficReceived;
                monitor.Start();
                Console.WriteLine("Listening for traffic on {0} adapter(s). Press ENTER to quit.", monitor.ListeningAdapterCount);
                Console.WriteLine();
                Console.ReadLine();
            }
            return 0;
        }

        static void OnTrafficReceived(object sender, Packet e)
        {
            logger.WriteLine("=========================================================================");
            logger.WriteLine("{0} : packet from {1}", e.UtcArrival.ToString("o"), e.RemoteEndPoint.Address);
            logger.WriteLine();
            HexDump(e.Data);
            logger.WriteLine();
            Interpret(e.Data);
            logger.WriteLine();
            logger.Flush();
        }

        static void HexDump(byte[] data)
        {
            logger.WriteLine("       0  1  2  3  4  5  6  7    8  9  a  b  c  d  e  f");
            logger.WriteLine("      -- -- -- -- -- -- -- --   -- -- -- -- -- -- -- --");
            for (int row = 0; row < data.Length; row += 0x10)
            {
                logger.Write("{0} ", row.ToString("x4"));
                for (int col = 0; col < 0x10; ++col)
                {
                    int ofs = row + col;
                    if (col == 8)
                        logger.Write("  ");
                    if (ofs < data.Length)
                        logger.Write(" {0}", data[ofs].ToString("x2"));
                    else
                        logger.Write("   ");
                }

                logger.Write("  ");

                for (int col = 0; col < 0x10; ++col)
                {
                    int ofs = row + col;
                    if (ofs >= data.Length)
                        break;
                    if (data[ofs] >= 0x20 && data[ofs] <= 0x7f)
                        logger.Write("{0}", (char)data[ofs]);
                    else
                        logger.Write(".");
                }

                logger.WriteLine();
            }
        }

        static void Interpret(byte[] data)
        {
            var response = new Response(data);

            foreach (Question q in response.Questions)
                PrintQuestion(q);

            foreach (RR a in response.Answers)
                PrintRR(a);

            foreach (RR a in response.Authorities)
                PrintRR(a);

            foreach (RR a in response.Additionals)
                PrintRR(a);
        }

        private static void PrintQuestion(Question q)
        {
            logger.WriteLine("Question: name=[{0}] type={1} class={2}", q.QName, q.QType, q.QClass);
        }

        private static void PrintRR(RR a)
        {
            logger.WriteLine("{0}: name=[{1}] type={2} class={3} TTL={4}", a.GetType().Name, a.NAME, a.Type, a.Class, a.TTL);
            if (a.RECORD != null)
                logger.WriteLine("{0}", a.RECORD);
            logger.WriteLine();
        }
    }
}
