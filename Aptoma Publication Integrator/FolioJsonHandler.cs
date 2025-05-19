using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aptoma_Publication_Integrator
{
    static class FolioJsonHandler
    {
        static JObject mappingObject = new JObject();
        static JObject folioTextObject = new JObject();

        public static void LoadTemplateMap(string fileName)
        {
            Console.WriteLine("Loading JSON template mapping file...");
            mappingObject = JObject.Parse(File.ReadAllText(fileName));
            Console.WriteLine("Done!");
        }

        public static void LoadFolioTextMap(string fileName)
        {
            Console.WriteLine("Loading JSON folio text mapping file...");
            folioTextObject= JObject.Parse(File.ReadAllText(fileName));
            Console.WriteLine("Done!");
        }

        public static string GetFolioMapping(string productName, string pdlTemplateName)
        {
            string folioMapping = "";

            try
            {
                folioMapping = mappingObject[productName]["folioMapping"][pdlTemplateName].ToString();

                Console.WriteLine("Mapping of " + pdlTemplateName + " found: " + folioMapping);
            }
            catch (Exception)
            {
                Console.WriteLine("Mapping of " + pdlTemplateName + " not found.");
            }

            return folioMapping;
        }

        public static List<string> GetFolioText(string pdlTemplateName)
        {
            List<string> folioValues = new List<string>();
            string folioText = "";
            string folioTextSize = "";

            try
            {
                folioText = folioTextObject[pdlTemplateName]["folioText"].ToString();
                folioTextSize = folioTextObject[pdlTemplateName]["folioTextSize"].ToString();

                Console.WriteLine("Folio text found: " + folioText + " with text size: " + folioTextSize);
            }
            catch (Exception)
            {
                Console.WriteLine("Folio text not found.");
            }

            folioValues.Add(folioText);
            folioValues.Add(folioTextSize);

            return folioValues;
        }
    }
}
