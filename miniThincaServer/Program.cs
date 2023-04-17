using System.Net;

var miniThincaHandler = new miniThincaLib.miniThinca();

var httpListener = new System.Net.HttpListener();
httpListener.Prefixes.Add("http://*:8088/");
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
        }catch(Exception)
        {

        }
    }
    httpListener.Stop();
}), TaskCreationOptions.LongRunning);

ListenerTask.Start();

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