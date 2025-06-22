using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Helpers
{
    public static class ECDSAKeyHelper
    {
        public static (string publicKeyPem, string privateKeyPem) GenerateECDsaKeyPair()
        {
            using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384))
            {
                string publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
                string privateKeyPem = ecdsa.ExportPkcs8PrivateKeyPem();
                return (publicKeyPem, privateKeyPem);
            }
        }

        public static bool ValidateECDsaKeyPair(string publicKeyPem, string privateKeyPem)
        {
            try
            {
                // Create ECDSA objects
                using var ecdsaPrivate = ECDsa.Create();
                using var ecdsaPublic = ECDsa.Create();

                ecdsaPrivate.ImportFromPem(privateKeyPem.ToCharArray());
                ecdsaPublic.ImportFromPem(publicKeyPem.ToCharArray());

                // Test message
                string testMessage = "Test message for ECDSA key validation";
                byte[] messageBytes = Encoding.UTF8.GetBytes(testMessage);

                // Sign with private key
                byte[] signature = ecdsaPrivate.SignData(messageBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

                // Verify with public key
                bool isValid = ecdsaPublic.VerifyData(messageBytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

                return isValid;
            }
            catch (Exception)
            {
                return false;
            }
        }


    }
}
