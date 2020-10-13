using dnlib.DotNet;
using Karambolo.PO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Chireiden.Terraria.Converter
{
    internal static class Program
    {
        private static void Usage()
        {
            Console.WriteLine(@"Usage:
    extract - extract .json and .po file from Terraria
        [filePath] - path of Terraria.exe
        [languages] - optional
    repack - import .po file to Terraria
        [filePath] - path of Terraria.exe
        [language] - language to import
        [files] - path of .po file");
            Console.ReadLine();
        }

        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Usage();
                return;
            }

            switch (args[0])
            {
                case "extract":
                    ExtractCommand(args);
                    return;
                case "convert":
                    ConvertCommand(args);
                    return;
                case "repack":
                    RepackCommand(args);
                    return;
                case "tojson":
                    ToJsonCommand(args);
                    return;
            }
        }

        private static void ExtractCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            var filePath = args[1];
            var languages = args.Skip(2).ToList();
            var langFileDict = new List<(string Language, string FileName)>();
            var asm = AssemblyDef.Load(filePath);
            foreach (var item in asm.ManifestModule.Resources)
            {
                if (item.Name.StartsWith("Terraria.Localization.Content"))
                {
                    var matched = languages.Where(l => item.Name.Contains(l));
                    if (languages.Count == 0)
                    {
                        File.WriteAllBytes(item.Name, (item as EmbeddedResource)?.CreateReader().ReadRemainingBytes() ?? new byte[0]);
                    }
                    else if (matched.Any())
                    {
                        langFileDict.Add((matched.Single(), item.Name));
                        File.WriteAllBytes(item.Name, (item as EmbeddedResource)?.CreateReader().ReadRemainingBytes() ?? new byte[0]);
                    }
                }
            }

            var list = new List<Element>();

            if (languages.Count == 2)
            {
                foreach (var (Language, FileName) in langFileDict)
                {
                    Load(list, FileName, Language == languages[0]);
                }
            }

            var catalog = new POCatalog
            {
                Encoding = "UTF-8"
            };

            foreach (var item in list.OrderBy(i => i.FileName).ThenBy(i => i.NodeName).ThenBy(i => int.TryParse(i.KeyName, out var r) ? r.ToString("000000") : i.KeyName))
            {
                catalog.AddEntry(item.Source, $"{item.FileName}.{item.NodeName}.{item.KeyName}", item.Target);
            }

            new POGenerator().Generate(File.OpenWrite("output.po"), catalog);
        }

        private static void Load(List<Element> list, string path, bool source)
        {
            foreach (var node in JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path)))
            {
                foreach (var kvp in node.Value)
                {
                    var fileName = Path.GetExtension(Path.GetFileNameWithoutExtension(path)).Trim('.');
                    if (fileName.Contains("-"))
                    {
                        fileName = "Main";
                    }
                    else if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = Path.GetFileNameWithoutExtension(path);
                    }

                    list.MergeEntry(source ? new Element
                    {
                        FileName = fileName,
                        NodeName = node.Key,
                        KeyName = kvp.Key,
                        Source = kvp.Value
                    } : new Element
                    {
                        FileName = fileName,
                        NodeName = node.Key,
                        KeyName = kvp.Key,
                        Target = kvp.Value
                    });
                }
            }
        }

        private static void ConvertCommand(string[] args)
        {
            if (args.Length < 3)
            {
                Usage();
                return;
            }

            var srcLang = args[1];
            var tgtLang = args[2];
            var list = new List<Element>();

            foreach (var item in Directory.GetFiles(srcLang))
            {
                Load(list, item, true);
            }

            foreach (var item in Directory.GetFiles(tgtLang))
            {
                Load(list, item, false);
            }

            var catalog = new POCatalog
            {
                Encoding = "UTF-8"
            };

            foreach (var item in list.OrderBy(i => i.FileName).ThenBy(i => i.NodeName).ThenBy(i => int.TryParse(i.KeyName, out var r) ? r.ToString("000000") : i.KeyName))
            {
                catalog.AddEntry(item.Source, $"{item.FileName}.{item.NodeName}.{item.KeyName}", item.Target);
            }

            new POGenerator().Generate(File.OpenWrite("output.po"), catalog);
        }

        private static void RepackCommand(string[] args)
        {
            if (args.Length < 4)
            {
                Usage();
                return;
            }

            var filePath = args[1];
            var locFiles = args.Skip(2).ToList();
            var lang = locFiles[0];
            var pofile = locFiles[1];

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

            var asm = AssemblyDef.Load(filePath);
            foreach (var item in result)
            {
                var fileName = "." + item.Key;
                if (fileName == ".Main")
                {
                    fileName = "";
                }
                var res = asm.ManifestModule.Resources.Where(r => r.Name.Contains("zh-Hans" + fileName + ".json"));
                if (!res.Any() && string.IsNullOrEmpty(fileName))
                {
                    fileName = ".Main";
                }
                try
                {
                    var single = res.Single();
                    asm.ManifestModule.Resources.Remove(single);
                    asm.ManifestModule.Resources.Add(new EmbeddedResource($"Terraria.Localization.Content.zh-Hans{fileName}.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.Value, Formatting.Indented)), ManifestResourceAttributes.Public));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{fileName} fail: {e}");
                }
            }

            asm.Write(Path.Combine(Path.GetDirectoryName(filePath), "Terraria_locpatched.exe"));
        }

        private static void ToJsonCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            var locFiles = args.Skip(1).ToList();
            var pofile = locFiles[0];

            var list = new List<Element>();
            var parseResult = new POParser().Parse(File.OpenRead(pofile));
            var catalog = parseResult.Catalog;
            foreach (var item in catalog)
            {
                var context = item.Key.ContextId.Split('.');
                if (!string.IsNullOrWhiteSpace(catalog.GetTranslation(item.Key)))
                {
                    list.Add(new Element
                    {
                        FileName = context[0],
                        NodeName = context[1],
                        KeyName = context[2],
                        Target = catalog.GetTranslation(item.Key)
                    });
                }
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

            foreach (var item in result)
            {
                File.WriteAllBytes(item.Key + ".json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.Value, Formatting.Indented)));
            }
        }
    }

    public static class Helper
    {
        public static void AddEntry(this POCatalog catalog, string source, string context, string translation)
        {
            if (source == "")
            {
                return;
            }

            catalog.Add(new POSingularEntry(new POKey(source, contextId: context))
            {
                Translation = translation,
                Comments = new List<POComment> { new POExtractedComment { Text = context } }
            });
        }

        public static void MergeEntry(this List<Element> list, Element element)
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

                    return;
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
