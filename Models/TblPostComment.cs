using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebTruyenTranh.Models
{
    [Table("tblPostComments")]
    public class TblPostComment
    {
        [Key]
        public int CommentId { get; set; }

        public int PostId { get; set; }
        public int UserId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        // ParentCommentId dùng để tạo tính năng Phản hồi (Reply) phân cấp
        public int? ParentCommentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("PostId")]
        public virtual TblPost? Post { get; set; }

        [ForeignKey("UserId")]
        public virtual TblUser? User { get; set; }

        [ForeignKey("ParentCommentId")]
        public virtual TblPostComment? ParentComment { get; set; }
        public int? StoryId { get; set; }
        public int? ChapterId { get; set; }
        public bool IsSpoiler { get; set; } = false;
        public virtual TblStory? Story { get; set; }
        public virtual TblChapter? Chapter { get; set; }

        public virtual ICollection<TblPostComment> Replies { get; set; } = new List<TblPostComment>();
    }
}