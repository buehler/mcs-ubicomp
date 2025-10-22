using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;


namespace SolidInteractionLibrary
{
#nullable enable
  public partial class AuthenticatedPodClient
  {
    private readonly string serverUrl;
    private readonly string webId;
    private readonly RSAParameters privateKey;
    private readonly RsaSecurityKey publicRSAKey;
    private readonly AuthenticationHeaderValue authHeader;
    private readonly string authString;
    // private string podName;
    private string? oidcToken;


    public AuthenticatedPodClient(string serverUrl, string webId, RSAParameters privateKey, RsaSecurityKey publicRSAKey, AuthenticationHeaderValue authHeader, string authString)
    {
      this.serverUrl = serverUrl;
      this.webId = webId;
      this.privateKey = privateKey;
      this.publicRSAKey = publicRSAKey;
      this.authHeader = authHeader;
      this.authString = authString;
    }

    async public static Task<AuthenticatedPodClient> BuildAsync(string serverUrl, string webId, string email, string password)
    {

      // explicitly use 2048 bit key, otherwise privateKey.Modulus.Length is 128 and not the required 256 in Unity
      using (var rsa = new RSACryptoServiceProvider(2048))
      {
        RSAParameters publicKey = rsa.ExportParameters(false);
        RSAParameters privateKey = rsa.ExportParameters(true);
        // RsaSecurityKey privateRSAKey = new RsaSecurityKey(privateKey);
        RsaSecurityKey publicRSAKey = new RsaSecurityKey(publicKey);

        AuthenticationHeaderValue authHeader = await LoginAsync(serverUrl, email, password);
        string authString = await GenerateAccountTokenAsync(serverUrl, authHeader, webId);
        return new AuthenticatedPodClient(serverUrl, webId, privateKey, publicRSAKey, authHeader, authString);
      }
    }

    async private Task CreateOrRenewOIDCToken()
    {
      if (oidcToken == null)
      {
        await GenerateOIDCTokenAsync();
      }
      else
      {
        // check if token is still valid
        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt = handler.ReadJwtToken(oidcToken);
        // check if token is expired
        if (jwt.ValidTo.AddHours(2).AddMinutes(-1) < DateTime.Now)
        {
          await GenerateOIDCTokenAsync();
        }
      }
    }
  }
}