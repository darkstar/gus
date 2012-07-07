using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class KOMUnpacker : IUnpacker
    {
        const string KOM_ID = "KOG GC TEAM MASSFILE V.0.3.";

        int numFiles;
        int xmlSize;
        System.Xml.XmlDocument fileList;

        public string GetName()
        {
            return "elsword.kom";
        }

        public string GetDescription()
        {
            return "ElSword KOM file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental;
        }

        bool ReadHeader(Stream strm)
        {
            byte[] buf = new byte[27];
            string id;
            BinaryReader rd = new BinaryReader(strm);

            strm.Read(buf, 0, 27);
            id = System.Text.Encoding.ASCII.GetString(buf);
            if (id != KOM_ID)
                return false;

            strm.Seek(0x34, SeekOrigin.Begin);
            numFiles = rd.ReadInt32();

            strm.Seek(0x44, SeekOrigin.Begin);
            xmlSize = rd.ReadInt32();

            byte[] xml = rd.ReadBytes(xmlSize);

            fileList = new System.Xml.XmlDocument();
            fileList.LoadXml(System.Text.Encoding.ASCII.GetString(xml));

            // check for unknown algorithms
            foreach (System.Xml.XmlNode elt in fileList.SelectNodes("Files/File/@Algorithm"))
            {
                if (elt.InnerText != "0")
                    return false;
            }
            return true;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return ReadHeader(strm);
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            List<FileEntry> results = new List<FileEntry>();

            if (!ReadHeader(strm))
                return null;

            foreach (System.Xml.XmlElement elt in fileList.SelectNodes("Files/File"))
            {
                FileEntry f = new FileEntry();
                f.Filename = elt.SelectNodes("@Name").Item(0).InnerText;
                f.UncompressedSize = Convert.ToInt64(elt.SelectNodes("@Size").Item(0).InnerText);
                results.Add(f);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            string dstFileName;
            int csize, usize, actualsize;
            BinaryReader rd = new BinaryReader(strm);
            IDataTransformer decompressor = callbacks.TransformerRegistry.GetTransformer("zlib_dec");

            if (!ReadHeader(strm))
                return;

            // the order is important, that's why we don't use "foreach" here...
            System.Xml.XmlNodeList allFiles = fileList.SelectNodes("Files/File");
            for (int i = 0; i < allFiles.Count; i++)
            {
                System.Xml.XmlNode elt = allFiles.Item(i);

                dstFileName = elt.SelectNodes("@Name").Item(0).InnerText;
                usize = Convert.ToInt32(elt.SelectNodes("@Size").Item(0).InnerText);
                csize = Convert.ToInt32(elt.SelectNodes("@CompressedSize").Item(0).InnerText);

                byte[] cdata = rd.ReadBytes(csize);
                byte[] udata = new byte[usize];
                actualsize = usize;
                decompressor.TransformData(cdata, udata, csize, ref actualsize);
                callbacks.WriteData(dstFileName, udata);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException("packing not supported");
        }
    }
}
