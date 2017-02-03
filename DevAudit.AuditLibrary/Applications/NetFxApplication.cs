﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alpheus;
using Mono.Collections.Generic;
using Mono.Cecil;
using System.Threading;

namespace DevAudit.AuditLibrary
{
    public class NetFx4Application : Application
    {
        #region Constructors
         public NetFx4Application(Dictionary<string, object> application_options, Dictionary<string, string[]> required_files,
            Dictionary<string, string[]> required_directories, string analyzer_type, EventHandler<EnvironmentEventArgs> message_handler) : base(application_options,
                required_files, required_directories, analyzer_type, message_handler)
        {
            if (!this.SkipPackagesAudit)
            {
                if (application_options.ContainsKey("PackageSource"))
                {
                    this.NugetPackageSource = new NuGetPackageSource(new Dictionary<string, object>(1) { { "File", this.CombinePath((string)application_options["PackageSource"]) } },
                        message_handler);
                    this.PackageSourceInitialized = true;
                    this.AuditEnvironment.Info("Using NuGet v2 package manager configuration file {0}", (string)application_options["PackageSource"]);
                }
                else
                {
                    AuditFileInfo packages_config = this.AuditEnvironment.ConstructFile(this.CombinePath("@packages.config"));
                    if (packages_config.Exists)
                    {
                        this.NugetPackageSource = new NuGetPackageSource(new Dictionary<string, object>(1) { { "File", packages_config.FullName } }, message_handler);
                        this.PackageSourceInitialized = true;
                        this.AuditEnvironment.Info("Using NuGet v2 package manager configuration file {0}", packages_config.FullName);
                    }

                    else
                    {
                        this.AuditEnvironment.Warning("The default NuGet v2 package manager configuration file {0} does not exist and no PackageSource parameter was specified.", packages_config.FullName);
                        PackageSourceInitialized = false;
                    }
                }
            }
            AuditFileInfo cf;
            if (this.ApplicationFileSystemMap.ContainsKey("AppConfig"))
            {
                this.AppConfig = this.ApplicationFileSystemMap["AppConfig"] as AuditFileInfo;
                this.AuditEnvironment.Info("Using {0} configuration file {1}.", this.ApplicationLabel, this.AppConfig.FullName);
            }
            else if (application_options.ContainsKey("AppConfig"))
            {
                cf = this.AuditEnvironment.ConstructFile(this.CombinePath((string)application_options["AppConfig"]));
                if (cf.Exists)
                {
                    this.AppConfig = cf;
                    this.AuditEnvironment.Info("Using {0} configuration file {1}.", this.ApplicationLabel, this.AppConfig.FullName);
                }
                else throw new ArgumentException(string.Format("The configuration file {0} does not exist.", cf.FullName), "application_options");
            }
            else if (application_options.ContainsKey("ConfigurationFile"))
            {
                cf = this.AuditEnvironment.ConstructFile(this.CombinePath((string)application_options["ConfigurationFile"]));
                if (cf.Exists)
                {
                    this.AppConfig = cf;
                    this.AuditEnvironment.Info("Using {0} configuration file {1}.", this.ApplicationLabel, this.AppConfig.FullName);
                }
                else throw new ArgumentException(string.Format("The configuration file {0} does not exist.", cf.FullName), "application_options");

            }
            else if(this.AuditEnvironment.FileExists(this.CombinePath("@App.config")))
            {
                cf = this.AuditEnvironment.ConstructFile(this.CombinePath(("@App.config")));
                this.AppConfig = cf;
                this.AuditEnvironment.Info("Using {0} configuration file {1}.", this.ApplicationLabel, this.AppConfig.FullName);
            }
            else if (this.ApplicationBinary != null)
            {
                cf = this.AuditEnvironment.ConstructFile(this.ApplicationBinary.FullName + ".config");
                if (!cf.Exists)
                {
                    this.AuditEnvironment.Warning("The default .NET application configuration file {0} does not exist and no AppConfig parameter was specified.", cf.FullName);
                }
                else
                {
                    this.AppConfig = cf;
                    this.AuditEnvironment.Info("Using {0} configuration file {1}.", this.ApplicationLabel, this.AppConfig.FullName);
                }
            }
            else
            {
                this.AuditEnvironment.Warning("The default .NET application configuration file could not be determined and no AppConfig parameter was specified.");
            }

        }

        public NetFx4Application(Dictionary<string, object> application_options, Dictionary<string, string[]> required_files,
            Dictionary<string, string[]> required_directories, string analyzer_type, EventHandler<EnvironmentEventArgs> message_handler, NuGetPackageSource package_source) : 
            this(application_options, required_files, required_directories, analyzer_type, message_handler)
        {
            if (package_source != null)
            {
                this.NugetPackageSource = package_source;
                this.PackageSourceInitialized = true;
            }
            else throw new ArgumentException("Package source is null.", "package_source");
        }
        
        public NetFx4Application(Dictionary<string, object> application_options, EventHandler<EnvironmentEventArgs> message_handler) : 
            this(application_options, new Dictionary<string, string[]> ()/*{ { "AppConfig", new string[] { "@", "App.config" } } }*/, new Dictionary<string, string[]>(), "NetFx", message_handler)
        { }
        #endregion

        #region Overriden properties
        public override string ApplicationId { get; } = "netfx4";

        public override string ApplicationLabel { get; } = ".NET Framework 4";

        public override string PackageManagerId => this.NugetPackageSource?.PackageManagerId;

        public override string PackageManagerLabel => this.NugetPackageSource?.PackageManagerLabel;

        public override PackageSource PackageSource => this as PackageSource;
        #endregion

        #region Overriden methods
        public override Task GetPackagesTask(CancellationToken ct)
        {
            CallerInformation caller = this.AuditEnvironment.Here();
            if (!this.PackageSourceInitialized)
            {
                //this.PackagesTask = Task.Run(() => this.Packages = this.ModulePackages["references"]);
                //this.AuditEnvironment.Warning("No NuGet v2 package manager configuration file specified, using assembly references as packages.");
                //this.PackageSourceInitialized = true;

            }
            else
            {
                this.AuditEnvironment.Status("Scanning {0} packages.", this.PackageManagerLabel);
                this.NugetPackageSource.GetPackagesTask(ct);
                this.PackagesTask = this.NugetPackageSource.GetPackagesTask(ct).ContinueWith((t) => this.Packages = this.NugetPackageSource.Packages, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            return this.PackagesTask;
        }

        public override IEnumerable<OSSIndexQueryObject> GetPackages(params string[] o)
        {
            if (this.PackageSourceInitialized)
            {
                return this.Packages = this.NugetPackageSource.GetPackages(o);
            }
            else
            {
                throw new InvalidOperationException("The package source is not intialized.");
            }
        }

        public override bool IsVulnerabilityVersionInPackageVersionRange(string vulnerability_version, string package_version)
        {
            if (this.PackageSourceInitialized)
            {
                return this.NugetPackageSource.IsVulnerabilityVersionInPackageVersionRange(vulnerability_version, package_version);
            }
            else throw new InvalidOperationException("The package source is not intialized.");
        }

        public override bool IsConfigurationRuleVersionInServerVersionRange(string configuration_rule_version, string server_version)
        {
            return configuration_rule_version == server_version;
        }

        protected override string GetVersion()
        {
            if (this.ModulesInitialised)
            {
                this.Version = this.AppAssemblyDefinition.Name.Version.ToString();
                this.VersionInitialised = true;
                this.AuditEnvironment.Success("Got {0} application version {1}.", this.ApplicationLabel, this.Version);

                return this.Version;
            }
            else return string.Empty;
        }

        protected override Dictionary<string, IEnumerable<OSSIndexQueryObject>> GetModules()
        {
            Stopwatch sw = new Stopwatch();
            if (this.ApplicationBinary == null)
            {
                this.AuditEnvironment.Warning("The .NET assembly application binary was not specified so application modules and version cannot be detected.");
                this.ModulesInitialised = false;
                return null;
            }
            sw.Start();
            LocalAuditFileInfo ab = this.ApplicationBinary.GetAsLocalFile();
            DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(ab.Directory.FullName);
            ReaderParameters reader = new ReaderParameters()
            {
                AssemblyResolver = resolver

            };
            this.AppModuleDefinition = ModuleDefinition.ReadModule(ab.FullName, reader);
            this.AppAssemblyDefinition = AssemblyDefinition.ReadAssembly(ab.FullName, reader);
            this.Modules = this.AppModuleDefinition;
            List<AssemblyNameReference> references = this.AppModuleDefinition.AssemblyReferences.ToList();
            
            references.RemoveAll(r => r.Name =="mscorlib" || r.Name.StartsWith("System"));
            sw.Stop();
            this.AuditEnvironment.Info("Got {0} referenced assemblies in {1} ms", references.Count(), sw.ElapsedMilliseconds);
            IEnumerable<OSSIndexQueryObject> modules = references.Select(r => new OSSIndexQueryObject("netfx", r.Name, r.Version.ToString()));
            this.ModulePackages = new Dictionary<string, IEnumerable<OSSIndexQueryObject>>
            {
                {"references", modules }
            };

            this.ModulesInitialised = true;
            return this.ModulePackages;
        }

        protected override IConfiguration GetConfiguration()
        {
            if (this.AppConfig == null) return null;//throw new InvalidOperationException("The application configuration file was not specified.");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            XMLConfig config = new XMLConfig(this.AppConfig);
            if (config.ParseSucceded)
            {
                this.Configuration = config;
                this.ConfigurationInitialised = true;
                sw.Stop();                
                this.AuditEnvironment.Success("Read configuration from {0} in {1} ms.", this.Configuration.File.Name, sw.ElapsedMilliseconds);
            }
            else
            {
                sw.Stop();
                this.Configuration = null;
                this.ConfigurationInitialised = false;                
                this.AuditEnvironment.Error("Failed to read configuration from {0}. Error: {1}. Time elapsed: {2} ms.",
                    this.AppConfig, config.LastException.Message, sw.ElapsedMilliseconds);
            }
            return this.Configuration;
        }

        private void GetAssemblyReferences(ref List<AssemblyNameReference> references, ReaderParameters reader)
        {
            List<string> names = references.Select(r => r.FullName).ToList();
            foreach (AssemblyNameReference r in references)
            {
                try
                {
                    AssemblyDefinition ad = AssemblyDefinition.ReadAssembly(r.Name, reader);
                }
                catch (Exception e)
                {
                    this.AuditEnvironment.Warning("Error attempting to load assembly reference {0}:{1}.", r.Name, e.Message);
                    continue;
                }
                

            }
        }

        #endregion

        #region Properties
        public NuGetPackageSource NugetPackageSource { get; protected set; }

        public AuditFileInfo AppConfig { get; protected set; }

        public ModuleDefinition AppModuleDefinition { get; protected set; }

        public AssemblyDefinition AppAssemblyDefinition { get; protected set; }
        #endregion
    }
}
