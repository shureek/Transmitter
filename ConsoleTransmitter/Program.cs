using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;

namespace KSoft
{
    class Program
    {
        static void Main(string[] args)
        {
            // Настроим имя файла трассировки
            string programName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
            Trace.Listeners.Add(
                new XmlWriterTraceListener(String.Format("{0} {1:MM-dd-HH-mm-ss}.svclog", programName, DateTime.Now)));

            if (Console.WindowWidth < 130)
                Console.WindowWidth = 130;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            Transmitter transmitter = new Transmitter();
            transmitter.LoadSettings("Settings.xml");
            //InitTransmitter(transmitter);

            WriteLine("Настройки прочитаны");
            transmitter.MessageReceived += TransmitterOnMessageReceived;
            transmitter.ErrorOccured += TransmitterOnErrorOccured;
            transmitter.Start();
            WriteLine("Получатели запущены");

            Console.ReadKey(true);
            WriteLine("Остановка");
            transmitter.Stop();
            WriteLine("Нажмите любую клавишу для выхода");
            Console.ReadKey(true);
        }

        /*
        static void InitTransmitter(Transmitter transmitter)
        {
            IReceiver receiver;
            receiver = new KSoft.NetReceiver()
            {
                Port = 3058,
                ProtocolType = ProtocolType.Tcp,
                Pult = "СаратовРитм",
                SourceName = "RitmTCP",
                MessageReaderFactory = new TypedObjectFactory<IMessageReader>(true) { ObjectType = typeof(KSoft.AddOn.RitmTCPReader) }
            };
            transmitter.AddReceiver(receiver);

            receiver = new COMReceiver()
                {
                    PortName = "COM2",
                    SourceName = "ComPort2",
                    Pult = "СаратовРитм",
                    BaudRate = 19200,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    DtrEnable = true,
                    RtsEnable = false
                };
            transmitter.AddReceiver(receiver);

            var sender = new KSoft.AddOn.MSMQSender()
            {
                QueueName = @"MAIN\events"
            };
            transmitter.AddSender(sender);
        }
        */
        static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            TraceHelper.WriteError(null, (Exception)e.ExceptionObject);
        }

        static void TransmitterOnErrorOccured(object sender, ErrorEventArgs errorEventArgs)
        {
            string strSource;
            if (sender is IReceiver)
                strSource = String.Format("{0} ({1})", sender.GetType().FullName, (sender as IReceiver).SourceName);
            else
                strSource = sender.ToString();
            if (errorEventArgs.Error is ReceiverException)
                strSource = strSource + ", " + ((ReceiverException)errorEventArgs.Error).SourceAddress;
            WriteError("{0}: Ошибка ({1}): {2}", strSource, errorEventArgs.Error.GetType().FullName, errorEventArgs.Error.Message);
        }

        static void TransmitterOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            string msgType = "Message";
            if (messageEventArgs.Message is ObjectMessage)
            {
                var type = (messageEventArgs.Message as ObjectMessage).Type;
                switch (type)
                {
                case MessageType.Event:
                    msgType = "Event";
                    WriteMessage("{0} from {1}: {2}", msgType, messageEventArgs.Message.Source, messageEventArgs.Message);
                    break;
                case MessageType.Test:
                    msgType = "Test";
                    WriteLine("{0} from {1}: {2}", msgType, messageEventArgs.Message.Source, messageEventArgs.Message);
                    break;
                default:
                    msgType = "Unknown message";
                    WriteLine("{0} from {1}: {2}", msgType, messageEventArgs.Message.Source, messageEventArgs.Message);
                    break;
                }
            }
            else
                WriteLine("{0} from {1}: {2}", msgType, messageEventArgs.Message.Source, messageEventArgs.Message);
        }

        static object consoleSync = new Object();

        static void WriteMessage(string format, params object[] arg)
        {
            lock(consoleSync)
            {
                Console.ForegroundColor = ConsoleColor.White;
                WriteLine(format, arg);
                Console.ResetColor();
            }
        }

        static void WriteError(string format, params object[] arg)
        {
            lock (consoleSync)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                WriteLine(format, arg);
                Console.ResetColor();
            }
        }

        static void WriteLine(string format, params object[] arg)
        {
            lock(consoleSync)
            {
                var sb = new StringBuilder();
                Console.Write("{0:dd.MM HH:mm:ss.fff} ", DateTime.Now);
                Console.WriteLine(format, arg);
            }
        }
    }
}
