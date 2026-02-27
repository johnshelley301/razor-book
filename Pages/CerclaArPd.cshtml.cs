using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PubSearchSite.Models;
using PubSearchSite.Services;

namespace PubSearchSite.Pages;

/// <summary>
/// Page model for the CERCLA AR/PD tab.
/// </summary>
public sealed class CerclaArPdModel : PageModel
{
    private readonly ILogger<CerclaArPdModel> _logger;
    private readonly ILocationService _locationService;

    public IReadOnlyList<string> Locations { get; private set; } = Array.Empty<string>();
    public string ActiveTab => "CERCLA";
    public string? SelectedSite { get; private set; }

    public CerclaArPdModel(ILogger<CerclaArPdModel> logger, ILocationService locationService)
    {
        _logger = logger;
        _locationService = locationService;
    }

    public async Task OnGetAsync(string? site)
    {
        Locations = await _locationService.GetCerclaSitesAsync(HttpContext.RequestAborted);
        
        // If a site is provided via query string, validate it exists in the list
        if (!string.IsNullOrWhiteSpace(site))
        {
            SelectedSite = Locations.FirstOrDefault(l => l.Equals(site, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task<IActionResult> OnGetArIndexAsync(string site)
    {
        if (string.IsNullOrWhiteSpace(site))
        {
            return new JsonResult(Array.Empty<CerclaArIndexItem>());
        }

        var items = await _locationService.GetCerclaArIndexAsync(site, HttpContext.RequestAborted);
        return new JsonResult(items);
    }

    public async Task<IActionResult> OnGetSiteDocumentsAsync(string site)
    {
        if (string.IsNullOrWhiteSpace(site))
        {
            return new JsonResult(Array.Empty<CerclaSiteDocumentItem>());
        }

        var items = await _locationService.GetCerclaSiteDocumentsAsync(site, HttpContext.RequestAborted);
        return new JsonResult(items);
    }
}
