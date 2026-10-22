using System.Security.Cryptography;
using System.Text;

namespace Ampheck.Adb.Authentication;

internal sealed class AdbKey : IDisposable {
    private readonly RSA _rsa;
    private readonly string _publicPayload;

    public AdbKey(RSA rsa) {
        _rsa = rsa;
        var publicKey = Convert.ToBase64String(
            rsa.ExportRSAPublicKey());
        _publicPayload = publicKey + " ampheck@rockchip-toolkit\0";
    }

    public byte[] Sign(ReadOnlySpan<byte> token) {
        return _rsa.SignData(
            token.ToArray(),
            HashAlgorithmName.SHA1,
            RSASignaturePadding.Pkcs1);
    }

    public byte[] PublicKeyPayload() {
        return Encoding.ASCII.GetBytes(_publicPayload);
    }

    public void Dispose() {
        _rsa.Dispose();
    }
}
