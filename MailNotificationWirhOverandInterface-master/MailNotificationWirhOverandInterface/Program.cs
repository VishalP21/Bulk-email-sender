using MailNotificationWirhOverandInterface;
using Microsoft.AspNetCore.DataProtection;
using MailNotificationWirhOverandInterface.Options;
using MailNotificationWirhOverandInterface.Services;

var builder = WebApplication.CreateBuilder(args);

var protectionRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "RecruiterMail",
    "keys");
Directory.CreateDirectory(protectionRoot);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(protectionRoot))
    .SetApplicationName("RecruiterMail");

builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(45);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".recruiter.outreach";
});
builder.Services.Configure<OutreachOptions>(builder.Configuration.GetSection(OutreachOptions.SectionName));
builder.Services.AddHttpClient(SheetImportService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RecruiterOutreach/1.0");
});
builder.Services.AddSingleton<IMailSettingsStore, MailSettingsStore>();
builder.Services.AddScoped<ISheetImportService, SheetImportService>();
builder.Services.AddTransient<mailNotification>();
builder.Services.AddScoped<IOutreachMailService, OutreachMailService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();

app.Run();
