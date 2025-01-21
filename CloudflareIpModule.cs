using System;
using System.IO;
using System.Web;
using System.Configuration;
using NetTools;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;

namespace CloudflareProxyTrust
{
    public class CloudflareIpModule : IHttpModule
    {
        private List<IPAddressRange> _cfips = new List<IPAddressRange>();
        private DateTime _lastupdated = DateTime.MinValue;
        private int count = 0;

        public void Dispose() { }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += new EventHandler(Begin);

            LoadCfipsData();
            _lastupdated = DateTime.Now;
            WriteDbg($"Init {_lastupdated}");
        }

        public void Begin(Object source, EventArgs e)
        {
            try
            {
                HttpApplication app = (HttpApplication)source;
                HttpRequest request = app.Context.Request;
                // stop the loop caused by defaultdocumentmodule
                if (!string.IsNullOrEmpty(request.ServerVariables["CF_SECURE"]))
                {
                    WriteDbg("we handled this already");
                    return;
                }

                // maybe we arent proxied
                var cfConnectingIp = request.ServerVariables["HTTP_CF_CONNECTING_IP"];
                if(AllowNonProxyRemote)
                    if (string.IsNullOrEmpty(cfConnectingIp))
                        return;

                // or maybe we just havent been configured for this site yet
                if (_cfips.Count == 0)
                    return;

                // perhaps iis has been reusing this instance for some time
                // since we have no idea how many times or for how long IIS may reuse an instance, we will keep track of when we last loaded the list
                if (_lastupdated.AddMinutes(20) < DateTime.Now)
                {
                    WriteDbg($"refreshing iplist {request.UserHostAddress}");
                    LoadCfipsData();
                }
#if DEBUG
                else
                    // some verification for our debug logging that we are indeed reusing instances across requests
                    count++;
#endif
                IPAddress remoteaddr = IPAddress.Parse(request.ServerVariables["REMOTE_ADDR"]);
                bool trusted = false;

                foreach (IPAddressRange cfip in _cfips)
                {
                    if (cfip.Contains(remoteaddr))
                    {
                        trusted = true;
                        // only if trusted, forward the real IP in REMOTE_ADDR
                        request.ServerVariables.Set("REMOTE_ADDR", cfConnectingIp);

                        // maybe you want the cloudflare IP? keep it here
                        request.ServerVariables.Set("HTTP_X_ORIGINAL_ADDR", remoteaddr.ToString());

                        // we cant rely on checking if X-Original-Addr exists since a malicious client could send this
                        // it is also useful to stop the defaultdocumentmodule looping
                        request.ServerVariables.Set("CF_SECURE", "true");
                    }
                }
                if(!trusted)
                {
                    // spoofing attempt
                    request.ServerVariables.Set("CF_SECURE", "false");
                    // this ends up logged twice, unsure how to prevent this one
                    EventLog.WriteEntry(".NET Runtime",
                        $"[CloudflareProxyTrust]: possible spoofing attempt\r\n" +
                        $"ip:[{request.ServerVariables["REMOTE_ADDR"]}]\r\n" +
                        $"host:[{request.ServerVariables["SERVER_NAME"]}]\r\n" +
                        $"url:[{request.RawUrl}]\r\n" +
                        $"useragent:[{request.UserAgent}]",
                        EventLogEntryType.Information,
                        1000
                    );
                }
                if (!trusted && DenyUntrusted)
                {
                    app.Response.StatusCode = DenyCode;
                    app.Response.StatusDescription = DenyDescription;
                    app.Response.Flush();
                    app.CompleteRequest();
                }

                WriteDbg($"trusted: {trusted} remoteaddr: {remoteaddr} cfip: {cfConnectingIp} url: {request.RawUrl} host: {request.ServerVariables["HTTP_HOST"]} reused {count} times");
            }
            catch (Exception ex)
            {
                WriteDbg("Exception occured " + ex.Message);
            }
        }

        // this one to deny if untrusted
        private static bool DenyUntrusted => Convert.ToBoolean(ConfigurationManager.AppSettings["CF_DenyUntrusted"]);
        // this one to allow direct connections to the server, only has effect if were denying untrusted.
        private static bool AllowNonProxyRemote => Convert.ToBoolean(ConfigurationManager.AppSettings["CF_AllowNonProxyRemote"] ?? "true");

        private static int DenyCode => Convert.ToInt16(ConfigurationManager.AppSettings["CF_DenyCode"] ?? "400");

        private static string DenyDescription => ConfigurationManager.AppSettings["CF_DenyDescription"] ?? "Bad Request";

        private void LoadCfipsData()
        {
            var path = ConfigurationManager.AppSettings["CF_IP_Path"];
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string[] filelines = File.ReadAllText(path).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in filelines)
                {
                    if (IPAddressRange.TryParse(line, out IPAddressRange ipRange))
                        _cfips.Add(ipRange);
                }
            }
        }

        private static void WriteDbg(string msg)
        {
#if DEBUG
            Debug.WriteLine($"[CloudflareProxyTrust]: {msg}");
#endif
        }
    }
}
