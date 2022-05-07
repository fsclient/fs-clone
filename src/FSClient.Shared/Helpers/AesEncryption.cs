namespace FSClient.Shared.Helpers
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    public static class AesEncryption
    {
        public static string Encrypt(byte[] input, byte[] key, byte[] iv)
        {
            var aes = Aes.Create();
            aes.KeySize = 128;
            aes.BlockSize = 128;
            aes.Key = key;
            aes.IV = iv;

            var encrypt = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] xBuff;
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encrypt, CryptoStreamMode.Write))
                {
                    cs.Write(input, 0, input.Length);
                }

                xBuff = ms.ToArray();
            }

            return Convert.ToBase64String(xBuff);
        }

        public static string Decrypt(byte[] input, byte[] key, byte[] iv)
        {
            var aes = Aes.Create();
            aes.KeySize = 128;
            aes.BlockSize = 128;
            aes.Key = key;
            aes.IV = iv;

            var decrypt = aes.CreateDecryptor();
            byte[] xBuff;
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, decrypt, CryptoStreamMode.Write))
                {
                    cs.Write(input, 0, input.Length);
                }

                xBuff = ms.ToArray();
            }

            return Encoding.UTF8.GetString(xBuff, 0, xBuff.Length);
        }
    }
}
