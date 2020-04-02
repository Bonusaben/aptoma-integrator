using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Xml;
using System.Windows.Media.Imaging;

namespace Aptoma_Publication_Integrator
{
    static class ImageMeta
    {
        public static string GetImageXml(string file)
        {
            return "";
        }

        static List<string> GetMeta(string file)
        {
            List<string> metaList = new List<string>();

            BitmapDecoder decoder = new JpegBitmapDecoder(new FileStream(file, FileMode.Open), BitmapCreateOptions.None, BitmapCacheOption.None);
            BitmapMetadata meta = (BitmapMetadata)decoder.Frames[0].Metadata;

            metaList.Add("Title: " + meta.Title.Replace("\r", ", "));
            metaList.Add("Author: " + meta.Author[0]);
            string keywords = "";
            foreach (string s in meta.Keywords)
            {
                keywords += s + ", ";
            }
            metaList.Add("Keywords: " + keywords);
            metaList.Add("Copyright: " + meta.Copyright.Replace("\r", ", "));
            metaList.Add("Date: " + meta.DateTaken);

            foreach (string s in metaList)
            {
                Console.WriteLine(s);
            }

            return metaList;
        }
    }
}
