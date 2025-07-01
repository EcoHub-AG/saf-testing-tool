using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Helpers
{
    public static class RSAKeyHelper
    {
        public static (string publicKeyPem, string privateKeyPem) GenerateKeyPair()
        {
            using (var rsa = RSA.Create(2048))
            {
                // Export public key in SPKI format
                string publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

                // Export private key in PKCS#8 format
                string privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

                return (publicKeyPem, privateKeyPem);
            }
        }



        public static bool ValidateKeyPair(string publicKeyPem, string privateKeyPem)
        {
            try
            {
                // Convert PEM to RSA objects
                using var rsaPublic = RSA.Create();
                using var rsaPrivate = RSA.Create();

                rsaPublic.ImportFromPem(publicKeyPem.ToCharArray());
                rsaPrivate.ImportFromPem(privateKeyPem.ToCharArray());

                // Test message
                string testMessage = "Test message for RSA key validation";

                // Encrypt with public key
                byte[] encryptedData = rsaPublic.Encrypt(Encoding.UTF8.GetBytes(testMessage), RSAEncryptionPadding.OaepSHA256);

                // Decrypt with private key
                byte[] decryptedData = rsaPrivate.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);

                // Convert decrypted data back to string
                string decryptedMessage = Encoding.UTF8.GetString(decryptedData);

                // Validation successful if decrypted message matches the original
                return testMessage == decryptedMessage;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
