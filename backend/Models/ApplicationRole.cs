using Microsoft.AspNetCore.Identity;
using System;

namespace backend.Models
{
    public class ApplicationRole: IdentityRole<Guid>
    {
        public bool IsActive { get; set; } = true;
    }
}
