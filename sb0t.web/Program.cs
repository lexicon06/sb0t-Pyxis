using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using sb0t.web.Services;
using sb0t.web.Components;

// Share the same settings identity as the desktop app so both frontends
// read/write the same configuration file.
// AppDomain.FriendlyName for sb0t.exe is "sb0t" (no extension).
iconnect.SettingsStore.Identity = Environment.GetEnvironmentVariable("SB0T_WEB_IDENTITY") is string ident && !string.IsNullOrEmpty(ident)
    ? ident
    : "sb0t";

var baseDir = AppDomain.CurrentDomain.BaseDirectory;
var wwwroot = Path.Combine(baseDir, "wwwroot");
if (!Directory.Exists(wwwroot))
{
    wwwroot = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? baseDir, "wwwroot");
    if (!Directory.Exists(wwwroot))
        wwwroot = Path.Combine(Environment.CurrentDirectory, "wwwroot");
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = baseDir,
    WebRootPath = Directory.Exists(wwwroot) ? wwwroot : null
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ServerService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<ServerAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ServerAuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "sb0t.web";
        options.LoginPath = "/login";
    });
builder.Services.AddCascadingAuthenticationState();

builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

// Print the web panel password on startup so the admin can log in.
// It is generated once on first run and can be changed later in Settings.
using (var scope = app.Services.CreateScope())
{
    var svc = scope.ServiceProvider.GetRequiredService<ServerService>();
    var pwd = svc.WebPassword;
    Console.WriteLine("");
    Console.WriteLine("==================================================");
    Console.WriteLine("  sb0t Pyxis web panel: http://localhost:8080");
    Console.WriteLine("  Login password: " + pwd);
    Console.WriteLine("  (change it later in Settings -> Web password)");
    Console.WriteLine("==================================================");
    Console.WriteLine("");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();

// Serve the Blazor.Flags package static assets (_content/Blazor.Flags/...)
// directly, since the custom WebRootPath bypasses the static web assets
// manifest that would normally map them.
try
{
    var flagsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget", "packages", "blazor.flags", "1.0.0.1", "staticwebassets");
    if (Directory.Exists(flagsDir))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(flagsDir),
            RequestPath = "/_content/Blazor.Flags"
        });
    }
}
catch { }

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Login endpoint: validates the web password and signs the auth cookie.
app.MapPost("/login", async (HttpContext context, ServerService server, Microsoft.AspNetCore.Http.IFormCollection form) =>
{
    var password = form["password"].ToString();
    if (server.CheckPassword(password))
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "owner") }, "sb0t");
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        context.Response.Redirect("/");
    }
    else
    {
        context.Response.Redirect("/login?error=1");
    }
}).DisableAntiforgery();

app.Run();
