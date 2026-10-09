using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebTruyenTranh.Models
{
    [Table("tblUserVocabularies")]
    public class TblUserVocabulary
    {
        [Key]
        public int VocabId { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string WordOrPhrase { get; set; } = string.Empty;

        [Required]
        public string Explanation { get; set; } = string.Empty;

        public string? ContextSentence { get; set; }

        public int? ChapterId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}