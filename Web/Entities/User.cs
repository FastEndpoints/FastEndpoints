namespace Web.Entities
{
    public record User(
        string? UserName,
        string? PasswordHash,
        string? Email,
        string? UserType,
        bool IsAdmin
        );
}
