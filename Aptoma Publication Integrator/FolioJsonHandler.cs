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
        static JObject o1 = new JObject();

        public static void LoadTemplateMap(string fileName)
        {
            Console.WriteLine("Loading JSON template file...");
            o1 = JObject.Parse(File.ReadAllText(fileName));
            Console.WriteLine("Done!");
        }

        public static string GetFolioMapping(string key)
        {
            string value = "aloha";
            string bla = o1.GetValue(key).ToString();

            Console.WriteLine(bla);

            /*
            "AAS": {
                "folioMapping": {
                    "AAS_Debat1_V": "LEDER AAS",
                    "AAS_Debat1_H": "LEDER AAS",
                    "AAS_Tegneserie_H": "Vejr Top",
                    "AAS_Tegneserie_V": "Vejr Top"
                }
            }
            */

            return value;
        }
    }
}
