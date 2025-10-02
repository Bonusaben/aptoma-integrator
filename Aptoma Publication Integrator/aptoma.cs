using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Aptoma_Publication_Integrator
{
    static class Aptoma
    {
        static string APIKEY;
        static string token = "";
        static DateTime tokenExpiration;

        static string AUTHBODY, AUTHURL, EDITIONURL, PAGEURL, IMGURL;

        public static void Init()
        {
            Program.Log("Initializing aptoma class");
            
            LoadSettings();
        }

        static void LoadSettings()
        {
            Program.Log("Loading Aptoma settings");

            APIKEY = ConfigurationManager.AppSettings.Get("APIKEY");

            string _authUser = ConfigurationManager.AppSettings.Get("AUTHUSER");
            string _authPass = ConfigurationManager.AppSettings.Get("AUTHPASS");
            AUTHBODY = "{\"user\":\""+_authUser+"\",\"password\":\""+_authPass+"\"}";
            AUTHURL = ConfigurationManager.AppSettings.Get("AUTHURL");

            EDITIONURL = ConfigurationManager.AppSettings.Get("EDITIONURL");
            PAGEURL = ConfigurationManager.AppSettings.Get("PAGEURL");
            IMGURL = ConfigurationManager.AppSettings.Get("IMGURL");

            Program.Log("Settings loaded");
        }
        
        static bool GetToken()
        {
            Program.Log("Getting token");
            if(token.Length == 0 || tokenExpiration < DateTime.Now)
            {
                // Get new token
                KeyValuePair<string, string> kp = new KeyValuePair<string, string>("application/json", AUTHBODY);
                string[] r = AptomaPost(AUTHURL, null, kp);

                JObject j = new JObject();

                if (r[0].Equals("OK")) // Status code "OK"
                {
                    j = JObject.Parse(r[1]);

                    token = (string)j.SelectToken("jwt");
                    tokenExpiration = DateTime.Parse((string)j.SelectToken("tokenExpires"));

                    Program.Log("New token: \n" + token);
                    Program.Log("Expires: " + tokenExpiration.ToString());
                }
                else
                {
                    Program.Log("Failed to get new token. Response: " + r[0]);

                    return false;
                }
            } // Else token still active

            return true;
        }

        /// <summary>
        /// Sends a Rest call to the Aptoma server
        /// </summary>
        /// <param name="url"></param>The url of tha api call
        /// <param name="body"></param>The body of the api call
        /// <returns>a string with the rest response</returns>
        /*
        public static string[] AptomaPost(string url, Dictionary<string, string> headers, KeyValuePair<string, string> requestBodyParameter)
        {
            Program.Log("Posting to Aptoma url: "+url);

            string[] result = new string[2];
            result[0] = "";
            result[1] = "";

            var client = new RestClient(url);
            var request = new RestRequest("Method.POST");

            if (headers != null)
            {
                Program.Log("Headers:");
                foreach (KeyValuePair<string, string> header in headers)
                {
                    Program.Log(header.Key + " - " + header.Value);
                    request.AddHeader(header.Key, header.Value);
                }
            }

            //For testing v11 changes
            //request.AddHeader("api-version", "11");

            Program.Log("Parameters: "+requestBodyParameter.Key + " - " + requestBodyParameter.Value);
            request.AddParameter(requestBodyParameter.Key, requestBodyParameter.Value, ParameterType.RequestBody);
            
            RestResponse response = client.Execute(request);

            Program.Log("StatusCode: "+response.StatusCode.ToString());
            Program.Log("StatusDescription: "+response.StatusDescription);
            Program.Log("Content: "+response.Content);

            if (response.StatusDescription!=null)
            {
                result[0] = response.StatusDescription;
                result[1] = response.Content;
            } else
            {
                Program.Log("Response from server empty!");
            }
            
            return result;
        }
        */

        public static string[] AptomaPost(string url, Dictionary<string, string> headers, KeyValuePair<string, string> requestBodyParameter)
        {
            Program.Log("Posting to Aptoma url: " + url);

            var result = new string[2];

            // Helps on older frameworks
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var options = new RestSharp.RestClientOptions
            {
                Timeout = System.TimeSpan.FromSeconds(30000)
            };
            var client = new RestSharp.RestClient(options);

            // FIX: Proper POST + full URL here (don’t use "Method.POST" as a string)
            var request = new RestSharp.RestRequest(url, RestSharp.Method.Post);

            if (headers != null)
            {
                Program.Log("Headers:");
                foreach (var header in headers)
                {
                    Program.Log(header.Key + " - " + header.Value);
                    request.AddHeader(header.Key, header.Value);
                }
            }

            Program.Log("Parameters: " + requestBodyParameter.Key + " - " + requestBodyParameter.Value);

            var contentTypeHeader = headers != null && headers.TryGetValue("Content-Type", out var ct) ? ct : null;
            if (string.Equals(contentTypeHeader, "application/json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestBodyParameter.Key, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                request.AddStringBody(requestBodyParameter.Value, RestSharp.DataFormat.Json);
            }
            else
            {
                request.AddParameter(requestBodyParameter.Key, requestBodyParameter.Value, RestSharp.ParameterType.RequestBody);
            }

            var response = client.Execute(request);

            //Program.Log("StatusCode: " + (int)response.StatusCode + " (" + response.StatusCode + ")");
            Program.Log("ResponseStatus: " + response.ResponseStatus);
            //Program.Log("ErrorMessage: " + response.ErrorMessage);
            //Program.Log("Content: " + response.Content);

            result[0] = response.StatusDescription ?? response.ResponseStatus.ToString();
            result[1] = response.Content ?? "";

            return result;
        }

        static public string[] PostEdition(string json)
        {
            Program.Log("Sending edition info to Aptoma");

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Authorization", "apikey "+APIKEY);
            headers.Add("Content-Type", "application/json");

            KeyValuePair<string, string> body = new KeyValuePair<string, string>("application/json", json);
            
            return AptomaPost(EDITIONURL, headers, body);
        }

        static public string[] PostPage(string json)
        {
            Program.Log("Sending page info to Aptoma");

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Authorization", "apikey " + APIKEY);
            headers.Add("Content-Type", "application/json");

            KeyValuePair<string, string> body = new KeyValuePair<string, string>("application/json", json);

            return AptomaPost(PAGEURL, headers, body);
        }

        static public string[] PostImage(string xml)
        {
            //Dictionary<string, string> headers = new Dictionary<string, string>();
            GetToken();
            //headers.Add("jwt", token);
            
            KeyValuePair<string, string> body = new KeyValuePair<string, string>("application/xml", xml);

            //return AptomaPost(IMGURL+"?jwt="+token, headers, body);
            return AptomaPost(IMGURL + "?jwt=" + token, null, body);
        }

        public static async Task TestAsync()
        {
            // If you’re on .NET Framework ≤ 4.7.1, uncomment the next line.
            // For 4.7.2+ you can usually leave it out, but enabling TLS 1.2 never hurts.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var options = new RestClientOptions("https://YOUR-HOST")
            {
                MaxTimeout = 30000,            // 30s
                ThrowOnAnyError = false,
                FollowRedirects = true,
                // Proxy = new WebProxy("http://127.0.0.1:8888") // <- uncomment to debug via Fiddler
            };

            var client = new RestClient(options);

            var req = new RestRequest("/your/path", Method.Post);
            req.AddHeader("Accept", "application/json");
            req.AddHeader("Content-Type", "application/json");
            // If you have a raw JSON string:
            // req.AddStringBody(yourJsonString, DataFormat.Json);
            // If you have a C# object:
            // req.AddJsonBody(yourObject);

            RestResponse resp = await client.ExecuteAsync(req);

            Console.WriteLine("StatusCode: " + (int)resp.StatusCode);
            Console.WriteLine("ResponseStatus: " + resp.ResponseStatus);   // Completed, Error, TimedOut, Aborted
            Console.WriteLine("ErrorMessage: " + resp.ErrorMessage);
            Console.WriteLine("ErrorException: " + resp.ErrorException);
            Console.WriteLine("Content length: " + (resp.Content?.Length ?? 0));
            Console.WriteLine("Raw Content: " + resp.Content);
        }
    }
}
