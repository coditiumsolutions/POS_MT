using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using POS_MT.Models;

namespace POS_MT.Data;

public partial class POSDbContext : DbContext
{
    public POSDbContext(DbContextOptions<POSDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerPayment> CustomerPayments { get; set; }

    public virtual DbSet<CustomerMonthlySupply> CustomerMonthlySupplies { get; set; }

    public virtual DbSet<CustomerMonthlySupplyOverride> CustomerMonthlySupplyOverrides { get; set; }

    public virtual DbSet<Configuration> Configurations { get; set; }

    public virtual DbSet<Expense> Expenses { get; set; }

    public virtual DbSet<ExpenseCategory> ExpenseCategories { get; set; }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<InventoryTransaction> InventoryTransactions { get; set; }

    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductCategory> ProductCategories { get; set; }

    public virtual DbSet<ProductUnit> ProductUnits { get; set; }

    public virtual DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }

    public virtual DbSet<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; }

    public virtual DbSet<SalesInvoice> SalesInvoices { get; set; }

    public virtual DbSet<SalesInvoiceDetail> SalesInvoiceDetails { get; set; }

    public virtual DbSet<SyncOutbox> SyncOutboxes { get; set; }

    public virtual DbSet<StockAdjustment> StockAdjustments { get; set; }

    public virtual DbSet<StockAdjustmentDetail> StockAdjustmentDetails { get; set; }

    public virtual DbSet<Terminal> Terminals { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }

    public virtual DbSet<Vendor> Vendors { get; set; }

    public virtual DbSet<VendorPayment> VendorPayments { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlite = Database.IsSqlite();
        var nowSql = isSqlite ? "CURRENT_TIMESTAMP" : "(sysdatetime())";

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.LogDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.AdvanceBalance).HasDefaultValue(0m);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<CustomerPayment>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.PaymentDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.AppliedToInvoiceAmount).HasDefaultValue(0m);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<CustomerMonthlySupply>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.DeliveryCharges).HasDefaultValue(0m);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<CustomerMonthlySupplyOverride>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Configuration>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.ExpenseDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql(nowSql);
        });

        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.TransactionDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ProductUnit>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.InvoiceStatus).HasDefaultValue("Completed");
            entity.Property(e => e.PaymentStatus).HasDefaultValue("Unpaid");
            entity.Property(e => e.PurchaseDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PurchaseInvoiceDetail>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.InvoiceDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.InvoiceStatus).HasDefaultValue("Completed");
            entity.Property(e => e.PaymentStatus).HasDefaultValue("Unpaid");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
            var clientTxFilter = isSqlite
                ? "ClientTransactionId IS NOT NULL"
                : "[ClientTransactionId] IS NOT NULL";
            entity.HasIndex(e => e.ClientTransactionId)
                .IsUnique()
                .HasFilter(clientTxFilter)
                .HasDatabaseName("UX_SalesInvoices_ClientTransactionId");
        });

        modelBuilder.Entity<SalesInvoiceDetail>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<SyncOutbox>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Status).HasDefaultValue(SyncOutbox.StatusPending);
            entity.Property(e => e.AttemptCount).HasDefaultValue(0);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.Property(e => e.AdjustmentDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Status).HasDefaultValue("Completed");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<StockAdjustmentDetail>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Terminal>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.Property(e => e.LoginDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.SessionStatus).HasDefaultValue("Active");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<VendorPayment>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.PaymentDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        if (isSqlite)
        {
            // SQL Server store types like varchar(max)/decimal(18,2) are invalid in SQLite DDL.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    var storeType = property.GetColumnType();
                    if (string.IsNullOrWhiteSpace(storeType))
                    {
                        continue;
                    }

                    if (storeType.Contains("max", StringComparison.OrdinalIgnoreCase)
                        || storeType.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase)
                        || storeType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase)
                        || storeType.StartsWith("decimal", StringComparison.OrdinalIgnoreCase)
                        || storeType.StartsWith("datetime", StringComparison.OrdinalIgnoreCase))
                    {
                        property.SetColumnType(null);
                    }
                }
            }
        }

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
