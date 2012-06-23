using System;
using System.IO;
using UnpackShell.Interfaces;

namespace UnpackShell.Shared
{
    public class XORStream : Stream
    {
        private Stream _baseStream;
        private IDataTransformer _transformer;

        public override bool CanRead
        {
            get { return _baseStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _baseStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _baseStream.CanWrite; }
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override long Length
        {
            get { return _baseStream.Length; }
        }

        public override long Position
        {
            get
            {
                return _baseStream.Position;
            }
            set
            {
                _baseStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int ocount = count;
            int result;
            byte[] tmp = new byte[count];
            
            result = _baseStream.Read(tmp, 0, count);
            _transformer.TransformData(tmp, tmp, count, ref ocount);
            tmp.CopyTo(buffer, offset);

            return result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            byte[] tmp = new byte[count];
            int ocount = count;

            Array.Copy(buffer, offset, tmp, 0, count);
            _transformer.TransformData(tmp, tmp, count, ref ocount);

            _baseStream.Write(tmp, 0, count);
        }

        public XORStream(Stream basestream, IDataTransformerRegistry reg, byte value)
        {
            _baseStream = basestream;
            _transformer = reg.GetTransformer("xor");
            _transformer.SetOption("value", value);
        }
    }
}
