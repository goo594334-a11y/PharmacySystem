using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacySystem.Models;
using PharmacySystem.Services.Dashboard;

namespace PharmacySystem.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IDashboardService dashboardService, ILogger<HomeController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboardData = await _dashboardService.GetDashboardDataAsync(cancellationToken);
        return View(dashboardData);
    }

    [AcceptVerbs("GET", "POST")]
    [AllowAnonymous]
    public IActionResult NotFoundPage()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        _logger.LogError("Application error occurred. RequestId: {RequestId}", requestId);

        return View(new ErrorViewModel { RequestId = requestId });
    }
}
