using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NSFWDownloader
{
    class Random
    {
        public static RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();

        /// <summary>
        /// </summary>
        /// <returns>Random double from 0.0 to 1.0</returns>
        public static double GetRandomDouble()
        {
            byte[] result = new byte[8];
            rngCsp.GetBytes(result);
            return (double)BitConverter.ToUInt64(result, 0) / ulong.MaxValue;
        }


        public static double GetRandomDouble(double min, double max)
        {
            byte[] result = new byte[8];
            rngCsp.GetBytes(result);
            return (double)BitConverter.ToUInt64(result, 0) / ulong.MaxValue * (max - min) + min;
        }

        /// <summary>
        /// Unlike C# pseudo-random, this one may return the value equal to param max
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int GetRandomInt(int min, int max)
        {
            byte[] result = new byte[8];
            rngCsp.GetBytes(result);
            return (int)Math.Round(((double)BitConverter.ToUInt64(result, 0) / ulong.MaxValue) * (max - min)) + min;
        }
    }
}
