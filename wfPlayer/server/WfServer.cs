using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace wfPlayer.server
{
    public class WfServer   : IDisposable
    {
        public delegate bool InvokeCommandProc(string cmd);

        private InvokeCommandProc mInvoker;
        private HttpListener mListener;
        private int mPort;

        private Regex mRegex = new Regex(@"/wfplayer/cmd/(?<cmd>[a-zA-Z]+)(/(?<param>\w*))?");

        public static WfServer CreateInstance(InvokeCommandProc invoker, int port=80)
        {
            return new WfServer(invoker, port);
        }

        public WfServer(InvokeCommandProc invoker, int port)
        {
            mInvoker = invoker;
            mPort = port;
        }

        public void Stop()
        {
            mListener.Stop();
        }

        public void Dispose()
        {
            mListener?.Close();
        }

        public void Start()
        {
            mListener = new HttpListener();
            mListener.Prefixes.Add($"http://*/");
            mListener.Start();
            Task.Run( async () =>
            {
                await Run();
            });
        }

        private async Task Run()
        {
            while(mListener.IsListening)
            {
                try
                {
                    var ctx = await mListener.GetContextAsync();
                    var req = ctx.Request;
                    var uri = req.RawUrl;
                    var match = mRegex.Match(uri.ToString());
                    if(match.Success)
                    {
                        var cmd = match.Groups["cmd"].Value;
                        bool result = mInvoker?.Invoke(cmd) ?? false;
                        var buff = Encoding.UTF8.GetBytes($"{{'cmd'='{cmd}','result'='{result}'}}");
                        using (var res = ctx.Response)
                        {
                            await res.OutputStream.WriteAsync(buff, 0, buff.Length);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"HttpListener error: {e.ToString()}");
                    break;
                }
            }
            mListener.Close();
            mListener = null;
        }
    }
}
