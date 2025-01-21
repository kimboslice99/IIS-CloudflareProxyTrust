# IIS-CloudflareProxyTrust

This module addresses security concerns related to Cloudflare by ensuring that the `REMOTE_ADDR` variable is replaced with `HTTP_CF_CONNECTING_IP` only when the source is deemed trustworthy.

## Purpose

Utilizing URL rewrite to replace `REMOTE_ADDR` with `HTTP_CF_CONNECTING_IP` might seem like a solution. However, it's important to note that `CF-Connecting-IP` is a header that can be spoofed by anyone. Therefore, it should only be relied upon when the source is verifiable.

This module serves to replace the `REMOTE_ADDR` variable with `HTTP_CF_CONNECTING_IP` exclusively if the request originates from a trusted IP address. 

Optionally you may configure this module to allow only connections through CF proxy, or to just block any spoofing attempts.

## Setup

- Build the Module

- Download Cloudflare IPs
   
Run the following script, perhaps scheduled to execute once a day, to download Cloudflare IPs and save them into a file:

   ```powershell
   # note that the use of -UseBasicParsing allows limited user accounts to run irm/iwr!
   $v4 = irm https://www.cloudflare.com/ips-v4 -UseBasicParsing
   $v6 = irm https://www.cloudflare.com/ips-v6 -UseBasicParsing
   "$v4`n$v6" | Out-File -NoNewLine CF_IPs.txt
   ```

## Deployment

   - For deployment to a single site, copy `CloudflareProxyTrust.dll` into your bin folder.
   - For deployment to the entire webserver, add the DLL to the Global Assembly Cache (GAC). You can do so with gacutil or the following (admin) powershell commands.
```powershell
$dllpath = "C:\full\path\to\CloudflareProxyTrust.dll"
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a") | Out-Null
$publish = New-Object System.EnterpriseServices.Internal.Publish
$publish.GacInstall($dllpath)
```

**Configure IIS**

   Add the following configuration to your web.config file:

   ```xml
   <system.webServer>
       <modules>
           <add name="CloudflareProxyTrust" type="CloudflareProxyTrust.CloudflareIpModule, CloudflareProxyTrust, Version=1.1.0.0, Culture=neutral, PublicKeyToken=7381665d8f939351" preCondition="runtimeVersionv4.0" />
       </modules>
   </system.webServer>
   ```

**Settings**

Create an Application Setting key named `CF_IP_Path` pointing to the file containing Cloudflare's proxy IPs, and ensure IIS_IUSRS has read access to this file. This is the only required setting for the module to function.
   
All of the following configuation choices set REMOTE_ADDR, but only some of them block.
Note that for CF_AllowNonProxyRemote=false to function CF_DenyUntrusted must be true

Scenario 1. (blocking mode)

You have a site that is proxied, and it should be the only route a legitimate client takes
Set CF_DenyUntrusted to true and CF_AllowNonProxyRemote to false. This will block all clients that arent coming through the CF proxy.

Scenario 2. (blocking mode)

You have a site that is proxied but also has an unproxied name, so clients can utilize CF or not if chosen.
You wish to block any request that comes with CF headers that is not a CF proxy IP (spoofing attempt).
Set CF_DenyUntrusted to true and CF_AllowNonProxyRemote to true

Scenario 3. (default/silent/non-blocking mode)

You have a site that is proxied and may have unproxied names.
You would like to handle REMOTE_ADDR replacement silently, if it exists and is trusted.
Set CF_DenyUntrusted to false and CF_AllowNonProxyRemote to true (these are the defaults)

Application setting key CF_DenyCode and CF_DenyDescription will allow you to customize the deny response from the default 400 Bad Request.
