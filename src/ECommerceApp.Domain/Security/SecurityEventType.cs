namespace ECommerceApp.Domain.Security;

public enum SecurityEventType
{
    RegisterSuccess,
    RegisterFailure,
    LoginSuccess,
    LoginFailure,
    AccountLockedOut,
    PasswordChanged,
    PasswordResetRequested,
    PasswordResetCompleted,
    RefreshTokenIssued,
    RefreshTokenRotated,
    RefreshTokenReuseDetected,
    Logout,
    LogoutAllSessions,
    UserCreatedByAdmin,
    UserRoleChanged,
    UserActivated,
    UserDeactivated,
    UserUnlocked,
    StoreSettingsUpdated,
}
