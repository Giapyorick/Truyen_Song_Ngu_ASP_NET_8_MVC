using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebTruyenTranh.Models
{
    [Table("tblPosts")]
    public class TblPost
    {
        [Key]
        public int PostId { get; set; }

        public int UserId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        // Liên kết tùy chọn tới Truyện và Chương cụ thể
        public int? StoryId { get; set; }
        public int? ChapterId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual TblUser? User { get; set; }

        [ForeignKey("StoryId")]
        public virtual TblStory? Story { get; set; }

        [ForeignKey("ChapterId")]
        public virtual TblChapter? Chapter { get; set; }
        public bool IsSpoiler { get; set; } = false;

        public virtual ICollection<TblPostLike> PostLikes { get; set; } = new List<TblPostLike>();
        public virtual ICollection<TblPostComment> PostComments { get; set; } = new List<TblPostComment>();
    }
}