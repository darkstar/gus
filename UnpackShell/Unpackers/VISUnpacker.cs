using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using UnpackShell.Shared;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class VISUnpacker : IUnpacker
    {
        /* TODO
         * Brute-force the PNG sizes
         * - start with 1x1 pixel and then move in a rectangular fashion:
         * 
         *   ^ height
         * ..|
         *  9|
         *  8|
         *  7|
         *  6|
         *  5|
         *  4|JKLM
         *  3|EFGN
         *  2|BCHO
         *  1|ADIP
         *   +-----------------------------------------> width
         *    12345678......
         *    
         * use some kind of iterator function: next(ref int w, ref int h) -> changes w,h to the next point
         * then use some sensible starting point (e.g. 512x512) and iterate in both directions
         * (one iterator goes UP until ??x??, one goes DOWN until 1x1)
         * */
        struct DirEntry
        {
            public int Offset;
            public int CompressedSize;
            public int Size;
            public int Flags;
        }

        string[] DecryptionKeys = new string[] {
            "76093af7c458e3c5",     // MD5 hash of "VSADV3PL" (many games)
            "98d1a1f120d8ce55",     // Deponia
            "af0512966ae16580",     // DSA - Satinavs Ketten
            "155c703a508c1c8a",     // unknown
            "d9ef88157bcbcae0",     // Deponia (French)
        };

        public string GetName()
        {
            return "visionaire.vis";
        }

        public string GetDescription()
        {
            return "Visionaire Studio VIS file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.NoFilenames;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm, true) != null;
        }

        public int FromBE(int val)
        {
            return System.Net.IPAddress.NetworkToHostOrder(val);
        }

        public string GetExtension(int type)
        {
            switch (type)
            {
                case 0: return "ogg";
                case 8: return "png";   // PNG files have their size information garbled, see here:
                                        // http://forum.xentax.com/viewtopic.php?f=10&t=5467
                                        // we simply brute-force the scramble-key as it is rather easy
                                        // (65536 checks max., we could probably do even better...)
            }
            return "dat";
        }

        public byte[] Decrypt(byte[] buf, string key)
        {
            byte[] result = new byte[buf.Length];

            for (int i = 0; i < buf.Length; i++)
            {
                result[i] = (byte)(buf[i] ^ (byte)key[i % key.Length]);
            }

            return result;
        }

        DirEntry[] GetDirectory(Stream strm, bool checkOnly)
        {
            byte[] buf = new byte[4];
            byte[] fileList;
            DirEntry[] results;
            int files;
            string key = "";
            BinaryReader rd = new BinaryReader(strm);

            rd.Read(buf, 0, 4);
            files = FromBE(rd.ReadInt32());

            if ((Encoding.ASCII.GetString(buf, 0, 4) != "VIS3") || (files > (1 << 24)))
                return null;

            results = new DirEntry[files];
            if (checkOnly)
                return results;

            fileList = new byte[6 + files * 16];
            rd.Read(fileList, 0, fileList.Length);
            // find decryption key
            foreach (string s in DecryptionKeys)
            {
                byte[] tmp = Decrypt(fileList, s);
                if (Encoding.ASCII.GetString(tmp, 0, 3) == "HDR"
                    && tmp[3] == 0
                    && tmp[4] == 0
                    && tmp[5] == 0)
                {
                    key = s;
                    fileList = tmp;
                    break;
                }
            }
            if (key == "")
                throw new InvalidOperationException("Unknown encryption key");

            using (BinaryReader rd2 = new BinaryReader(new MemoryStream(fileList)))
            {
                rd2.BaseStream.Seek(3, SeekOrigin.Begin); // skip the 6 bytes ID
                for (int i = 0; i < files; i++)
                {
                    results[i].Offset = FromBE(rd2.ReadInt32());
                    results[i].CompressedSize = FromBE(rd2.ReadInt32());
                    results[i].Size = FromBE(rd2.ReadInt32());
                    results[i].Flags = FromBE(rd2.ReadInt32());
                }
                // we should read the final "END" marker here...
            }            

            // populate file entry list
            return results;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            DirEntry[] files = GetDirectory(strm, false);
            List<FileEntry> fes = new List<FileEntry>();

            for (int i = 0; i < files.Length; i++)
            {
                FileEntry fe = new FileEntry();
                
                fe.Filename = String.Format("{0:x8}.{1}", i, GetExtension(files[i].Flags));
                fe.UncompressedSize = files[i].Size;

                fes.Add(fe);
            }

            return fes;
        }

        // fixing PNG headers by guessing is quite simple
        // - the width and heght fields (2x 4 bytes) are XORed with a "random" key
        //   -> this key is an ascii representation of a hash.
        //   -> Thus it consists of 8 chars in the range of '0'..'9' or 'a'..'f'
        // - first we assume dimensions < 65536 in each direction (sounds sane)
        //   -> this means we already have 4 of the 8 key bytes (the two high order bytes from each dimension)
        // - now we walk through all possible key values "00" to "ff" for each of the lower order byte pairs
        //   -> this means 256 (for x dimension) x 256 (for y dimension) tries
        //   -> in each try, we check if the CRC if the IHDR chunk is now correct
        //   ->  if it is correct then we found the remaining 2x2 bytes of the key
        private byte[] FixPngHdr(byte[] hdr, ref string key, ICRCAlgorithm crcAlgo)
        {
            byte[] result = new byte[hdr.Length];
            ulong target_crc = (ulong)hdr[hdr.Length - 4] << 24 |
                (ulong)hdr[hdr.Length - 3] << 16 |
                (ulong)hdr[hdr.Length - 2] << 8 |
                (ulong)hdr[hdr.Length - 1];
            ulong crc;

            hdr.CopyTo(result, 0);

            // first, set higher order positions to zero
            result[4] = result[5] = 0;
            result[8] = result[9] = 0;

            for (int key1 = 0; key1 < 256; key1++)
            {
                // ascii representation of key1
                string k1 = key1.ToString("x2").ToLower();
                byte b1 = (byte)(k1[0]);
                byte b2 = (byte)(k1[1]);

                result[6] = (byte)(hdr[6] ^ b1);
                result[7] = (byte)(hdr[7] ^ b2);

                for (int key2 = 0; key2 < 256; key2++)
                {
                    string k2 = key2.ToString("x2").ToLower();
                    byte b3 = (byte)(k2[0]);
                    byte b4 = (byte)(k2[1]);

                    result[10] = (byte)(hdr[10] ^ b3);
                    result[11] = (byte)(hdr[11] ^ b4);

                    crc = crcAlgo.CalculateCRC(result, result.Length - 4);
                    if (crc == target_crc)
                    {
                        key = Encoding.ASCII.GetString(new byte[] { hdr[4], hdr[5], b1, b2, hdr[8], hdr[9], b3, b4 });
                        return result;
                    }
                }
            }

            key = "";
            return null;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            DirEntry[] files = GetDirectory(strm, false);
            IDataTransformer trn = callbacks.TransformerRegistry.GetTransformer("zlib_dec");
            string key;

            // base offset = 4 bytes magic "VIS3" + 4 bytes #files + 3 bytes "HDR" marker + 16 bytes üer file + 3 bytes "END" marker
            strm.Seek(8 + 3 + files.Length * 16 + 3, SeekOrigin.Begin);
            for (int i = 0; i < files.Length; i++)
            {
                byte[] file = new byte[files[i].CompressedSize];

                strm.Read(file, 0, file.Length);
                key = "xxxxxxxx";
                if ((files[i].Flags & 0x2) != 0)
                {
                    // TODO: decrypt using our predefined key
                }

                if (files[i].CompressedSize != files[i].Size)
                {
                    // compressed -> decompress
                    byte[] unc = new byte[files[i].Size];
                    int usize = files[i].Size;
                    trn.TransformData(file, unc, files[i].CompressedSize, ref usize);
                    file = unc;
                }
                if (files[i].Flags == 0x08)
                {
                    // PNG file. Fix the scrambled dimensions
                    // find IHDR
                    int hdrpos = -1;
                    for (int j = 0; j < 32; j++)
                    {
                        if (Encoding.ASCII.GetString(file, j, 4) == "IHDR")
                        {
                            hdrpos = j - 4;
                            break;
                        }
                    }
                    if (hdrpos > 0)
                    {
                        // valid IHDR found. now try to fix it
                        int len = (int)(file[hdrpos] << 24) | (int)(file[hdrpos + 1] << 16) | (int)(file[hdrpos + 2]) << 8 | (int)(file[hdrpos + 3]);
                        byte[] hdr = new byte[len + 8];
                        byte[] fixed_hdr;

                        // copy to temporary array
                        Array.Copy(file, hdrpos + 4, hdr, 0, hdr.Length);
                        //.. and fix it by guessing
                        fixed_hdr = FixPngHdr(hdr, ref key, callbacks.GetCRCAlgorithm("CRC-32"));
                        if (fixed_hdr != null)
                        {
                            // could be fixed? great! then overwrite the old data with the fixed header
                            Array.Copy(fixed_hdr, 0, file, hdrpos + 4, fixed_hdr.Length);
                        }
                    }
                }
                callbacks.WriteData(String.Format("{0:x8}_{2}.{1}", i, GetExtension(files[i].Flags), key), file);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
