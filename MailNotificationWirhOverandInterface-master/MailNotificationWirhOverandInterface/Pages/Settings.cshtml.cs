using System.Net.Mail;
using MailNotificationWirhOverandInterface.Models;
using MailNotificationWirhOverandInterface.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MailNotificationWirhOverandInterface.Pages;

public class SettingsModel : PageModel
{
    private readonly IMailSettingsStore _settings;

    public SettingsModel(IMailSettingsStore settings)
    {
        _settings = settings;
    }

    [BindProperty]
    public MailSettings Input { get; set; } = new();

    public string? Banner { get; private set; }

    public bool BannerIsError { get; private set; }

    public void OnGet()
    {
        LoadInput();
        LoadBanner();
    }

    public IActionResult OnPost()
    {
        var existing = _settings.Get();
        Input.HasPassword = existing.HasPassword;
        var typedPassword = (Input.AppPassword ?? "").Replace(" ", "", StringComparison.Ordinal).Trim();

        if (string.IsNullOrWhiteSpace(typedPassword) && !existing.HasPassword)
        {
            ModelState.AddModelError(string.Empty, "Enter the app password.");
        }

        if (string.IsNullOrWhiteSpace(Input.Host) || Input.Host.Contains('/') || Input.Host.Contains(' '))
        {
            ModelState.AddModelError(nameof(Input.Host), "Enter a mail server such as smtp.gmail.com.");
        }

        if (!ModelState.IsValid)
        {
            Input.AppPassword = "";
            ModelState.Remove("Input.AppPassword");
            Banner = "Fix the highlighted fields, then save again.";
            BannerIsError = true;
            return Page();
        }

        Input.AppPassword = typedPassword;
        _settings.Save(Input);
        TempData["Banner"] = "Settings saved. The mail page will use this account.";
        return RedirectToPage();
    }

    public IActionResult OnPostClear()
    {
        _settings.Clear();
        TempData["Banner"] = "Saved settings were removed from this computer.";
        return RedirectToPage();
    }

    private void LoadInput()
    {
        Input = _settings.Get();
        var defaults = new ComposeInput();
        if (string.IsNullOrWhiteSpace(Input.Subject))
        {
            Input.Subject = defaults.Subject;
        }

        if (string.IsNullOrWhiteSpace(Input.Body))
        {
            Input.Body = defaults.Body;
        }

        if (!MailAddress.TryCreate(Input.FromEmail, out _))
        {
            Input.FromEmail = "";
        }
    }

    private void LoadBanner()
    {
        if (TempData["Banner"] is string banner)
        {
            Banner = banner;
            BannerIsError = TempData["BannerIsError"] as string == "1";
        }
    }
}
