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
    public struct Ad
    {
        public string OrderNumber { get; set; }
        public string File { get; set; }
        public string ImageFile { get; set; }
        public string Url { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string BookingCode { get; set; }
        public bool Unpaid { get; set; }
        public string Customer { get; set; }
        public string AdReady { get; set; }
        public string Comment { get; set; }
    }

    public struct Divider
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
    }

    public struct Header
    {
        public string File { get; set; }
        public string ImageFile { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
    public struct Filler
    {
        public string File { get; set; }
        public string ImageFile { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    public struct PDL
    {
        public string Zone { get; set; }
        public string PublishDate { get; set; }
        public string Section { get; set; }
        public int Page { get; set; }
        public string Folio { get; set; }
        public string FolioText { get; set; }
        public string FolioTextSize { get; set; }
        public List<Ad> Ads { get; set; }
        public List<Divider> Dividers { get; set; }
        public List<Header> Headers { get; set; }
        public List<Filler> Fillers { get; set; }
    }

    class Program
    {
        static string INPUTDIR;
        static string ERRORDIR;
        static string LOGFILE;
        static string OUTPUTDIR;
        static string XMLURLDIR;
        static string XMLNAMESPACE;
        static string TEMPLATEFILE;
        static string FOLIOFILE;
        
        static Timer TIMER;
        static int TIMERINTERVAL = 5000; // Milliseconds

        static List<string> FILELIST;

        static bool WORKING = false;
        static bool SAVEOUTPUT = false;
        static bool DEBUG = false;

        static string DBURL, DBPORT, DBSERVICE, DBUSER, DBPASS;
        static string CONNECTIONSTRING;

        static string DATEFORSQL;

        private static PDL _currentPdl;

        static void Main(string[] args)
        {
            LoadSettings();

            _currentPdl = new PDL
            {
                Ads = new List<Ad>(),
                Dividers = new List<Divider>(),
                Headers = new List<Header>(),
                Fillers = new List<Filler>()
            };

            FolioJsonHandler.LoadTemplateMap(TEMPLATEFILE);
            FolioJsonHandler.LoadFolioTextMap(FOLIOFILE);

            StartPolling();
            Aptoma.Init();



            //FolioJsonHandler.GetFolioMapping("FST","FST_1Sek_Side2_V");
            //FolioJsonHandler.GetFolioText("FST_1Sek_Side2_V");
            

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
            XMLNAMESPACE = appSettings.Get("XMLNAMESPACE");

            TEMPLATEFILE = appSettings.Get("TEMPLATEFILE");
            FOLIOFILE = appSettings.Get("FOLIOFILE");

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
            DBSERVICE = appSettings.Get("DBSERVICE");
            DBUSER = appSettings.Get("DBUSER");
            DBPASS = appSettings.Get("DBPASS");

            CONNECTIONSTRING = @"Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=" + DBURL + ")(PORT=" + DBPORT + ")))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=" + DBSERVICE + ")));User Id=" + DBUSER + ";Password=" + DBPASS;
            //Console.WriteLine("Connection string: " + CONNECTIONSTRING);
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

                                _currentPdl.Ads.Clear();
                                _currentPdl.Dividers.Clear();
                                _currentPdl.Headers.Clear();
                                _currentPdl.Fillers.Clear();

                                //LoadPdl(file, ref _currentPdl);
                                string json = JsonBuilder.BuildPageJson(@file, "https://d3lxqcpoavadc.cloudfront.net/Ads/", "https://d3lxqcpoavadc.cloudfront.net/Ads/");

                                //string json = ConvertPDLtoJSON(file);

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

                                string json = IngestBuilder.LoadXML(file);
                                //string json = ConvertXMLToJson(file);

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

        private static void LoadPdl(string filePath, ref PDL pdl)
        {
            var lines = new List<string>();
            using (var reader = new StreamReader(filePath, Encoding.GetEncoding("windows-1252")))
            {
                while (!reader.EndOfStream)
                    lines.Add(reader.ReadLine());
            }
            if (!lines.Any()) return;

            // Parse header line
            var parts = lines[0].Split('\t');
            var dateRaw = parts[0].Split('=')[1];
            var dt = DateTime.ParseExact(dateRaw, "yyyyMMdd", CultureInfo.InvariantCulture);
            pdl.PublishDate = dt.ToString("yyyy-MM-ddTHH:mm:ssZ");
            pdl.Zone = parts[3].Split('=')[1];
            //pdl.Folio = parts[1].Split('=')[1];
            pdl.Folio = FolioJsonHandler.GetFolioMapping(pdl.Zone, parts[1].Split('=')[1]);
            List<string> _folioText = FolioJsonHandler.GetFolioText(parts[1].Split('=')[1]);
            pdl.FolioText = _folioText[0];
            pdl.FolioTextSize = _folioText[1];

            var fileName = Path.GetFileName(filePath);
            pdl.Section = fileName.Substring(10, 1);
            pdl.Page = int.Parse(fileName.Substring(11, 3));

            var xmlFilename = parts[3].Split('=')[1] + "_" + dt.ToString("yyyy-MM-dd") + ".xml";

            // Remove header
            lines.RemoveAt(0);

            // Ads
            foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l)))
            {
                var cols = line.Split('\t');
                var type = cols[5].Split('=')[1].Split(',')[0];
                if (type == "EPS" || type == "PDF")
                {
                    var orderNr = int.Parse(cols[8].Split('=')[1].Split(',')[0]);
                    var ad = new Ad
                    {
                        OrderNumber = cols[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ""),
                        File = cols[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ".PDF"),
                        ImageFile = cols[6].Split('=')[1].Split('\\').Last().Split('.')[0] + ".jpg",
                        Url = OrderLinkLookup(orderNr, xmlFilename),
                        X = float.Parse(cols[0].Split('=')[1], CultureInfo.InvariantCulture),
                        Y = float.Parse(cols[1].Split('=')[1], CultureInfo.InvariantCulture),
                        Width = float.Parse(cols[2].Split('=')[1], CultureInfo.InvariantCulture),
                        Height = float.Parse(cols[3].Split('=')[1], CultureInfo.InvariantCulture),
                        BookingCode = cols[8].Split('=')[1].Split(',')[1],
                        Unpaid = OrderPaidLookup(orderNr),
                        Customer = cols[8].Split('=')[1].Split(',')[2],
                        AdReady = cols[4].Split('=')[1],
                        Comment = cols[8].Split('=')[1].Split(',')[5]
                    };
                    pdl.Ads.Add(ad);
                }
            }

            // Dividers
            foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l)))
            {
                var cols = line.Split('\t');
                if (cols[5].Split('=')[1].Split(',')[0] == "LINES")
                {
                    var d = new Divider
                    {
                        X = float.Parse(cols[0].Split('=')[1], CultureInfo.InvariantCulture),
                        Y = float.Parse(cols[1].Split('=')[1], CultureInfo.InvariantCulture),
                        X2 = float.Parse(cols[2].Split('=')[1], CultureInfo.InvariantCulture),
                        Y2 = float.Parse(cols[3].Split('=')[1], CultureInfo.InvariantCulture)
                    };
                    pdl.Dividers.Add(d);
                }
            }

            // Headers
            foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l)))
            {
                var cols = line.Split('\t');
                if (cols[5].Split('=')[1].Split(',')[0] == "HEADLINE")
                {
                    var h = new Header
                    {
                        File = "Header\\" + cols[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ".PDF"),
                        ImageFile = "Header\\" + cols[6].Split('=')[1].Split('\\').Last().Split('.')[0] + ".jpg",
                        X = float.Parse(cols[0].Split('=')[1], CultureInfo.InvariantCulture),
                        Y = float.Parse(cols[1].Split('=')[1], CultureInfo.InvariantCulture),
                        Width = float.Parse(cols[2].Split('=')[1], CultureInfo.InvariantCulture),
                        Height = float.Parse(cols[3].Split('=')[1], CultureInfo.InvariantCulture)
                    };
                    pdl.Headers.Add(h);
                }
            }

            // Fillers
            foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l)))
            {
                var cols = line.Split('\t');
                if (cols[5].Split('=')[1].Split(',')[0] == "FILLER")
                {
                    var f = new Filler
                    {
                        File = "Filler\\" + cols[6].Split('=')[1].Split('\\').Last().Replace(".EPS", ".PDF"),
                        ImageFile = "Filler\\" + cols[6].Split('=')[1].Split('\\').Last().Split('.')[0] + ".jpg",
                        X = float.Parse(cols[0].Split('=')[1], CultureInfo.InvariantCulture),
                        Y = float.Parse(cols[1].Split('=')[1], CultureInfo.InvariantCulture),
                        Width = float.Parse(cols[2].Split('=')[1], CultureInfo.InvariantCulture),
                        Height = float.Parse(cols[3].Split('=')[1], CultureInfo.InvariantCulture)
                    };
                    pdl.Fillers.Add(f);
                }
            }
        }

        /*
        public static string BuildPageJson(
        string name,
        string productName,
        string editionName,
        string editionTitle,
        string publishDateIso,
        string pageId,
        string template,
        IEnumerable<AdItem> ads,
        string folioText = "",
        string folioTextSize = "small"
    )
        {
            var sb = new StringBuilder();
            using var sw = new System.IO.StringWriter(sb, CultureInfo.InvariantCulture);
            using var jw = new JsonTextWriter(sw) { Formatting = Formatting.Indented, Culture = CultureInfo.InvariantCulture };

            jw.WriteStartObject();

            jw.WritePropertyName("name"); jw.WriteValue(name);
            jw.WritePropertyName("productName"); jw.WriteValue(productName);
            jw.WritePropertyName("editionName"); jw.WriteValue(editionName);
            jw.WritePropertyName("editionTitle"); jw.WriteValue(editionTitle);
            jw.WritePropertyName("publishDate"); jw.WriteValue(publishDateIso);

            // page
            jw.WritePropertyName("page");
            jw.WriteStartObject();
            jw.WritePropertyName("id"); jw.WriteValue(pageId);
            jw.WritePropertyName("template"); jw.WriteValue(template);

            // properties (after template)
            jw.WritePropertyName("properties");
            jw.WriteStartObject();
            jw.WritePropertyName("folioText"); jw.WriteValue(folioText);
            jw.WritePropertyName("folioTextSize"); jw.WriteValue(folioTextSize);
            jw.WriteEndObject();

            // objects
            jw.WritePropertyName("objects");
            jw.WriteStartObject();

            // ads
            jw.WritePropertyName("ads");
            jw.WriteStartArray();
            if (ads != null)
            {
                foreach (var a in ads)
                {
                    jw.WriteStartObject();
                    jw.WritePropertyName("id"); jw.WriteValue(a.id);
                    jw.WritePropertyName("title"); jw.WriteValue(a.title);
                    jw.WritePropertyName("pdfUrl"); jw.WriteValue(a.pdfUrl);
                    jw.WritePropertyName("previewUrl"); jw.WriteValue(a.previewUrl);
                    jw.WritePropertyName("ready"); jw.WriteValue(a.ready);

                    jw.WritePropertyName("placement");
                    jw.WriteStartObject();
                    jw.WritePropertyName("x"); jw.WriteValue(a.placement.x);
                    jw.WritePropertyName("y"); jw.WriteValue(a.placement.y);
                    jw.WritePropertyName("width"); jw.WriteValue(a.placement.width);
                    jw.WritePropertyName("height"); jw.WriteValue(a.placement.height);
                    jw.WriteEndObject();

                    jw.WritePropertyName("properties");
                    jw.WriteStartObject();
                    jw.WritePropertyName("bookingCode"); jw.WriteValue(a.properties.bookingCode);
                    jw.WritePropertyName("comment"); jw.WriteValue(a.properties.comment);
                    jw.WritePropertyName("adReady"); jw.WriteValue(a.properties.adReady);
                    jw.WriteEndObject();

                    jw.WriteEndObject();
                }
            }
            jw.WriteEndArray(); // ads

            jw.WriteEndObject(); // objects
            jw.WriteEndObject(); // page

            // config
            jw.WritePropertyName("config");
            jw.WriteStartObject();
            jw.WritePropertyName("adItemSchemaName"); jw.WriteValue("print-ad-item");
            jw.WritePropertyName("pdfItemSchemaName"); jw.WriteValue("print-pdf-item");
            jw.WritePropertyName("allowTemplateUpdate"); jw.WriteValue(true);
            jw.WriteEndObject();

            jw.WriteEndObject();
            jw.Flush();

            return sb.ToString();
        }
        /*
        public static string BuildPageJson(PDL pdl)
        {
            using (var sw = new StringWriter())
            using (var writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.Indented;
                writer.WriteStartObject();

                // edition
                writer.WritePropertyName("edition");
                writer.WriteStartObject();
                writer.WritePropertyName("zone"); writer.WriteValue(pdl.Zone);
                writer.WritePropertyName("publishDate"); writer.WriteValue(pdl.PublishDate);
                writer.WritePropertyName("section"); writer.WriteValue(pdl.Section);
                writer.WritePropertyName("page"); writer.WriteValue(pdl.Page);
                writer.WritePropertyName("folio"); writer.WriteValue(pdl.Folio);
                writer.WritePropertyName("folioText"); writer.WriteValue(pdl.FolioText);
                writer.WritePropertyName("folioTextSize"); writer.WriteValue(pdl.FolioTextSize);
                writer.WriteEndObject();

                // ads
                writer.WritePropertyName("ads");
                writer.WriteStartArray();
                foreach (var ad in pdl.Ads)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("orderNumber"); writer.WriteValue(ad.OrderNumber);
                    writer.WritePropertyName("file"); writer.WriteValue(ad.File);
                    writer.WritePropertyName("imageFile"); writer.WriteValue(ad.ImageFile);
                    writer.WritePropertyName("url"); writer.WriteValue(ad.Url);
                    writer.WritePropertyName("x"); writer.WriteValue(ad.X);
                    writer.WritePropertyName("y"); writer.WriteValue(ad.Y);
                    writer.WritePropertyName("width"); writer.WriteValue(ad.Width);
                    writer.WritePropertyName("height"); writer.WriteValue(ad.Height);
                    writer.WritePropertyName("bookingCode"); writer.WriteValue(ad.BookingCode);
                    writer.WritePropertyName("unpaid"); writer.WriteValue(ad.Unpaid);
                    writer.WritePropertyName("customer"); writer.WriteValue(ad.Customer);
                    writer.WritePropertyName("adReady"); writer.WriteValue(ad.AdReady);
                    writer.WritePropertyName("comment"); writer.WriteValue(ad.Comment);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                // dividers
                writer.WritePropertyName("dividers");
                writer.WriteStartArray();
                foreach (var d in pdl.Dividers)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("x"); writer.WriteValue(d.X);
                    writer.WritePropertyName("y"); writer.WriteValue(d.Y);
                    writer.WritePropertyName("x2"); writer.WriteValue(d.X2);
                    writer.WritePropertyName("y2"); writer.WriteValue(d.Y2);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                // headers
                writer.WritePropertyName("headers");
                writer.WriteStartArray();
                foreach (var h in pdl.Headers)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("file"); writer.WriteValue(h.File);
                    writer.WritePropertyName("imageFile"); writer.WriteValue(h.ImageFile);
                    writer.WritePropertyName("x"); writer.WriteValue(h.X);
                    writer.WritePropertyName("y"); writer.WriteValue(h.Y);
                    writer.WritePropertyName("width"); writer.WriteValue(h.Width);
                    writer.WritePropertyName("height"); writer.WriteValue(h.Height);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                // fillers
                writer.WritePropertyName("fillers");
                writer.WriteStartArray();
                foreach (var f in pdl.Fillers)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("file"); writer.WriteValue(f.File);
                    writer.WritePropertyName("imageFile"); writer.WriteValue(f.ImageFile);
                    writer.WritePropertyName("x"); writer.WriteValue(f.X);
                    writer.WritePropertyName("y"); writer.WriteValue(f.Y);
                    writer.WritePropertyName("width"); writer.WriteValue(f.Width);
                    writer.WritePropertyName("height"); writer.WriteValue(f.Height);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WriteEndObject();
                return sw.ToString();
            }
        }
        */
        
        static string ConvertXMLToJson(string file)
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            JsonWriter jw = new JsonTextWriter(sw);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(file);

            XmlNamespaceManager xmlnsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            //xmlnsManager.AddNamespace("ns", "http://www.ccieurope.com/xmlns/CCIPlanner");
            xmlnsManager.AddNamespace("ns", XMLNAMESPACE);

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

        
        /*
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
        */

        static string OrderLinkLookup(int orderNr,string xmlFilename)
        {
            
            string link = "";
            
            OracleConnection con = new OracleConnection(@CONNECTIONSTRING);

            string query = @"SELECT OWK_SearchValue FROM F_OrderWDetSK JOIN F_OrderWAdOrg ON OWK_AdOrgID=OWO_OrgID WHERE OWK_SearchKeyID=4 AND OWK_SearchValue NOT LIKE '%@%' AND OWK_SearchValue LIKE '%.%' AND OWO_ONo=" + orderNr + " AND rownum <= 1";
            string queryNEW = @"SELECT OD_URL FROM F_OrderDet WHERE OD_ONO =" + orderNr + " AND OD_ISSUE_DATE = TO_DATE('" + DATEFORSQL + "', 'YYYY-MM-DD') AND OD_URL IS NOT NULL";
            //string queryNEW = @"SELECT OD_URL FROM F_OrderDet WHERE OD_ONO =" + orderNr + " AND OD_ISSUE_DATE = TO_DATE('" + DATEFORSQL + "', 'YYYY-MM-DD')";

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
                Console.WriteLine("Message: " + ex.Message);
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
                            Console.WriteLine("Line number: " + i);
                            Console.WriteLine("Multiline XML: " + multiLine);
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
