using System;
using System.IO;

namespace Watcher
{
    internal class Logger : IDisposable
    {
        private bool console;
        private StreamWriter outfile;

        public Logger(bool console, string outFileName)
        {
            this.console = console;
            if (outFileName != null)
                this.outfile = File.CreateText(outFileName);
        }

        public void Dispose()
        {
            Flush();
            if (outfile != null)
            {
                outfile.Dispose();
                outfile = null;
            }
        }

        public void Write(string format, params object[] args)
        {
            if (console)
                Console.Write(format, args);

            if (outfile != null)
                outfile.Write(format, args);
        }

        public void WriteLine()
        {
            if (console)
                Console.WriteLine();

            if (outfile != null)
                outfile.WriteLine();
        }

        public void WriteLine(string format, params object[] args)
        {
            if (console)
                Console.WriteLine(format, args);

            if (outfile != null)
                outfile.WriteLine(format, args);
        }

        public void Flush()
        {
            if (console)
                Console.Out.Flush();

            if (outfile != null)
                outfile.Flush();
        }
    }
}
