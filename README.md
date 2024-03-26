# IIS-CloudflareProxyTrust

This module addresses security concerns related to Cloudflare by ensuring that the `REMOTE_ADDR` variable is replaced with `HTTP_CF_CONNECTING_IP` only when the source is deemed trustworthy.

## Purpose

Utilizing URL rewrite to replace `REMOTE_ADDR` with `HTTP_CF_CONNECTING_IP` might seem like a solution. However, it's important to note that `Cf-Connecting-Ip` is a header that can be spoofed by anyone. Therefore, it should only be relied upon when the source is verifiable.

This module serves to replace the `REMOTE_ADDR` variable with `HTTP_CF_CONNECTING_IP` exclusively if the request originates from a trusted IP address.

## Setup

- Build the Module

- Download Cloudflare IPs
   
Run the following script, perhaps scheduled to execute once a day, to download Cloudflare IPs and save them into a file:

   ```powershell
   $v4 = irm https://www.cloudflare.com/ips-v4 -UseBasicParsing
   $v6 = irm https://www.cloudflare.com/ips-v6 -UseBasicParsing
   "$v4`n$v6" | Out-File -NoNewLine CF_IPs.txt
   ```

## Deployment

   - For deployment to a single site, copy `CloudflareProxyTrust.dll` into your bin folder.
   - For deployment to the entire webserver, add the DLL to the Global Assembly Cache (GAC) and `%SystemDrive%\Windows\System32\inetsrv`.

**Configure IIS**

   Add the following configuration to your web.config file:

   ```xml
   <system.webServer>
       <modules>
           <add name="CloudflareProxyTrust" type="CloudflareProxyTrust.CloudflareIpModule, CloudflareProxyTrust, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7381665d8f939351" preCondition="runtimeVersionv4.0" />
       </modules>
   </system.webServer>
   ```

**Application Setting**

   Create an Application Setting key named `CF_IP_Path` pointing to the file containing Cloudflare's proxy IPs.
