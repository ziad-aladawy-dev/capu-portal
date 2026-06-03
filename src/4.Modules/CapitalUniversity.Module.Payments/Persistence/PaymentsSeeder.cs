using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.Payments.Persistence;

/// <summary>
/// Seeds representative invoice + payment-transaction data for the first
/// seeded student (Ahmed Mohamed Ali – 20250001) so the student-facing
/// payments dashboard has realistic data on first boot.
///
/// <para>
/// Idempotent — skipped if the <c>Invoice</c> table already contains any
/// rows. Uses <c>context.Set&lt;T&gt;()</c> because the module's entities
/// live outside Core.Infrastructure.
/// </para>
/// </summary>
public static class PaymentsSeeder
{
    public static async Task SeedAsync(CoreDbContext context)
    {
        if (await context.Set<Invoice>().AnyAsync())
        {
            Console.WriteLine("[Seed] Payments: already populated, skipping.");
            return;
        }

        // Resolve Ahmed's student row — seeded by DataSeeder / IdentitySeeder.
        var ahmed = await context.Students.FirstOrDefaultAsync(s => s.StudentCode == "20250001");
        if (ahmed is null)
        {
            Console.WriteLine("[Seed] Payments: student 20250001 not found, skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        // ── Invoice 1: مصاريف الترم الأول 2025 - الفرقة الثانية ────────
        var inv1 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = ahmed.Id,
            TotalAmount = 37_500.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Paid,
            DueAt = new DateTime(2025, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        inv1.Items.Add(new InvoiceItem
        {
            InvoiceId = inv1.Id,
            Amount = 37_500.00m,
            FeeType = "مصروفات دراسية",
            SourceModule = "registration",
            Description = "مصاريف الترم الأول - 2025 - الفرقة الثانية",
        });
        inv1.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = inv1.Id,
            Provider = "Online",
            ProviderTransactionId = "TXN-20250930-001",
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 37_500.00m,
            IdempotencyKey = "idem-inv1-pay1",
            RawPayloadJson = "{}",
            CreatedAt = new DateTime(2025, 9, 30, 5, 54, 0, DateTimeKind.Utc),
        });

        // ── Invoice 2: مصاريف الترم الثاني 2025 - الفرقة الثانية ───────
        var inv2 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = ahmed.Id,
            TotalAmount = 37_500.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Paid,
            DueAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc),
        };
        inv2.Items.Add(new InvoiceItem
        {
            InvoiceId = inv2.Id,
            Amount = 37_500.00m,
            FeeType = "مصروفات دراسية",
            SourceModule = "registration",
            Description = "مصاريف الترم الثاني- 2025 - الفرقة الثانية",
        });
        inv2.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = inv2.Id,
            Provider = "Online",
            ProviderTransactionId = "TXN-20260218-001",
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 37_500.00m,
            IdempotencyKey = "idem-inv2-pay1",
            RawPayloadJson = "{}",
            CreatedAt = new DateTime(2026, 2, 18, 8, 18, 0, DateTimeKind.Utc),
        });

        // ── Invoice 3: خدمات- تربية عسكرية ────────────────────────────
        var inv3 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = ahmed.Id,
            TotalAmount = 800.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Paid,
            DueAt = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2025, 6, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        inv3.Items.Add(new InvoiceItem
        {
            InvoiceId = inv3.Id,
            Amount = 800.00m,
            FeeType = "خدمات",
            SourceModule = "services",
            Description = "خدمات- تربية عسكرية",
        });
        inv3.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = inv3.Id,
            Provider = "Online",
            ProviderTransactionId = "TXN-20250617-001",
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 800.00m,
            IdempotencyKey = "idem-inv3-pay1",
            RawPayloadJson = "{}",
            CreatedAt = new DateTime(2025, 6, 17, 17, 11, 0, DateTimeKind.Utc),
        });

        // ── Invoice 4: الأنشطة الطلابية (Unpaid) ──────────────────────
        var inv4 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = ahmed.Id,
            TotalAmount = 500.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Pending,
            DueAt = null,
            CreatedAt = new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        inv4.Items.Add(new InvoiceItem
        {
            InvoiceId = inv4.Id,
            Amount = 500.00m,
            FeeType = "مصروفات إدارية",
            SourceModule = "admin",
            Description = "الأنشطة الطلابية",
        });

        // ── Invoice 5: رسوم التربيه العسكريه (Unpaid) ──────────────────
        var inv5 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = ahmed.Id,
            TotalAmount = 900.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Pending,
            DueAt = null,
            CreatedAt = new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        inv5.Items.Add(new InvoiceItem
        {
            InvoiceId = inv5.Id,
            Amount = 900.00m,
            FeeType = "مصروفات إدارية",
            SourceModule = "admin",
            Description = "رسوم التربيه العسكريه",
        });

        // ── Invoice 6: math ماده (Unpaid) ──────────────────────────────
        var inv6 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = ahmed.Id,
            TotalAmount = 3_400.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Pending,
            DueAt = null,
            CreatedAt = new DateTime(2024, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        inv6.Items.Add(new InvoiceItem
        {
            InvoiceId = inv6.Id,
            Amount = 3_400.00m,
            FeeType = "خدمات",
            SourceModule = "services",
            Description = "math ماده",
        });

        context.Set<Invoice>().AddRange(inv1, inv2, inv3, inv4, inv5, inv6);
        await context.SaveChangesAsync();

        Console.WriteLine("[Seed] Payments: 6 invoices + 3 transactions seeded for student 20250001.");
    }
}
