using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
internal class Program
{
    private static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        string comPort = "COM11";
        if(args.Length > 0 ) comPort = args[0];

        using (var commPort = new SerialPort(comPort)){
            commPort.BaudRate = 115200;
            commPort.Parity = Parity.None;
            commPort.DataBits = 8;
            commPort.StopBits = StopBits.One;
            commPort.DtrEnable = true;
            commPort.RtsEnable = true;

            try
            {
                commPort.Open();
                Console.Title = "vfd.";
                Console.WriteLine("vfd online.");
                for(; ; )
                {
                    var byteGet = commPort.ReadByte();
                    if (byteGet == 0x1B)
                    {
                        byteGet = commPort.ReadByte();
                        switch (byteGet)
                        {
                            case 0x0b:
                                Console.WriteLine("vfd Reset.");
                                Console.Title = "";
                                break;
                            case 0x0c:
                                Console.WriteLine("vfd Clear.");
                                Console.Title = "vfd.";
                                break;
                            case 0x21:
                                Console.WriteLine("vfd PowerOn." + commPort.ReadByte().ToString("X"));
                                Console.Title = "vfd.";
                                break;
                            case 0x30:
                                Console.Write("vfd Set Message 0x30 Line:{0} " , (commPort.ReadByte().ToString("X")));
                                var msgStaticSize = commPort.ReadByte();
                                var msgStaticBuffer = new byte[msgStaticSize];
                                if (commPort.Read(msgStaticBuffer, 0, msgStaticSize) > 0)
                                {
                                    var jpnText = Encoding.GetEncoding(932).GetString(msgStaticBuffer);
                                    Console.WriteLine(jpnText);
                                    Console.Title = jpnText;
                                }
                                else Console.WriteLine(msgStaticSize.ToString("X"));
                                break;
                            case 0x32:
                                Console.WriteLine("vfd Set Language." + commPort.ReadByte().ToString("X"));
                                break;
                            case 0x40:
                                Console.Write("vfd Set Option: ");
                                Console.Write("prm1:{0} ", commPort.ReadByte().ToString("X"));
                                Console.Write("prm2:{0} ", commPort.ReadByte().ToString("X"));
                                Console.Write("Line:{0} ", (int)commPort.ReadByte());
                                Console.WriteLine("BoxSize:{0}*{1}" , (int)commPort.ReadByte() , (int)commPort.ReadByte());
                                break;
                            case 0x41:
                                Console.WriteLine("vfd Set Speed:{0}", (int)commPort.ReadByte());
                                break;
                            case 0x50:
                                Console.Write("vfd Set Message 0x50:");
                                System.Threading.Thread.Sleep(500);
                                var msgSize = commPort.ReadByte();
                                var msgBuffer = new byte[msgSize];
                                if (commPort.Read(msgBuffer, 0, msgSize) > 0)
                                {
                                    var jpnText = Encoding.GetEncoding(932).GetString(msgBuffer);
                                    Console.WriteLine(jpnText);
                                    Console.Title = jpnText;
                                }
                                else Console.WriteLine("Error");
                                break;
                            case 0x51:
                                Console.WriteLine("vfd Start Scroll.");
                                break;
                            case 0x52:
                                Console.WriteLine("vfd Stop Scroll.");
                                Console.Title = "vfd.";
                                break;
                            default:
                                Console.WriteLine("unknown opcode:" + byteGet.ToString("X"));
                                break;
                        }

                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}

internal class vfd
{
    SerialPort commPort;
    public vfd(string port) { commPort = new SerialPort(port); }
}