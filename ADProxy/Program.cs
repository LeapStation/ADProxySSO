using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Attach the authentication-failed event via configuration of the OpenIdConnectOptions
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Events ??= new OpenIdConnectEvents();
    options.Events.OnAuthenticationFailed = async ctx =>
    {
        try
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var cache = ctx.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
            var errorId = Guid.NewGuid().ToString("N");
            var errorContent = new
            {
                Message = ctx.Exception?.Message,
                Stack = ctx.Exception?.ToString(),
                Path = ctx.HttpContext.Request.Path.ToString(),
                TimeUtc = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(errorContent);
            await cache.SetStringAsync($"error:{errorId}", json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
            logger.LogError(ctx.Exception, "Authentication failed and stored error id {ErrorId}", errorId);
            ctx.Response.Redirect($"/Error?errorid={errorId}");
            ctx.HandleResponse();
        }
        catch (Exception ex)
        {
            var logger = ctx.HttpContext.RequestServices.GetService<ILogger<Program>>();
            logger?.LogError(ex, "Failed while handling authentication failure");
        }
    };
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
