using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using EasyIO;

namespace Manhood
{
    public static class ManPacker
    {
        public static void PackToFile(string path, IEnumerable<Definition> defs, IEnumerable<WordList> vocab, IEnumerable<Pattern> patterns)
        {
            using(EasyWriter writer = new EasyWriter(path))
            {
                // Write defs
                writer.Write(ContentType.DefTable);
                writer.Write(defs.Count());
                foreach(var def in defs)
                {
                    writer.Write(def.Type);
                    writer.Write(def.Name);
                    writer.Write(def.Body);
                }

                // Write patterns
                foreach(var pat in patterns)
                {
                    writer.Write(ContentType.Pattern);
                    writer.Write(pat.Title);
                    writer.Write(pat.PatternText);
                }

                // Write dictionary
                foreach(var dict in vocab)
                {
                    writer.Write(ContentType.Vocabulary);
                    dict.WriteToStream(writer);
                }
            }
        }

        public static void ConvertPack(string oldPackPath, string newPackPath)
        {
            using(BinaryReader reader = new BinaryReader(File.Open(oldPackPath, FileMode.Open)))
            {
                reader.ReadUInt32(); // magic
                reader.ReadLongString();
                reader.ReadLongString();
                reader.ReadLongString();

                List<Definition> defs = new List<Definition>();

                if (reader.ReadBoolean()) // macros
                {
                    string[] raw = reader.ReadStringArray();
                    foreach(string entry in raw)
                    {
                        var match = Regex.Match(entry, @"(?<key>.+)::(?<value>.+)", RegexOptions.ExplicitCapture);
                        if (match.Success)
                        {
                            defs.Add(new Definition(DefinitionType.Macro, match.Groups["key"].Value, match.Groups["value"].Value));
                        }
                    }
                }

                if (reader.ReadBoolean()) // globals
                {
                    string[] raw = reader.ReadStringArray();
                    foreach (string entry in raw)
                    {
                        var match = Regex.Match(entry, @"(?<key>.+)::(?<value>.+)", RegexOptions.ExplicitCapture);
                        if (match.Success)
                        {
                            defs.Add(new Definition(DefinitionType.Global, match.Groups["key"].Value, match.Groups["value"].Value));
                        }
                    }
                }

                if (reader.ReadBoolean()) // outlines (ignore)
                {
                    if (reader.ReadBoolean())
                    {
                        reader.ReadStringArray();
                    }
                    if (reader.ReadBoolean())
                    {
                        reader.ReadStringArray();
                    }
                    if (reader.ReadBoolean())
                    {
                        reader.ReadStringArray();
                    }
                }

                List<WordList> vocab = new List<WordList>();

                if (reader.ReadBoolean()) // vocab
                {
                    int c = reader.ReadInt32();

                    for(int i = 0; i < c; i++)
                    {
                        reader.ReadLongString();
                        int bufferLength = reader.ReadInt32(); // data length - skipping because we can just load right off the stream
                        int bufferEnd = bufferLength + (int)reader.BaseStream.Position;
                        vocab.Add(WordList.LoadLegacyList(reader));                        
                        reader.BaseStream.Position = bufferEnd;
                    }
                }

                List<Pattern> pats = new List<Pattern>();

                if (reader.ReadBoolean()) // patterns
                {
                    int c = reader.ReadInt32();

                    for(int i = 0; i < c; i++)
                    {
                        reader.ReadLongString();

                        char ch = reader.ReadChar();
                        string symbol = ch.ToString();
                        string title = reader.ReadLongString();

                        pats.AddRange(reader.ReadStringArray().Select<string, Pattern>((str, index) => new Pattern(String.Format("{0}_{1:D4}", symbol, index), str)));

                    }
                }

                PackToFile(newPackPath, defs, vocab, pats);
            }
        }
    }
}
