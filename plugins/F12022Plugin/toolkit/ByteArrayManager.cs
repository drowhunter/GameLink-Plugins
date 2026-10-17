using System;
using System.Collections.Generic;

namespace TimHanewich.Toolkit
{
    public class ByteArrayManager
    {
        private byte[] SuppliedSourceBytes;
        private long Index = -1;

        public ByteArrayManager(byte[] bytes)
        {
            SuppliedSourceBytes = bytes;
        }

        public byte NextByte()
        {
            try
            {
                Index = Index + 1;
                return SuppliedSourceBytes[Index];
            }
            catch
            {
                throw new Exception("Critical ByteArrayManager error.  Length of supplied byte array: " + SuppliedSourceBytes.Length.ToString() + " Trying to Access Byte index #" + Index.ToString());
            }
        }

        public byte[] NextBytes(int number_of_bytes)
        {
            List<byte> ToReturn = new List<byte>();
            int t = 1;
            for (t = 1; t <= number_of_bytes; t++)
            {
                ToReturn.Add(NextByte());
            }
            return ToReturn.ToArray();
        }
    }
}