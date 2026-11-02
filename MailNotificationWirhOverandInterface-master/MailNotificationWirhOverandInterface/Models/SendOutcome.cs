namespace MailNotificationWirhOverandInterface.Models;

public class SendOutcome
{
    public string CompanyName { get; set; } = "";

    public string Email { get; set; } = "";

    public bool Success { get; set; }

    public string Detail { get; set; } = "";
}
