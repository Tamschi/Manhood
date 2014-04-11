using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Manhood
{
    public partial class ManEngine
    {
        private const uint magicMB = 0xBADDF001;
        private const uint magicTV = 0xBADD5456;

        public void MountLegacy(string addonPath)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(addonPath, FileMode.Open), Encoding.ASCII))
            {
                int p = 0;
                uint magic = reader.ReadUInt32();
                if (magic != magicMB && magic != magicTV)
                {
                    throw new InvalidDataException("File is corrupt.");
                }

                reader.ReadLongString();
                reader.ReadLongString();
                reader.ReadLongString();

                if (reader.ReadBoolean()) // macros
                {
                    LoadDefinitions(DefinitionType.Macro, reader.ReadStringArray());
                }
                if (reader.ReadBoolean()) // globals
                {
                    LoadDefinitions(DefinitionType.Global, reader.ReadStringArray());
                }

                if (reader.ReadBoolean()) // outlines (deprecated)
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

                if (reader.ReadBoolean()) // vocab
                {
                    int c = reader.ReadInt32();

                    for (int i = 0; i < c; i++)
                    {
                        reader.ReadLongString(); // filename
                        int bufferLength = reader.ReadInt32(); // data length - skipping because we can just load right off the stream
                        int bufferEnd = bufferLength + (int)reader.BaseStream.Position;
                        var list = new WordList(reader, ref p);
                        reader.BaseStream.Position = bufferEnd;

                        if (wordBank.ContainsKey(list._symbol))
                        {
                            wordBank[list._symbol].Merge(list);
                        }
                        else
                        {
                            wordBank.Add(list._symbol, list);
                        }
                    }
                }

                if (reader.ReadBoolean())
                {
                    int c = reader.ReadInt32();

                    for (int i = 0; i < c; i++)
                    {
                        reader.ReadLongString(); // dir name
                        char sc = reader.ReadChar();
                        string symbol = sc.ToString(); // symbol
                        string title = reader.ReadLongString(); // title

                        var list = new PatternList(title, sc, reader.ReadStringArray().Select<string, Pattern>((str, index) => new Pattern(String.Format("{0}_{1}", symbol, index), str)).ToList());

                        if (patternBank.ContainsKey(symbol))
                        {
                            patternBank[symbol].Merge(list);
                        }
                        else
                        {
                            patternBank.Add(symbol, list);
                        }
                    }
                }

                if (magic == magicTV)
                {
                    if (reader.ReadBoolean())
                    {
                        int c = reader.ReadInt32();

                        for (int i = 0; i < c; i++)
                        {
                            string scriptName = reader.ReadLongString();
                            string scriptContent = reader.ReadLongString();
                            if (!patternScriptBank.ContainsKey(scriptName))
                            {
                                patternScriptBank.Add(scriptName, scriptContent);
                            }
                        }
                    }
                }
            }
        }

        
    }
}
