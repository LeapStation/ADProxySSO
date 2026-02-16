using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

namespace ADProxy.Pages;

public class Error : PageModel
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<Error> _logger;

    public string? ErrorJson { get; private set; }
    public string? ErrorId { get; private set; }

    public Error(IDistributedCache cache, ILogger<Error> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task OnGetAsync([FromQuery(Name = "errorid")] string? errorid)
    {
        ErrorId = errorid;
        if (!string.IsNullOrWhiteSpace(errorid))
        {
            try
            {
                var val = await _cache.GetStringAsync($"error:{errorid}");
                ErrorJson = val;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read error details for {ErrorId}", errorid);
            }
        }
    }
}