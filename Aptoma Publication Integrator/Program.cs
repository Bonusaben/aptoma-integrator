using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using System.Configuration;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml;
using System.Drawing;

namespace Aptoma_Publication_Integrator
{
    class Program
    {
        static string INPUTDIR;
        static string ERRORDIR;
        static string LOGFILE;

        static Timer TIMER;
        static int TIMERINTERVAL = 5000; // Milliseconds

        static List<string> FILELIST;

        static bool WORKING = false;

        static string DBURL, DBPORT, DBUSER, DBPASS;
        static string CONNECTIONSTRING;

        static void Main(string[] args)
        {
            LoadSettings();
            StartPolling();
            Aptoma.Init();

            Console.Write("Press <Escape> to exit... ");
            while (Console.ReadKey().Key != ConsoleKey.Escape) { }
        }

        static void LoadSettings()
        {
            var appSettings = ConfigurationManager.AppSettings;

            OracleConfiguration.LoadBalancing = false;
            OracleConfiguration.HAEvents = false;

            INPUTDIR = appSettings.Get("INPUTDIR");
            ERRORDIR = appSettings.Get("ERRORDIR");
            LOGFILE = appSettings.Get("LOGFILE");
            TIMERINTERVAL = Int32.Parse(appSettings.Get("TIMERINTERVAL"));

            DBURL = appSettings.Get("DBURL");
            DBPORT = appSettings.Get("DBPORT");
            DBUSER = appSettings.Get("DBUSER");
            DBPASS = appSettings.Get("DBPASS");

            CONNECTIONSTRING = @"Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=" + DBURL + ")(PORT=" + DBPORT + ")))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=JFMDB)));User Id=" + DBUSER + ";Password=" + DBPASS;
        }

        static void StartPolling()
        {
            TIMER = new Timer(TIMERINTERVAL);
            TIMER.Elapsed += new ElapsedEventHandler(Poll);
            TIMER.Enabled = true;

            Log("Timer started with interval: " + TIMERINTERVAL + " milliseconds.");
        }

        static void Poll(object sender, ElapsedEventArgs e)
        {
            if (!WORKING)
            {
                WORKING = true;

                FILELIST = new List<string>(Directory.GetFiles(INPUTDIR));

                foreach(string file in FILELIST)
                {
                    string[] fileSplit = file.Split('\\');
                    string fileName = fileSplit[fileSplit.Length - 1];

                    string[] fileNameSplit = fileName.Split('.');
                    string extension = fileNameSplit[fileNameSplit.Length - 1].ToLower();
                    
                    if (extension.Substring(0,1).ToLower().Equals("p"))
                    {
                        Log("Processing pdl file: " + fileName);
                        string json = ConvertPDLtoJSON(file);
                        string[] response = Aptoma.PostPage(json);
                        if (response[0].Equals("OK"))
                        {
                            Log("Page successfully uploaded");
                        } else
                        {
                            Log("Error uploading page!");
                            Log("Moving " + fileName + " to error folder.");
                            File.Copy(file, ERRORDIR + "\\" + fileName, true);
                        }
                    } else if (extension.Equals("xml"))
                    {
                        Log("Processing xml file: " + fileName);
                        string json = ConvertXMLToJson(file);
                        string[] response = Aptoma.PostEdition(json);
                        if (response[0].Equals("OK"))
                        {
                            Log("Edition successfully uploaded");
                        }
                        else
                        {
                            Log("Error uploading edition!");
                            Log("Moving " + fileName + " to error folder.");
                            File.Copy(file, ERRORDIR + "\\" + fileName, true);
                        }
                    } else if (extension.Equals("jpg"))
                    {
                        Log("Processing jpg file: " + fileName);
                        string xml = ImageMeta.GetImageXml(file);
                        string[] response = Aptoma.PostImage(xml);
                        if (response[0].Equals("OK"))
                        {
                            Log("Image successfully uploaded");
                        }
                        else
                        {
                            Log("Error uploading image!");
                            Log("Moving " + fileName + " to error folder.");
                            File.Copy(file, ERRORDIR + "\\" + fileName, true);
                        }
                    } else
                    {
                        Log("Unknown fileformat: " + extension);
                        Log("Moving " + fileName + " to error folder.");
                        File.Copy(file, ERRORDIR + "\\" + fileName,true);
                    }

                    try
                    {
                        File.Delete(file);
                    } catch(Exception ex)
                    {
                        Log(ex.Message);
                    }

                }

                WORKING = false;
            } else
            {
                Log("Already processing files. Skipping poll.");
            }
        }

        static string ConvertXMLToJson(string file)
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            JsonWriter jw = new JsonTextWriter(sw);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(file);

            XmlNamespaceManager xmlnsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            xmlnsManager.AddNamespace("ns", "http://www.ccieurope.com/xmlns/CCIPlanner");

            XmlNode root = xmlDoc.DocumentElement;

            // JSON
            jw.WriteStartObject();
                jw.WritePropertyName("data");
            jw.WriteStartObject();
                jw.WritePropertyName("edition");
            // END JSON

            string zone, date;
            
            zone = root.SelectSingleNode("//ns:Zone", xmlnsManager).InnerText;
            date = root.SelectSingleNode("//ns:Date", xmlnsManager).InnerText;

            int year, month, day;

            year = Int32.Parse(date.Substring(4,2)) + 2000;
            month = Int32.Parse(date.Substring(2, 2));
            day = Int32.Parse(date.Substring(0, 2));
            DateTime d = new DateTime(year, month, day);
            date = d.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // JSON
            jw.WriteStartObject();
                jw.WritePropertyName("zone");
                    jw.WriteValue(zone);
                jw.WritePropertyName("publishDate");
                    jw.WriteValue(date);
            jw.WriteEndObject();
            // END JSON

            List<string> sections = new List<string>();
            XmlNodeList nodes = root.SelectNodes("//ns:PhysicalBook", xmlnsManager);

            foreach(XmlNode node in nodes)
            {
                string s = node.InnerText;
                if (!sections.Contains(s))
                {
                    sections.Add(s);
                }
            }

            // JSON
            jw.WritePropertyName("sections");
            jw.WriteStartArray();
            foreach(string s in sections)
            {
                jw.WriteStartObject();
                jw.WritePropertyName("section");
                jw.WriteValue(s);
                jw.WritePropertyName("pages");
                jw.WriteStartArray();

                XmlNodeList pageNodes = root.SelectNodes("//ns:PhysicalBook[text()='"+s+"']/parent::*", xmlnsManager);
                
                foreach (XmlNode pageNode in pageNodes)
                {
                    XmlNode pageNum = pageNode.SelectSingleNode("./ns:BookPageNumber",xmlnsManager);
                    jw.WriteStartObject();
                    jw.WritePropertyName("page");
                    jw.WriteValue(pageNum.InnerText);

                    XmlNode folioNode = pageNode.SelectSingleNode("./ns:PageId", xmlnsManager);
                    jw.WritePropertyName("folio");
                    jw.WriteValue(folioNode.InnerText);

                    jw.WriteEndObject();
                }

                jw.WriteEndArray();
                jw.WriteEndObject();
                
            }

            jw.WriteEndArray();
            jw.WriteEndObject();
            jw.WriteEndObject();
            // END JSON

            return sb.ToString();
        }

        static string ConvertPDLtoJSON(string pdlFile)
        {
            //
            // First line is page info:
            // I=20200121	T=KUO_Forside_H	ZONE=KUO	AVIS=KUO	EDITION=	HEIGHT=1034.66		C	M	Y	K	C	M	Y	K
            //
            // Subsequent lines are ads:
            // X=19.84	Y=615.13	dX=243.78	dY=212.6	Class=1	Type=EPS,A	File=\\jf.medier\Data\CrossAd\JFMDB-data\Eps\7\1\015569517.EPS	Id=1	String=15569517,FORB1,Kolding Bedemandsforretning,,2x75,CMYK,Forsider KUO	
            // X = 19.84 Y = 836.24    dX = 243.78   dY = 212.6    Class = 0 Type = EPS,A File =\\jf.medier\Data\CrossAd\JFMDB - data\Eps\0\1\015584410_002.EPS  Id = 2    String = 15584410,FORB1,Realmæglerne Kolding v / René Lorentzen,2x75,CMYK,GROW GIS print 2020 - 2021
            //

            List<string> lines = new List<string>();
            var reader = new StreamReader(pdlFile, Encoding.GetEncoding("windows-1252"));
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                lines.Add(line);
            }
            reader.Close();

            // Get the date
            string date = lines[0].Split('\t')[0].Split('=')[1];
            int year = Int32.Parse(date.Substring(0, 4));
            int month = Int32.Parse(date.Substring(4, 2));
            int day = Int32.Parse(date.Substring(6, 2));
            DateTime d = new DateTime(year, month, day);
            date = d.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Start building the JSON
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            JsonWriter jw = new JsonTextWriter(sw);

            jw.WriteStartObject();

            jw.WritePropertyName("data");
            jw.WriteStartObject();

            jw.WritePropertyName("edition");
            jw.WriteStartObject();

            jw.WritePropertyName("zone");
            jw.WriteValue(lines[0].Split('\t')[3].Split('=')[1]);

            jw.WritePropertyName("publishDate");
            jw.WriteValue(date);

            jw.WritePropertyName("section");
            jw.WriteValue(pdlFile.Split('\\').Last().Substring(10, 1));

            jw.WritePropertyName("page");
            jw.WriteValue(Int32.Parse(pdlFile.Split('\\').Last().Substring(11, 3)));

            jw.WritePropertyName("folio");
            jw.WriteValue(lines[0].Split('\t')[1].Split('=')[1]);
            jw.WriteEndObject();

            // Removes the page info, so we can loop through the ads
            lines.Remove(lines[0]);

            jw.WritePropertyName("ads");
            jw.WriteStartArray();
            foreach (string line in lines)
            {
                if (line.Length != 0)
                {
                    // Ads
                    if (line.Split('\t')[5].Split('=')[1].Split(',')[0].Equals("EPS"))
                    {
                        int orderNr = Int32.Parse(line.Split('\t')[8].Split('=')[1].Split(',')[0]);
                        //int orderNr = Int32.Parse(line.Split('\t')[6].Split('\\').Last().Split('.')[0]);
                        
                        // X=274.97	Y=615.13	dX=243.78	dY=212.6	Class=1	Type=EPS,A	File=\\jf.medier\Data\CrossAd\JFMDB-data\eps\6\5\015628556.EPS	Id=3	String=15628556,FORB1,Kolding Mægleren ApS,2x75,CMYK,Forsider 2020	

                        string url = OrderLinkLookup(orderNr);
                        bool unpaid = OrderPaidLookup(orderNr);

                        jw.WriteStartObject();
                        jw.WritePropertyName("orderNumber");
                        //jw.WriteValue(orderNr);
                        jw.WriteValue(line.Split('\t')[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ""));
                        jw.WritePropertyName("file");
                        jw.WriteValue(line.Split('\t')[6].Split('=')[1].Split('\\').Last().Replace(".EPS",".PDF"));
                        jw.WritePropertyName("imageFile");
                        jw.WriteValue(line.Split('\t')[6].Split('=')[1].Split('\\').Last().Split('.')[0]+".jpg");
                        jw.WritePropertyName("url");
                        jw.WriteValue(url);
                        jw.WritePropertyName("x");
                        jw.WriteValue(float.Parse(line.Split('\t')[0].Split('=')[1], CultureInfo.InvariantCulture));
                        jw.WritePropertyName("y");
                        jw.WriteValue(float.Parse(line.Split('\t')[1].Split('=')[1], CultureInfo.InvariantCulture));
                        jw.WritePropertyName("width");
                        jw.WriteValue(float.Parse(line.Split('\t')[2].Split('=')[1], CultureInfo.InvariantCulture));
                        jw.WritePropertyName("height");
                        jw.WriteValue(float.Parse(line.Split('\t')[3].Split('=')[1], CultureInfo.InvariantCulture));
                        jw.WritePropertyName("bookingCode");
                        jw.WriteValue(line.Split('\t')[8].Split('=')[1].Split(',')[1]);
                        jw.WritePropertyName("unpaid");
                        jw.WriteValue(unpaid);
                        jw.WritePropertyName("customer");
                        jw.WriteValue(line.Split('\t')[8].Split('=')[1].Split(',')[2]);
                        jw.WritePropertyName("adReady");
                        jw.WriteValue(line.Split('\t')[4].Split('=')[1]);
                        jw.WritePropertyName("comment");
                        jw.WriteValue(line.Split('\t')[8].Split('=')[1].Split(',')[5]);
                        jw.WriteEndObject();
                    }
                }
            }
            jw.WriteEndArray();

            jw.WritePropertyName("dividers");
            jw.WriteStartArray();
            foreach (string line in lines)
            {
                if (line.Length != 0)
                {
                    // Dividers
                    if (line.Split('\t')[5].Split('=')[1].Split(',')[0].Equals("LINES"))
                    {
                        jw.WriteStartObject();
                        jw.WritePropertyName("x");
                        jw.WriteValue(float.Parse(line.Split('\t')[0].Split('=')[1], CultureInfo.InvariantCulture));
                        jw.WritePropertyName("y");
                        jw.WriteValue(float.Parse(line.Split('\t')[1].Split('=')[1], CultureInfo.InvariantCulture));
                        jw.WritePropertyName("x2");
                        jw.WriteValue(float.Parse(line.Split('\t')[2].Split('=')[1], CultureInfo.InvariantCulture));
                        jw.WritePropertyName("y2");
                        jw.WriteValue(float.Parse(line.Split('\t')[3].Split('=')[1], CultureInfo.InvariantCulture));
                        //jw.WritePropertyName("thickness");
                        //jw.WriteValue(line.Split('\t')[5].Split('=')[1].Split(',')[1]);
                        jw.WriteEndObject();
                    }
                }
            }
            jw.WriteEndArray();

            jw.WriteEndObject();
            jw.WriteEndObject();



            return sb.ToString();
        }

        static string OrderLinkLookup(int orderNr)
        {
            string link = "";

            OracleConnection con = new OracleConnection(@CONNECTIONSTRING);

            string query = @"SELECT OWK_SearchValue FROM F_OrderWDetSK JOIN F_OrderWAdOrg ON OWK_AdOrgID=OWO_OrgID WHERE OWK_SearchKeyID=4 AND OWK_SearchValue NOT LIKE '%@%' AND OWK_SearchValue LIKE '%.%' AND OWO_ONo=" + orderNr + " AND rownum <= 1";
            OracleCommand command = new OracleCommand(query, con);

            try
            {
                con.Open();
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    link = (string)reader.GetValue(0);
                }
                //Console.WriteLine("Link for ordernumber " + orderNr + ": " + link);

                con.Close();
            }
            catch (Exception ex)
            {
                Log("Unable to get link from database");
            }

            return link;
        }

        static bool OrderPaidLookup(int orderNr)
        {
            bool unpaid = false;

            OracleConnection con = new OracleConnection(@CONNECTIONSTRING);

            string query = @"SELECT OP_PrFlowCode FROM F_OrProdFlow WHERE OP_ONo=" + orderNr + " AND OP_PrFlowCode='UGEBG'";
            OracleCommand command = new OracleCommand(query, con);

            try
            {
                con.Open();
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    //Console.WriteLine("Value in DB: " + (string)reader.GetValue(0));
                    unpaid = true;
                }
                //Console.WriteLine("Order unpaid: " + unpaid);

                con.Close();
            }
            catch (Exception ex)
            {
                Log("Unable to get paid status from database");
            }

            return unpaid;
        }

        public static void Log(string s)
        {
            s = DateTime.Now.ToString(CultureInfo.CurrentUICulture) + " - " + s;
            Console.WriteLine(s);

            if (LOGFILE != "")
            {
                StreamWriter sw = File.AppendText(LOGFILE);
                sw.WriteLine(s);
                sw.Close();
            }
        }

        
    }
}
