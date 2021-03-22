using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Test
{
    public enum TargetFramework
    {
        NetFramework,
        NetCore,
        Net,
    }

    public enum Platform
    {
        x64,
        x86,
    }

    public sealed class HostRunner
    {
        private static readonly Dictionary<Token, ConsoleColor> s_colorMap = new()
        {
            { Token.Namespace, ConsoleColor.Gray },
            { Token.StructRef, ConsoleColor.Green },
            { Token.ClassRef, ConsoleColor.Blue },
            { Token.Keyword, ConsoleColor.Cyan },
            { Token.Identifier, ConsoleColor.DarkYellow },
            { Token.Symbol, ConsoleColor.White },
        };

        public static async Task Main()
        {
            const string projectAssembly = "C:\\Users\\stefa\\source\\repos\\LayoutAnalyzer\\devsko.LayoutAnalyzer.Test\\bin\\Debug\\net5.0\\devsko.LayoutAnalyzer.Test.dll";
            const string typeName = "devsko.LayoutAnalyzer.Test.TestClass, devsko.LayoutAnalyzer.Test";

            HostRunner runner = new(TargetFramework.Net, Platform.x86);
            var layout = await runner.SendAsync(projectAssembly + '|' + typeName).ConfigureAwait(false);

            if (layout is not null)
            {
                Inspect(layout);
            }
        }

        private unsafe static void Inspect(Layout layout)
        {
            Console.BackgroundColor = ConsoleColor.DarkGray;

            Console.WriteLine($"Total size:    {layout.TotalSize} bytes");
            Console.WriteLine($"Total padding: {layout.TotalPadding} bytes");
            if (layout.AttributeKind != (layout.IsValueType ? LayoutKind.Sequential : LayoutKind.Auto) || layout.AttributeSize != 0)
            {
                Console.Write($"Layout:        {layout.AttributeKind}");
                if (layout.AttributeSize != 0)
                {
                    Console.Write($", Size = {layout.AttributeSize}");
                }
                if (layout.AttributePack != 0)
                {
                    Console.Write($", Pack = {layout.AttributePack}");
                }
                Console.WriteLine();
            }
            Console.WriteLine();

            Console.Write(layout.IsValueType ? "struct " : "class ");
            Write(layout.Name);
            Console.WriteLine();

            WriteFields(layout.FieldsWithPaddings);

            Console.WriteLine();
            Console.BackgroundColor = ConsoleColor.Black;
            Console.WriteLine();

            static void WriteFields(IEnumerable<(FieldBase Field, int TotalOffset, int Level)> fieldsAndPaddings)
            {
                int level = 0;
                string indent = "";
                foreach (var fieldOrPadding in fieldsAndPaddings)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write($"{fieldOrPadding.Field.Offset:X3} : {fieldOrPadding.Field.Size,-3}    ");

                    Console.ForegroundColor = ConsoleColor.White;
                    while (level < fieldOrPadding.Level)
                    {
                        Console.WriteLine($"{indent}{{");
                        level++;
                        indent = new string(' ', level * 4);
                    }
                    while (level > fieldOrPadding.Level)
                    {
                        level--;
                        indent = new string(' ', level * 4);
                        Console.WriteLine($"{indent}}}");
                    }

                    if (fieldOrPadding.Field is Field field)
                    {
                        Console.Write($"{indent}{(field.IsPublic ? "public" : "private")} ");
                        Write(field.TypeName);
                        Console.Write(' ');
                        Console.WriteLine(field.Name);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"{indent}=== PADDING ({fieldOrPadding.Field.Size} bytes) ===");
                    }
                }
                Console.ForegroundColor = ConsoleColor.White;
                while (level > 0)
                {
                    level--;
                    indent = new string(' ', level * 4);
                    Console.WriteLine($"{indent}}}");
                }
            }

            static void Write(TokenizedString value)
            {
                ReadOnlySpan<char> chars = value.Value.AsSpan();
                foreach (TokenSpan span in value.Tokens)
                {
                    Console.ForegroundColor = s_colorMap[span.Token];
                    Console.Write(chars.Slice(0, span.Length).ToString());
                    chars = chars[span.Length..];
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private Process _process;
        private JsonSerializerOptions _jsonOptions;
        private SemaphoreSlim _semaphore;

        public HostRunner(TargetFramework framework, Platform platform)
        {
            _jsonOptions = new JsonSerializerOptions();
            _semaphore = new SemaphoreSlim(1);

            (string frameworkDirectory, string fileExtension) = framework switch
            {
                TargetFramework.NetFramework => ("net472", "exe"),
                TargetFramework.NetCore => ("netcoreapp3.1", "dll"),
                TargetFramework.Net => ("net5.0", "exe"),
                _ => throw new ArgumentException("", nameof(framework))
            };
            (string platformDirectory, string programFilesDirectory) = platform switch
            {
                Platform.x64 => ("x64", "%ProgramW6432%"),
                Platform.x86 => ("x86", "%ProgramFiles(x86)%"),
                _ => throw new ArgumentException("", nameof(platform))
            };

            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
#if DEBUG
            string hostAssemblyPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisAssemblyPath)!,
                "..", "..", "..", "..", "devsko.LayoutAnalyzer.Host", "bin", platformDirectory, "Debug", frameworkDirectory, Path.ChangeExtension("devsko.LayoutAnalyzer.Host.", fileExtension)));
#else
            throw new NotImplementedException();
#endif
            string exePath;
            string arguments;
            if (Path.GetExtension(hostAssemblyPath) == ".exe")
            {
                exePath = hostAssemblyPath;
                arguments = "";
            }
            else
            {
                exePath = Path.Combine(Environment.ExpandEnvironmentVariables(programFilesDirectory), "dotnet", "dotnet.exe");
                if (!File.Exists(exePath))
                {
                    throw new InvalidOperationException($"Install .NET ('{exePath}' not found.)");
                }
                arguments = hostAssemblyPath;
            }
#if DEBUG
            //arguments += " -wait";
#endif

            ProcessStartInfo start = new()
            {
                FileName = exePath,
                Arguments = arguments,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(hostAssemblyPath)!,
            };

            Process? process = Process.Start(start);
            if (process is null)
            {
                throw new InvalidOperationException("could not start new host");
            }

            _process = process;
            _process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
            _process.BeginErrorReadLine();
        }

        public async Task<Layout?> SendAsync(string command, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _process.StandardInput.WriteLine(command);

//                var buffer = new byte[1024];
//#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'
//                var result = await _process.StandardOutput.BaseStream.ReadAsync(buffer, 0, 1024, cancellationToken).ConfigureAwait(false);
//#pragma warning restore CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

                return await JsonSerializer.DeserializeAsync<Layout?>(new StreamWrapper(_process.StandardOutput.BaseStream), _jsonOptions, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private class StreamWrapper : Stream
        {
            private Stream _stream;
            private bool _endDetected;

            public StreamWrapper(Stream stream)
            {
                _stream = stream;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_endDetected)
                {
                    return 0;
                }
#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'
                int result = await _stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'
                if (result == 0)
                {
                    _endDetected = true;
                    return 0;
                }
                else
                {
                    _endDetected |= buffer[offset + result - 1] == '\n';
                }

                return _endDetected ? result - 1 : result;
            }

#if NETCOREAPP3_1_OR_GREATER
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_endDetected)
                {
                    return 0;
                }
                int result = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result == 0)
                {
                    _endDetected = true;
                    return 0;
                }
                else
                {
                    Memory<byte> memory = buffer.Slice(result - 1);
                    _endDetected |= memory.Span[0] == (byte)'\n';
                }

                return _endDetected ? result - 1 : result;
            }
#endif

            #region 
            public override bool CanRead => _stream.CanRead;

            public override bool CanSeek => _stream.CanSeek;

            public override bool CanWrite => _stream.CanWrite;

            public override long Length => _stream.Length;

            public override long Position { get => _stream.Position; set => _stream.Position = value; }

            public override void Flush() => _stream.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
            public override void SetLength(long value) => _stream.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);
            #endregion
        }
    }
}
