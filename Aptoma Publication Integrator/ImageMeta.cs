using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Xml;

namespace Aptoma_Publication_Integrator
{
    static class ImageMeta
    {
        static public string GetImageXml(string img)
        {
            string b64 = Convert.ToBase64String(File.ReadAllBytes(img));

            XmlDataDocument doc = new XmlDataDocument();

            GetImageMeta(Image.FromFile(img));

            return "";
        }

        static List<string> GetImageMeta(Image img)
        {
            List<string> meta = new List<string>();

            PropertyItem[] propItems = img.PropertyItems;

            try
            {
                foreach(PropertyItem p in propItems)
                {
                    Console.WriteLine("ID: 0x"+p.Id.ToString());
                    Console.WriteLine("Type: " + p.Type);
                    Console.WriteLine();
                    //Console.WriteLine(Encoding.ASCII.GetString(img.GetPropertyItem(p.Id).Value));
                    
                }
            } catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
            

            

            return meta;
        }
    }
}
