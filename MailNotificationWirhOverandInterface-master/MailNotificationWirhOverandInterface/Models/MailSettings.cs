using System.ComponentModel.DataAnnotations;

namespace MailNotificationWirhOverandInterface.Models;

public class MailSettings
{
    [StringLength(80)]
    public string YourName { get; set; } = "";

    [StringLength(80)]
    public string Role { get; set; } = "";

    [StringLength(40)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? Link { get; set; }

    [StringLength(200)]
    public string Subject { get; set; } = "";

    [StringLength(4000)]
    public string Body { get; set; } = "";

    [Required(ErrorMessage = "Enter the Gmail address you send from.")]
    [EmailAddress(ErrorMessage = "Enter a valid From email.")]
    public string FromEmail { get; set; } = "";

    [StringLength(200)]
    public string AppPassword { get; set; } = "";

    [Required]
    [StringLength(120)]
    public string Host { get; set; } = "smtp.gmail.com";

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool HasPassword { get; set; }
}
