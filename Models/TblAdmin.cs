using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebTruyenTranh.Models
{
    [Table("tbl_Admins")]
    public partial class TblAdmin
    {
        [Key]
        public int AdminId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = null!;

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = null!;

        [StringLength(100)]
        public string? FullName { get; set; }

        [Required]  
        [StringLength(20)]
        public string Role { get; set; } = "Viewer"; 

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLogin { get; set; }
    }
}