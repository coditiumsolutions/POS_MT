using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

/// <summary>
/// Catalog of which tables participate in offline/online sync.
/// PullFromOnline controls what <c>offline pull</c> copies from central SQL into local SQLite.
/// </summary>
[Table("OfflineSyncTables")]
[Index("TableName", IsUnique = true, Name = "UX_OfflineSyncTables_TableName")]
public partial class OfflineSyncTable
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [Required, StringLength(100)]
    public string TableName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>When true, offline pull replaces local rows from online SQL.</summary>
    public bool PullFromOnline { get; set; }

    /// <summary>
    /// When true, changes are expected to go online (sales use SyncOutbox; others may be future).
    /// </summary>
    public bool PushToOnline { get; set; }

    /// <summary>Lookup | Transactional | LocalOnly</summary>
    [Required, StringLength(30)]
    public string SyncMode { get; set; } = "Lookup";

    public int SortOrder { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    [Precision(0)]
    public DateTime UpdatedDate { get; set; }
}
