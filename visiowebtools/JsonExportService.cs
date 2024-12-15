using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text.Json;
using System.Collections.Generic;

namespace VisioWebTools
{
    /// <summary>
    /// Service for generating random readable strings with specific constraints.
    /// </summary>
    public static class JsonExportService
    {
        public static void ProcessPage(PackagePart pagePart, JsonExportOptions options, PageInfo pageInfo)
        {
            var pageStream = pagePart.GetStream(FileMode.Open);
            var xmlPage = XDocument.Load(pageStream);

            var xmlShapes = xmlPage.XPathSelectElements("/v:PageContents//v:Shape", VisioParser.NamespaceManager).ToList();
            foreach (var xmlShape in xmlShapes)
            {
                var shapeId = int.Parse(xmlShape.Attribute("ID").Value);

                pageInfo.Shapes ??= [];
                if (!pageInfo.Shapes.TryGetValue(shapeId, out var shapeInfo))
                {
                    shapeInfo = new ShapeInfo();
                    pageInfo.Shapes.Add(shapeId, shapeInfo);
                }

                if (options.IncludeShapeText)
                    ProcessShapeText(xmlShape, shapeInfo);

                if (options.IncludeShapeFields)
                    GetShapeFields(xmlShape, () => shapeInfo.FieldRows ??= []);

                if (options.IncludePropertyRows)
                    GetPropertyRows(xmlShape, () => shapeInfo.PropRows ??= []);

                if (options.IncludeUserRows)
                    GetUserRows(xmlShape, () => pageInfo.UserRows ??= []);
            }
        }

        private static void ProcessShapeText(XElement xmlShape, ShapeInfo shapeInfo)
        {
            var xmlText = xmlShape.XPathSelectElement("v:Text", VisioParser.NamespaceManager);
            if (xmlText == null)
                return;

            var text = TranslateFormattedTextService.GetShapeText(xmlText);
            shapeInfo.Text = text.PlainText;
        }

        private static void GetShapeFields(XElement xmlShape, Func<Dictionary<string, FieldInfo>> getShapeInfos)
        {
            var xmlRows = xmlShape.XPathSelectElements("v:Section[@N='Field']/v:Row", VisioParser.NamespaceManager).ToList();
            foreach (var xmlRow in xmlRows)
            {
                var fieldInfo = DiagramInfoService.EnsureCollection(xmlRow, getShapeInfos);

                var xmlValue = xmlRow.XPathSelectElement("v:Cell[@N='Value' and @U='STR']", VisioParser.NamespaceManager);
                fieldInfo.Value = xmlValue?.Attribute("V")?.Value;
            }
        }

        private static void GetUserRows(XElement xmlShape, Func<Dictionary<string, UserRowInfo>> getUserRows)
        {
            var xmlRows = xmlShape.XPathSelectElements("v:Section[@N='User']/v:Row", VisioParser.NamespaceManager).ToList();
            foreach (var xmlRow in xmlRows)
            {
                var userRowInfo = DiagramInfoService.EnsureCollection(xmlRow, getUserRows);

                var xmlValue = xmlRow.XPathSelectElement("v:Cell[@N='Value']", VisioParser.NamespaceManager);
                userRowInfo.Value = xmlValue?.Attribute("V")?.Value;

                var xmlPrompt = xmlRow.XPathSelectElement("v:Cell[@N='Prompt']", VisioParser.NamespaceManager);
                userRowInfo.Prompt = xmlPrompt?.Attribute("V")?.Value;
            }
        }

        private static void GetPropertyRows(XElement xmlShape, Func<Dictionary<string, PropertyInfo>> getShapeInfo)
        {
            var xmlRows = xmlShape.XPathSelectElements("v:Section[@N='Property']/v:Row", VisioParser.NamespaceManager).ToList();
            foreach (var xmlRow in xmlRows)
            {
                var propertyInfo = DiagramInfoService.EnsureCollection(xmlRow, getShapeInfo);

                var xmlType = xmlRow.XPathSelectElement("v:Cell[@N='Type']", VisioParser.NamespaceManager);
                propertyInfo.Type = xmlType?.Attribute("V")?.Value;

                var xmlValue = xmlRow.XPathSelectElement("v:Cell[@N='Value']", VisioParser.NamespaceManager);
                propertyInfo.Value = xmlValue?.Attribute("V")?.Value;

                var xmlFormat = xmlRow.XPathSelectElement("v:Cell[@N='Format']", VisioParser.NamespaceManager);
                propertyInfo.Format = xmlFormat?.Attribute("V")?.Value;

                var xmlPrompt = xmlRow.XPathSelectElement("v:Cell[@N='Prompt']", VisioParser.NamespaceManager);
                propertyInfo.Prompt = xmlPrompt?.Attribute("V")?.Value;

                var xmlLabel = xmlRow.XPathSelectElement("v:Cell[@N='Label']", VisioParser.NamespaceManager);
                propertyInfo.Label = xmlLabel?.Attribute("V")?.Value;
            }
        }

        public static void GetDocProps(Package package, DocumentInfo documentInfo)
        {
            var corePropsRel = package.GetRelationshipsByType("http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties").First();
            Uri corePropsUri = PackUriHelper.ResolvePartUri(new Uri("/", UriKind.Relative), corePropsRel.TargetUri);
            var corePropsPart = package.GetPart(corePropsUri);

            var pagesStream = corePropsPart.GetStream(FileMode.Open);
            var xmlCoreProps = XDocument.Load(pagesStream);

            var xmlCreator = xmlCoreProps.XPathSelectElements("/cp:coreProperties/dc:creator", VisioParser.NamespaceManager).FirstOrDefault();
            documentInfo.Creator = xmlCreator?.Value;

            var xmlTitle = xmlCoreProps.XPathSelectElements("/cp:coreProperties/dc:title", VisioParser.NamespaceManager).FirstOrDefault();
            documentInfo.Title = xmlTitle?.Value;
        }

        public static void ProcessPages(Stream stream, JsonExportOptions options, DocumentInfo documentInfo)
        {
            using (Package package = Package.Open(stream, FileMode.Open))
            {
                if (options.IncludeDocumentProperties)
                    GetDocProps(package, documentInfo);

                var documentRel = package.GetRelationshipsByType("http://schemas.microsoft.com/visio/2010/relationships/document").First();
                Uri docUri = PackUriHelper.ResolvePartUri(new Uri("/", UriKind.Relative), documentRel.TargetUri);
                var documentPart = package.GetPart(docUri);

                var pagesRel = documentPart.GetRelationshipsByType("http://schemas.microsoft.com/visio/2010/relationships/pages").First();
                Uri pagesUri = PackUriHelper.ResolvePartUri(documentPart.Uri, pagesRel.TargetUri);
                var pagesPart = package.GetPart(pagesUri);

                var pagesStream = pagesPart.GetStream(FileMode.Open);
                var xmlPages = XDocument.Load(pagesStream);

                var pageRels = pagesPart.GetRelationshipsByType("http://schemas.microsoft.com/visio/2010/relationships/page").ToList();
                foreach (var pageRel in pageRels)
                {
                    var xmlPage = xmlPages.XPathSelectElement($"/v:Pages/v:Page[v:Rel/@r:id='{pageRel.Id}']", VisioParser.NamespaceManager);

                    var pageId = int.Parse(xmlPage.Attribute("ID").Value);
                    documentInfo.Pages ??= [];
                    if (!documentInfo.Pages.TryGetValue(pageId, out var pageInfo))
                    {
                        pageInfo = new();
                        documentInfo.Pages.Add(pageId, pageInfo);
                    }

                    if (options.IncludeUserRows)
                    {
                        var pageSheet = xmlPage.XPathSelectElement("/v:PageSheet", VisioParser.NamespaceManager);
                        GetUserRows(xmlPage, () => pageInfo.UserRows ??= []);
                    }

                    var attributeName = xmlPage.Attribute("Name");
                    pageInfo.Name = attributeName?.Value;

                    Uri pageUri = PackUriHelper.ResolvePartUri(pagesPart.Uri, pageRel.TargetUri);
                    var pagePart = package.GetPart(pageUri);
                    ProcessPage(pagePart, options, pageInfo);
                }
            }
        }

        public static string Process(byte[] input, JsonExportOptions options)
        {
            using (var stream = new MemoryStream(input))
            {
                var documentInfo = new DocumentInfo();
                ProcessPages(stream, options, documentInfo);
                var json = JsonSerializer.Serialize(documentInfo, DocumentInfoJsonContext.Context.DocumentInfo);
                return json;
            }
        }
    }
}