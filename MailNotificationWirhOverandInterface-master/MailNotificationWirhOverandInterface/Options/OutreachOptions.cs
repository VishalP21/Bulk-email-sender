namespace MailNotificationWirhOverandInterface.Options;

public class OutreachOptions
{
    public const string SectionName = "Outreach";

    public int MaxRecipientsPerSend { get; set; } = 30;

    public int DelayMilliseconds { get; set; } = 1200;
}
