using System.Net;
using System.Net.Sockets;

Console.WriteLine("miniThinca Server.");

string ipAddress =  "127.0.0.1";
if(args.Length > 0)
{
    Console.WriteLine("Using ip address:" + ipAddress);
    ipAddress = args[0];
}
else
{
    var host = Dns.GetHostEntry(Dns.GetHostName());
    foreach (var ip in host.AddressList)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString().StartsWith("192"))
        {
            Console.WriteLine("Using ip address:" + ip.ToString());
            ipAddress = ip.ToString();
            break;
        }
    }
}

var miniThincaHandler = new miniThincaLib.miniThinca(ipAddress);

var httpListener = new System.Net.HttpListener();
httpListener.Prefixes.Add("http://*:80/");
bool Running = true;

var ListenerTask = new Task(new Action(() => {
    httpListener.Start();

    while (Running)
    {
        try
        {
            var Result = httpListener.BeginGetContext(
                    (result) => {
                        var listener = result.AsyncState as HttpListener;
                        if (listener == null) return;

                        var context = listener.EndGetContext(result);

                        miniThincaHandler.Dispatcher(context);

                    }, httpListener);
            Result.AsyncWaitHandle.WaitOne();
        }catch(Exception e)
        {
            Console.WriteLine("Exception:" + e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }
    httpListener.Stop();
}), TaskCreationOptions.LongRunning);

ListenerTask.Start();
Console.WriteLine("Server online,press esc to exit.");
while (true)
{
    switch (Console.ReadKey().Key)
    {
        case ConsoleKey.Escape:
            Console.WriteLine("Escape Pressed.");
            Running = false;
            if (ListenerTask.Status == TaskStatus.Running)
            {
                Console.WriteLine("Waiting for ListenerTask Exit.");
                ListenerTask.Wait(5000);
            }
            Environment.Exit(0);
            break;
        default:
            break;
    }
}