using duolingo_rum.Models;
using Microsoft.EntityFrameworkCore;
using System;
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

        // Регистрация
        public async Task<(bool Success, string Message, User User)> Register(string name, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                return (false, "Email пустой", null);

            if (string.IsNullOrWhiteSpace(password))
                return (false, "Пароль пустой", null);

            var existingUser = _context.Users.FirstOrDefault(u => u.Email == email);

            if (existingUser != null)
                return (false, "Пользователь уже существует", null);

            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = password
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return (true, "Регистрация успешна", user);
        }

        // Логин
        public async Task<(bool Success, string Message, User User)> Login(string email, string password)
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Email == email && u.PasswordHash == password);

            if (user == null)
                return (false, "Неверный логин или пароль", null);

            return (true, "Успешный вход", user);
        }
    }
}