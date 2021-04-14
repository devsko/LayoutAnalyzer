using System;
using System.Reflection;

namespace devsko.LayoutAnalyzer.Host
{
    public class MarshaledTypeLoader : MarshalByRefObject
    {
        private readonly Analyzer _analyzer;

        public MarshaledTypeLoader()
        {
            _analyzer = new Analyzer();
        }

        public Layout? LoadAndAnalyze(AssemblyName name, string typeName)
        {
            Assembly assembly = Assembly.Load(name.FullName);
            Type? type = assembly.GetType(typeName);

            if (type is null)
            {
                throw new InvalidOperationException($"Type not found {typeName}");
            }

            return _analyzer.Analyze(type);
        }
    }
}
