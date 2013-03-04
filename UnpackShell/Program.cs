using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using UnpackShell.Interfaces;
using UnpackShell.Shared;

namespace UnpackShell
{
    class Program
    {
        CompositionContainer m_compositionContainer;

        [ImportMany]
        IEnumerable<IDataTransformer> m_transformers = null;

        [ImportMany]
        IEnumerable<IUnpacker> m_unpackers = null;

        DataTransformerRegistry m_registry = null;

        string destDir = null;

        void InitializeMEF()
        {
            // scan current assembly and app dir for all exported types
            AggregateCatalog cat = new AggregateCatalog(
                new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly()),
                new DirectoryCatalog(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)));

            m_compositionContainer = new CompositionContainer(cat);

            m_compositionContainer.ComposeParts(this);
        }

        void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  gus list [-lf listfile] [-t type] file");
            Console.WriteLine("  gus unpack [-lf listfile] [-t type] [-d dstdir] file");
            Console.WriteLine("  gus pack [-lf listfile] -t type [-d srcdir] file [filespec]");
            Console.WriteLine("  gus crc file");
            Console.WriteLine("  gus [options]");
            Console.WriteLine("Available options:");
            Console.WriteLine("  -? -h --help     Show this usage");
            Console.WriteLine("  -ll   --listall  List available unpack modules");
            Console.WriteLine("        --test     Run unittests (developers only)");
            Console.WriteLine("  -d <dstdir>      Set destination directory for extraction (will be created)");
            Console.WriteLine("  -lf listfile     Set listfile name. ");
            Console.WriteLine("                   Will be created on list/unpack and used on pack");
            Console.WriteLine("  -t type          Set the unpacker/packer plugin to use. Use -ll to list.");
            Console.WriteLine("                   Required on pack, optional (autodetected) on list/unpack");
            Console.WriteLine("Either [filespec] or [-lf listfile] MUST be given on pack");
        }

        static void Main(string[] args)
        {
            Program p = new Program();

            p.Run(args);
        }

        IUnpacker GetUnpackerByName(string name)
        {
            foreach (IUnpacker unp in m_unpackers)
            {
                if (unp.GetName() == name)
                    return unp;
            }

            return null;
        }

        IUnpacker FindUnpacker(Stream strm)
        {
            Callbacks cb = new Callbacks();
            cb.TransformerRegistry = m_registry;
            cb.WriteData = new Callbacks.WriteDataDelegate(WriteDataDummy);

            foreach (IUnpacker unp in m_unpackers)
            {
                strm.Seek(0, SeekOrigin.Begin);
                if (unp.IsSupported(strm, cb))
                    return unp;
            }

            return null;
        }

        string GetUnpackerFlags(IUnpacker unp)
        {
            UnpackerFlags flg = unp.GetFlags();
            string result;

            result = String.Format("{0}{1}{2}{3}{4}",
                flg.HasFlag(UnpackerFlags.SupportsPack) ? "P" : "-",
                flg.HasFlag(UnpackerFlags.SupportsSubdirectories) ? "S" : "-",
                flg.HasFlag(UnpackerFlags.SupportsTimestamps) ? "T" : "-",
                flg.HasFlag(UnpackerFlags.NoFilenames) ? "-" : "N",
                flg.HasFlag(UnpackerFlags.Experimental) ? "X" : "-");

            return result;
        }

        void DoCRC(string fname)
        {
            BinaryReader strm;
            if (!File.Exists(fname))
            {
                Console.WriteLine("CRC: File {0} not found", fname);
                return;
            }
            strm = new BinaryReader(new FileStream(fname, FileMode.Open, FileAccess.Read));
            byte[] buf = new byte[strm.BaseStream.Length];
            strm.Read(buf, 0, buf.Length);

            Console.WriteLine("CRC Algorithm            CRC");
            Console.WriteLine("-------------------------------------");
            foreach (string s in CRC.AllCRCMethods)
            {
                ICRCAlgorithm algo = CRC.Create(s);
                Console.WriteLine("{0,-25}0x{1:x10}", s, algo.CalculateCRC(buf, buf.Length));
            }
        }

        void Run(string[] args)
        {
            string[] helpOptions = new string[] { "-?", "-h", "--help" };
            string[] listallOptions = new string[] { "-ll", "--listall" };
            string fileName = null;
            string mode = "";
            IUnpacker unpacker = null;
            string listFile = null;

            Console.WriteLine("[GUS] - General Unpack Shell  v1.0");
            Console.WriteLine("(C) 2012 by Darkstar <darkstar@drueing.de>");
            Console.WriteLine();
            InitializeMEF();

            RegisterDataTransformers();

            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            if (args[0].ToLower() == "pack")
                mode = "pack";
            if (args[0].ToLower() == "unpack")
                mode = "unpack";
            if (args[0].ToLower() == "list")
                mode = "list";
            if (args[0].ToLower() == "crc")
            {
                if (args.Length > 1)
                    DoCRC(args[1]);
                else
                {
                    Console.WriteLine("CRC command needs a filename as argument");
                }
                return;
            }

            for (int i = (mode == "" ? 0 : 1); i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("-"))
                {
                    if (helpOptions.Contains(arg.ToLower()))
                    {
                        PrintUsage();
                        return;
                    }
                    if (listallOptions.Contains(arg.ToLower()))
                    {
                        Console.WriteLine("Registered Data Transformers:");
                        foreach (IDataTransformer trn in m_transformers)
                        {
                            Console.WriteLine("{0,-20} {1,-40} v{2}", trn.GetName(), trn.GetDescription(), trn.GetVersion());
                        }
                        Console.WriteLine();
                        Console.WriteLine("Registered Unpackers:");
                        foreach (IUnpacker unp in m_unpackers)
                        {
                            Console.WriteLine("{0,-20} {1,-40} v{2} [{3}]", unp.GetName(), unp.GetDescription(), unp.GetVersion(), GetUnpackerFlags(unp));
                        }
                        return;
                    }
                    if (arg == "--test")
                    {
                        UnitTests tests = new UnitTests();
                        tests.DoTests();
                        return;
                    }
                    if (arg == "-d")
                    {
                        if (++i >= args.Length)
                        {
                            Console.WriteLine("Option -d requires an argument");
                            return;
                        }
                        destDir = args[i];
                        continue;
                    }
                    if (arg == "-t")
                    {
                        if (++i >= args.Length)
                        {
                            Console.WriteLine("Option -t requires an argument");
                            return;
                        }
                        unpacker = GetUnpackerByName(args[i]);
                        if (unpacker == null)
                        {
                            Console.WriteLine("Unpacker '{0}' not found. Use 'gus -ll' for supported unpackers.", args[i]);
                            return;
                        }
                        continue;
                    }
                    if (arg == "-lf")
                    {
                        if (++i >= args.Length)
                        {
                            Console.WriteLine("Option -lf requires an argument");
                            return;
                        }
                        listFile = args[i];
                        continue;
                    }
                    Console.WriteLine("Invalid option '{0}' given", args[i]);
                    return;
                }
                else
                {
                    if (fileName == null)
                    {
                        fileName = arg;
                    }
                    else
                    {
                        Console.WriteLine("Error: Only one filename may be given!");
                        return;
                    }
                }
            }

            if (fileName == null)
            {
                Console.WriteLine("No filename given");
                return;
            }

            Callbacks cb = new Callbacks();
            cb.TransformerRegistry = m_registry;
            cb.WriteData = new Callbacks.WriteDataDelegate(WriteData);
            cb.ReadData = new Callbacks.ReadDataDelegate(ReadData);
            cb.GetCRCAlgorithm = new Callbacks.GetCRCAlgorithmDelegate(GetCRCAlgorithm);
            //cb.GetFileSize = new Callbacks.GetFileSizeDelegate(GetFileSize);

            if (mode == "list" || mode == "unpack")
            {
                if (!File.Exists(fileName))
                {
                    Console.WriteLine("File '{0}' not found", fileName);
                    return;
                }
                using (Stream str = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    if (unpacker == null)
                    {
                        unpacker = FindUnpacker(str);
                        if (unpacker != null)
                            Console.WriteLine("...file identified as {0}", unpacker.GetDescription());
                        else
                        {
                            Console.WriteLine("No suitable unpacker found for file '{0}'.", fileName);
                            return;
                        }
                    }
                    else
                    {
                        str.Seek(0, SeekOrigin.Begin);
                        if (!unpacker.IsSupported(str, cb))
                        {
                            Console.WriteLine("The specified unpacker '{0}' cannot unpack that file", unpacker.GetName());
                            return;
                        }
                    }

                    str.Seek(0, SeekOrigin.Begin);

                    if (listFile != null)
                    {
                        using (StreamWriter listFileStream = new StreamWriter(new FileStream(listFile, FileMode.Create, FileAccess.Write)))
                        {
                            IEnumerable<FileEntry> files;
                            files = unpacker.ListFiles(str, cb);
                            foreach (FileEntry f in files)
                            {
                                listFileStream.WriteLine(f.Filename);
                            }
                            str.Seek(0, SeekOrigin.Begin);
                        }
                    }

                    if (mode == "list")
                    {
                        IEnumerable<FileEntry> files;
                        files = unpacker.ListFiles(str, cb);
                        Console.WriteLine("{0,-60} {1,10}", "Filename", "Size");
                        Console.WriteLine("{0} {1}", new String('=', 60), new String('=', 10));
                        foreach (FileEntry f in files)
                        {
                            Console.WriteLine("{0,-60} {1,10}", f.Filename, f.UncompressedSize);
                        }
                    }
                    else
                    {
                        unpacker.UnpackFiles(str, cb);
                    }
                }
            }
            else if (mode == "pack")
            {
                if (unpacker == null)
                {
                    Console.WriteLine("You need to specify a packer module (-t ...) when packing.");
                    return;
                }

                if (!unpacker.GetFlags().HasFlag(UnpackerFlags.SupportsPack))
                {
                    Console.WriteLine("The unpacker '{0}' does not support packing", unpacker.GetName());
                    return;
                }

                List<PackFileEntry> packFiles = new List<PackFileEntry>();
                if (listFile != null)
                {
                    StreamReader sr = new StreamReader(listFile);
                    while (!sr.EndOfStream)
                    {
                        string l = sr.ReadLine();
                        PackFileEntry pfe = new PackFileEntry();
                        if (l.Length == 0)
                            continue;

                        pfe.relativePathName = l;
                        //pfe.fullPathName = Path.Combine(destDir, pfe.relativePathName);
                        string fullPathName = Path.Combine(destDir, pfe.relativePathName);
                        pfe.fileSize = (new FileInfo(fullPathName)).Length;
                        packFiles.Add(pfe);
                    }
                }
                else
                {
                    Console.WriteLine("Not yet implemented");
                    return;
                }

                // do the packing
                Console.WriteLine("Packing {0} as {1}...", fileName, unpacker.GetName());
                using (Stream ostrm = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    unpacker.PackFiles(ostrm, packFiles, cb);
                }
            }
            else
            {
                // this should not happen at all...
                Console.WriteLine("No mode specified. Use 'gus -h' for help.");
                return;
            }
        }

        void RegisterDataTransformers()
        {
            m_registry = new DataTransformerRegistry();

            foreach (IDataTransformer trans in m_transformers)
            {
                m_registry.AddTransformer(trans);
            }
        }

        public void WriteData(string relativeFileName, byte[] data)
        {
            string fullFilePath;

            if (destDir == null)
                fullFilePath = relativeFileName;
            else
                fullFilePath = destDir + "/" + relativeFileName;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullFilePath));
            }
            catch
            {
            }
            Console.WriteLine("Writing file {0}...", fullFilePath);
            using (FileStream fs = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write))
            {
                fs.Write(data, 0, data.Length);
            }
        }

        public void WriteDataDummy(string relativeFileName, byte[] data)
        {
            return;
        }

        public byte[] ReadData(string relativeFileName)
        {
            string fullPath;

            if (destDir == null)
                fullPath = relativeFileName;
            else
                fullPath = destDir + "/" + relativeFileName;

            return File.ReadAllBytes(fullPath);
        }

        public ICRCAlgorithm GetCRCAlgorithm(string algo)
        {
            return Shared.CRC.Create(algo);
        }
    }
}
