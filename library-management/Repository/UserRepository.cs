using LibraryManagement.Contexts;
using LibraryManagement.Dto;
using LibraryManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.UserRepository;

public class UserRepository : IUserRepository
{
    private readonly IDatabaseContext databaseContext;
    private readonly PasswordHasher<User> passwordHasher;

    public UserRepository(IDatabaseContext databaseContext)
    {
        this.databaseContext = databaseContext;
        this.passwordHasher = new PasswordHasher<User>();
    }

    public IEnumerable<UserResponseDto> GetAll()
    {
        var users = databaseContext
            .Users.Include(u => u.Loans)
            .Select(user => new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Role = user.Role,
                PhoneNumber = user.PhoneNumber,
            })
            .ToList();

        if (!users.Any())
        {
            throw new Exception("No users found");
        }

        return users;
    }

    public async Task<UserResponseDto> GetById(int userId)
    {
        var user = await databaseContext
            .Users.Include(u => u.Loans)
            .Where(u => u.Id == userId)
            .Select(user => new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Role = user.Role,
                PhoneNumber = user.PhoneNumber,
            })
            .FirstOrDefaultAsync();

        if (user == null)
            throw new Exception("User not found");

        return user;
    }

    public UserResponseDto Create(UserInsertDto userData)
    {
        var existingUserWithEmail = databaseContext.Users.Any(user => user.Email == userData.Email);
        if (existingUserWithEmail)
        {
            throw new Exception("Email already in use");
        }

        var existingUserWithPhone = databaseContext.Users.Any(user =>
            user.PhoneNumber == userData.PhoneNumber
        );
        if (existingUserWithPhone)
        {
            throw new Exception("Phone number already in use");
        }

        var newUser = new User
        {
            Email = userData.Email,
            Name = userData.Name,
            Role = userData.Role,
            PhoneNumber = userData.PhoneNumber,
        };

        newUser.Password = passwordHasher.HashPassword(newUser, userData.Password);

        databaseContext.Users.Add(newUser);
        databaseContext.SaveChanges();

        return new UserResponseDto
        {
            Id = newUser.Id,
            Name = newUser.Name,
            Email = newUser.Email,
            Role = newUser.Role,
            PhoneNumber = newUser.PhoneNumber,
        };
    }

    public async Task Update(User userData)
    {
        databaseContext.Users.Update(userData);
        await databaseContext.SaveChangesAsync();
    }

    public UserResponseDto Login(UserLoginDto userLoginData)
    {
        var user = databaseContext.Users.FirstOrDefault(u => u.Email == userLoginData.Email);
        if (user == null)
        {
            throw new Exception("Not found any user for this E-Mail");
        }

        var result = passwordHasher.VerifyHashedPassword(
            user,
            user.Password,
            userLoginData.Password
        );
        if (result == PasswordVerificationResult.Failed)
        {
            throw new Exception("Invalid password");
        }

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            PhoneNumber = user.PhoneNumber,
        };
    }

    public async Task Remove(int id)
    {
        var user = await databaseContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            throw new Exception("User not found");
        }

        databaseContext.Users.Remove(user);
        await databaseContext.SaveChangesAsync();
    }

    public async Task<UserDto> GetUserByEmail(string email)
    {
        var userModel = await databaseContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (userModel == null)
        {
            throw new Exception("User not found");
        }
        ;

        return new UserDto
        {
            Name = userModel.Name,
            Email = userModel.Email,
            Password = userModel.Password,
            Role = userModel.Role,
            PhoneNumber = userModel.PhoneNumber,
        };
    }

    public async Task<PasswordResetTokenResponseDto> CreatePasswordResetToken(UserDto userDto)
    {
        var userModel = await databaseContext.Users.FirstOrDefaultAsync(u =>
            u.Email == userDto.Email
        );
        if (userModel == null)
        {
            throw new Exception("User not found");
        }

        var token = Guid.NewGuid().ToString();
        var expirationDate = DateTime.UtcNow.AddHours(24);

        var resetToken = new PasswordResetToken
        {
            UserId = userModel.Id,
            Token = token,
            ExpirationDate = expirationDate,
        };

        databaseContext.PasswordResetTokens.Add(resetToken);
        await databaseContext.SaveChangesAsync();

        return new PasswordResetTokenResponseDto
        {
            Id = resetToken.Id,
            Token = resetToken.Token,
            ExpirationDate = resetToken.ExpirationDate,
            UserId = resetToken.UserId,
            User = userDto,
        };
    }

    public async Task<PasswordResetTokenResponseDto> GetPasswordResetToken(string token)
    {
        var tokenEntity = await databaseContext
            .PasswordResetTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && t.ExpirationDate > DateTime.UtcNow);

        if (tokenEntity == null)
        {
            {
                throw new Exception("tokenEntity not found");
            }
        }

        return new PasswordResetTokenResponseDto
        {
            Id = tokenEntity.Id,
            Token = tokenEntity.Token,
            ExpirationDate = tokenEntity.ExpirationDate,
            UserId = tokenEntity.UserId,
            User = new UserDto
            {
                Name = tokenEntity.User.Name,
                Email = tokenEntity.User.Email,
                Password = tokenEntity.User.Password,
                Role = tokenEntity.User.Role,
            },
        };
    }

    public async Task RemovePasswordResetToken(PasswordResetTokenResponseDto tokenDto)
    {
        var tokenEntity = await databaseContext.PasswordResetTokens.FirstOrDefaultAsync(t =>
            t.Id == tokenDto.Id
        );

        if (tokenEntity == null)
            throw new Exception("Token not found");

        databaseContext.PasswordResetTokens.Remove(tokenEntity);
        await databaseContext.SaveChangesAsync();
    }

    public async Task UpdateUserPassword(UserDto userDto, string newPassword)
    {
        var userEntity = await databaseContext.Users.FirstOrDefaultAsync(u =>
            u.Email == userDto.Email
        );

        if (userEntity == null)
            throw new Exception("User not found");

        userEntity.Password = passwordHasher.HashPassword(userEntity, newPassword);
        databaseContext.Users.Update(userEntity);
        await databaseContext.SaveChangesAsync();
    }

    public async Task<bool> ExistsWithEmail(string email, int excludeUserId)
    {
        return await databaseContext.Users.AnyAsync(u => u.Email == email && u.Id != excludeUserId);
    }

    public async Task<bool> ExistsWithPhoneNumber(string phoneNumber, int excludeUserId)
    {
        return await databaseContext.Users.AnyAsync(u =>
            u.PhoneNumber == phoneNumber && u.Id != excludeUserId
        );
    }

    public async Task<User> GetEntityById(int id)
    {
        var user = await databaseContext
            .Users.Include(u => u.Loans)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            throw new Exception("User not found");

        return user;
    }
};
