using duolingo_rum.Models;
using duolingo_rum.Services;
using duolingo_rum.ViewModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace duolingo_rum.Tests
{
    public class AppTests
    {
        private _43pRumiantsefContext GetInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<_43pRumiantsefContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new _43pRumiantsefContext(options);
        }

        // 1. Регистрация
        [Fact]
        public async Task AuthService_Register_ShouldCreateUser()
        {
            using var context = GetInMemoryContext("RegisterTest");
            var authService = new AuthService(context);

            var (success, message, user) = await authService.Register("Test", "test@test.com", "1234");

            success.Should().BeTrue();
            user.Name.Should().Be("Test");
            context.Users.Count().Should().Be(1);
        }

        // 2. Успешный вход
        [Fact]
        public async Task AuthService_Login_ShouldReturnUser()
        {
            using var context = GetInMemoryContext("LoginTest");
            var authService = new AuthService(context);
            await authService.Register("Test", "test@test.com", "1234");

            var (success, message, user) = await authService.Login("test@test.com", "1234");

            success.Should().BeTrue();
            user.Email.Should().Be("test@test.com");
        }

        // 3. Неверный пароль
        [Fact]
        public async Task AuthService_Login_WrongPassword_ShouldFail()
        {
            using var context = GetInMemoryContext("WrongPassTest");
            var authService = new AuthService(context);
            await authService.Register("Test", "test@test.com", "1234");

            var (success, message, user) = await authService.Login("test@test.com", "wrong");

            success.Should().BeFalse();
            message.Should().Contain("Неверный логин или пароль");
        }

        // 4. Получение активных языков
        [Fact]
        public async Task AuthService_GetAllLanguages_ShouldReturnActiveLanguages()
        {
            using var context = GetInMemoryContext("LanguagesTest");
            context.Languages.AddRange(
                new Language { Id = 1, Name = "English", IsActive = true },
                new Language { Id = 2, Name = "Russian", IsActive = false }
            );
            await context.SaveChangesAsync();
            var authService = new AuthService(context);

            var languages = await authService.GetAllLanguages();

            languages.Count.Should().Be(1);
            languages.First().Name.Should().Be("English");
        }

        // 5. Сохранение профиля (ProfileViewModel)
        [Fact]
        public async Task ProfileViewModel_SaveProfile_UpdatesUserName()
        {
            using var context = GetInMemoryContext("ProfileTest");
            var user = new User { Id = Guid.NewGuid(), Name = "OldName", Email = "test@test.com" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var mainVM = new MainViewModel();
            var profileVM = new ProfileViewModel(user, mainVM);
            profileVM.Name = "NewName";
            await profileVM.SaveCommand.Execute();

            var updatedUser = await context.Users.FindAsync(user.Id);
            updatedUser.Name.Should().Be("NewName");
        }

        // 6. Проверка несовпадения паролей в RegisterViewModel
        [Fact]
        public async Task RegisterViewModel_WhenPasswordsDoNotMatch_StatusIsSet()
        {
            using var context = GetInMemoryContext("RegNavTest");
            var authService = new AuthService(context);
            var mainVM = new MainViewModel();
            var registerVM = new RegisterViewModel(authService, mainVM);
            registerVM.Password = "1234";
            registerVM.ConfirmPassword = "4321";

            await registerVM.RegisterCommand.Execute(); // Ждём завершения

            registerVM.Status.Should().Be("❌ Пароли не совпадают");
        }

        // 7. Выдача достижения (требует правильного ConditionType)
        [Fact]
        public async Task AchievementService_CheckAndAwardAchievements_ShouldAwardWhenXpReached()
        {
            using var context = GetInMemoryContext("AchievementTest");
            var user = new User { Id = Guid.NewGuid(), TotalXp = 100 };
            var achievement = new Achievement { Id = 1, Code = "XP100", ConditionType = "total_xp", ConditionValue = 100, XpReward = 10 };
            context.Users.Add(user);
            context.Achievements.Add(achievement);
            await context.SaveChangesAsync();

            var service = new AchievementService(context);
            var earned = await service.CheckAndAwardAchievements(user.Id);

            earned.Should().Contain(a => a.Id == 1);
            var updatedUser = await context.Users.FindAsync(user.Id);
            updatedUser.TotalXp.Should().Be(110);
        }

        // 8. Регистрация с выбором языка (через LanguageSelectionViewModel)
        [Fact]
        public async Task LanguageSelectionViewModel_RegisterWithLanguage_ShouldCreateUser()
        {
            using var context = GetInMemoryContext("LangSelectTest");
            var authService = new AuthService(context);
            var mainVM = new MainViewModel();

            // Добавляем тестовые языки
            context.Languages.AddRange(
                new Language { Id = 1, Code = "en", Name = "English", IsActive = true },
                new Language { Id = 2, Code = "ru", Name = "Russian", IsActive = true }
            );
            await context.SaveChangesAsync();

            var langVM = new LanguageSelectionViewModel(authService, mainVM, "TestUser", "test@test.com", "1234");
            // Имитируем выбор языка
            langVM.SelectedTargetLanguage = context.Languages.First(l => l.Code == "en");
            langVM.SelectedNativeLanguage = context.Languages.First(l => l.Code == "ru");
            langVM.SelectedDifficulty = "beginner";

            await langVM.RegisterCommand.Execute();

            // Проверяем, что пользователь создан с выбранными языками
            var user = context.Users.FirstOrDefault(u => u.Email == "test@test.com");
            user.Should().NotBeNull();
            user.TargetLanguageId.Should().Be(1);
            user.NativeLanguageId.Should().Be(2);
            user.DifficultyLevel.Should().Be("beginner");
        }
    }
}