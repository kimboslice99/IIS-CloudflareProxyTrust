using System;
using System.IO;
using System.Web;
using System.Configuration;
using NetTools;
using System.Net;
using System.Diagnostics;
using System.Threading;

namespace CloudflareProxyTrust
{
    public class IPCheck : IHttpModule
    {
        private static string[] _cfips;
        private static Timer _timer;

        #region IHttpModule implementation

        public void Dispose() {  }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += new EventHandler(Begin);

            LoadCfipsData();

            // Start a timer to reload cfips data periodically
            int reloadInterval = GetReloadInterval(); // Get the reload interval from configuration
            if(_timer  == null)
                _timer = new Timer(ReloadCfipsData, null, TimeSpan.Zero, TimeSpan.FromHours(reloadInterval));
        }

        public void Begin(Object source, EventArgs e)
        {
            try
            {
                HttpApplication app = (HttpApplication)source;
                HttpRequest request = app.Context.Request;

                // Check if cfips data is loaded into memory
                if (_cfips == null)
                {
                    // If not loaded, load it
                    LoadCfipsData();
                }

                if (_cfips == null ||
                    string.IsNullOrEmpty(request.ServerVariables["HTTP_CF_CONNECTING_IP"]))
                {
                    return;
                }

                // set current REMOTE_ADDR to variable and parse into IPAddress
                IPAddress remoteaddr = IPAddress.Parse(request.ServerVariables["REMOTE_ADDR"]);

                bool trusted = false;
                string cf_connecting_ip = request.ServerVariables["HTTP_CF_CONNECTING_IP"];

                // now we have to parse ranges into addresses, and loop through them
                foreach (string cfip in _cfips)
                {
                    // Dont try to parse empty lines
                    if (string.IsNullOrWhiteSpace(cfip))
                        continue;

                    IPAddressRange IPRange = IPAddressRange.Parse(cfip);
                    if (IPRange.Contains(remoteaddr))
                    {
                        trusted = true;
                        // only if trusted we will forward the real IP in REMOTE_ADDR
                        request.ServerVariables.Set("REMOTE_ADDR", cf_connecting_ip);
                        // maybe you want the cloudflare IP? keep it here
                        request.ServerVariables.Set("HTTP_X_ORIGINAL_ADDR", remoteaddr.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CloudflareProxyTrust]: " + ex);
            }
#if DEBUG
        Debug.WriteLine("[CloudflareProxyTrust] trusted: " + trusted.ToString() + " remoteaddr:" + remoteaddr + " cfip:" + cf_connecting_ip + " url:" + request.RawUrl + " host:" + request.ServerVariables["HTTP_HOST"]);
#endif
        }

        private static void LoadCfipsData()
        {
            // Only load cfips data if it hasn't been loaded before
            if (_cfips == null)
            {
                var path = ConfigurationManager.AppSettings["CF_IP_Path"];

                if (!string.IsNullOrEmpty(path))
                {
                    // Read lines into string array and store in cfips
                    _cfips = File.ReadAllLines(path);
                }
            }
        }

        private static int GetReloadInterval()
        {
            // Get the reload interval from configuration, default to 24 hours
            int interval;
            if (!int.TryParse(ConfigurationManager.AppSettings["CF_IP_ReloadInterval"], out interval))
            {
                interval = 24; // Default reload interval (in hours)
            }
            return interval;
        }

        private static void ReloadCfipsData(object state)
        {
            // Reload cfips data periodically
            LoadCfipsData();
        }
        #endregion
    }
}
