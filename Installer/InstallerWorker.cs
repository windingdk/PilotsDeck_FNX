﻿using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Installer
{
    public class InstallerWorker
    {
        private Queue<string> messageList = null;

        public bool IsRunning { get; private set; } = false;
        public bool HasError { get; private set; } = false;

        public bool CfgDesktopLink { get; set; } = false;
        public AutoStart CfgAutoStart { get; set; } = AutoStart.NONE;
        public ConfigFile CfgConfigFile { get; set; } = ConfigFile.NOCHANGE;

        public InstallerWorker(Queue<string> messageList)
        {
            this.messageList = messageList;
        }

        public void Run()
        {
            IsRunning = true;

            messageList.Enqueue("Checking FSUIPC Version/State ...");
            if (!InstallerFunctions.CheckFSUIPC())
                messageList.Enqueue($"WARNING: FSUIPC not installed our outdated! Minimum Version for Offset Modes: {Parameters.ipcVersion}");

            if (!HasError)
                InstallDotNet();
            if (!HasError)
                InstallWasm();
            if (!HasError)
                InstallApp();
            if (!HasError)
                SetupAutoStart();
            
            messageList.Enqueue("\nDone.");
            if (!HasError)
                messageList.Enqueue($"FenixQuartz was installed to {Parameters.appDir}");
            IsRunning = false;
        }

        protected void InstallDotNet()
        {
            messageList.Enqueue("\nChecking .NET 7 Desktop Runtime ...");

            if (InstallerFunctions.CheckDotNet())
                messageList.Enqueue("Runtime already installed!");
            else
            {
                messageList.Enqueue("Runtime not installed or outdated!");
                messageList.Enqueue("Downloading .NET Desktop Runtime ...");
                if (!InstallerFunctions.DownloadFile(Parameters.netUrl, Parameters.netUrlFile))
                {
                    HasError = true;
                    messageList.Enqueue("Could not download .NET Runtime!");
                    return;
                }
                messageList.Enqueue("Installing .NET Desktop Runtime ...");
                InstallerFunctions.RunCommand($"{Parameters.netUrlFile} /install /quiet /norestart");
                File.Delete(Parameters.netUrlFile);
            }
        }

        protected void InstallWasm()
        {
            messageList.Enqueue("\nChecking MobiFlight WASM/Event Module ...");

            if (!InstallerFunctions.CheckInstalledMSFS(out string packagePath))
            {
                HasError = true;
                messageList.Enqueue("Could not determine Package Path!");
                return;
            }


            if (InstallerFunctions.CheckPackageVersion(packagePath, Parameters.wasmMobiName, Parameters.wasmMobiVersion))
            {
                messageList.Enqueue("Module already installed!");
            }
            else
            {
                if (!InstallerFunctions.GetProcessRunning("FlightSimulator"))
                {
                    messageList.Enqueue("Module not installed or outdated!");
                    if (Directory.Exists(packagePath + @"\" + Parameters.wasmMobiName))
                    {
                        messageList.Enqueue("Deleting old Version ...");
                        Directory.Delete(packagePath + @"\" + Parameters.wasmMobiName, true);
                    }
                    messageList.Enqueue("Downloading MobiFlight Module ...");
                    if (!InstallerFunctions.DownloadFile(Parameters.wasmUrl, Parameters.wasmUrlFile))
                    {
                        HasError = true;
                        messageList.Enqueue("Could not download MobiFlight Module!");
                        return;
                    }
                    messageList.Enqueue("Extracting new Version ...");
                    if (!InstallerFunctions.ExtractZip(packagePath, Parameters.wasmUrlFile))
                    {
                        HasError = true;
                        messageList.Enqueue("Error while extracting MobiFlight Module!");
                        return;
                    }
                    File.Delete(Parameters.wasmUrlFile);
                }
                else
                {
                    HasError = true;
                    messageList.Enqueue("Can not install/update Module while MSFS is running.");
                    MessageBox.Show("Please stop MSFS and try again.", "MSFS is running!", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        protected void InstallApp()
        {
            messageList.Enqueue("\nChecking Application State ...");

            if (!Directory.Exists(Parameters.appDir))
            {
                messageList.Enqueue("Installing FenixQuartz ...");
                messageList.Enqueue("Extracting Application ...");
                if (!InstallerFunctions.ExtractZip())
                {
                    HasError = true;
                    messageList.Enqueue("Error while extracting Application!");
                    return;
                }
            }
            else
            {
                messageList.Enqueue("Deleting old Version ...");
                if (Directory.Exists(Parameters.binDir))
                    Directory.Delete(Parameters.binDir, true);
                Directory.CreateDirectory(Parameters.binDir);
                messageList.Enqueue("Extracting new Version ...");
                if (!InstallerFunctions.ExtractZip())
                {
                    HasError = true;
                    messageList.Enqueue("Error while extracting Application!");
                    return;
                }
            }

            string confFile = "Installer.FenixQuartz.config.string";
            if (CfgConfigFile == ConfigFile.NUMOFFSET)
                confFile = "Installer.FenixQuartz.config.numoffset";
            else if (CfgConfigFile == ConfigFile.NUMLVAR)
                confFile = "Installer.FenixQuartz.config.numlvar";

            if (!File.Exists(Parameters.confFile) || CfgConfigFile > 0)
            {
                messageList.Enqueue($"Creating Config File ({CfgConfigFile.ToString()}) ...");
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(confFile))
                {
                    using (var file = new FileStream(Parameters.confFile, FileMode.Create, FileAccess.Write))
                    {
                        resource.CopyTo(file);
                    }
                }
            }

            if (CfgDesktopLink)
            {
                messageList.Enqueue("Placing Shortcut ...");
                InstallerFunctions.PlaceDesktopLink();
            }
        }

        protected void SetupAutoStart()
        {
            if (CfgAutoStart == AutoStart.NONE)
                return;

            if (CfgAutoStart == AutoStart.FSUIPC)
            {
                messageList.Enqueue("Setup FSUIPC Auto-Start ...");
                if (InstallerFunctions.AutoStartFsuipc())
                    messageList.Enqueue("Auto-Start added successfully!");
                else
                {
                    messageList.Enqueue("Failed to add Auto-Start!");
                    HasError = true;
                }
            }

            if (CfgAutoStart == AutoStart.EXE)
            {
                messageList.Enqueue("Setup EXE.xml Auto-Start ...");
                if (InstallerFunctions.AutoStartExe())
                    messageList.Enqueue("Auto-Start added successfully!");
                else
                {
                    messageList.Enqueue("Failed to add Auto-Start!");
                    HasError = true;
                }
            }
        }
    }
}
