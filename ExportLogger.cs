using System;
using System.IO;
using System.Text;

namespace apam_clash.Export
{
    internal sealed class ExportLogger
    {
        private readonly string logPath;
        private readonly object syncRoot = new object();

        public ExportLogger(string logPath)
        {
            if (string.IsNullOrWhiteSpace(logPath))
            {
                throw new ArgumentException("A log file path is required.", nameof(logPath));
            }

            this.logPath = Path.GetFullPath(logPath);
            string directory = Path.GetDirectoryName(this.logPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Error(string message, Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            Write("ERROR", message, exception);
        }

        private void Write(string level, string message, Exception exception)
        {
            var entry = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [")
                .Append(level)
                .Append("] ")
                .AppendLine(message);

            if (exception != null)
            {
                entry.AppendLine(exception.ToString());
            }

            lock (syncRoot)
            {
                File.AppendAllText(logPath, entry.ToString());
            }
        }
    }
}
