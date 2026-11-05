using Microsoft.AspNetCore.Identity;
using System;

namespace backend.Models
{
    public class ApplicationUser: IdentityUser<Guid>
    {
        public bool IsActive { get; set; } = true;
    }
}
