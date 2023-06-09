# IIS-CloudflareProxyTrust

This is a simple module to resolve some security concerns with Cloudflare, one might assume its fine to use URL rewrite to replace REMOTE_ADDR with HTTP_CF_CONNECTING_IP 

It's not, CF-CONNECTING-IP is a header anyone can send, so it should only be used if the source is trustworthy 

This Module will simply replace the REMOTE_ADDR variable with HTTP_CF_CONNECTING_IP if from a trusted IP address

## Setup

- Get NuGet package IPAddressRange

- Build

- Download CF ips into a file, perhaps run this as a script once a day

```powershell
$v4 = (iwr https://www.cloudflare.com/ips-v4).Content

$v6 = (iwr https://www.cloudflare.com/ips-v6).Content

Write-Output $v4 $v6 | Out-File CF_IPs.txt
```

- Copy `CloudflareProxyTrust.dll` and `IPAddressRange.dll` into your bin folder if being deployed to just one site or if deploying to entire webserver then youll need to add the dlls to the GAC and `%SystemDrive%\Windows\System32\inetsrv`

```xml
<system.webServer>
    <modules>
        <add name="CloudflareProxyTrust" type="CloudflareProxyTrust.IPCheck, CloudflareProxyTrust, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7381665d8f939351" preCondition="runtimeVersionv4.0" />
    <modules>
<system.webServer>
```

- Create Application Setting key `CF_IP_Path` which needs to point to the file containing Cloudflares proxy IPs
