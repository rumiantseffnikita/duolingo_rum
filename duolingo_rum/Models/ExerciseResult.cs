using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace duolingo_rum.Models;

public partial class ExerciseResult
{
    public int Id { get; set; }

    public Guid SessionId { get; set; }

    public int WordId { get; set; }

    [Column("exercise_type")]  // Добавить этот атрибут!
    public string ExerciseType { get; set; } = "translation";

    [Column("user_answer")]    // Добавить этот атрибут!
    public string? UserAnswer { get; set; }

    [Column("is_correct")]     // Добавить этот атрибут!
    public bool IsCorrect { get; set; }

    [Column("response_time_ms")] // Добавить этот атрибут!
    public int? ResponseTimeMs { get; set; }

    [Column("ai_feedback")]    // Добавить этот атрибут!
    public string? AiFeedback { get; set; }

    [Column("answered_at")]    // Добавить этот атрибут!
    public DateTime? AnsweredAt { get; set; }

    public virtual LearningSession Session { get; set; } = null!;
    public virtual Word Word { get; set; } = null!;
}