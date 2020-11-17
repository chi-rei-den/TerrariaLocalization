using dnlib.DotNet;
using Karambolo.PO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;

namespace Chireiden.Terraria.Converter
{
    internal static class Program
    {
        private static void Usage()
        {
            Console.WriteLine(@"Usage:
    <input> -- <output>

    <input>
        One of following:
        asm <filePath> <bool:dumpAll> (languages..)
        json <srcLang> <dstLang>
        po <path>
    <output>
        One of following:
        asm <filePath> <dstLang> [bool:useMainReplacement=true]
        json <dstLang> [bool:useMainReplacement=true]
        po <path>

    Example:
        asm Terraria.exe true en-US en-CA -- json en true
            Read ""Terraria.exe"" and dump all language files.
            Load en-US as source language and en-CA (doesn't exist) as target language.
            Save target language into en.*.json (e.g. en.Items.json, en.NPCs.json)
            The en.json will be saved as en.Main.json to prevent satellite assembly generation
        json testLang targetLang -- po name.po
            Read all files ""testLang/*"" as json source language and ""targetLang/*"" as target.
            Save it to name.po
        po name.po -- asm Terraria.exe en-US
            Read ""name.po"" for source and target language.
            Read ""Terraria.exe"" and replace all en-US language file with content of target.
            If en-US.json does not exist in Terraria.exe, it will be saved as en-US.Main.json");
            Console.ReadLine();
        }

        private static void Main(string[] args)
        {
            if (args.Length < 1 || !args.Contains("--"))
            {
                Usage();
                return;
            }

            var splitIndex = args.ToList().IndexOf("--");
            var input = args.Take(splitIndex).ToList();
            var output = args.Skip(splitIndex + 1).ToList();

            if (input.Count < 1 || output.Count < 1)
            {
                Usage();
                return;
            }

            LangMapping mapping;
            switch (input[0])
            {
                case "asm":
                    if (input.Count < 3)
                    {
                        goto default;
                    }
                    mapping = LangMapping.FromAsm(input[1], bool.Parse(input[2]), input.Skip(3).ToList());
                    break;
                case "json":
                    if (input.Count < 3)
                    {
                        goto default;
                    }
                    mapping = LangMapping.FromJson(input[1], input[2]);
                    break;
                case "po":
                    if (input.Count != 2)
                    {
                        goto default;
                    }
                    mapping = LangMapping.FromPO(input[1]);
                    break;
                default:
                    Usage();
                    return;
            }

            switch (output[0])
            {
                case "asm":
                    if (output.Count < 3)
                    {
                        goto default;
                    }
                    mapping.ToAsm(output[1], output[2], bool.Parse(output.Count > 3 ? output[3] : "true"));
                    break;
                case "json":
                    if (output.Count < 2)
                    {
                        goto default;
                    }
                    mapping.ToJson(output[1], bool.Parse(output.Count > 2 ? output[2] : "true"));
                    break;
                case "po":
                    if (output.Count != 2)
                    {
                        goto default;
                    }
                    mapping.ToPO(output[1]);
                    break;
                default:
                    Usage();
                    return;
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
    }

    public class LangMapping
    {
        public List<Element> List = new List<Element>();

        public static LangMapping FromAsm(string path, bool dumpAll, List<string> languages)
        {
            var asm = AssemblyDef.Load(path);
            foreach (var item in asm.ManifestModule.Resources)
            {
                if (item.Name.StartsWith("Terraria.Localization.Content"))
                {
                    var lang = item.Name.String.Split('.')[3];
                    if (dumpAll || languages.Contains(lang))
                    {
                        if (!Directory.Exists(lang))
                        {
                            Directory.CreateDirectory(lang);
                        }
                        File.WriteAllBytes(Path.Combine(lang, item.Name), (item as EmbeddedResource)?.CreateReader().ReadRemainingBytes() ?? new byte[0]);
                    }
                }
            }

            if (languages.Count == 0)
            {
                languages.Add("en-US");
            }

            if (languages.Count == 1)
            {
                languages.Add("");
            }

            return FromJson(languages[0], languages[1]);
        }

        public static LangMapping FromJson(string srcLang, string dstLang)
        {
            var lang = new LangMapping();

            foreach (var item in Directory.GetFiles(srcLang))
            {
                lang.LoadJson(item, true);
            }

            if (Directory.Exists(dstLang))
            {
                foreach (var item in Directory.GetFiles(dstLang))
                {
                    lang.LoadJson(item, false);
                }
            }

            return lang;
        }

        public static LangMapping FromPO(string path)
        {
            var lang = new LangMapping();
            var parseResult = new POParser().Parse(File.OpenRead(path));
            var catalog = parseResult.Catalog;
            foreach (var item in catalog)
            {
                var context = item.Key.ContextId.Split('.');
                if (!string.IsNullOrWhiteSpace(catalog.GetTranslation(item.Key)))
                {
                    lang.List.Add(new Element
                    {
                        FileName = context[0],
                        NodeName = context[1],
                        KeyName = context[2],
                        Target = catalog.GetTranslation(item.Key)
                    });
                }
            }

            return lang;
        }

        public LangMapping ToAsm(string path, string dstLang, bool useMainReplacement = true)
        {
            var result = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            foreach (var fileGroup in this.List.GroupBy(i => i.FileName))
            {
                result[fileGroup.Key] = new Dictionary<string, Dictionary<string, string>>();
                foreach (var node in fileGroup.GroupBy(i => i.NodeName))
                {
                    result[fileGroup.Key][node.Key] = node.ToDictionary(i => i.KeyName, i => i.Target);
                }
            }

            var asm = AssemblyDef.Load(path);
            foreach (var item in result)
            {
                var fileName = "." + item.Key;
                if (fileName == ".Main")
                {
                    fileName = "";
                }

                var res = asm.ManifestModule.Resources.Where(r => r.Name.Contains(dstLang + fileName + ".json"));
                if (useMainReplacement && !res.Any() && string.IsNullOrEmpty(fileName))
                {
                    fileName = ".Main";
                }
                try
                {
                    try
                    {
                        var single = res.Single();
                        asm.ManifestModule.Resources.Remove(single);
                    }
                    catch
                    {
                    }
                    asm.ManifestModule.Resources.Add(new EmbeddedResource($"Terraria.Localization.Content.{dstLang}{fileName}.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.Value, Formatting.Indented)), ManifestResourceAttributes.Public));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{fileName} fail: {e}");
                }
            }

            asm.Write(Path.Combine(Path.GetDirectoryName(path), "Terraria_locpatched.exe"));

            return this;
        }

        public LangMapping ToJson(string dstLang, bool useMainReplacement = true)
        {
            var result = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            foreach (var fileGroup in this.List.GroupBy(i => i.FileName))
            {
                result[fileGroup.Key] = new Dictionary<string, Dictionary<string, string>>();
                foreach (var node in fileGroup.GroupBy(i => i.NodeName))
                {
                    result[fileGroup.Key][node.Key] = node.ToDictionary(i => i.KeyName, i => i.Target);
                }
            }

            if (string.IsNullOrWhiteSpace(dstLang))
            {
                foreach (var item in result)
                {
                    File.WriteAllBytes($"{item.Key}.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.Value, Formatting.Indented)));
                }
            }
            else if (useMainReplacement)
            {
                foreach (var item in result)
                {
                    if (item.Key == "Main")
                    {
                        File.WriteAllBytes($"Terraria.Localization.Content.{dstLang}.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.Value, Formatting.Indented)));
                    }
                    else
                    {
                        File.WriteAllBytes($"Terraria.Localization.Content.{dstLang}.{item.Key}.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.Value, Formatting.Indented)));
                    }
                }
            }
            else
            {
                foreach (var item in result)
                {
                    File.WriteAllBytes($"Terraria.Localization.Content.{dstLang}.{item.Key}.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.Value, Formatting.Indented)));
                }
            }

            return this;
        }

        public LangMapping ToPO(string path)
        {
            var catalog = new POCatalog
            {
                Encoding = "UTF-8"
            };

            foreach (var item in this.List.OrderBy(i => i.FileName).ThenBy(i => i.NodeName).ThenBy(i => int.TryParse(i.KeyName, out var r) ? r.ToString("000000") : i.KeyName))
            {
                catalog.AddEntry(item.Source, $"{item.FileName}.{item.NodeName}.{item.KeyName}", item.Target);
            }

            new POGenerator().Generate(File.OpenWrite(path), catalog);

            return this;
        }

        private void LoadJson(string path, bool isSource)
        {
            var fileName = "";
            var parts = Path.GetFileName(path).Split('.');
            if (parts.Length == 6)
            {
                // Terraria.Localization.Content.{lang}.{name}.json
                fileName = parts[4];
            }
            else if (parts.Length == 5)
            {
                // Terraria.Localization.Content.{lang}.json
                fileName = "Main";
            }
            else if (parts.Length > 3)
            {
                fileName = parts[parts.Length - 2];
            }
            else if (parts.Length > 1)
            {
                fileName = parts[0];
            }

            foreach (var node in JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path)))
            {
                foreach (var kvp in node.Value)
                {
                    this.MergeEntry(isSource ? new Element
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

        public void MergeEntry(Element element)
        {
            foreach (var item in this.List)
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

            this.List.Add(element);
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
