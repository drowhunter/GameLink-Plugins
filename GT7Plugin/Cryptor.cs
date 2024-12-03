using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using System.Buffers.Binary;
using GT7Plugin;

namespace GT7Plugin
{
    /// <summary>
    /// Used to decrypt packets from GT7's Simulator Interface.
    /// </summary>
    public class Cryptor
    {
        private Salsa20 _salsa;

        public const string Key = "Simulator Interface Packet GT7 ver 0.0";

        public Cryptor()
        {
            _salsa = new Salsa20(Encoding.ASCII.GetBytes(Key), 0x26);
        }

        public void Decrypt(Span<byte> bytes)
        {
            _salsa.Set(0);

            int iv1 = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0x40)); // Seed IV is always located there
            int iv2 = (int)(iv1 ^ 0xDEADBEAF); // Notice DEADBEAF, not DEADBEEF

            Span<byte> iv = stackalloc byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(iv, iv2);
            BinaryPrimitives.WriteInt32LittleEndian(iv.Slice(4), iv1);
            _salsa.SetIV(iv);

            _salsa.Decrypt(bytes, bytes.Length);
            // Magic should be "G7S0" when decrypted 
        }
    }
}