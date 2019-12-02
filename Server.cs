using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TriggerScan
{
    interface ILogger
    {
        void Log(string message);
    }

    class Server : IDisposable
    {
        ManualResetEvent finishRequest_ = new ManualResetEvent(false);
        ManualResetEvent finished_ = new ManualResetEvent(false);
        Task task_;
        HttpListener httpListener_;
        string lastMessage_;

        private bool ProcessClient(HttpListenerContext context, Properties.Settings settings, Scanner scanner, ILogger logger)
        {
            EventHandler<Scanner.LogEventArgs> onMessage = null;

            Action<string> sendMessage = (str) =>
            {
                try
                {
                    lock (context)
                    {
                        var data = Encoding.UTF8.GetBytes($"data: {str}\r\n\r\n");
                        context.Response.OutputStream.Write(data, 0, data.Length);
                        context.Response.OutputStream.Flush();
                        lock (task_)
                        {
                            lastMessage_ = str;
                        }
                    }
                }
                catch (Exception)
                {
                    scanner.LogMessage -= onMessage;
                }
            };
            onMessage = (s, e) => sendMessage(e.Message);

            context.Response.StatusCode = 200;
            if (context.Request.RawUrl == "/")
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                using (var sw = new StreamWriter(context.Response.OutputStream, Encoding.UTF8))
                {
                    sw.Write(Resources.Resource.index);
                }
            }
            else if (context.Request.RawUrl == "/scan")
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                scanner.Go(settings.Device, settings.DestPath, settings.Dpi, settings.WidthMM, settings.HeightMM);

                var data = Encoding.UTF8.GetBytes($"OK\t\n");
                context.Response.OutputStream.Write(data, 0, data.Length);
            }
            else if (context.Request.RawUrl == "/status")
            {
                context.Response.ContentType = "text/event-stream";
                scanner.LogMessage += onMessage;
                sendMessage(lastMessage_);
                return true; // Keep stream opened
            }
            else if (context.Request.RawUrl == "/lastproduction")
            {
                context.Response.ContentType = "image/jpeg";
                context.Response.AddHeader("Cache-control", "no-cache");
                using (var file = File.OpenRead(scanner.LastProductionFileName))
                {
                    file.CopyTo(context.Response.OutputStream);
                }
            }
            else
            {
                context.Response.StatusCode = 404;
            }

            return false;
        }

        public Server(ILogger logger)
        {
            task_ = Task.Factory.StartNew(() =>
            {
                var scanner = new Scanner();


                scanner.LogMessage += (s, e) =>
                {
                    logger.Log(e.Message);
                };

                while (finishRequest_.WaitOne(0) == false)
                {
                    try
                    {
                        var settings = Properties.Settings.Default;

                        httpListener_ = new HttpListener();
                        httpListener_.Prefixes.Add($"http://*:{settings.Port}/");

                        var lastMessage = string.Empty;

                        httpListener_.Start();

                        logger.Log("Listening on port: 80");

                        for (;;)
                        {
                            var asyncResult = httpListener_.BeginGetContext((result) =>
                            {
                                bool keepOpen = false;
                                HttpListenerContext context = null;
                                try
                                {
                                    context = httpListener_.EndGetContext(result);
                                    keepOpen = ProcessClient(context, settings, scanner, logger);
                                }
                                catch (Exception e)
                                {
                                    logger.Log($"Error in ProcessClient> {e.Message}");
                                }
                                finally
                                {
                                    if (keepOpen == false)
                                    {
                                        context?.Response.Close();
                                    }
                                }
                            }, null);

                            int waitId = WaitHandle.WaitAny(new WaitHandle[] { finishRequest_, asyncResult.AsyncWaitHandle });

                            if (waitId == 0)
                            {
                                httpListener_.Close();
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log($"ERROR: {e.Message} (Restarting)");
                        Thread.Sleep(1000);
                    }
                    finally
                    {
                        httpListener_.Close();
                    }
                }

                scanner.Dispose();
                finished_.Set();
            });
        }

        public void Dispose()
        {
            httpListener_.Stop();
            finishRequest_.Set();
            finished_.WaitOne();
        }
    }
}
