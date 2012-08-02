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
                                        // another way of solving it would be to brute-force the size,
                                        // starting with 1x1 up to 1920x1080 (or similar), and then
                                        // checking the IHDR CRC each time (as explained in the link)
                                        // An easier brute-force: assume upper 16bits are 0 (no image
                                        // dimension > 65536 pixels) and brute-force the remaining 4
                                        // bytes with the 16 possible hex digits each -> 65k iterations
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

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            DirEntry[] files = GetDirectory(strm, false);
            IDataTransformer trn = callbacks.TransformerRegistry.GetTransformer("zlib_dec");

            // base offset = 4 bytes magic "VIS3" + 4 bytes #files + 3 bytes "HDR" marker + 16 bytes üer file + 3 bytes "END" marker
            strm.Seek(8 + 3 + files.Length * 16 + 3, SeekOrigin.Begin);
            for (int i = 0; i < files.Length; i++)
            {
                byte[] file = new byte[files[i].CompressedSize];

                strm.Read(file, 0, file.Length);
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
                callbacks.WriteData(String.Format("{0:x8}.{1}", i, GetExtension(files[i].Flags)), file);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
