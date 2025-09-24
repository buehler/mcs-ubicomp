using System;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace SolidInteractionLibrary
{

#nullable enable
  public partial class AuthenticatedPodClient
  {
    public static async Task<AuthenticationHeaderValue> LoginAsync(string serverUrl, string email, string password)
    {
      HttpClient client = new HttpClient();
      // HttpResponseMessage indexResponse = await client.GetAsync(serverUrl + ".account/");
      // indexResponse.EnsureSuccessStatusCode();
      // string indexResponseBody = await indexResponse.Content.ReadAsStringAsync();
      string indexResponseBody = await client.GetStringAsync(serverUrl + ".account/");
      JsonDocument indexResponseBodyJson = JsonDocument.Parse(indexResponseBody);
      if (indexResponseBodyJson.RootElement.TryGetProperty("controls", out JsonElement controlsElement))
      {
        if (controlsElement.TryGetProperty("password", out JsonElement passwordElement))
        {
          if (passwordElement.TryGetProperty("login", out JsonElement loginElement))
          {
            string? loginUrl = loginElement.GetString();
            HttpResponseMessage loginResponse = await client.PostAsync(
              loginUrl,
              new StringContent(JsonSerializer.Serialize(new { email, password }),
              Encoding.UTF8,
              "application/json")
            );
            string loginResponseBody = await loginResponse.Content.ReadAsStringAsync();
            JsonDocument loginResponseBodyJson = JsonDocument.Parse(loginResponseBody);
            if (!loginResponse.IsSuccessStatusCode)
            {
              string? errorMessage = loginResponseBodyJson.RootElement.GetProperty("message").GetString();
              throw new Exception("login failed. The server responded with the following message: " + errorMessage);
            }

            if (loginResponseBodyJson.RootElement.TryGetProperty("authorization", out JsonElement authorizationElement))
            {
              string? authorization = authorizationElement.GetString();
              if (authorization != null)
              {
                Console.WriteLine("Login successful");
                return new AuthenticationHeaderValue("CSS-Account-Token", authorization);
              }
              else
              {
                throw new Exception("authorization token from login not found");
              }
            }
            else
            {
              throw new Exception("authorization token from login not found");
            }
          }
          else
          {
            throw new Exception("login url not found in controls");
          }
        }
        else
        {
          throw new Exception("password controls not found");
        }
      }
      else
      {
        throw new Exception("controls not found");
      }
    }

    public static async Task<string> GenerateAccountTokenAsync(string serverUrl, AuthenticationHeaderValue authHeader, string webId)
    {
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Authorization = authHeader;
      HttpResponseMessage indexResponse = await client.GetAsync($"{serverUrl}.account/");
      indexResponse.EnsureSuccessStatusCode();
      string indexResponseBody = await indexResponse.Content.ReadAsStringAsync();
      JsonDocument indexResponseBodyJson = JsonDocument.Parse(indexResponseBody);
      if (indexResponseBodyJson.RootElement.TryGetProperty("controls", out JsonElement controlsElement))
      {
        if (controlsElement.TryGetProperty("account", out JsonElement accountElement))
        {
          if (accountElement.TryGetProperty("clientCredentials", out JsonElement clientCredentialsElement))
          {
            HttpResponseMessage clientCredentialsResponse = await client.PostAsync(
              clientCredentialsElement.GetString(),
              new StringContent(JsonSerializer.Serialize(new { name = "my-token", webId = webId }),
              Encoding.UTF8,
              "application/json")
            );
            string clientCredentialsResponseBody = await clientCredentialsResponse.Content.ReadAsStringAsync();
            JsonDocument clientCredentialsResponseBodyJson = JsonDocument.Parse(clientCredentialsResponseBody);
            if (!clientCredentialsResponse.IsSuccessStatusCode)
            {
              string? errorMessage = clientCredentialsResponseBodyJson.RootElement.GetProperty("message").GetString();
              throw new Exception("Getting clientCredentials failed. The server responded with the following message: " + errorMessage);
            }
            if (clientCredentialsResponseBodyJson.RootElement.TryGetProperty("id", out JsonElement idElement) &&
                clientCredentialsResponseBodyJson.RootElement.TryGetProperty("secret", out JsonElement secretElement) &&
                clientCredentialsResponseBodyJson.RootElement.TryGetProperty("resource", out JsonElement resourceElement))
            {
              string? id = idElement.GetString();
              string? secret = secretElement.GetString();
              // ensure id and secret are not null
              if (id != null && secret != null)
              {
                // form-encode the id and secret (RFC 1738)
                string authString = $"{Uri.EscapeDataString(id)}:{Uri.EscapeDataString(secret)}";
                Console.WriteLine("Account token generated");
                return authString;
              }
              else
              {
                throw new Exception("id, secret, or resource not found in clientCredentials response");
              }
            }
            else
            {
              throw new Exception("id, secret, or resource not found in clientCredentials response");
            }
          }
          else
          {
            throw new Exception("clientCredentials url not found in controls");
          }
        }
        else
        {
          throw new Exception("account controls not found");
        }
      }
      else
      {
        throw new Exception("controls not found");
      }
    }

    public async Task GenerateOIDCTokenAsync()
    {
      string tokenUrl = $"{serverUrl}.oidc/token";
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Add(
          "Authorization",
          $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(authString))}"
      );
      // Console.WriteLine("Authorisation: " + $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(authString))}");
      string customDPoP = BuildJwtForContent("POST", tokenUrl, privateKey, publicRSAKey);
      // Console.WriteLine("customDPoP1 " + customDPoP);
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      HttpResponseMessage response = await client.PostAsync(tokenUrl, new StringContent("grant_type=client_credentials&scope=webid", Encoding.UTF8, "application/x-www-form-urlencoded"));
      response.EnsureSuccessStatusCode();

      string responseBody = await response.Content.ReadAsStringAsync();
      JsonDocument responseBodyJson = JsonDocument.Parse(responseBody);
      // Console.WriteLine("responseBody " + responseBody);
      if (responseBodyJson.RootElement.TryGetProperty("access_token", out JsonElement accessTokenElement))
      {
        string? accessToken = accessTokenElement.GetString();
        if (accessToken != null)
        {
          Console.WriteLine("OIDC token generated");
          oidcToken = accessToken;
        }
        else
        {
          throw new Exception("No access token found in response.");
        }
      }
      else
      {
        throw new Exception("No access token found in response.");
      }
    }

    public static string BuildJwtForContent(string httpMethod, string resourceUri, RSAParameters _privateKey, RsaSecurityKey _publicRSAKey)
    {
      // Console.WriteLine("Building JWT for content");
      // Console.WriteLine("HTTP Method: " + httpMethod);
      // Console.WriteLine("Resource URI: " + resourceUri);

      // a secret key that we know
      var key = new RsaSecurityKey(_privateKey);

      // how this token is generated
      var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

      // https://solid.github.io/solid-oidc/primer/#authorization-code-pkce-flow
      // per the above, we need to send as "dpop+jwt"
      // we'll add this in later, but for now, add our signing creds
      var header = new JwtHeader(creds);

      // Microsoft's API automatically adds this as "jwt",
      // but we need to set it to "dpop+jwt",
      // so let's remove it and add it back later
      header.Remove("typ");
      header.Add("typ", "dpop+jwt");
      var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(_publicRSAKey);

      // not sure why, but Solid wants the alg property...
      jwk.AdditionalData.Add("alg", "RS256");

      var jwkDictionary = new Dictionary<string, object>
      {
          { "kty", jwk.Kty },
          { "e", jwk.E },
          { "n", jwk.N },
          { "alg", "RS256" },
          // { "use", jwk.Use },
          // { "kid", jwk.Kid }
      };

      // header.Add("jwk", jwk);
      header.Add("jwk", jwkDictionary);


      var payload = new JwtPayload(new List<Claim>());
      payload.AddClaim(new Claim("htu", resourceUri));
      payload.AddClaim(new Claim("htm", httpMethod));
      // unique identifier for the token
      payload.AddClaim(new Claim("jti", Guid.NewGuid().ToString()));

      // we rebuild the token with all the additional headers, claims, etc.
      var dpopToken = new JwtSecurityToken(header, payload);

      var utc0 = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
      var issueTime = DateTime.UtcNow;

      double CurrentIat = (int)issueTime.Subtract(utc0).TotalSeconds;
      dpopToken.Payload.AddClaim(new Claim("iat", CurrentIat.ToString(), ClaimValueTypes.Integer));
      var text = new JwtSecurityTokenHandler().WriteToken(dpopToken);
      // Console.WriteLine(text);
      return text;
    }
  }
}
