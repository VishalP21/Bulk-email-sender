using System.Text.Json;
using MailNotificationWirhOverandInterface.Models;
using Microsoft.AspNetCore.DataProtection;

namespace MailNotificationWirhOverandInterface.Services;

public interface IMailSettingsStore
{
    MailSettings Get();

    string? ReadPassword();

    void Save(MailSettings settings);

    void Clear();
}

public sealed class MailSettingsStore : IMailSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly IDataProtector _protector;
    private readonly ILogger<MailSettingsStore> _logger;
    private readonly object _gate = new();

    public MailSettingsStore(
        IDataProtectionProvider protection,
        ILogger<MailSettingsStore> logger)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruiterMail");
        _path = Path.Combine(root, "settings.json");
        _protector = protection.CreateProtector("RecruiterMail.MailAccount.v1");
        _logger = logger;
    }

    public MailSettings Get()
    {
        lock (_gate)
        {
            var stored = Read();
            return ToSettings(stored, CanReadPassword(stored));
        }
    }

    public string? ReadPassword()
    {
        lock (_gate)
        {
            return Unprotect(Read().ProtectedPassword);
        }
    }

    public void Save(MailSettings settings)
    {
        lock (_gate)
        {
            var existing = Read();
            var protectedPassword = existing.ProtectedPassword;
            var typedPassword = (settings.AppPassword ?? "").Replace(" ", "", StringComparison.Ordinal).Trim();
            if (!string.IsNullOrWhiteSpace(typedPassword))
            {
                protectedPassword = _protector.Protect(typedPassword);
            }

            var stored = new StoredSettings
            {
                YourName = settings.YourName.Trim(),
                Role = settings.Role.Trim(),
                Phone = settings.Phone?.Trim() ?? "",
                Link = settings.Link?.Trim() ?? "",
                Subject = settings.Subject.Trim(),
                Body = settings.Body.Trim(),
                FromEmail = settings.FromEmail.Trim(),
                Host = string.IsNullOrWhiteSpace(settings.Host) ? "smtp.gmail.com" : settings.Host.Trim(),
                Port = settings.Port <= 0 ? 587 : settings.Port,
                ProtectedPassword = protectedPassword
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(stored, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }

    private StoredSettings Read()
    {
        if (!File.Exists(_path))
        {
            return new StoredSettings();
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<StoredSettings>(json) ?? new StoredSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Saved mail settings could not be read");
            return new StoredSettings();
        }
    }

    private bool CanReadPassword(StoredSettings stored)
    {
        return Unprotect(stored.ProtectedPassword) is not null;
    }

    private string? Unprotect(string? protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return null;
        }

        try
        {
            var password = _protector.Unprotect(protectedPassword);
            return string.IsNullOrWhiteSpace(password) ? null : password;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Saved app password could not be opened");
            return null;
        }
    }

    private static MailSettings ToSettings(StoredSettings stored, bool hasPassword)
    {
        return new MailSettings
        {
            YourName = stored.YourName,
            Role = stored.Role,
            Phone = stored.Phone,
            Link = stored.Link,
            Subject = stored.Subject,
            Body = stored.Body,
            FromEmail = stored.FromEmail,
            Host = string.IsNullOrWhiteSpace(stored.Host) ? "smtp.gmail.com" : stored.Host,
            Port = stored.Port is < 1 or > 65535 ? 587 : stored.Port,
            HasPassword = hasPassword,
            AppPassword = ""
        };
    }

    private sealed class StoredSettings
    {
        public string YourName { get; set; } = "";

        public string Role { get; set; } = "";

        public string Phone { get; set; } = "";

        public string Link { get; set; } = "";

        public string Subject { get; set; } = "";

        public string Body { get; set; } = "";

        public string FromEmail { get; set; } = "";

        public string Host { get; set; } = "smtp.gmail.com";

        public int Port { get; set; } = 587;

        public string ProtectedPassword { get; set; } = "";
    }
}
