using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Table("SyncOutbox")]
[Index(nameof(ClientTransactionId), Name = "UX_SyncOutbox_ClientTransactionId", IsUnique = true)]
[Index(nameof(Status), nameof(CreatedDate), Name = "IX_SyncOutbox_Status_CreatedDate")]
public sealed class SyncOutbox
{
    public const string StatusPending = "Pending";
    public const string StatusSyncing = "Syncing";
    public const string StatusSynced = "Synced";
    public const string StatusFailed = "Failed";

    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string EntityType { get; set; } = "SalesInvoice";

    public int EntityUid { get; set; }

    public Guid ClientTransactionId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = StatusPending;

    public int AttemptCount { get; set; }

    [StringLength(2000)]
    public string? LastError { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    [Precision(0)]
    public DateTime? SyncedDate { get; set; }
}
