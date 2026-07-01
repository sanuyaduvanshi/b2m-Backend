using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pettle.Application.Common;
using Pettle.Domain.Audit;
using Pettle.Domain.Bookings;
using Pettle.Domain.ClientEnquiries;
using Pettle.Domain.Clients;
using Pettle.Domain.Common;
using Pettle.Domain.DailyTasks;
using Pettle.Domain.Expenses;
using Pettle.Domain.Identity;
using Pettle.Domain.Inventory;
using Pettle.Domain.Invoices;
using Pettle.Domain.Kennels;
using Pettle.Domain.Messages;
using Pettle.Domain.MyBusiness;
using Pettle.Domain.Reminders;
using Pettle.Domain.Subscriptions;
using Pettle.Domain.Tenancy;
using Pettle.Infrastructure.Identity;

namespace Pettle.Infrastructure.Persistence;

public class PettleDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ICurrentUser? _currentUser;

    public PettleDbContext(DbContextOptions<PettleDbContext> options, ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // Tenancy + RBAC + Audit
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Role> AppRoles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();

    // Clients
    public DbSet<PetParent> PetParents => Set<PetParent>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<PetMedicalProfile> PetMedicalProfiles => Set<PetMedicalProfile>();
    public DbSet<PetVaccination> PetVaccinations => Set<PetVaccination>();
    public DbSet<ClientTag> ClientTags => Set<ClientTag>();
    public DbSet<ClientTagAssignment> ClientTagAssignments => Set<ClientTagAssignment>();

    // Bookings
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingService> BookingServices => Set<BookingService>();
    public DbSet<BoardingDetail> BoardingDetails => Set<BoardingDetail>();
    public DbSet<GroomingDetail> GroomingDetails => Set<GroomingDetail>();
    public DbSet<VetDetail> VetDetails => Set<VetDetail>();
    public DbSet<DayCareDetail> DayCareDetails => Set<DayCareDetail>();
    public DbSet<BookingAddOn> BookingAddOns => Set<BookingAddOn>();
    public DbSet<BookingServiceAddOn> BookingServiceAddOns => Set<BookingServiceAddOn>();
    public DbSet<BookingEstimateLine> BookingEstimateLines => Set<BookingEstimateLine>();
    public DbSet<BookingChangeRequest> BookingChangeRequests => Set<BookingChangeRequest>();
    public DbSet<BookingRequest> BookingRequests => Set<BookingRequest>();

    // Invoices
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Inventory
    public DbSet<SkuCategory> SkuCategories => Set<SkuCategory>();
    public DbSet<SkuBrand> SkuBrands => Set<SkuBrand>();
    public DbSet<Sku> Skus => Set<Sku>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<SkuBatch> SkuBatches => Set<SkuBatch>();

    // Kennels
    public DbSet<KennelGroup> KennelGroups => Set<KennelGroup>();
    public DbSet<Kennel> Kennels => Set<Kennel>();
    public DbSet<KennelBlocking> KennelBlockings => Set<KennelBlocking>();

    // Calendar
    public DbSet<Pettle.Domain.Calendar.CalendarAppointment> CalendarAppointments => Set<Pettle.Domain.Calendar.CalendarAppointment>();

    // Expenses
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();

    // Subscriptions
    public DbSet<SubscriptionPackage> SubscriptionPackages => Set<SubscriptionPackage>();
    public DbSet<SubscriptionPackageService> SubscriptionPackageServices => Set<SubscriptionPackageService>();
    public DbSet<IssuedSubscription> IssuedSubscriptions => Set<IssuedSubscription>();

    // Reminders
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<ReminderRule> ReminderRules => Set<ReminderRule>();

    // Daily Tasks
    public DbSet<DailyTaskEntry> DailyTaskEntries => Set<DailyTaskEntry>();

    // Client Enquiries
    public DbSet<ClientEnquiry> ClientEnquiries => Set<ClientEnquiry>();

    // Messages
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    // My Business
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
    public DbSet<ServiceVariant> ServiceVariants => Set<ServiceVariant>();
    public DbSet<ServiceAddOn> ServiceAddOns => Set<ServiceAddOn>();
    public DbSet<AddOnService> AddOnServices => Set<AddOnService>();
    public DbSet<VetCatalogueItem> VetCatalogueItems => Set<VetCatalogueItem>();
    public DbSet<Staff> Staffs => Set<Staff>();
    public DbSet<StaffShift> StaffShifts => Set<StaffShift>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<IntakeForm> IntakeForms => Set<IntakeForm>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("pettle");

        builder.Entity<ApplicationUser>(b => b.ToTable("Users"));
        builder.Entity<ApplicationRole>(b => b.ToTable("IdentityRoles"));

        builder.Entity<Tenant>(b =>
        {
            b.HasIndex(x => x.Slug).IsUnique();
            b.HasMany(x => x.Branches).WithOne(x => x.Tenant!).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Role>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasMany(x => x.RolePermissions).WithOne(x => x.Role!).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RolePermission>(b => b.HasKey(x => new { x.RoleId, x.PermissionKey }));

        builder.Entity<UserBranch>(b =>
        {
            b.HasKey(x => new { x.UserId, x.BranchId, x.RoleId });
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.BranchId);
        });

        builder.Entity<PetParent>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Phone });
            b.HasIndex(x => x.LegacyClientId);
            b.HasMany(x => x.Pets).WithOne(x => x.PetParent!).HasForeignKey(x => x.PetParentId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.WalletBalance).HasPrecision(12, 2);
            b.Property(x => x.OutstandingBalance).HasPrecision(12, 2);
        });

        builder.Entity<Pet>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.PetParentId });
            b.HasIndex(x => x.LegacyPetId);
            b.HasOne(x => x.MedicalProfile).WithOne(x => x.Pet!).HasForeignKey<PetMedicalProfile>(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Vaccinations).WithOne(x => x.Pet!).HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.WeightKg).HasPrecision(6, 2);
        });

        builder.Entity<ClientTagAssignment>(b => b.HasKey(x => new { x.PetParentId, x.ClientTagId }));

        builder.Entity<Booking>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.BookingDate });
            b.HasIndex(x => x.LegacyBookingId);
            b.HasOne(x => x.PetParent).WithMany().HasForeignKey(x => x.PetParentId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Services).WithOne(x => x.Booking!).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.AddOns).WithOne(x => x.Booking!).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.TotalBillingAmount).HasPrecision(12, 2);
        });

        builder.Entity<BookingService>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.FinalAmount).HasPrecision(12, 2);
            b.HasMany(x => x.AddOns).WithOne(x => x.BookingService!).HasForeignKey(x => x.BookingServiceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<BookingServiceAddOn>(b =>
        {
            b.Property(x => x.Price).HasPrecision(12, 2);
        });

        builder.Entity<BoardingDetail>(b =>
        {
            b.HasOne(x => x.BookingService).WithMany().HasForeignKey(x => x.BookingServiceId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.CheckInDate });
            b.Property(x => x.Weight).HasPrecision(6, 2);
            b.Property(x => x.CheckOutWeight).HasPrecision(6, 2);
            b.Property(x => x.LateCheckoutFees).HasPrecision(12, 2);
            b.Property(x => x.RefundAmount).HasPrecision(12, 2);
        });
        builder.Entity<GroomingDetail>(b => b.HasOne(x => x.BookingService).WithMany().HasForeignKey(x => x.BookingServiceId).OnDelete(DeleteBehavior.Cascade));
        builder.Entity<VetDetail>(b => b.HasOne(x => x.BookingService).WithMany().HasForeignKey(x => x.BookingServiceId).OnDelete(DeleteBehavior.Cascade));
        builder.Entity<DayCareDetail>(b => b.HasOne(x => x.BookingService).WithMany().HasForeignKey(x => x.BookingServiceId).OnDelete(DeleteBehavior.Cascade));
        builder.Entity<BookingAddOn>(b => b.Property(x => x.FinalAmount).HasPrecision(12, 2));

        builder.Entity<BookingRequest>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasIndex(x => x.LegacyRequestId);
        });

        builder.Entity<Invoice>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.InvoiceDate });
            b.HasIndex(x => x.LegacyInvoiceNo);
            b.HasMany(x => x.Lines).WithOne(x => x.Invoice!).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Payments).WithOne(x => x.Invoice!).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => !x.IsDeleted);
            foreach (var prop in new[] { nameof(Invoice.BaseAmount), nameof(Invoice.AddOnAmount), nameof(Invoice.DiscountAmount),
                nameof(Invoice.AdditionalAmount), nameof(Invoice.AdditionalDiscountAmount), nameof(Invoice.IgstAmount),
                nameof(Invoice.CgstAmount), nameof(Invoice.SgstAmount), nameof(Invoice.Revenue), nameof(Invoice.Paid), nameof(Invoice.Due) })
                b.Property(prop).HasPrecision(12, 2);
        });

        builder.Entity<InvoiceLineItem>(b =>
        {
            foreach (var prop in new[] { nameof(InvoiceLineItem.Quantity), nameof(InvoiceLineItem.UnitAmount), nameof(InvoiceLineItem.Discount),
                nameof(InvoiceLineItem.Subtotal), nameof(InvoiceLineItem.IgstAmount), nameof(InvoiceLineItem.CgstAmount),
                nameof(InvoiceLineItem.SgstAmount), nameof(InvoiceLineItem.Total) })
                b.Property(prop).HasPrecision(12, 2);
        });

        builder.Entity<Payment>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.PaymentTime });
            b.HasIndex(x => x.LegacyPaymentId);
            b.Property(x => x.Amount).HasPrecision(12, 2);
        });

        builder.Entity<Sku>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            b.HasIndex(x => x.LegacySkuId);
            b.HasQueryFilter(x => !x.IsDeleted);
            foreach (var prop in new[] { nameof(Sku.MrpPrice), nameof(Sku.SellingPrice), nameof(Sku.CostPrice), nameof(Sku.TaxPercent) })
                b.Property(prop).HasPrecision(12, 2);
        });

        builder.Entity<SkuCategory>(b =>
        {
            b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SkuBrand>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<Vendor>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Name });
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.CreditLimit).HasPrecision(12, 2);
        });

        builder.Entity<PurchaseOrder>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.PoNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.PurchaseDate });
            b.HasIndex(x => x.LegacyPoNumber);
            b.HasMany(x => x.Lines).WithOne(x => x.PurchaseOrder!).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => !x.IsDeleted);
            foreach (var prop in new[] { nameof(PurchaseOrder.SubTotal), nameof(PurchaseOrder.TaxAmount), nameof(PurchaseOrder.Adjustment),
                nameof(PurchaseOrder.Total), nameof(PurchaseOrder.Paid), nameof(PurchaseOrder.Due),
                nameof(PurchaseOrder.FlatDiscountPercent), nameof(PurchaseOrder.FlatDiscountAmount), nameof(PurchaseOrder.AdditionalCharges),
                nameof(PurchaseOrder.GrossAmount), nameof(PurchaseOrder.DiscountAmount), nameof(PurchaseOrder.TaxableAmount),
                nameof(PurchaseOrder.RoundOff) })
                b.Property(prop).HasPrecision(12, 2);
        });

        builder.Entity<PurchaseOrderLine>(b =>
        {
            foreach (var prop in new[] { nameof(PurchaseOrderLine.Quantity), nameof(PurchaseOrderLine.ReceivedQuantity),
                nameof(PurchaseOrderLine.UnitCost), nameof(PurchaseOrderLine.TaxPercent), nameof(PurchaseOrderLine.LineTotal),
                nameof(PurchaseOrderLine.FreeQuantity), nameof(PurchaseOrderLine.Mrp), nameof(PurchaseOrderLine.SellingPrice),
                nameof(PurchaseOrderLine.PurDisc1Percent), nameof(PurchaseOrderLine.PurDisc2Percent),
                nameof(PurchaseOrderLine.TaxableAmount), nameof(PurchaseOrderLine.TaxAmount), nameof(PurchaseOrderLine.LandingCost) })
                b.Property(prop).HasPrecision(12, 2);
        });

        builder.Entity<StockMovement>(b => b.HasIndex(x => new { x.TenantId, x.SkuId, x.CreatedAt }));

        builder.Entity<KennelGroup>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasMany(x => x.Kennels).WithOne(x => x.Group).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Kennel>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.PricePerNight).HasPrecision(12, 2);
        });

        builder.Entity<KennelBlocking>(b =>
        {
            b.HasOne(x => x.Kennel).WithMany().HasForeignKey(x => x.KennelId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.KennelId, x.FromDate });
        });

        builder.Entity<Expense>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Time });
            b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.Amount).HasPrecision(12, 2);
            b.Property(x => x.AmountIncTax).HasPrecision(12, 2);
        });

        builder.Entity<SubscriptionPackage>(b =>
        {
            b.HasMany(x => x.Services).WithOne(x => x.Package!).HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.Price).HasPrecision(12, 2);
            b.Property(x => x.TaxPercent).HasPrecision(6, 3);
        });

        builder.Entity<SubscriptionPackageService>(b =>
        {
            b.Property(x => x.Discount).HasPrecision(12, 2);
        });

        builder.Entity<IssuedSubscription>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.PetParentId, x.ValidUntil });
            b.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PetParent).WithMany().HasForeignKey(x => x.PetParentId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.AmountPaid).HasPrecision(12, 2);
            b.Property(x => x.AmountDue).HasPrecision(12, 2);
        });

        builder.Entity<Reminder>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.DueDate, x.Status });
            b.HasIndex(x => new { x.TenantId, x.Type });
        });

        builder.Entity<DailyTaskEntry>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Date });
            b.HasIndex(x => new { x.BookingServiceId, x.Date, x.TaskType }).IsUnique();
            b.HasOne(x => x.BookingService).WithMany().HasForeignKey(x => x.BookingServiceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClientEnquiry>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasIndex(x => new { x.TenantId, x.CreatedAt });
            b.HasIndex(x => x.LegacyEnquiryId);
            b.HasOne(x => x.ConvertedClient).WithMany().HasForeignKey(x => x.ConvertedClientId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MessageTemplate>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Channel, x.Category });
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Conversation>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.PetParentId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LastMessageAt });
            b.HasOne(x => x.PetParent).WithMany().HasForeignKey(x => x.PetParentId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Messages).WithOne(x => x.Conversation!).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Message>(b =>
        {
            b.HasIndex(x => new { x.ConversationId, x.CreatedAt });
            b.HasIndex(x => new { x.TenantId, x.PetParentId, x.CreatedAt });
            b.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ServiceItem>(b =>
        {
            b.HasMany(x => x.Variants).WithOne(x => x.ServiceItem!).HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.AddOns).WithOne(x => x.ServiceItem!).HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tax).WithMany().HasForeignKey(x => x.TaxId).OnDelete(DeleteBehavior.SetNull);
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.BasePrice).HasPrecision(12, 2);
            b.Property(x => x.TaxPercent).HasPrecision(6, 3);
        });

        builder.Entity<ServiceVariant>(b => b.Property(x => x.Price).HasPrecision(12, 2));
        builder.Entity<ServiceAddOn>(b => b.Property(x => x.Price).HasPrecision(12, 2));

        builder.Entity<Staff>(b =>
        {
            b.HasMany(x => x.Shifts).WithOne(x => x.Staff!).HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Tax>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Name });
            b.HasQueryFilter(x => !x.IsDeleted);
            b.Property(x => x.Percent).HasPrecision(6, 3);
        });

        builder.Entity<AuditEntry>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
            b.HasIndex(x => x.CreatedAt);
        });

        builder.Entity<TenantSetting>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        });

        builder.Entity<IntakeForm>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Type });
            b.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<SkuBatch>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.SkuId });
            b.HasIndex(x => new { x.TenantId, x.SkuId, x.ExpiryDate });
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = _currentUser?.UserId;
        var tenantId = _currentUser?.TenantId;
        var branchId = _currentUser?.BranchId;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedById ??= userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedById = userId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty && tenantId.HasValue)
            {
                entry.Entity.TenantId = tenantId.Value;
                entry.Entity.BranchId ??= branchId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<SoftDeletableTenantEntity>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
                entry.Entity.DeletedById = userId;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
