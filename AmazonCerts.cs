using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace ObeliskPanel;

public static class AmazonCerts
{
    const string RootThumbprint = "8DA7F965EC5EFC37910F1C6E59FDC1CC6A6EDE16";
    const string RootUrl = "https://www.amazontrust.com/repository/AmazonRootCA1.cer";
    const string M02Url = "https://crt.r2m02.amazontrust.com/r2m02.cer";
    const string M02Subject = "Amazon RSA 2048 M02";
    const string M02Thumbprint = "414A2060B738C635CC7FC243E052615592830C53";

    static bool InStore(StoreName name, Func<X509Certificate2, bool> match)
    {
        foreach (var loc in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
        {
            try
            {
                using var store = new X509Store(name, loc);
                store.Open(OpenFlags.ReadOnly);
                if (store.Certificates.Cast<X509Certificate2>().Any(match)) return true;
            }
            catch { }
        }
        return false;
    }

    public static bool RootInstalled() =>
        InStore(StoreName.Root, c => c.Thumbprint.Equals(RootThumbprint, StringComparison.OrdinalIgnoreCase));

    public static bool IntermediateInstalled() =>
        InStore(StoreName.CertificateAuthority, c => c.GetNameInfo(X509NameType.SimpleName, false) == M02Subject)
        || InStore(StoreName.Root, c => c.GetNameInfo(X509NameType.SimpleName, false) == M02Subject);

    public static bool Missing => !RootInstalled() || !IntermediateInstalled();

    public static async Task Install(Action<string> log)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        if (!RootInstalled())
        {
            var root = X509CertificateLoader.LoadCertificate(await http.GetByteArrayAsync(RootUrl));
            if (!root.Thumbprint.Equals(RootThumbprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Downloaded Amazon Root CA 1 does not match the expected fingerprint – not installed.");
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(root);
            log("Amazon Root CA 1 installed (Local Machine → Trusted Root Certification Authorities).");
        }
        if (!IntermediateInstalled())
        {
            var m02 = X509CertificateLoader.LoadCertificate(await http.GetByteArrayAsync(M02Url));
            if (!m02.Thumbprint.Equals(M02Thumbprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Downloaded Amazon RSA 2048 M02 certificate could not be verified – not installed.");
            using var store = new X509Store(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(m02);
            log("Amazon RSA 2048 M02 installed (Local Machine → Intermediate Certification Authorities).");
        }
    }

    public static async Task CheckAndPrompt(Window owner, Action<string> log)
    {
        if (!Missing) return;
        var answer = MessageBox.Show(owner,
            "The Amazon certificates needed by ARK: Survival Ascended servers are not installed on this PC " +
            "(Amazon Root CA 1 and Amazon RSA 2048 M02).\n\n" +
            "Without them ASA servers may not show up in the server list or connect to the online services correctly.\n\n" +
            "Download them from amazontrust.com and install them now?",
            "ObeliskPanel – Amazon certificates", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
        if (answer != MessageBoxResult.Yes) { log("Amazon certificates are missing – ASA servers may not work correctly."); return; }
        if (!App.IsAdmin)
        {
            MessageBox.Show(owner, "Installing certificates for the whole PC needs administrator rights. Restart ObeliskPanel as administrator and try again.",
                            "ObeliskPanel", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try { await Install(log); }
        catch (Exception ex)
        {
            log("Installing the Amazon certificates failed: " + ex.Message);
            MessageBox.Show(owner, "Installing the certificates failed:\n" + ex.Message, "ObeliskPanel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
