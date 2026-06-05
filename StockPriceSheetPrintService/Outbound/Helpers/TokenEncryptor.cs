using System.Security.Cryptography;
using System.Text;

namespace StockPriceSheetPrintService.Outbound.Helpers
{
	public class TokenEncryptor
	{
		public static string Encrypt(string text, string key)
		{
			var iv = new byte[16];
			using var aes = Aes.Create();
			aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
			aes.IV = iv;

			using var encryptor = aes.CreateEncryptor();
			byte[] buffer = Encoding.UTF8.GetBytes(text);
			return Convert.ToBase64String(encryptor.TransformFinalBlock(buffer, 0, buffer.Length));
		}

		public static string Decrypt(string cipherText, string key)
		{
			using var aes = Aes.Create();
			aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
			aes.IV = new byte[16];

			using var decryptor = aes.CreateDecryptor();
			byte[] buffer = Convert.FromBase64String(cipherText);
			return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(buffer, 0, buffer.Length));
		}
	}
}
