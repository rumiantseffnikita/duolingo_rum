using System;
using System.Collections.Generic;

namespace duolingo_rum.Models;

public partial class WordProgress
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public int WordId { get; set; }

    public int? Repetitions { get; set; }

    public decimal? EasinessFactor { get; set; }

    public int? IntervalDays { get; set; }

    public DateOnly? NextReview { get; set; }

    public DateOnly? LastReview { get; set; }

    public int? CorrectCount { get; set; }

    public int? WrongCount { get; set; }

    public bool? IsLearned { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Word Word { get; set; } = null!;
}
