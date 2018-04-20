﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Parameters;
using BenchmarkDotNet.Running;

namespace BenchmarkDotNet.Exporters
{
    public class BenchViewExporter : JsonExporterBase
    {
        /// <summary>
        /// Utf8 with indent JSON
        /// </summary>
        public static readonly IExporter Default = new BenchViewExporter();

        /// <param name="encoding">if not provided, Utf8 will be used</param>
        /// <param name="benchmarkNameProvider">if not provided, the default implementation will be used</param>
        /// <param name="indentJson">true by default</param>
        public BenchViewExporter(Encoding encoding = null, Func<Benchmark, string> benchmarkNameProvider = null, bool indentJson = true) : base(indentJson, excludeMeasurements: false)
        {
            Encoding = encoding ?? Encoding.UTF8;
            BenchmarkNameProvider = benchmarkNameProvider ?? GetBenchmarkName;
        }

        protected override void BeforeSerialize(Dictionary<string, object> data, Benchmark benchmark) 
            => data["xUnitName"] = BenchmarkNameProvider(benchmark);

        protected override string FileNameSuffix => "-xunit";

        protected override Encoding Encoding { get; }

        private Func<Benchmark, string> BenchmarkNameProvider { get; }

        internal static string GetBenchmarkName(Benchmark benchmark)
        {
            var type = benchmark.Target.Type;
            var method = benchmark.Target.Method;

            // we can't just use type.FullName because we need sth different for generics (it reports SimpleGeneric`1[[System.Int32, mscorlib, Version=4.0.0.0)
            var name = new StringBuilder();

            if (!string.IsNullOrEmpty(type.Namespace))
                name.Append(type.Namespace).Append('.');

            name.Append(GetNestedTypes(type));

            name.Append(type.Name).Append('.');

            name.Append(method.Name);

            if (benchmark.HasArguments)
                name.Append(GetMethodArguments(method, benchmark.Parameters));

            return name.ToString();
        }

        private static string GetNestedTypes(Type type)
        {
            var nestedTypes = "";
            Type child = type, parent = type.DeclaringType;
            while (child.IsNested && parent != null)
            {
                nestedTypes = parent.Name + "+" + nestedTypes;

                child = parent;
                parent = parent.DeclaringType;
            }

            return nestedTypes;
        }

        private static string GetMethodArguments(MethodInfo method, ParameterInstances benchmarkParameters)
        {
            var methodParameters = method.GetParameters();
            var arguments = new StringBuilder(methodParameters.Length * 20).Append('(');

            for (int i = 0; i < methodParameters.Length; i++)
            {
                if (i > 0)
                    arguments.Append(", ");

                arguments.Append(methodParameters[i].Name).Append(':').Append(' ');
                arguments.Append(GetArgument(benchmarkParameters.GetArgument(methodParameters[i].Name).Value, methodParameters[i].ParameterType));
            }

            return arguments.Append(')').ToString();
        }

        private static string GetArgument(object argumentValue, Type argumentType)
        {
            if (argumentValue == null)
                return "null";

            if (argumentValue is string text)
                return $"\"{text}\"";
            if (argumentValue is char character)
                return $"'{character}'";
            if (argumentValue is DateTime time)
                return time.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffffffK");

            if (argumentType != null && argumentType.IsArray)
                return GetArray((IEnumerable)argumentValue);

            return argumentValue.ToString();
        }

        // it's not generic so I can't simply use .Skip and all other LINQ goodness
        private static string GetArray(IEnumerable collection)
        {
            var buffer = new StringBuilder().Append('[');

            int index = 0;
            foreach (var item in collection)
            {
                if (index > 0)
                    buffer.Append(", ");

                if (index > 4)
                {
                    buffer.Append("..."); // [0, 1, 2, 3, 4, ...]
                    break;
                }

                buffer.Append(GetArgument(item, item.GetType()));

                ++index;
            }

            buffer.Append(']');

            return buffer.ToString();
        }
    }
}
