using Microsoft.EntityFrameworkCore;
using SMRT_QC_Web.Models;

namespace SMRT_QC_Web.Data;

/// <summary>
/// EF Core DbContext for SmartIQC. Registers all entity sets and configures
/// table mappings, indexes, and default values via Fluent API.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ─── Entity Sets ─────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<CheckSheet> CheckSheets => Set<CheckSheet>();
    public DbSet<InspectionRecord> InspectionRecords => Set<InspectionRecord>();
    public DbSet<NcnRecord> NcnRecords => Set<NcnRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Users table ─────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserName).HasColumnName("user_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Password).HasColumnName("password").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Position).HasColumnName("position").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.UserName).IsUnique();
        });

        // ─── Parts table ─────────────────────────────────────────────────────
        modelBuilder.Entity<Part>(entity =>
        {
            entity.ToTable("parts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.PartNumber).HasColumnName("part_number").HasMaxLength(100).IsRequired();
            entity.Property(e => e.PartName).HasColumnName("part_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(100);
            entity.Property(e => e.Spec).HasColumnName("spec").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.PartNumber).IsUnique();
        });

        // ─── CheckSheets table ────────────────────────────────────────────────
        modelBuilder.Entity<CheckSheet>(entity =>
        {
            entity.ToTable("check_sheets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.PartNumber).HasColumnName("part_number").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CheckItem).HasColumnName("check_item").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Specification).HasColumnName("specification").HasMaxLength(300);
            entity.Property(e => e.Method).HasColumnName("method").HasMaxLength(100);
            entity.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ─── InspectionRecords table ──────────────────────────────────────────
        modelBuilder.Entity<InspectionRecord>(entity =>
        {
            entity.ToTable("inspection_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.SupplyNumber).HasColumnName("supply_number").HasMaxLength(100).IsRequired();
            entity.Property(e => e.SupplyDate).HasColumnName("supply_date");
            entity.Property(e => e.LineNumber).HasColumnName("line_number").HasMaxLength(50);
            entity.Property(e => e.PartNumber).HasColumnName("part_number").HasMaxLength(100);
            entity.Property(e => e.PoNumber).HasColumnName("po_number").HasMaxLength(100);
            entity.Property(e => e.Operator).HasColumnName("operator").HasMaxLength(100);
            entity.Property(e => e.ItemCategory).HasColumnName("item_category").HasMaxLength(100);
            entity.Property(e => e.NcnNo).HasColumnName("ncn_no").HasMaxLength(100);
            entity.Property(e => e.ChekName).HasColumnName("chek_name").HasMaxLength(100);
            entity.Property(e => e.AppvName).HasColumnName("appv_name").HasMaxLength(100);
            entity.Property(e => e.Validasi).HasColumnName("validasi").HasMaxLength(50);
            entity.Property(e => e.NcnStatus).HasColumnName("ncn_status").HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
            // Indexes for dashboard COUNT queries filtering by ncn_status and sorting by created_at
            entity.HasIndex(e => e.NcnStatus).HasDatabaseName("idx_inspection_ncn_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_inspection_created_at");
        });

        // ─── NcnRecords table ─────────────────────────────────────────────────
        modelBuilder.Entity<NcnRecord>(entity =>
        {
            entity.ToTable("ncn_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.NcnNo).HasColumnName("ncn_no").HasMaxLength(100).IsRequired();
            entity.Property(e => e.SupplyNumber).HasColumnName("supply_number").HasMaxLength(100);
            entity.Property(e => e.PartNumber).HasColumnName("part_number").HasMaxLength(100);
            entity.Property(e => e.DefectType).HasColumnName("defect_type").HasMaxLength(200);
            entity.Property(e => e.DefectQty).HasColumnName("defect_qty");
            entity.Property(e => e.CheckerName).HasColumnName("checker_name").HasMaxLength(100);
            entity.Property(e => e.ApproverName).HasColumnName("approver_name").HasMaxLength(100);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20)
                .HasDefaultValue("CHECK");
            entity.Property(e => e.PhotoPath).HasColumnName("photo_path").HasMaxLength(500);
            entity.Property(e => e.Notes).HasColumnName("notes").HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.NcnNo).IsUnique();
            // Indexes for dashboard COUNT queries filtering by status and sorting by created_at
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_ncn_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_ncn_created_at");
        });

        // ─── Notifications table ──────────────────────────────────────────────
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.SuppNumber).HasColumnName("supp_number").HasMaxLength(100);
            entity.Property(e => e.PartNumber).HasColumnName("part_number").HasMaxLength(100);
            entity.Property(e => e.Position).HasColumnName("position").HasMaxLength(20);
            entity.Property(e => e.Notif).HasColumnName("notif").HasMaxLength(500);
            entity.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            // Composite index for the frequent WHERE is_read=0 AND position=? filter
            entity.HasIndex(e => new { e.IsRead, e.Position }).HasDatabaseName("idx_notif_read_position");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_notif_created_at");
        });
    }
}
