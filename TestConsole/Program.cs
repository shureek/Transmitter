using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());

            var listener = new XmlWriterTraceListener(String.Format("TestConsole {0:yyyy-MM-dd HH-mm-ss}.svclog", DateTime.Now));
            listener.TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ProcessId | TraceOptions.ThreadId | TraceOptions.Timestamp | TraceOptions.LogicalOperationStack | TraceOptions.Callstack;
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;

            Trace.TraceInformation("Program started");

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            //Console.Write("Creating runner:");
            var obj = new LongRunner();
            //Console.WriteLine(" done.");
            //Console.WriteLine("Initiating long operation");
            for (int i = 1; i < 5; i++)
                obj.LongOperation(i, tokenSource.Token);

            Console.WriteLine("Press any key to stop");
            Console.ReadKey(true);
            tokenSource.Cancel();

            //Console.WriteLine("Press any key");
            Console.ReadKey(true);
            Trace.TraceInformation("Program ended");
        }
    }

    public class LongRunner
    {
        static readonly Random rnd;
        static readonly object rndSync;
        static LongRunner()
        {
            rndSync = new Object();
            lock(rndSync)
                rnd = new Random();
        }
        static int NextRandom(int minValue, int maxValue)
        {
            lock(rndSync)
            {
                return rnd.Next(minValue, maxValue);
            }
        }

        public void LongOperation(int id = 0, CancellationToken? cancellationToken = null)
        {
            Thread thread = new Thread(LongOperationInternal);
            var args = new OperationArgs();
            args.Id = id;
            args.CancellationToken = cancellationToken.HasValue ? cancellationToken.Value : new CancellationToken();
            args.Guid = Guid.NewGuid();
            Guid oldGuid = Trace.CorrelationManager.ActivityId;
            Trace.CorrelationManager.StartLogicalOperation(String.Format("Operation {0}", args.Id));
            Trace.CorrelationManager.ActivityId = args.Guid;
            Trace.TraceInformation("Starting long operation {0} ({1})", id, args.Guid);
            Trace.CorrelationManager.ActivityId = oldGuid;
            thread.Start(args);
        }

        void LongOperationInternal(object state)
        {
            var args = (OperationArgs)state;
            Trace.CorrelationManager.ActivityId = args.Guid;
            if (args.CancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Operation {0} is cancelled before start", args.Id);
                Trace.CorrelationManager.StopLogicalOperation();
                return;
            }

            for (int i = 0; i < 10; i++)
            {
                if (args.CancellationToken.IsCancellationRequested)
                {
                    Trace.TraceInformation("Operation {0} is cancelled", args.Id);
                    return;
                }
                if (i == args.Id)
                    LongOperation(i + 10, args.CancellationToken);
                else
                    Trace.TraceInformation("Running operation {0}", args.Id);
                Thread.Sleep(NextRandom(0, 1000));
            }

            Trace.TraceInformation("Operation {0} completed", args.Id);
        }

        struct OperationArgs
        {
            public CancellationToken CancellationToken;
            public int Id;
            public Guid Guid;
        }
    }
}
