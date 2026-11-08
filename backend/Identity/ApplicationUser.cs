using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;

namespace backend.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        [StringLength(150)]
        public string? FirstName { get; set; }
        [StringLength(150)]
        public string? LastName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
    }
}
