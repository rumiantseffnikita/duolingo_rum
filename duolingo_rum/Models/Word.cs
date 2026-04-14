using System;
using System.Collections.Generic;

namespace duolingo_rum.Models;

public partial class Word
{
    public int Id { get; set; }

    public int LanguageId { get; set; }

    public int? TopicId { get; set; }

    public string Word1 { get; set; } = null!;

    public string Translation { get; set; } = null!;

    public string? Transcription { get; set; }

    public string? ExampleSentence { get; set; }

    public string? ExampleTranslation { get; set; }

    public int? FrequencyRank { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<ExerciseResult> ExerciseResults { get; set; } = new List<ExerciseResult>();

    public virtual Language Language { get; set; } = null!;

    public virtual Topic? Topic { get; set; }

    public virtual ICollection<WordProgress> WordProgresses { get; set; } = new List<WordProgress>();
}
