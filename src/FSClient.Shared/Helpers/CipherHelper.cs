namespace FSClient.Shared.Helpers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    [SuppressMessage("Security", "CA5379:Do Not Use Weak Key Derivation Function Algorithm")]
    public static class CipherHelper
    {
        private static readonly byte[] GlobalSalt =
            new byte[] { 0x14, 0x76, 0x61, 0x6e, 0x20, 0x4a, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

        public static string Encrypt(string clearText, string key)
        {
            var clearBytes = Encoding.Unicode.GetBytes(clearText);

            using var encryptor = Aes.Create();
            using var pdb = new Rfc2898DeriveBytes(key, GlobalSalt);
            encryptor.Key = pdb.GetBytes(32);
            encryptor.IV = pdb.GetBytes(16);

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(clearBytes, 0, clearBytes.Length);
            cs.FlushFinalBlock();

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText, string key)
        {
            var cipherBytes = Convert.FromBase64String(cipherText.Replace(" ", "+"));

            using var encryptor = Aes.Create();
            using var pdb = new Rfc2898DeriveBytes(key, GlobalSalt);
            encryptor.Key = pdb.GetBytes(32);
            encryptor.IV = pdb.GetBytes(16);

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(cipherBytes, 0, cipherBytes.Length);
            cs.FlushFinalBlock();

            return Encoding.Unicode.GetString(ms.ToArray());
        }
    }
}
