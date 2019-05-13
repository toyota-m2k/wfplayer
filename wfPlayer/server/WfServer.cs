using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer.server
{
    public class WfServer   : IDisposable
    {
        public delegate void InvokeCommandProc(string cmd);

        private InvokeCommandProc mInvoker;
        private HttpListener mListener;
        private int mPort;
        private bool mAlive;

        public static WfServer CreateInstance(InvokeCommandProc invoker, int port=80)
        {
            return new WfServer(invoker, port);
        }

        public WfServer(InvokeCommandProc invoker, int port)
        {
            mInvoker = invoker;
            mPort = port;
        }

        public void Dispose()
        {
            mAlive = false;
        }

        private void Start()
        {
            mListener = new HttpListener();
            mListener.Prefixes.Add($"http://*:{mPort}");
            mListener.Start();
            mAlive = true;
            Run();
        }
        private void Run()
        {
            while(mAlive)
            {
                var ctx = mListener.GetContext();
                var req = ctx.Request;
            }
            mListener.Close();
            mListener = null;
        }
    }
}
