using backend.Identity;
using backend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;

namespace backend.Data;

/// <summary>
/// Contexto principal de EF Core de la aplicación. Hereda de <see cref="IdentityDbContext{TUser, TRole, TKey}"/>,
/// por lo que incluye las tablas de ASP.NET Core Identity (usuarios, roles, claims, etc.)
/// junto con las entidades del dominio sobre PostgreSQL.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Generado por scaffolding y ajustado a mano (herencia de Identity).</item>
/// <item>Extensiones de PostgreSQL requeridas: <c>btree_gist</c>, <c>pg_trgm</c>, <c>pgrouting</c>, <c>postgis</c> y <c>unaccent</c>.</item>
/// <item>Los identificadores usan <c>uuidv7()</c> como valor por defecto en la base de datos.</item>
/// <item>Las columnas de auditoría <c>CreatedAt</c> (<c>now()</c>) y <c>CreatedByUserId</c> (<c>app.actor()</c>) se llenan en la base de datos.</item>
/// <item>Muchas tablas usan borrado lógico mediante la columna <c>Active</c>, y varios índices son parciales (<c>HasFilter</c>) sobre registros activos.</item>
/// <item>Las funciones SQL mapeadas (<c>IsOpenAt</c>, <c>Normalize</c>, etc.) están en la otra parte parcial de esta clase.</item>
/// </list>
/// </remarks>
public partial class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options) // DbContext(options)
{
    public virtual DbSet<AdPlacement> AdPlacements { get; set; }

    public virtual DbSet<AdminBusinessSummary> AdminBusinessSummaries { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchCategory> BranchCategories { get; set; }

    public virtual DbSet<BranchContact> BranchContacts { get; set; }

    public virtual DbSet<BranchFeature> BranchFeatures { get; set; }

    public virtual DbSet<BranchHourOverride> BranchHourOverrides { get; set; }

    public virtual DbSet<BranchPayment> BranchPayments { get; set; }

    public virtual DbSet<BranchPhoto> BranchPhotos { get; set; }

    public virtual DbSet<BranchServiceProduct> BranchServiceProducts { get; set; }

    public virtual DbSet<BranchStatus> BranchStatuses { get; set; }

    public virtual DbSet<Business> Businesses { get; set; }

    public virtual DbSet<BusinessOwner> BusinessOwners { get; set; }

    public virtual DbSet<BusinessPhoto> BusinessPhotos { get; set; }

    public virtual DbSet<BusinessStatus> BusinessStatuses { get; set; }

    public virtual DbSet<BusinessSubscription> BusinessSubscriptions { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<DailyMetric> DailyMetrics { get; set; }

    public virtual DbSet<DashboardAdPerformance> DashboardAdPerformances { get; set; }

    public virtual DbSet<DashboardBranchMetric> DashboardBranchMetrics { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<EventHist> EventHists { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<Feature> Features { get; set; }

    public virtual DbSet<Holiday> Holidays { get; set; }

    public virtual DbSet<MonthlyMetric> MonthlyMetrics { get; set; }

    public virtual DbSet<OpeningHour> OpeningHours { get; set; }

    public virtual DbSet<ParameterDefinition> ParameterDefinitions { get; set; }

    public virtual DbSet<ParameterValue> ParameterValues { get; set; }

    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }

    public virtual DbSet<PublishedBranch> PublishedBranches { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<ReviewModerationStatus> ReviewModerationStatuses { get; set; }

    public virtual DbSet<SearchSynonym> SearchSynonyms { get; set; }

    public virtual DbSet<ServiceProduct> ServiceProducts { get; set; }

    public virtual DbSet<ServiceProductPrice> ServiceProductPrices { get; set; }

    public virtual DbSet<SubscriptionPayment> SubscriptionPayments { get; set; }

    public virtual DbSet<SubscriptionPaymentStatus> SubscriptionPaymentStatuses { get; set; }

    public virtual DbSet<SubscriptionStatus> SubscriptionStatuses { get; set; }

    public virtual DbSet<SubscriptionType> SubscriptionTypes { get; set; }

    public virtual DbSet<Territory> Territories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurar las entidades de Identity
        base.OnModelCreating(modelBuilder);
        // Model Builder
        modelBuilder
            .HasPostgresExtension("btree_gist")
            .HasPostgresExtension("pg_trgm")
            .HasPostgresExtension("pgrouting")
            .HasPostgresExtension("postgis")
            .HasPostgresExtension("unaccent");

        modelBuilder.Entity<AdPlacement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ad_placement_pkey");

            entity.HasIndex(e => e.BranchId, "ad_branch_idx").HasFilter("(branch_id IS NOT NULL)");

            entity.HasIndex(e => e.BusinessId, "ad_business_idx").HasFilter("(business_id IS NOT NULL)");

            entity.HasIndex(e => new { e.StartsAt, e.EndsAt }, "ad_period_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

            entity.HasOne(d => d.Branch).WithMany(p => p.AdPlacements).HasConstraintName("ad_placement_branch_id_fkey");

            entity.HasOne(d => d.Business).WithMany(p => p.AdPlacements).HasConstraintName("ad_placement_business_id_fkey");
        });

        modelBuilder.Entity<AdminBusinessSummary>(entity =>
        {
            entity.ToView("admin_business_summary", "app");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("branch_pkey");

            entity.HasIndex(e => e.BusinessId, "branch_business_idx").HasFilter("((status)::text = ANY ((ARRAY['active'::character varying, 'temporarily_closed'::character varying])::text[]))");

            entity.HasIndex(e => e.Location, "branch_location_gix")
                .HasFilter("((status)::text = ANY ((ARRAY['active'::character varying, 'temporarily_closed'::character varying])::text[]))")
                .HasMethod("gist");

            entity.HasIndex(e => e.TerritoryId, "branch_territory_idx").HasFilter("((status)::text = ANY ((ARRAY['active'::character varying, 'temporarily_closed'::character varying])::text[]))");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.Status).HasDefaultValueSql("'active'::character varying");

            entity.HasOne(d => d.Business).WithMany(p => p.Branches)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("branch_business_id_fkey");

            entity.HasOne(d => d.StatusNavigation).WithMany(p => p.Branches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_status_fkey");

            entity.HasOne(d => d.Territory).WithMany(p => p.Branches).HasConstraintName("branch_territory_id_fkey");
        });

        modelBuilder.Entity<BranchCategory>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.CategoryId }).HasName("branch_category_pkey");

            entity.HasIndex(e => e.CategoryId, "branch_category_reverse").HasFilter("active");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

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

            entity.HasIndex(e => e.BranchId, "contact_branch_idx").HasFilter("active");

            entity.HasIndex(e => new { e.BranchId, e.ContactType }, "contact_primary_idx")
                .IsUnique()
                .HasFilter("(is_primary AND active)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchContacts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_contact_branch_id_fkey");
        });

        modelBuilder.Entity<BranchFeature>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.FeatureId }).HasName("branch_feature_pkey");

            entity.HasIndex(e => new { e.FeatureId, e.BranchId }, "branch_feature_reverse").HasFilter("active");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

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
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchHourOverrides)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_hour_override_branch_id_fkey");
        });

        modelBuilder.Entity<BranchPayment>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.MethodId }).HasName("branch_payment_pkey");

            entity.HasIndex(e => new { e.MethodId, e.BranchId }, "branch_payment_reverse").HasFilter("active");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

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

            entity.HasIndex(e => new { e.BranchId, e.SortOrder }, "photo_branch_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.FileSize).HasComputedColumnSql("octet_length(photo_data)", true);
            entity.Property(e => e.PhotoSha256).HasComputedColumnSql("sha256(photo_data)", true);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchPhotos)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_photo_branch_id_fkey");
        });

        modelBuilder.Entity<BranchServiceProduct>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.ServiceProductId }).HasName("branch_service_product_pkey");

            entity.HasIndex(e => new { e.ServiceProductId, e.BranchId }, "branch_service_product_reverse").HasFilter("active");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchServiceProducts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_service_product_branch_id_fkey");

            entity.HasOne(d => d.ServiceProduct).WithMany(p => p.BranchServiceProducts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_service_product_service_product_id_fkey");
        });

        modelBuilder.Entity<BranchStatus>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("branch_status_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
        });

        modelBuilder.Entity<Business>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.Status).HasDefaultValueSql("'pending'::character varying");

            entity.HasOne(d => d.StatusNavigation).WithMany(p => p.Businesses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("business_status_fkey");
        });

        modelBuilder.Entity<BusinessOwner>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_owner_pkey");

            entity.HasIndex(e => new { e.UserId, e.BusinessId }, "owner_user_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

            entity.HasOne(d => d.Business).WithMany(p => p.BusinessOwners)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("business_owner_business_id_fkey");
        });

        modelBuilder.Entity<BusinessPhoto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_photo_pkey");

            entity.HasIndex(e => new { e.BusinessId, e.SortOrder }, "photo_business_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.FileSize).HasComputedColumnSql("octet_length(photo_data)", true);
            entity.Property(e => e.PhotoSha256).HasComputedColumnSql("sha256(photo_data)", true);

            entity.HasOne(d => d.Business).WithMany(p => p.BusinessPhotos)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("business_photo_business_id_fkey");
        });

        modelBuilder.Entity<BusinessStatus>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("business_status_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
        });

        modelBuilder.Entity<BusinessSubscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_subscription_pkey");

            entity.HasIndex(e => new { e.BusinessId, e.StartsAt }, "subscription_business_idx")
                .IsDescending(false, true)
                .HasFilter("((status)::text <> 'cancelled'::text)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.Status).HasDefaultValueSql("'pending'::character varying");

            entity.HasOne(d => d.Business).WithMany(p => p.BusinessSubscriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("business_subscription_business_id_fkey");

            entity.HasOne(d => d.StatusNavigation).WithMany(p => p.BusinessSubscriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("business_subscription_status_fkey");

            entity.HasOne(d => d.SubscriptionType).WithMany(p => p.BusinessSubscriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("business_subscription_subscription_type_id_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("category_pkey");

            entity.HasIndex(e => e.SortOrder, "category_sort_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

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

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

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
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("holiday_pkey");

            entity.HasIndex(e => e.HolidayDate, "holiday_date_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

            entity.HasOne(d => d.Territory).WithMany(p => p.Holidays).HasConstraintName("holiday_territory_id_fkey");
        });

        modelBuilder.Entity<OpeningHour>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("opening_hour_pkey");

            entity.HasIndex(e => new { e.BranchId, e.Weekday }, "opening_hour_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

            entity.HasOne(d => d.Branch).WithMany(p => p.OpeningHours)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("opening_hour_branch_id_fkey");
        });

        modelBuilder.Entity<ParameterDefinition>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("parameter_definition_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
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
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.ScopeType).HasDefaultValueSql("'global'::character varying");
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
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
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
                .HasFilter("((moderation_status)::text <> 'deleted'::text)");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "review_user_idx")
                .IsDescending(false, true)
                .HasFilter("((moderation_status)::text <> 'deleted'::text)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.ModerationStatus).HasDefaultValueSql("\nCASE\n    WHEN catalog.param_bool('review.moderation_post'::text) THEN 'visible'::text\n    ELSE 'hidden'::text\nEND");

            entity.HasOne(d => d.Branch).WithMany(p => p.Reviews)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("review_branch_id_fkey");

            entity.HasOne(d => d.ModerationStatusNavigation).WithMany(p => p.Reviews)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("review_moderation_status_fkey");
        });

        modelBuilder.Entity<ReviewModerationStatus>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("review_moderation_status_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
        });

        modelBuilder.Entity<SearchSynonym>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("search_synonym_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.Locale).HasDefaultValueSql("'es-BO'::character varying");
        });

        modelBuilder.Entity<ServiceProduct>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("service_product_pkey");

            entity.HasIndex(e => e.SortOrder, "service_product_sort_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.ItemType).HasDefaultValueSql("'S'::bpchar");
        });

        modelBuilder.Entity<ServiceProductPrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("service_product_price_pkey");

            entity.HasIndex(e => new { e.BranchId, e.ServiceProductId }, "service_product_price_one_idx")
                .IsUnique()
                .HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");

            entity.HasOne(d => d.BranchServiceProduct).WithOne(p => p.ServiceProductPrice)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("service_product_price_branch_id_service_product_id_fkey");
        });

        modelBuilder.Entity<SubscriptionPayment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subscription_payment_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.Status).HasDefaultValueSql("'pending'::character varying");

            entity.HasOne(d => d.PaymentMethod).WithMany(p => p.SubscriptionPayments).HasConstraintName("subscription_payment_payment_method_id_fkey");

            entity.HasOne(d => d.StatusNavigation).WithMany(p => p.SubscriptionPayments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subscription_payment_status_fkey");

            entity.HasOne(d => d.Subscription).WithMany(p => p.SubscriptionPayments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subscription_payment_subscription_id_fkey");
        });

        modelBuilder.Entity<SubscriptionPaymentStatus>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("subscription_payment_status_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
        });

        modelBuilder.Entity<SubscriptionStatus>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("subscription_status_pkey");

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
        });

        modelBuilder.Entity<SubscriptionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subscription_type_pkey");

            entity.HasIndex(e => e.SortOrder, "subscription_type_sort_idx").HasFilter("active");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
        });

        modelBuilder.Entity<Territory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("territory_pkey");

            entity.HasIndex(e => e.Boundary, "territory_gix").HasMethod("gist");

            entity.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CreatedByUserId).HasDefaultValueSql("app.actor()");
            entity.Property(e => e.Source).HasDefaultValueSql("'INE'::character varying");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasConstraintName("territory_parent_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
