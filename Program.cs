using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleHttpServer;
using SimpleHttpServer.Models;

namespace TriggerScan
{

    class Program
    {
        static void Main(string[] args)
        {
            for (;;)
            {
                try
                {
                    var settings = Properties.Settings.Default;

                    var server = new HttpServer(settings.Port, new List<Route>
                    {
                        new Route()
                        {
                            Method = "GET",
                            Name = "",
                            UrlRegex = "/",
                            Callable = (request) =>
                            {
                                if (request.Path == "/scan")
                                {
                                    return new HttpResponse()
                                    {
                                        StatusCode = "200",
                                        ContentAsUTF8 = Scanner.Go(settings.Device, settings.DestPath, settings.Dpi, settings.WidthMM, settings.HeightMM),
                                        Headers = new Dictionary<string, string>()
                                        {
                                            { "Content-Type", "text/plain" }
                                        }
                                    };
                                }
                                else if (request.Path == "/")
                                {

                                    return new HttpResponse()
                                    {
                                        StatusCode = "200",
                                        ContentAsUTF8 = Resources.Resource.index
                                    };
                                }
                                else
                                {
                                    return new HttpResponse()
                                    {
                                        StatusCode = "404"
                                    };
                                }
                            }
                        }
                    });

                    server.Listen();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: {e.Message} (Restarting)");
                }
            }

        }
    }
}
