using SimpleHttpServer;
using SimpleHttpServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace wfPlayer.server
{
    public class WfServerEx : IDisposable
    {
        public delegate bool InvokeCommandProc(string cmd);

        private InvokeCommandProc mInvoker;
        private int mPort;
        private HttpServer mServer;
        private Regex mRegex = new Regex(@"/wfplayer/cmd/(?<cmd>[a-zA-Z]+)(/(?<param>\w*))?");

        public bool IsListening { get; private set; } = false;

        public static WfServerEx CreateInstance(InvokeCommandProc invoker, int port = 80)
        {
            return new WfServerEx(invoker, port);
        }

        public WfServerEx(InvokeCommandProc invoker, int port = 80)
        {
            mInvoker = invoker;
            mPort = port;
            InitRoutes();
        }

        public void Start()
        {
            if (!IsListening)
            {
                if (null == mServer)
                {
                    mServer = new HttpServer(mPort, Routes);
                }
                mServer.Start();
                IsListening = true;
            }
        }

        public void Stop()
        {
            mServer?.Stop();
            IsListening = false;
        }

        public void Dispose()
        {
            Stop();
            mServer = null;
        }


        public List<Route> Routes { get; set; } = null;

        private void InitRoutes()
        {
            if(null==Routes)
            {
                Routes = new List<Route>
                {
                    new Route {
                        Name = "wfPlayer command",
                        UrlRegex = @"^/wfplayer/cmd/.*",
                        Method = "GET",
                        Callable = (HttpRequest request) => {
                            var match = mRegex.Match(request.Url);
                            var result = false;
                            var cmd = "unknown";
                            if (match.Success)
                            {
                                cmd = match.Groups["cmd"].Value;
                                result = mInvoker?.Invoke(cmd) ?? false;
                            }
                            return new HttpResponse()
                            {
                                ContentAsUTF8 = $"{{'cmd'='{cmd}','result'='{result}'}}",
                                ReasonPhrase = "OK",
                                StatusCode = "200"
                            };
                         }
                    },
                };
            }
        }

    }
}
