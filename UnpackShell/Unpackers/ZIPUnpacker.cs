using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;
using Ionic.Zip;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class ZIPUnpacker : IUnpacker
    {
        public ZIPUnpacker()
        {
            SetupZIPParams();
        }

        protected virtual void SetupZIPParams()
        {
            // use the regular ZIP file magics
            ZipConstants.PackedToRemovableMedia = 0x30304b50;
            ZipConstants.Zip64EndOfCentralDirectoryRecordSignature = 0x06064b50;
            ZipConstants.Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
            ZipConstants.EndOfCentralDirectorySignature = 0x06054b50;
            ZipConstants.ZipEntrySignature = 0x04034b50;
            ZipConstants.ZipEntryDataDescriptorSignature = 0x08074b50;
            ZipConstants.SplitArchiveSignature = 0x08074b50;
            ZipConstants.ZipDirEntrySignature = 0x02014b50;
        }

        public virtual string GetName()
        {
            return "zip";
        }

        public virtual string GetDescription()
        {
            return "generic ZIP file";
        }

        public virtual string GetVersion()
        {
            return "1.0";
        }

        public virtual UnpackerFlags GetFlags()
        {
            return UnpackerFlags.SupportsPack | UnpackerFlags.SupportsSubdirectories | UnpackerFlags.SupportsTimestamps;
        }

        private bool ReadEntries(ZipInputStream input)
        {
            ZipEntry e;

            try
            {
                while ((e = input.GetNextEntry()) != null)
                {
                    if (e.IsDirectory) continue;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            SetupZIPParams();
            BinaryReader rd = new BinaryReader(strm);
            Int32 sig = rd.ReadInt32();
            return (sig == ZipConstants.ZipEntrySignature);
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            ZipEntry ze;
            ZipInputStream zstrm = new ZipInputStream(strm, true);
            List<FileEntry> results = new List<FileEntry>();
            FileEntry fe;

            SetupZIPParams();
            while (true)
            {
                ze = zstrm.GetNextEntry();
                if (ze == null)
                    break;

                if (ze.IsDirectory)
                    continue;

                fe = new FileEntry();
                fe.Filename = ze.FileName;
                fe.UncompressedSize = ze.UncompressedSize;
                fe.Timestamp = ze.LastModified;
                results.Add(fe);
            };

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            ZipEntry ze;
            ZipInputStream zstrm = new ZipInputStream(strm, true);
            List<FileEntry> results = new List<FileEntry>();

            SetupZIPParams();
            while (true)
            {
                ze = zstrm.GetNextEntry();
                if (ze == null)
                    break;

                if (ze.IsDirectory)
                    continue;

                byte[] data = new byte[ze.UncompressedSize];
                zstrm.Read(data, 0, (int)ze.UncompressedSize);
                callbacks.WriteData(ze.FileName, data);
            };
        }

        public void PackFiles(Stream strm, List<string> fullPathNames, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
