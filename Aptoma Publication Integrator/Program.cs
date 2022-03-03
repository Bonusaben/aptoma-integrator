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
        static string OUTPUTDIR;
        static string XMLURLDIR;

        static Timer TIMER;
        static int TIMERINTERVAL = 5000; // Milliseconds

        static List<string> FILELIST;

        static bool WORKING = false;
        static bool SAVEOUTPUT = false;
        static bool DEBUG = false;

        static string DBURL, DBPORT, DBUSER, DBPASS;
        static string CONNECTIONSTRING;

        static string DATEFORSQL;

        static void Main(string[] args)
        {
            LoadSettings();
            StartPolling();
            Aptoma.Init();

            if (SAVEOUTPUT)
            {
                Log("ATTENTION! SAVEOUTPUT is set to true. All files will be saved in " + OUTPUTDIR);
            }

            if (DEBUG)
            {
                Log("PROGRAM IS IN DEBUG MODE! Info will not be sent to Aptoma");
            }

            Console.Write("Press <Escape> to exit...\n");
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

            DEBUG = Boolean.Parse(appSettings.Get("DEBUG"));
            SAVEOUTPUT = Boolean.Parse(appSettings.Get("SAVEOUTPUT"));
            OUTPUTDIR = appSettings.Get("OUTPUTDIR");
            XMLURLDIR = appSettings.Get("XMLURLDIR");

            if (INPUTDIR.Substring(INPUTDIR.Length - 1) != "\\")
            {
                INPUTDIR += "\\";
            }
            if (ERRORDIR.Substring(ERRORDIR.Length - 1) != "\\")
            {
                ERRORDIR += "\\";
            }
            if (OUTPUTDIR.Substring(OUTPUTDIR.Length - 1) != "\\")
            {
                OUTPUTDIR += "\\";
            }
            if (XMLURLDIR.Substring(XMLURLDIR.Length - 1) != "\\")
            {
                XMLURLDIR += "\\";
            }

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

                FILELIST = new List<string>(Directory.GetFiles(INPUTDIR).OrderBy(d => new FileInfo(d).CreationTime));

                if (FILELIST.Count > 0)
                {

                    System.Threading.Thread.Sleep(2000); // Short pause to ensure all files in list are readable

                    foreach (string file in FILELIST)
                    {
                        bool skipFile = false;

                        string[] fileSplit = file.Split('\\');
                        string fileName = fileSplit[fileSplit.Length - 1];

                        string[] fileNameSplit = fileName.Split('.');
                        string extension = fileNameSplit[fileNameSplit.Length - 1].ToLower();

                        try
                        {
                            if (extension.Substring(0, 1).ToLower().Equals("p"))
                            {
                                Log("Processing pdl file: " + fileName);
                                string json = ConvertPDLtoJSON(file);

                                if (!DEBUG)
                                {
                                    string[] response = Aptoma.PostPage(json);
                                    if (response[0].Equals("OK"))
                                    {
                                        Log("Page successfully uploaded");
                                    }
                                    else
                                    {
                                        Log("Error uploading page!");
                                        Log("Server response: " + response[0]);
                                        Log("Moving " + fileName + " to error folder.");
                                        File.Copy(file, ERRORDIR + "\\" + fileName, true);
                                    }
                                }

                                if (SAVEOUTPUT)
                                {
                                    SaveOutput(fileName + ".txt", json);
                                }
                            }
                            else if (extension.Equals("xml"))
                            {
                                Log("Processing xml file: " + fileName);
                                string json = ConvertXMLToJson(file);

                                if (!DEBUG)
                                {
                                    string[] response = Aptoma.PostEdition(json);
                                    if (response[0].Equals("OK"))
                                    {
                                        Log("Edition successfully uploaded");
                                    }
                                    else
                                    {
                                        Log("Error uploading edition!");
                                        Log("Server response: " + response[0]);
                                        Log("Moving " + fileName + " to error folder.");
                                        File.Copy(file, ERRORDIR + "\\" + fileName, true);
                                    }
                                }

                                if (SAVEOUTPUT)
                                {
                                    SaveOutput(fileName + ".txt", json);
                                }
                            }
                            else if (extension.Equals("jpg"))
                            {
                                Log("Processing jpg file: " + fileName);
                                string xml = ImageMeta.GetImageXml(file);

                                if (!DEBUG)
                                {
                                    string[] response = Aptoma.PostImage(xml);
                                    if (response[0].Equals("OK"))
                                    {
                                        Log("Image successfully uploaded");
                                    }
                                    else
                                    {
                                        Log("Error uploading image!");
                                        Log("Server response: " + response[0]);
                                        Log("Moving " + fileName + " to error folder.");
                                        File.Copy(file, ERRORDIR + "\\" + fileName, true);
                                    }
                                }

                                if (SAVEOUTPUT)
                                {
                                    SaveOutput(fileName + ".txt", xml);
                                }
                            }
                            else
                            {
                                Log("Unknown fileformat: " + extension);
                                Log("Moving " + fileName + " to error folder.");
                                File.Copy(file, ERRORDIR + "\\" + fileName, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log(ex.ToString());
                            //Log(ex.Message);
                            Log("Skipping file...");
                            skipFile = true;
                        }


                        if (!skipFile)
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                Log(ex.Message);
                            }
                        }

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

            // Filename for link lookup in XML files
            string xmlFilename = lines[0].Split('\t')[3].Split('=')[1] + "_" + date.Substring(0, 4) + "-" + date.Substring(4, 2) + "-" + date.Substring(6, 2) + ".xml"; //FT_2022-01-14.xml


            int year = Int32.Parse(date.Substring(0, 4));
            int month = Int32.Parse(date.Substring(4, 2));
            int day = Int32.Parse(date.Substring(6, 2));

            DateTime d = new DateTime(year, month, day);
            date = d.ToString("yyyy-MM-ddTHH:mm:ssZ");

            DATEFORSQL = GetDateForSQL(year, month, day);

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

            // ADS
            jw.WritePropertyName("ads");
            jw.WriteStartArray();

            if (lines.Count > 0)
            {
                foreach (string line in lines)
                {
                    if (line.Length != 0)
                    {
                        if (line.Split('\t')[5].Split('=')[1].Split(',')[0].Equals("EPS") || line.Split('\t')[5].Split('=')[1].Split(',')[0].Equals("PDF"))
                        {
                            int orderNr = Int32.Parse(line.Split('\t')[8].Split('=')[1].Split(',')[0]);

                            // X=274.97	Y=615.13	dX=243.78	dY=212.6	Class=1	Type=EPS,A	File=\\jf.medier\Data\CrossAd\JFMDB-data\eps\6\5\015628556.EPS	Id=3	String=15628556,FORB1,Kolding Mægleren ApS,2x75,CMYK,Forsider 2020	

                            string url = OrderLinkLookup(orderNr, xmlFilename); 
                            bool unpaid = OrderPaidLookup(orderNr);

                            //string prefix = @"Ads\";

                            jw.WriteStartObject();
                            jw.WritePropertyName("orderNumber");
                            jw.WriteValue(line.Split('\t')[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ""));
                            //jw.WritePropertyName("file");
                            //jw.WriteValue(@prefix+line.Split('\t')[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ".PDF"));
                            //jw.WritePropertyName("imageFile");
                            //jw.WriteValue(@prefix + line.Split('\t')[6].Split('=')[1].Split('\\').Last().Split('.')[0] + ".jpg");
                            jw.WritePropertyName("file");
                            jw.WriteValue(line.Split('\t')[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ".PDF"));
                            jw.WritePropertyName("imageFile");
                            jw.WriteValue(line.Split('\t')[6].Split('=')[1].Split('\\').Last().Split('.')[0] + ".jpg");
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
            }
            
            jw.WriteEndArray();

            // DIVIDERS
            jw.WritePropertyName("dividers");
            jw.WriteStartArray();

            if (lines.Count > 0)
            {
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
            }
            
            jw.WriteEndArray();

            // HEADERS
            jw.WritePropertyName("headers");
            jw.WriteStartArray();

            if (lines.Count > 0)
            {
                foreach (string line in lines)
                {
                    if (line.Length != 0)
                    {
                        if (line.Split('\t')[5].Split('=')[1].Split(',')[0].Equals("HEADLINE"))
                        {
                            string prefix = @"Header\";

                            jw.WriteStartObject();

                            jw.WritePropertyName("file");
                            jw.WriteValue(prefix + line.Split('\t')[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ".PDF"));
                            jw.WritePropertyName("imageFile");
                            jw.WriteValue(prefix + line.Split('\t')[6].Split('=')[1].Split('\\').Last().Split('.')[0] + ".jpg");
                            jw.WritePropertyName("x");
                            jw.WriteValue(float.Parse(line.Split('\t')[0].Split('=')[1], CultureInfo.InvariantCulture));
                            jw.WritePropertyName("y");
                            jw.WriteValue(float.Parse(line.Split('\t')[1].Split('=')[1], CultureInfo.InvariantCulture));
                            jw.WritePropertyName("width");
                            jw.WriteValue(float.Parse(line.Split('\t')[2].Split('=')[1], CultureInfo.InvariantCulture));
                            jw.WritePropertyName("height");
                            jw.WriteValue(float.Parse(line.Split('\t')[3].Split('=')[1], CultureInfo.InvariantCulture));

                            jw.WriteEndObject();
                        }
                    }
                }
            }

            jw.WriteEndArray();

            // FILLERS
            jw.WritePropertyName("fillers");
            jw.WriteStartArray();

            if (lines.Count > 0)
            {
                foreach (string line in lines)
                {
                    if (line.Length != 0)
                    {
                        if (line.Split('\t')[5].Split('=')[1].Split(',')[0].Equals("FILLER"))
                        {
                            string prefix = @"Filler\";

                            jw.WriteStartObject();

                            jw.WritePropertyName("file");
                            jw.WriteValue(@prefix + line.Split('\t')[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ".PDF"));
                            jw.WritePropertyName("imageFile");
                            jw.WriteValue(@prefix + line.Split('\t')[6].Split('=')[1].Split('\\').Last().Split('.')[0] + ".jpg");
                            jw.WritePropertyName("x");
                            jw.WriteValue(float.Parse(line.Split('\t')[0].Split('=')[1], CultureInfo.InvariantCulture));
                            jw.WritePropertyName("y");
                            jw.WriteValue(float.Parse(line.Split('\t')[1].Split('=')[1], CultureInfo.InvariantCulture));
                            jw.WritePropertyName("width");
                            jw.WriteValue(float.Parse(line.Split('\t')[2].Split('=')[1], CultureInfo.InvariantCulture));
                            jw.WritePropertyName("height");
                            jw.WriteValue(float.Parse(line.Split('\t')[3].Split('=')[1], CultureInfo.InvariantCulture));

                            jw.WriteEndObject();
                        }
                    }
                }
            }

            jw.WriteEndArray();

            jw.WriteEndObject();
            jw.WriteEndObject();



            return sb.ToString();
        }

        static string OrderLinkLookup(int orderNr,string xmlFilename)
        {
            string link = "";
            
            OracleConnection con = new OracleConnection(@CONNECTIONSTRING);

            //SELECT OD_URL FROM F_OrderDet WHERE OD_ONO =:orno AND OD_ISSUE_DATE = TO_DATE(:thedate, 'YYYY-MM-DD')
            string query = @"SELECT OWK_SearchValue FROM F_OrderWDetSK JOIN F_OrderWAdOrg ON OWK_AdOrgID=OWO_OrgID WHERE OWK_SearchKeyID=4 AND OWK_SearchValue NOT LIKE '%@%' AND OWK_SearchValue LIKE '%.%' AND OWO_ONo=" + orderNr + " AND rownum <= 1";
            string queryNEW = @"SELECT OD_URL FROM F_OrderDet WHERE OD_ONO =" + orderNr + " AND OD_ISSUE_DATE = TO_DATE('" + DATEFORSQL + "', 'YYYY-MM-DD')";
            //string queryNEW = @"SELECT OD_URL FROM F_OrderDet WHERE OD_ONO =" + orderNr + " AND OD_ISSUE_DATE = " + DATEFORSQL;

            OracleCommand command = new OracleCommand(queryNEW, con);

            try
            {
                con.Open();
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    link = (string)reader.GetValue(0);
                }

                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                //Log("Unable to get link from database");
                //Console.WriteLine("Unable to get link with new method");
                //Console.WriteLine("Message: " + ex.Message);
            }
            

            if (link.Length == 0)
            {
                command = new OracleCommand(query, con);

                try
                {
                    con.Open();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        link = (string)reader.GetValue(0);
                    }

                    con.Close();
                }
                catch (Exception ex)
                {
                    con.Close();
                    //Log("Unable to get link from database");
                    //Console.WriteLine("Unable to get link with old method");
                    //Console.WriteLine("Message: " + ex.Message);
                }
            }

            if (link.Length == 0)
            {
                // Get link from XML file
                link = GetLinkFromXML(orderNr, xmlFilename);
            }

            if (link.Length > 0)
            {
                Console.WriteLine("Link: " + link);
            }
            
            return link;
        }

        static string GetLinkFromXML(int orderNr,string xmlFilename)
        {
            string urlFromXml = "";
            List<string> lines = new List<string>();

            if (File.Exists(XMLURLDIR + xmlFilename))
            {
                if (DEBUG)
                {
                    Log("XMLURL file exists");
                }

                var reader = new StreamReader(XMLURLDIR + xmlFilename, Encoding.GetEncoding("windows-1252"));
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    lines.Add(line);
                }
                reader.Close();
            } else
            {
                if (DEBUG)
                {
                    Log("XMLURL file does not exist");
                }
            }

            // if single-line xml split it
            bool multiLine = true;
            if (lines.Count == 1)
            {
                lines = lines[0].Split(new string[] { "<homepage>" }, StringSplitOptions.None).ToList();
                multiLine = false;
            }

            if (lines.Count > 0)
            {
                if (DEBUG)
                {
                    Console.WriteLine("Numner of lines in xml: "+lines.Count);
                    Console.WriteLine("Searching for orderNr: "+orderNr.ToString());
                }
                for (int i = 0; i < lines.Count; i++){
                    if (DEBUG)
                    {
                        Console.WriteLine("Line " + i + ": " + lines[i]);
                    }
                    if (lines[i].Contains(orderNr.ToString()))
                    {
                        if (DEBUG)
                        {
                            Console.WriteLine("orderNr found!");
                        }

                        //<homepage>https://www.femoekro.dk/</homepage>
                        if (multiLine)
                        {
                            urlFromXml = lines[i + 1].Split('>')[1].Split('<')[0];
                        } else
                        {
                            urlFromXml = lines[i + 1].Split('<')[0];
                        }
                        
                        break;
                    }
                }
            }
            

            return urlFromXml;
        }

        static bool OrderPaidLookup(int orderNr)
        {
            bool unpaid = false;

            OracleConnection con = new OracleConnection(@CONNECTIONSTRING);

            string query = @"SELECT OP_PrFlowCode FROM F_OrProdFlow WHERE OP_ONo=" + orderNr + " AND OP_PrFlowCode='UGEB'";
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

        static void SaveOutput(string filename, string s)
        {
            File.WriteAllText(OUTPUTDIR + filename, s);
            //StreamWriter sw = File.AppendText(OUTPUTDIR + filename);
            //sw.WriteLine(json);
            //sw.Close();
            Log("Saved " + filename + " to output dir");
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
        
        static string GetDateForSQL(int year, int month, int day)
        {
            DateTime myDateTime = new DateTime(year, month, day);
            string sqlFormattedDate = myDateTime.ToString("yyyy-MM-dd");
            //string sqlFormattedDate = myDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

            return sqlFormattedDate;
            //return "2021-01-20";
        }
    }
}
