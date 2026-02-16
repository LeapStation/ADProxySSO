using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADProxy.Models;
using Duende.IdentityModel.Client;
using EnsureThat;
using Flurl.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ADProxy.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDistributedCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _serviceUrl;
    private readonly DistributedCacheEntryOptions _cacheOptions;
    private readonly TokenSettings _tokenSettings;

    public IndexModel(
        ILogger<IndexModel> logger,
        IDistributedCache cache,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _serviceUrl = configuration.GetValue<string>("serviceUrl");
        var cacheMinutes = configuration.GetValue<int>("TokenCacheMinutes", 2);
        _cacheOptions = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(cacheMinutes));
        _tokenSettings = new TokenSettings();
        configuration.Bind("TokenSettings", _tokenSettings);
    }
    
    private async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        var cacheKey = $"$token$clientId={_tokenSettings.ClientId}/$scope={_tokenSettings.ClientScope}";
        var rawData = await _cache.GetAsync(cacheKey, cancellationToken);
        if (rawData != null)
        {
            return Encoding.UTF8.GetString(rawData);
        }
        _logger.LogInformation("renewing token for {clientId} and {scope}", _tokenSettings.ClientId, _tokenSettings.ClientScope);
        var httpClient = _httpClientFactory.CreateClient("token");
        var token = await httpClient.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = _tokenSettings.ClientEndpoint,

                ClientId = _tokenSettings.ClientId,
                ClientSecret = _tokenSettings.ClientSecret,
                Scope = _tokenSettings.ClientScope
            }, cancellationToken: cancellationToken);
        if (token.IsError)
        {
            _logger.LogCritical("Error fetching token: {error}", token.Error);
            if (!string.IsNullOrWhiteSpace(token.ErrorDescription))
            {
                _logger.LogCritical(token.ErrorDescription);
            }
            return null;
        }
        await _cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(token.AccessToken), _cacheOptions, cancellationToken);
        return token.AccessToken;
    }

 public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
{
    try
    {
        var userId =
            User.Claims.FirstOrDefault(x => x.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier");
        if (userId == null)
        {
            userId = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
        }

        if (userId == null)
        {
            _logger.LogWarning("Couldn't get userID from claims");
            _logger.LogWarning(JsonSerializer.Serialize(User.Claims));
        }
        Ensure.Any.IsNotNull(userId);
        var surname = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Surname);
        var firstName = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.GivenName);
        var surnameStr = surname != null ? surname.Value : "";
        var firstnameStr = firstName != null ? firstName.Value : "";
        if (string.IsNullOrEmpty(surnameStr) && string.IsNullOrEmpty(firstnameStr))
        {
            var name = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name || x.Type == "name");
            surnameStr = name?.Value;
        }
        var email = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email || x.Type == "emails")?.Value;
        if (string.IsNullOrEmpty(email))
        {
            if (User.Claims.Any(x => x.Type == "preferred_username"))
            {
                var preferredUsername = User.Claims.First(x => x.Type == "preferred_username");
                if (preferredUsername.Value.Contains("@"))
                {
                    email = preferredUsername.Value;
                }
            }
        }
        if (string.IsNullOrEmpty(surnameStr) && string.IsNullOrEmpty(firstnameStr))
        {
            _logger.LogWarning("Couldn't get userID from claims");
            _logger.LogWarning(JsonSerializer.Serialize(User.Claims));
        }

        if (string.IsNullOrEmpty(email) )
        {
            _logger.LogWarning("Couldn't get email from claims");
            _logger.LogWarning(JsonSerializer.Serialize(User.Claims));
        }
        
        //get the BiB token
        var token = await GetTokenAsync(cancellationToken);
        if (token == null)
        {
            _logger.LogWarning("Failed to get token");
            return Redirect("/Error");
        }

        var adUser = new EpdActorModel()
        {
            DisplayName = $"{firstName.Value} {surname.Value}".Trim(),
            Uid = userId.Value
        };
        var epdAccessModel = new EpdAccessModel()
        {
            Actor = adUser
        };

        var url = await $"{_serviceUrl}/epd/access-ad"
            .WithOAuthBearerToken(token)
            .PostJsonAsync(epdAccessModel, cancellationToken: cancellationToken).ReceiveString();
        
        return Redirect(url);
    }
    catch (Exception ex)
    {
        try
        {
            var errorId = Guid.NewGuid().ToString("N");
            var errorContent = new
            {
                Message = ex.Message,
                Stack = ex.ToString(),
                Path = HttpContext?.Request?.Path.ToString(),
                TimeUtc = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(errorContent);
            await _cache.SetStringAsync($"error:{errorId}", json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }, token: cancellationToken);
            _logger.LogError(ex, "Unhandled exception in Index.OnGet - stored error id {ErrorId}", errorId);
            return Redirect($"/Error?errorid={errorId}");
        }
        catch (Exception inner)
        {
            _logger.LogError(inner, "Failed while handling an exception in Index.OnGet");
            // Fallback: redirect to generic error page without id
            return Redirect("/Error");
        }
    }
}
}