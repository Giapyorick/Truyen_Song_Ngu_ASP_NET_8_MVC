using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace WebTruyenTranh.ViewModels
{
    public class UsersViewModel
    {
        public int UserId { get; set; }

        public string? Name { get; set; }

        public DateOnly? DoB { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Img { get; set; }

        public string? Gender { get; set; }

        public DateTime? CreateAd { get; set; }

        public string? Status { get; set; }

        public string? Passwork { get; set; }
        
        [NotMapped]
        public IFormFile? formFile { get; set; }

    }
}