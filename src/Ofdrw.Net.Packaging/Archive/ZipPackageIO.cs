using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#if !NET40
using System.IO.Compression;
#endif

namespace Ofdrw.Net.Packaging.Archive;

internal static class ZipPackageIO
{
    public static async Task WriteZipAsync(
        Stream destination,
        IEnumerable<KeyValuePair<string, byte[]>> entries,
        bool enableCompression,
        CancellationToken cancellationToken)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        if (entries is null) throw new ArgumentNullException(nameof(entries));

        cancellationToken.ThrowIfCancellationRequested();

#if NET40
        using (var zip = new Ionic.Zip.ZipFile())
        {
            zip.CompressionLevel = enableCompression
                ? Ionic.Zlib.CompressionLevel.BestCompression
                : Ionic.Zlib.CompressionLevel.None;

            foreach (var entry in entries.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                zip.AddEntry(entry.Key, entry.Value);
            }

            zip.Save(destination);
        }
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
        using (var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var zipEntry = zip.CreateEntry(
                    entry.Key,
                    enableCompression ? CompressionLevel.Optimal : CompressionLevel.NoCompression);
                using var stream = zipEntry.Open();
                await stream.WriteAsync(entry.Value, 0, entry.Value.Length, cancellationToken).ConfigureAwait(false);
            }
        }
#endif
    }
}
