using System.Security.Cryptography;
using System.Text;

namespace StockPriceSheetPrintService.Outbound.Helpers
{
	public class TokenEncryptor
	{
		public static string Encrypt(string text, string key)
		{
			using var aes = Aes.Create();
			aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
			aes.GenerateIV();

			using var encryptor = aes.CreateEncryptor();
			byte[] buffer = Encoding.UTF8.GetBytes(text);
			byte[] cipherBytes = encryptor.TransformFinalBlock(buffer, 0, buffer.Length);

			return Convert.ToBase64String([.. aes.IV, .. cipherBytes]);
		}

		public static string Decrypt(string cipherText, string key)
		{
			using var aes = Aes.Create();
			aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);

			byte[] combined = Convert.FromBase64String(cipherText);
			aes.IV = combined[..16];

			using var decryptor = aes.CreateDecryptor();
			return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(combined, 16, combined.Length - 16));
		}
	}
}
