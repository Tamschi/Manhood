using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Manhood
{
    public class ManRandom
    {
        public long Seed, Generation;

        public ManRandom(long seed)
        {
            Seed = seed;
            Generation = 0;
        }

        public ManRandom(long seed, long generation)
        {
            Seed = seed;
            Generation = generation;
        }

        public ManRandom()
        {
            Seed = Environment.TickCount;
            Generation = 0;
        }

        public static long GetRaw(long s, long g)
        {
            unchecked
            {
                long v = 6364136223846793005;
                long ss = (s + 113) * 982451653 + 12345;
                long gg = (g + 119) * 32416189717 + 98772341;
                v *= ss.RotR((int)gg) ^ gg.RotL((int)ss);
                v += ss + gg;
                v ^= ss * gg;
                return v;
            }

        }

        public long this[int g]
        {
            get
            {
                return GetRaw(this.Seed, g);
            }
        }

        public long NextRaw()
        {
            return GetRaw(Seed, Generation++);
        }

        public long PrevRaw()
        {
            return GetRaw(Seed, --Generation);
        }

        public void Reset()
        {
            Generation = 0;
        }

        public void Reset(long newSeed)
        {
            Generation = 0;
            Seed = newSeed;
        }

        public static long Chain(long seed, params long[] gens)
        {
            long num = seed;
            for (int i = 0; i < gens.Length; i++)
            {
                num = GetRaw(num, gens[i]);
            }
            return num;
        }

        public int Peek()
        {
            return (int)GetRaw(Seed, Generation) & 0x7FFFFFFF;
        }

        public int PeekAt(long generation)
        {
            return (int)GetRaw(Seed, generation) & 0x7FFFFFFF;
        }

        public int Next()
        {
            return (int)NextRaw() & 0x7FFFFFFF;
        }

        public int Prev()
        {
            return (int)PrevRaw() & 0x7FFFFFFF;
        }

        public int Next(int max)
        {
            return ((int)NextRaw() & 0x7FFFFFFF) % max;
        }

        public int Prev(int max)
        {
            return ((int)PrevRaw() & 0x7FFFFFFF) % max;
        }

        public int Peek(int max)
        {
            return ((int)GetRaw(Seed, Generation) & 0x7FFFFFFF) % max;
        }

        public int PeekAt(long generation, int max)
        {
            return ((int)GetRaw(Seed, generation) & 0x7FFFFFFF) % max;
        }

        public int Next(int min, int max)
        {
            if (min >= max)
            {
                throw new ArgumentException("Min must be less than max.");
            }

            return (((int)NextRaw() & 0x7FFFFFFF) - min) % (max - min) + min;
        }

        public int Prev(int min, int max)
        {
            if (min >= max)
            {
                throw new ArgumentException("Min must be less than max.");
            }

            return (((int)PrevRaw() & 0x7FFFFFFF) - min) % (max - min) + min;
        }

        public int Peek(int min, int max)
        {
            if (min >= max)
            {
                throw new ArgumentException("Min must be less than max.");
            }

            return (((int)GetRaw(Seed, Generation) & 0x7FFFFFFF) - min) % (max - min) + min;
        }

        public int PeekAt(int generation, int min, int max)
        {
            if (min >= max)
            {
                throw new ArgumentException("Min must be less than max.");
            }

            return (((int)GetRaw(Seed, generation) & 0x7FFFFFFF) - min) % (max - min) + min;
        }
    }
}
