using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PubSearchSite.Models;
using PubSearchSite.Services;

namespace PubSearchSite.Pages;

/// <summary>
/// Page model for the LM Sites tab.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ILocationService _locationService;

    public IReadOnlyList<string> Locations { get; private set; } = Array.Empty<string>();
    public string ActiveTab => "LM";
    public string? SelectedSite { get; private set; }

    public IndexModel(ILogger<IndexModel> logger, ILocationService locationService)
    {
        _logger = logger;
        _locationService = locationService;
    }

    public async Task OnGetAsync(string? site)
    {
        Locations = await _locationService.GetLmSitesAsync(HttpContext.RequestAborted);
        
        // If a site is provided via query string, validate it exists in the list
        if (!string.IsNullOrWhiteSpace(site))
        {
            // Try exact match first, then case-insensitive match
            SelectedSite = Locations.FirstOrDefault(l => l.Equals(site, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task<IActionResult> OnGetDocumentsAsync(string site)
    {
        if (string.IsNullOrWhiteSpace(site))
        {
            return new JsonResult(new { keyDocuments = Array.Empty<DocumentItem>(), siteDocuments = Array.Empty<DocumentItem>() });
        }

        var result = await _locationService.GetLmDocumentsAsync(site, HttpContext.RequestAborted);
        return new JsonResult(new
        {
            keyDocuments = result.KeyDocuments,
            siteDocuments = result.SiteDocuments
        });
    }
}
