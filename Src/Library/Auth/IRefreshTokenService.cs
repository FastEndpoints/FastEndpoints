namespace FastEndpoints;

[HideFromDocs]
public interface IRefreshTokenService<TResponse>
{
    internal Task<TResponse> CreateToken(string userId, Action<UserPrivileges>? privileges, bool isRenewal, object? request);
}