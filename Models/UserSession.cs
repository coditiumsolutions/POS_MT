using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("LoginDate", Name = "IX_UserSessions_LoginDate")]
[Index("UserUid", Name = "IX_UserSessions_UserUID")]
public partial class UserSession
{
    [Column("uid")]
    [Key]
    public long Uid { get; set; }

    [Column("UserUID")]
    public int UserUid { get; set; }

    [Column("TerminalUID")]
    public int? TerminalUid { get; set; }

    [Precision(0)]
    public DateTime LoginDate { get; set; }

    [Precision(0)]
    public DateTime? LogoutDate { get; set; }

    [Column("IPAddress")]
    [StringLength(50)]
    public string? Ipaddress { get; set; }

    [StringLength(30)]
    public string SessionStatus { get; set; } = null!;
}
