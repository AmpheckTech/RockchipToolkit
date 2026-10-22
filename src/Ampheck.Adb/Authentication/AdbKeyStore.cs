using System.Security.Cryptography;
using System.Text;

namespace Ampheck.Adb.Authentication;

internal static class AdbKeyStore {
    public static AdbKey? Load(bool createIfMissing) {
        var directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".android");
        var privateKeyPath = Path.Combine(directory, "adbkey");
        var publicKeyPath = privateKeyPath + ".pub";
        if (File.Exists(privateKeyPath)) {
            var rsa = RSA.Create();
            try {
                rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
                return new AdbKey(rsa);
            } catch {
                rsa.Dispose();
                throw;
            }
        }

        if (!createIfMissing) {
            return null;
        }

        Directory.CreateDirectory(directory);
        var newKey = RSA.Create(2048);
        try {
            File.WriteAllText(
                privateKeyPath,
                newKey.ExportPkcs8PrivateKeyPem(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (!OperatingSystem.IsWindows()) {
                File.SetUnixFileMode(
                    privateKeyPath,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite);
            }

            var publicKey = Convert.ToBase64String(
                newKey.ExportRSAPublicKey());
            File.WriteAllText(
                publicKeyPath,
                publicKey + " ampheck@rockchip-toolkit\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new AdbKey(newKey);
        } catch {
            newKey.Dispose();
            throw;
        }
    }
}
