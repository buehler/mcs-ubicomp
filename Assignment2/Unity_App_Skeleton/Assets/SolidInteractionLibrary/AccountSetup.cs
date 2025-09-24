using System;
using System.Text;
using System.Diagnostics;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SolidInteractionLibrary
{
#nullable enable
  public static partial class SolidClient
  {
    public static async Task<AuthenticationHeaderValue> CreateAccount(string serverUrl, string email, string password)
    {
      HttpClient client = new HttpClient();
      var indexResponse = await client.GetStringAsync(serverUrl + ".account/");
      JsonDocument indexResponseJson = JsonDocument.Parse(indexResponse);

      if (indexResponseJson.RootElement.TryGetProperty("controls", out JsonElement controlsElement))
      {
        if (controlsElement.TryGetProperty("account", out JsonElement accountElement))
        {
          if (accountElement.TryGetProperty("create", out JsonElement createElement))
          {
            string? createUrl = createElement.GetString();

            HttpResponseMessage createResponse = await client.PostAsync(createUrl, null);
            createResponse.EnsureSuccessStatusCode();
            string createResponseBody = await createResponse.Content.ReadAsStringAsync();
            JsonDocument createResponseBodyJson = JsonDocument.Parse(createResponseBody);
            if (createResponseBodyJson.RootElement.TryGetProperty("authorization", out JsonElement cookieElement))
            {
              var cookieHeaders = new AuthenticationHeaderValue("CSS-Account-Token", cookieElement.GetString());
              client.DefaultRequestHeaders.Authorization = cookieHeaders;
              var updatedIndexResponse = await client.GetStringAsync(serverUrl + ".account/");
              JsonDocument updatedIndexResponseJson = JsonDocument.Parse(updatedIndexResponse);

              if (updatedIndexResponseJson.RootElement.TryGetProperty("controls", out JsonElement updatedControlsElement))
              {
                if (updatedControlsElement.TryGetProperty("password", out JsonElement passwordElement))
                {
                  if (passwordElement.TryGetProperty("create", out JsonElement createPasswordElement))
                  {
                    string? createPasswordUrl = createPasswordElement.GetString();
                    HttpResponseMessage createPasswordResponse = await client.PostAsync(
                      createPasswordUrl,
                      new StringContent(JsonSerializer.Serialize(new { email, password }),
                      Encoding.UTF8,
                      "application/json")
                    );

                    if (!createPasswordResponse.IsSuccessStatusCode)
                    {
                      string responseMessage = createPasswordResponse.Content.ReadAsStringAsync().Result;
                      JsonDocument responseMessageJson = JsonDocument.Parse(responseMessage);
                      string? errorMessage = responseMessageJson.RootElement.GetProperty("message").GetString();
                      throw new Exception("creating account failed. The server responded with the following error: " + errorMessage);
                    }

                    Console.WriteLine("Account created");
                    return cookieHeaders;
                  }
                  else
                  {
                    throw new Exception("create password url not found in controls");
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
            else
            {
              throw new Exception("authorization token from create account not found");
            }
          }
          else
          {
            throw new Exception("create account url not found in controls");
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

    public static async Task CreatePod(string serverUrl, string podName, AuthenticationHeaderValue authHeader)
    {
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Authorization = authHeader;

      var loggedinIndexResponse = await client.GetStringAsync(serverUrl + ".account/");
      JsonDocument loggedinIndexResponseJson = JsonDocument.Parse(loggedinIndexResponse);

      if (loggedinIndexResponseJson.RootElement.TryGetProperty("controls", out JsonElement loggedinControlsElement))
      {
        if (loggedinControlsElement.TryGetProperty("account", out JsonElement accountElement))
        {
          if (accountElement.TryGetProperty("pod", out JsonElement podElement))
          {
            string? createPodUrl = podElement.GetString();
            HttpResponseMessage createPodResponse = await client.PostAsync(
              createPodUrl,
              new StringContent(JsonSerializer.Serialize(new { name = podName }),
              Encoding.UTF8,
              "application/json")
            );
            // Console.WriteLine(createPodResponse.Content.ReadAsStringAsync().Result);
            // createPodResponse.EnsureSuccessStatusCode();
            if (!createPodResponse.IsSuccessStatusCode)
            {
              string responseMessage = createPodResponse.Content.ReadAsStringAsync().Result;
              JsonDocument responseMessageJson = JsonDocument.Parse(responseMessage);
              string? errorMessage = responseMessageJson.RootElement.GetProperty("message").GetString();
              throw new Exception("creating pod failed. The server responded with the following error: " + errorMessage);
            }
            Console.WriteLine("Pod created");
          }
          else
          {
            throw new Exception("pod url not found in controls");
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

    public static async Task<List<string>> GetPods(string serverUrl, AuthenticationHeaderValue authHeader)
    {
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Authorization = authHeader;

      var loggedinIndexResponse = await client.GetStringAsync(serverUrl + ".account/");
      JsonDocument loggedinIndexResponseJson = JsonDocument.Parse(loggedinIndexResponse);

      if (loggedinIndexResponseJson.RootElement.TryGetProperty("controls", out JsonElement loggedinControlsElement))
      {
        if (loggedinControlsElement.TryGetProperty("account", out JsonElement accountElement))
        {
          if (accountElement.TryGetProperty("pod", out JsonElement podElement))
          {
            string? podUrl = podElement.GetString();
            HttpResponseMessage podResponse = await client.GetAsync(podUrl);
            string podResponseBody = await podResponse.Content.ReadAsStringAsync();
            List<string> podNames = new List<string>();
            JsonDocument podResponseJson = JsonDocument.Parse(podResponseBody);
            if (podResponseJson.RootElement.TryGetProperty("pods", out JsonElement podsElement))
            {
              foreach (var pod in podsElement.EnumerateObject())
              {
                string podName = pod.Name;
                podNames.Add(podName);
              }
              return podNames;
            }
            else
            {
              throw new Exception("items not found in pod response");
            }

          }
          else
          {
            throw new Exception("pod url not found in controls");
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

    public static async Task<List<string>> GetWebIds(string serverUrl, AuthenticationHeaderValue authHeader)
    {
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Authorization = authHeader;

      var loggedinIndexResponse = await client.GetStringAsync(serverUrl + ".account/");
      JsonDocument loggedinIndexResponseJson = JsonDocument.Parse(loggedinIndexResponse);

      if (loggedinIndexResponseJson.RootElement.TryGetProperty("controls", out JsonElement loggedinControlsElement))
      {
        if (loggedinControlsElement.TryGetProperty("account", out JsonElement accountElement))
        {
          if (accountElement.TryGetProperty("webId", out JsonElement webIdsElement))
          {
            string? webIdsUrl = webIdsElement.GetString();
            HttpResponseMessage webIdsResponse = await client.GetAsync(webIdsUrl);
            string webIdsResponseBody = await webIdsResponse.Content.ReadAsStringAsync();
            List<string> webIds = new List<string>();
            JsonDocument webIdsResponseJson = JsonDocument.Parse(webIdsResponseBody);
            if (webIdsResponseJson.RootElement.TryGetProperty("webIdLinks", out JsonElement webIdLinksElement))
            {
              foreach (var webId in webIdLinksElement.EnumerateObject())
              {
                if (webId.Name != null)
                  webIds.Add(webId.Name);
              }
              return webIds;
            }
            else
            {
              throw new Exception("items not found in webIds response");
            }

          }
          else
          {
            throw new Exception("webIds url not found in controls");
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


    // public static async Task Test(string serverUrl, AuthenticationHeaderValue authHeader, string webId)
    // {
    //   HttpClient client = new HttpClient();
    //   client.DefaultRequestHeaders.Authorization = authHeader;

    //   var loggedinIndexResponse = await client.GetStringAsync(serverUrl + ".account/");
    //   JsonDocument loggedinIndexResponseJson = JsonDocument.Parse(loggedinIndexResponse);

    //   if (loggedinIndexResponseJson.RootElement.TryGetProperty("controls", out JsonElement loggedinControlsElement))
    //   {
    //     if (loggedinControlsElement.TryGetProperty("account", out JsonElement accountElement))
    //     {

    //       if (accountElement.TryGetProperty("webId", out JsonElement webIdElement))
    //       {
    //         string? webIdUrl = webIdElement.GetString();
    //         HttpResponseMessage createWebIdResponse = await client.PostAsync(
    //           webIdUrl,
    //           new StringContent(JsonSerializer.Serialize(new { webId = webId }),
    //           Encoding.UTF8,
    //           "application/json")
    //         );
    //         Console.WriteLine(createWebIdResponse.Content.ReadAsStringAsync().Result);
    //         JsonDocument createWebIdResponseJson = JsonDocument.Parse(createWebIdResponse.Content.ReadAsStringAsync().Result);
    //         // {"name":"BadRequestHttpError","message":"Verification token not found. Please add the RDF triple <http://localhost:3000/testwebid/profile/card#me> <http://www.w3.org/ns/solid/terms#oidcIssuerRegistrationToken> \"69480c3f-ac36-4573-ae0f-908f9653a06d\". to the WebID document at http://localhost:3000/testwebid/profile/card to prove it belongs to you. You can remove this triple again after validation.","statusCode":400,"errorCode":"H400","details":{"quad":"<http://localhost:3000/testwebid/profile/card#me> <http://www.w3.org/ns/solid/terms#oidcIssuerRegistrationToken> \"69480c3f-ac36-4573-ae0f-908f9653a06d\"."}}

    //         string? quad = createWebIdResponseJson.RootElement.GetProperty("details").GetProperty("quad").GetString();
    //         Console.WriteLine("asdf" + quad);

    //         // add the quad to the webId
    //         HttpResponseMessage addQuadResponse = await client.PostAsync(
    //           webId,
    //           new StringContent(JsonSerializer.Serialize(new { quad }),
    //           Encoding.UTF8,
    //           "application/json")
    //         );
    //         Console.WriteLine(addQuadResponse.Content.ReadAsStringAsync().Result);
    //       }

    //     }
    //   }
    // }
  }
}