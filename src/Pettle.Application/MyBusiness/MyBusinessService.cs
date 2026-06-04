using Pettle.Application.Clients;
using Pettle.Domain.MyBusiness;

namespace Pettle.Application.MyBusiness;

public record TenantProfileDto(Guid Id, string Name, string Slug, string? LogoUrl, string? PrimaryColor, string? SecondaryColor, string? AccentColor,
    string? Currency, string? Locale, string? TimeZone, int IdleSessionMinutes);
public record UpdateTenantProfileRequest(string Name, string? LogoUrl, string? PrimaryColor, string? SecondaryColor, string? AccentColor,
    string? Currency, string? Locale, string? TimeZone, int IdleSessionMinutes);

public record ServiceItemListItem(Guid Id, string Name, string Vertical, string? CategoryName, decimal BasePrice, decimal? TaxPercent, bool IsActive);
public record CreateOrUpdateServiceRequest(string Name, string? Description, string Vertical, Guid? CategoryId, decimal BasePrice, decimal? TaxPercent, Guid? TaxId, int? DurationMinutes, bool IsActive);

public record StaffListItem(Guid Id, string Name, string? RoleLabel, string? Vertical, string? Phone, string? Email, bool IsActive);
public record CreateOrUpdateStaffRequest(string Name, string? Phone, string? Email, string? RoleLabel, string? Vertical, Guid? UserId, bool IsActive);

public record TaxListItem(Guid Id, string Name, TaxKind Kind, decimal Percent, bool IsInclusive, DateOnly EffectiveFrom, bool IsActive);
public record CreateOrUpdateTaxRequest(string Name, TaxKind Kind, decimal Percent, bool IsInclusive, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive);

public record ClientTagListItem(Guid Id, string Name, string? Color, string? Description, int Usage);
public record CreateOrUpdateClientTagRequest(string Name, string? Color, string? Description);

public record AccessUserListItem(
    Guid Id, string Email, string DisplayName, string? PhoneNumber,
    bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt,
    Guid? RoleId, string? RoleName, Guid? BranchId, string? BranchName
);
public record AccessRoleOption(Guid Id, string Name, string? Description, bool IsSystemRole);
public record AccessBranchOption(Guid Id, string Name);
public record AccessLookups(IReadOnlyList<AccessRoleOption> Roles, IReadOnlyList<AccessBranchOption> Branches);
public record InviteUserRequest(string Email, string DisplayName, string? PhoneNumber, Guid RoleId, Guid BranchId);
public record InviteUserResponse(Guid UserId, string Email, string TemporaryPassword);
public record UpdateUserRoleRequest(Guid RoleId, Guid? BranchId);

/// <summary>Wraps any JSON-serializable settings payload for a tenant setting group.</summary>
public record TenantSettingDto(string Key, System.Text.Json.JsonElement Value, DateTimeOffset UpdatedAt);

public record KennelUnitDto(Guid Id, string Name, int Capacity, bool IsActive, int SortOrder);
public record KennelGroupDto(Guid Id, string Name, string? Color, int SortOrder, IReadOnlyList<KennelUnitDto> Kennels);
public record CreateOrUpdateKennelGroupRequest(string Name, string? Color, int? SortOrder);
public record CreateOrUpdateKennelUnitRequest(string Name, int Capacity, bool IsActive, int? SortOrder);

public record IntakeFormListItem(Guid Id, string Name, string Type, string? Description, bool IsActive, int FieldCount);
public record IntakeFormDto(Guid Id, string Name, string Type, string? Description, bool IsActive, System.Text.Json.JsonElement Fields);
public record CreateOrUpdateIntakeFormRequest(string Name, string Type, string? Description, bool IsActive, System.Text.Json.JsonElement Fields);

public interface IMyBusinessService
{
    Task<TenantProfileDto?> GetProfileAsync(CancellationToken ct = default);
    Task<TenantProfileDto?> UpdateProfileAsync(UpdateTenantProfileRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<ServiceItemListItem>> ListServicesAsync(CancellationToken ct = default);
    Task<ServiceItemListItem> CreateServiceAsync(CreateOrUpdateServiceRequest req, CancellationToken ct = default);
    Task<ServiceItemListItem?> UpdateServiceAsync(Guid id, CreateOrUpdateServiceRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<StaffListItem>> ListStaffAsync(CancellationToken ct = default);
    Task<StaffListItem> CreateStaffAsync(CreateOrUpdateStaffRequest req, CancellationToken ct = default);
    Task<StaffListItem?> UpdateStaffAsync(Guid id, CreateOrUpdateStaffRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<TaxListItem>> ListTaxesAsync(CancellationToken ct = default);
    Task<TaxListItem> CreateTaxAsync(CreateOrUpdateTaxRequest req, CancellationToken ct = default);
    Task<TaxListItem?> UpdateTaxAsync(Guid id, CreateOrUpdateTaxRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<ClientTagListItem>> ListClientTagsAsync(CancellationToken ct = default);
    Task<ClientTagListItem> CreateClientTagAsync(CreateOrUpdateClientTagRequest req, CancellationToken ct = default);
    Task<ClientTagListItem?> UpdateClientTagAsync(Guid id, CreateOrUpdateClientTagRequest req, CancellationToken ct = default);
    Task<bool> DeleteClientTagAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AccessUserListItem>> ListUsersAsync(CancellationToken ct = default);
    Task<AccessLookups> GetAccessLookupsAsync(CancellationToken ct = default);
    Task<InviteUserResponse> InviteUserAsync(InviteUserRequest req, CancellationToken ct = default);
    Task<bool> SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default);
    Task<bool> ChangeUserRoleAsync(Guid userId, UpdateUserRoleRequest req, CancellationToken ct = default);

    Task<TenantSettingDto> GetSettingAsync(string key, CancellationToken ct = default);
    Task<TenantSettingDto> SetSettingAsync(string key, System.Text.Json.JsonElement value, CancellationToken ct = default);

    Task<IReadOnlyList<KennelGroupDto>> ListKennelGroupsAsync(CancellationToken ct = default);
    Task<KennelGroupDto> CreateKennelGroupAsync(CreateOrUpdateKennelGroupRequest req, CancellationToken ct = default);
    Task<KennelGroupDto?> UpdateKennelGroupAsync(Guid id, CreateOrUpdateKennelGroupRequest req, CancellationToken ct = default);
    Task<bool> DeleteKennelGroupAsync(Guid id, CancellationToken ct = default);
    Task<KennelUnitDto?> CreateKennelUnitAsync(Guid groupId, CreateOrUpdateKennelUnitRequest req, CancellationToken ct = default);
    Task<KennelUnitDto?> UpdateKennelUnitAsync(Guid groupId, Guid unitId, CreateOrUpdateKennelUnitRequest req, CancellationToken ct = default);
    Task<bool> DeleteKennelUnitAsync(Guid groupId, Guid unitId, CancellationToken ct = default);

    Task<IReadOnlyList<IntakeFormListItem>> ListIntakeFormsAsync(CancellationToken ct = default);
    Task<IntakeFormDto?> GetIntakeFormAsync(Guid id, CancellationToken ct = default);
    Task<IntakeFormDto> CreateIntakeFormAsync(CreateOrUpdateIntakeFormRequest req, CancellationToken ct = default);
    Task<IntakeFormDto?> UpdateIntakeFormAsync(Guid id, CreateOrUpdateIntakeFormRequest req, CancellationToken ct = default);
    Task<bool> DeleteIntakeFormAsync(Guid id, CancellationToken ct = default);
}
