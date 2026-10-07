using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("LogDate", Name = "IX_AuditLogs_LogDate")]
[Index("ModuleName", Name = "IX_AuditLogs_ModuleName")]
[Index("RecordUid", Name = "IX_AuditLogs_RecordUID")]
[Index("UserUid", Name = "IX_AuditLogs_UserUID")]
public partial class AuditLog
{
    [Column("uid")]
    [Key]
    public long Uid { get; set; }

    [Precision(0)]
    public DateTime LogDate { get; set; }

    [Column("UserUID")]
    public int? UserUid { get; set; }

    [Column("TerminalUID")]
    public int? TerminalUid { get; set; }

    [Column("BranchUID")]
    public int? BranchUid { get; set; }

    [StringLength(100)]
    public string ModuleName { get; set; } = null!;

    [StringLength(100)]
    public string ActionName { get; set; } = null!;

    [Column("RecordUID")]
    public int? RecordUid { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    [Column("IPAddress")]
    [StringLength(50)]
    public string? Ipaddress { get; set; }
}
