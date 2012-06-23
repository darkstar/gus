using System;
using System.Collections.Generic;
using UnpackShell.Interfaces;
using System.Reflection;

namespace UnpackShell
{
    public class DataTransformerRegistry : IDataTransformerRegistry
    {
        public Dictionary<string, Type> m_transformers = new Dictionary<string, Type>();

        public void AddTransformer(IDataTransformer trn)
        {
            m_transformers.Add(trn.GetName(), trn.GetType());
        }

        public IDataTransformer GetTransformer(string name)
        {
            Type t = m_transformers[name];
            object o = t.GetConstructor(new Type[] { }).Invoke(new object[] { });

            // instantiate a new object for the given type
            return o as IDataTransformer;
        }
    }
}
