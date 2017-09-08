using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace FaceApi_PoC
{
    class Program
    {

        // Replace the subscriptionKey string value with your valid subscription key.
        const string _subscriptionKey = "*** Your Subscription Key ****";

        // Replace or verify the region.
        //
        // You must use the same region in your REST API call as you used to obtain your subscription keys.
        // For example, if you obtained your subscription keys from the westus region, replace 
        // "westcentralus" in the URI below with "westus".
        //
        // NOTE: Free trial subscription keys are generated in the westcentralus region, so if you are using
        // a free trial subscription key, you should not need to change this region.
        const string uriBase = "https://westcentralus.api.cognitive.microsoft.com/face/v1.0/detect";

        private static FaceServiceClient _faceServiceClient;

        private static Guid _dadId;
        private static Guid _momId;
        private static Guid _daughterId;

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }
        static async Task MainAsync(string[] args)
        {
            _faceServiceClient = new FaceServiceClient(_subscriptionKey, "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");

            // Create a person group for employees
            string employeesGroupId = "employees";

            try
            {
                await SetupFaceVerification(employeesGroupId);

                await DetectAndVerifyEmployee(employeesGroupId, Guid.Parse("07fcac24-fb03-4fe5-826e-34fc52685cd8"));

                //DeleteEverything(employeesGroupId);
            }
            catch (Exception itsBad)
            {
                Console.WriteLine("Something bad happened");
                Console.WriteLine(itsBad.Message);
            }

            Console.WriteLine("That's it, we're done !");
            Console.ReadLine();
        }

        static async Task DetectAndVerifyEmployee(string personGroupId, Guid personId)
        {
            string testImageFile = @"C:\Dev\GitHub\Cognitive-Face-Windows\Data\PersonGroup\Family1-Mom\Family1-Mom1.jpg";

            // This is the person we hope to find !
            var person = await _faceServiceClient.GetPersonAsync(personGroupId, personId);
            Console.WriteLine("...Trying to confirm that the photo submitted matches with {0}", person.Name);

            using (Stream s = File.OpenRead(testImageFile))
            {
                var faces = await _faceServiceClient.DetectAsync(s);
                var faceIds = faces.Select(face => face.FaceId).ToArray();

                // Here, I only care about the first face in the list of faces detected assuming there is only one.
                var verifyResult = await _faceServiceClient.VerifyAsync(faceIds[0], personGroupId, personId);

                Console.WriteLine("We believe the picture correspond to: {0} with confidence: {1}", person.Name, verifyResult.Confidence);
            }
        }

        static async Task SetupFaceVerification(string personGroupId)
        {
            try
            {
                bool groupExists = false;
                var personGroups = await _faceServiceClient.ListPersonGroupsAsync();
                foreach (PersonGroup personGroup in personGroups)
                {
                    if (personGroup.PersonGroupId == personGroupId)
                    {
                        groupExists = true;
                    }
                }
                if (!groupExists)
                {
                    await _faceServiceClient.CreatePersonGroupAsync(personGroupId, "All Employees");

                    _dadId = await CreatePerson(personGroupId, "John");
                    _momId = await CreatePerson(personGroupId, "Samantha");
                    _daughterId = await CreatePerson(personGroupId, "Natasha");

                    string imageDir = @"C:\Dev\GitHub\Cognitive-Face-Windows\Data\PersonGroup\Family1-Dad\";
                    await DetectFacesAndAddtoPerson(imageDir, personGroupId, _dadId);

                    imageDir = @"C:\Dev\GitHub\Cognitive-Face-Windows\Data\PersonGroup\Family1-Mom\";
                    await DetectFacesAndAddtoPerson(imageDir, personGroupId, _momId);

                    imageDir = @"C:\Dev\GitHub\Cognitive-Face-Windows\Data\PersonGroup\Family1-Daughter\";
                    await DetectFacesAndAddtoPerson(imageDir, personGroupId, _daughterId);

                    await _faceServiceClient.TrainPersonGroupAsync(personGroupId);
                    while (true)
                    {
                        TrainingStatus trainingStatus = null;
                        try
                        {
                            trainingStatus = await _faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);
                            Console.WriteLine("...Train status: CreatedDateTime: " + trainingStatus.CreatedDateTime + ", Status:" + trainingStatus.Status);

                            if (trainingStatus.Status != Status.Running)
                            {
                                break;
                            }

                            await Task.Delay(1000);

                        }
                        catch (FaceAPIException ex)
                        {
                            Console.WriteLine(ex.ErrorCode);
                            Console.WriteLine(ex.ErrorMessage);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("That PersonGroup already exists, skipping setup...");
                }
            }
            catch (FaceAPIException oops)
            {
                Console.WriteLine(oops.ErrorCode);
                Console.WriteLine(oops.ErrorMessage);

                // Since it's just a poc...
                throw;
            }
        }

        static async Task DetectFacesAndAddtoPerson(string imgDir, string personGroupId, Guid personId)
        {
            foreach (string imagePath in Directory.GetFiles(imgDir, "*.jpg"))
            {
                using (Stream s = File.OpenRead(imagePath))
                {
                    // Detect faces in the image and add to person
                    await _faceServiceClient.AddPersonFaceAsync(personGroupId, personId, s);
                }
            }
        }

        static async Task<Guid> CreatePerson(string personGroupId, string personName)
        {
            // Create the person
            CreatePersonResult dad = await _faceServiceClient.CreatePersonAsync(personGroupId, personName);

            return dad.PersonId;
        }

        static async void DeleteEverything(string personGroupId)
        {
            await _faceServiceClient.DeletePersonGroupAsync(personGroupId);
        }

        static void CreatePersonGroupTest()
        {
            Console.WriteLine("Enter an ID for the group you wish to create:");
            Console.WriteLine("(Use numbers, lower case letters, '-' and '_'. The maximum length of the personGroupId is 64.)");

            string personGroupId = Console.ReadLine();

            MakeCreateGroupRequest(personGroupId);

            Console.WriteLine("\n\n\nWait for the result below, then hit ENTER to exit...\n\n\n");
            Console.ReadLine();
        }

        /// <summary>
        /// Gets the analysis of the specified image file by using the Computer Vision REST API.
        /// </summary>
        /// <param name="imageFilePath">The image file.</param>
        static async void MakeAnalysisRequest(string imageFilePath)
        {
            HttpClient client = new HttpClient();

            // Request headers.
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);

            // Request parameters. A third optional parameter is "details".
            string requestParameters = "returnFaceId=true&returnFaceLandmarks=false&returnFaceAttributes=age,gender,headPose,smile,facialHair,glasses,emotion,hair,makeup,occlusion,accessories,blur,exposure,noise";

            // Assemble the URI for the REST API Call.
            string uri = uriBase + "?" + requestParameters;

            HttpResponseMessage response;

            // Request body. Posts a locally stored JPEG image.
            byte[] byteData = GetImageAsByteArray(imageFilePath);

            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                // This example uses content type "application/octet-stream".
                // The other content types you can use are "application/json" and "multipart/form-data".
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                // Execute the REST API call.
                response = await client.PostAsync(uri, content);

                // Get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();

                // Display the JSON response.
                Console.WriteLine("\nResponse:\n");
                Console.WriteLine(JsonPrettyPrint(contentString));
            }
        }

        static async void MakeCreateGroupRequest(string personGroupId)
        {
            var client = new HttpClient();

            // Request headers - replace this example key with your valid key.
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);

            // Request URI string.
            // NOTE: You must use the same region in your REST call as you used to obtain your subscription keys.
            //   For example, if you obtained your subscription keys from westus, replace "westcentralus" in the 
            //   URI below with "westus".
            string uri = "https://westcentralus.api.cognitive.microsoft.com/face/v1.0/persongroups/" + personGroupId;

            // Here "name" is for display and doesn't have to be unique. Also, "userData" is optional.
            string json = "{\"name\":\"My Group\", \"userData\":\"Some data related to my group.\"}";
            HttpContent content = new StringContent(json);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response = await client.PutAsync(uri, content);

            // If the group was created successfully, you'll see "OK".
            // Otherwise, if a group with the same personGroupId has been created before, you'll see "Conflict".
            Console.WriteLine("Response status: " + response.StatusCode);
        }

        /// <summary>
        /// Returns the contents of the specified file as a byte array.
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>The byte array of the image data.</returns>
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            return binaryReader.ReadBytes((int)fileStream.Length);
        }


        /// <summary>
        /// Formats the given JSON string by adding line breaks and indents.
        /// </summary>
        /// <param name="json">The raw JSON string to format.</param>
        /// <returns>The formatted JSON string.</returns>
        static string JsonPrettyPrint(string json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            json = json.Replace(Environment.NewLine, "").Replace("\t", "");

            StringBuilder sb = new StringBuilder();
            bool quote = false;
            bool ignore = false;
            int offset = 0;
            int indentLength = 3;

            foreach (char ch in json)
            {
                switch (ch)
                {
                    case '"':
                        if (!ignore) quote = !quote;
                        break;
                    case '\'':
                        if (quote) ignore = !ignore;
                        break;
                }

                if (quote)
                    sb.Append(ch);
                else
                {
                    switch (ch)
                    {
                        case '{':
                        case '[':
                            sb.Append(ch);
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', ++offset * indentLength));
                            break;
                        case '}':
                        case ']':
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', --offset * indentLength));
                            sb.Append(ch);
                            break;
                        case ',':
                            sb.Append(ch);
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', offset * indentLength));
                            break;
                        case ':':
                            sb.Append(ch);
                            sb.Append(' ');
                            break;
                        default:
                            if (ch != ' ') sb.Append(ch);
                            break;
                    }
                }
            }

            return sb.ToString().Trim();
        }
    }
}
