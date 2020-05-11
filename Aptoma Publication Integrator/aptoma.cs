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
            Program.Log("Loading settings");

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
        public static string[] AptomaPost(string url, Dictionary<string, string> headers, KeyValuePair<string, string> requestBodyParameter)
        {
            string[] result = new string[2];

            var client = new RestClient(url);
            var request = new RestRequest(Method.POST);

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    request.AddHeader(header.Key, header.Value);
                }
            }            

            request.AddParameter(requestBodyParameter.Key, requestBodyParameter.Value, ParameterType.RequestBody);
            
            IRestResponse response = client.Execute(request);
            //HttpStatusCode sCode = response.StatusCode;
            string sCode = response.StatusDescription;

            result[0] = sCode;
            result[1] = response.Content;

            Program.Log(result[0]);

            if (sCode.Equals("Bad Request") || sCode.Equals("Forbidden") || sCode.Equals("Unauthorized"))
            {                
                Program.Log(result[1]);
            }

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

    }
}
