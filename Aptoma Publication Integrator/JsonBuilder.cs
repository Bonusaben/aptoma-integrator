using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Aptoma_Publication_Integrator;
using Newtonsoft.Json;

public static class JsonBuilder
{
    private const double PtToMm = 0.3527777778d;

    // --------- structs (camelCase) ----------
    public struct Pdl
    {
        public string zone;
        public string publishDate;   // "yyyy-MM-ddTHH:mm:ss.000Z"
        public string section;       // "A","B","C", ...
        public int page;             // section page number
        public string folio;         // used as template (after mapping)
        public string folioText;     // from FolioJsonHandler.GetFolioText(...)
        public string folioTextSize; // from FolioJsonHandler.GetFolioText(...), e.g. "small"
    }

    public struct Ad
    {
        public string orderNumber;
        public string file;
        public string imageFile;
        public string url;
        public float x;      // mm
        public float y;      // mm
        public float width;  // mm
        public float height; // mm
        public string bookingCode;
        public bool unpaid;
        public string customer;
        public string adReady; // "1"/"0" or "true"/"false"
        public string comment;
    }

    public struct Divider
    {
        public float x;   // mm
        public float y;   // mm
        public float x2;  // mm
        public float y2;  // mm
    }

    public struct Header
    {
        public string file;
        public string imageFile;
        public float x;      // mm
        public float y;      // mm
        public float width;  // mm
        public float height; // mm
    }

    public struct Filler
    {
        public string file;
        public string imageFile;
        public float x;      // mm
        public float y;      // mm
        public float width;  // mm
        public float height; // mm
    }

    // ---------- PUBLIC API ----------

    // Build directly from a PDL file path (calls LoadPDL internally)
    public static string BuildPageJson(
        string pdlFile,
        string pdfBaseUrl,
        string previewBaseUrl,
        string name = "jfm-ad-ingest")
    {
        Pdl pdl;
        List<Ad> ads;
        List<Divider> dividers;
        List<Header> headers;
        List<Filler> fillers;

        LoadPDL(pdlFile, out pdl, out ads, out dividers, out headers, out fillers);
        return BuildPageJson(pdl, ads, dividers, headers, fillers, pdfBaseUrl, previewBaseUrl, name);
    }

    // Build when you already have the parsed PDL data
    public static string BuildPageJson(
        Pdl pdl,
        IEnumerable<Ad> ads,
        IEnumerable<Divider> dividers,
        IEnumerable<Header> headers,
        IEnumerable<Filler> fillers,
        string pdfBaseUrl,
        string previewBaseUrl,
        string name = "jfm-ad-ingest")
    {
        string productName = pdl.zone ?? "";
        string datePart = (pdl.publishDate ?? "").Length >= 10 ? pdl.publishDate.Substring(0, 10) : "";
        string editionName = (productName ?? "").ToLowerInvariant() + "-" + datePart;
        string editionTitle = productName + " " + datePart;

        string pageId = (pdl.section ?? "") + pdl.page.ToString(CultureInfo.InvariantCulture);
        string template = string.IsNullOrEmpty(pdl.folio) ? "MPP" : pdl.folio;

        var sb = new StringBuilder();

        using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture))
        using (var jw = new JsonTextWriter(sw))
        {
            jw.Formatting = Newtonsoft.Json.Formatting.Indented;
            jw.Culture = CultureInfo.InvariantCulture;

            jw.WriteStartObject();

            jw.WritePropertyName("name"); jw.WriteValue(name);
            jw.WritePropertyName("productName"); jw.WriteValue(productName);
            jw.WritePropertyName("editionName"); jw.WriteValue(editionName);
            jw.WritePropertyName("editionTitle"); jw.WriteValue(editionTitle);
            jw.WritePropertyName("publishDate"); jw.WriteValue(pdl.publishDate);

            // page
            jw.WritePropertyName("page");
            jw.WriteStartObject();
            jw.WritePropertyName("id"); jw.WriteValue(pageId);
            jw.WritePropertyName("template"); jw.WriteValue(template);

            // properties (after template) — from PDL folioText & folioTextSize
            jw.WritePropertyName("properties");
            jw.WriteStartObject();
            jw.WritePropertyName("folioText"); jw.WriteValue(pdl.folioText ?? "");
            jw.WritePropertyName("folioTextSize"); jw.WriteValue(string.IsNullOrEmpty(pdl.folioTextSize) ? "small" : pdl.folioTextSize);
            jw.WriteEndObject();

            // objects
            jw.WritePropertyName("objects");
            jw.WriteStartObject();

            // Ads
            jw.WritePropertyName("ads");
            jw.WriteStartArray();
            if (ads != null)
            {
                foreach (var ad in ads)
                {
                    var id = MakeIdFromAd(ad);
                    var pdfUrl = CombineUrl(pdfBaseUrl, ChangeExtension(id, ".pdf"));
                    var previewUrl = CombineUrl(previewBaseUrl, ChangeExtension(id, ".jpg"));
                    var readyBool = ToBool(ad.adReady);

                    jw.WriteStartObject();
                    jw.WritePropertyName("id"); jw.WriteValue(id);
                    jw.WritePropertyName("title"); jw.WriteValue(ad.customer ?? "");
                    jw.WritePropertyName("pdfUrl"); jw.WriteValue(pdfUrl);
                    jw.WritePropertyName("previewUrl"); jw.WriteValue(previewUrl);
                    jw.WritePropertyName("ready"); jw.WriteValue(readyBool);

                    jw.WritePropertyName("placement");
                    jw.WriteStartObject();
                    jw.WritePropertyName("x"); jw.WriteValue(ad.x);
                    jw.WritePropertyName("y"); jw.WriteValue(ad.y);
                    jw.WritePropertyName("width"); jw.WriteValue(ad.width);
                    jw.WritePropertyName("height"); jw.WriteValue(ad.height);
                    jw.WriteEndObject();

                    jw.WritePropertyName("properties");
                    jw.WriteStartObject();
                    jw.WritePropertyName("bookingCode"); jw.WriteValue(ad.bookingCode ?? "");
                    jw.WritePropertyName("comment"); jw.WriteValue(ad.comment ?? "");
                    jw.WritePropertyName("adReady"); jw.WriteValue(readyBool);
                    jw.WriteEndObject();

                    jw.WriteEndObject();
                }
            }
            jw.WriteEndArray(); // ads

            // Dividers
            jw.WritePropertyName("dividers"); // Dividers
            jw.WriteStartArray();
            if (dividers != null)
            {
                foreach (var d in dividers)
                {
                    jw.WriteStartObject();
                    jw.WritePropertyName("x"); jw.WriteValue(d.x);
                    jw.WritePropertyName("y"); jw.WriteValue(d.y);
                    jw.WritePropertyName("x2"); jw.WriteValue(d.x2);
                    jw.WritePropertyName("y2"); jw.WriteValue(d.y2);
                    jw.WriteEndObject();
                }
            }
            jw.WriteEndArray();

            // Headers
            jw.WritePropertyName("headers"); // Headers
            jw.WriteStartArray();
            if (headers != null)
            {
                foreach (var h in headers)
                {
                    jw.WriteStartObject();
                    jw.WritePropertyName("file"); jw.WriteValue(h.file ?? "");
                    jw.WritePropertyName("imageFile"); jw.WriteValue(h.imageFile ?? "");
                    jw.WritePropertyName("x"); jw.WriteValue(h.x);
                    jw.WritePropertyName("y"); jw.WriteValue(h.y);
                    jw.WritePropertyName("width"); jw.WriteValue(h.width);
                    jw.WritePropertyName("height"); jw.WriteValue(h.height);
                    jw.WriteEndObject();
                }
            }
            jw.WriteEndArray();

            // Fillers
            jw.WritePropertyName("fillers"); // Fillers
            jw.WriteStartArray();
            if (fillers != null)
            {
                foreach (var f in fillers)
                {
                    jw.WriteStartObject();
                    jw.WritePropertyName("file"); jw.WriteValue(f.file ?? "");
                    jw.WritePropertyName("imageFile"); jw.WriteValue(f.imageFile ?? "");
                    jw.WritePropertyName("x"); jw.WriteValue(f.x);
                    jw.WritePropertyName("y"); jw.WriteValue(f.y);
                    jw.WritePropertyName("width"); jw.WriteValue(f.width);
                    jw.WritePropertyName("height"); jw.WriteValue(f.height);
                    jw.WriteEndObject();
                }
            }
            jw.WriteEndArray();

            jw.WriteEndObject(); // objects
            jw.WriteEndObject(); // page

            // config
            jw.WritePropertyName("config");
            jw.WriteStartObject();
            jw.WritePropertyName("adItemSchemaName"); jw.WriteValue("print-ad-item");
            jw.WritePropertyName("pdfItemSchemaName"); jw.WriteValue("print-pdf-item");
            jw.WritePropertyName("allowTemplateUpdate"); jw.WriteValue(true);
            jw.WriteEndObject();

            jw.WriteEndObject(); // root
            jw.Flush();
        }

        return sb.ToString();
    }

    // ---------- PDL loader (ads + dividers + headers + fillers, pt -> mm) ----------
    public static void LoadPDL(
        string pdlFile,
        out Pdl pdl,
        out List<Ad> ads,
        out List<Divider> dividers,
        out List<Header> headers,
        out List<Filler> fillers)
    {
        var lines = new List<string>();
        using (var reader = new StreamReader(pdlFile, Encoding.GetEncoding("windows-1252")))
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null) lines.Add(line);
            }
        }
        if (lines.Count == 0) throw new InvalidOperationException("PDL file is empty.");

        // Header line (tab-separated)
        var header = lines[0].Split('\t');
        string dateRaw = GetField(header, "I");     // yyyymmdd
        string rawFolioKey = GetField(header, "T");     // folio key
        string zone = GetField(header, "ZONE");  // product code

        var year = int.Parse(dateRaw.Substring(0, 4), CultureInfo.InvariantCulture);
        var month = int.Parse(dateRaw.Substring(4, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(dateRaw.Substring(6, 2), CultureInfo.InvariantCulture);
        var d = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        string publishDateIso = d.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'", CultureInfo.InvariantCulture);

        // Section/page derived from filename (same as your earlier convention)
        var fileOnly = Path.GetFileName(pdlFile) ?? "";
        string section = fileOnly.Length >= 11 ? fileOnly.Substring(10, 1) : "";
        int page = 0;
        if (fileOnly.Length >= 14)
        {
            int.TryParse(fileOnly.Substring(11, 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out page);
        }

        // Folio mapping + folio text/size
        string mappedFolio = rawFolioKey;
        string folioText = "";
        string folioTextSize = "small";
        try
        {
            mappedFolio = FolioJsonHandler.GetFolioMapping(zone, rawFolioKey);
            var ft = FolioJsonHandler.GetFolioText(rawFolioKey); // [text, size]
            if (ft != null && ft.Count > 0) folioText = ft[0];
            if (ft != null && ft.Count > 1) folioTextSize = ft[1];
        }
        catch
        {
            mappedFolio = string.IsNullOrEmpty(rawFolioKey) ? "MPP" : rawFolioKey;
            folioText = "";
            folioTextSize = "small";
        }

        pdl = new Pdl
        {
            zone = zone,
            publishDate = publishDateIso,
            section = section,
            page = page,
            folio = string.IsNullOrEmpty(mappedFolio) ? "MPP" : mappedFolio,
            folioText = folioText ?? "",
            folioTextSize = string.IsNullOrEmpty(folioTextSize) ? "small" : folioTextSize
        };

        // Remove header to iterate content
        if (lines.Count > 0) lines.RemoveAt(0);

        ads = new List<Ad>();
        dividers = new List<Divider>();
        headers = new List<Header>();
        fillers = new List<Filler>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('\t');
            if (parts.Length < 6) continue;

            var typeVal = GetField(parts, "Type");
            var typeToken = (typeVal ?? "").Split(',')[0].Trim().ToUpperInvariant();

            // common reads
            float xPt = ParseFloat(GetField(parts, "X"));
            float yPt = ParseFloat(GetField(parts, "Y"));
            float dXP = ParseFloat(GetField(parts, "dX"));
            float dYP = ParseFloat(GetField(parts, "dY"));

            // convert to mm
            float xMm = (float)(xPt * PtToMm);
            float yMm = (float)(yPt * PtToMm);
            float wMm = (float)(dXP * PtToMm);
            float hMm = (float)(dYP * PtToMm);

            if (typeToken == "EPS" || typeToken == "PDF")
            {
                // ----- Ads -----
                var filePath = GetField(parts, "File");
                var fileName = Path.GetFileName(filePath ?? "");
                var orderNumberBase = Path.GetFileNameWithoutExtension(fileName ?? "");

                var str = GetField(parts, "String") ?? "";
                var seg = str.Split(',');
                string bookingCode = seg.Length > 1 ? seg[1] : "";
                string customer = seg.Length > 2 ? seg[2] : "";
                string comment = seg.Length > 5 ? seg[5] : "";

                var adReady = GetField(parts, "Class"); // "1"/"0"
                bool unpaid = false;

                ads.Add(new Ad
                {
                    orderNumber = orderNumberBase + ".PDF",
                    file = (fileName ?? "").Replace(".EPS", ".PDF"),
                    imageFile = (orderNumberBase ?? "") + ".jpg",
                    url = "",
                    x = xMm,
                    y = yMm,
                    width = wMm,
                    height = hMm,
                    bookingCode = bookingCode,
                    unpaid = unpaid,
                    customer = customer,
                    adReady = adReady,
                    comment = comment
                });
            }
            else if (typeToken == "LINES")
            {
                // ----- Dividers -----
                dividers.Add(new Divider
                {
                    x = xMm,
                    y = yMm,
                    x2 = wMm,  // matches your previous mapping (dX -> x2, dY -> y2)
                    y2 = hMm
                });
            }
            else if (typeToken == "HEADLINE")
            {
                // ----- Headers -----
                var filePath = GetField(parts, "File");
                var fileName = Path.GetFileName(filePath ?? "");
                var baseName = Path.GetFileNameWithoutExtension(fileName ?? "");

                headers.Add(new Header
                {
                    file = (fileName ?? "").Replace(".EPS", ".PDF"),
                    imageFile = (baseName ?? "") + ".jpg",
                    x = xMm,
                    y = yMm,
                    width = wMm,
                    height = hMm
                });
            }
            else if (typeToken == "FILLER")
            {
                // ----- Fillers -----
                var filePath = GetField(parts, "File");
                var fileName = Path.GetFileName(filePath ?? "");
                var baseName = Path.GetFileNameWithoutExtension(fileName ?? "");

                fillers.Add(new Filler
                {
                    file = (fileName ?? "").Replace(".EPS", ".PDF"),
                    imageFile = (baseName ?? "") + ".jpg",
                    x = xMm,
                    y = yMm,
                    width = wMm,
                    height = hMm
                });
            }
        }
    }

    // ---------- helpers ----------

    private static string GetField(string[] parts, string key)
    {
        foreach (var p in parts)
        {
            int idx = p.IndexOf('=');
            if (idx <= 0) continue;
            var k = p.Substring(0, idx).Trim();
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                return p.Substring(idx + 1).Trim();
        }
        return null;
    }

    private static float ParseFloat(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        float v;
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
    }

    private static string MakeIdFromAd(Ad ad)
    {
        var fname = Path.GetFileName(ad.file ?? "");
        if (!string.IsNullOrEmpty(fname)) return fname;
        if (!string.IsNullOrEmpty(ad.orderNumber)) return ad.orderNumber;
        return Guid.NewGuid().ToString("N").ToUpperInvariant() + ".PDF";
    }

    private static string ChangeExtension(string filename, string newExt)
    {
        if (string.IsNullOrEmpty(filename)) return "";
        var without = Path.GetFileNameWithoutExtension(filename);
        return string.IsNullOrEmpty(without) ? filename : without + newExt;
    }

    private static string CombineUrl(string baseUrl, string tail)
    {
        if (string.IsNullOrEmpty(baseUrl)) return tail ?? "";
        if (string.IsNullOrEmpty(tail)) return baseUrl;
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return baseUrl + tail.TrimStart('/');
    }

    private static bool ToBool(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)) return true;
        bool b;
        return bool.TryParse(value, out b) ? b : false;
    }
}
