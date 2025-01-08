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
    public class CloudflareIpModule : IHttpModule
    {
        private static string[] _cfips;
        private static Timer _timer;

        #region IHttpModule implementation

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            if (_cfips != null)
            {
                _cfips = null;
            }
        }

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

                if(_cfips == null)
                    LoadCfipsData();

                // we may not be proxied, or the path may not be configured for this site
                if (_cfips == null ||
                    string.IsNullOrEmpty(request.ServerVariables["HTTP_CF_CONNECTING_IP"]))
                {
                    return;
                }

                IPAddress remoteaddr = IPAddress.Parse(request.ServerVariables["REMOTE_ADDR"]);
                string cf_connecting_ip = request.ServerVariables["HTTP_CF_CONNECTING_IP"];
                bool trusted = false;

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
#if DEBUG
                Debug.WriteLine($"[CloudflareProxyTrust]: trusted: {trusted.ToString()} remoteaddr: {remoteaddr} cfip: {cf_connecting_ip} url: {request.RawUrl} host: {request.ServerVariables["HTTP_HOST"]}");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine("[CloudflareProxyTrust]: " + ex.Message);
#endif
            }
        }

        private static void LoadCfipsData()
        {
            var path = ConfigurationManager.AppSettings["CF_IP_Path"];

            if (!string.IsNullOrEmpty(path))
            {
                // Read lines into string array and store in cfips
                _cfips = File.ReadAllLines(path);
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
