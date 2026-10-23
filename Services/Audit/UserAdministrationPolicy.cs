using ProGlassAutomation.Models.Audit;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Audit
{
    /// <summary>Pure account-management rules, kept separate for direct testing.</summary>
    public static class UserAdministrationPolicy
    {
        public static int CountActiveAdministrators(IEnumerable<AppUser> users) =>
            users.Count(user => user.IsActive && AppRoles.IsAdministrator(user.Role));

        /// <summary>
        /// Returns true only when a proposed active/role change leaves at
        /// least one active canonical Administrator. The database is read in
        /// the caller's write transaction before this result is acted on.
        /// </summary>
        public static bool HasActiveAdministratorAfterChange(
            IEnumerable<AppUser> users,
            int targetUserId,
            bool? isActive = null,
            string? role = null)
        {
            var rows = users as IReadOnlyCollection<AppUser> ?? users.ToArray();
            var target = rows.FirstOrDefault(user => user.Id == targetUserId);
            if (target == null) return false;

            return rows.Any(user =>
            {
                bool active = user.Id == targetUserId && isActive.HasValue
                    ? isActive.Value
                    : user.IsActive;
                string effectiveRole = user.Id == targetUserId && role != null
                    ? AppRoles.Normalize(role)
                    : AppRoles.Normalize(user.Role);
                return active && AppRoles.IsAdministrator(effectiveRole);
            });
        }
    }
}
