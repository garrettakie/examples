﻿using System;
using System.Reflection;
using System.Threading;
using log4net;
using SondeFileImporter.Config;
using SondeFileImporter.FileManagement;
using SondeFileImporter.Importer;

namespace SondeFileImporter
{
    public class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Mutex InstanceMutex = new Mutex(true, "{F876AC5B-FFE5-43B9-86F4-DA380231699B}");

        static Program()
        {
            log4net.Config.XmlConfigurator.Configure();
        }

        static void Main()
        {
            Log.Info("Program started.");

            if (AnotherInstanceIsRunning())
            {
                Log.Debug("Another instance of the program is still running. Do nothing."); 
                EndProgram(release:false);

                return;
            }

            try
            {
                Environment.ExitCode = 1;

                var context = GetValidatedContext();

                ArchiveSuccessFilesNoThrow(context);

                var importer = new Importer.SondeFileImporter(context);
                importer.Synchronize();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                EndProgram(release:true);
            }
        }

        private static void ArchiveSuccessFilesNoThrow(Context context)
        {
            try
            {
                var fileMan = new SondeFileManager(context);
                fileMan.ArchiveSuccessFiles();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to archive files: {e.Message}");
            }
        }

        private static Context GetValidatedContext()
        {
            var context = ConfigLoader.FromConfigFile();
            context.Validate();

            return context;
        }

        private static bool AnotherInstanceIsRunning()
        {
            try
            {
                return !InstanceMutex.WaitOne(TimeSpan.Zero, true);
            }
            catch (AbandonedMutexException)
            {
                Log.Debug("Previous run of the program did not clear the mutex cleanly.");
                return false;
            }
            catch(Exception ex)
            {
                Log.Warn($"Error occurred while checking if the program is still running:'{ex.Message}'. Will continue.");
            }

            return false;
        }

        private static void EndProgram(bool release)
        {
            try
            {
                if (release)
                {
                    InstanceMutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Error releasing the mutex: '{ex.Message}");
            }
            finally
            {
                InstanceMutex.Dispose();
            }

            Environment.ExitCode = 0;
            Log.Info("Program finished.");
        }
    }
}
