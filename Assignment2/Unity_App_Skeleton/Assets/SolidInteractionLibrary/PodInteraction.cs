using System.Text;
using System.Text.Json;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Diagnostics;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;
using System.Net.Http.Headers;

namespace SolidInteractionLibrary
{
#nullable enable
  public partial class AuthenticatedPodClient
  {

    // get pods connected to the webId
    public async Task<List<string>> GetPods()
    {
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Authorization = authHeader;

      // get urls
      var loggedinIndexResponse = await client.GetStringAsync(serverUrl + ".account/");
      JsonDocument loggedinIndexResponseJson = JsonDocument.Parse(loggedinIndexResponse);

      if (loggedinIndexResponseJson.RootElement.TryGetProperty("controls", out JsonElement loggedinControlsElement))
      {
        if (loggedinControlsElement.TryGetProperty("account", out JsonElement accountElement))
        {
          if (accountElement.TryGetProperty("pod", out JsonElement podsUrlElement))
          {
            string? podsUrl = podsUrlElement.GetString();
            // get pods info
            HttpResponseMessage podsResponse = await client.GetAsync(podsUrl);
            string podsResponseBody = await podsResponse.Content.ReadAsStringAsync();
            JsonDocument podsResponseJson = JsonDocument.Parse(podsResponseBody);
            List<string> podNames = new List<string>();
            if (podsResponseJson.RootElement.TryGetProperty("pods", out JsonElement podsElement))
            {
              // check if user is owner of pod and add to list if true
              foreach (var podInfo in podsElement.EnumerateObject())
              {
                string podName = podInfo.Name;

                string podResponse = await client.GetStringAsync(podInfo.Value.GetString());
                JsonDocument podResponseJson = JsonDocument.Parse(podResponse);
                if (podResponseJson.RootElement.TryGetProperty("owners", out JsonElement ownersElement))
                {
                  foreach (var owner in ownersElement.EnumerateArray())
                  {
                    if (owner.TryGetProperty("webId", out JsonElement webIdElement))
                    {
                      string? podWebId = webIdElement.GetString();
                      if (podWebId == webId)
                      {
                        podNames.Add(podName);
                        break;
                      }
                    }
                  }
                }
                else
                {
                  throw new Exception("owners not found in pod response");
                }
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

    public async Task<string> SaveFileAsync(string url, string contentType, string fileContent)
    {
      await CreateOrRenewOIDCToken();
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      string customDPoP = BuildJwtForContent("PUT", url, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);

      HttpResponseMessage response = await client.PutAsync(url, new StringContent(fileContent, Encoding.UTF8, contentType));
      response.EnsureSuccessStatusCode();
      string? location = response.Headers.Location?.ToString();
      if (response.StatusCode == System.Net.HttpStatusCode.ResetContent)
      {
        Console.WriteLine("File updated at " + url);
        return url;
      }
      else if (location != null)
      {
        Console.WriteLine($"File saved at {location}");
        return location;
      }
      else
      {
        throw new Exception("No location header found in response.");
      }
    }

    public async Task<string> DeleteFileAsync(string url)
    {
      await CreateOrRenewOIDCToken();
      HttpClient client = new HttpClient();
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      string customDPoP = BuildJwtForContent("DELETE", url, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);

      HttpResponseMessage response = await client.DeleteAsync(url);
      response.EnsureSuccessStatusCode();
      // Console.WriteLine($"File deleted at {url}");
      return url;
    }

    public async Task<string> GetFileAsync(string location)
    {
      await CreateOrRenewOIDCToken();
      HttpClient client = new HttpClient();
      string customDPoP = BuildJwtForContent("GET", location, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      HttpResponseMessage response = await client.GetAsync(location);
      if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
      {
        throw new Exception("webId does not have read access to this resource");
      }
      else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
      {
        throw new Exception("Resource does not exist");
      }
      string responseBody = await response.Content.ReadAsStringAsync();
      return responseBody;
    }

    public async Task GrantAccessToPod(string podLocation, string webId, string accessType)
    {
      if (accessType != "Read" && accessType != "Write" && accessType != "ReadWrite")
      {
        throw new Exception("Invalid access type. Must be Read, Write or ReadWrite");
      }
      await CreateOrRenewOIDCToken();
      HttpClient client = new HttpClient();
      string customDPoP = BuildJwtForContent("GET", podLocation, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      HttpResponseMessage response = await client.GetAsync(podLocation);
      response.EnsureSuccessStatusCode();
      string aclLink = getAclLink(response);

      string customDPoP2 = BuildJwtForContent("GET", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP2);
      client.DefaultRequestHeaders.Add("Accept", "application/n-triples");
      // client.DefaultRequestHeaders.Add("Accept", "text/turtle");
      HttpResponseMessage aclResponse = await client.GetAsync(aclLink);
      aclResponse.EnsureSuccessStatusCode();
      string aclResponseBody = await aclResponse.Content.ReadAsStringAsync();

      Graph g = new Graph();
      StringParser.Parse(g, aclResponseBody, new TurtleParser());

      // check if any subject is <{podLocation}.acl#agent1>, <{podLocation}.acl#agent2>, etc.
      List<INode>? subjects = g.Triples
          .Where(t => t.Subject.ToString().Contains($"{podLocation}.acl#agent"))
          .Select(t => t.Subject)
          .ToList();

      // Find the highest number in the subjects
      int highestNumber = 0;
      foreach (var subject in subjects)
      {
        string subjectString = subject.ToString();
        int number = int.Parse(subjectString.Substring(subjectString.LastIndexOf("agent") + 5));
        if (number > highestNumber)
        {
          highestNumber = number;
        }
      }

      string newAcl = aclResponseBody + $@"
<{podLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/ns/auth/acl#Authorization> .
<{podLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#agent> <{webId}> .
<{podLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#accessTo> <{podLocation}> .
<{podLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#default> <{podLocation}> .";
      switch (accessType)
      {
        case "Read":
          newAcl += $"\n<{podLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .";
          break;
        case "Write":
          newAcl += $"\n<{podLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Write> .";
          break;
        case "ReadWrite":
          newAcl += $"\n<{podLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .";
          newAcl += $"\n<{podLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Write> .";
          break;
      }

      string customDPoP3 = BuildJwtForContent("PUT", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP3);

      HttpResponseMessage aclPutResponse = await client.PutAsync(
          aclLink,
          new StringContent(newAcl, Encoding.UTF8, "application/n-triples")
      );

      aclPutResponse.EnsureSuccessStatusCode();
      // Console.WriteLine($"Access to pod {podLocation} granted to {webId}");
    }

    public async Task GrantAccessToSubFolder(string folderLocation, string webId, string accessType)
    {
      if (accessType != "Read" && accessType != "Write" && accessType != "ReadWrite")
      {
        throw new Exception("Invalid access type. Must be Read, Write or ReadWrite");
      }
      await CreateOrRenewOIDCToken();
      HttpClient client = new HttpClient();
      string customDPoP = BuildJwtForContent("GET", folderLocation, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      HttpResponseMessage response = await client.GetAsync(folderLocation);
      response.EnsureSuccessStatusCode();

      string aclLink = getAclLink(response);
      string customDPoP2 = BuildJwtForContent("GET", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP2);
      client.DefaultRequestHeaders.Add("Accept", "application/n-triples");
      HttpResponseMessage aclResponse = await client.GetAsync(aclLink);

      int highestNumber = 0;
      if (aclResponse.StatusCode == System.Net.HttpStatusCode.OK)
      {
        Graph g = new Graph();
        StringParser.Parse(g, await aclResponse.Content.ReadAsStringAsync(), new TurtleParser());

        // check if any subject is <{folderLocation}.acl#agent1>, <{folderLocation}.acl#agent2>, etc.
        List<INode>? subjects = g.Triples
            .Where(t => t.Subject.ToString().Contains($"{folderLocation}.acl#agent"))
            .Select(t => t.Subject)
            .ToList();

        // Find the highest number in the subjects
        foreach (var subject in subjects)
        {
          string subjectString = subject.ToString();
          int number = int.Parse(subjectString.Substring(subjectString.LastIndexOf("agent") + 5));
          if (number > highestNumber)
          {
            highestNumber = number;
          }
        }
      }

      string accessString = $@"
<{folderLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/ns/auth/acl#Authorization> .
<{folderLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#agent> <{webId}> .
<{folderLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#accessTo> <{folderLocation}> .
<{folderLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#default> <{folderLocation}> .";
      switch (accessType)
      {
        case "Read":
          accessString += $"<{folderLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .";
          break;
        case "Write":
          accessString += $"<{folderLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Write> .";
          break;
        case "ReadWrite":
          accessString += $"<{folderLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .";
          accessString += $"<{folderLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Write> .";
          break;
      }

      string newAcl;
      if (aclResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
      {
        newAcl = $@"
<{folderLocation}.acl#public> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/ns/auth/acl#Authorization> .
<{folderLocation}.acl#public> <http://www.w3.org/ns/auth/acl#agentClass> <http://xmlns.com/foaf/0.1/Agent> .
<{folderLocation}.acl#public> <http://www.w3.org/ns/auth/acl#accessTo> <{folderLocation}> .
<{folderLocation}.acl#public> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .
<{folderLocation}.acl#owner> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/ns/auth/acl#Authorization> .
<{folderLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#agent> <{this.webId}> .
<{folderLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#accessTo> <{folderLocation}> .
<{folderLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#default> <{folderLocation}> .
<{folderLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .
<{folderLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Write> .
<{folderLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Control> ." + accessString;
      }
      else
      {
        string aclResponseBody = await aclResponse.Content.ReadAsStringAsync();
        newAcl = aclResponseBody + accessString;
      }

      string customDPoP3 = BuildJwtForContent("PUT", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP3);
      HttpResponseMessage aclPutResponse = await client.PutAsync(aclLink, new StringContent(newAcl, Encoding.UTF8, "text/turtle"));
      aclPutResponse.EnsureSuccessStatusCode();
      // Console.WriteLine($"Access to subfolder {folderLocation} granted to {webId}");
    }


    public async Task GrantAccessToFile(string fileLocation, string webId, string accessType)
    {
      if (accessType != "Read" && accessType != "Write" && accessType != "ReadWrite")
      {
        throw new Exception("Invalid access type. Must be Read, Write or ReadWrite");
      }
      await CreateOrRenewOIDCToken();
      HttpClient client = new HttpClient();
      string customDPoP = BuildJwtForContent("GET", fileLocation, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      HttpResponseMessage response = await client.GetAsync(fileLocation);
      response.EnsureSuccessStatusCode();
      string aclLink = getAclLink(response);

      string customDPoP2 = BuildJwtForContent("GET", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP2);
      client.DefaultRequestHeaders.Add("Accept", "application/n-triples");
      HttpResponseMessage aclResponse = await client.GetAsync(aclLink);

      string folderLocation = fileLocation.Substring(0, fileLocation.LastIndexOf("/"));
      int highestNumber = 0;
      if (aclResponse.StatusCode == System.Net.HttpStatusCode.OK)
      {

        Graph g = new Graph();
        StringParser.Parse(g, await aclResponse.Content.ReadAsStringAsync(), new TurtleParser());

        // check if any subject is <{fileLocation}.acl#agent1>, <{fileLocation}.acl#agent2>, etc.
        List<INode>? subjects = g.Triples
            .Where(t => t.Subject.ToString().Contains($"{fileLocation}.acl#agent"))
            .Select(t => t.Subject)
            .ToList();

        // Find the highest number in the subjects
        foreach (var subject in subjects)
        {
          string subjectString = subject.ToString();
          int number = int.Parse(subjectString.Substring(subjectString.LastIndexOf("agent") + 5));
          if (number > highestNumber)
          {
            highestNumber = number;
          }
        }
      }

      string accessString = $@"
<{fileLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/ns/auth/acl#Authorization> .
<{fileLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#agent> <{webId}> .
<{fileLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#accessTo> <{fileLocation}> .
<{fileLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#default> <{folderLocation}> .";
      switch (accessType)
      {
        case "Read":
          accessString += $"<{fileLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .";
          break;
        case "Write":
          accessString += $"<{fileLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Write> .";
          break;
        case "ReadWrite":
          accessString += $"<{fileLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .";
          accessString += $"<{fileLocation}.acl#agent{highestNumber + 1}> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Write> .";
          break;
      }

      string newAcl;
      if (aclResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
      {
        newAcl = $@"
<{fileLocation}.acl#public> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/ns/auth/acl#Authorization> .
<{fileLocation}.acl#public> <http://www.w3.org/ns/auth/acl#agentClass> <http://xmlns.com/foaf/0.1/Agent> .
<{fileLocation}.acl#public> <http://www.w3.org/ns/auth/acl#accessTo> <{fileLocation}> .
<{fileLocation}.acl#public> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .
<{fileLocation}.acl#owner> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/ns/auth/acl#Authorization> .
<{fileLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#agent> <{this.webId}> .
<{fileLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#accessTo> <{fileLocation}> .
<{fileLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#default> <{folderLocation}> .
<{fileLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Read> .
<{fileLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Write> .
<{fileLocation}.acl#owner> <http://www.w3.org/ns/auth/acl#mode> <http://www.w3.org/ns/auth/acl#Control> ." + accessString;
      }
      else
      {
        string aclResponseBody = await aclResponse.Content.ReadAsStringAsync();
        newAcl = aclResponseBody + accessString;
      }

      string customDPoP3 = BuildJwtForContent("PUT", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP3);
      HttpResponseMessage aclPutResponse = await client.PutAsync(aclLink, new StringContent(newAcl, Encoding.UTF8, "text/turtle"));
      aclPutResponse.EnsureSuccessStatusCode();
      // Console.WriteLine($"Access to {fileLocation} granted to {webId}");
    }

    public async Task RevokeAccessToResource(string resourceLocation, string webId)
    {
      await CreateOrRenewOIDCToken();
      HttpClient client = new HttpClient();
      string customDPoP = BuildJwtForContent("GET", resourceLocation, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      HttpResponseMessage response = await client.GetAsync(resourceLocation);
      response.EnsureSuccessStatusCode();
      string aclLink = getAclLink(response);

      string customDPoP2 = BuildJwtForContent("GET", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP2);
      client.DefaultRequestHeaders.Add("Accept", "application/n-triples");
      // client.DefaultRequestHeaders.Add("Accept", "text/turtle");
      HttpResponseMessage aclResponse = await client.GetAsync(aclLink);
      aclResponse.EnsureSuccessStatusCode();
      string aclResponseBody = await aclResponse.Content.ReadAsStringAsync();

      Graph g = new Graph();
      StringParser.Parse(g, aclResponseBody, new TurtleParser());

      // Find all subjects where the object matches the WebID
      List<INode>? subjectsToRemove = g.Triples
          .Where(t => t.Object.ToString() == webId)
          .Select(t => t.Subject)
          .Distinct()
          .ToList();

      // Remove all triples with these subjects
      foreach (var subject in subjectsToRemove)
      {
        var triplesToRemove = g.Triples.Where(t => t.Subject.Equals(subject)).ToList();
        foreach (var triple in triplesToRemove)
        {
          g.Retract(triple);
        }
      }
      // Convert the updated RDF back to a string
      NTriplesWriter writer = new NTriplesWriter();
      StringBuilder sb = new StringBuilder();
      writer.Save(g, new System.IO.StringWriter(sb));

      string customDPoP3 = BuildJwtForContent("PUT", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP3);

      HttpResponseMessage aclPutResponse = await client.PutAsync(
          aclLink,
          new StringContent(sb.ToString(), Encoding.UTF8, "application/n-triples")
      );
      aclPutResponse.EnsureSuccessStatusCode();
      // Console.WriteLine($"Access to resource {resourceLocation} revoked for {webId}");
    }

    public async Task<List<string>> ListResources(string containerUrl)
    {
      // Console.WriteLine("Listing resources in " + containerUrl);
      await CreateOrRenewOIDCToken();
      using HttpClient client = new HttpClient();
      string customDPoP = BuildJwtForContent("GET", containerUrl, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      client.DefaultRequestHeaders.Add("Accept", "application/n-triples");
      HttpResponseMessage response = await client.GetAsync(containerUrl);
      response.EnsureSuccessStatusCode();

      string turtleData = await response.Content.ReadAsStringAsync();
      Graph g = new Graph();
      StringParser.Parse(g, turtleData, new TurtleParser());
      List<string> resources = new List<string>();
      foreach (Triple t in g.Triples)
      {
        if (t.Predicate.ToString() == "http://www.w3.org/ns/ldp#contains")
        {
          resources.Add(t.Object.ToString());
        }
      }
      // get all subresources
      foreach (Triple t in g.Triples)
      {
        if (t.Object.ToString().EndsWith("/"))
        {
          List<string> subResources = await ListResources(t.Object.ToString());
          resources.AddRange(subResources);
        }
      }
      return resources;
    }

    public async Task<Dictionary<string, Dictionary<string, List<string>>>?> GetAccessTreeFromResource(string containerUrl)
    {
      Console.WriteLine("Getting accessTree from " + containerUrl);
      await CreateOrRenewOIDCToken();

      Dictionary<string, Dictionary<string, List<string>>> accessTree = new Dictionary<string, Dictionary<string, List<string>>>(); // <resource, <webId, accessModes>>
      Dictionary<string, List<string>> resourceAccess = await GetAccessesFromResource(containerUrl);
      accessTree.Add(containerUrl, resourceAccess);

      using HttpClient client = new HttpClient();
      string customDPoP = BuildJwtForContent("GET", containerUrl, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/turtle"));

      client.DefaultRequestHeaders.Add("Accept", "application/n-triples");
      HttpResponseMessage response = await client.GetAsync(containerUrl);
      response.EnsureSuccessStatusCode();

      string turtleData = await response.Content.ReadAsStringAsync();
      Graph g = new Graph();
      StringParser.Parse(g, turtleData, new TurtleParser());
      foreach (Triple t in g.Triples)
      {
        if (t.Predicate.ToString() == "http://www.w3.org/ns/ldp#contains")
        {
          if (t.Object.ToString().EndsWith("/"))
          {
            Dictionary<string, Dictionary<string, List<string>>>? subAccessTree = await GetAccessTreeFromResource(t.Object.ToString());
            // add all subresources to the accessTree
            if (subAccessTree != null)
              foreach (var subResource in subAccessTree)
              {
                accessTree.Add(subResource.Key, subResource.Value);
              }
          }
          else
          {
            Dictionary<string, List<string>> accesses = await GetAccessesFromResource(t.Object.ToString());
            accessTree.Add(t.Object.ToString(), accesses);
          }
        }
      }
      if (accessTree.Count == 0)
      {
        return null;
      }
      return accessTree;
    }

    public async Task<Dictionary<string, List<string>>> GetAccessesFromResource(string resourceLocation)
    {
      Console.WriteLine("Getting accesses from " + resourceLocation);
      await CreateOrRenewOIDCToken();
      HttpClient client = new HttpClient();
      string customDPoP = BuildJwtForContent("GET", resourceLocation, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP);
      HttpResponseMessage response = await client.GetAsync(resourceLocation);
      response.EnsureSuccessStatusCode();
      string aclLink = getAclLink(response);

      string customDPoP2 = BuildJwtForContent("GET", aclLink, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP2);
      client.DefaultRequestHeaders.Add("Accept", "application/n-triples");
      HttpResponseMessage aclResponse = await client.GetAsync(aclLink);
      Dictionary<string, List<string>> accesses = new Dictionary<string, List<string>>();
      if (aclResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
      {
        return accesses;
      }
      aclResponse.EnsureSuccessStatusCode();
      string aclResponseBody = await aclResponse.Content.ReadAsStringAsync();

      // parse all accesses and store in a dictionary
      Graph g = new Graph();
      StringParser.Parse(g, aclResponseBody, new TurtleParser());
      // List<AccessInfo> accesses = new List<AccessInfo>();
      var accessModeMapping = new Dictionary<string, string>
      {
          { "http://www.w3.org/ns/auth/acl#Read", "Read" },
          { "http://www.w3.org/ns/auth/acl#Write", "Write" },
          { "http://www.w3.org/ns/auth/acl#Control", "Control" }
      };
      foreach (Triple t in g.Triples)
      {
        if (t.Object.ToString() == "http://www.w3.org/ns/auth/acl#Authorization")
        {
          string? webId = null;
          List<string> accessModes = new List<string>();
          foreach (Triple t2 in g.GetTriplesWithSubject(t.Subject))
          {
            if (t2.Predicate.ToString() == "http://www.w3.org/ns/auth/acl#agent")
            {
              webId = t2.Object.ToString();
            }
            else if (t2.Predicate.ToString() == "http://www.w3.org/ns/auth/acl#mode")
            {
              if (accessModeMapping.TryGetValue(t2.Object.ToString(), out string? accessMode))
              {
                accessModes.Add(accessMode);
              }
            }
          }
          if (webId != null)
          {
            if (accesses.ContainsKey(webId))
            {
              accesses[webId].AddRange(accessModes);
              // deduplicate
              accesses[webId] = accesses[webId].Distinct().ToList();
            }
            else
            {
              accesses.Add(webId, accessModes);
            }
          }
        }
      }
      return accesses;
    }

    public async IAsyncEnumerable<string> SubscribeToResource(string serverLocation, string resourceLocation)
    {
      await CreateOrRenewOIDCToken();

      HttpClient client = new HttpClient();
      string webSocketUrl = $"{serverUrl}.notifications/WebSocketChannel2023/";
      string customDPoP2 = BuildJwtForContent("POST", webSocketUrl, privateKey, publicRSAKey);
      client.DefaultRequestHeaders.Add("Authorization", $"DPoP {oidcToken}");
      client.DefaultRequestHeaders.Remove("DPoP");
      client.DefaultRequestHeaders.Add("DPoP", customDPoP2);

      string body = "{\n  \"@context\": [ \"https://www.w3.org/ns/solid/notification/v1\" ],\n  \"type\": \"http://www.w3.org/ns/solid/notifications#WebSocketChannel2023\",\n  \"topic\": \"" + resourceLocation + "\"\n}";

      HttpResponseMessage subscriptionPostResponse = await client.PostAsync(webSocketUrl, new StringContent(body, Encoding.UTF8, "application/ld+json"));
      string responseBody = await subscriptionPostResponse.Content.ReadAsStringAsync();
      // {"name":"ForbiddenHttpError","message":"","statusCode":403,"errorCode":"H403","details":{}}
      // if forbidden, throw error
      if (subscriptionPostResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
      {
        throw new Exception("webId does not have read access to this resource or it does not exist");
      }

      subscriptionPostResponse.EnsureSuccessStatusCode();
      Console.WriteLine($"Subscribed to resource {resourceLocation}");

      string receiveFrom = responseBody.Split("\"receiveFrom\":\"")[1].Split("\"")[0];

      using (var ws = new ClientWebSocket())
      {
        await ws.ConnectAsync(new Uri(receiveFrom), CancellationToken.None);
        Console.WriteLine("Connected to WebSocket server at " + receiveFrom);

        // await ReceiveMessages(ws);
        await foreach (var message in ReceiveMessages(ws))
        {
          yield return message; // Yield each message as it's received
        }
      }

    }
    //   else
    //   {
    //     throw new Exception("No subscription service found in response.");
    //   }
    // }

    static async IAsyncEnumerable<string> ReceiveMessages(ClientWebSocket ws)
    {
      var buffer = new byte[1024 * 4];  // 4 KB buffer for incoming messages

      while (ws.State == WebSocketState.Open)
      {
        WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Text)
        {
          string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
          yield return message; // Yield each message as it's received
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
          Console.WriteLine("WebSocket closed by the server.");
          await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
      }
    }

    static string getAclLink(HttpResponseMessage response)
    {
      IEnumerable<string> linkHeaders = response.Headers.GetValues("Link");
      string? aclInfo = linkHeaders
          .SelectMany(header => header.Split(','))
          .Select(link => link.Trim())
          .FirstOrDefault(link => link.Contains("rel=\"acl\""));
      if (aclInfo != null)
      {
        int start = aclInfo.IndexOf('<') + 1;
        int end = aclInfo.IndexOf('>');
        string aclLink = aclInfo.Substring(start, end - start);
        return aclLink;
      }
      else
      {
        throw new Exception("No ACL url found in response.");
      }
    }
  }
}
