# Development seed kullanıcıları ve login düzeltmesi

## Amaç

Development ortamındaki Admin ve Employee hesaplarının `.env` dosyasındaki bilgilerle oluşturulması ve parola değiştiğinde mevcut hesabın güncellenmesi sağlandı.

## Kök neden

`SecureShop.Api` projesinin .NET User Secrets deposunda eski `SeedUsers:*` değerleri bulunuyordu. `DotEnvConfiguration.AddMissingFromDotEnv` yalnızca eksik değerleri eklediği için User Secrets içindeki eski parola `.env` parolasından önce kullanılıyordu.

Seed kullanıcı anahtarları User Secrets deposundan kaldırıldı. `Email:SenderName` gibi seed kullanıcılarla ilgisiz ayarlara dokunulmadı.

## Yapılandırma

Dosya yolu: `.env`

Uzantı: `.env`

Gerçek parolalar yalnızca git tarafından yok sayılan `.env` dosyasında tutulur. Dokümantasyonda tekrar edilmez.

```dotenv
SeedUsers__Admin__Email=admin@secureshop.local
SeedUsers__Admin__Password="<DEVELOPMENT_ADMIN_PASSWORD>"
SeedUsers__Admin__FirstName=System
SeedUsers__Admin__LastName=Administrator

SeedUsers__Employee__Email=employee@secureshop.local
SeedUsers__Employee__Password="<DEVELOPMENT_EMPLOYEE_PASSWORD>"
SeedUsers__Employee__FirstName=Development
SeedUsers__Employee__LastName=Employee
```

User Secrets içindeki eski seed anahtarlarını kontrol etmek için:

```powershell
dotnet user-secrets list --project src/SecureShop.Api/SecureShop.Api.csproj
```

## Parola senkronizasyonu

Dosya yolu: `src/SecureShop.Api/Data/Seed/IdentitySeeder.cs`

Uzantı: `.cs`

Mevcut development kullanıcısı bulunduğunda çalışan ek kod:

```csharp
else if (!string.IsNullOrWhiteSpace(password))
{
    var resetToken =
        await _userManager.GeneratePasswordResetTokenAsync(user);

    var resetResult = await _userManager.ResetPasswordAsync(
        user,
        resetToken,
        password);

    ThrowIfFailed(
        resetResult,
        $"{roleName} development user password could not be synchronized.");

    var resetAccessFailedResult =
        await _userManager.ResetAccessFailedCountAsync(user);

    ThrowIfFailed(
        resetAccessFailedResult,
        $"{roleName} development user access-failed count could not be reset.");

    var clearLockoutResult =
        await _userManager.SetLockoutEndDateAsync(user, null);

    ThrowIfFailed(
        clearLockoutResult,
        $"{roleName} development user lockout could not be cleared.");

    if (!await _userManager.CheckPasswordAsync(user, password))
    {
        throw new InvalidOperationException(
            $"{roleName} development user password synchronization verification failed.");
    }

    _logger.LogInformation(
        "{RoleName} development user password synchronized from development configuration.",
        roleName);
}
```

Bu kod yalnızca `Development` ortamında çalışır. API her başlatıldığında yapılandırılmış parola mevcut seed hesabıyla senkronize edilir, kilit süresi temizlenir ve başarısız giriş sayacı sıfırlanır.

## Login sonucunun ayrıştırılması

Dosya yolu: `src/SecureShop.Api/Controllers/LocalAuthController.cs`

Uzantı: `.cs`

`PasswordSignInAsync` sonucu artık aşağıdaki durumları ayrı HTTP kodlarıyla döndürür:

```text
Başarılı             204 No Content
Kilitli hesap          423 Locked
2FA gerekli           409 Conflict
Girişe izin verilmiyor 403 Forbidden
Hatalı bilgi           401 Unauthorized
```

## Doğrulama

```text
Solution Release build    : 0 uyarı, 0 hata
Employee login            : 204 No Content
Authentication cookie     : oluşturuldu
Employee rolü             : Employee
AccessFailedCount         : 0
LockoutEnd                : NULL
MVC form sonucu           : /account/session
MVC authentication cookie : oluşturuldu
MVC oturum kullanıcısı    : employee@secureshop.local
```
