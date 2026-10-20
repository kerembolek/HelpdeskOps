using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace HelpdeskOps.Models.Enums
{
    public static class EnumExtensions
    {
        // TicketStatus.InProgress -> "İşlemde" (the [Display] name), falling back to the member name
        public static string GetDisplayName(this Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
        }
    }
}
