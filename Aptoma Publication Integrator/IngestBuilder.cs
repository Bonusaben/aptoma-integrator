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

            // Pages: ID = PhysicalBook + BookPageNumber (e.g., "A1", "B7")
            var pages = doc.Descendants(ns + "Page")
                .Select(p => new
                {
                    Section = ((string)p.Element(ns + "PhysicalBook")) ?? "",
                    PageNo = (int?)p.Element(ns + "BookPageNumber") ?? 0
                })
                .Select(x => new Page
                {
                    Id = $"{x.Section}{x.PageNo}",
                    Template = "MPP"
                })
                .OrderBy(pg => pg.Id.Length > 0 ? pg.Id[0] : '\uffff') // section letter
                .ThenBy(pg =>
                {
                    // extract numeric tail for stable ordering (A1, A2, A10, ...)
                    var num = 0;
                    int i = 1;
                    while (i < pg.Id.Length && char.IsDigit(pg.Id[i])) i++;
                    int.TryParse(pg.Id.Substring(1, pg.Id.Length - 1), out num);
                    return num;
                })
                .ToList();

            // Sections: first A*, first B* (use new IDs)
            var sections = new List<Section>();
            var firstA = pages.FirstOrDefault(p => p.Id.StartsWith("A", StringComparison.OrdinalIgnoreCase));
            var firstB = pages.FirstOrDefault(p => p.Id.StartsWith("B", StringComparison.OrdinalIgnoreCase));
            if (firstA != null) sections.Add(new Section { Prefix = "A-", FirstPageId = firstA.Id });
            if (firstB != null) sections.Add(new Section { Prefix = "B-", FirstPageId = firstB.Id });

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
                Formatting = Formatting.Indented
            };

            return JsonConvert.SerializeObject(result, settings);
        }

        // DTOs (PascalCase in C#, camelCase via resolver)
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
            public string Id { get; set; }       // "A1", "B2"
            public string Template { get; set; } // "MPP"
        }

        public class Section
        {
            public string Prefix { get; set; }      // "A-", "B-"
            public string FirstPageId { get; set; } // "A1", "B1"
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
