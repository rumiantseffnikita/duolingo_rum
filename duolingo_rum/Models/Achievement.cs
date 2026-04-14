using System;
using System.Collections.Generic;

namespace duolingo_rum.Models;

public partial class Achievement
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? IconEmoji { get; set; }

    public int? XpReward { get; set; }

    public string? ConditionType { get; set; }

    public int? ConditionValue { get; set; }

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
