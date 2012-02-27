using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Globalization;
using System.Diagnostics;
using NuGet;

namespace NuGetGallery.Services
{
    public class CommandLineExeExtractor : ICommandLineExtractorService
    {
        private const string NuGetCommandLinePackage = "NuGet.CommandLine";
        private static readonly object fetchLock = new object();
        private static readonly byte[] exeContent;
        private readonly IPackageService packageSvc;
        private readonly IFileStorageService fileStorageSvc;

        public CommandLineExeExtractor(IPackageService packageSvc, IFileStorageService fileStorageSvc)
        {
            this.packageSvc = packageSvc;
            this.fileStorageSvc = fileStorageSvc;
        }

        public byte[] ExtractExecutable()
        {
            if (exeContent == null)
            {
                lock (fetchLock)
                {
                    if (exeContent == null)
                    {
                        var package = packageSvc.FindPackageByIdAndVersion(NuGetCommandLinePackage, version: null, allowPrerelease: false);
                        Debug.Assert(package != null);
                        var fileName = String.Format(CultureInfo.InvariantCulture, Constants.PackageFileSavePathTemplate, NuGetCommandLinePackage, package.Version, 
                            Constants.NuGetPackageFileExtension);

                        using (var stream = fileStorageSvc.GetFile(Constants.PackagesFolderName, fileName))
                        {
                            var nugetPackage = new ZipPackage(stream);
                        }
                    }
                }
            }
            return null;
            
        }
    }
}