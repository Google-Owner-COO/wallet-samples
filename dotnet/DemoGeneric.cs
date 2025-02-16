﻿/*
 * Copyright 2022 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// [START setup]
// [START imports]
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
// [END imports]


/*
* keyFilePath - Path to service account key file from Google Cloud Console
*             - Environment variable: GOOGLE_APPLICATION_CREDENTIALS
*/
string keyFilePath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") ?? "/path/to/key.json";

/*
* issuerId - The issuer ID being used in this request
*          - Environment variable: WALLET_ISSUER_ID
*/
string issuerId = Environment.GetEnvironmentVariable("WALLET_ISSUER_ID") ?? "issuer-id";

/*
* classId - Developer-defined ID for the wallet class
*         - Environment variable: WALLET_CLASS_ID
*/
string classId = Environment.GetEnvironmentVariable("WALLET_CLASS_ID") ?? "test-generic-class-id";

/*
* userId - Developer-defined ID for the user, such as an email address
*        - Environment variable: WALLET_USER_ID
*/
string userId = Environment.GetEnvironmentVariable("WALLET_USER_ID") ?? "user-id";

/*
* objectId - ID for the wallet object
*          - Format: `issuerId.identifier`
*          - Should only include alphanumeric characters, '.', '_', or '-'
*          - `identifier` is developer-defined and unique to the user
*/
string objectId = $"{issuerId}.{new Regex(@"[^\w.-]", RegexOptions.Compiled).Replace(userId, "_")}-{classId}";
// [END setup]

///////////////////////////////////////////////////////////////////////////////
// Create authenticated HTTP client, using service account file.
///////////////////////////////////////////////////////////////////////////////

// [START auth]
ServiceAccountCredential credentials = (ServiceAccountCredential)GoogleCredential.FromFile(keyFilePath)
    .CreateScoped(new[] { "https://www.googleapis.com/auth/wallet_object.issuer" })
    .UnderlyingCredential;

HttpClient httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
    "Bearer",
    await credentials.GetAccessTokenForRequestAsync()
);
// [END auth]

///////////////////////////////////////////////////////////////////////////////
// Create a class via the API (this can also be done in the business console).
///////////////////////////////////////////////////////////////////////////////

// [START class]
string classUrl = "https://walletobjects.googleapis.com/walletobjects/v1/genericClass/";
var classPayload = new
{
  id = $"{issuerId}.{classId}",
  issuerName = "test issuer name"
};

HttpRequestMessage classRequest = new HttpRequestMessage(HttpMethod.Post, classUrl);
classRequest.Content = new StringContent(JsonConvert.SerializeObject(classPayload));
HttpResponseMessage classResponse = httpClient.Send(classRequest);

string classContent = await classResponse.Content.ReadAsStringAsync();

Console.WriteLine($"class POST response: {classContent}");
// [END class]

///////////////////////////////////////////////////////////////////////////////
// Create an object via the API.
///////////////////////////////////////////////////////////////////////////////

// [START object]
string objectUrl = "https://walletobjects.googleapis.com/walletobjects/v1/genericObject/";
var objectPayload = new
{
  id = objectId,
  classId = $"{issuerId}.{classId}",
  heroImage = new
  {
    sourceUri = new
    {
      uri = "https://farm4.staticflickr.com/3723/11177041115_6e6a3b6f49_o.jpg",
      description = "Test heroImage description"
    }
  },
  textModulesData = new object[]
  {
    new
    {
      header = "Test text module header",
      body = "Test text module body"
    }
  },
  linksModuleData = new
  {
    uris = new object[]
    {
      new
      {
        kind = "walletobjects#uri",
        uri = "http://maps.google.com/",
        description = "Test link module uri description"
      },
      new
      {
        kind = "walletobjects#uri",
        uri = "tel:6505555555",
        description = "Test link module tel description"
      }
    }
  },
  imageModulesData = new object[]
  {
    new
    {
      mainImage = new
      {
        kind = "walletobjects#image",
        sourceUri = new
        {
          kind = "walletobjects#uri",
          uri = "http://farm4.staticflickr.com/3738/12440799783_3dc3c20606_b.jpg",
          description = "Test image module description"
        }
      }
    }
  },
  barcode = new
  {
    kind = "walletobjects#barcode",
    type = "qrCode",
    value = "Test QR Code"
  },
  genericType = "GENERIC_TYPE_UNSPECIFIED",
  hexBackgroundColor = "#4285f4",
  logo = new
  {
    sourceUri = new
    {
      uri = "https://storage.googleapis.com/wallet-lab-tools-codelab-artifacts-public/pass_google_logo.jpg"
    }
  },
  cardTitle = new
  {
    defaultValue = new
    {
      language = "en-US",
      value = "Testing Generic Title"
    }
  },
  header = new
  {
    defaultValue = new
    {
      language = "en-US",
      value = "Testing Generic Header"
    }
  },
  subheader = new
  {
    defaultValue = new
    {
      language = "en",
      value = "Testing Generic Sub Header"
    }
  }
};

HttpRequestMessage objectRequest = new HttpRequestMessage(HttpMethod.Get, $"{objectUrl}{objectId}");
HttpResponseMessage objectResponse = httpClient.Send(objectRequest);
if (objectResponse.StatusCode == HttpStatusCode.NotFound)
{
  // Object does not yet exist
  // Send POST request to create it
  objectRequest = new HttpRequestMessage(HttpMethod.Post, objectUrl);
  objectRequest.Content = new StringContent(JsonConvert.SerializeObject(objectPayload));
  objectResponse = httpClient.Send(objectRequest);
}

string objectContent = await objectResponse.Content.ReadAsStringAsync();
Console.WriteLine($"object GET or POST response: {objectContent}");
// [END object]

///////////////////////////////////////////////////////////////////////////////
// Create a JWT for the object, and encode it to create a "Save" URL.
///////////////////////////////////////////////////////////////////////////////

// [START jwt]
JwtPayload claims = new JwtPayload();
claims.Add("iss", credentials.Id);
claims.Add("aud", "google");
claims.Add("origins", new string[] { "www.example.com" });
claims.Add("typ", "savetowallet");
claims.Add("payload", new
{
  genericObjects = new object[]
  {
    new
    {
      id = objectId
    }
  }
});

RsaSecurityKey key = new RsaSecurityKey(credentials.Key);
SigningCredentials signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
JwtSecurityToken jwt = new JwtSecurityToken(new JwtHeader(signingCredentials), claims);
string token = new JwtSecurityTokenHandler().WriteToken(jwt);
string saveUrl = $"https://pay.google.com/gp/v/save/{token}";

Console.WriteLine(saveUrl);
// [END jwt]

///////////////////////////////////////////////////////////////////////////////
// Create a new Google Wallet issuer account
///////////////////////////////////////////////////////////////////////////////

// [START createIssuer]
// New issuer name
string issuerName = "name";

// New issuer email address
string issuerEmail = "email-address";

// Issuer API endpoint
string issuerUrl = "https://walletobjects.googleapis.com/walletobjects/v1/issuer";

// New issuer information
var issuerPayload = new
{
  name = issuerName,
  contactInfo = new
  {
    email = issuerEmail
  }
};

HttpRequestMessage issuerRequest = new HttpRequestMessage(HttpMethod.Post, issuerUrl);
issuerRequest.Content = new StringContent(JsonConvert.SerializeObject(issuerPayload));
HttpResponseMessage issuerResponse = httpClient.Send(issuerRequest);

Console.WriteLine($"issuer POST response: {await issuerResponse.Content.ReadAsStringAsync()}");
// [END createIssuer]

///////////////////////////////////////////////////////////////////////////////
// Update permissions for an existing Google Wallet issuer account
///////////////////////////////////////////////////////////////////////////////

// [START updatePermissions]
// Permissions API endpoint
string permissionsUrl = $"https://walletobjects.googleapis.com/walletobjects/v1/permissions/{issuerId}";

// New issuer permissions information
var permissionsPayload = new
{
  issuerId = issuerId,
  permissions = new object[]
  {
    // Copy as needed for each email address that will need access
    new
    {
      emailAddress = "email-address",
      role = "READER | WRITER | OWNER"
    }
  }
};

HttpRequestMessage permissionsRequest = new HttpRequestMessage(HttpMethod.Put, permissionsUrl);
permissionsRequest.Content = new StringContent(JsonConvert.SerializeObject(permissionsPayload));
HttpResponseMessage permissionsResponse = httpClient.Send(permissionsRequest);

Console.WriteLine($"permissions PUT response: {await permissionsResponse.Content.ReadAsStringAsync()}");
// [END updatePermissions]

///////////////////////////////////////////////////////////////////////////////
// Batch create Google Wallet objects from an existing class
///////////////////////////////////////////////////////////////////////////////

// [START batch]
// The request body will be a multiline string
// See below for more information
// https://cloud.google.com/compute/docs/api/how-tos/batch//example
string data = "";

// Example: Generate three new pass objects
for (int i = 0; i < 3; i++)
{
  // Generate a random user ID
  userId = Regex.Replace(Guid.NewGuid().ToString(), "[^\\w.-]", "_");

  // Generate an object ID with the user ID
  objectId = $"{issuerId}.{new Regex(@"[^\w.-]", RegexOptions.Compiled).Replace(userId, "_")}-{classId}";
  var batchObject = new
    {
      id = objectId,
      classId = $"{issuerId}.{classId}",
      heroImage = new
      {
        sourceUri = new
        {
          uri = "https://farm4.staticflickr.com/3723/11177041115_6e6a3b6f49_o.jpg",
          description = "Test heroImage description"
        }
      },
      textModulesData = new object[]
      {
        new
        {
          header = "Test text module header",
          body = "Test text module body"
        }
      },
      linksModuleData = new
      {
        uris = new object[]
        {
          new
          {
            kind = "walletobjects#uri",
            uri = "http://maps.google.com/",
            description = "Test link module uri description"
          },
          new
          {
            kind = "walletobjects#uri",
            uri = "tel:6505555555",
            description = "Test link module tel description"
          }
        }
      },
      imageModulesData = new object[]
      {
        new
        {
          mainImage = new
          {
            kind = "walletobjects#image",
            sourceUri = new
            {
              kind = "walletobjects#uri",
              uri = "http://farm4.staticflickr.com/3738/12440799783_3dc3c20606_b.jpg",
              description = "Test image module description"
            }
          }
        }
      },
      barcode = new
      {
        kind = "walletobjects#barcode",
        type = "qrCode",
        value = "Test QR Code"
      },
      genericType = "GENERIC_TYPE_UNSPECIFIED",
      hexBackgroundColor = "#4285f4",
      logo = new
      {
        sourceUri = new
        {
          uri = "https://storage.googleapis.com/wallet-lab-tools-codelab-artifacts-public/pass_google_logo.jpg"
        }
      },
      cardTitle = new
      {
        defaultValue = new
        {
          language = "en-US",
          value = "Testing Generic Title"
        }
      },
      header = new
      {
        defaultValue = new
        {
          language = "en-US",
          value = "Testing Generic Header"
        }
      },
      subheader = new
      {
        defaultValue = new
        {
          language = "en",
          value = "Testing Generic Sub Header"
        }
      }
    };

  data += "--batch_createobjectbatch\n";
  data += "Content-Type: application/json\n\n";
  data += "POST /walletobjects/v1/genericObject/\n\n";

  data += JsonConvert.SerializeObject(batchObject) + "\n\n";
}
data += "--batch_createobjectbatch--";

// Invoke the batch API calls
HttpRequestMessage objectRequest = new HttpRequestMessage(
    HttpMethod.Post,
    "https://walletobjects.googleapis.com/batch");

objectRequest.Content = new StringContent(data);
objectRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("multipart/mixed");
objectRequest.Content.Headers.ContentType.Parameters.Add(
    // `boundary` is the delimiter between API calls in the batch request
    new NameValueHeaderValue("boundary", "batch_createobjectbatch"));

HttpResponseMessage objectResponse = httpClient.Send(objectRequest);

string objectContent = await objectResponse.Content.ReadAsStringAsync();

Console.WriteLine($"object GET or POST response: {objectContent}");
// [END batch]
