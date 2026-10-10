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

    public virtual DbSet<CustomerMonthlyItem> CustomerMonthlyItems { get; set; }

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
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.LogDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<CustomerPayment>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.PaymentDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<CustomerMonthlyItem>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Configuration>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.ExpenseDate).HasDefaultValueSql("(sysdatetime())");
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
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(sysdatetime())");
        });

        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TransactionDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
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
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.InvoiceStatus).HasDefaultValue("Completed");
            entity.Property(e => e.PaymentStatus).HasDefaultValue("Unpaid");
            entity.Property(e => e.PurchaseDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PurchaseInvoiceDetail>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.InvoiceDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.InvoiceStatus).HasDefaultValue("Completed");
            entity.Property(e => e.PaymentStatus).HasDefaultValue("Unpaid");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<SalesInvoiceDetail>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.Property(e => e.AdjustmentDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status).HasDefaultValue("Completed");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<StockAdjustmentDetail>(entity =>
        {
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Terminal>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.Property(e => e.LoginDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.SessionStatus).HasDefaultValue("Active");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<VendorPayment>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.PaymentDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Uid).ValueGeneratedOnAdd();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
