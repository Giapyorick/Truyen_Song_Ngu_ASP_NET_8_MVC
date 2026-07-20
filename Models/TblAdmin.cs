using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblAdmin
{
    public int AdminId { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public bool? Status { get; set; }

    public DateTime? CreatedAt { get; set; }
}
