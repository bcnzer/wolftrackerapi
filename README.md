# Wolf Tracker API

Please see the blog post on https://liftcodeplay.com(https://liftcodeplay.com)

In order to debug this locally you will need to add a local.settings.json file. 

```json
{
    "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<conneciton string to blob storage>",
    "AzureWebJobsDashboard": "",
    "WolfImageContainer": "wolfpictures-originals",
    "WolfImageQueue": "queue-wolfimages",
    "Auth0Audience": "<auth0 key>",
    "Auth0Issuer": "https://wolftracker.au.auth0.com/",
    "CognitiveServicesVisionUrl": "https://southcentralus.api.cognitive.microsoft.com/vision/v1.0/",
    "CognitiveServicesVisionKey": "<cognitive services key>"
  }
}
```