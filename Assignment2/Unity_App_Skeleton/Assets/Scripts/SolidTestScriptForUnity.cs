using SolidInteractionLibrary;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Net.Http.Headers;
using System.Diagnostics;

public class SolidTestSolidLibrary : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void StartTest()
    {
        Task.Run(async () =>
        {
            await TestPod();
        }).Wait();
    }

    public async Task TestPod()
    {
        string serverUrl = "http://localhost:3000/";
        // string serverUrl = "https://wiser-solid-xi.interactions.ics.unisg.ch/";
        string email = "test@example.com";
        string password = "password";
        string podName = "my-pod";
        // string webId = $"{serverUrl}{podName}/profile/card#me";

        try
        {
            AuthenticationHeaderValue _authHeader = await SolidClient.CreateAccount(serverUrl, email, password);
        }
        // fails if account already exists
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.Message);
        }

        AuthenticationHeaderValue authHeader = await AuthenticatedPodClient.LoginAsync(serverUrl, email, password);
        try
        {
            await SolidClient.CreatePod(serverUrl, podName, authHeader);
            // following pod is not connected to the webId
            await SolidClient.CreatePod(serverUrl, "test", authHeader);
        }
        // fails if pods already exists
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.Message);
        }

        List<string> webIds = await SolidClient.GetWebIds(serverUrl, authHeader);
        if (webIds.Count == 0)
        {
            UnityEngine.Debug.Log("Something went wrong, no webIds found");
            return;
        }
        UnityEngine.Debug.Log("WebIds connected to this account:");
        // print length of webids
        foreach (string connectedWebId in webIds)
        {
            UnityEngine.Debug.Log(connectedWebId);
        }
        UnityEngine.Debug.Log("Authenticating with webid " + webIds[0]);
        AuthenticatedPodClient authenticatedClient = await AuthenticatedPodClient.BuildAsync(serverUrl, webIds[0], email, password);
        List<string> podUrls = await authenticatedClient.GetPods();
        UnityEngine.Debug.Log("Pod URLs connected to webId:");
        foreach (string podUrl in podUrls)
        {
            UnityEngine.Debug.Log(podUrl);
        }

        string fileContent = "Hello, World!";
        string contentType = "text/plain";
        string location = await authenticatedClient.SaveFileAsync($"{podUrls[0]}folder/hello.txt", contentType, fileContent);
        string fileContentResponse = await authenticatedClient.GetFileAsync(location);
        UnityEngine.Debug.Log("file content: " + fileContentResponse);

        string location2 = await authenticatedClient.SaveFileAsync($"{podUrls[0]}folder/hello.txt", contentType, "hello again");
        string fileContentResponse2 = await authenticatedClient.GetFileAsync(location2);
        UnityEngine.Debug.Log("file content: " + fileContentResponse2);
    }
}
