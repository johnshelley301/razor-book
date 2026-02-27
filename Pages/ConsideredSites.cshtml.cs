using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PubSearchSite.Services;

namespace PubSearchSite.Pages;

/// <summary>
/// Page model for the Considered Sites tab.
/// </summary>
public sealed class ConsideredSitesModel : PageModel
{
    private readonly ILogger<ConsideredSitesModel> _logger;
    private readonly ILocationService _locationService;

    public IReadOnlyList<string> Locations { get; private set; } = Array.Empty<string>();
    public string ActiveTab => "Considered";
    public string? SelectedSite { get; private set; }

    public ConsideredSitesModel(ILogger<ConsideredSitesModel> logger, ILocationService locationService)
    {
        _logger = logger;
        _locationService = locationService;
    }

    public async Task OnGetAsync(string? site)
    {
        Locations = await _locationService.GetConsideredSitesAsync(HttpContext.RequestAborted);
        
        // If a site is provided via query string, validate it exists in the list
        if (!string.IsNullOrWhiteSpace(site))
        {
            SelectedSite = Locations.FirstOrDefault(l => l.Equals(site, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task<IActionResult> OnGetSiteDetailsAsync(string filterName)
    {
        if (string.IsNullOrWhiteSpace(filterName))
        {
            return new JsonResult((object?)null);
        }

        var details = await _locationService.GetConsideredSiteDetailsAsync(filterName, HttpContext.RequestAborted);
        return new JsonResult(details);
    }

    public async Task<IActionResult> OnGetSiteDocumentsAsync(string filterName)
    {
        if (string.IsNullOrWhiteSpace(filterName))
        {
            return new JsonResult(Array.Empty<object>());
        }

        var documents = await _locationService.GetConsideredSiteDocumentsAsync(filterName, HttpContext.RequestAborted);
        return new JsonResult(documents);
    }
}
