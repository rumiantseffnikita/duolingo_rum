using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using duolingo_rum.Models;
using Microsoft.EntityFrameworkCore;

namespace duolingo_rum.Services
{
    // ✅ ФИКС: WordService теперь IDisposable — контекст корректно освобождается
    public class WordService : IDisposable
    {
        private readonly _43pRumiantsefContext _context;
        private bool _disposed = false;

        public WordService()
        {
            _context = new _43pRumiantsefContext();
        }

        // ✅ НОВЫЙ МЕТОД: получаем свежие данные юзера из БД
        public async Task<User?> GetUserById(Guid userId)
        {
            try
            {
                // AsNoTracking чтобы не конфликтовало с уже трекаемыми объектами
                return await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserById Error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Word>> GetWordsForLesson(Guid userId)
        {
            try
            {
                var words = await _context.Words
                    .Take(10)
                    .ToListAsync();

                return words ?? new List<Word>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetWordsForLesson Error: {ex.Message}");
                return new List<Word>();
            }
        }

        public async Task<List<Word>> GetAnyWords(int count)
        {
            try
            {
                var words = await _context.Words
                    .Take(count)
                    .ToListAsync();

                return words ?? new List<Word>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAnyWords Error: {ex.Message}");
                return new List<Word>();
            }
        }

        public async Task SaveExerciseResult(Guid userId, int wordId, bool isCorrect, string userAnswer, string aiFeedback)
        {
            try
            {
                Debug.WriteLine($"SaveExerciseResult started for user {userId}, word {wordId}");

                var activeSession = await _context.LearningSessions
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.FinishedAt == null);

                if (activeSession == null)
                {
                    activeSession = new LearningSession
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        LanguageId = 2,
                        StartedAt = DateTime.UtcNow,
                        WordsStudied = 0,
                        CorrectAnswers = 0,
                        WrongAnswers = 0,
                        XpEarned = 0
                    };
                    _context.LearningSessions.Add(activeSession);
                    await _context.SaveChangesAsync();
                    Debug.WriteLine($"Created new session with ID: {activeSession.Id}");
                }

                var exerciseResult = new ExerciseResult
                {
                    SessionId = activeSession.Id,
                    WordId = wordId,
                    ExerciseType = "translation",
                    UserAnswer = userAnswer ?? string.Empty,
                    IsCorrect = isCorrect,
                    AiFeedback = aiFeedback ?? string.Empty,
                    AnsweredAt = DateTime.UtcNow,
                    ResponseTimeMs = 0
                };

                _context.ExerciseResults.Add(exerciseResult);
                Debug.WriteLine("Exercise result added to context");

                if (isCorrect)
                    activeSession.CorrectAnswers = (activeSession.CorrectAnswers ?? 0) + 1;
                else
                    activeSession.WrongAnswers = (activeSession.WrongAnswers ?? 0) + 1;

                activeSession.WordsStudied = (activeSession.WordsStudied ?? 0) + 1;
                activeSession.XpEarned = (activeSession.XpEarned ?? 0) + (isCorrect ? 10 : 5);

                await _context.SaveChangesAsync();
                Debug.WriteLine("Session stats saved");

                // ✅ ФИКС: используем Find (трекает по PK, не дублирует запросы)
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.TotalXp = (user.TotalXp ?? 0) + (isCorrect ? 10 : 5);
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    Debug.WriteLine($"User XP updated to {user.TotalXp}");
                }

                try
                {
                    await UpdateWordProgressSimple(userId, wordId, isCorrect);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating word progress: {ex.Message}");
                }

                Debug.WriteLine("SaveExerciseResult completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveExerciseResult ERROR: {ex.Message}");
                if (ex.InnerException != null)
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                throw;
            }
        }

        private async Task UpdateWordProgressSimple(Guid userId, int wordId, bool isCorrect)
        {
            try
            {
                var progress = await _context.WordProgresses
                    .FirstOrDefaultAsync(wp => wp.UserId == userId && wp.WordId == wordId);

                if (progress == null)
                {
                    progress = new WordProgress
                    {
                        UserId = userId,
                        WordId = wordId,
                        Repetitions = 0,
                        EasinessFactor = 2.5m,
                        IntervalDays = 1,
                        CorrectCount = 0,
                        WrongCount = 0,
                        IsLearned = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastReview = DateOnly.FromDateTime(DateTime.UtcNow),
                        NextReview = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
                    };
                    _context.WordProgresses.Add(progress);
                    await _context.SaveChangesAsync();
                }

                if (isCorrect)
                {
                    progress.CorrectCount = (progress.CorrectCount ?? 0) + 1;
                    progress.Repetitions = (progress.Repetitions ?? 0) + 1;
                }
                else
                {
                    progress.WrongCount = (progress.WrongCount ?? 0) + 1;
                }

                progress.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateWordProgressSimple Error: {ex.Message}");
            }
        }

        public async Task<int> GetLearnedWordsCount(Guid userId)
        {
            try
            {
                return await _context.WordProgresses
                    .Where(wp => wp.UserId == userId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetLearnedWordsCount ERROR: {ex.Message}");
                return 0;
            }
        }

        public async Task<int> GetTodayProgress(Guid userId)
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                var sessions = await _context.LearningSessions
                    .Where(s => s.UserId == userId)
                    .Select(s => s.Id)
                    .ToListAsync();

                return await _context.ExerciseResults
                    .Where(r => sessions.Contains(r.SessionId) &&
                                r.AnsweredAt.HasValue &&
                                r.AnsweredAt.Value.Date == today)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetTodayProgress ERROR: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<Word>> GetWordsToReview(Guid userId, int count)
        {
            try
            {
                var words = await _context.WordProgresses
                    .Where(wp => wp.UserId == userId)
                    .Include(wp => wp.Word)
                    .Take(count)
                    .Select(wp => wp.Word)
                    .ToListAsync();

                return words ?? new List<Word>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetWordsToReview ERROR: {ex.Message}");
                return new List<Word>();
            }
        }

        public async Task EndCurrentSession(Guid userId)
        {
            try
            {
                var activeSession = await _context.LearningSessions
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.FinishedAt == null);

                if (activeSession != null)
                {
                    activeSession.FinishedAt = DateTime.UtcNow;
                    if (activeSession.StartedAt.HasValue)
                    {
                        activeSession.DurationSec = (int)(DateTime.UtcNow - activeSession.StartedAt.Value).TotalSeconds;
                    }
                    await _context.SaveChangesAsync();
                    Debug.WriteLine($"Session ended for user {userId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EndCurrentSession Error: {ex.Message}");
            }
        }

        // ✅ ФИКС: Dispose освобождает DbContext и закрывает соединение с БД
        public void Dispose()
        {
            if (!_disposed)
            {
                _context?.Dispose();
                _disposed = true;
            }
        }
    }
}