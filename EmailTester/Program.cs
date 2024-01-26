using EmailTester;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Net;

var x = CredentialCache.DefaultNetworkCredentials;
// *** Load Settings **************************************************************** //

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var smtp = config.GetSection("Smtp").Get<SmtpSettings>();
var toName = config.GetValue<string>("Smtp:ToName");
var toAddress = config.GetValue<string>("Smtp:ToAddress");

// *** Build Email Message **************************************************************** //

var message = new MimeMessage();
message.To.Add(new MailboxAddress(toName, toAddress));
message.Subject = "Test";
var body = new BodyBuilder
{
    TextBody = "This is test email",
    HtmlBody = "<html><body><h1>This is test email</h1></body></html>"
}.ToMessageBody();
message.Body = body;

// *** Send The Message **************************************************************** //

try
{
    var result = await SmtpUtil.SendMessage(message, smtp);
    Console.WriteLine(result);
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}