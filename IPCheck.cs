using System;
using System.IO;
using System.Web;
using System.Configuration;
using NetTools;
using System.Net;
using System.Diagnostics;

namespace CloudflareProxyTrust
{
    public class IPCheck : IHttpModule
    {
        #region IHttpModule implementation

        public void Dispose()
        {
            
        }

        public void Init(HttpApplication context)
        {
                context.BeginRequest += new EventHandler(Begin);
        }

        public void Begin(Object source, EventArgs e)
        {
            HttpApplication app = (HttpApplication)source;
            HttpRequest request = app.Context.Request;

            var path = ConfigurationManager.AppSettings["CF_IP_Path"];

            if ( // some checks
                string.IsNullOrEmpty(path) ||

                string.IsNullOrEmpty(request.ServerVariables["HTTP_CF_CONNECTING_IP"])
                )

                return;
#if DEBUG
            Debug.WriteLine("[CloudflareProxyTrust] start module");
#endif
            // set current REMOTE_ADDR to variable and parse into IPAddress
            IPAddress remoteaddr = IPAddress.Parse(request.ServerVariables["REMOTE_ADDR"]);
            // read lines into string array
            string[] cfips = File.ReadAllLines(path);

            bool trusted = false;
            string cf_connecting_ip = request.ServerVariables["HTTP_CF_CONNECTING_IP"];

            // now we have to parse ranges into addresses, and loop through them
            foreach (string cfip in cfips)
            {
                var IP = IPAddressRange.Parse(cfip);
                if (IP.Contains(remoteaddr))
                {
                    trusted = true;
                    // only if trusted we will forward the real IP in REMOTE_ADDR
                    request.ServerVariables.Set("REMOTE_ADDR", cf_connecting_ip);
                    // maybe you want the cloudflare IP? keep it here
                    request.ServerVariables.Set("HTTP_X_ORIGINAL_ADDR", remoteaddr.ToString());
                }
            }
#if DEBUG
        Debug.WriteLine("[CloudflareProxyTrust] trusted: " + trusted.ToString() + " remoteaddr:" + remoteaddr + " cfip:" + cf_connecting_ip + " url:" + request.RawUrl + " host:" + request.ServerVariables["HTTP_HOST"]);
#endif
                
#if DEBUG
        Debug.WriteLine("[CloudflareProxyTrust] end module");
#endif

        }

        #endregion

    }
}
