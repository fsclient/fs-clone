namespace FSClient.Shared.Test
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TestFilesHelper
    {
        private static readonly string LocalFolder = Environment.CurrentDirectory;
        private static readonly string TestFilesFolder = Path.Combine(LocalFolder, "./TestFiles");

        public static Task<string> ReadAsTextAsync(string path, CancellationToken cancellationToken = default)
        {
            return File.ReadAllTextAsync(Path.Combine(TestFilesFolder, path), cancellationToken);
        }

        public static async Task<TType?> ReadAsJsonAsync<TType>(string path, CancellationToken cancellationToken = default)
        {
            using (var file = File.Open(Path.Combine(TestFilesFolder, path), FileMode.Open))
            {
                return await JsonSerializer.DeserializeAsync<TType>(file, cancellationToken: cancellationToken);
            }
        }
    }
}
