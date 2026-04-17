using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;  // ✅ Добавить этот using

namespace duolingo_rum.Models;

public partial class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public int? NativeLanguageId { get; set; }
    public int? TargetLanguageId { get; set; }

    // ✅ Добавить атрибут Column
    [Column("difficulty_level")]
    public string? DifficultyLevel { get; set; }

    public int? DailyGoalMinutes { get; set; }
    public int? DailyGoalWords { get; set; }
    public int? StreakDays { get; set; }
    public int? LongestStreak { get; set; }
    public int? TotalXp { get; set; }
    public DateOnly? LastActivityDate { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<LearningSession> LearningSessions { get; set; } = new List<LearningSession>();
    public virtual Language? NativeLanguage { get; set; }
    public virtual Language? TargetLanguage { get; set; }
    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
    public virtual ICollection<WordProgress> WordProgresses { get; set; } = new List<WordProgress>();
}