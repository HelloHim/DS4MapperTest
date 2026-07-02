using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DS4MapperTest
{
    public static class AtomicFileWriter
    {
        public static void WriteText(string path, string contents)
        {
            string dirPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            string tempPath = Path.Combine(
                string.IsNullOrEmpty(dirPath) ? "." : dirPath,
                $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (FileStream fs = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    writer.Write(contents);
                    writer.Flush();
                    fs.Flush(true);
                }

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        public static void WriteJson(string path, JToken token)
        {
            using (StringWriter stringWriter = new StringWriter())
            using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;
                jsonWriter.Indentation = 2;
                token.WriteTo(jsonWriter);
                jsonWriter.Flush();
                WriteText(path, stringWriter.ToString());
            }
        }
    }
}
