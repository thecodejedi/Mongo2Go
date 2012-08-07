﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Mongo2Go.Helper
{
    public class MongoDbProcess : IMongoDbProcess
    {
        private const string ProcessReadyIdentifier = "waiting for connections";
        private Process _process;

        public bool Disposed { get; private set; }

        internal MongoDbProcess(Process process)
        {
            _process = process;
        }

        public IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port)
        {
            string fileName  = string.Format(CultureInfo.InvariantCulture, @"{0}\{1}", binariesDirectory, MongoDbDefaults.ExecutableName);
            string arguments = string.Format(CultureInfo.InvariantCulture, @"--dbpath ""{0}"" --port {1} --nohttpinterface --nojournal", dataDirectory, port);

            ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    //CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };

            Process process = new Process { StartInfo = startInfo };

            StartAndBlockUntilReady(process, 5);

            return new MongoDbProcess(process);
        }

        /// <summary>
        /// Reads from Output stream to determine if MongoDB is ready
        /// </summary>
        private static void StartAndBlockUntilReady(Process process, int timeoutInSeconds)
        {
            bool processReady = false;
            process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data) &&
                        args.Data.Contains(ProcessReadyIdentifier))
                    {
                        processReady = true;
                    }
                };

            process.Start();
            process.BeginOutputReadLine();

            int lastResortCounter = 0;
            int timeOut = timeoutInSeconds*10;
            while (!processReady)
            {
                Thread.Sleep(100);
                if (++lastResortCounter > timeOut)
                {
                    // we waited X seconds.
                    // for any reason the detection did not worked, eg. the identifier changed
                    // lets assume everything is still ok
                    break;
                }
            }

            process.CancelOutputRead();
        }

        #region IDisposable and friends

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Disposed) { return; }
            if (disposing)
            {
                // we have no "managed resources" - but we leave this switch to avoid an FxCop CA1801 warnig
            }
            Kill();

            // the c# driver takes care of the connections - and totally fails if you kill the database, this is a temporarely workaround
            Thread.Sleep(1000);

            Disposed = true;
        }

        ~MongoDbProcess()
        {
            Dispose(false);
        }

        private void Kill()
        {
            if (_process == null)
            {
                return;
            }

            if (!_process.HasExited)
            {
                _process.Kill();
            }

            _process.Dispose();
            _process = null;
        }

        #endregion
    }
}
