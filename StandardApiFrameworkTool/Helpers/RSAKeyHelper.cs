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
                // Export keys in PEM format
                string publicKeyPem = ExportPublicKeyToPem(rsa);
                string privateKeyPem = ExportPrivateKeyToPem(rsa);

                return (publicKeyPem, privateKeyPem);
            }
        }

        private static string ExportPublicKeyToPem(RSA rsa)
        {
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            return ConvertToPem(publicKeyBytes, "PUBLIC KEY");
        }

        private static string ExportPrivateKeyToPem(RSA rsa)
        {
            var privateKeyBytes = rsa.ExportPkcs8PrivateKey();
            return ConvertToPem(privateKeyBytes, "PRIVATE KEY");
        }

        private static string ConvertToPem(byte[] keyBytes, string keyType)
        {
            var base64Key = Convert.ToBase64String(keyBytes, Base64FormattingOptions.InsertLineBreaks);
            var builder = new StringBuilder();
            builder.AppendLine($"-----BEGIN {keyType}-----");
            builder.AppendLine(base64Key);
            builder.AppendLine($"-----END {keyType}-----");
            return builder.ToString();
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
