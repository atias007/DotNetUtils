using MailKit.Net.Smtp;
using MimeKit;
using System.Net;
using System.Text;

namespace EmailTester;

internal static class SmtpUtil
{
    public static async Task<string> SendMessage(MimeMessage message, SmtpSettings smtp)
    {
        if (message.From.Count == 0)
        {
            message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        }

        using var client = new SmtpClient();
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await client.ConnectAsync(
            host: smtp.Host,
            port: smtp.Port,
            useSsl: smtp.UseSsl,
            cancellationToken: tokenSource.Token);

        if (smtp.UseDefaultCredentials)
        {
            await client.AuthenticateAsync(CredentialCache.DefaultCredentials, tokenSource.Token);
        }
        else
        {
            await client.AuthenticateAsync(encoding: Encoding.UTF8, smtp.Username, smtp.Password, tokenSource.Token);
        }

        var result = await client.SendAsync(message, tokenSource.Token);
        await client.DisconnectAsync(quit: true, tokenSource.Token);
        return result;
    }
}