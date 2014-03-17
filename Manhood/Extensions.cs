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
    public static class Extensions
    {

        public static string ReadLongString(this BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }

        public static void WriteLongString(this BinaryWriter writer, string value)
        {
            writer.Write(value.Length);
            writer.Write(Encoding.ASCII.GetBytes(value));
        }

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

        public static string ToRoman(this int number)
        {
            if ((number < 0) || (number > 3999)) throw new ArgumentOutOfRangeException("insert value betwheen 1 and 3999");
            if (number < 1) return string.Empty;
            if (number >= 1000) return "M" + ToRoman(number - 1000);
            if (number >= 900) return "CM" + ToRoman(number - 900);
            if (number >= 500) return "D" + ToRoman(number - 500);
            if (number >= 400) return "CD" + ToRoman(number - 400);
            if (number >= 100) return "C" + ToRoman(number - 100);
            if (number >= 90) return "XC" + ToRoman(number - 90);
            if (number >= 50) return "L" + ToRoman(number - 50);
            if (number >= 40) return "XL" + ToRoman(number - 40);
            if (number >= 10) return "X" + ToRoman(number - 10);
            if (number >= 9) return "IX" + ToRoman(number - 9);
            if (number >= 5) return "V" + ToRoman(number - 5);
            if (number >= 4) return "IV" + ToRoman(number - 4);
            if (number >= 1) return "I" + ToRoman(number - 1);
            throw new ArgumentOutOfRangeException("something bad happened");
        }
        public static string Capitalize(this string str, WordFormat capFormat)
        {
            switch(capFormat)
            {
                case WordFormat.AllCaps:
                    return str.ToUpper();
                case WordFormat.Capitalized:
                    return Regex.Replace(str, @"^\w", m => m.Value.ToUpper());
                case WordFormat.Proper:
                    return Regex.Replace(str, @"\b\w", m => m.Value.ToUpper());
                case WordFormat.None:
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
                if (str[i] == '/' && balance == 0)
                {
                    subs.Add(i + 1);
                }
                else if (str[i] == '{')
                {
                    balance++;
                }
                else if (str[i] == '}')
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
                if (str[i] == '<')
                {
                    balance++;
                }
                else if (str[i] == '>')
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

        public static int FindClosingSquareBracket(this string str, int start)
        {
            int balance = 0;
            for (int i = start; i < str.Length; i++)
            {
                if (str[i] == '[')
                {
                    balance++;
                }
                else if (str[i] == ']')
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
                if (str[i] == '{')
                {
                    balance++;
                }
                else if (str[i] == '}')
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
