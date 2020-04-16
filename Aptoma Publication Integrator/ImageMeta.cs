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
            string xml = "";
            string base64 = ImageToBase64(file);

            Dictionary<string, string> meta = GetMetaDict(file);

            xml += "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<DPIT:drpublishImportTransformation xmlns:DPIT=\"http://drpublish.aptoma.no/xml/dpit\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://drpublish.aptoma.no/xml/dpit http://drp-dev.aptoma.no/stefan/drpublish/io/dpit.xsd\">";
            xml += "<DPIT:asset>";
            xml += "<DPIT:meta>";
            xml += "<DPIT:assetType>picture</DPIT:assetType>";
            xml += "<DPIT:publication id=\"2\">stpaper</DPIT:publication>";
            xml += "<DPIT:fileName>sonne.jpg</DPIT:fileName>";
            xml += "<DPIT:mimeType>image/jpeg</DPIT:mimeType>";
            xml += "<DPIT:assetOptions>";
            xml += "<DPIT:assetOption name=\"directoryName\" dataType=\"string\" index=\"true\">Sport</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"foo\" dataType=\"string\">bar</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"title\" dataType=\"text\" index=\"true\">";
            xml += meta["title"];
            xml += "</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"description\" dataType=\"text\">Ein schöner Frühlingstag läßt alles so viel freundlicher aussehen</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"credit\" dataType=\"string\" index=\"true\">";
            xml += meta["copyright"];
            xml += "</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"dimensions\" dataType=\"json\">{\"width\": \"150\", \"height\": \"60\"}</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"width\" dataType=\"int\">150</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"height\" dataType=\"int\">60</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"bla\" dataType=\"string\" index=\"true\" multiple=\"true\">eins</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"bla\" dataType=\"string\" index=\"true\" multiple=\"true\">zwei</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"isFool\" dataType=\"boolean\" index=\"true\">true</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"someDate\" dataType=\"date\" index=\"true\">2020-02-02 02:22:22</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"aoi\" dataType=\"json\">{\"focus\":{\"x\":949,\"y\":317},\"width\":181,\"height\":182,\"origin\":\"auto\",\"x\":859,\"y\":226}</DPIT:assetOption>";
            xml += "</DPIT:assetOptions>";
            xml += "</DPIT:meta>";
            xml += "<DPIT:contents>";
            xml += "<DPIT:content type=\"default\">";
            xml += "<DPIT:data encoding=\"base64\">";
            xml += base64;
            xml += "</DPIT:data>";
            xml += "</DPIT:content>";
            xml += "</DPIT:contents>";
            xml += "</DPIT:asset>";
            xml += "</DPIT:drpublishImportTransformation>";

            return xml;
        }

        static Dictionary<string,string> GetMetaDict(string file)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();

            BitmapDecoder decoder = new JpegBitmapDecoder(new FileStream(file, FileMode.Open), BitmapCreateOptions.None, BitmapCacheOption.None);
            BitmapMetadata meta = (BitmapMetadata)decoder.Frames[0].Metadata;

            dict.Add("title", meta.Title.Replace("\r", ", "));
            dict.Add("author", meta.Author[0]);
            dict.Add("copyright", meta.Copyright.Replace("\r", ", "));
            dict.Add("date", meta.DateTaken);
            dict.Add("format", meta.Format);
            dict.Add("subject", meta.Subject);
            dict.Add("comment", meta.Comment);
            dict.Add("height", Math.Floor(decoder.Frames[0].Height / 4 * 3).ToString());
            dict.Add("width", Math.Floor(decoder.Frames[0].Width / 4 * 3).ToString());
            
            string keywords = "";
            foreach (string s in meta.Keywords)
            {
                keywords += s + ", ";
            }

            dict.Add("keywords", keywords);

            return dict;
        }

        static List<string> GetMetaList(string file)
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

        static string ImageToBase64(string file)
        {
            string b64 = "";

            b64 = Convert.ToBase64String(File.ReadAllBytes(file));

            return b64;
        }
    }
}
