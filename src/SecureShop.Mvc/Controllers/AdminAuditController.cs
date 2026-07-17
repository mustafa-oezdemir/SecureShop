using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[Route("admin/audit")]
public sealed class AdminAuditController : Controller
{
    private readonly IAuditApiService _auditApiService;

    public AdminAuditController(
        IAuditApiService auditApiService)
    {
        _auditApiService = auditApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _auditApiService.GetLatestAsync(
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            ViewData["ErrorMessage"] = result.ErrorMessage;
        }

        return View(result.Data ?? []);
    }
}
