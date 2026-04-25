using Microsoft.AspNetCore.Identity;

namespace CafeBot.Data.Entities;

public class AppUser : IdentityUser
{
    // Tenant bazlı ekstra bilgiler buraya eklenebilir.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CompanyName { get; set; }
    public string? PersonalGeminiApiKey { get; set; }

    // Abonelik Altyapısı (BÖLÜM 7.3)
    public int? SubscriptionPlanId { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }

    // Admin Yönetimi
    public bool IsBanned { get; set; } = false;
}
