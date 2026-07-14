using Microsoft.EntityFrameworkCore;
using Pettle.Application.Bookings;
using Pettle.Application.Clients;
using Pettle.Application.Common;
using Pettle.Application.Common.Errors;
using Pettle.Domain.Bookings;
using Pettle.Domain.Inventory;
using Pettle.Domain.Invoices;
using Pettle.Domain.Subscriptions;
using Pettle.Infrastructure.Inventory;
using Pettle.Infrastructure.Persistence;

namespace Pettle.Infrastructure.Bookings;

public class BookingServiceImpl : IBookingService
{
    private readonly PettleDbContext _db;
    private readonly ICurrentUser _user;

    public BookingServiceImpl(PettleDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<PagedResult<BookingListItem>> ListAsync(BookingListQuery query, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return new PagedResult<BookingListItem>(Array.Empty<BookingListItem>(), 0, 1, query.PageSize);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = _db.Bookings.AsNoTracking()
            .Include(b => b.PetParent)
            .Include(b => b.Services)
            .Where(b => b.TenantId == _user.TenantId);

        q = query.Tab switch
        {
            "all" => q,
            "active" => q.Where(b => b.Services.Any(s => s.Status == BookingStatus.CheckedIn || s.Status == BookingStatus.Active)),
            "past" => q.Where(b => b.BookingDate < today),
            "noshow" => q.Where(b => b.Services.Any(s => s.Status == BookingStatus.NoShow)),
            "cancelled" => q.Where(b => b.Services.All(s => s.Status == BookingStatus.Cancelled)),
            _ => q.Where(b => b.BookingDate >= today && b.Services.Any(s => s.Status == BookingStatus.Upcoming || s.Status == BookingStatus.Accepted))
        };

        if (query.ServiceType is { } st && Enum.TryParse<BookingServiceType>(st, true, out var typeEnum))
            q = q.Where(b => b.Services.Any(s => s.ServiceType == typeEnum));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(b => (b.PetParent != null && b.PetParent.Name.ToLower().Contains(s))
                || (b.PetParent != null && b.PetParent.Phone.Contains(s))
                || (b.GuestName != null && b.GuestName.ToLower().Contains(s))
                || (b.GuestPhone != null && b.GuestPhone.Contains(s))
                || (b.InvoiceNumber != null && b.InvoiceNumber.ToLower().Contains(s)));
        }

        if (query.FromDate is { } f) q = q.Where(b => b.BookingDate >= f);
        if (query.ToDate is { } t) q = q.Where(b => b.BookingDate <= t);
        if (query.PetParentId is { } pid) q = q.Where(b => b.PetParentId == pid);

        var total = await q.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = await q.OrderByDescending(b => b.BookingDate)
            .Skip((page - 1) * size).Take(size)
            .Select(b => new BookingListItem(
                b.Id, b.LegacyBookingId, b.BookingDate,
                b.PetParent != null ? b.PetParent.Name : (b.GuestName ?? "Walk-in"),
                b.PetParent != null ? b.PetParent.Phone : (b.GuestPhone ?? ""),
                string.Join(", ", b.Services.Select(s => s.ServiceType.ToString()).Distinct()),
                b.PaymentStatus, b.TotalBillingAmount, b.InvoiceNumber, b.Source,
                b.Services.OrderByDescending(s => (int)s.Status).Select(s => s.Status).FirstOrDefault()
            )).ToListAsync(ct);

        return new PagedResult<BookingListItem>(items, total, page, size);
    }

    public async Task<BookingDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var b = await _db.Bookings.AsNoTracking()
            .Include(x => x.PetParent)
            .Include(x => x.Services).ThenInclude(s => s.Pet)
            .Include(x => x.Services).ThenInclude(s => s.AddOns)
            .Include(x => x.BoardingDetails)
            .Include(x => x.GroomingDetails)
            .Include(x => x.VetDetails)
            .Include(x => x.DayCareDetails)
            .Include(x => x.AddOns)
            .Include(x => x.EstimateLines)
            .Include(x => x.ChangeRequests)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (b is null) return null;

        var inv = await _db.Invoices.AsNoTracking()
            .Where(i => i.BookingId == id && i.TenantId == _user.TenantId)
            .Select(i => new { i.Id, i.Paid, i.Due })
            .FirstOrDefaultAsync(ct);

        var subByService = new Dictionary<Guid, BookingSubDetail>();
        foreach (var d in b.BoardingDetails)
            subByService[d.BookingServiceId] = new BookingSubDetail(d.CheckInDate, d.CheckOutDate, d.CheckInTime, d.CheckOutTime, null, d.KennelLabel, d.BoardingType, d.CompanionName);
        foreach (var d in b.GroomingDetails)
            subByService[d.BookingServiceId] = new BookingSubDetail(d.ServiceDate, d.ServiceDate, d.StartTime, d.EndTime, d.StaffName, null, null, null);
        foreach (var d in b.VetDetails)
            subByService[d.BookingServiceId] = new BookingSubDetail(d.ServiceDate, d.ServiceDate, d.StartTime, d.EndTime, d.StaffName, null, null, null);
        foreach (var d in b.DayCareDetails)
            subByService[d.BookingServiceId] = new BookingSubDetail(d.ServiceDate, d.ServiceDate, d.StartTime, d.EndTime, d.StaffName, null, null, null);

        return new BookingDetail(
            b.Id, b.LegacyBookingId, b.BookingDate, b.PetParentId,
            b.PetParent?.Name ?? b.GuestName ?? "Walk-in",
            b.PetParent?.Phone ?? b.GuestPhone ?? "",
            b.PetParent?.Email,
            b.Source, b.PaymentStatus, b.TotalBillingAmount, b.GrossBillingAmount, b.DiscountPercent, b.InvoiceNumber,
            inv?.Id, inv?.Paid ?? 0m, inv?.Due ?? 0m,
            b.Notes, b.AdditionalInstruction,
            b.Services.Select(s => new BookingServiceLine(
                s.Id, s.ServiceType, s.Status, s.PetId,
                s.Pet?.Name ?? s.PetNameSnapshot ?? "(walk-in)",
                s.ServiceName, s.FinalAmount, s.Notes,
                subByService.TryGetValue(s.Id, out var sub) ? sub : null,
                s.SkuId, s.SkuQuantity,
                s.AddOns.Select(a => new ServiceAddOnLine(a.Id, a.Name, a.Price, a.CatalogueItemId)).ToList()
            )).ToList(),
            b.AddOns.Select(a => new BookingAddOnLine(a.Id, a.AddOnService, a.Count, a.Distance, a.Days, a.FinalAmount)).ToList(),
            b.EstimateLines.OrderBy(e => e.SortOrder).Select(e => new BookingEstimateLineDto(e.Id, e.Label, e.Quantity, e.UnitAmount, e.Amount, e.SortOrder)).ToList(),
            b.ChangeRequests.OrderByDescending(c => c.RequestedAt).Select(c => new BookingChangeRequestDto(c.Id, c.Description, c.Status, c.RequestedAt, c.RequestedBy, c.ResolutionNote, c.ResolvedAt)).ToList()
        );
    }

    public async Task<IReadOnlyList<BookingEstimateLineDto>?> SaveEstimateAsync(Guid bookingId, SaveEstimateRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var tid = _user.TenantId.Value;
        var exists = await _db.Bookings.AnyAsync(x => x.Id == bookingId && x.TenantId == tid, ct);
        if (!exists) return null;

        // Replace the whole estimate. Bulk-delete the old rows directly (avoids tracked-graph fix-up),
        // then insert the new ones as standalone entities.
        await _db.BookingEstimateLines.Where(e => e.BookingId == bookingId && e.TenantId == tid).ExecuteDeleteAsync(ct);

        int order = 0;
        foreach (var line in req.Lines ?? Array.Empty<EstimateLineInput>())
        {
            if (string.IsNullOrWhiteSpace(line.Label)) continue;
            var qty = line.Quantity <= 0 ? 1 : line.Quantity;
            _db.BookingEstimateLines.Add(new BookingEstimateLine
            {
                BookingId = bookingId,
                TenantId = tid,
                Label = line.Label.Trim(),
                Quantity = qty,
                UnitAmount = line.UnitAmount,
                Amount = Math.Round(qty * line.UnitAmount, 2, MidpointRounding.AwayFromZero),
                SortOrder = order++,
            });
        }
        await _db.SaveChangesAsync(ct);

        return await _db.BookingEstimateLines.AsNoTracking()
            .Where(e => e.BookingId == bookingId && e.TenantId == tid)
            .OrderBy(e => e.SortOrder)
            .Select(e => new BookingEstimateLineDto(e.Id, e.Label, e.Quantity, e.UnitAmount, e.Amount, e.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<BookingChangeRequestDto?> AddChangeRequestAsync(Guid bookingId, CreateChangeRequestRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var b = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == _user.TenantId, ct);
        if (b is null) return null;
        if (string.IsNullOrWhiteSpace(req.Description))
            throw AppException.Validation("Empty change request",
                new Dictionary<string, string[]> { ["description"] = new[] { "Describe the requested change." } });

        var cr = new BookingChangeRequest
        {
            BookingId = b.Id,
            Description = req.Description.Trim(),
            Status = ChangeRequestStatus.Pending,
            RequestedAt = DateTimeOffset.UtcNow,
            RequestedBy = _user.DisplayName,
        };
        _db.BookingChangeRequests.Add(cr);
        await _db.SaveChangesAsync(ct);
        return new BookingChangeRequestDto(cr.Id, cr.Description, cr.Status, cr.RequestedAt, cr.RequestedBy, cr.ResolutionNote, cr.ResolvedAt);
    }

    public async Task<BookingChangeRequestDto?> ResolveChangeRequestAsync(Guid bookingId, Guid changeRequestId, ResolveChangeRequestRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return null;
        var cr = await _db.BookingChangeRequests
            .FirstOrDefaultAsync(x => x.Id == changeRequestId && x.BookingId == bookingId && x.TenantId == _user.TenantId, ct);
        if (cr is null) return null;
        if (req.Status == ChangeRequestStatus.Pending)
            throw AppException.BusinessRule("Resolve a change request as Approved or Rejected.");
        cr.Status = req.Status;
        cr.ResolutionNote = req.ResolutionNote;
        cr.ResolvedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new BookingChangeRequestDto(cr.Id, cr.Description, cr.Status, cr.RequestedAt, cr.RequestedBy, cr.ResolutionNote, cr.ResolvedAt);
    }

    public async Task<BookingDetail> CreateAsync(CreateBookingRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();

        var isGuest = !req.PetParentId.HasValue;

        // For registered clients: validate ownership
        if (!isGuest)
        {
            var parentOk = await _db.PetParents.AnyAsync(p => p.Id == req.PetParentId && p.TenantId == _user.TenantId, ct);
            if (!parentOk) throw AppException.Validation("Invalid pet parent",
                new Dictionary<string, string[]> { ["petParentId"] = new[] { "Pet parent does not belong to this business." } });

            var petIds = req.Services.Where(s => s.PetId.HasValue).Select(s => s.PetId!.Value).Distinct().ToList();
            if (petIds.Count > 0)
            {
                var validPets = await _db.Pets
                    .Where(p => petIds.Contains(p.Id) && p.PetParentId == req.PetParentId && p.TenantId == _user.TenantId)
                    .Select(p => p.Id).ToListAsync(ct);
                var invalidPets = petIds.Except(validPets).ToList();
                if (invalidPets.Count > 0)
                    throw AppException.Validation("Invalid pet selection",
                        new Dictionary<string, string[]> { ["services"] = new[] { $"{invalidPets.Count} pet(s) do not belong to this parent." } });
            }
        }
        var guestName = isGuest && string.IsNullOrWhiteSpace(req.GuestName) ? "Walk-in Customer" : req.GuestName;

        // Kennel ownership for boarding lines
        var kennelIds = req.Services.Where(s => s.KennelId.HasValue).Select(s => s.KennelId!.Value).Distinct().ToList();
        if (kennelIds.Count > 0)
        {
            var validKennels = await _db.Kennels
                .Where(k => kennelIds.Contains(k.Id) && k.TenantId == _user.TenantId && k.IsActive)
                .Select(k => k.Id).ToListAsync(ct);
            var invalidKennels = kennelIds.Except(validKennels).ToList();
            if (invalidKennels.Count > 0)
                throw AppException.Validation("Invalid kennel",
                    new Dictionary<string, string[]> { ["services"] = new[] { "One or more selected kennels are inactive or unknown." } });
        }

        var parent = isGuest ? null : await _db.PetParents.AsNoTracking()
            .Where(p => p.Id == req.PetParentId && p.TenantId == _user.TenantId)
            .Select(p => new { p.Name, p.Phone })
            .FirstOrDefaultAsync(ct);

        var invNum = await NextBookingInvoiceNumberAsync(ct);

        var b = new Booking
        {
            PetParentId = req.PetParentId,
            GuestName = guestName,
            GuestPhone = req.GuestPhone,
            BookingDate = req.BookingDate,
            Source = req.Source,
            Notes = req.Notes,
            AdditionalInstruction = req.AdditionalInstruction,
            TotalBillingAmount = req.Services.Sum(s => s.FinalAmount + (s.AddOns?.Sum(a => a.Price) ?? 0m)),
            GrossBillingAmount = req.Services.Sum(s => s.FinalAmount + (s.AddOns?.Sum(a => a.Price) ?? 0m)),
            InvoiceNumber = invNum,
        };
        foreach (var line in req.Services)
        {
            var svc = new BookingService
            {
                ServiceType = line.ServiceType,
                PetId = line.PetId,
                PetNameSnapshot = line.PetNameOverride,
                ServiceName = line.ServiceName,
                FinalAmount = line.FinalAmount,
                Notes = line.Notes,
                Status = BookingStatus.Upcoming,
                SkuId = line.SkuId,
                SkuQuantity = line.SkuQuantity > 0 ? line.SkuQuantity : 1,
            };
            b.Services.Add(svc);

            foreach (var ao in line.AddOns ?? [])
                svc.AddOns.Add(new BookingServiceAddOn { Name = ao.Name, Price = ao.Price, CatalogueItemId = ao.CatalogueItemId });

            switch (line.ServiceType)
            {
                case BookingServiceType.Boarding when line.CheckIn is { } ci && line.CheckOut is { } co:
                    b.BoardingDetails.Add(new BoardingDetail { BookingService = svc, CheckInDate = ci, CheckOutDate = co, KennelId = line.KennelId });
                    break;
                case BookingServiceType.Grooming when line.CheckIn is { } d:
                    b.GroomingDetails.Add(new GroomingDetail { BookingService = svc, ServiceDate = d, StartTime = line.StartTime, EndTime = line.EndTime });
                    break;
                case BookingServiceType.Vet when line.CheckIn is { } d:
                    b.VetDetails.Add(new VetDetail { BookingService = svc, ServiceDate = d, StartTime = line.StartTime, EndTime = line.EndTime });
                    break;
                case BookingServiceType.DayCare when line.CheckIn is { } d:
                    b.DayCareDetails.Add(new DayCareDetail { BookingService = svc, ServiceDate = d, StartTime = line.StartTime, EndTime = line.EndTime });
                    break;
            }
        }

        var invoice = new Invoice
        {
            InvoiceNumber = invNum,
            InvoiceType = InvoiceType.Booking,
            InvoiceDate = b.BookingDate,
            BookingId = b.Id,
            PetParentId = b.PetParentId,
            ParentNameSnapshot = parent?.Name ?? b.GuestName ?? "",
            PhoneSnapshot = parent?.Phone ?? b.GuestPhone ?? "",
            Revenue = b.TotalBillingAmount,
            BaseAmount = b.TotalBillingAmount,
            Due = b.TotalBillingAmount,
            Paid = 0,
            PaymentStatus = InvoicePaymentStatus.Pending,
        };
        foreach (var line in req.Services)
        {
            invoice.Lines.Add(new InvoiceLineItem
            {
                BillItemName = line.ServiceName,
                BillSection = line.ServiceType.ToString(),
                Quantity = 1,
                UnitAmount = line.FinalAmount,
                Subtotal = line.FinalAmount,
                Total = line.FinalAmount,
            });
            foreach (var ao in line.AddOns ?? [])
                invoice.Lines.Add(new InvoiceLineItem
                {
                    BillItemName = ao.Name,
                    BillSection = line.ServiceType.ToString() + " Add-on",
                    Quantity = 1,
                    UnitAmount = ao.Price,
                    Subtotal = ao.Price,
                    Total = ao.Price,
                });
        }

        _db.Bookings.Add(b);
        _db.Invoices.Add(invoice);

        // SUB-4/5: Auto-debit from subscription if client has an active subscription — but only
        // for the portion of this booking the package actually covers. Each booked service line
        // is matched by name against the package's included items; a matched item's Discount/
        // DiscountType determines how much of that line is covered (100% Percentage = fully free,
        // 50% = half covered, FlatAmount = up to that ₹ amount). Anything unmatched (a new/extra
        // service not in the package) is left as Due on the invoice for separate payment — the
        // whole point being a subscription should never silently make out-of-plan services free.
        if (req.UseSubscriptionId.HasValue && req.PetParentId.HasValue)
        {
            var sub = await _db.IssuedSubscriptions
                .Include(s => s.Package).ThenInclude(p => p!.Services)
                .FirstOrDefaultAsync(s => s.Id == req.UseSubscriptionId.Value
                    && s.TenantId == _user.TenantId && s.PetParentId == req.PetParentId.Value
                    && s.Status == IssuedSubscriptionStatus.Active, ct);
            if (sub?.Package != null)
            {
                decimal coveredAmount = 0;
                foreach (var line in req.Services)
                {
                    var match = sub.Package.Services
                        .FirstOrDefault(s => string.Equals(s.ServiceName, line.ServiceName, StringComparison.OrdinalIgnoreCase));
                    if (match is null) continue;
                    var portion = match.DiscountType == DiscountType.Percentage
                        ? line.FinalAmount * (match.Discount / 100m)
                        : Math.Min(match.Discount, line.FinalAmount);
                    coveredAmount += Math.Clamp(portion, 0, line.FinalAmount);
                }
                coveredAmount = Math.Round(Math.Min(coveredAmount, invoice.Revenue), 2, MidpointRounding.AwayFromZero);

                if (coveredAmount > 0)
                {
                    sub.BalanceUsed += coveredAmount;
                    if (sub.RemainingSessions > 0) sub.RemainingSessions--;
                    invoice.Paid = coveredAmount;
                    invoice.Due = Math.Max(0, invoice.Revenue - coveredAmount);
                    invoice.PaymentStatus = invoice.Due <= 0.01m
                        ? InvoicePaymentStatus.Paid
                        : InvoicePaymentStatus.PartiallyPaid;
                    _db.Payments.Add(new Payment
                    {
                        InvoiceId = invoice.Id,
                        PaymentTime = DateTimeOffset.UtcNow,
                        Amount = coveredAmount,
                        Mode = PaymentMode.Other,
                        Source = PaymentSource.WalkIn,
                        Type = PaymentType.Balance,
                        Status = PaymentRecordStatus.Success,
                        Notes = coveredAmount + 0.01m >= invoice.Revenue
                            ? $"Auto-debited from subscription: {sub.Id}"
                            : $"Auto-debited from subscription: {sub.Id} (covered ₹{coveredAmount:F2} of ₹{invoice.Revenue:F2} — rest is outside the package)",
                    });
                }
            }
        }

        // Deduct inventory for service lines that have an associated SKU
        var skuIds = req.Services
            .Where(l => l.SkuId.HasValue)
            .Select(l => l.SkuId!.Value)
            .Distinct()
            .ToList();
        if (skuIds.Count > 0)
        {
            var skus = await _db.Skus
                .Where(s => skuIds.Contains(s.Id) && s.TenantId == _user.TenantId)
                .ToListAsync(ct);

            foreach (var line in req.Services.Where(l => l.SkuId.HasValue))
            {
                var sku = skus.FirstOrDefault(s => s.Id == line.SkuId!.Value);
                if (sku is null) continue;
                var qty = line.SkuQuantity > 0 ? line.SkuQuantity : 1;

                if (sku.StockOnHand < qty)
                    throw AppException.Conflict($"Not enough stock for '{sku.Name}' — {sku.StockOnHand} on hand, {qty} requested.");

                // FIFO: deduct from the oldest-received batch first, same as POS sales, so the
                // SKU's batch table stays consistent with what StockOnHand shows.
                await FifoBatchDeductor.DeductAsync(_db, sku.Id, _user.TenantId!.Value, qty, ct);

                sku.StockOnHand = Math.Max(0, sku.StockOnHand - qty);
                _db.Set<StockMovement>().Add(new StockMovement
                {
                    SkuId = sku.Id,
                    TenantId = _user.TenantId!.Value,
                    Reason = StockMovementReason.Sale,
                    QuantityChange = -qty,
                    StockAfter = sku.StockOnHand,
                    Note = $"Booking service: {line.ServiceName}",
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return (await GetAsync(b.Id, ct))!;
    }

    public async Task<bool> ChangeStatusAsync(Guid bookingServiceId, BookingStateChangeRequest req, CancellationToken ct = default)
    {
        if (_user.TenantId is null) throw AppException.Forbidden();
        var s = await _db.BookingServices.FirstOrDefaultAsync(x => x.Id == bookingServiceId && x.TenantId == _user.TenantId, ct);
        if (s is null) return false;

        if (!BookingStateChangeValidator.IsAllowed(s.Status, req.NewStatus))
            throw AppException.BusinessRule($"Can't move this booking from \"{s.Status.Humanize()}\" to \"{req.NewStatus.Humanize()}\".");

        s.Status = req.NewStatus;
        if (!string.IsNullOrWhiteSpace(req.Reason)) s.Notes = (s.Notes is null ? "" : s.Notes + " | ") + $"Status->{req.NewStatus}: {req.Reason}";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var b = await _db.Bookings.Include(x => x.Services).FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);
        if (b is null) return false;
        foreach (var s in b.Services) s.Status = BookingStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason)) b.Notes = (b.Notes is null ? "" : b.Notes + " | ") + $"Cancelled: {reason}";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ApplyDiscountAsync(Guid bookingId, decimal discountPercent, CancellationToken ct = default)
    {
        if (_user.TenantId is null) return false;
        var tid = _user.TenantId.Value;
        var b = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tid, ct);
        if (b is null) return false;
        if (discountPercent < 0 || discountPercent > 100)
            throw AppException.Validation("Invalid discount",
                new Dictionary<string, string[]> { ["discountPercent"] = new[] { "Discount must be between 0 and 100%." } });

        // Older bookings created before GrossBillingAmount existed default to 0 — treat the
        // current TotalBillingAmount as the gross basis the first time a discount is applied.
        if (b.GrossBillingAmount <= 0) b.GrossBillingAmount = b.TotalBillingAmount;

        b.DiscountPercent = discountPercent;
        b.TotalBillingAmount = Math.Round(b.GrossBillingAmount * (1 - discountPercent / 100m), 2, MidpointRounding.AwayFromZero);

        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.BookingId == bookingId && i.TenantId == tid, ct);
        if (invoice is not null)
        {
            invoice.DiscountAmount = Math.Round(b.GrossBillingAmount - b.TotalBillingAmount, 2, MidpointRounding.AwayFromZero);
            invoice.BaseAmount = b.GrossBillingAmount;
            invoice.Revenue = b.TotalBillingAmount;
            invoice.Due = Math.Max(0, b.TotalBillingAmount - invoice.Paid);
            invoice.PaymentStatus = invoice.Due == 0 && invoice.Paid > 0
                ? InvoicePaymentStatus.Paid
                : invoice.Paid > 0 ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Pending;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> NextBookingInvoiceNumberAsync(CancellationToken ct)
    {
        var count = await _db.Invoices.IgnoreQueryFilters()
            .Where(i => i.TenantId == _user.TenantId && i.InvoiceType == InvoiceType.Booking && i.LegacyInvoiceNo == null)
            .CountAsync(ct);
        return $"BKG-{(count + 1).ToString().PadLeft(5, '0')}";
    }
}
