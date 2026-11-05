using backend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;

namespace backend.Data;

public partial class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options) //DbContext
{
    public virtual DbSet<AdPlacement> AdPlacements { get; set; }

    public virtual DbSet<AdminBusinessSummary> AdminBusinessSummaries { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchCategory> BranchCategories { get; set; }

    public virtual DbSet<BranchContact> BranchContacts { get; set; }

    public virtual DbSet<BranchFeature> BranchFeatures { get; set; }

    public virtual DbSet<BranchHourOverride> BranchHourOverrides { get; set; }

    public virtual DbSet<BranchPayment> BranchPayments { get; set; }

    public virtual DbSet<BranchPhoto> BranchPhotos { get; set; }

    public virtual DbSet<BranchService> BranchServices { get; set; }

    public virtual DbSet<Business> Businesses { get; set; }

    public virtual DbSet<BusinessOwner> BusinessOwners { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<DailyMetric> DailyMetrics { get; set; }

    public virtual DbSet<DashboardAdPerformance> DashboardAdPerformances { get; set; }

    public virtual DbSet<DashboardBranchMetric> DashboardBranchMetrics { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<EventHist> EventHists { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<Feature> Features { get; set; }

    public virtual DbSet<Holiday> Holidays { get; set; }

    public virtual DbSet<ImportBatch> ImportBatches { get; set; }

    public virtual DbSet<ImportRow> ImportRows { get; set; }

    public virtual DbSet<MonthlyMetric> MonthlyMetrics { get; set; }

    public virtual DbSet<OpeningHour> OpeningHours { get; set; }

    public virtual DbSet<OwnerIdentity> OwnerIdentities { get; set; }

    public virtual DbSet<ParameterDefinition> ParameterDefinitions { get; set; }

    public virtual DbSet<ParameterValue> ParameterValues { get; set; }

    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }

    public virtual DbSet<PublishedBranch> PublishedBranches { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<ReviewReply> ReviewReplies { get; set; }

    public virtual DbSet<SearchSynonym> SearchSynonyms { get; set; }

    public virtual DbSet<Service> Services { get; set; }

    public virtual DbSet<ServicePrice> ServicePrices { get; set; }

    public virtual DbSet<Territory> Territories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurar las entidades de Identity
        base.OnModelCreating(modelBuilder);
        // Model Builder
        modelBuilder
            .HasPostgresExtension("btree_gist")
            .HasPostgresExtension("pg_trgm")
            .HasPostgresExtension("postgis")
            .HasPostgresExtension("unaccent");

        modelBuilder.Entity<AdPlacement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ad_placement_pkey");

            entity.HasIndex(e => e.BranchId, "ad_branch_idx").HasFilter("(branch_id IS NOT NULL)");

            entity.HasIndex(e => e.BusinessId, "ad_business_idx").HasFilter("(business_id IS NOT NULL)");

            entity.HasIndex(e => new { e.StartsAt, e.EndsAt, e.Slot }, "ad_period_idx").HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PaymentStatus).HasDefaultValueSql("'pending'::text");
            entity.Property(e => e.Status).HasDefaultValueSql("'draft'::text");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Branch).WithMany(p => p.AdPlacements).HasConstraintName("ad_placement_branch_id_fkey");

            entity.HasOne(d => d.Business).WithMany(p => p.AdPlacements).HasConstraintName("ad_placement_business_id_fkey");
        });

        modelBuilder.Entity<AdminBusinessSummary>(entity =>
        {
            entity.ToView("admin_business_summary", "app");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("audit_log_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("clock_timestamp()");
            entity.Property(e => e.TransactionId).HasDefaultValueSql("pg_current_xact_id()");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("branch_pkey");

            entity.HasIndex(e => e.BusinessId, "branch_business_idx").HasFilter("(deleted_at IS NULL)");

            entity.HasIndex(e => e.Location, "branch_location_gix")
                .HasFilter("(deleted_at IS NULL)")
                .HasMethod("gist");

            entity.HasIndex(e => e.BusinessId, "branch_primary_one_idx")
                .IsUnique()
                .HasFilter("(is_primary AND (deleted_at IS NULL))");

            entity.HasIndex(e => e.TerritoryId, "branch_territory_idx").HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Accessibility).HasDefaultValueSql("'{}'::jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.LocationSource).HasDefaultValueSql("'manual'::text");
            entity.Property(e => e.Source).HasDefaultValueSql("'manual'::text");
            entity.Property(e => e.Status).HasDefaultValueSql("'active'::text");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Business).WithOne(p => p.Branch)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("branch_business_id_fkey");

            entity.HasOne(d => d.Territory).WithMany(p => p.Branches).HasConstraintName("branch_territory_id_fkey");
        });

        modelBuilder.Entity<BranchCategory>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.CategoryId }).HasName("branch_category_pkey");

            entity.HasIndex(e => e.CategoryId, "branch_category_reverse").HasFilter("active");

            entity.Property(e => e.Active).HasDefaultValue(true);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_category_branch_id_fkey");

            entity.HasOne(d => d.Category).WithMany(p => p.BranchCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_category_category_id_fkey");
        });

        modelBuilder.Entity<BranchContact>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("branch_contact_pkey");

            entity.HasIndex(e => new { e.BranchId, e.ContactType }, "contact_primary_idx")
                .IsUnique()
                .HasFilter("(is_primary AND (deleted_at IS NULL))");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchContacts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_contact_branch_id_fkey");
        });

        modelBuilder.Entity<BranchFeature>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.FeatureId }).HasName("branch_feature_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchFeatures)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_feature_branch_id_fkey");

            entity.HasOne(d => d.Feature).WithMany(p => p.BranchFeatures)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_feature_feature_id_fkey");
        });

        modelBuilder.Entity<BranchHourOverride>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("branch_hour_override_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchHourOverrides)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_hour_override_branch_id_fkey");
        });

        modelBuilder.Entity<BranchPayment>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.MethodId }).HasName("branch_payment_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchPayments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_payment_branch_id_fkey");

            entity.HasOne(d => d.Method).WithMany(p => p.BranchPayments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_payment_method_id_fkey");
        });

        modelBuilder.Entity<BranchPhoto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("branch_photo_pkey");

            entity.HasIndex(e => new { e.BranchId, e.SortOrder }, "photo_branch_idx").HasFilter("(deleted_at IS NULL)");

            entity.HasIndex(e => e.BranchId, "photo_primary_one_idx")
                .IsUnique()
                .HasFilter("(is_primary AND (deleted_at IS NULL))");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileSize).HasComputedColumnSql("octet_length(photo_data)", true);

            entity.HasOne(d => d.Branch).WithOne(p => p.BranchPhoto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_photo_branch_id_fkey");
        });

        modelBuilder.Entity<BranchService>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.ServiceId }).HasName("branch_service_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchServices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_service_branch_id_fkey");

            entity.HasOne(d => d.Service).WithMany(p => p.BranchServices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_service_service_id_fkey");
        });

        modelBuilder.Entity<Business>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PublicationStatus).HasDefaultValueSql("'draft'::text");
            entity.Property(e => e.Source).HasDefaultValueSql("'manual'::text");
            entity.Property(e => e.Status).HasDefaultValueSql("'active'::text");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<BusinessOwner>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_owner_pkey");

            entity.HasIndex(e => e.UserId, "owner_user_idx").HasFilter("(revoked_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Business).WithMany(p => p.BusinessOwners)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("business_owner_business_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.BusinessOwners)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("business_owner_user_id_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("category_pkey");

            entity.HasIndex(e => e.SortOrder, "category_sort_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasConstraintName("category_parent_id_fkey");
        });

        modelBuilder.Entity<DashboardAdPerformance>(entity =>
        {
            entity.ToView("dashboard_ad_performance", "app");
        });

        modelBuilder.Entity<DashboardBranchMetric>(entity =>
        {
            entity.ToView("dashboard_branch_metrics", "app");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("event_pkey");

            entity.HasIndex(e => new { e.BranchId, e.OccurredAt }, "event_branch_time").HasFilter("(branch_id IS NOT NULL)");

            entity.HasIndex(e => e.OccurredAt, "event_time_brin").HasMethod("brin");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Metadata).HasDefaultValueSql("'{}'::jsonb");
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("clock_timestamp()");
        });

        modelBuilder.Entity<EventHist>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("event_hist_pkey");

            entity.HasIndex(e => new { e.BranchId, e.OccurredAt }, "event_hist_branch_id_occurred_at_idx").HasFilter("(branch_id IS NOT NULL)");

            entity.HasIndex(e => e.OccurredAt, "event_hist_occurred_at_idx").HasMethod("brin");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Metadata).HasDefaultValueSql("'{}'::jsonb");
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("clock_timestamp()");
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.BranchId }).HasName("favorite_pkey");

            entity.HasIndex(e => e.BranchId, "favorite_branch_idx").HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Branch).WithMany(p => p.Favorites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("favorite_branch_id_fkey");
        });

        modelBuilder.Entity<Feature>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("feature_pkey");

            entity.HasIndex(e => e.SortOrder, "feature_sort_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("holiday_pkey");

            entity.HasIndex(e => e.HolidayDate, "holiday_date_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);

            entity.HasOne(d => d.Territory).WithMany(p => p.Holidays).HasConstraintName("holiday_territory_id_fkey");
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("import_batch_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status).HasDefaultValueSql("'uploaded'::text");
        });

        modelBuilder.Entity<ImportRow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("import_row_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Decision).HasDefaultValueSql("'pending'::text");

            entity.HasOne(d => d.Batch).WithMany(p => p.ImportRows)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("import_row_batch_id_fkey");

            entity.HasOne(d => d.MatchedBranch).WithMany(p => p.ImportRows).HasConstraintName("import_row_matched_branch_id_fkey");

            entity.HasOne(d => d.MatchedBusiness).WithMany(p => p.ImportRows).HasConstraintName("import_row_matched_business_id_fkey");
        });

        modelBuilder.Entity<OpeningHour>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("opening_hour_pkey");

            entity.HasIndex(e => new { e.BranchId, e.Weekday }, "opening_hour_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);

            entity.HasOne(d => d.Branch).WithMany(p => p.OpeningHours)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("opening_hour_branch_id_fkey");
        });

        modelBuilder.Entity<OwnerIdentity>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("owner_identity_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ParameterDefinition>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("parameter_definition_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);
        });

        modelBuilder.Entity<ParameterValue>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("parameter_value_pkey");

            entity.HasIndex(e => new { e.Code, e.ScopeType, e.ScopeId, e.ValidFrom }, "parameter_lookup_idx")
                .IsDescending(false, false, false, true)
                .HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ScopeType).HasDefaultValueSql("'global'::text");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ValidFrom).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CodeNavigation).WithMany(p => p.ParameterValues)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("parameter_value_code_fkey");
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payment_method_pkey");

            entity.HasIndex(e => e.SortOrder, "payment_method_sort_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<PublishedBranch>(entity =>
        {
            entity.ToView("published_branch", "app");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("review_pkey");

            entity.HasIndex(e => new { e.BranchId, e.ModerationStatus, e.CreatedAt }, "review_branch_idx")
                .IsDescending(false, false, true)
                .HasFilter("(deleted_at IS NULL)");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "review_user_idx")
                .IsDescending(false, true)
                .HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ModerationStatus).HasDefaultValueSql("'visible'::text");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Branch).WithMany(p => p.Reviews)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("review_branch_id_fkey");
        });

        modelBuilder.Entity<ReviewReply>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("review_reply_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Review).WithOne(p => p.ReviewReply)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("review_reply_review_id_fkey");
        });

        modelBuilder.Entity<SearchSynonym>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("search_synonym_pkey");

            entity.HasIndex(e => e.Term, "synonym_term_idx")
                .HasMethod("gin")
                .HasOperators(new[] { "gin_trgm_ops" });

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.Locale).HasDefaultValueSql("'es-BO'::text");
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("service_pkey");

            entity.HasIndex(e => e.SortOrder, "service_sort_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ServicePrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("service_price_pkey");

            entity.HasIndex(e => new { e.BranchId, e.ServiceId }, "service_price_one_idx")
                .IsUnique()
                .HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.BranchService).WithOne(p => p.ServicePrice)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("service_price_branch_id_service_id_fkey");
        });

        modelBuilder.Entity<Territory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("territory_pkey");

            entity.HasIndex(e => e.Boundary, "territory_gix").HasMethod("gist");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Source).HasDefaultValueSql("'INE'::text");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasConstraintName("territory_parent_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
