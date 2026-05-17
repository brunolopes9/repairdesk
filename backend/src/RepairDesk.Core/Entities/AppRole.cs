using Microsoft.AspNetCore.Identity;

namespace RepairDesk.Core.Entities;

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }
    public AppRole(string name) : base(name) { }
}
