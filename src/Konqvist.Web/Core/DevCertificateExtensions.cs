using System.Diagnostics;

namespace Konqvist.Web.Core;

public static class DevCertificateExtensions
{

    /// <summary>
    ///     This extension method tries to configure Kestrel to bind a locally generated
    ///     certificate `devcert.pfx`. This certificate is necessary if you want to access
    ///     your app over ssl on the local network. 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="port">This is the port that Kestrel will be listening on</param>
    /// <remarks>
    ///     To generate a local development certificate that supports this feature, you can
    ///     generate one by running the `setup-local-ssl.cmd` command from an elevated shell
    /// </remarks>
    public static void AddLocalDevCertificate(this WebApplicationBuilder builder, int port)
    {
        if (!builder.Environment.IsDevelopment()) return;
            
        // Look for devcert.pfx in the obj folder
        string certPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".certs", "devcert.pfx");
        
        if (File.Exists(certPath))
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(port, listenOptions =>
                {
                    listenOptions.UseHttps(certPath, "");
                });
            });
        }
        else
        {
            builder.WebHost.UseUrls($"https://0.0.0.0:{port}");
            // Print colored info message for missing dev cert
            const string cyan = "\u001b[36m"; // ANSI cyan
            const string reset = "\u001b[0m";
            Console.WriteLine($"{cyan}• To enable local network HTTPS access, run: setup-local-ssl.cmd{reset}");
            Debug.WriteLine("• To enable local network HTTPS access, run: setup-local-ssl.cmd");
        }
    }
}