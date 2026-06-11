using Microsoft.AspNetCore.Mvc;
using Pettle.Api.Authorization;
using Pettle.Application.MyBusiness;
using Pettle.Domain.Identity;

namespace Pettle.Api.Controllers;

[ApiController]
[Route("api/my-business")]
public class MyBusinessController : ControllerBase
{
    private readonly IMyBusinessService _svc;
    public MyBusinessController(IMyBusinessService svc) => _svc = svc;

    [HttpGet("profile")]
    [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var r = await _svc.GetProfileAsync(ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("profile")]
    [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateTenantProfileRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateProfileAsync(req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("services")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> Services(CancellationToken ct) => Ok(await _svc.ListServicesAsync(ct));

    [HttpGet("services/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> GetService(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetServiceAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("services")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> CreateService([FromBody] CreateOrUpdateServiceRequest req, CancellationToken ct)
        => Ok(await _svc.CreateServiceAsync(req, ct));

    [HttpPut("services/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> UpdateService(Guid id, [FromBody] CreateOrUpdateServiceRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateServiceAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("services/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Delete)]
    public async Task<IActionResult> DeleteService(Guid id, CancellationToken ct)
        => await _svc.DeleteServiceAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("service-categories")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> ServiceCategories(CancellationToken ct) => Ok(await _svc.ListServiceCategoriesAsync(ct));

    [HttpPost("service-categories")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> CreateServiceCategory([FromBody] CreateServiceCategoryRequest req, CancellationToken ct)
        => Ok(await _svc.CreateServiceCategoryAsync(req, ct));

    [HttpGet("staff")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> Staff(CancellationToken ct) => Ok(await _svc.ListStaffAsync(ct));

    [HttpPost("staff")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateOrUpdateStaffRequest req, CancellationToken ct)
        => Ok(await _svc.CreateStaffAsync(req, ct));

    [HttpPut("staff/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] CreateOrUpdateStaffRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateStaffAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("staff/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Delete)]
    public async Task<IActionResult> DeleteStaff(Guid id, CancellationToken ct)
        => await _svc.DeleteStaffAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("taxes")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> Taxes(CancellationToken ct) => Ok(await _svc.ListTaxesAsync(ct));

    [HttpPost("taxes")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> CreateTax([FromBody] CreateOrUpdateTaxRequest req, CancellationToken ct)
        => Ok(await _svc.CreateTaxAsync(req, ct));

    [HttpPut("taxes/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> UpdateTax(Guid id, [FromBody] CreateOrUpdateTaxRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateTaxAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("taxes/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Delete)]
    public async Task<IActionResult> DeleteTax(Guid id, CancellationToken ct)
        => await _svc.DeleteTaxAsync(id, ct) ? NoContent() : NotFound();

    // ---- Client Tags ----
    [HttpGet("client-tags")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> ClientTags(CancellationToken ct) => Ok(await _svc.ListClientTagsAsync(ct));

    [HttpPost("client-tags")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> CreateClientTag([FromBody] CreateOrUpdateClientTagRequest req, CancellationToken ct)
        => Ok(await _svc.CreateClientTagAsync(req, ct));

    [HttpPut("client-tags/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> UpdateClientTag(Guid id, [FromBody] CreateOrUpdateClientTagRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateClientTagAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("client-tags/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Delete)]
    public async Task<IActionResult> DeleteClientTag(Guid id, CancellationToken ct)
        => await _svc.DeleteClientTagAsync(id, ct) ? NoContent() : NotFound();

    // ---- Access Management ----
    [HttpGet("access")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> ListUsers(CancellationToken ct) => Ok(await _svc.ListUsersAsync(ct));

    [HttpGet("access/lookups")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> AccessLookups(CancellationToken ct) => Ok(await _svc.GetAccessLookupsAsync(ct));

    [HttpPost("access/invite")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest req, CancellationToken ct)
        => Ok(await _svc.InviteUserAsync(req, ct));

    [HttpPatch("access/{userId:guid}/active")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> SetActive(Guid userId, [FromBody] SetActiveRequest req, CancellationToken ct)
        => await _svc.SetUserActiveAsync(userId, req.IsActive, ct) ? NoContent() : NotFound();

    [HttpPatch("access/{userId:guid}/role")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> ChangeRole(Guid userId, [FromBody] UpdateUserRoleRequest req, CancellationToken ct)
        => await _svc.ChangeUserRoleAsync(userId, req, ct) ? NoContent() : NotFound();

    // ---- Settings (per-group key→JSON store) ----
    [HttpGet("settings/{key:regex(" + MyBusinessSettingsEndpoints.AllowedKeyRegex + ")}")]
    [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> GetSetting(string key, CancellationToken ct)
        => Ok(await _svc.GetSettingAsync(key, ct));

    [HttpPut("settings/{key:regex(" + MyBusinessSettingsEndpoints.AllowedKeyRegex + ")}")]
    [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> PutSetting(string key, [FromBody] System.Text.Json.JsonElement value, CancellationToken ct)
        => Ok(await _svc.SetSettingAsync(key, value, ct));

    // ---- Kennel Groups ----
    [HttpGet("kennel-groups")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> KennelGroups(CancellationToken ct) => Ok(await _svc.ListKennelGroupsAsync(ct));

    [HttpPost("kennel-groups")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> CreateKennelGroup([FromBody] CreateOrUpdateKennelGroupRequest req, CancellationToken ct)
        => Ok(await _svc.CreateKennelGroupAsync(req, ct));

    [HttpPut("kennel-groups/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> UpdateKennelGroup(Guid id, [FromBody] CreateOrUpdateKennelGroupRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateKennelGroupAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("kennel-groups/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Delete)]
    public async Task<IActionResult> DeleteKennelGroup(Guid id, CancellationToken ct)
        => await _svc.DeleteKennelGroupAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("kennel-groups/{groupId:guid}/units")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> CreateKennelUnit(Guid groupId, [FromBody] CreateOrUpdateKennelUnitRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateKennelUnitAsync(groupId, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("kennel-groups/{groupId:guid}/units/{unitId:guid}")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> UpdateKennelUnit(Guid groupId, Guid unitId, [FromBody] CreateOrUpdateKennelUnitRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateKennelUnitAsync(groupId, unitId, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("kennel-groups/{groupId:guid}/units/{unitId:guid}")] [HasPermission(Modules.MyBusiness, Actions.Delete)]
    public async Task<IActionResult> DeleteKennelUnit(Guid groupId, Guid unitId, CancellationToken ct)
        => await _svc.DeleteKennelUnitAsync(groupId, unitId, ct) ? NoContent() : NotFound();

    // ---- Intake Forms ----
    [HttpGet("forms")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> Forms(CancellationToken ct) => Ok(await _svc.ListIntakeFormsAsync(ct));

    [HttpGet("forms/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.View)]
    public async Task<IActionResult> GetForm(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetIntakeFormAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("forms")] [HasPermission(Modules.MyBusiness, Actions.Create)]
    public async Task<IActionResult> CreateForm([FromBody] CreateOrUpdateIntakeFormRequest req, CancellationToken ct)
        => Ok(await _svc.CreateIntakeFormAsync(req, ct));

    [HttpPut("forms/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Edit)]
    public async Task<IActionResult> UpdateForm(Guid id, [FromBody] CreateOrUpdateIntakeFormRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateIntakeFormAsync(id, req, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("forms/{id:guid}")] [HasPermission(Modules.MyBusiness, Actions.Delete)]
    public async Task<IActionResult> DeleteForm(Guid id, CancellationToken ct)
        => await _svc.DeleteIntakeFormAsync(id, ct) ? NoContent() : NotFound();
}

public record SetActiveRequest(bool IsActive);

public static class MyBusinessSettingsEndpoints
{
    public const string AllowedKeyRegex = @"^(parent-app|push|whatsapp|invoice|printer|occasions|inventory)$";
}
