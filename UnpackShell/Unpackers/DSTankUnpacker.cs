using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class DSTankUnpacker : IUnpacker
    {
        // source: http://scottbilas.com/files/dungeon_siege/TankStructure.h
        private class GlobalHeader
        {
            public struct SystemTime
            {
               public short Year;
               public short Month;
               public short DayOfWeek;
               public short Day;
               public short Hour;
               public short Minute;
               public short Second;
               public short Milliseconds;
            }
            public uint Version;
            public uint DirSetOffset;
            public uint FileSetOffset;
            public uint IndexSize;
            public uint DataOffset;
            public byte[] ProductVersion;
            public byte[] MinimumVersion;
            public uint Priority;
            public uint Flags;
            public string CreatorId;
            public byte[] Guid;
            public uint IndexCRC;
            public uint DataCRC;
            public SystemTime BuildTime;
            public string CopyrightText;
            public string BuildText;
            public string TitleText;
            public string AuthorText;
            public short AdditionalHeaderStringLength;
        }

        private class DirSetEntry
        {
            public uint ParentOffset;
            public uint ChildCount;
            public DateTime FileTime;
            public string Name;
            public uint[] ChildOffsets;
        }

        private class DirSet
        {
            public uint Count;
            public uint[] Offsets;
            public DirSetEntry[] Entries;
            public uint EndOffset;
        }

        private class ChunkHeader
        {
            public uint UncompressedSize;
            public uint CompressedSize;
            public uint ExtraBytes;
            public uint Offset;
        }

        private class FileSetEntry
        {
            public uint ParentOffset;
            public uint Size;
            public uint Offset;
            public uint CRC;
            public DateTime FileTime;
            public short Format;
            public short Flags;
            public string Name;
            // only if compression is used
            public uint CompressedSize = 0;
            public uint ChunkSize = 0;
            public uint NumChunks = 0;
            public ChunkHeader[] ChunkHeaders;
        }

        private class FileSet
        {
            public uint Count;
            public uint[] Offsets;
            public FileSetEntry[] Entries; 
        }

        private class ArchiveFile
        {
            public GlobalHeader Header;
            public DirSet DirectorySet;
            public FileSet FileSet;
            public Dictionary<uint, DirSetEntry> DirOffsets = new Dictionary<uint, DirSetEntry>();
            public Dictionary<uint, FileSetEntry> FileOffsets = new Dictionary<uint, FileSetEntry>();
        }

        public string GetName()
        {
            return "dsiege.tank";
        }

        public string GetDescription()
        {
            return "Dungeon Siege TANK file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        private bool IsTankFile(Stream strm)
        {
            byte[] buf = new byte[4];

            strm.Read(buf, 0, 4);
            // TODO: add DS2 files here too (after making certain that the format is still the same)
            if (Encoding.ASCII.GetString(buf) != "DSig")
                return false;
            strm.Read(buf, 0, 4);

            return Encoding.ASCII.GetString(buf) == "Tank";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.SupportsSubdirectories | UnpackerFlags.Experimental;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return IsTankFile(strm);
        }

        void ReadDirSet(ArchiveFile af, Stream strm)
        {
            BinaryReader rd = new BinaryReader(strm);

            strm.Seek(af.Header.DirSetOffset, SeekOrigin.Begin);
            af.DirectorySet = new DirSet();
            af.DirectorySet.Count = rd.ReadUInt32();
            af.DirectorySet.Offsets = new uint[af.DirectorySet.Count];
            af.DirectorySet.Entries = new DirSetEntry[af.DirectorySet.Count];

            // read the offsets
            for (uint i = 0; i < af.DirectorySet.Count; i++)
            {
                af.DirectorySet.Offsets[i] = rd.ReadUInt32();
            }
            for (uint i = 0; i < af.DirectorySet.Count; i++)
            {
                short strlen;
                byte[] strbuf;

                strm.Seek(af.Header.DirSetOffset + af.DirectorySet.Offsets[i], SeekOrigin.Begin);
                af.DirectorySet.Entries[i] = new DirSetEntry();
                af.DirectorySet.Entries[i].ParentOffset = rd.ReadUInt32();
                af.DirectorySet.Entries[i].ChildCount = rd.ReadUInt32();
                af.DirectorySet.Entries[i].FileTime = DateTime.FromFileTime(rd.ReadInt64());
                strlen = rd.ReadInt16();
                strlen++; // add 0-byte at the end
                while ((2 + strlen) % 4 != 0) // pad to dword size
                    strlen++;
                strbuf = new byte[strlen];
                rd.Read(strbuf, 0, strlen);

                string name = Encoding.ASCII.GetString(strbuf);
                if (name.IndexOf('\0') >= 0)
                    name = name.Substring(0, name.IndexOf('\0'));
                af.DirectorySet.Entries[i].Name = name;
                af.DirectorySet.Entries[i].ChildOffsets = new uint[af.DirectorySet.Entries[i].ChildCount];
                for (int j = 0; j < af.DirectorySet.Entries[i].ChildCount; j++)
                {
                    af.DirectorySet.Entries[i].ChildOffsets[j] = rd.ReadUInt32();
                }

                // add to lookup table
                af.DirOffsets.Add(af.DirectorySet.Offsets[i], af.DirectorySet.Entries[i]);
            }
            af.DirectorySet.EndOffset = (uint)strm.Position;
        }

        void ReadFileSet(ArchiveFile af, Stream strm)
        {
            BinaryReader rd = new BinaryReader(strm);

            strm.Seek(af.Header.FileSetOffset, SeekOrigin.Begin);

            af.FileSet = new FileSet();
            af.FileSet.Count = rd.ReadUInt32();
            af.FileSet.Offsets = new uint[af.FileSet.Count];
            af.FileSet.Entries = new FileSetEntry[af.FileSet.Count];

            for (int i = 0; i < af.FileSet.Count; i++)
            {
                af.FileSet.Offsets[i] = rd.ReadUInt32();
            }

            for (int i = 0; i < af.FileSet.Count; i++)
            {
                short strlen;
                byte[] strbuf; 
                
                strm.Seek(af.Header.FileSetOffset + af.FileSet.Offsets[i], SeekOrigin.Begin);

                af.FileSet.Entries[i] = new FileSetEntry();
                af.FileSet.Entries[i].ParentOffset = rd.ReadUInt32();
                af.FileSet.Entries[i].Size = rd.ReadUInt32();
                af.FileSet.Entries[i].Offset = rd.ReadUInt32();
                af.FileSet.Entries[i].CRC = rd.ReadUInt32();
                af.FileSet.Entries[i].FileTime = DateTime.FromFileTime(rd.ReadInt64());
                af.FileSet.Entries[i].Format = rd.ReadInt16();
                af.FileSet.Entries[i].Flags = rd.ReadInt16();

                strlen = rd.ReadInt16();
                strlen++; // add 0-byte at the end
                while ((2 + strlen) % 4 != 0) // pad to dword size
                    strlen++;
                strbuf = new byte[strlen];
                rd.Read(strbuf, 0, strlen);

                string name = Encoding.ASCII.GetString(strbuf);
                if (name.IndexOf('\0') >= 0)
                    name = name.Substring(0, name.IndexOf('\0'));

                af.FileSet.Entries[i].Name = name;

                if (af.FileSet.Entries[i].Format != 0)
                {
                    // compressed resource; read compression header
                    af.FileSet.Entries[i].CompressedSize = rd.ReadUInt32();
                    af.FileSet.Entries[i].ChunkSize = rd.ReadUInt32();
                    af.FileSet.Entries[i].NumChunks = (uint)Math.Ceiling((double)af.FileSet.Entries[i].Size / (double)af.FileSet.Entries[i].ChunkSize);
                    af.FileSet.Entries[i].ChunkHeaders = new ChunkHeader[af.FileSet.Entries[i].NumChunks];
                    for (int j = 0; j < af.FileSet.Entries[i].NumChunks; j++)
                    {
                        af.FileSet.Entries[i].ChunkHeaders[j] = new ChunkHeader();
                        af.FileSet.Entries[i].ChunkHeaders[j].UncompressedSize = rd.ReadUInt32();
                        af.FileSet.Entries[i].ChunkHeaders[j].CompressedSize = rd.ReadUInt32();
                        af.FileSet.Entries[i].ChunkHeaders[j].ExtraBytes = rd.ReadUInt32();
                        af.FileSet.Entries[i].ChunkHeaders[j].Offset = rd.ReadUInt32();
                    }
                }

                // add to lookup table
                af.FileOffsets.Add(af.FileSet.Offsets[i], af.FileSet.Entries[i]);
            }
        }

        GlobalHeader ReadGlobalHeader(Stream strm)
        {
            GlobalHeader hd = new GlobalHeader();
            BinaryReader rd = new BinaryReader(strm);
            byte[] buf;

            hd.Version = rd.ReadUInt32();
            hd.DirSetOffset = rd.ReadUInt32();
            hd.FileSetOffset = rd.ReadUInt32();
            hd.IndexSize = rd.ReadUInt32();
            hd.DataOffset = rd.ReadUInt32();

            hd.ProductVersion = new byte[12];
            rd.Read(hd.ProductVersion, 0, 12);
            hd.MinimumVersion = new byte[12];
            rd.Read(hd.MinimumVersion, 0, 12);

            hd.Priority = rd.ReadUInt32();
            hd.Flags = rd.ReadUInt32();
            buf = new byte[4];
            rd.Read(buf, 0, 4);
            hd.CreatorId = Encoding.ASCII.GetString(buf, 0, 4);
            hd.Guid = new byte[16];
            rd.Read(hd.Guid, 0, 16);
            hd.IndexCRC = rd.ReadUInt32();
            hd.DataCRC = rd.ReadUInt32();

            hd.BuildTime.Year = rd.ReadInt16();
            hd.BuildTime.Month = rd.ReadInt16();
            hd.BuildTime.DayOfWeek = rd.ReadInt16();
            hd.BuildTime.Day = rd.ReadInt16();
            hd.BuildTime.Hour = rd.ReadInt16();
            hd.BuildTime.Minute = rd.ReadInt16();
            hd.BuildTime.Second = rd.ReadInt16();
            hd.BuildTime.Milliseconds = rd.ReadInt16();

            buf= new byte[200];
            rd.Read(buf, 0, 200);
            hd.CopyrightText = Encoding.Unicode.GetString(buf, 0, 200);
            hd.CopyrightText = hd.CopyrightText.Substring(0, hd.CopyrightText.IndexOf('\0'));

            rd.Read(buf, 0, 200);
            hd.BuildText = Encoding.Unicode.GetString(buf, 0, 200);
            hd.BuildText = hd.BuildText.Substring(0, hd.BuildText.IndexOf('\0'));

            rd.Read(buf, 0, 200);
            hd.TitleText = Encoding.Unicode.GetString(buf, 0, 200);
            hd.TitleText = hd.TitleText.Substring(0, hd.TitleText.IndexOf('\0'));

            rd.Read(buf, 0, 80);
            hd.AuthorText = Encoding.Unicode.GetString(buf, 0, 80);
            hd.AuthorText = hd.AuthorText.Substring(0, hd.AuthorText.IndexOf('\0'));

            // skip remaining text (2 byte chars!)
            hd.AdditionalHeaderStringLength = rd.ReadInt16();
            strm.Seek(2 * hd.AdditionalHeaderStringLength, SeekOrigin.Current);

            return hd;
        }

        private ArchiveFile ReadDirectory(Stream strm)
        {
            ArchiveFile af = new ArchiveFile();

            af.Header = ReadGlobalHeader(strm);
            ReadDirSet(af, strm);
            ReadFileSet(af, strm);

            return af;
        }

        private string GetRelPath(ArchiveFile af, FileSetEntry fse)
        {
            string res = fse.Name;
            uint ParentOffset = fse.ParentOffset;

            while (ParentOffset != 0)
            {
                DirSetEntry dse = af.DirOffsets[ParentOffset];
                if (dse.ParentOffset == 0)
                    break;
                res = String.Format("{0}/{1}", dse.Name, res);
                ParentOffset = dse.ParentOffset;
            }

            return res;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            ArchiveFile afile;

            if (!IsTankFile(strm))
                yield break;

            afile = ReadDirectory(strm);

            foreach (FileSetEntry fse in afile.FileSet.Entries)
            {
                FileEntry fe = new FileEntry();
                fe.UncompressedSize = fse.Size;
                fe.Filename = GetRelPath(afile, fse);

                yield return fe;
            }
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            ArchiveFile afile;
            IDataTransformer zdec = callbacks.TransformerRegistry.GetTransformer("zlib_dec");

            if (!IsTankFile(strm))
                return;

            afile = ReadDirectory(strm);

            foreach (FileSetEntry fse in afile.FileSet.Entries)
            {
                string path = GetRelPath(afile, fse);
                if (fse.Format == 0)
                {
                    // RAW
                    if (fse.ChunkSize == 0)
                    {
                        // direct extract
                        byte[] data = new byte[fse.Size];
                        strm.Seek(fse.Offset + afile.Header.DataOffset, SeekOrigin.Begin);
                        strm.Read(data, 0, (int)fse.Size);
                        callbacks.WriteData(path, data);
                    }
                    else
                    {
                        throw new Exception(String.Format("uncompressed chunked format not yet supported for file {0}", path));
                    }
                }
                else if (fse.Format == 1)
                {
                    // zlib
                    if (fse.ChunkSize == 0)
                    {
                        throw new Exception(String.Format("compressed unchunked format not yet supported for file {0}", path));
                    }
                    else
                    {
                        // uncompress chunked data, this is a bit involved but oh well
                        byte[] compressed;
                        int writeOffset = 0;
                        byte[] uncompressed = new byte[fse.ChunkSize];
                        byte[] extradata = new byte[fse.ChunkSize];
                        byte[] file = new byte[fse.Size];              // the final (uncompressed) file data

                        for (int chunk = 0; chunk < fse.NumChunks; chunk++)
                        {
                            ChunkHeader ch = fse.ChunkHeaders[chunk];
                            compressed = new byte[ch.CompressedSize];
                            int outLength = (int)ch.UncompressedSize;

                            // seek to compressed data 
                            //if (fse.Name == "b_c_edm_shard-04-static.raw")
                                //System.Diagnostics.Debugger.Break();

                            // seek to the beginning of the chunk
                            strm.Seek(afile.Header.DataOffset + fse.Offset + ch.Offset, SeekOrigin.Begin);

                            // read the compressed portion
                            strm.Read(compressed, 0, (int)ch.CompressedSize);

                            if (ch.CompressedSize != ch.UncompressedSize)
                            {
                                // a compressed chunk
                                zdec.TransformData(compressed, uncompressed, (int)ch.CompressedSize, ref outLength);

                                // ...read the extra data (if required)
                                strm.Read(extradata, 0, (int)ch.ExtraBytes);

                                // combine both in final data
                                Array.Copy(uncompressed, 0, file, writeOffset, outLength);
                                writeOffset += outLength;
                                Array.Copy(extradata, 0, file, writeOffset, ch.ExtraBytes);
                                writeOffset += (int)ch.ExtraBytes;
                            }
                            else
                            {
                                // an uncompressed chunk, copy directly to output
                                Array.Copy(compressed, 0, file, writeOffset, ch.CompressedSize);
                                writeOffset += (int)ch.CompressedSize;
                            }
                        }
                        callbacks.WriteData(path, file);
                    }
                }
                else if (fse.Format == 2)
                {
                    // lzo
                    throw new Exception(String.Format("LZO decompression is not yet supported for file {0}", path));
                }
                else
                    throw new Exception(String.Format("Unknown compression format {0} for file {1}", fse.Format, path));
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
