using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pettle.Domain.Bookings;
using Pettle.Domain.Clients;
using Pettle.Infrastructure.Persistence;
using static Pettle.MigrationTool.ImportHelpers;

namespace Pettle.MigrationTool;

public class BookingsImporter
{
    private readonly PettleDbContext _db;
    private readonly ILogger<BookingsImporter> _log;

    public BookingsImporter(PettleDbContext db, ILogger<BookingsImporter> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, string xlsxPath, bool dryRun, CancellationToken ct)
    {
        var result = new ImportResult();

        // ----- caches: existing bookings + parents (by phone) + pets (by parent + lowercase name)
        var existingBookings = (await _db.Bookings.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.LegacyBookingId != null)
            .Select(b => new { b.Id, b.LegacyBookingId })
            .ToListAsync(ct))
            .ToDictionary(x => x.LegacyBookingId!, x => x.Id, StringComparer.Ordinal);

        // Key parents by a country-code-agnostic phone key (last 10 digits): the clients file and the
        // bookings sheet in the same export can carry phones with/without the 91 country code.
        var parentsByPhone = (await _db.PetParents.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.Phone != null)
            .Select(p => new { p.Id, p.Phone })
            .ToListAsync(ct))
            .Select(p => new { p.Id, Key = PhoneKey(p.Phone) })
            .Where(p => p.Key.Length > 0)
            .GroupBy(p => p.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var petsList = await _db.Pets.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new { p.Id, p.PetParentId, p.Name })
            .ToListAsync(ct);
        var petsByParentAndName = petsList
            .GroupBy(p => $"{p.PetParentId}|{p.Name.ToLowerInvariant()}", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        _log.LogInformation("Booking cache: {ExistingBookings} bookings, {Parents} parents, {Pets} pets pre-loaded.",
            existingBookings.Count, parentsByPhone.Count, petsList.Count);

        // ===== 1) Bookings header sheet =====
        // Track newly created bookings in this run so child sheets can find them without a SaveChanges.
        var newBookings = new Dictionary<string, Booking>(StringComparer.Ordinal);

        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "Bookings"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;

            var legacyId = row.GetOrNull("Booking ID");
            if (legacyId is null) { result.Inc("skipped_no_booking_id"); continue; }
            if (existingBookings.ContainsKey(legacyId)) { result.Inc("skipped_existing_bookings"); continue; }

            try
            {
                var phone = NormalisePhone(row.Get("Phone"));
                var phoneKey = PhoneKey(row.Get("Phone"));
                if (phoneKey.Length == 0 || !parentsByPhone.TryGetValue(phoneKey, out var parentId))
                {
                    result.Inc("skipped_unknown_parent");
                    continue;
                }

                var bookingDate = ParseDate(row.Get("Booking Date")) ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var booking = new Booking
                {
                    TenantId = tenantId,
                    LegacyBookingId = legacyId,
                    PetParentId = parentId,
                    BookingDate = bookingDate,
                    Source = ParseBookingSource(row.Get("Booking Source")),
                    PaymentStatus = ParseBookingPaymentStatus(row.Get("Payment Status")),
                    TotalBillingAmount = ParseDecimal(row.Get("Total Billing Amount")),
                    InvoiceNumber = row.GetOrNull("Invoice Number"),
                    PhoneSnapshot = phone,
                    EmailSnapshot = row.GetOrNull("Email"),
                    Notes = row.GetOrNull("Notes"),
                    AdditionalInstruction = row.GetOrNull("Additional Instruction"),
                };
                _db.Bookings.Add(booking);
                newBookings[legacyId] = booking;
                existingBookings[legacyId] = booking.Id;
                result.Inc("bookings_created");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Booking row {Row} (#{Legacy}) failed.", row.RowNumber, legacyId);
                result.Errors++;
            }
        }

        // ===== 2) per-service sheets =====
        // We need to attach BookingService + the matching detail entity to each booking.
        // Since EF tracks them, we resolve booking by either newBookings[legacyId] (in this run) or
        // an existing tracked entity via _db.Bookings.Local. For previously-imported bookings we
        // fetch them on first access (rare path — pre-existing bookings still get *additional* services
        // appended if not duplicated).

        var loadedBookings = new Dictionary<string, Booking>(StringComparer.Ordinal);

        async Task<Booking?> ResolveBookingAsync(string legacyId)
        {
            if (newBookings.TryGetValue(legacyId, out var nb)) return nb;
            if (loadedBookings.TryGetValue(legacyId, out var lb)) return lb;
            if (!existingBookings.ContainsKey(legacyId)) return null;
            var booking = await _db.Bookings.IgnoreQueryFilters()
                .Include(b => b.Services)
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.LegacyBookingId == legacyId, ct);
            if (booking != null) loadedBookings[legacyId] = booking;
            return booking;
        }

        Guid? FindPetId(Guid parentId, string? petName)
        {
            if (string.IsNullOrWhiteSpace(petName)) return null;
            return petsByParentAndName.TryGetValue($"{parentId}|{petName.Trim().ToLowerInvariant()}", out var id)
                ? id : null;
        }

        // ----- Boarding sheet -----
        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "Boarding"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;
            var legacyId = row.GetOrNull("Booking ID");
            if (legacyId is null) { result.Inc("boarding_skipped_no_id"); continue; }
            var booking = await ResolveBookingAsync(legacyId);
            if (booking is null) { result.Inc("boarding_skipped_no_booking"); continue; }

            var petId = FindPetId(booking.PetParentId, row.GetOrNull("Pet Name"));
            if (petId is null) { result.Inc("boarding_skipped_no_pet"); continue; }

            var svc = new BookingService
            {
                TenantId = tenantId,
                BookingId = booking.Id,
                Booking = booking,
                ServiceType = BookingServiceType.Boarding,
                PetId = petId.Value,
                Status = ParseBookingStatus(row.Get("Status")),
                ServiceName = row.GetOrNull("Service Name") ?? "Boarding",
                FinalAmount = ParseDecimal(row.Get("Final Amount")),
                Notes = row.GetOrNull("Notes"),
            };
            _db.BookingServices.Add(svc);

            var checkIn = ParseDate(row.Get("Check-In Date")) ?? booking.BookingDate;
            var checkOut = ParseDate(row.Get("Check-Out Date")) ?? checkIn;
            _db.BoardingDetails.Add(new BoardingDetail
            {
                TenantId = tenantId,
                BookingServiceId = svc.Id,
                BookingService = svc,
                BoardingType = row.GetOrNull("Boarding Type"),
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                CheckInTime = ParseTime(row.Get("Check-In Time")),
                CheckOutTime = ParseTime(row.Get("Check-Out Time")),
                CheckInSlot = row.GetOrNull("Check-In Slot"),
                CheckOutSlot = row.GetOrNull("Check-Out Slot"),
                Weight = TryDecimal(row.Get("Weight")),
                CheckOutWeight = TryDecimal(row.Get("Check-Out Weight")),
                MealType = row.GetOrNull("Meal Type"),
                KennelLabel = row.GetOrNull("Kennel"),
                LateCheckoutFees = ParseDecimal(row.Get("Late Checkout Fees")),
                RefundAmount = ParseDecimal(row.Get("Refund Amount")),
                RefundReason = row.GetOrNull("Refund Reason"),
                CompanionName = row.GetOrNull("Companion Name"),
                CompanionPhone = NullIfEmpty(NormalisePhone(row.Get("Companion Phone"))),
            });
            result.Inc("boarding_services_created");
        }

        // ----- Grooming / Vet / Day Care share the same shape -----
        await ImportSimpleServiceSheet(xlsxPath, "Grooming", BookingServiceType.Grooming, ResolveBookingAsync, FindPetId, tenantId, result, ct);
        await ImportSimpleServiceSheet(xlsxPath, "Vet",      BookingServiceType.Vet,      ResolveBookingAsync, FindPetId, tenantId, result, ct);
        await ImportSimpleServiceSheet(xlsxPath, "Day Care", BookingServiceType.DayCare,  ResolveBookingAsync, FindPetId, tenantId, result, ct);

        // ----- Add-On Services -----
        foreach (var row in XlsxReader.ReadSheet(xlsxPath, "Add-On Services"))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;
            var legacyId = row.GetOrNull("Booking ID");
            if (legacyId is null) { result.Inc("addon_skipped_no_id"); continue; }
            var booking = await ResolveBookingAsync(legacyId);
            if (booking is null) { result.Inc("addon_skipped_no_booking"); continue; }
            var name = row.GetOrNull("Add-On Service");
            if (name is null) { result.Inc("addon_skipped_no_name"); continue; }

            _db.BookingAddOns.Add(new BookingAddOn
            {
                TenantId = tenantId,
                BookingId = booking.Id,
                Booking = booking,
                AddOnService = name,
                Count = TryInt(row.Get("Count")) ?? 1,
                Distance = TryDecimal(row.Get("Distance")),
                Days = TryInt(row.Get("Days")),
                FinalAmount = ParseDecimal(row.Get("Final Amount")),
            });
            result.Inc("addons_created");
        }

        if (!dryRun) await _db.SaveChangesAsync(ct);
        return result;
    }

    private async Task ImportSimpleServiceSheet(
        string xlsxPath,
        string sheetName,
        BookingServiceType type,
        Func<string, Task<Booking?>> resolveBooking,
        Func<Guid, string?, Guid?> findPet,
        Guid tenantId,
        ImportResult result,
        CancellationToken ct)
    {
        var keyBase = type.ToString().ToLowerInvariant();
        foreach (var row in XlsxReader.ReadSheet(xlsxPath, sheetName))
        {
            ct.ThrowIfCancellationRequested();
            if (row.AllEmpty()) continue;
            var legacyId = row.GetOrNull("Booking ID");
            if (legacyId is null) { result.Inc($"{keyBase}_skipped_no_id"); continue; }
            var booking = await resolveBooking(legacyId);
            if (booking is null) { result.Inc($"{keyBase}_skipped_no_booking"); continue; }
            var petId = findPet(booking.PetParentId, row.GetOrNull("Pet Name"));
            if (petId is null) { result.Inc($"{keyBase}_skipped_no_pet"); continue; }

            var serviceDate = ParseDate(row.Get("Booking Date")) ?? booking.BookingDate;
            var svc = new BookingService
            {
                TenantId = tenantId,
                BookingId = booking.Id,
                Booking = booking,
                ServiceType = type,
                PetId = petId.Value,
                Status = ParseBookingStatus(row.Get("Status")),
                ServiceName = row.GetOrNull("Services") ?? type.ToString(),
                Notes = row.GetOrNull("Notes"),
            };
            _db.BookingServices.Add(svc);

            switch (type)
            {
                case BookingServiceType.Grooming:
                    _db.GroomingDetails.Add(new GroomingDetail
                    {
                        TenantId = tenantId,
                        BookingServiceId = svc.Id,
                        BookingService = svc,
                        ServiceDate = serviceDate,
                        StartTime = ParseTime(row.Get("Start Time")),
                        EndTime = ParseTime(row.Get("End Time")),
                        StaffName = row.GetOrNull("Staff"),
                        ServicesText = row.GetOrNull("Services"),
                    });
                    break;
                case BookingServiceType.Vet:
                    _db.VetDetails.Add(new VetDetail
                    {
                        TenantId = tenantId,
                        BookingServiceId = svc.Id,
                        BookingService = svc,
                        ServiceDate = serviceDate,
                        StartTime = ParseTime(row.Get("Start Time")),
                        EndTime = ParseTime(row.Get("End Time")),
                        StaffName = row.GetOrNull("Staff"),
                        ServicesText = row.GetOrNull("Services"),
                    });
                    break;
                case BookingServiceType.DayCare:
                    _db.DayCareDetails.Add(new DayCareDetail
                    {
                        TenantId = tenantId,
                        BookingServiceId = svc.Id,
                        BookingService = svc,
                        ServiceDate = serviceDate,
                        StartTime = ParseTime(row.Get("Start Time")),
                        EndTime = ParseTime(row.Get("End Time")),
                        StaffName = row.GetOrNull("Staff"),
                        ServicesText = row.GetOrNull("Services"),
                    });
                    break;
            }

            result.Inc($"{keyBase}_services_created");
        }
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
