using System.Text;

namespace Finalpass.Cryptography.Tests;

public sealed class SodiumKnownAnswerTests
{
    [Fact]
    public void XChaCha20Poly1305MatchesLibsodiumVector()
    {
        byte[] key = Enumerable.Range(0x80, 32).Select(value => (byte)value).ToArray();
        byte[] nonce = Convert.FromHexString(
            "07000000404142434445464748494A4B4C4D4E4F50515253");
        byte[] associatedData = Convert.FromHexString("50515253C0C1C2C3C4C5C6C7");
        byte[] message = Encoding.ASCII.GetBytes(
            "Ladies and Gentlemen of the class of '99: If I could offer you " +
            "only one tip for the future, sunscreen would be it.");
        byte[] expected = Convert.FromHexString(
            "F8EBEA4875044066FC162A0604E171FEECFB3D20425248563BCFD5A155DCC47B" +
            "BDA70B86E5AB9B55002BD1274C02DB35321ACD7AF8B2E2D25015E136B7679458" +
            "E9F43243BF719D639BADB5FEAC03F80A19A96EF10CB1D15333A837B90946BA38" +
            "54EE74DA3F2585EFC7E1E170E17E15E563E77601F4F85CAFA8E5877614E143E6" +
            "8420");

        byte[] actual = Sodium.Encrypt(message, nonce, key, associatedData);

        Assert.Equal(expected, actual);
        Assert.Equal(message, Sodium.Decrypt(actual, nonce, key, associatedData));
    }

    [Fact]
    public void Argon2IdMatchesReferenceVector()
    {
        byte[] password = Convert.FromHexString(
            "A347AE92BCE9F80F6F595A4480FC9C2FE7E7D7148D371E9487D75F5C23008FFA" +
            "E065577A928FEBD9B1973A5A95073ACDBEB6A030CFC0D79CAA2DC5CD011CEF02C" +
            "08DA232D76D52DFBCA38CA8DCBD665B17D1665F7CF5FE59772EC909733B24DE9" +
            "7D6F58D220B20C60D7C07EC1FD93C52C31020300C6C1FACD77937A597C7A6");
        byte[] salt = Convert.FromHexString("5541FBC995D5C197BA290346D2C559DE");
        byte[] expected = Convert.FromHexString(
            "B55073915DA6375037FC90291233A9F53A2A936D5B3FB008F849BC3E8D920197");
        byte[] actual = new byte[Sodium.KeyBytes];

        Sodium.DeriveArgon2IdKey(password, salt, 5, 7_256_678, actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ModifiedCiphertextIsRejected()
    {
        byte[] key = Sodium.RandomBytes(Sodium.KeyBytes);
        byte[] nonce = Sodium.RandomBytes(Sodium.NonceBytes);
        byte[] ciphertext = Sodium.Encrypt("secret"u8, nonce, key, "header"u8);
        ciphertext[0] ^= 1;

        Assert.Throws<VaultAuthenticationException>(
            () => Sodium.Decrypt(ciphertext, nonce, key, "header"u8));
    }
}
