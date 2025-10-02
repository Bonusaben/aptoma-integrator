using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Aptoma_Publication_Integrator
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public static class IngestBuilder
    {
        public static string LoadXML(string xmlPath)
        {
            XDocument doc = XDocument.Load(xmlPath);
            XNamespace ns = "http://www.w3.org/1999/xhtml";

            // Header
            var header = doc.Root.Element(ns + "Header");
            string product = (string)header.Element(ns + "Zone") ?? "";
            string dateRaw = (string)header.Element(ns + "Date") ?? ""; // ddMMyy

            // ddMMyy -> yyyy-MM-ddTHH:mm:ss.000Z
            int dd = int.Parse(dateRaw.Substring(0, 2), CultureInfo.InvariantCulture);
            int MM = int.Parse(dateRaw.Substring(2, 2), CultureInfo.InvariantCulture);
            int yy = int.Parse(dateRaw.Substring(4, 2), CultureInfo.InvariantCulture);
            int yyyy = yy <= 68 ? 2000 + yy : 1900 + yy; // adjust if needed
            var pub = new DateTime(yyyy, MM, dd, 0, 0, 0, DateTimeKind.Utc);

            string publishDateIso = pub.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'", CultureInfo.InvariantCulture);
            string editionName = $"{product.ToLowerInvariant()}-{pub:yyyy-MM-dd}";
            string editionTitle = $"{product} {pub:yyyy-MM-dd}";

            // Pages: ID = PhysicalBook + BookPageNumber (e.g., "A1", "B7", "C3")
            var rawPages = doc.Descendants(ns + "Page")
                .Select(p => new
                {
                    Section = ((string)p.Element(ns + "PhysicalBook")) ?? "",
                    PageNo = (int?)p.Element(ns + "BookPageNumber") ?? 0
                })
                .Where(x => !string.IsNullOrEmpty(x.Section) && x.PageNo > 0)
                .ToList();

            var pages = rawPages
                .Select(x => new Page
                {
                    Id = x.Section + x.PageNo.ToString(CultureInfo.InvariantCulture),
                    Template = "MPP"
                })
                .OrderBy(pg => pg.Id.Substring(0, 1), StringComparer.OrdinalIgnoreCase) // section letter
                .ThenBy(pg =>
                {
                    int num;
                    return int.TryParse(pg.Id.Length > 1 ? pg.Id.Substring(1) : "0", out num) ? num : int.MaxValue;
                })
                .ToList();

            // Sections: include ALL distinct sections found, with their first page (lowest page number)
            var sections = rawPages
                .GroupBy(x => x.Section, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var firstPageNo = g.Min(x => x.PageNo);
                    return new Section
                    {
                        Prefix = g.Key + "-",
                        FirstPageId = g.Key + firstPageNo.ToString(CultureInfo.InvariantCulture)
                    };
                })
                .OrderBy(s => s.Prefix, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new IngestDoc
            {
                Name = "jfm-ad-ingest",
                ProductName = product,
                EditionName = editionName,
                EditionTitle = editionTitle,
                PublishDate = publishDateIso,
                Pages = pages,
                Sections = sections,
                Config = new Config
                {
                    AdItemSchemaName = "print-ad-item",
                    PdfItemSchemaName = "print-pdf-item",
                    AllowTemplateUpdate = true
                }
            };

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            return JsonConvert.SerializeObject(result, settings);
        }

        // DTOs (PascalCase here; camelCase via resolver)
        private class IngestDoc
        {
            public string Name { get; set; }
            public string ProductName { get; set; }
            public string EditionName { get; set; }
            public string EditionTitle { get; set; }
            public string PublishDate { get; set; }
            public List<Page> Pages { get; set; }
            public List<Section> Sections { get; set; }
            public Config Config { get; set; }
        }

        public class Page
        {
            public string Id { get; set; }       // "A1", "B2", "C3"
            public string Template { get; set; } // "MPP"
        }

        public class Section
        {
            public string Prefix { get; set; }      // "A-", "B-", "C-"
            public string FirstPageId { get; set; } // "A1", "B1", "C1"
        }

        public class Config
        {
            public string AdItemSchemaName { get; set; }
            public string PdfItemSchemaName { get; set; }
            public bool AllowTemplateUpdate { get; set; }
        }
    }

        /*
        public static class IngestBuilder
        {
            public static string LoadXML(string xmlPath)
            {
                XDocument doc = XDocument.Load(xmlPath);
                XNamespace ns = "http://www.w3.org/1999/xhtml";

                var header = doc.Root.Element(ns + "Header");
                string product = (string)header.Element(ns + "Zone") ?? "";
                string dateRaw = (string)header.Element(ns + "Date") ?? ""; // ddMMyy

                int dd = int.Parse(dateRaw.Substring(0, 2), CultureInfo.InvariantCulture);
                int MM = int.Parse(dateRaw.Substring(2, 2), CultureInfo.InvariantCulture);
                int yy = int.Parse(dateRaw.Substring(4, 2), CultureInfo.InvariantCulture);
                int yyyy = yy <= 68 ? 2000 + yy : 1900 + yy;
                var pub = new DateTime(yyyy, MM, dd, 0, 0, 0, DateTimeKind.Utc);

                string publishDateIso = pub.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'", CultureInfo.InvariantCulture);
                string editionName = $"{product.ToLowerInvariant()}-{pub:yyyy-MM-dd}";
                string editionTitle = $"{product} {pub:yyyy-MM-dd}";

                var pages = doc.Descendants(ns + "Page")
                    .Select(p => new Page
                    {
                        Id = ((int?)p.Element(ns + "RunningPage"))?.ToString(CultureInfo.InvariantCulture)
                                ?? ((int?)p.Element(ns + "BookPageNumber"))?.ToString(CultureInfo.InvariantCulture)
                                ?? "0",
                        Template = "MPP"
                    })
                    .OrderBy(pg => int.TryParse(pg.Id, out var n) ? n : int.MaxValue)
                    .ToList();

                var sections = new List<Section>();
                var firstA = doc.Descendants(ns + "Page").FirstOrDefault(x => ((string)x.Element(ns + "PhysicalBook")) == "A");
                var firstB = doc.Descendants(ns + "Page").FirstOrDefault(x => ((string)x.Element(ns + "PhysicalBook")) == "B");
                if (firstA != null) sections.Add(new Section { Prefix = "A-", FirstPageId = ((int?)firstA.Element(ns + "RunningPage"))?.ToString() ?? "1" });
                if (firstB != null) sections.Add(new Section { Prefix = "B-", FirstPageId = ((int?)firstB.Element(ns + "RunningPage"))?.ToString() ?? "1" });

                var result = new IngestDoc
                {
                    Name = "jfm-ad-ingest",
                    ProductName = product,
                    EditionName = editionName,
                    EditionTitle = editionTitle,
                    PublishDate = publishDateIso,
                    Pages = pages,
                    Sections = sections,
                    Config = new Config
                    {
                        AdItemSchemaName = "print-ad-item",
                        PdfItemSchemaName = "print-pdf-item",
                        AllowTemplateUpdate = true
                    }
                };

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    },
                    NullValueHandling = NullValueHandling.Ignore,
                    //Formatting = Formatting.Indented
                };

                return JsonConvert.SerializeObject(result, settings);
            }

            // DTOs (PascalCase in C#, camelCase in JSON via resolver)
            private class IngestDoc
            {
                public string Name { get; set; }
                public string ProductName { get; set; }
                public string EditionName { get; set; }
                public string EditionTitle { get; set; }
                public string PublishDate { get; set; }
                public List<Page> Pages { get; set; }
                public List<Section> Sections { get; set; }
                public Config Config { get; set; }
            }

            public class Page
            {
                public string Id { get; set; }
                public string Template { get; set; }
            }

            public class Section
            {
                public string Prefix { get; set; }
                public string FirstPageId { get; set; }
            }

            public class Config
            {
                public string AdItemSchemaName { get; set; }
                public string PdfItemSchemaName { get; set; }
                public bool AllowTemplateUpdate { get; set; }
            }
        }
        */

}
