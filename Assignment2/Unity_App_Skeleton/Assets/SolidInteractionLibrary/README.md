This project folder contains the C# library at /SolidInteractionLibrary to interact with solid Pods from a [Community Solid Server](https://communitysolidserver.github.io/CommunitySolidServer/latest/). The folder SolidInteractionConsoleApp contains an example program to showcase how to use the library.

The library allows users to authenticate and interact with their private pods without user input. It does not follow the usual flow of OIDC authentication, which includes redirecting to the identity provider in the browser. Instead it uses the client credentials flow described [here](https://communitysolidserver.github.io/CommunitySolidServer/latest/usage/client-credentials/) for JavaScript.

## Getting Started

### NuGet Packages
Install the following packages in your Unity project using [NuGet for Unity](https://github.com/GlitchEnzo/NuGetForUnity):
- dotNetRdf
- Microsoft.IdentityModel.Tokens
- System.IdentityModel.Tokens.Jwt
- System.Security.Permissions

### Solid Server

start a Community Solid Server, for example locally on port 3000 with

```bash
$ npx @solid/community-server
```

We create an account

```csharp
AuthenticationHeaderValue authHeader = await SolidClient.CreateAccount(serverUrl, email, password);
```

or login to an existing account

```csharp
AuthenticationHeaderValue authHeader = await AuthenticatedPodClient.LoginAsync(serverUrl, email, password);
```

Then we create a pod if we didn't already create one.

```csharp
await SolidClient.CreatePod(serverUrl, podName, authHeader);
```

## Interaction with the Pod

First we make an authenticated client. It creates a credentials token connected to the given webId. Every time an authenticated request is made it checks if it has a valid OIDC token required for authenticated requests. If there is still none, or the token is about to expire, it renews the OIDC token.

```csharp
AuthenticatedPodClient authenticatedClient = await AuthenticatedPodClient.BuildAsync(serverUrl, webId, email, password);
```

Listing of pod urls connected to the given webId as the owner and saving and fetching a private file on any of these pods:

```csharp
List<string> podUrls = await authenticatedClient.GetPods();
string location = await authenticatedClient.SaveFileAsync($"{podUrls[0]}folder/hello.txt", contentType, fileContent);
string fileContentResponse = await authenticatedClient.GetFileAsync(location);
Console.WriteLine("file content: " + fileContentResponse);
```

Deleting resources:

```csharp
await authenticatedClient.DeleteFileAsync(location);
```

Granting "Read", "Write", or "ReadWrite" access to another WebId to a pod, subfolder or file:

```csharp
await authenticatedClient.GrantAccessToPod("http://localhost:3000/my-pod/", "http://localhost:3000/podFromAnotherAccount/profile/card#me", "ReadWrite");
await authenticatedClient.GrantAccessToSubFolder($"http://localhost:3000/my-pod/folder/", "http://localhost:3000/podFromAnotherAccount/profile/card#me", "ReadWrite");
await authenticatedClient.GrantAccessToFile($"http://localhost:3000/my-pod/folder/hello.txt", "http://localhost:3000/podFromAnotherAccount/profile/card#me", "ReadWrite");
```

Revoking access given to a WebId for any resource:

```csharp
await authenticatedClient.RevokeAccessToResource($"http://localhost:3000/my-pod/", "http://localhost:3000/podFromAnotherAccount/profile/card#me");
```

Checking which resources are in a given container:

```csharp
List<string> resources = await authenticatedClient.ListResources("http://localhost:3000/my-pod/");
```

Checking all the accesses given to a resource and all sub-resources:

```csharp
Dictionary<string, Dictionary<string, List<string>>> accessTree = await authenticatedClient.GetAccessTreeFromResource($"http://localhost:3000/my-pod/");
```

Getting all accesses given to a single resource:

```csharp
Dictionary<string, List<string>>? accesses = await authenticatedClient.GetAccessesFromResource($"http://localhost:3000/my-pod/folder/hello.txt");
```

Subscribing to a resource (file or folder) to receive e.g. create or update notifications:

```csharp
await foreach (var message in authenticatedClient.SubscribeToResource("http://localhost:3000/", "http://localhost:3000/folder/hello.txt"))
{
  Console.WriteLine($"Received message: {message}");
}
```

## Adding the library to a Unity project

First, add [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) to your Unity proejct. Add packages System.Text.Json, System.IdentityModel.Tokens.Jwt, Microsoft.IdentityModel, Microsoft.IdentityModel.Tokens with NuGet.
Drag the SolidInteractionLibrary folder into the Assets folder in Unity. Make sure to delete any possible obj folder in SolidInteractionLibrary.

## Testing the library with Unity

Start a community solid server on localhost 3000 by running community-solid-server in the command line. Import the[TestScriptForUnity.cs](TestScriptForUnity.cs) script to Unity and add it to e.g. a Button. Finally, run the Unity project and click the button and check the Console.
