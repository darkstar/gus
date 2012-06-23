using System;
using System.IO;
using System.Collections.Generic;
using UnpackShell.DataTransformers;
using UnpackShell.Interfaces;
using System.Text;
using Ionic.Zlib;

namespace UnpackShell
{
    public class UnitTests
    {
        public void DoTests()
        {
            DoCompressTests();
        }

        void DoCompressTests()
        {
            byte[] data1 = new byte[512];
            byte[] data2 = new byte[512];
            MemoryStream mstrm1;
            MemoryStream mstrm2;
            byte[] out1;
            byte[] out2;
            Random rnd = new Random();
            int csize1, csize2, osize;

            for (int i = 0; i < 512; i++)
            {
                data1[i] = (byte)(i >> 4);
                data2[i] = (byte)rnd.Next();
            }

            mstrm1 = new MemoryStream(data1, false);
            mstrm2 = new MemoryStream(data2, false);

            Console.Write("Compressing 512 non-random bytes down...");
            out1 = new byte[1024];
            MemoryStream mo1 = new MemoryStream(out1, 0, 1024, true);
            ZlibStream zstrm = new ZlibStream(mo1, CompressionMode.Compress, true);
            mstrm1.CopyTo(zstrm);
            zstrm.Close();
            csize1 = (int)mo1.Position;
            Console.WriteLine("{0} bytes", mo1.Position);

            Console.Write("Compressing 512 random bytes down...");
            out2 = new byte[1024];
            MemoryStream mo2 = new MemoryStream(out2, 0, 1024, true);
            zstrm = new ZlibStream(mo2, CompressionMode.Compress, true);
            mstrm2.CopyTo(zstrm);
            zstrm.Close();
            csize2 = (int)mo2.Position;
            Console.WriteLine("{0} bytes", mo2.Position);

            // decompress again, using ZLibDataTransformer
            byte[] final1 = new byte[1024];
            byte[] final2 = new byte[1024];

            ZLibDecompressor trns = new ZLibDecompressor();
            osize = 512;
            trns.TransformData(out1, final1, csize1, ref osize);
            Console.WriteLine("Decompressed buffer 1 back to {0} bytes", osize);
            osize = 512;
            trns.TransformData(out2, final2, csize2, ref osize);
            Console.WriteLine("Decompressed buffer 2 back to {0} bytes", osize);

            for (int i = 0; i < 512; i++)
            {
                if (final1[i] != data1[i])
                {
                    Console.WriteLine("Decompression failed for buffer 1, byte {0}", i);
                    break;
                }
                if (final2[i] != data2[i])
                {
                    Console.WriteLine("Decompression failed for buffer 2, byte {0}", i);
                    break;
                }
            }
            Console.WriteLine("Buffer comparison finished");
        }
    }
}
