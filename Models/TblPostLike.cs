using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebTruyenTranh.Models
{
    [Table("tblPostLikes")]
    public class TblPostLike
    {
        [Key]
        public int LikeId { get; set; }

        public int PostId { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("PostId")]
        public virtual TblPost? Post { get; set; }

        [ForeignKey("UserId")]
        public virtual TblUser? User { get; set; }
    }
}