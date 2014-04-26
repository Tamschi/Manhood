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
    /// <summary>
    /// Contains methods for converting and creating pack files.
    /// </summary>
    public static class ManPacker
    {
        /// <summary>
        /// Writes Manhood content to a pack file.
        /// </summary>
        /// <param name="path">The path to the pack file to write.</param>
        /// <param name="defs">The definitions to pack.</param>
        /// <param name="vocab">The vocabulary to pack.</param>
        /// <param name="patterns">The patterns to pack.</param>
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
                    writer.Write(def.Parameters.ToArray());
                    writer.Write(def.Body);
                }

                // Write patterns
                foreach(var pat in patterns)
                {
                    writer.Write(ContentType.Pattern);
                    writer.Write(pat.Title);
                    writer.Write(pat.Body);                    
                }

                // Write dictionary
                foreach(var dict in vocab)
                {
                    writer.Write(ContentType.Vocabulary);
                    dict.WriteToStream(writer);
                }
            }
        }

        /// <summary>
        /// Unpacks a pack file and outputs the contents to the provided lists.
        /// </summary>
        /// <param name="path">The path to the pack file.</param>
        /// <param name="defs">The list to unpack definitions to.</param>
        /// <param name="vocab">The list to unpack vocabulary to.</param>
        /// <param name="patterns">The list to unpack patterns to.</param>
        public static void Unpack(string path, List<Definition> defs, List<WordList> vocab, List<Pattern> patterns)
        {
            using (EasyReader reader = new EasyReader(path))
            {
                ContentType type;
                while(!reader.EndOfStream)
                {
                    switch (type = (ContentType)reader.ReadByte())
                    {
                        case ContentType.DefTable:
                            {
                                int count = reader.ReadInt32();
                                for(int i = 0; i < count; i++)
                                {
                                    var defType = (DefinitionType)reader.ReadByte();
                                    string name = reader.ReadString();
                                    string[] parameters = reader.ReadStringArray();
                                    string body = reader.ReadString();
                                    if (Definition.IsValidName(name))
                                    {
                                        defs.Add(new Definition(defType, name, body, parameters.ToList()));
                                    }
                                }
                            }
                            break;
                        case ContentType.Pattern:
                            {
                                string title = reader.ReadString();
                                string body = reader.ReadString();
                                patterns.Add(new Pattern(title, body));
                            }
                            break;
                        case ContentType.Vocabulary:
                            {
                                vocab.Add(WordList.LoadModernList(reader));
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Converts a legacy-formatted .moist pack to the new format.
        /// </summary>
        /// <param name="oldPackPath">The path to the legacy pack.</param>
        /// <param name="newPackPath">The path at which to save the converted file.</param>
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
