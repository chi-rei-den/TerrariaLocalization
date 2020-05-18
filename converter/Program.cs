using Karambolo.PO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Chireiden.Terraria.Converter
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                ExportJson(args[0]);
                return;
            }

            var list = new List<Element>();
            Console.Write("Source language folder: ");
            var folder = Console.ReadLine();
            foreach (var item in Directory.GetFiles(folder))
            {
                var fileName = Path.GetFileNameWithoutExtension(item);
                if (fileName.Contains("."))
                {
                    fileName = Path.GetExtension(fileName).Trim('.');
                }

                foreach (var node in JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(item)))
                {
                    foreach (var kvp in node.Value)
                    {
                        list.MergeEntry(new Element
                        {
                            FileName = fileName,
                            NodeName = node.Key,
                            KeyName = kvp.Key,
                            Source = kvp.Value
                        });
                    }
                }
            }

            Console.Write("Target language folder: ");
            folder = Console.ReadLine();
            foreach (var item in Directory.GetFiles(folder))
            {
                var fileName = Path.GetFileNameWithoutExtension(item);
                if (fileName.Contains("."))
                {
                    fileName = Path.GetExtension(fileName).Trim('.');
                }

                foreach (var node in JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(item)))
                {
                    foreach (var kvp in node.Value)
                    {
                        list.MergeEntry(new Element
                        {
                            FileName = fileName,
                            NodeName = node.Key,
                            KeyName = kvp.Key,
                            Target = kvp.Value
                        });
                    }
                }
            }

            var catalog = new POCatalog
            {
                Encoding = "UTF-8"
            };

            foreach (var item in list)
            {
                catalog.AddEntry(item.Source, $"{item.FileName}.{item.NodeName}.{item.KeyName}", item.Target);
            }

            new POGenerator().Generate(File.OpenWrite("output.po"), catalog);
        }

        private static void ExportJson(string pofile)
        {
            var list = new List<Element>();
            var parseResult = new POParser().Parse(File.OpenRead(pofile));
            var catalog = parseResult.Catalog;
            foreach (var item in catalog)
            {
                var context = item.Key.ContextId.Split('.');
                list.Add(new Element
                {
                    FileName = context[0],
                    NodeName = context[1],
                    KeyName = context[2],
                    Target = catalog.GetTranslation(item.Key)
                });
            }

            var result = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            foreach (var fileGroup in list.GroupBy(i => i.FileName))
            {
                result[fileGroup.Key] = new Dictionary<string, Dictionary<string, string>>();
                foreach (var node in fileGroup.GroupBy(i => i.NodeName))
                {
                    result[fileGroup.Key][node.Key] = node.ToDictionary(i => i.KeyName, i => i.Target);
                }
            }

            Console.Write("Language: ");
            var lang = Console.ReadLine();

            foreach (var file in result)
            {
                File.WriteAllText($"Terraria.Localization.Content.{lang}.{file.Key}.json‎", JsonConvert.SerializeObject(file.Value, Formatting.Indented));
            }
        }

        private static void AddEntry(this POCatalog catalog, string source, string context, string translation)
        {
            if (source == "")
            {
                return;
            }

            catalog.Add(new POSingularEntry(new POKey(source, contextId: context))
            {
                Translation = translation,
                Comments = new List<POComment>()
            });
        }

        private static void MergeEntry(this List<Element> list, Element element)
        {
            foreach (var item in list)
            {
                if (item.FileName == element.FileName && item.NodeName == element.NodeName && item.KeyName == element.KeyName)
                {
                    if (!string.IsNullOrWhiteSpace(element.Source))
                    {
                        item.Source = element.Source;
                    }

                    if (!string.IsNullOrWhiteSpace(element.Target))
                    {
                        item.Target = element.Target;
                    }

                    break;
                }
            }

            list.Add(element);
        }
    }

    public class Element
    {
        public string FileName = "";
        public string NodeName = "";
        public string KeyName = "";
        public string Source = "";
        public string Target = "";
    }
}
