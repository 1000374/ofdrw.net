using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ionic.Zip;
using Ofdrw.Net.Converter.Abstractions.Interfaces;
using Ofdrw.Net.Converter.Svg.Converters;
using Ofdrw.Net.Core.Compatibility;
using Ofdrw.Net.Core.Models;
using Ofdrw.Net.Layout.Builders;
using Ofdrw.Net.Packaging;
using Ofdrw.Net.Packaging.Archive;
using Ofdrw.Net.Packaging.Validation;
using Ofdrw.Net.Reader.Extraction;
using Ofdrw.Net.Reader.Readers;
using Ofdrw.Net.Signatures.Crypto;
using Ofdrw.Net.Signatures.Signing;
using Ofdrw.Net.Signatures.Verification;

namespace Ofdrw.Net.Net40.SmokeTests;

internal static class Program
{
    private static int _passedCount;
    private static int _failedCount;

    private readonly struct TargetTypeEntry
    {
        public readonly string Name;
        public readonly Type Type;

        public TargetTypeEntry(string name, Type type)
        {
            Name = name;
            Type = type;
        }
    }

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("===============================================================");
        Console.WriteLine(" Ofdrw.Net .NET Framework 4.0 (net40) Comprehensive Smoke Tests");
        Console.WriteLine("===============================================================");
        Console.WriteLine("CLR Runtime Version: " + Environment.Version);
        Console.WriteLine("OS Version: " + Environment.OSVersion);
        Console.WriteLine();

        await RunTestAsync("1. Target Framework Runtime Assertion (.NETFramework,Version=v4.0)", TestTargetFrameworks);
        await RunTestAsync("2. Document Creation and Packaging Write", TestDocumentCreationAndPackaging);
        await RunTestAsync("3. Package Re-reading and Text Extraction", TestReReadingAndTextExtraction);
        await RunTestAsync("4. Page Operations (Add, Modify, Reorder)", TestPageOperations);
        await RunTestAsync("5. SVG Vector Export", TestSvgExport);
        await RunTestAsync("6. SM3 and SHA-256 Digest Test Vectors", TestCryptographicDigests);
        await RunTestAsync("7. Digital Signature Creation and Reference Integrity", TestDigitalSignatureCreationAndVerification);
        await RunTestAsync("8. Non-Seekable Stream Reading", TestNonSeekableStreamReading);
        await RunTestAsync("9. Directory Traversal Security Protection", TestPathTraversalRejection);
        await RunTestAsync("10. Package Structure Verification", TestPackageStructureValidation);

        Console.WriteLine();
        Console.WriteLine("===============================================================");
        Console.WriteLine($" Summary: {_passedCount} passed, {_failedCount} failed, total {_passedCount + _failedCount}");
        Console.WriteLine("===============================================================");

        return _failedCount == 0 ? 0 : 1;
    }

    private static async Task RunTestAsync(string testName, Func<Task> testFunc)
    {
        Console.Write($"[TEST] {testName} ... ");
        try
        {
            await testFunc();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
            _passedCount++;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
            Console.WriteLine($"  Stack: {ex.StackTrace}");
            _failedCount++;
        }
    }

    private static Task TestTargetFrameworks()
    {
        var targetTypes = new[]
        {
            new TargetTypeEntry("Ofdrw.Net.Core", typeof(OfdDocumentPackage)),
            new TargetTypeEntry("Ofdrw.Net.Packaging", typeof(OfdPackageWriter)),
            new TargetTypeEntry("Ofdrw.Net.Reader", typeof(OfdReader)),
            new TargetTypeEntry("Ofdrw.Net.Layout", typeof(OfdDocumentBuilder)),
            new TargetTypeEntry("Ofdrw.Net.Converter.Abstractions", typeof(IDocxToOfdConverter)),
            new TargetTypeEntry("Ofdrw.Net.Converter.Svg", typeof(OfdToSvgConverter)),
            new TargetTypeEntry("Ofdrw.Net.Signatures", typeof(OfdSignatureService))
        };

        foreach (var entry in targetTypes)
        {
            var assembly = entry.Type.Assembly;
            var tfa = (TargetFrameworkAttribute?)assembly
                .GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
                .FirstOrDefault();

            if (tfa == null)
            {
                throw new InvalidOperationException($"Assembly {entry.Name} does not contain TargetFrameworkAttribute.");
            }

            if (!tfa.FrameworkName.StartsWith(".NETFramework,Version=v4.0", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Assembly {entry.Name} is targeting '{tfa.FrameworkName}', expected '.NETFramework,Version=v4.0'!");
            }

            Console.Write($"[{entry.Name} -> {tfa.FrameworkName}] ");
        }

        return TaskCompat.CompletedTask;
    }

    private static async Task TestDocumentCreationAndPackaging()
    {
        var package = CreateSamplePackage("Page 1 Content");
        var writer = new OfdPackageWriter();

        using var ms = new MemoryStream();
        await writer.WriteAsync(package, ms).ConfigureAwait(false);

        if (ms.Length == 0)
        {
            throw new InvalidOperationException("Package write produced 0 bytes.");
        }

        ms.Position = 0;
        var loader = new OfdPackageLoader();
        var archive = await loader.LoadAsync(ms).ConfigureAwait(false);

        if (!archive.Contains("OFD.xml")) throw new InvalidOperationException("Missing OFD.xml");
        if (!archive.Contains("Doc_0/Document.xml")) throw new InvalidOperationException("Missing Doc_0/Document.xml");
        if (!archive.Contains("Doc_0/Pages/Page_0/Content.xml")) throw new InvalidOperationException("Missing Doc_0/Pages/Page_0/Content.xml");
    }

    private static async Task TestReReadingAndTextExtraction()
    {
        var expectedText = "Hello net40 OFD Text Extraction";
        var package = CreateSamplePackage(expectedText);

        using var ms = new MemoryStream();
        await new OfdPackageWriter().WriteAsync(package, ms).ConfigureAwait(false);

        ms.Position = 0;
        var reader = new OfdReader();
        var readPackage = await reader.ReadAsync(ms).ConfigureAwait(false);

        if (readPackage.Pages.Count != 1)
        {
            throw new InvalidOperationException($"Expected 1 page, got {readPackage.Pages.Count}");
        }

        var extractor = new OfdTextExtractor();
        var extracted = extractor.Extract(readPackage);

        if (!extracted.Contains(expectedText))
        {
            throw new InvalidOperationException($"Extracted text '{extracted}' does not contain expected '{expectedText}'.");
        }
    }

    private static async Task TestPageOperations()
    {
        var package = CreateSamplePackage("Initial Page");

        // Add second page
        var page2 = new OfdPage
        {
            Index = 1,
            WidthMillimeters = 210,
            HeightMillimeters = 297,
            Elements =
            {
                new OfdTextElement
                {
                    Text = "Second Added Page",
                    FontName = "SimSun",
                    FontSizeMillimeters = 5,
                    XMillimeters = 15,
                    YMillimeters = 25
                }
            }
        };
        package.Pages.Add(page2);

        // Reorder: swap page 0 and page 1
        var temp = package.Pages[0];
        package.Pages[0] = package.Pages[1];
        package.Pages[1] = temp;
        package.Pages[0].Index = 0;
        package.Pages[1].Index = 1;

        using var ms = new MemoryStream();
        await new OfdPackageWriter().WriteAsync(package, ms).ConfigureAwait(false);

        ms.Position = 0;
        var readPackage = await new OfdReader().ReadAsync(ms).ConfigureAwait(false);

        if (readPackage.Pages.Count != 2)
        {
            throw new InvalidOperationException($"Expected 2 pages after reorder, got {readPackage.Pages.Count}");
        }

        var extractor = new OfdTextExtractor();
        var page0Text = extractor.ExtractPages(readPackage)[0];
        var page1Text = extractor.ExtractPages(readPackage)[1];

        if (!page0Text.Contains("Second Added Page"))
        {
            throw new InvalidOperationException($"Page 0 should contain 'Second Added Page', got '{page0Text}'");
        }
        if (!page1Text.Contains("Initial Page"))
        {
            throw new InvalidOperationException($"Page 1 should contain 'Initial Page', got '{page1Text}'");
        }
    }

    private static async Task TestSvgExport()
    {
        var package = CreateSamplePackage("SVG Export Test Page");

        using var ofdStream = new MemoryStream();
        await new OfdPackageWriter().WriteAsync(package, ofdStream).ConfigureAwait(false);

        ofdStream.Position = 0;
        using var svgStream = new MemoryStream();
        var converter = new OfdToSvgConverter();
        await converter.ConvertAsync(ofdStream, svgStream, 0).ConfigureAwait(false);

        svgStream.Position = 0;
        var svgContent = Encoding.UTF8.GetString(svgStream.ToArray());

        if (!svgContent.Contains("<svg") || !svgContent.Contains("http://www.w3.org/2000/svg"))
        {
            throw new InvalidOperationException("Exported SVG does not contain valid svg root element.");
        }

        if (!svgContent.Contains("SVG Export Test Page"))
        {
            throw new InvalidOperationException("Exported SVG missing text content.");
        }
    }

    private static Task TestCryptographicDigests()
    {
        // 1. SM3 Test Vector 1: "abc"
        var sm3Input1 = Encoding.ASCII.GetBytes("abc");
        var sm3Actual1 = ToHex(OfdDigestAlgorithms.Compute("sm3", sm3Input1));
        var sm3Expected1 = "66c7f0f462eeedd9d1f2d46bdc10e4e24167c4875cf2f7a2297da02b8f4ba8e0";
        if (!string.Equals(sm3Actual1, sm3Expected1, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SM3 Vector 1 mismatch: got {sm3Actual1}, expected {sm3Expected1}");
        }

        // 2. SM3 Test Vector 2: 64-byte repeated string
        var sm3Input2 = Encoding.ASCII.GetBytes("abcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcd");
        var sm3Actual2 = ToHex(OfdDigestAlgorithms.Compute("sm3", sm3Input2));
        var sm3Expected2 = "debe9ff92275b8a138604889c18e5a4d6fdb70e5387e5765293dcba39c0c5732";
        if (!string.Equals(sm3Actual2, sm3Expected2, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SM3 Vector 2 mismatch: got {sm3Actual2}, expected {sm3Expected2}");
        }

        // 3. SHA-256 Test Vector: "abc"
        var shaInput = Encoding.ASCII.GetBytes("abc");
        var shaActual = ToHex(OfdDigestAlgorithms.Compute("sha256", shaInput));
        var shaExpected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        if (!string.Equals(shaActual, shaExpected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SHA-256 Vector mismatch: got {shaActual}, expected {shaExpected}");
        }

        return TaskCompat.CompletedTask;
    }

    private static async Task TestDigitalSignatureCreationAndVerification()
    {
        var package = CreateSamplePackage("Signed Document Page");

        using var ofdStream = new MemoryStream();
        await new OfdPackageWriter().WriteAsync(package, ofdStream).ConfigureAwait(false);

        ofdStream.Position = 0;
        using var signedStream = new MemoryStream();

        var provider = new TestSignatureProvider();
        var signer = new OfdSignatureService();
        var signOptions = new OfdSignatureOptions
        {
            CheckMethod = "sm3",
            ProtectSignatureList = true
        };

        await signer.SignAsync(ofdStream, signedStream, provider, signOptions).ConfigureAwait(false);

        if (signedStream.Length == 0)
        {
            throw new InvalidOperationException("Signing produced empty stream.");
        }

        // Verify signed package
        signedStream.Position = 0;
        var verifier = new OfdSignatureVerifier();
        var report = await verifier.VerifyAsync(signedStream).ConfigureAwait(false);

        if (!report.HasSignatures)
        {
            throw new InvalidOperationException("Verification report found no signatures.");
        }

        if (!report.ReferenceIntegrityValid)
        {
            var failedRefs = string.Join("; ", report.Signatures
                .SelectMany(s => s.References)
                .Where(r => !r.DigestMatches)
                .Select(r => $"{r.FileReference}: match={r.DigestMatches}"));
            throw new InvalidOperationException($"Signature reference integrity check failed: {failedRefs}");
        }
    }

    private static async Task TestNonSeekableStreamReading()
    {
        var package = CreateSamplePackage("Non-seekable stream content");

        using var ms = new MemoryStream();
        await new OfdPackageWriter().WriteAsync(package, ms).ConfigureAwait(false);

        var bytes = ms.ToArray();
        using var nonSeekable = new NonSeekableStream(bytes);

        var reader = new OfdReader();
        var readPackage = await reader.ReadAsync(nonSeekable).ConfigureAwait(false);

        if (readPackage.Pages.Count != 1)
        {
            throw new InvalidOperationException($"Non-seekable read failed to load page correctly, count={readPackage.Pages.Count}");
        }

        var text = new OfdTextExtractor().Extract(readPackage);
        if (!text.Contains("Non-seekable stream content"))
        {
            throw new InvalidOperationException("Non-seekable read corrupted text content.");
        }
    }

    private static async Task TestPathTraversalRejection()
    {
        using var evilMs = new MemoryStream();
        using (var zip = new ZipFile())
        {
            zip.AddEntry("../evil.xml", Encoding.UTF8.GetBytes("<Evil/>"));
            zip.AddEntry("OFD.xml", Encoding.UTF8.GetBytes("<OFD/>"));
            zip.Save(evilMs);
        }

        evilMs.Position = 0;
        var loader = new OfdPackageLoader();

        try
        {
            await loader.LoadAsync(evilMs).ConfigureAwait(false);
            throw new InvalidOperationException("Loader unexpectedly accepted a ZIP containing '../evil.xml' path traversal!");
        }
        catch (InvalidDataException)
        {
            // Expected security rejection!
        }
    }

    private static async Task TestPackageStructureValidation()
    {
        var package = CreateSamplePackage("Structure Check Page");

        using var ms = new MemoryStream();
        await new OfdPackageWriter().WriteAsync(package, ms).ConfigureAwait(false);

        ms.Position = 0;
        var archive = await new OfdPackageLoader().LoadAsync(ms).ConfigureAwait(false);
        var issues = OfdPackageStructureChecker.Check(archive);

        if (issues.Any(x => x.IsError))
        {
            var errors = string.Join(", ", issues.Where(x => x.IsError).Select(x => x.Message));
            throw new InvalidOperationException($"Structure check failed for valid package: {errors}");
        }
    }

    private static OfdDocumentPackage CreateSamplePackage(string text)
    {
        var builder = new OfdDocumentBuilder();
        builder.AddPage(new OfdPage
        {
            Index = 0,
            WidthMillimeters = 210,
            HeightMillimeters = 297,
            Elements =
            {
                new OfdTextElement
                {
                    Text = text,
                    FontName = "SimSun",
                    FontSizeMillimeters = 4,
                    XMillimeters = 10,
                    YMillimeters = 12,
                    WidthMillimeters = 80,
                    HeightMillimeters = 10
                },
                new OfdPathElement
                {
                    AbbreviatedData = "M 0 0 L 100 0 L 100 100 L 0 100 Z",
                    Stroke = true,
                    LineWidthMillimeters = 0.5,
                    XMillimeters = 10,
                    YMillimeters = 30
                }
            }
        });
        return builder.Build();
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    private sealed class TestSignatureProvider : IOfdSignatureProvider
    {
        public string ProviderName => "Net40TestSigner";
        public string Company => "Ofdrw.Net";
        public string Version => "1.0";
        public string SignatureMethod => "1.2.156.10197.1.501";
        public string SignatureType => "Seal";
        public byte[]? SealData => null;

        public Task<byte[]> SignAsync(byte[] signatureXml, string propertyInformation, CancellationToken cancellationToken = default)
        {
            // Simple mock signature value
            var dummySignature = new byte[64];
            for (var i = 0; i < dummySignature.Length; i++)
            {
                dummySignature[i] = (byte)(i & 0xFF);
            }
            return TaskCompat.FromResult(dummySignature);
        }
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly byte[] _data;
        private int _position;

        public NonSeekableStream(byte[] data)
        {
            _data = data;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _data.Length) return 0;
            var remaining = _data.Length - _position;
            var toRead = Math.Min(remaining, count);
            Buffer.BlockCopy(_data, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
    }
}
