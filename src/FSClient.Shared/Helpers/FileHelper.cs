namespace FSClient.Shared.Helpers
{
    using System.Linq;

    public static class FileHelper
    {
        public static string GetUniqueFileName(string fileName, string[] files)
        {
            string fileBase, ext;
            var extPoint = fileName.LastIndexOf(".", System.StringComparison.Ordinal);
            if (extPoint > 0)
            {
                fileBase = fileName[..extPoint];
                ext = fileName[extPoint..];
            }
            else
            {
                fileBase = fileName;
                ext = string.Empty;
            }

            for (var index = 0; index < 1024; index++)
            {
                var name = (index == 0)
                    ? fileName
                    : $"{fileBase} ({index}){ext}";

                if (files.Contains(name))
                {
                    continue;
                }

                return name;
            }
            return fileName;
        }
    }
}
