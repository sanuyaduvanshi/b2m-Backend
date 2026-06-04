using Microsoft.EntityFrameworkCore;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Application.Messages;
using Pettle.Domain.Messages;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Messages;

public class MessageTemplateService : IMessageTemplateService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public MessageTemplateService(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<PagedResult<MessageTemplateRow>> ListAsync(
        string? search, MessageChannel? channel, MessageTemplateCategory? category,
        bool activeOnly, int page, int pageSize, CancellationToken ct = default)
    {
        if (_user.TenantId is null)
            return new PagedResult<MessageTemplateRow>(Array.Empty<MessageTemplateRow>(), 0, page, pageSize);

        var q = _db.MessageTemplates.AsNoTracking().Where(t => t.TenantId == _user.TenantId);
        if (activeOnly) q = q.Where(t => t.IsActive);
        if (channel.HasValue) q = q.Where(t => t.Channel == channel.Value);
        if (category.HasValue) q = q.Where(t => t.Category == category.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(t => t.Name.ToLower().Contains(s) || t.Body.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var p = Math.Max(page, 1);
        var sz = Math.Clamp(pageSize, 1, 200);

        var items = await q.OrderBy(t => t.Name)
            .Skip((p - 1) * sz).Take(sz)
            .ToListAsync(ct);

        return new PagedResult<MessageTemplateRow>(items.Select(Map).ToList(), total, p, sz);
    }

    public async Task<MessageTemplateRow?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var t = await _db.MessageTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        return t is null ? null : Map(t);
    }

    public async Task<MessageTemplateRow> CreateAsync(CreateMessageTemplateRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        await EnsureNameUnique(req.Name, null, ct);
        var t = new MessageTemplate
        {
            Name = req.Name.Trim(),
            Channel = req.Channel,
            Category = req.Category,
            Subject = req.Subject?.Trim(),
            Body = req.Body,
            IsActive = req.IsActive,
        };
        _db.MessageTemplates.Add(t);
        await _db.SaveChangesAsync(ct);
        return Map(t);
    }

    public async Task<MessageTemplateRow?> UpdateAsync(Guid id, UpdateMessageTemplateRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var t = await _db.MessageTemplates.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (t is null) return null;
        await EnsureNameUnique(req.Name, id, ct);
        t.Name = req.Name.Trim();
        t.Channel = req.Channel;
        t.Category = req.Category;
        t.Subject = req.Subject?.Trim();
        t.Body = req.Body;
        t.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return Map(t);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var t = await _db.MessageTemplates.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (t is null) return false;
        _db.MessageTemplates.Remove(t); // SoftDeletableTenantEntity → SaveChanges interceptor marks IsDeleted
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task EnsureNameUnique(string name, Guid? excludeId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var exists = await _db.MessageTemplates.AnyAsync(t =>
            t.TenantId == _user.TenantId
            && t.Name.ToLower() == trimmed.ToLower()
            && (excludeId == null || t.Id != excludeId), ct);
        if (exists)
            throw AppException.Validation("Duplicate template name",
                new Dictionary<string, string[]> { ["name"] = new[] { "A template with this name already exists." } });
    }

    private static MessageTemplateRow Map(MessageTemplate t) => new(
        t.Id, t.Name, t.Channel, t.Category, t.Subject, t.Body,
        TemplateVariables.Extract(t.Body),
        t.IsActive, t.CreatedAt, t.UpdatedAt);
}
