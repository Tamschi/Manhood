using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization;
using System.Drawing;

namespace Manhood
{
    internal static class Extensions
    {
        public static sbyte RotL(this sbyte data, int times)
        {
            return (sbyte)((data << (times % 8)) | (data >> (8 - (times % 8))));
        }
        public static sbyte RotR(this sbyte data, int times)
        {
            return (sbyte)((data >> (times % 8)) | (data << (8 - (times % 8))));
        }
        public static byte RotL(this byte data, int times)
        {
            return (byte)((data << (times % 8)) | (data >> (8 - (times % 8))));
        }
        public static byte RotR(this byte data, int times)
        {
            return (byte)((data >> (times % 8)) | (data << (8 - (times % 8))));
        }
        public static short RotL(this short data, int times)
        {
            return (short)((data << (times % 16)) | (data >> (16 - (times % 16))));
        }
        public static short RotR(this short data, int times)
        {
            return (short)((data >> (times % 16)) | (data << (16 - (times % 16))));
        }
        public static ushort RotL(this ushort data, int times)
        {
            return (ushort)((data << (times % 16)) | (data >> (16 - (times % 16))));
        }
        public static ushort RotR(this ushort data, int times)
        {
            return (ushort)((data >> (times % 16)) | (data << (16 - (times % 16))));
        }
        public static int RotL(this int data, int times)
        {
            return (data << (times % 32)) | (data >> (32 - times % 32));
        }
        public static int RotR(this int data, int times)
        {
            return (data >> (times % 32)) | (data << (32 - times % 32));
        }
        public static uint RotL(this uint data, int times)
        {
            return (uint)((data << (times % 32)) | (data >> (32 - (times % 32))));
        }
        public static uint RotR(this uint data, int times)
        {
            return (uint)((data >> (times % 32)) | (data << (32 - (times % 32))));
        }
        public static long RotL(this long data, int times)
        {
            return (long)((data << (times % 64)) | (data >> (64 - (times % 64))));
        }
        public static long RotR(this long data, int times)
        {
            return (long)((data >> (times % 64)) | (data << (64 - (times % 64))));
        }
        public static ulong RotL(this ulong data, int times)
        {
            return (ulong)((data << (times % 64)) | (data >> (64 - (times % 64))));
        }
        public static ulong RotR(this ulong data, int times)
        {
            return (ulong)((data >> (times % 64)) | (data << (64 - (times % 64))));
        }

        [Obsolete]
        public static string ReadLongString(this BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }

        [Obsolete]
        public static void WriteLongString(this BinaryWriter writer, string value)
        {
            writer.Write(value.Length);
            writer.Write(Encoding.ASCII.GetBytes(value));
        }

        [Obsolete]
        public static string[] ReadStringArray(this BinaryReader reader)
        {
            int count = reader.ReadInt32();
            string[] list = new string[count];
            for (int i = 0; i < count; i++)
            {
                list[i] = reader.ReadLongString();
            }
            return list;
        }

        [Obsolete]
        public static void WriteStringArray(this BinaryWriter writer, string[] array)
        {
            writer.Write(array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                writer.WriteLongString(array[i]);
            }
        }

        public static int GetLineNumberFromIndex(this string text, int index, out int column)
        {
            if (index >= text.Length || index < 0)
            {
                index = text.Length - 1;
            }
            int lineNum = 1;
            int columnNum = 0;
            for (int i = 0; i <= index; i++)
            {
                columnNum++;
                if (text[i] == '\n')
                {
                    lineNum++;
                    columnNum = 0;
                }
            }
            column = columnNum;
            return lineNum;
        }

        public static string Capitalize(this string str, WordCase capFormat)
        {
            switch(capFormat)
            {
                case WordCase.AllCaps:
                    return str.ToUpper();
                case WordCase.Capitalized:
                    return Regex.Replace(str, @"^\w", m => m.Value.ToUpper());
                case WordCase.Proper:
                    return Regex.Replace(str, @"\b\w", m => m.Value.ToUpper());
                case WordCase.None:
                default:
                    return str;
            }
        }

        public static int[] GetSelectorSubs(this string str, int start)
        {
            int balance = 0;
            List<int> subs = new List<int>();
            subs.Add(start);
            for (int i = start; i < str.Length; i++)
            {
                if (str[i] == '/' && balance == 0 && (i == 0 || str[i - 1] != '\\'))
                {
                    subs.Add(i + 1);
                }
                else if (str[i] == '{' && (i == 0 || str[i - 1] != '\\'))
                {
                    balance++;
                }
                else if (str[i] == '}' && (i == 0 || str[i - 1] != '\\'))
                {
                    if (balance == 0)
                    {
                        return subs.ToArray();
                    }
                    balance--;
                }
            }
            return new int[0];
        }

        public static int FindClosingTriangleBracket(this string str, int start)
        {
            int balance = 0;
            for (int i = start; i < str.Length; i++)
            {
                if (str[i] == '<' && (i == 0 || str[i - 1] != '\\'))
                {
                    balance++;
                }
                else if (str[i] == '>' && (i == 0 || str[i - 1] != '\\'))
                {
                    balance--;
                }
                if (balance == -1)
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool ParseParameterList(this string str, out List<string> list)
        {
            list = new List<string>();

            if (str == null) return false;
            if (str.Length == 0) return false;

            CharReader reader = new CharReader(str, 0);            
            char c = '\0';
            while(!reader.EndOfString)
            {
                c = reader.ReadChar();
                if (c == '[')
                {
                    int end = str.FindClosingSquareBracket(reader.Position);
                    if (end == -1) return false;                    
                    list.Add(reader.ReadTo(end));
                }
            }
            return true;
        }

        public static int FindClosingSquareBracket(this string str, int start)
        {
            int balance = 0;
            for (int i = start; i < str.Length; i++)
            {
                if (str[i] == '[' && (i == 0 || str[i - 1] != '\\'))
                {
                    balance++;
                }
                else if (str[i] == ']' && (i == 0 || str[i - 1] != '\\'))
                {
                    balance--;
                }
                if (balance == -1)
                {
                    return i;
                }
            }
            return -1;
        }

        public static int FindClosingCurlyBracket(this string str, int start)
        {
            int balance = 0;
            for (int i = start; i < str.Length; i++)
            {
                if (str[i] == '{' && (i == 0 || str[i - 1] != '\\'))
                {
                    balance++;
                }
                else if (str[i] == '}' && (i == 0 || str[i - 1] != '\\'))
                {
                    balance--;
                }
                if (balance == -1)
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool StartsWithVowel(this string str)
        {
            if (str.Length == 0) return false;
            char c = str[0];
            if (str.StartsWith("X")) return true;
            if (str.StartsWith("ur") || str.StartsWith("uv") || str.StartsWith("honest") || str.StartsWith("honor")) return false;
            return c == 'a' || c == 'A' || c == 'e' || c == 'E' || c == 'i' || c == 'I' || c == 'o' || c == 'O' || c == 'u' || c == 'U';
        }

        public static bool PermitsCap(this char c)
        {
            if (c == ' ' || char.IsSymbol(c) || char.IsSeparator(c)) return true;
            UnicodeCategory uni = char.GetUnicodeCategory(c);
            return (uni == UnicodeCategory.DashPunctuation || uni == UnicodeCategory.SpaceSeparator || uni == UnicodeCategory.OpenPunctuation || uni == UnicodeCategory.ClosePunctuation);
        }
    }
}
