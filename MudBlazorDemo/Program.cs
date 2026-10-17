using MudBlazor.Services;
using MudBlazorDemo.Client.Pages;
using MudBlazorDemo.Components;
using MudBlazorDemo.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

// Configure Forwarded Headers so the app sees the real client IP.
// Request path: Client -> OpenShift Router (HAProxy) -> oauth2-proxy sidecar -> app.
// X-Forwarded-For arrives as "<client>, <router-ip>", so two trusted hops must be processed.
var forwardedHeadersConfig = builder.Configuration.GetSection("ForwardedHeaders");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = forwardedHeadersConfig.GetValue("ForwardLimit", 2);
    options.RequireHeaderSymmetry = false;
    options.AllowedHosts.Clear(); // Allow all hosts (or restrict as needed)

    // Only trust X-Forwarded-* from the oauth2-proxy sidecar (loopback) and the OpenShift router network.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
    foreach (var cidr in forwardedHeadersConfig.GetSection("KnownNetworks").Get<string[]>() ?? [])
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
    }
});

var app = builder.Build();

// Must run first so HSTS, HTTPS redirection, logging and auth see the real client IP/scheme
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Add custom ProxyAuth middleware (reads X-Remote-User, X-Remote-Email from proxy)
app.UseMiddleware<ProxyAuthMiddleware>();

app.UseRouting();
app.UseAntiforgery();

app.MapControllers();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MudBlazorDemo.Client._Imports).Assembly);

app.Run();
