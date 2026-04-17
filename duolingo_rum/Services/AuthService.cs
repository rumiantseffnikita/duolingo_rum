using duolingo_rum.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace duolingo_rum.Services
{
    public class AuthService
    {
        private readonly _43pRumiantsefContext _context;

        public AuthService(_43pRumiantsefContext context)
        {
            _context = context;
        }

        // Регистрация (простая, без языков)
        public async Task<(bool Success, string Message, User User)> Register(string name, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                return (false, "Email пустой", null);

            if (string.IsNullOrWhiteSpace(password))
                return (false, "Пароль пустой", null);

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
                return (false, "Пользователь уже существует", null);

            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = password,
                DailyGoalWords = 10,
                DailyGoalMinutes = 15,
                TotalXp = 0,
                StreakDays = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return (true, "Регистрация успешна", user);
        }

        // Регистрация с выбором языка и уровня
        public async Task<(bool Success, string Message, User User)> RegisterWithLanguage(
            string name,
            string email,
            string password,
            int targetLanguageId,
            int nativeLanguageId,
            string difficultyLevel = "beginner")
        {
            if (string.IsNullOrWhiteSpace(email))
                return (false, "Email пустой", null);

            if (string.IsNullOrWhiteSpace(password))
                return (false, "Пароль пустой", null);

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
                return (false, "Пользователь уже существует", null);

            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = password,
                TargetLanguageId = targetLanguageId,
                NativeLanguageId = nativeLanguageId,
                DifficultyLevel = difficultyLevel,
                DailyGoalWords = 10,
                DailyGoalMinutes = 15,
                TotalXp = 0,
                StreakDays = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return (true, "Регистрация успешна", user);
        }

        // Логин
        public async Task<(bool Success, string Message, User User)> Login(string email, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == password);

            if (user == null)
                return (false, "Неверный логин или пароль", null);

            return (true, "Успешный вход", user);
        }

        // Получить все активные языки
        public async Task<List<Language>> GetAllLanguages()
        {
            return await _context.Languages
                .Where(l => l.IsActive == true)
                .OrderBy(l => l.Name)
                .ToListAsync();
        }

        // Получить язык по ID
        public async Task<Language?> GetLanguageById(int id)
        {
            return await _context.Languages
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        // Получить язык по коду
        public async Task<Language?> GetLanguageByCode(string code)
        {
            return await _context.Languages
                .FirstOrDefaultAsync(l => l.Code == code);
        }

        // Обновить языки пользователя
        public async Task<bool> UpdateUserLanguages(Guid userId, int? targetLanguageId, int? nativeLanguageId, string difficultyLevel = null)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                if (targetLanguageId.HasValue)
                    user.TargetLanguageId = targetLanguageId.Value;
                if (nativeLanguageId.HasValue)
                    user.NativeLanguageId = nativeLanguageId.Value;
                if (!string.IsNullOrEmpty(difficultyLevel))
                    user.DifficultyLevel = difficultyLevel;

                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Проверить, существует ли пользователь
        public async Task<bool> UserExists(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        // Получить пользователя по ID
        public async Task<User?> GetUserById(Guid userId)
        {
            return await _context.Users
                .Include(u => u.TargetLanguage)
                .Include(u => u.NativeLanguage)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }
    }
}