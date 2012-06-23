using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.ComponentModel.Composition;
using UnpackShell.Interfaces;

namespace UnpackShell.DataTransformers
{
    [Export(typeof(IDataTransformer))]
    public class XORDataTransformer : IDataTransformer
    {
        byte Value = 0xff;

        public string GetName()
        {
            return "xor";
        }

        public string GetDescription()
        {
            return "xor data transformer";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public void SetOption(string option, object value)
        {
            if (option == "value")
            {
                Value = Convert.ToByte(value);
            }
        }

        public TransformationResult TransformData(byte[] InBuffer, byte[] OutBuffer, int InLength, ref int OutLength)
        {
            if (InLength != OutLength)
                return TransformationResult.BufferTooSmall;

            for (int i = 0; i < OutLength; i++)
            {
                OutBuffer[i] = (byte)((InBuffer[i] ^ Value) & 0xff);
            }

            return TransformationResult.OK;
        }
    }
}
