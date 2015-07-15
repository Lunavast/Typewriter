﻿using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Typewriter.Generation.Controllers;

namespace Typewriter.VisualStudio
{
    [Guid(Constants.ExtensionPackageId)]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideLanguageService(typeof(LanguageService), Constants.LanguageName, 100, DefaultToInsertSpaces = true)]
    [ProvideLanguageExtension(typeof(LanguageService), Constants.Extension)]
    public sealed class ExtensionPackage : Package, IDisposable
    {
        private DTE dte;
        private Log log;
        private IVsStatusbar statusBar;
        private SolutionMonitor solutionMonitor;
        private TemplateController templateController;
        private EventQueue eventQueue;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            GetDte();
            GetStatusbar();
            RegisterLanguageService();
            RegisterIcons();
            ClearTempDirectory();
            
            this.eventQueue = new EventQueue(statusBar);
            this.solutionMonitor = new SolutionMonitor();
            this.templateController = new TemplateController(dte, solutionMonitor, eventQueue);
            var generationController = new GenerationController(this, dte, solutionMonitor, templateController, eventQueue);
        }

        private void GetDte()
        {
            this.dte = GetService(typeof(DTE)) as DTE;
            this.log = new Log(dte);

            if (this.dte == null)
                ErrorHandler.ThrowOnFailure(1);
        }
        
        private void GetStatusbar()
        {
            this.statusBar = GetService(typeof(SVsStatusbar)) as IVsStatusbar;

            if (this.statusBar == null)
                ErrorHandler.ThrowOnFailure(1);
        }

        public void Debug(string message)
        {
            Log.Debug(message);
        }

        public T GetWorkspace<T>() where T : class
        {
            try
            {
                var componentModel = GetService(typeof (SComponentModel)) as IComponentModel;
                return componentModel?.GetService<T>();
            }
            catch(Exception exception)
            {
                Log.Debug(exception.Message);
            }

            return default(T);
        }

        private void RegisterLanguageService()
        {
            var languageService = new LanguageService();
            ((IServiceContainer)this).AddService(typeof(LanguageService), languageService, true);
        }

        private static void RegisterIcons()
        {
            try
            {
                var icon = ThemeInfo.IsDark ? "dark.ico" : "light.ico";
                var directory = Path.Combine(Path.GetDirectoryName(typeof(ExtensionPackage).Assembly.Location), "Resources");
                var path = Path.Combine(directory, icon);

                using (RegistryKey classes = Registry.CurrentUser.OpenSubKey("SoftWare\\Classes", true))
                {
                    if (classes == null) return;

                    using (var key = classes.CreateSubKey(Constants.Extension + "\\DefaultIcon"))
                    {
                        if (key != null) key.SetValue(string.Empty, path);
                    }
                }
            }
            catch
            {
            }
        }

        private static void ClearTempDirectory()
        {
            try
            {
                if (Directory.Exists(Constants.TempDirectory))
                {
                    Directory.Delete(Constants.TempDirectory, true);
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            if (this.eventQueue != null)
            {
                this.eventQueue.Dispose();
                this.eventQueue = null;
            }
        }
    }
}