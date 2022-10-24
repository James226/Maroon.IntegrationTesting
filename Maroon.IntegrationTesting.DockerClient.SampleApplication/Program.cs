using System.Net.Mime;

var shutdown = new CancellationTokenSource();

AppDomain.CurrentDomain.ProcessExit += (_,_) =>
{
    shutdown.Cancel();
};

Console.CancelKeyPress += (s, e) =>
{
    Environment.Exit(0);
};

Console.WriteLine("Starting up...");

await Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder => webBuilder
        .Configure(app =>
        {
            app.UseHealthChecks("/healthz");
            app.Run(async context =>
            {
                if (context.Request.Path == "/api/echo" && context.Request.Method == "POST")
                {
                    //getting the content of our POST request
                    using var reader = new StreamReader(context.Request.Body);
                    var content = await reader.ReadToEndAsync();

                    //sending it back in the response
                    context.Response.ContentType = MediaTypeNames.Text.Plain;
                    await context.Response.WriteAsync(content);
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
        })
        .ConfigureServices(services =>
        {
            services.AddHealthChecks();
        })
    )
    .Build()
    .StartAsync(shutdown.Token);

Console.WriteLine("Startup complete!");

shutdown.Token.WaitHandle.WaitOne();

Console.WriteLine("Exit Complete");