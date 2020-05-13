﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Xml;
using System.Windows.Media.Imaging;
using System.Globalization;

namespace Aptoma_Publication_Integrator
{
    static class ImageMeta
    {
        public static string GetImageXml(string file)
        {
            string xml = "";
            string base64 = ImageToBase64(file);
            string[] filenameSplit = file.Split('\\');
            string filename = filenameSplit[filenameSplit.Length - 1];

            Dictionary<string, string> meta = GetMetaDict(file);
            Dictionary<string, string> pubInfo = GetPublicationInfo(filename);

            xml += "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<DPIT:drpublishImportTransformation xmlns:DPIT=\"http://drpublish.aptoma.no/xml/dpit\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://drpublish.aptoma.no/xml/dpit http://drp-dev.aptoma.no/stefan/drpublish/io/dpit.xsd\">";
            xml += "<DPIT:asset>";
            xml += "<DPIT:meta>";
            xml += "<DPIT:assetType>picture</DPIT:assetType>";
            xml += "<DPIT:publication id=\"2\">Default</DPIT:publication>";
            //xml += "<DPIT:publication id=\"3\">JFM</DPIT:publication>";
            
            xml += "<DPIT:fileName>";
            xml += filename;
            xml += "</DPIT:fileName>";
            xml += "<DPIT:mimeType>image/jpeg</DPIT:mimeType>";

            xml += "<DPIT:assetOptions>";

            xml += "<DPIT:assetOption name=\"publication\" dataType=\"text\" index=\"true\">";
            xml += pubInfo["publication"];
            xml += "</DPIT:assetOption>";

            xml += "<DPIT:assetOption name=\"publishDate\" dataType=\"date\" index=\"true\">";
            xml += pubInfo["pubDate"];
            xml += "</DPIT:assetOption>";

            xml += "<DPIT:assetOption name=\"title\" dataType=\"text\" index=\"true\">";
            //xml += meta["title"];
            xml += pubInfo["title"];
            xml += "</DPIT:assetOption>";

            xml += "<DPIT:assetOption name=\"comment\" dataType=\"text\">";
            xml += meta["comment"];
            xml += "</DPIT:assetOption>";
            //xml += "<DPIT:assetOption name=\"keywords\" dataType=\"text\">";
            //xml += meta["keywords"];
            //xml += "</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"credit\" dataType=\"string\" index=\"true\">";
            xml += meta["copyright"];
            xml += "</DPIT:assetOption>";
            //xml += "<DPIT:assetOption name=\"dimensions\" dataType=\"json\">{\"width\": \"150\", \"height\": \"60\"}</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"width\" dataType=\"int\">";
            xml += meta["width"];
            xml += "</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"height\" dataType=\"int\">";
            xml += meta["height"];
            xml += "</DPIT:assetOption>";
            //xml += "<DPIT:assetOption name=\"isFool\" dataType=\"boolean\" index=\"true\">true</DPIT:assetOption>";
            xml += "<DPIT:assetOption name=\"dateTaken\" dataType=\"date\" index=\"true\">";
            xml += meta["dateTaken"];
            xml += "</DPIT:assetOption>";
            //xml += "<DPIT:assetOption name=\"aoi\" dataType=\"json\">{\"focus\":{\"x\":949,\"y\":317},\"width\":181,\"height\":182,\"origin\":\"auto\",\"x\":859,\"y\":226}</DPIT:assetOption>";

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

            FileStream fs = new FileStream(file, FileMode.Open);
            BitmapDecoder decoder = new JpegBitmapDecoder(fs, BitmapCreateOptions.None, BitmapCacheOption.None);
            //BitmapDecoder decoder = new JpegBitmapDecoder(new FileStream(file, FileMode.Open), BitmapCreateOptions.None, BitmapCacheOption.None);
            BitmapMetadata meta = (BitmapMetadata)decoder.Frames[0].Metadata;

            string dateTaken = DateTime.Parse(meta.DateTaken).ToString("yyyy-MM-ddTHH:mm:ssZ");

            dict.Add("title", meta.Title.Replace("\r", ", "));
            dict.Add("author", meta.Author[0]);
            dict.Add("copyright", meta.Copyright.Replace("\r", ", "));
            dict.Add("dateTaken", dateTaken);
            dict.Add("format", meta.Format);
            dict.Add("subject", meta.Subject);
            dict.Add("comment", meta.Comment);
            dict.Add("height", decoder.Frames[0].PixelHeight.ToString());
            dict.Add("width", decoder.Frames[0].PixelWidth.ToString());
            //dict.Add("height", Math.Floor(decoder.Frames[0].Height / 4 * 3).ToString());
            //dict.Add("width", Math.Floor(decoder.Frames[0].Width / 4 * 3).ToString());

            //string keywords = "";
            //foreach (string s in meta.Keywords)
            //{
            //    keywords += s + ", ";
            //}

            //dict.Add("keywords", keywords);

            fs.Close();

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

        public static void MetaTest(string file)
        {
            Uri.TryCreate(file, UriKind.Absolute, out Uri uriResult);
            //BitmapDecoder decoder = new JpegBitmapDecoder(uriResult, BitmapCreateOptions.None, BitmapCacheOption.None);
            BitmapDecoder decoder = new JpegBitmapDecoder(new FileStream(file, FileMode.Open), BitmapCreateOptions.None, BitmapCacheOption.None);
            BitmapMetadata meta = (BitmapMetadata)decoder.Frames[0].Metadata;

            Console.WriteLine(meta.Author[0]);
        }

        static string ImageToBase64(string file)
        {
            string b64 = "";

            b64 = Convert.ToBase64String(File.ReadAllBytes(file));

            return b64;
        }

        static Dictionary<string,string> GetPublicationInfo(string filename)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();

            // 140520 FST Brand i staldbygning.jpg

            string pubDate = "";
            string publication = "";
            string title = "";

            try
            {
                pubDate = DateTime.ParseExact(filename.Substring(0, 6), "ddMMyy", CultureInfo.InvariantCulture).ToString("yyyy-MM-ddTHH:mm:ssZ");
                //pubDate = filename.Substring(0, 6);
            } catch(Exception ex)
            {
                Program.Log(ex.Message);
            }

            try
            {
                publication = filename.Substring(7, 3);
            }
            catch (Exception ex)
            {
                Program.Log(ex.Message);
            }

            try
            {
                title = filename.Substring(11).Split('.')[0];
            }
            catch (Exception ex)
            {
                Program.Log(ex.Message);
            }

            dict.Add("pubDate", pubDate);
            dict.Add("publication", publication);
            dict.Add("title", title);

            return dict;
        }
    }
}
