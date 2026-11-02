using System.ComponentModel.DataAnnotations;

namespace MailNotificationWirhOverandInterface.Models;

public class ComposeInput
{
    [Required(ErrorMessage = "Enter your name.")]
    [StringLength(80)]
    public string YourName { get; set; } = "";

    [Required(ErrorMessage = "Enter the role you want.")]
    [StringLength(80)]
    public string Role { get; set; } = "";

    [StringLength(40)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? Link { get; set; }

    [StringLength(1000)]
    public string? Extra { get; set; }

    [Required(ErrorMessage = "Enter a subject.")]
    [StringLength(200)]
    public string Subject { get; set; } = "Interested in joining {{CompanyName}}";

    [Required(ErrorMessage = "Enter the mail message.")]
    [StringLength(4000)]
    public string Body { get; set; } =
        """
        Hello {{CompanyName}} team,

        My name is {{YourName}}. I am a {{Role}}, and I am interested in joining {{CompanyName}}.

        If you have an opening that fits, I would be glad to be considered. I can share more about my work and a time to talk.

        {{Extra}}

        Thank you,
        {{YourName}}
        {{Phone}}
        {{Link}}
        """;

    [EmailAddress(ErrorMessage = "Enter a valid From email.")]
    public string FromEmail { get; set; } = "";

    [StringLength(200)]
    public string FromPassword { get; set; } = "";

    [Required]
    [StringLength(120)]
    public string Host { get; set; } = "smtp.gmail.com";

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool Confirmed { get; set; }
}
