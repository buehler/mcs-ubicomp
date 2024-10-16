import { createDpopHeader, generateDpopKeyPair } from '@inrupt/solid-client-authn-core';
import fetch from 'cross-fetch';


// First we request the account API controls to find out where we can log in
const authenticate = async (): Promise<any> => {

  const indexResponse = await fetch('https://wiser-solid-xi.interactions.ics.unisg.ch/.account/');
  const { controls } = await indexResponse.json();

  //console.log ("**** Index response: ", indexResponse.json());
  console.log ("**** Controls: ", controls);

  // And then we log in to the account API
  const response = await fetch(controls.password.login, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ email: 'example@unisg.ch', password: 'pass123' }),
  });
  // This authorization value will be used to authenticate in the next step
  const { authorization } = await response.json();
  return authorization;
}

const getAuthorizationToken = async (authorization: any): Promise<any> => {
  
  console.log("This is the authorization that I get: ", authorization)
  // Now that we are logged in, we need to request the updated controls from the server.
  // These will now have more values than in the previous example.
  const indexResponse = await fetch('https://wiser-solid-xi.interactions.ics.unisg.ch/.account/', {
    headers: { authorization: `CSS-Account-Token ${authorization}` }
  });
  const{controls} = await indexResponse.json();

  // Here we request the server to generate a token on our account
  const response= await fetch(controls.account.clientCredentials, {
    method: 'POST',
    headers: { authorization: `CSS-Account-Token ${authorization}`, 'content-type': 'application/json' },
    // The name field will be used when generating the ID of your token.
    // The WebID field determines which WebID you will identify as when using the token.
    // Only WebIDs linked to your account can be used.
    body: JSON.stringify({ name: 'my-token', webId: 'yourWebID' }),
});

  // These are the identifier and secret of your token.
  // Store the secret somewhere safe as there is no way to request it again from the server!
  // The `resource` value can be used to delete the token at a later point in time.
  
  const { id, secret, resource } = await response.json();
  return [id, secret, resource]
}

const getTokenUsage = async(id: any, secret: any): Promise<any> => {
  // A key pair is needed for encryption.
  // This function from `solid-client-authn` generates such a pair for you.
  const dpopKey = await generateDpopKeyPair();

  // These are the ID and secret generated in the previous step.
  // Both the ID and the secret need to be form-encoded.
  const authString = `${encodeURIComponent(id)}:${encodeURIComponent(secret)}`;
  // This URL can be found by looking at the "token_endpoint" field at
  const tokenUrl = 'https://wiser-solid-xi.interactions.ics.unisg.ch/.oidc/token';
  const response = await fetch(tokenUrl, {
    method: 'POST',
    headers: {
      // The header needs to be in base64 encoding.
      authorization: `Basic ${Buffer.from(authString).toString('base64')}`,
      'content-type': 'application/x-www-form-urlencoded',
      dpop: await createDpopHeader(tokenUrl, 'POST', dpopKey),
    },
    body: 'grant_type=client_credentials&scope=webid',
  });

  // This is the Access token that will be used to do an authenticated request to the server.
  // The JSON also contains an "expires_in" field in seconds,
  // which you can use to know when you need request a new Access token.
  //const  access_token = await response.json();
  const { access_token: accessToken } = await response.json();

 return [accessToken, dpopKey];

}

const runAsyncFunctions = async () => {

    const idInfo = await authenticate();
    const tokenAuth = await getAuthorizationToken(idInfo);
    
    const [token,dpopKey] = await getTokenUsage(tokenAuth[0],tokenAuth[1]);
    
    console.log("Token usage ", token);
    
    console.log("dpopKey here: ", dpopKey);


  }
 
  runAsyncFunctions()
