using System;
using System.Collections.Generic;

namespace duolingo_rum.Models;

public partial class LearningSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int LanguageId { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public int? DurationSec { get; set; }

    public int? WordsStudied { get; set; }

    public int? CorrectAnswers { get; set; }

    public int? WrongAnswers { get; set; }

    public int? XpEarned { get; set; }

    public virtual ICollection<ExerciseResult> ExerciseResults { get; set; } = new List<ExerciseResult>();

    public virtual Language Language { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
